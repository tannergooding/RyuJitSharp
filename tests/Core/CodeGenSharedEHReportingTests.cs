// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.CORINFO_EH_CLAUSE_FLAGS;
using static RyuJitSharp.EHHandlerType;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CodeGenSharedEHReportingTests
{
#if !TARGET_WASM
    [Test]
    public static void EmptyEhTableLeavesCallbacksAndMetricsUntouched()
    {
        WithReporting((compiler, codeGen, context) =>
        {
            compiler.Metrics.EHClauseCount = 19;

            codeGen.genReportEH();

            Assert.That(context->Calls, Is.Zero);
            Assert.That(compiler.Metrics.EHClauseCount, Is.EqualTo(19));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public static void SharedReportingPreservesVmOrderFilterOffsetsAndSameTry(bool matchedVm)
    {
        WithReporting((compiler, codeGen, context) =>
        {
            var blocks = Blocks(compiler, 10);
            compiler.compHndBBtab =
            [
                Handler(blocks[4], blocks[5], blocks[6], EH_HANDLER_CATCH, 3, 0x111),
                Handler(blocks[0], blocks[1], blocks[2], EH_HANDLER_CATCH, 1, 0x222),
                Handler(blocks[0], blocks[1], blocks[3], EH_HANDLER_CATCH, 2, 0x333),
                Handler(blocks[7], blocks[7], blocks[9], EH_HANDLER_FILTER, 5, 0, blocks[8]),
            ];
            compiler.compHndBBtabCount = 4;
            compiler.compEHTabOrderToVMClauseOrder = [2, 0, 1, 3];
            compiler.compVMClauseOrderToEHTabOrder = [1, 2, 0, 3];
            compiler.info.compNativeCodeSize = 100;
            compiler.info.compMatchedVM = matchedVm;

            codeGen.genReportEH();

            Assert.That(compiler.Metrics.EHClauseCount, Is.EqualTo(4));
            Assert.That(context->Calls, Is.EqualTo(matchedVm ? 5 : 0));
            if (matchedVm)
            {
                Assert.That(context->Count, Is.EqualTo(4));
                Assert.That(context->FirstEvent, Is.EqualTo(-1));
                Assert.That(context->NextIndex, Is.EqualTo(4));
                AssertClause(context->Clauses[0], CORINFO_EH_CLAUSE_NONE, 0, 20, 20, 30, 0x222);
                AssertClause(context->Clauses[1], CORINFO_EH_CLAUSE_SAMETRY, 0, 20, 30, 40, 0x333);
                AssertClause(context->Clauses[2], CORINFO_EH_CLAUSE_NONE, 40, 60, 60, 70, 0x111);
                AssertClause(context->Clauses[3], CORINFO_EH_CLAUSE_FILTER, 70, 80, 90, 100, 80);
            }
        });
    }

    [TestCase(EH_HANDLER_CATCH, CORINFO_EH_CLAUSE_NONE)]
    [TestCase(EH_HANDLER_FILTER, CORINFO_EH_CLAUSE_FILTER)]
    [TestCase(EH_HANDLER_FAULT, CORINFO_EH_CLAUSE_FAULT)]
    [TestCase(EH_HANDLER_FAULT_WAS_FINALLY, CORINFO_EH_CLAUSE_FAULT)]
    [TestCase(EH_HANDLER_FINALLY, CORINFO_EH_CLAUSE_FINALLY)]
    public static void SharedReportingRetainsEveryNativeHandlerKind(
        EHHandlerType kind, CORINFO_EH_CLAUSE_FLAGS flags)
    {
        WithReporting((compiler, codeGen, context) =>
        {
            var blocks = Blocks(compiler, 4);
            compiler.compHndBBtab =
            [
                Handler(blocks[0], blocks[0], blocks[3], kind,
                    (ushort)(kind == EH_HANDLER_FILTER ? 2 : 1), 0x123,
                    kind == EH_HANDLER_FILTER ? blocks[2] : null),
            ];
            compiler.compHndBBtabCount = 1;
            compiler.compEHTabOrderToVMClauseOrder = [0];
            compiler.compVMClauseOrderToEHTabOrder = [0];
            compiler.info.compNativeCodeSize = 40;

            codeGen.genReportEH();

            Assert.That(context->Calls, Is.EqualTo(2));
            AssertClause(context->Clauses[0], flags, 0, 10, 30, 40,
                kind == EH_HANDLER_FILTER ? 20 : 0x123);
        });
    }

    [Test]
    public static void SharedOffsetsRetainUnsignedNativeOffsetBits()
    {
        WithReporting((compiler, codeGen, context) =>
        {
            var blocks = Blocks(compiler, 3);
            compiler.ehEmitCookie(blocks[0]).igOffs = 0x80000000;
            compiler.ehEmitCookie(blocks[1]).igOffs = 0x80000010;
            compiler.ehEmitCookie(blocks[2]).igOffs = 0x80000020;
            compiler.compHndBBtab =
            [
                Handler(blocks[0], blocks[0], blocks[2], EH_HANDLER_CATCH, 1, 0x123),
            ];
            compiler.compHndBBtabCount = 1;
            compiler.compEHTabOrderToVMClauseOrder = [0];
            compiler.compVMClauseOrderToEHTabOrder = [0];
            compiler.info.compNativeCodeSize = unchecked((int)0x80000030u);

            codeGen.genReportEH();

            AssertClause(context->Clauses[0], CORINFO_EH_CLAUSE_NONE,
                unchecked((int)0x80000000u), unchecked((int)0x80000010u),
                unchecked((int)0x80000020u), unchecked((int)0x80000030u), 0x123);
        });
    }
#else
    [Test]
    public static void WasmRetainsTheExcludedTableConstructionBoundary()
    {
        WithReporting((_, codeGen, context) =>
        {
            Assert.Throws<FatalJitException>(codeGen.genReportEH);
            Assert.That(context->Calls, Is.Zero);
        });
    }
#endif

    [TestCase(true)]
    [TestCase(false)]
    public static void ClausePublicationUsesTryIdentityRatherThanEqualNativeOffsets(bool sameTry)
    {
        WithReporting((compiler, codeGen, context) =>
        {
            var blocks = Blocks(compiler, 4);
            var secondTry = sameTry ? blocks[0] : new BasicBlock(null, null)
            {
                bbEmitCookie = blocks[0].bbEmitCookie,
            };
            compiler.compHndBBtab =
            [
                Handler(blocks[0], blocks[1], blocks[2], EH_HANDLER_CATCH, 1, 0x123),
                Handler(secondTry, blocks[1], blocks[3], EH_HANDLER_CATCH, 2, 0x456),
            ];
            compiler.compHndBBtabCount = 2;
            compiler.compVMClauseOrderToEHTabOrder = [0, 1];
            var clauses = new EHClauseInfo[]
            {
                new() { EHIndex = 0, clause = new CORINFO_EH_CLAUSE { ClassToken = 0x123 } },
                new() { EHIndex = 1, clause = new CORINFO_EH_CLAUSE { ClassToken = 0x456 } },
            };

            codeGen.genReportEHClauses(clauses);

            Assert.That(context->Calls, Is.EqualTo(2));
            Assert.That(context->Clauses[0].Flags, Is.EqualTo(CORINFO_EH_CLAUSE_NONE));
            Assert.That(context->Clauses[1].Flags,
                Is.EqualTo(sameTry ? CORINFO_EH_CLAUSE_SAMETRY : CORINFO_EH_CLAUSE_NONE));
            Assert.That(clauses[1].clause.Flags, Is.EqualTo(context->Clauses[1].Flags));
        });
    }

    [Test]
    public static void MissingEmitterCookiePreservesNativeRecoverableFailure()
    {
        WithReporting((compiler, _, _) =>
        {
            compiler.opts.SetMinOpts(false);
            var block = new BasicBlock(null, null);
            var error = Assert.Throws<FatalJitException>(() => compiler.ehEmitCookie(block));

            Assert.That(error, Has.Property(nameof(FatalJitException.Result))
                .EqualTo(CorJitResult.CORJIT_RECOVERABLEERROR));
        });
    }

    private static EHblkDsc Handler(BasicBlock start, BasicBlock last, BasicBlock handler,
        EHHandlerType kind, ushort funclet, int token, BasicBlock? filter = null)
    {
        return new EHblkDsc
        {
            ebdTryBeg = start,
            ebdTryLast = last,
            ebdHndBeg = handler,
            ebdHndLast = handler,
            ebdHandlerType = kind,
            ebdFuncIndex = funclet,
            ebdTyp = (bbCatchType)token,
            ebdFilter = filter,
        };
    }

    private static BasicBlock[] Blocks(Compiler compiler, int count)
    {
        var blocks = new BasicBlock[count];
        for (var index = 0; index < count; index++)
        {
            var group = new insGroup { igOffs = (uint)(index * 10), igSize = 10 };
#if DEBUG
            group.igSelf = group;
#endif
            blocks[index] = new BasicBlock(null, null) { bbEmitCookie = group };
            if (index > 0)
            {
                blocks[index - 1].Next = blocks[index];
            }
        }
        compiler.fgLastBB = blocks[^1];
        return blocks;
    }

    private static void AssertClause(CORINFO_EH_CLAUSE clause, CORINFO_EH_CLAUSE_FLAGS flags,
        int tryStart, int tryEnd, int handlerStart, int handlerEnd, int token)
    {
        Assert.That(clause.Flags, Is.EqualTo(flags));
        Assert.That(clause.TryOffset, Is.EqualTo(tryStart));
        Assert.That(clause.TryLength, Is.EqualTo(tryEnd));
        Assert.That(clause.HandlerOffset, Is.EqualTo(handlerStart));
        Assert.That(clause.HandlerLength, Is.EqualTo(handlerEnd));
        Assert.That(clause.ClassToken, Is.EqualTo(token));
    }

    private delegate void ReportingAction(Compiler compiler, CodeGen codeGen, PublicationContext* context);

    private static void WithReporting(ReportingAction action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.setEHcount = &SetCount;
        vtable.setEHinfo = &SetInfo;
        var captured = stackalloc CORINFO_EH_CLAUSE[4];
        var context = new PublicationContext
        {
            JitInfo = new ICorJitInfo { lpVtbl = &vtable },
            Clauses = captured,
            FirstEvent = int.MinValue,
        };
        compiler.info.compCompHnd = &context.JitInfo;
        compiler.info.compMatchedVM = true;
        JitTls.Compiler = compiler;
        try
        {
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            action(compiler, codeGen, &context);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    private struct PublicationContext
    {
        public ICorJitInfo JitInfo;
        public CORINFO_EH_CLAUSE* Clauses;
        public int Calls;
        public int Count;
        public int FirstEvent;
        public int NextIndex;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void SetCount(ICorJitInfo* jitInfo, int count)
    {
        var context = (PublicationContext*)jitInfo;
        context->Count = count;
        context->FirstEvent = context->Calls == 0 ? -1 : int.MinValue;
        context->Calls++;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void SetInfo(ICorJitInfo* jitInfo, int index, CORINFO_EH_CLAUSE* clause)
    {
        var context = (PublicationContext*)jitInfo;
        context->Clauses[index] = *clause;
        if (index != context->NextIndex)
        {
            context->NextIndex = int.MinValue;
        }
        else
        {
            context->NextIndex++;
        }
        context->Calls++;
    }
}
