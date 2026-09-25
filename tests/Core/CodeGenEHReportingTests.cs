// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
#if DEBUG
using System.IO;
#endif
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if DEBUG
using System.Text;
#endif
using NUnit.Framework;
using static RyuJitSharp.CORINFO_EH_CLAUSE_FLAGS;
using static RyuJitSharp.EHHandlerType;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CodeGenEHReportingTests
{
    [TestCase(true)]
    [TestCase(false)]
    public static void ReportsVmOrderedClausesWithSameTryFilterAndEndOffsets(bool matchedVm)
    {
        WithReporting((compiler, codeGen, context) =>
        {
            var blocks = SetBlocks(compiler, codeGen, 10);
            var captured = stackalloc CORINFO_EH_CLAUSE[4];
            var events = stackalloc int[5];
            context->Captured = captured;
            context->Events = events;

            compiler.compHndBBtab =
            [
                Clause(blocks[4], blocks[5], blocks[6], blocks[6], EH_HANDLER_CATCH, 0x110, 3),
                Clause(blocks[0], blocks[1], blocks[2], blocks[2], EH_HANDLER_CATCH, 0x220, 1),
                Clause(blocks[0], blocks[1], blocks[3], blocks[3], EH_HANDLER_CATCH, 0x330, 2),
                Clause(blocks[7], blocks[7], blocks[9], blocks[9], EH_HANDLER_FILTER, 0,
                    5, blocks[8]),
            ];
            compiler.compHndBBtabCount = 4;
            compiler.compEHTabOrderToVMClauseOrder = [2, 0, 1, 3];
            compiler.compVMClauseOrderToEHTabOrder = [1, 2, 0, 3];
            compiler.info.compMatchedVM = matchedVm;
            compiler.info.compNativeCodeSize = 100;
            codeGen.genReportEH();

            Assert.That(compiler.Metrics.EHClauseCount, Is.EqualTo(4));
            Assert.That(context->Calls, Is.EqualTo(matchedVm ? 5 : 0));
            if (!matchedVm)
            {
                return;
            }

            Assert.That(context->Count, Is.EqualTo(4));
            Assert.That(events[0], Is.EqualTo(-1));
            for (var i = 0; i < 4; i++)
            {
                Assert.That(events[i + 1], Is.EqualTo(i));
            }
            AssertClause(captured[0], CORINFO_EH_CLAUSE_NONE, 0, 20, 20, 30, 0x220);
            AssertClause(captured[1], CORINFO_EH_CLAUSE_SAMETRY, 0, 20, 30, 40, 0x330);
            AssertClause(captured[2], CORINFO_EH_CLAUSE_NONE, 40, 60, 60, 70, 0x110);
            AssertClause(captured[3], CORINFO_EH_CLAUSE_FILTER, 70, 80, 90, 100, 80);
        });
    }

    [TestCase(EH_HANDLER_CATCH, CORINFO_EH_CLAUSE_NONE)]
    [TestCase(EH_HANDLER_FILTER, CORINFO_EH_CLAUSE_FILTER)]
    [TestCase(EH_HANDLER_FAULT, CORINFO_EH_CLAUSE_FAULT)]
    [TestCase(EH_HANDLER_FAULT_WAS_FINALLY, CORINFO_EH_CLAUSE_FAULT)]
    [TestCase(EH_HANDLER_FINALLY, CORINFO_EH_CLAUSE_FINALLY)]
    public static void MapsAllHandlerKinds(EHHandlerType handler, CORINFO_EH_CLAUSE_FLAGS flags)
    {
        WithReporting((compiler, codeGen, context) =>
        {
            var blocks = SetBlocks(compiler, codeGen, 4);
            var captured = stackalloc CORINFO_EH_CLAUSE[1];
            var events = stackalloc int[2];
            context->Captured = captured;
            context->Events = events;
            compiler.compHndBBtab =
            [
                Clause(blocks[0], blocks[0], blocks[3], blocks[3], handler, 0x123,
                    (ushort)(handler == EH_HANDLER_FILTER ? 2 : 1),
                    handler == EH_HANDLER_FILTER ? blocks[2] : null),
            ];
            compiler.compHndBBtabCount = 1;
            compiler.compEHTabOrderToVMClauseOrder = [0];
            compiler.compVMClauseOrderToEHTabOrder = [0];
            compiler.info.compNativeCodeSize = 40;

            codeGen.genReportEH();

            Assert.That(context->Calls, Is.EqualTo(2));
            AssertClause(captured[0], flags, 0, 10, 30, 40, handler == EH_HANDLER_FILTER ? 20 : 0x123);
        });
    }

    [Test]
    public static void EqualNativeOffsetsDoNotMarkDistinctTryBlocksAsSameTry()
    {
        WithReporting((compiler, codeGen, context) =>
        {
            var blocks = SetBlocks(compiler, codeGen, 4);
            var otherTryBeg = new BasicBlock(null, null) { bbEmitCookie = blocks[0].bbEmitCookie };
            var otherTryLast = new BasicBlock(null, null)
            {
                bbEmitCookie = blocks[1].bbEmitCookie,
                Next = blocks[2],
            };
            var captured = stackalloc CORINFO_EH_CLAUSE[2];
            var events = stackalloc int[3];
            context->Captured = captured;
            context->Events = events;
            compiler.compHndBBtab =
            [
                Clause(blocks[0], blocks[1], blocks[2], blocks[2], EH_HANDLER_CATCH, 0x123, 1),
                Clause(otherTryBeg, otherTryLast, blocks[3], blocks[3], EH_HANDLER_CATCH, 0x456, 2),
            ];
            compiler.compHndBBtabCount = 2;
            compiler.compEHTabOrderToVMClauseOrder = [0, 1];
            compiler.compVMClauseOrderToEHTabOrder = [0, 1];
            compiler.info.compNativeCodeSize = 40;

            codeGen.genReportEH();

            Assert.That(context->Calls, Is.EqualTo(3));
            AssertClause(captured[0], CORINFO_EH_CLAUSE_NONE, 0, 20, 20, 30, 0x123);
            AssertClause(captured[1], CORINFO_EH_CLAUSE_NONE, 0, 20, 30, 40, 0x456);
        });
    }

    [Test]
    public static void NoHandlersMakesNoEeCallsOrMetricChanges()
    {
        WithReporting((compiler, codeGen, context) =>
        {
            compiler.Metrics.EHClauseCount = 13;
            codeGen.genReportEH();

            Assert.That(context->Calls, Is.Zero);
            Assert.That(compiler.Metrics.EHClauseCount, Is.EqualTo(13));
        });
    }

#if DEBUG
    [TestCase(true, "EH#0: try [G_M000_IG01..G_M000_IG03) handled by [G_M000_IG04..END) (class: 0123)")]
    [TestCase(false, "EH#0: try [0000..0014) handled by [001E..0028) (class: 0123)")]
    public static void OutgoingDiagnosticsPreserveDiffableLabelsAndNumericOffsets(bool diffable, string line)
    {
        WithReporting((compiler, codeGen, context) =>
        {
            var blocks = SetBlocks(compiler, codeGen, 4);
            Assert.That(codeGen.Emitter.emitOffsetToLabel(5), Is.EqualTo("UNKNOWN"));
            Assert.That(codeGen.Emitter.emitOffsetToLabel(40), Is.EqualTo("END"));
            var captured = stackalloc CORINFO_EH_CLAUSE[1];
            var events = stackalloc int[2];
            context->Captured = captured;
            context->Events = events;
            compiler.compHndBBtab =
            [
                Clause(blocks[0], blocks[1], blocks[3], blocks[3], EH_HANDLER_CATCH, 0x123, 1),
            ];
            compiler.compHndBBtabCount = 1;
            compiler.compEHTabOrderToVMClauseOrder = [0];
            compiler.compVMClauseOrderToEHTabOrder = [0];
            compiler.info.compNativeCodeSize = 40;
            compiler.opts.dspEHTable = true;
            compiler.opts.dspDiffable = diffable;

            using var stream = new MemoryStream();
            using var writer = new JitTextWriter(stream, leaveOpen: true);
            var previous = Globals.s_jitstdout;
            try
            {
                Globals.s_jitstdout = writer;
                codeGen.genReportEH();
                writer.Flush();
            }
            finally
            {
                Globals.s_jitstdout = previous;
            }

            var output = Encoding.UTF8.GetString(stream.ToArray());
            Assert.That(output, Does.Contain(line + Environment.NewLine));
            Assert.That(context->Calls, Is.EqualTo(2));
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitIGlist")]
    private static extern ref insGroup? GroupList(Emitter emitter);
#endif

    private static EHblkDsc Clause(BasicBlock tryBeg, BasicBlock tryLast, BasicBlock handlerBeg,
        BasicBlock handlerLast, EHHandlerType kind, int token, ushort funcIndex, BasicBlock? filter = null)
    {
        return new EHblkDsc
        {
            ebdTryBeg = tryBeg,
            ebdTryLast = tryLast,
            ebdHndBeg = handlerBeg,
            ebdHndLast = handlerLast,
            ebdHandlerType = kind,
            ebdTyp = (bbCatchType)token,
            ebdFuncIndex = funcIndex,
            ebdFilter = filter,
        };
    }

    private static BasicBlock[] SetBlocks(Compiler compiler, CodeGen codeGen, int count)
    {
        Assert.That(compiler.codeGen, Is.SameAs(codeGen));
        var blocks = new BasicBlock[count];
        var groups = new insGroup[count];
        for (var i = 0; i < count; i++)
        {
            groups[i] = new insGroup { igOffs = (uint)(i * 10), igSize = 10 };
            groups[i].InitializeNum((uint)(i + 1));
#if DEBUG
            groups[i].igSelf = groups[i];
#endif
            blocks[i] = new BasicBlock(null, null) { bbEmitCookie = groups[i] };
            if (i > 0)
            {
                blocks[i - 1].Next = blocks[i];
                groups[i - 1].igNext = groups[i];
            }
        }
        compiler.fgLastBB = blocks[^1];
#if DEBUG
        GroupList(codeGen.Emitter) = groups[0];
#endif
        return blocks;
    }

    private static void AssertClause(CORINFO_EH_CLAUSE clause, CORINFO_EH_CLAUSE_FLAGS flags,
        int tryBegin, int tryEnd, int handlerBegin, int handlerEnd, int token)
    {
        Assert.That(clause.Flags, Is.EqualTo(flags));
        Assert.That(clause.TryOffset, Is.EqualTo(tryBegin));
        Assert.That(clause.TryLength, Is.EqualTo(tryEnd));
        Assert.That(clause.HandlerOffset, Is.EqualTo(handlerBegin));
        Assert.That(clause.HandlerLength, Is.EqualTo(handlerEnd));
        Assert.That(clause.ClassToken, Is.EqualTo(token));
    }

    private delegate void ReportingAction(Compiler compiler, CodeGen codeGen, PublicationContext* context);

    private static void WithReporting(ReportingAction action)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_INT, REG_RAX, (compiler, codeGen, _) =>
        {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.setEHcount = &SetCount;
            vtable.setEHinfo = &SetInfo;
            var context = new PublicationContext
            {
                JitInfo = new ICorJitInfo { lpVtbl = &vtable },
            };
            compiler.info.compCompHnd = &context.JitInfo;
            compiler.info.compMatchedVM = true;
            action(compiler, codeGen, &context);
        });
    }

    private struct PublicationContext
    {
        public ICorJitInfo JitInfo;
        public CORINFO_EH_CLAUSE* Captured;
        public int* Events;
        public int Count;
        public int Calls;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void SetCount(ICorJitInfo* jitInfo, int count)
    {
        var context = (PublicationContext*)jitInfo;
        context->Count = count;
        context->Events[context->Calls++] = -1;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void SetInfo(ICorJitInfo* jitInfo, int index, CORINFO_EH_CLAUSE* clause)
    {
        GC.Collect();
        var context = (PublicationContext*)jitInfo;
        context->Captured[index] = *clause;
        context->Events[context->Calls++] = index;
    }
}
