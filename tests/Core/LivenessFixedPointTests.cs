// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.EHHandlerType;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;
using SetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class LivenessFixedPointTests
{
    private readonly struct Policy : ILivenessPolicy
    {
        public static bool IsLIR => true;
        public static bool ComputeMemoryLiveness => true;
    }

    private readonly struct EarlyPolicy : ILivenessPolicy
    {
        public static bool IsEarly => true;
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(65)]
    public static void AcyclicPropagationUsesDfsPostorderAndKillsOnlyDefinedLocals(int count)
    {
        WithCompiler(count, compiler => {
            var blocks = Blocks(compiler, 4);
            Connect(blocks[0], blocks[2]);
            Connect(blocks[2], blocks[1]);
            for (var local = 0; local < count; local++)
            {
                Append(compiler, blocks[1], new GenTreeLclVar(TYP_INT, local));
            }
            if (count != 0)
            {
                Append(compiler, blocks[2], compiler.gtNewStoreLclVarNode(count - 1, compiler.gtNewIconNode(TYP_INT, 42)));
            }
            var liveness = Prepare<Policy>(compiler);
            var epoch = compiler.CurLVEpoch;
            var current = compiler.compCurBB;
            compiler.fgStmtRemoved = true;

            liveness.DoLiveVarAnalysis();

            Assert.That(compiler._dfsTree!.HasCycle, Is.False);
            Assert.That(compiler._dfsTree.GetPostOrder(0), Is.SameAs(blocks[1]));
            Assert.That(compiler._dfsTree.Contains(blocks[3]), Is.False);
            Assert.That(Count(compiler, blocks[1].bbLiveIn), Is.EqualTo(count));
            Assert.That(Count(compiler, blocks[2].bbLiveOut), Is.EqualTo(count));
            Assert.That(Count(compiler, blocks[2].bbLiveIn), Is.EqualTo(Math.Max(0, count - 1)));
            Assert.That(Count(compiler, blocks[0].bbLiveIn), Is.EqualTo(Math.Max(0, count - 1)));
            Assert.That(Count(compiler, blocks[3].bbLiveIn), Is.Zero);
            Assert.That(Count(compiler, blocks[3].bbLiveOut), Is.Zero);
            Assert.That(compiler.CurLVEpoch, Is.EqualTo(epoch));
            Assert.That(compiler.compCurBB, Is.SameAs(current));
            Assert.That(compiler.fgStmtRemoved, Is.True);
            Assert.That(compiler.fgLocalVarLivenessDone, Is.False);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void CyclesConvergeForLocalAndMemoryOnlyChanges(bool locals)
    {
        WithCompiler(locals ? 65 : 0, compiler => {
            var blocks = Blocks(compiler, 3);
            Connect(blocks[0], blocks[1]);
            Connect(blocks[1], blocks[2]);
            Connect(blocks[2], blocks[0]);
            if (locals)
            {
                Append(compiler, blocks[0], new GenTreeLclVar(TYP_INT, 64));
            }
            Append(compiler, blocks[0], new GenTreeIndir(GT_IND, TYP_INT, compiler.gtNewIconNode(TYP_I_IMPL, 4096)));
            Append(compiler, blocks[1], new GenTreeStoreInd(TYP_INT, compiler.gtNewIconNode(TYP_I_IMPL, 8192),
                compiler.gtNewIconNode(TYP_INT, 1)));
            var liveness = Prepare<Policy>(compiler);

            liveness.DoLiveVarAnalysis();

            Assert.That(compiler._dfsTree!.HasCycle, Is.True);
            foreach (var block in blocks)
            {
                Assert.That(Count(compiler, block.bbLiveIn), Is.EqualTo(locals ? 1 : 0));
                Assert.That(Count(compiler, block.bbLiveOut), Is.EqualTo(locals ? 1 : 0));
                Assert.That(block.bbMemoryLiveIn, Is.EqualTo(3));
                Assert.That(block.bbMemoryLiveOut, Is.EqualTo(3));
                if (locals)
                {
                    Assert.That(SetOps.IsMember(compiler, block.bbLiveIn, compiler.lvaTable[64]._varIndex), Is.True);
                }
            }

            var before = blocks.Select(block => block.bbLiveIn.ToArray()).ToArray();
            liveness.DoLiveVarAnalysis();
            for (var index = 0; index < blocks.Length; index++)
            {
                Assert.That(blocks[index].bbLiveIn, Is.EqualTo(before[index]));
            }
        });
    }

    [TestCase(BBJ_COND)]
    [TestCase(BBJ_SWITCH)]
    [TestCase(BBJ_EHFINALLYRET)]
    public static void EveryRegularSuccessorContributesIncludingDuplicateEdges(BBKinds kind)
    {
        WithCompiler(3, compiler => {
            var blocks = Blocks(compiler, 4);
            var first = new FlowEdge(blocks[0], blocks[1], null);
            var second = new FlowEdge(blocks[0], blocks[2], null);
            blocks[0].Kind = kind;
            switch (kind)
            {
                case BBJ_COND:
                {
                    blocks[0].SetCond(first, second);
                    break;
                }

                case BBJ_SWITCH:
                {
                    blocks[0].SwitchTargets = new BBswtDesc([first, second, first], [0, 1, 2], true, 0);
                    break;
                }

                case BBJ_EHFINALLYRET:
                {
                    blocks[0].bbEhfTargets = new BBJumpTable([first, second, first]);
                    break;
                }
            }
            Append(compiler, blocks[1], new GenTreeLclVar(TYP_INT, 0));
            Append(compiler, blocks[2], new GenTreeLclVar(TYP_INT, 1));
            Append(compiler, blocks[3], new GenTreeLclVar(TYP_INT, 2));

            Prepare<Policy>(compiler).DoLiveVarAnalysis();

            Assert.That(Count(compiler, blocks[0].bbLiveIn), Is.EqualTo(2));
            Assert.That(SetOps.IsMember(compiler, blocks[0].bbLiveIn, compiler.lvaTable[0]._varIndex), Is.True);
            Assert.That(SetOps.IsMember(compiler, blocks[0].bbLiveIn, compiler.lvaTable[1]._varIndex), Is.True);
            Assert.That(SetOps.IsMember(compiler, blocks[0].bbLiveIn, compiler.lvaTable[2]._varIndex), Is.False);
        });
    }

    [Test]
    public static void JmpMakesTrackedArgumentsLiveOutEvenWhenTheyAreLocallyDefined()
    {
        WithCompiler(4, compiler => {
            compiler.info.compArgsCount = 3;
            compiler.compJmpOpUsed = true;
            compiler.lvaTable[1].lvPinned = true;
            for (var index = 0; index < 3; index++)
            {
                compiler.lvaTable[index].lvIsParam = true;
            }
            var block = Blocks(compiler, 1)[0];
            Append(compiler, block, compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 1)));
            Append(compiler, block, new GenTreeVal(GT_JMP, TYP_VOID, 0));
            block.SetFlags(BBF_HAS_JMP);

            Prepare<Policy>(compiler).DoLiveVarAnalysis();

            Assert.That(Count(compiler, block.bbLiveOut), Is.EqualTo(2));
            Assert.That(Count(compiler, block.bbLiveIn), Is.EqualTo(1));
            Assert.That(SetOps.IsMember(compiler, block.bbLiveIn, compiler.lvaTable[2]._varIndex), Is.True);
        });
    }

    [TestCase(BBJ_RETURN, true)]
    [TestCase(BBJ_THROW, true)]
    [TestCase(BBJ_ALWAYS, true)]
    [TestCase(BBJ_RETURN, false)]
    public static void KeepAliveThisAppliesToReturnsThrowsAndInfiniteLoops(BBKinds kind, bool tracked)
    {
        WithCompiler(1, compiler => {
            compiler.info.compIsStatic = false;
            compiler.info.compMethodInfo->options = CorInfoOptions.CORINFO_GENERICS_CTXT_FROM_THIS |
                CorInfoOptions.CORINFO_GENERICS_CTXT_KEEP_ALIVE;
            compiler.lvaTable[0].Type = TYP_REF;
            compiler.lvaTable[0].lvIsParam = true;
            compiler.lvaTable[0].lvPinned = !tracked;
            var block = Blocks(compiler, 1)[0];
            if (kind is BBJ_ALWAYS)
            {
                Connect(block, block);
            }
            else
            {
                block.Kind = kind;
            }

            Prepare<Policy>(compiler).DoLiveVarAnalysis();

            Assert.That(Count(compiler, block.bbLiveIn), Is.EqualTo(tracked ? 1 : 0));
            Assert.That(Count(compiler, block.bbLiveOut), Is.EqualTo(tracked ? 1 : 0));
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void OnlyEarlyOsrLivenessModelsTheUnexpandedRecursiveTailcall(bool early, bool osr)
    {
        WithCompiler(1, compiler => {
            var blocks = Blocks(compiler, 3, lir: !early);
            blocks[0].SetCond(new FlowEdge(blocks[0], blocks[2], null), new FlowEdge(blocks[0], blocks[1], null));
            Connect(blocks[1], blocks[0]);
            blocks[2].SetFlags(BBF_RECURSIVE_TAILCALL);
            if (osr)
            {
                compiler.fgEntryBB = blocks[0];
                compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_OSR);
            }
            if (early)
            {
                compiler.fgNodeThreading = NodeThreading.AllLocals;
                var root = new GenTreeLclVar(TYP_INT, 0);
                var statement = new Statement(root, 0);
                compiler.fgInsertStmtAtEnd(blocks[0], statement);
                var sequencer = new LocalSequencer(compiler);
                sequencer.Start(statement);
                sequencer.SequenceLocal(root);
                sequencer.Finish(statement);
                Prepare<EarlyPolicy>(compiler).DoLiveVarAnalysis();
            }
            else
            {
                Append(compiler, blocks[0], new GenTreeLclVar(TYP_INT, 0));
                Prepare<Policy>(compiler).DoLiveVarAnalysis();
            }

            Assert.That(compiler._dfsTree!.HasCycle, Is.True);
            Assert.That(Count(compiler, blocks[2].bbLiveIn), Is.EqualTo(early && osr ? 1 : 0));
            Assert.That(Count(compiler, blocks[2].bbLiveOut), Is.EqualTo(early && osr ? 1 : 0));
        });
    }

    [Test]
    public static void CallfinallyTargetIsBothRegularAndExceptionalButItsTailHasNoEhFlow()
    {
        WithCompiler(2, compiler => {
            compiler.lvaTable[1].SetAddressExposed(true, AddressExposedReason.EXTERNALLY_VISIBLE_IMPLICITLY);
            var blocks = Blocks(compiler, 4);
            var call = blocks[0];
            var tail = blocks[1];
            var handler = blocks[2];
            var exit = blocks[3];
            call.TryIndex = tail.TryIndex = 0;
            handler.HndIndex = 0;
            call.SetKindAndTargetEdge(BBJ_CALLFINALLY, new FlowEdge(call, handler, null));
            tail.SetKindAndTargetEdge(BBJ_CALLFINALLYRET, new FlowEdge(tail, exit, null));
            handler.Kind = BBJ_EHFINALLYRET;
            handler.bbEhfTargets = new BBJumpTable([new FlowEdge(handler, tail, null)]);
            compiler.compHndBBtab = [Clause(EH_HANDLER_FINALLY, call, handler)];
            compiler.compHndBBtab[0].ebdTryLast = tail;
            compiler.compHndBBtabCount = 1;
            Append(compiler, call, compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 42)));
            Append(compiler, handler, new GenTreeLclVar(TYP_INT, 0));
            Append(compiler, handler, new GenTreeLclVar(TYP_INT, 1));

            Prepare<Policy>(compiler).DoLiveVarAnalysis();

            Assert.That(Count(compiler, call.bbLiveIn), Is.EqualTo(1));
            Assert.That(Count(compiler, call.bbLiveOut), Is.EqualTo(1));
            Assert.That(call.bbMemoryLiveIn, Is.EqualTo(1));
            Assert.That(call.bbMemoryLiveOut, Is.EqualTo(1));
            Assert.That(Count(compiler, tail.bbLiveIn), Is.Zero);
            Assert.That(tail.bbMemoryLiveIn, Is.Zero);
            var successors = new List<BasicBlock>();
            _ = call.VisitEHSuccs(compiler, block => {
                successors.Add(block);
                return BasicBlockVisit.Continue;
            });
            Assert.That(successors, Is.EqualTo([handler]));
            Assert.That(tail.VisitEHSuccs(compiler, _ => throw new InvalidOperationException()),
                Is.EqualTo(BasicBlockVisit.Continue));

            var accumulated = SetOps.MakeEmpty(compiler);
            var memory = 2;
            compiler.fgAddHandlerLiveVars(call, accumulated, ref memory);
            Assert.That(Count(compiler, accumulated), Is.EqualTo(1));
            Assert.That(memory, Is.EqualTo(3));
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public static void ExceptionalTraversalPreservesFilterHandlerOuterOrderAndAbort(int abortAfter)
    {
        WithCompiler(3, compiler => {
            var blocks = Blocks(compiler, 4);
            blocks[0].TryIndex = 0;
            blocks[1].HndIndex = blocks[2].HndIndex = 0;
            blocks[3].HndIndex = 1;
            compiler.compHndBBtab = [
                Clause(EH_HANDLER_FILTER, blocks[0], blocks[2]),
                Clause(EH_HANDLER_CATCH, blocks[0], blocks[3]),
            ];
            compiler.compHndBBtabCount = 2;
            compiler.compHndBBtab[0].ebdFilter = blocks[1];
            compiler.compHndBBtab[0].ebdEnclosingTryIndex = 1;
            for (var index = 0; index < 3; index++)
            {
                Append(compiler, blocks[0], compiler.gtNewStoreLclVarNode(index, compiler.gtNewIconNode(TYP_INT, 0)));
                Append(compiler, blocks[index + 1], new GenTreeLclVar(TYP_INT, index));
            }
            var visited = new List<BasicBlock>();
            var result = blocks[0].VisitEHSuccs(compiler, block => {
                visited.Add(block);
                return visited.Count == abortAfter ? BasicBlockVisit.Abort : BasicBlockVisit.Continue;
            });
            Assert.That(visited, Is.EqualTo(blocks.Skip(1).Take(abortAfter == 0 ? 3 : abortAfter)));
            Assert.That(result, Is.EqualTo(abortAfter == 0 ? BasicBlockVisit.Continue : BasicBlockVisit.Abort));

            Prepare<Policy>(compiler).DoLiveVarAnalysis();

            Assert.That(Count(compiler, blocks[0].bbLiveIn), Is.EqualTo(3));
            Assert.That(Count(compiler, blocks[0].bbLiveOut), Is.EqualTo(3));
            Assert.That(Count(compiler, blocks[1].bbLiveOut), Is.EqualTo(1));
            Assert.That(SetOps.IsMember(compiler, blocks[1].bbLiveOut, compiler.lvaTable[2]._varIndex), Is.True);
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void FilterSecondPassReachesOnlyEnclosedTryFinallyAndFaultHandlers(bool abort, bool inHandler)
    {
        WithCompiler(2, compiler => {
            var blocks = Blocks(compiler, 7);
            blocks[0].TryIndex = 2;
            blocks[1].HndIndex = 0;
            blocks[2].HndIndex = 1;
            blocks[3].HndIndex = 2;
            blocks[4].HndIndex = blocks[5].HndIndex = blocks[6].HndIndex = 3;
            compiler.compHndBBtab = [
                Clause(EH_HANDLER_CATCH, blocks[0], blocks[1]),
                Clause(EH_HANDLER_FINALLY, blocks[0], blocks[2]),
                Clause(EH_HANDLER_FAULT, blocks[0], blocks[3]),
                Clause(EH_HANDLER_FILTER, blocks[0], blocks[6]),
            ];
            compiler.compHndBBtabCount = 4;
            compiler.compHndBBtab[1].ebdEnclosingTryIndex = inHandler ? EHblkDsc.NO_ENCLOSING_INDEX : (ushort)3;
            compiler.compHndBBtab[1].ebdEnclosingHndIndex = inHandler ? (ushort)3 : EHblkDsc.NO_ENCLOSING_INDEX;
            compiler.compHndBBtab[2].ebdEnclosingTryIndex = 3;
            compiler.compHndBBtab[3].ebdFilter = blocks[4];
            Append(compiler, blocks[2], new GenTreeLclVar(TYP_INT, 0));
            Append(compiler, blocks[3], new GenTreeLclVar(TYP_INT, 1));
            var visited = new List<BasicBlock>();
            var result = blocks[4].VisitEHSuccs(compiler, block => {
                visited.Add(block);
                return abort ? BasicBlockVisit.Abort : BasicBlockVisit.Continue;
            });
            Assert.That(visited, Is.EqualTo(abort || inHandler ? new[] { blocks[3] } : [blocks[3], blocks[2]]));
            Assert.That(result, Is.EqualTo(abort ? BasicBlockVisit.Abort : BasicBlockVisit.Continue));
            Assert.That(blocks[6].VisitEHSuccs(compiler, _ => throw new InvalidOperationException()),
                Is.EqualTo(BasicBlockVisit.Continue));

            Prepare<Policy>(compiler).DoLiveVarAnalysis();

            Assert.That(Count(compiler, blocks[4].bbLiveIn), Is.EqualTo(inHandler ? 1 : 2));
            Assert.That(Count(compiler, blocks[4].bbLiveOut), Is.EqualTo(inHandler ? 1 : 2));
        });
    }

    [TestCase(TYP_INT, false, 0, false)]
    [TestCase(TYP_INT, true, 0, true)]
    [TestCase(TYP_REF, false, 0, true)]
    [TestCase(TYP_BYREF, false, 0, true)]
    [TestCase(TYP_REF, true, 1, false)]
    [TestCase(TYP_REF, true, 2, false)]
    [TestCase(TYP_REF, true, 3, false)]
    [TestCase(TYP_REF, true, 4, false)]
    public static void EntryInitializationRespectsParametersPromotionAndUntrackedLocals(var_types type, bool initMem, int special, bool expected)
    {
        WithCompiler(2, compiler => {
            ref var local = ref compiler.lvaTable[0];
            local.Type = type;
            local.lvIsParam = special == 1;
            local.lvIsParamRegTarget = special == 2;
            local.lvPinned = special == 4;
            if (special == 3)
            {
                MakeDependentField(compiler);
            }
            compiler.info.compInitMem = initMem;
            var block = Blocks(compiler, 1)[0];
            Append(compiler, block, new GenTreeLclVar(type, 0));
            var liveness = Prepare<Policy>(compiler);
            liveness.DoLiveVarAnalysis();
            compiler.lvaTable[0].lvMustInit = true;
            compiler.lvaTable[0]._lvLiveInOutOfHandler = true;

            var empty = SetOps.MakeEmpty(compiler);
            liveness.MarkMustInitAndEHVars(empty, empty);

            Assert.That(compiler.lvaTable[0].lvMustInit, Is.EqualTo(expected));
            Assert.That(compiler.lvaTable[0]._lvLiveInOutOfHandler, Is.False);
        });
    }

    [TestCase(TYP_INT, 0, false)]
    [TestCase(TYP_REF, 0, true)]
    [TestCase(TYP_BYREF, 0, true)]
    [TestCase(TYP_REF, 1, false)]
    [TestCase(TYP_REF, 2, false)]
    [TestCase(TYP_REF, 3, true)]
    public static void FinallyLiveOutMarksHandlerStateAndRequiresGcInitialization(var_types type, int special, bool expected)
    {
        WithCompiler(2, compiler => {
            compiler.lvaTable[0].Type = type;
            compiler.lvaTable[0].lvIsParam = special == 1;
            compiler.lvaTable[0].lvIsParamRegTarget = special == 2;
            if (special == 3)
            {
                MakeDependentField(compiler);
            }
            var blocks = Blocks(compiler, 2);
            blocks[0].Kind = BBJ_EHFINALLYRET;
            blocks[0].bbEhfTargets = new BBJumpTable([new FlowEdge(blocks[0], blocks[1], null)]);
            Append(compiler, blocks[1], new GenTreeLclVar(type, 0));
            var liveness = Prepare<Policy>(compiler);
            liveness.DoLiveVarAnalysis();

            liveness.MarkMustInitAndEHVars(blocks[0].bbLiveOut, SetOps.MakeEmpty(compiler));

            Assert.That(compiler.lvaTable[0].IsLiveInOutOfHandler, Is.True);
            Assert.That(compiler.lvaTable[0].lvMustInit, Is.EqualTo(expected));
        });
    }

    [Test]
    public static void CatchLivenessMarksHandlerStateWithoutForcingNonGcInitialization()
    {
        WithCompiler(1, compiler => {
            var blocks = Blocks(compiler, 2);
            blocks[0].TryIndex = 0;
            blocks[1].HndIndex = 0;
            compiler.compHndBBtab = [Clause(EH_HANDLER_CATCH, blocks[0], blocks[1])];
            compiler.compHndBBtabCount = 1;
            Append(compiler, blocks[1], new GenTreeLclVar(TYP_INT, 0));
            var liveness = Prepare<Policy>(compiler);
            liveness.DoLiveVarAnalysis();

            liveness.MarkMustInitAndEHVars(SetOps.MakeEmpty(compiler), blocks[1].bbLiveIn);

            Assert.That(compiler.lvaTable[0].IsLiveInOutOfHandler, Is.True);
            Assert.That(compiler.lvaTable[0].lvMustInit, Is.False);
        });
    }

    [Test]
    public static void RawHandlerBitPreservesIndependentDescriptorCopiesAndNeighborFlags()
    {
        LclVarDsc original = default;
        original.lvPinned = true;
        var copy = original;
        original._lvLiveInOutOfHandler = true;
        Assert.That(copy._lvLiveInOutOfHandler, Is.False);
        copy = original;
        original._lvLiveInOutOfHandler = false;
        Assert.That(copy._lvLiveInOutOfHandler, Is.True);
        Assert.That(original.lvPinned, Is.True);
        Assert.That(copy.lvPinned, Is.True);
    }

#if DEBUG
    [Test]
    public static void SolverAndBothDumpOverloadsPreserveExactNativeFormatting()
    {
        WithCompiler(1, compiler => {
            var block = Blocks(compiler, 1)[0];
            Append(compiler, block, new GenTreeLclVar(TYP_INT, 0));
            Append(compiler, block, new GenTreeIndir(GT_IND, TYP_INT, compiler.gtNewIconNode(TYP_I_IMPL, 4096)));
            var liveness = Prepare<Policy>(compiler);
            compiler.verbose = true;
            var expected = ($"{FMT_BB(block.bbNum)} IN (1)={{V00}} + ByrefExposed + GcHeap\n" +
                "     OUT(0)={   }\n\n").ReplaceLineEndings(Environment.NewLine);

            Assert.That(Capture(liveness.DoLiveVarAnalysis),
                Is.EqualTo("\nBB liveness after DoLiveVarAnalysis():\n\n".ReplaceLineEndings(Environment.NewLine) + expected));
            Assert.That(Capture(() => compiler.fgDispBBLiveness(block)), Is.EqualTo(expected));
            Assert.That(Capture(compiler.fgDispBBLiveness), Is.EqualTo(expected));
        });
    }

    private static string Capture(Action action)
    {
        using var stream = new MemoryStream();
        using var writer = new JitTextWriter(stream, leaveOpen: true);
        var previous = s_jitstdout;
        try
        {
            s_jitstdout = writer;
            action();
            writer.Flush();
        }
        finally
        {
            s_jitstdout = previous;
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
#endif

    private static int Count(Compiler compiler, nint[] set) => (int)SetOps.Count(compiler, set);

    private static void MakeDependentField(Compiler compiler)
    {
        compiler.lvaTable[0].lvIsStructField = true;
        compiler.lvaTable[0].lvParentLcl = 1;
        compiler.lvaTable[1].Type = TYP_STRUCT;
        compiler.lvaTable[1].lvPromoted = true;
        compiler.lvaTable[1].lvDoNotEnregister = true;
        compiler.lvaTable[1].lvFieldLclStart = 0;
        compiler.lvaTable[1].lvFieldCnt = 1;
    }

    private static EHblkDsc Clause(EHHandlerType kind, BasicBlock tryBlock, BasicBlock handler) => new() {
        ebdHandlerType = kind,
        ebdTryBeg = tryBlock,
        ebdTryLast = tryBlock,
        ebdHndBeg = handler,
        ebdHndLast = handler,
        ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
        ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
    };

    private static void Connect(BasicBlock source, BasicBlock target)
    {
        source.SetKindAndTargetEdge(BBJ_ALWAYS, new FlowEdge(source, target, null));
    }

    private static Liveness<TPolicy> Prepare<TPolicy>(Compiler compiler)
        where TPolicy : ILivenessPolicy
    {
        compiler._dfsTree = compiler.fgComputeDfs(false);
        var liveness = new Liveness<TPolicy>(compiler);
        liveness.Init();
        liveness.PerBlockLocalVarLiveness();

        return liveness;
    }

    private static BasicBlock[] Blocks(Compiler compiler, int count, bool lir = true)
    {
        var blocks = new BasicBlock[count];
        for (var index = 0; index < count; index++)
        {
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            blocks[index] = block;
            if (lir)
            {
                block.MakeLir(null, null);
            }
            if (index != 0)
            {
                blocks[index - 1].Next = block;
                block.Prev = blocks[index - 1];
            }
        }
        compiler.fgFirstBB = blocks[0];
        compiler.fgLastBB = blocks[^1];

        return blocks;
    }

    private static void Append(Compiler compiler, BasicBlock block, GenTree root)
    {
        var statement = new Statement(root, 0);
        compiler.fgSetStmtSeq(statement);
        var nodes = statement.TreeList.ToArray();
        foreach (var node in nodes)
        {
            node.Prev = null;
            node.Next = null;
        }
        foreach (var node in nodes)
        {
            block.InsertAtEnd(node);
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitMaxLocalsToTrack")]
    private static extern ref int MaxLocalsToTrack(ref JitConfigValues config);

    private static void WithCompiler(int count, Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var previousConfig = JitConfig;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        CORINFO_METHOD_INFO methodInfo = default;
        compiler.info.compMethodInfo = &methodInfo;
        compiler.info.compIsStatic = true;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.compHndBBtab = [];
        compiler.fgNodeThreading = NodeThreading.AllTrees;
        compiler.lvaRefCountState = RefCountState.RCS_NORMAL;
        compiler.lvaCount = count;
        compiler.lvaTable = new LclVarDsc[count];
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
#endif
        JitTls.Compiler = compiler;
        MaxLocalsToTrack(ref JitConfig) = 1024;
        try
        {
            for (var index = 0; index < count; index++)
            {
                compiler.lvaTable[index].Type = TYP_INT;
                compiler.lvaTable[index].setLvRefCnt(3);
                compiler.lvaTable[index].setLvRefCntWtd(index + 1);
            }
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
            JitConfig = previousConfig;
        }
    }
}
