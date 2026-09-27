// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, fgprofile.cpp.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.Compiler;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public sealed unsafe class BlockCountInstrumentor(Compiler compiler) : Instrumentor(compiler)
{
    private BasicBlock? _entryBlock;

    public override bool ShouldProcess(BasicBlock block)
        => block.HasFlag(BBF_IMPORTED) && !block.HasFlag(BBF_INTERNAL);

    public override void Prepare(bool preImport)
    {
        if (preImport)
        {
            return;
        }

        RelocateProbes();

#if DEBUG
        foreach (var block in Compiler.Blocks)
        {
            block.bbCountSchemaIndex = -1;
        }
#endif
    }

    private void RelocateProbes()
    {
        if (!Compiler.opts.IsInstrumentedAndOptimized ||
            (Compiler.optMethodFlags & OMF_HAS_TAILCALL_SUCCESSOR) == 0)
        {
            return;
        }

        JITDUMP("Optimized + instrumented + potential tail calls --- preparing to relocate edge probes\n");
        var criticalPreds = new Stack<BasicBlock>();

        foreach (var block in Compiler.Blocks)
        {
            if (!ShouldProcess(block) || !block.HasFlag(BBF_TAILCALL_SUCCESSOR))
            {
                continue;
            }

            JITDUMP($"Return {FMT_BB(block.bbNum)} is successor of possible tail call\n");
            assert(block.Kind is BBJ_RETURN);
            criticalPreds.Clear();

            foreach (var pred in block.PredBlocks)
            {
                if (!ShouldProcess(pred))
                {
                    JITDUMP($"{FMT_BB(pred.bbNum)} -> {FMT_BB(block.bbNum)} is dead edge\n");
                    continue;
                }

                var succ = pred.UniqueSucc;
                if ((succ is null) || pred.isBBCallFinallyPairTail)
                {
                    JITDUMP($"{FMT_BB(pred.bbNum)} -> {FMT_BB(block.bbNum)} is critical edge\n");
                    criticalPreds.Push(pred);
                }
                else
                {
                    assert(pred.Kind is BBJ_ALWAYS);
                }
            }

            if (criticalPreds.Count > 0)
            {
                var intermediary = Compiler.fgNewBBbefore(BBJ_ALWAYS, block, extendRegion: true);
                intermediary.SetFlags(BBF_IMPORTED | BBF_MARKED);
                intermediary.inheritWeight(block);
                var newEdge = Compiler.fgAddRefPred(block, intermediary);
                intermediary.TargetEdge = newEdge;
                SetModifiedFlow();

                while (criticalPreds.Count > 0)
                {
                    Compiler.fgReplaceJumpTarget(criticalPreds.Pop(), block, intermediary);
                }
            }
        }
    }

    public override void BuildSchemaElements(BasicBlock block, List<ICorJitInfo.PgoInstrumentationSchema> schema)
    {
        var count = CounterSlots;
        assert(block.bbCountSchemaIndex == -1);
        block.bbCountSchemaIndex = schema.Count;
        assert(block.bbCodeOffs >= 0);

        schema.Add(new ICorJitInfo.PgoInstrumentationSchema {
            Count = count,
            Other = 0,
            InstrumentationKind = Compiler.opts.compCollect64BitCounts
                ? ICorJitInfo.PgoInstrumentationKind.BasicBlockLongCount
                : ICorJitInfo.PgoInstrumentationKind.BasicBlockIntCount,
            ILOffset = block.bbCodeOffs,
            Offset = 0,
        });
        SchemaCountValue++;

        if (block.bbCodeOffs == 0)
        {
            assert(_entryBlock is null);
            _entryBlock = block;
        }
    }

    internal static int CounterSlots
    {
        get
        {
            if ((JitConfig.JitScalableProfiling > 0) && (JitConfig.JitInterlockedProfiling > 0))
            {
                return 2;
            }

            return JitConfig.JitCounterPadding > 0 ? JitConfig.JitCounterPadding : 1;
        }
    }

    public override void Instrument(BasicBlock block, List<ICorJitInfo.PgoInstrumentationSchema> schema, byte* profileMemory)
    {
        var entry = schema[block.bbCountSchemaIndex];
        assert(block.bbCodeOffs == entry.ILOffset);
        assert(entry.InstrumentationKind is ICorJitInfo.PgoInstrumentationKind.BasicBlockIntCount
            or ICorJitInfo.PgoInstrumentationKind.BasicBlockLongCount);
        var counter = profileMemory + entry.Offset;

#if DEBUG
        if (JitConfig.JitPropagateSynthesizedCountsToProfileData > 0)
        {
            if (entry.InstrumentationKind is ICorJitInfo.PgoInstrumentationKind.BasicBlockIntCount)
            {
                *(uint*)counter = ConvertSynthesizedCount32(block.bbWeight);
            }
            else
            {
                *(ulong*)counter = ConvertSynthesizedCount64(block.bbWeight);
            }

            return;
        }
#endif
        var countType = entry.InstrumentationKind is ICorJitInfo.PgoInstrumentationKind.BasicBlockIntCount
            ? TYP_INT : TYP_LONG;
        var increment = CreateCounterIncrement(Compiler, counter, countType);

        if (block.HasFlag(BBF_TAILCALL_SUCCESSOR))
        {
            var first = true;
            foreach (var pred in block.PredBlocks)
            {
                if (!ShouldProcess(pred) && !pred.HasFlag(BBF_MARKED))
                {
                    continue;
                }

                JITDUMP($"Placing copy of block probe for {FMT_BB(block.bbNum)} in pred {FMT_BB(pred.bbNum)}\n");
                if (!first)
                {
                    increment = Compiler.gtCloneExpr(increment)!;
                }

                Compiler.fgInsertStmtAtBeg(pred, Compiler.gtNewStmt(increment));
                pred.RemoveFlags(BBF_MARKED);
                first = false;
            }
        }
        else
        {
            Compiler.fgInsertStmtAtBeg(block, Compiler.gtNewStmt(increment));
        }

        InstrCountValue++;
    }

    public static GenTree CreateCounterIncrement(Compiler compiler, byte* counter, var_types countType)
    {
        var interlocked = JitConfig.JitInterlockedProfiling > 0;
        var scalable = JitConfig.JitScalableProfiling > 0;

        if (interlocked || scalable)
        {
            GenTree? result = null;
            if (interlocked)
            {
                var address = compiler.gtNewIconHandleNode((nint)counter, GTF_ICON_BBC_PTR);
                result = compiler.gtNewAtomicNode(GT_XADD, countType, address, compiler.gtNewIconNode(countType, 1));
            }

            if (scalable)
            {
                if (interlocked)
                {
                    counter += countType is TYP_INT ? 4 : 8;
                }

                var address = compiler.gtNewIconHandleNode((nint)counter, GTF_ICON_BBC_PTR);
                var scalableIncrement = compiler.gtNewHelperCallNode(countType,
                    countType is TYP_INT ? CORINFO_HELP_COUNTPROFILE32 : CORINFO_HELP_COUNTPROFILE64, address);
                result = interlocked ? compiler.gtNewCommaNode(countType, result!, scalableIncrement)
                    : scalableIncrement;
            }

            assert(result is not null);
            return result;
        }

        var value = compiler.gtNewIndOfIconHandleNode(countType, (nint)counter, GTF_ICON_BBC_PTR);
        var incremented = compiler.gtNewBinaryNode(GT_ADD, countType, value, compiler.gtNewIconNode(countType, 1));
        var counterAddress = compiler.gtNewIconHandleNode((nint)counter, GTF_ICON_BBC_PTR);
        return compiler.gtNewStoreIndNode(countType, counterAddress, incremented);
    }
}
