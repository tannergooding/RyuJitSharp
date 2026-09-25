// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CORINFO_RUNTIME_ABI;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.GenCondition;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class EmitterFlagQueryTests
{
    [Test]
    public static void RedundantComparisonsRetainWidthOperandOrderAndMinoptsBehavior(
        [Values(EA_1BYTE, EA_2BYTE, EA_4BYTE, EA_8BYTE)] emitAttr size,
        [Values(false, true)] bool minopts)
    {
        WithEmitter((_, emitter) =>
        {
            emitter.emitIns_R_R(INS_cmp, size, REG_RAX, REG_RCX);
            emitter.emitIns_Nop(1);
            Assert.That(emitter.IsRedundantCmp(size, REG_RAX, REG_RCX), Is.True);
            Assert.That(emitter.IsRedundantCmp(size, REG_RCX, REG_RAX), Is.False);
            Assert.That(emitter.IsRedundantCmp(size == EA_8BYTE ? EA_4BYTE : EA_8BYTE, REG_RAX, REG_RCX), Is.False);
            Assert.That(emitter.IsRedundantCmp(size, REG_XMM0, REG_RCX), Is.False);
            Assert.That(emitter.IsRedundantCmp(size, REG_RAX, REG_NA), Is.False);
        }, minopts);
    }

    [TestCase(INS_mov, REG_R8, true)]
    [TestCase(INS_mov, REG_RAX, false)]
    [TestCase(INS_mov, REG_RCX, false)]
    [TestCase(INS_add, REG_R8, false)]
    [TestCase(INS_cmp, REG_R8, false)]
    public static void InterveningWritesAndTheFirstComparisonBoundTheSearch(
        instruction ins, regNumber target, bool expected)
    {
        WithEmitter((compiler, emitter) =>
        {
            emitter.emitIns_R_R(INS_cmp, EA_8BYTE, REG_RAX, REG_RCX);
            if (ins == INS_mov)
            {
                _ = emitter.emitIns_Mov(ins, EA_8BYTE, target, REG_RDX, canSkip: false);
            }
            else
            {
                emitter.emitIns_R_R(ins, EA_8BYTE, target, REG_RDX);
            }
            Assert.That(emitter.IsRedundantCmp(EA_8BYTE, REG_RAX, REG_RCX), Is.EqualTo(expected));
        });
    }

    [Test]
    public static void ImmediateComparisonsStopTheSearchEvenWhenAnEarlierComparisonMatches()
    {
        WithEmitter((_, emitter) =>
        {
            emitter.emitIns_R_R(INS_cmp, EA_8BYTE, REG_RAX, REG_RCX);
            emitter.emitIns_R_I(INS_cmp, EA_8BYTE, REG_R8, 0);

            Assert.That(emitter.IsRedundantCmp(EA_8BYTE, REG_RAX, REG_RCX), Is.False);
        });
    }

    [TestCase(INS_call, REG_RAX, false)]
    [TestCase(INS_cdq, REG_RDX, false)]
    [TestCase(INS_cwde, REG_RAX, false)]
    [TestCase(INS_r_movsq, REG_RCX, false)]
    [TestCase(INS_movsq, REG_RDI, false)]
    [TestCase(INS_stosq, REG_RSI, true)]
    public static void ImplicitRegisterWritesStopRedundantComparisonSearch(
        instruction ins, regNumber reg, bool expected)
    {
        WithEmitter((_, emitter) =>
        {
            emitter.emitIns_R_R(INS_cmp, EA_8BYTE, reg, REG_R8);
            emitter.emitIns_Nop(1);
            // Query only: these implicit writes have no explicit register operands.
            Last(emitter).idIns(ins);
            Last(emitter).idInsFmt(IF_NONE);

            Assert.That(emitter.IsRedundantCmp(EA_8BYTE, reg, REG_R8), Is.EqualTo(expected));
        });
    }

    [TestCase(31, true)]
    [TestCase(32, false)]
    public static void SearchRetainsTheNativeThirtyTwoInstructionLimit(int intervening, bool expected)
    {
        WithEmitter((_, emitter) =>
        {
            emitter.emitIns_R_R(INS_cmp, EA_8BYTE, REG_RAX, REG_RCX);
            for (var i = 0; i < intervening; i++)
            {
                emitter.emitIns_Nop(1);
            }
            Assert.That(emitter.IsRedundantCmp(EA_8BYTE, REG_RAX, REG_RCX), Is.EqualTo(expected));
        });
    }

    [TestCase(false, true)]
    [TestCase(true, false)]
    public static void RedundantComparisonSearchCrossesOnlyCompatibleExtensionGroups(bool changeGcState, bool expected)
    {
        WithEmitter((_, emitter) =>
        {
            emitter.emitIns_R_R(INS_cmp, EA_8BYTE, REG_RAX, REG_RCX);
            ForceNewGroup(emitter) = true;
            Assert.That(emitter.IsRedundantCmp(EA_8BYTE, REG_RAX, REG_RCX), Is.False);
            emitter.emitIns_Nop(1);
            var group = emitter.emitCurIG ?? throw new AssertionException("Missing instruction group.");
            if (changeGcState)
            {
                group.igFlags ^= InsGroupFlags.NoGCInterrupt;
            }
            Assert.That(emitter.IsRedundantCmp(EA_8BYTE, REG_RAX, REG_RCX), Is.EqualTo(expected));
            group.igFlags &= ~InsGroupFlags.Extend;
            Assert.That(emitter.IsRedundantCmp(EA_8BYTE, REG_RAX, REG_RCX), Is.False);
        });
    }

    [TestCase(INS_and, SGT, true, false)]
    [TestCase(INS_or, SGE, true, true)]
    [TestCase(INS_xor, SLT, true, true)]
    [TestCase(INS_add, EQ, true, false)]
    [TestCase(INS_add, NE, true, false)]
    [TestCase(INS_add, SGE, false, true)]
    [TestCase(INS_add, SLT, false, true)]
    [TestCase(INS_add, SGT, false, false)]
    [TestCase(INS_add, UGE, false, false)]
    [TestCase(INS_sub, ULT, false, false)]
    [TestCase(INS_bsf, EQ, false, false)]
    [TestCase(INS_bsr, NE, false, false)]
    public static void ResultFlagsRespectTheConsumerConditionAndSourceBasedZeroFlags(
        instruction ins, CodeKind condition, bool zero, bool sign)
    {
        WithEmitter((_, emitter) =>
        {
            emitter.emitIns_R_R(ins, EA_8BYTE, REG_RAX, REG_RCX);
            var cond = new GenCondition(condition);
            Assert.That(emitter.AreFlagsSetToZeroCmp(REG_RAX, EA_8BYTE, cond), Is.EqualTo(zero));
            Assert.That(emitter.AreFlagsSetForSignJumpOpt(REG_RAX, EA_8BYTE, cond), Is.EqualTo(sign));
            Assert.That(emitter.AreFlagsSetToZeroCmp(REG_RAX, EA_4BYTE, cond), Is.False);
            Assert.That(emitter.AreFlagsSetForSignJumpOpt(REG_RAX, EA_4BYTE, cond), Is.False);
            Assert.That(emitter.AreFlagsSetToZeroCmp(REG_RDX, EA_8BYTE, cond), Is.False);
            Assert.That(emitter.AreFlagsSetForSignJumpOpt(REG_RDX, EA_8BYTE, cond), Is.False);
        });
    }

    [TestCase(IF_RRD_RRD)]
    [TestCase(IF_RRW_RRW)]
    [TestCase(IF_ARW_RRW)]
    [TestCase(IF_SRW_RRW)]
    public static void FlagReuseRejectsReadOnlyRegistersAndAdditionalWrites(Emitter.insFormat format)
    {
        WithEmitter((_, emitter) =>
        {
            emitter.emitIns_R_R(INS_and, EA_8BYTE, REG_RAX, REG_RCX);
            // Isolate the descriptor's side-effect predicate from instruction encoding.
            Last(emitter).idInsFmt(format);
            Assert.That(emitter.AreFlagsSetToZeroCmp(REG_RAX, EA_8BYTE, new GenCondition(EQ)), Is.False);
            Assert.That(emitter.AreFlagsSetForSignJumpOpt(REG_RAX, EA_8BYTE, new GenCondition(SGE)), Is.False);
        });
    }

    [TestCase(0, false)]
    [TestCase(1, true)]
    [TestCase(16, true)]
    [TestCase(64, true)]
    public static void ImmediateShiftFlagQueriesRetainNativeRecordedCountRules(int count, bool expected)
    {
        WithEmitter((_, emitter) =>
        {
            if (count == 1)
            {
                emitter.emitIns_R(INS_shl_1, EA_8BYTE, REG_RAX);
            }
            else
            {
                emitter.emitIns_R_I(INS_shl_N, EA_8BYTE, REG_RAX, count);
            }
            Assert.That(Emitter.IsFlagsAlwaysModified(Last(emitter)), Is.EqualTo(expected));
            Assert.That(emitter.AreFlagsSetToZeroCmp(REG_RAX, EA_8BYTE, new GenCondition(EQ)), Is.EqualTo(expected));
            Assert.That(emitter.AreFlagsSetForSignJumpOpt(REG_RAX, EA_8BYTE, new GenCondition(SLT)), Is.EqualTo(expected));
        });
    }

    [Test]
    public static void ShiftExceptionsApplyOnlyToTheNativeLegacyFormats(
        [Values(INS_rcl_N, INS_rcr_N, INS_rol_N, INS_ror_N, INS_shl_N, INS_shr_N, INS_sar_N)] instruction ins)
    {
        WithEmitter((_, emitter) =>
        {
            emitter.emitIns_R_I(ins, EA_8BYTE, REG_RAX, 0);
            var id = Last(emitter);
            Assert.That(Emitter.IsFlagsAlwaysModified(id), Is.False);
            id.idInsFmt(IF_RWR_RRD_SHF);
            Assert.That(Emitter.IsFlagsAlwaysModified(id), Is.True);
            id.idInsFmt(IF_RRW_SHF);
            id.idSetIsLargeCns();
            Assert.That(Emitter.IsFlagsAlwaysModified(id), Is.True);
        });
    }

    [Test]
    public static void RegisterCountShiftsCannotGuaranteeModifiedFlags(
        [Values(INS_rcl, INS_rcr, INS_rol, INS_ror, INS_shl, INS_shr, INS_sar)] instruction ins)
    {
        WithEmitter((_, emitter) =>
        {
            emitter.emitIns_R(ins, EA_8BYTE, REG_RAX);
            Assert.That(Emitter.IsFlagsAlwaysModified(Last(emitter)), Is.False);
            Assert.That(emitter.AreFlagsSetToZeroCmp(REG_RAX, EA_8BYTE, new GenCondition(NE)), Is.False);
            Assert.That(emitter.AreFlagsSetForSignJumpOpt(REG_RAX, EA_8BYTE, new GenCondition(SGE)), Is.False);
        });
    }

    [Test]
    public static void FlagReuseIsDisabledInMinopts()
    {
        WithEmitter((_, emitter) =>
        {
            emitter.emitIns_R_R(INS_and, EA_8BYTE, REG_RAX, REG_RCX);
            Assert.That(emitter.AreFlagsSetToZeroCmp(REG_RAX, EA_8BYTE, new GenCondition(EQ)), Is.False);
            Assert.That(emitter.AreFlagsSetForSignJumpOpt(REG_RAX, EA_8BYTE, new GenCondition(SLT)), Is.False);
        }, minopts: true);
    }

    [Test]
    public static void FlagReuseRequiresASafePredecessorAndAnImmediateProducerWithoutMutatingState()
    {
        WithEmitter((compiler, emitter) =>
        {
            var equal = new GenCondition(EQ);
            var signed = new GenCondition(SLT);
            Assert.That(emitter.IsRedundantCmp(EA_8BYTE, REG_RAX, REG_RCX), Is.False);
            Assert.That(emitter.AreFlagsSetToZeroCmp(REG_RAX, EA_8BYTE, equal), Is.False);
            Assert.That(emitter.AreFlagsSetForSignJumpOpt(REG_RAX, EA_8BYTE, signed), Is.False);
            emitter.emitIns_R_R(INS_and, EA_8BYTE, REG_RAX, REG_RCX);
            ForceNewGroup(emitter) = true;
            Assert.That(emitter.AreFlagsSetToZeroCmp(REG_RAX, EA_8BYTE, equal), Is.False);
            Assert.That(emitter.AreFlagsSetForSignJumpOpt(REG_RAX, EA_8BYTE, signed), Is.False);
            ForceNewGroup(emitter) = false;
            Assert.That(emitter.AreFlagsSetToZeroCmp(REG_RAX, EA_8BYTE, equal), Is.True);
            Assert.That(emitter.AreFlagsSetForSignJumpOpt(REG_RAX, EA_8BYTE, signed), Is.True);
            var beforeCount = CurrentCount(emitter);
            var beforeSize = CurrentSize(emitter);
            var before = Last(emitter);
#if DEBUG
            compiler.opts.dspCode = true;
#endif
            Assert.That(emitter.AreFlagsSetToZeroCmp(REG_RAX, EA_8BYTE, equal), Is.True);
            Assert.That(emitter.AreFlagsSetForSignJumpOpt(REG_RAX, EA_8BYTE, signed), Is.True);
            Assert.That(CurrentCount(emitter), Is.EqualTo(beforeCount));
            Assert.That(CurrentSize(emitter), Is.EqualTo(beforeSize));
            Assert.That(Last(emitter), Is.SameAs(before));
#if DEBUG
            compiler.opts.dspCode = false;
#endif
            emitter.emitIns_Nop(1);
            Assert.That(emitter.AreFlagsSetToZeroCmp(REG_RAX, EA_8BYTE, equal), Is.False);
            Assert.That(emitter.AreFlagsSetForSignJumpOpt(REG_RAX, EA_8BYTE, signed), Is.False);
        });
    }

    [TestCase(INS_mov, false)]
    [TestCase(INS_not, false)]
    [TestCase(INS_sahf, true)]
    [TestCase(INS_mulEAX, true)]
    [TestCase(INS_and, true)]
    [TestCase(INS_add, true)]
    public static void FlagMutationIncludesResetUndefinedAndRestoredFlags(instruction ins, bool expected)
    {
        Assert.That(Emitter.emitDoesInsModifyFlags(ins), Is.EqualTo(expected));
    }

    private static void WithEmitter(Action<Compiler, Emitter> action, bool minopts = false)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_LONG, REG_RAX, (compiler, codeGen, _) =>
        {
            compiler.eeInfoInitialized = true;
            compiler.eeInfo.targetAbi = CORINFO_CORECLR_ABI;
            action(compiler, codeGen.Emitter);
        }, minopts);
    }

    private static Emitter.instrDesc Last(Emitter emitter)
    {
        return LastInstruction(emitter) ?? throw new AssertionException("Missing recorded instruction.");
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? LastInstruction(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitForceNewIG")]
    private static extern ref bool ForceNewGroup(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGinsCnt")]
    private static extern ref int CurrentCount(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGsize")]
    private static extern ref int CurrentSize(Emitter emitter);
}
