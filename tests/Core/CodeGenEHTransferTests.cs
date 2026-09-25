// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;
using static RyuJitSharp.var_types;
using JumpView = RyuJitSharp.UnitTests.EmitterJumpInstructionTests.JumpView;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenEHTransferTests
{
    [TestCase(0, true)]
    [TestCase(1, false)]
    [TestCase(2, true)]
    [TestCase(3, true)]
    [TestCase(4, false)]
    public static void RetlessCallsInsertBreakpointsOnlyAtCodeEndOrEHRegionBoundaries(int scenario, bool breakpoint)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var target = Label();
            var block = Transfer(BBJ_CALLFINALLY, target);
            block.SetFlags(BBF_RETLESS_CALL);
            if (scenario != 0)
            {
                var next = Label();
                block.Next = next;
                if (scenario == 2)
                {
                    next.TryIndex = 0;
                }
                else if (scenario == 3)
                {
                    next.HndIndex = 0;
                }
                else if (scenario == 4)
                {
                    next.SetFlags(BBF_COLD);
                    compiler.fgFirstColdBlock = next;
                }
            }
            compiler.compCurBB = block;
            codeGen.genCallFinally(block);
            var ids = Descriptors(codeGen);

            Assert.That(ids.Select(id => id.idIns()),
                Is.EqualTo(breakpoint ? new[] { INS_call, INS_int3 } : [INS_call]));
            Assert.That(JumpView.Target(ids[0]), Is.SameAs(target));
            Assert.That(ids[0].idCodeSize(), Is.EqualTo(5u));
            Assert.That(NoGcRequests(codeGen.Emitter), Is.Zero);
            Assert.That(codeGen.Emitter.emitCurIG!.igFlags & InsGroupFlags.NoGCInterrupt,
                Is.EqualTo(InsGroupFlags.None));
#if DEBUG
            Assert.That(ids[0].idDebugOnlyInfo()!.idFinallyCall, Is.True);
#endif
        });
    }

    [Test]
    public static void ReturningCallsUseContinuationAdjacencyAndConfiguredColdRegions(
        [Values(false, true)] bool adjacent, [Values(false, true)] bool cold,
        [Values(false, true)] bool splitConfigured)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var target = Label();
            var continuation = Label();
            var block = Transfer(BBJ_CALLFINALLY, target);
            var step = Transfer(BBJ_CALLFINALLYRET, continuation);
            block.Next = step;
            step.Next = adjacent ? continuation : Label();
            // EH identity is intentionally different: the returning decision
            // uses cold-region separation, not sameEHRegion.
            continuation.TryIndex = 0;
            if (cold)
            {
                continuation.SetFlags(BBF_COLD);
            }
            if (splitConfigured)
            {
                var firstCold = cold ? continuation : Label();
                firstCold.SetFlags(BBF_COLD);
                compiler.fgFirstColdBlock = firstCold;
            }
            compiler.compCurBB = block;
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RBX, TYP_REF);
            codeGen.GCInfo.gcMarkRegPtrVal(REG_R12, TYP_BYREF);

            codeGen.genCallFinally(block);

            var ids = Descriptors(codeGen);
            var fallThrough = adjacent && !(cold && splitConfigured);
            Assert.That(ids.Select(id => id.idIns()),
                Is.EqualTo([INS_call, fallThrough ? INS_nop : INS_jmp]));
            Assert.That(JumpView.Target(ids[0]), Is.SameAs(target));
            if (!fallThrough)
            {
                Assert.That(JumpView.Target(ids[1]), Is.SameAs(continuation));
            }
            Assert.That(codeGen.Emitter.emitCurIG!.igFlags & InsGroupFlags.NoGCInterrupt,
                Is.EqualTo(InsGroupFlags.NoGCInterrupt));
            Assert.That(NoGcRequests(codeGen.Emitter), Is.Zero);
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(RBM_RBX));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(RBM_R12));

            codeGen.instGen(INS_nop);
            Assert.That(codeGen.Emitter.emitCurIG!.igFlags & InsGroupFlags.NoGCInterrupt,
                Is.EqualTo(InsGroupFlags.None));
        });
    }

    [Test]
    public static void ReturningCallPreservesAnOuterNoGcRequest()
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var continuation = Label();
            var block = Transfer(BBJ_CALLFINALLY, Label());
            block.Next = Transfer(BBJ_CALLFINALLYRET, continuation);
            block.Next.Next = continuation;
            compiler.compCurBB = block;
            codeGen.Emitter.emitDisableGC();

            codeGen.genCallFinally(block);

            Assert.That(NoGcRequests(codeGen.Emitter), Is.EqualTo(1));
            Assert.That(codeGen.Emitter.emitCurIG!.igFlags & InsGroupFlags.NoGCInterrupt,
                Is.EqualTo(InsGroupFlags.NoGCInterrupt));
            codeGen.Emitter.emitEnableGC();
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void CatchReturnAlwaysRecordsRelocatableRaxLeaEvenWithoutRelocMode(bool reloc)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var target = Label();
            var block = Transfer(BBJ_EHCATCHRET, target);
            compiler.compCurBB = block;
            compiler.opts.compReloc = reloc;

            codeGen.genEHCatchRet(block);

            var id = Descriptors(codeGen).Single();
            Assert.That(id.idIns(), Is.EqualTo(INS_lea));
            Assert.That(id.idInsFmt(), Is.EqualTo(IF_RWR_LABEL));
            Assert.That(id.idReg1(), Is.EqualTo(REG_INTRET));
            Assert.That(id.idOpSize(), Is.EqualTo(EA_PTRSIZE));
            Assert.That(id.idIsDspReloc(), Is.True);
            Assert.That(id.idCodeSize(), Is.EqualTo(7u));
            Assert.That(JumpView.Target(id), Is.SameAs(target));
            Assert.That(JumpView.KeepLong(id), Is.True);
            Assert.That(JumpView.IsShort(id), Is.False);
            Assert.That(NoGcRequests(codeGen.Emitter), Is.Zero);
#if DEBUG
            Assert.That(id.idDebugOnlyInfo()!.idCatchRet, Is.True);
#endif
        });
    }

#if DEBUG
    [TestCase(false)]
    [TestCase(true)]
    public static void D005RejectsBeforeCallsLabelsOrGcChanges(bool catchReturn)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var target = Label();
            var block = Transfer(catchReturn ? BBJ_EHCATCHRET : BBJ_CALLFINALLY, target);
            compiler.compCurBB = block;
            compiler.opts.dspCode = true;

            _ = Assert.Throws<FatalJitException>(() =>
            {
                if (catchReturn)
                {
                    codeGen.genEHCatchRet(block);
                }
                else
                {
                    codeGen.genCallFinally(block);
                }
            });

            Assert.That(Descriptors(codeGen), Is.Empty);
            Assert.That(NoGcRequests(codeGen.Emitter), Is.Zero);
            Assert.That(target.bbEmitCookie, Is.Null);
        });
    }
#endif

    private static BasicBlock Label()
    {
        var block = new BasicBlock(null, null);
        block.SetFlags(BBF_HAS_LABEL);

        return block;
    }

    private static BasicBlock Transfer(BBKinds kind, BasicBlock target)
    {
        var block = new BasicBlock(null, null);
        block.SetKindAndTargetEdge(kind, new FlowEdge(block, target, null));

        return block;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitNoGCRequestCount")]
    private static extern ref int NoGcRequests(Emitter emitter);
}
