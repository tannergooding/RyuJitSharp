// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.Emitter.insSize;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.insFlags;
using static RyuJitSharp.instruction;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm32CodeGenUnsupportedNodesTests
{
    [TestCase(INS_b, emitJumpKind.EJ_jmp, emitJumpKind.EJ_jmp)]
    [TestCase(INS_beq, emitJumpKind.EJ_eq, emitJumpKind.EJ_ne)]
    [TestCase(INS_bne, emitJumpKind.EJ_ne, emitJumpKind.EJ_eq)]
    [TestCase(INS_bhs, emitJumpKind.EJ_hs, emitJumpKind.EJ_lo)]
    [TestCase(INS_blo, emitJumpKind.EJ_lo, emitJumpKind.EJ_hs)]
    [TestCase(INS_bmi, emitJumpKind.EJ_mi, emitJumpKind.EJ_pl)]
    [TestCase(INS_bpl, emitJumpKind.EJ_pl, emitJumpKind.EJ_mi)]
    [TestCase(INS_bvs, emitJumpKind.EJ_vs, emitJumpKind.EJ_vc)]
    [TestCase(INS_bvc, emitJumpKind.EJ_vc, emitJumpKind.EJ_vs)]
    [TestCase(INS_bhi, emitJumpKind.EJ_hi, emitJumpKind.EJ_ls)]
    [TestCase(INS_bls, emitJumpKind.EJ_ls, emitJumpKind.EJ_hi)]
    [TestCase(INS_bge, emitJumpKind.EJ_ge, emitJumpKind.EJ_lt)]
    [TestCase(INS_blt, emitJumpKind.EJ_lt, emitJumpKind.EJ_ge)]
    [TestCase(INS_bgt, emitJumpKind.EJ_gt, emitJumpKind.EJ_le)]
    [TestCase(INS_ble, emitJumpKind.EJ_le, emitJumpKind.EJ_gt)]
    public static void ArmJumpKindsMapToInstructionsAndReverseConditions(
        instruction ins, emitJumpKind jumpKind, emitJumpKind reverseJumpKind)
    {
        Assert.That(Emitter.emitJumpKindToIns(jumpKind), Is.EqualTo(ins));
        Assert.That(Emitter.emitInsToJumpKind(ins), Is.EqualTo(jumpKind));
        Assert.That(Emitter.emitReverseJumpKind(jumpKind), Is.EqualTo(reverseJumpKind));
    }

    [Test]
    public static void ConditionDescriptionsMatchNativeArmMapping()
    {
        var mappings = new (GenCondition.CodeKind Code, emitJumpKind JumpKind1, genTreeOps Oper, emitJumpKind JumpKind2)[]
        {
            (GenCondition.SLT, emitJumpKind.EJ_lt, GT_NONE, emitJumpKind.EJ_NONE),
            (GenCondition.SLE, emitJumpKind.EJ_le, GT_NONE, emitJumpKind.EJ_NONE),
            (GenCondition.SGE, emitJumpKind.EJ_ge, GT_NONE, emitJumpKind.EJ_NONE),
            (GenCondition.SGT, emitJumpKind.EJ_gt, GT_NONE, emitJumpKind.EJ_NONE),
            (GenCondition.S, emitJumpKind.EJ_mi, GT_NONE, emitJumpKind.EJ_NONE),
            (GenCondition.NS, emitJumpKind.EJ_pl, GT_NONE, emitJumpKind.EJ_NONE),
            (GenCondition.EQ, emitJumpKind.EJ_eq, GT_NONE, emitJumpKind.EJ_NONE),
            (GenCondition.NE, emitJumpKind.EJ_ne, GT_NONE, emitJumpKind.EJ_NONE),
            (GenCondition.ULT, emitJumpKind.EJ_lo, GT_NONE, emitJumpKind.EJ_NONE),
            (GenCondition.ULE, emitJumpKind.EJ_ls, GT_NONE, emitJumpKind.EJ_NONE),
            (GenCondition.UGE, emitJumpKind.EJ_hs, GT_NONE, emitJumpKind.EJ_NONE),
            (GenCondition.UGT, emitJumpKind.EJ_hi, GT_NONE, emitJumpKind.EJ_NONE),
            (GenCondition.C, emitJumpKind.EJ_hs, GT_NONE, emitJumpKind.EJ_NONE),
            (GenCondition.NC, emitJumpKind.EJ_lo, GT_NONE, emitJumpKind.EJ_NONE),
            (GenCondition.FEQ, emitJumpKind.EJ_eq, GT_NONE, emitJumpKind.EJ_NONE),
            (GenCondition.FNE, emitJumpKind.EJ_gt, GT_AND, emitJumpKind.EJ_lo),
            (GenCondition.FLT, emitJumpKind.EJ_lo, GT_NONE, emitJumpKind.EJ_NONE),
            (GenCondition.FLE, emitJumpKind.EJ_ls, GT_NONE, emitJumpKind.EJ_NONE),
            (GenCondition.FGE, emitJumpKind.EJ_ge, GT_NONE, emitJumpKind.EJ_NONE),
            (GenCondition.FGT, emitJumpKind.EJ_gt, GT_NONE, emitJumpKind.EJ_NONE),
            (GenCondition.O, emitJumpKind.EJ_vs, GT_NONE, emitJumpKind.EJ_NONE),
            (GenCondition.NO, emitJumpKind.EJ_vc, GT_NONE, emitJumpKind.EJ_NONE),
            (GenCondition.FEQU, emitJumpKind.EJ_eq, GT_OR, emitJumpKind.EJ_vs),
            (GenCondition.FNEU, emitJumpKind.EJ_ne, GT_NONE, emitJumpKind.EJ_NONE),
            (GenCondition.FLTU, emitJumpKind.EJ_lt, GT_NONE, emitJumpKind.EJ_NONE),
            (GenCondition.FLEU, emitJumpKind.EJ_le, GT_NONE, emitJumpKind.EJ_NONE),
            (GenCondition.FGEU, emitJumpKind.EJ_hs, GT_NONE, emitJumpKind.EJ_NONE),
            (GenCondition.FGTU, emitJumpKind.EJ_hi, GT_NONE, emitJumpKind.EJ_NONE),
        };

        ArmCalleeSavedRegisterTests.WithCodeGen((_, _) =>
        {
            foreach (var (code, jumpKind1, oper, jumpKind2) in mappings)
            {
                var description = GenConditionDesc.Get(new GenCondition(code));
                Assert.That(description.JumpKind1, Is.EqualTo(jumpKind1), code.ToString());
                Assert.That(description.Oper, Is.EqualTo(oper), code.ToString());
                Assert.That(description.JumpKind2, Is.EqualTo(jumpKind2), code.ToString());
            }
        });
    }

    [TestCase(INS_add, REG_R3, 7, INS_FLAGS_SET, INS_add, IF_T1_J0, 7, INS_FLAGS_SET)]
    [TestCase(INS_add, REG_R3, -7, INS_FLAGS_SET, INS_sub, IF_T1_J0, 7, INS_FLAGS_SET)]
    [TestCase(INS_add, REG_SP, 12, INS_FLAGS_NOT_SET, INS_add, IF_T1_F, 12, INS_FLAGS_NOT_SET)]
    [TestCase(INS_mov, REG_R8, 0x1234, INS_FLAGS_NOT_SET, INS_movw, IF_T2_N, 0x1234, INS_FLAGS_NOT_SET)]
    [TestCase(INS_cmp, REG_R8, -1, INS_FLAGS_SET, INS_cmp, IF_T2_L2, -1, INS_FLAGS_SET)]
    public static void RegisterImmediateInstructionRecordingUsesThumbFormats(
        instruction ins, regNumber reg, int immediate, insFlags flags, instruction expectedIns,
        Emitter.insFormat expectedFormat, int expectedImmediate, insFlags expectedFlags)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((_, codeGen) =>
        {
            RecordArm32Instructions(() => codeGen.Emitter.emitIns_R_I(ins, EA_4BYTE, reg, immediate, flags));

            var descriptor = LastInstruction(codeGen.Emitter)
                ?? throw new AssertionException("No register-immediate instruction was recorded.");
            Assert.That(descriptor.idIns(), Is.EqualTo(expectedIns));
            Assert.That(descriptor.idInsFmt(), Is.EqualTo(expectedFormat));
            Assert.That(codeGen.Emitter.emitGetInsSC(descriptor), Is.EqualTo((nint)expectedImmediate));
            Assert.That(descriptor.idInsFlags(), Is.EqualTo(expectedFlags));
            Assert.That(descriptor.idReg1(), Is.EqualTo(reg));
        });
    }

    [TestCase(INS_mov, REG_R2, REG_R3, EA_4BYTE, INS_FLAGS_SET, IF_T1_E, INS_FLAGS_SET)]
    [TestCase(INS_mov, REG_R8, REG_R9, EA_4BYTE, INS_FLAGS_NOT_SET, IF_T1_D0, INS_FLAGS_NOT_SET)]
    [TestCase(INS_mov, REG_R8, REG_R9, EA_4BYTE, INS_FLAGS_SET, IF_T2_C3, INS_FLAGS_SET)]
    [TestCase(INS_vmov, REG_F2, REG_F4, EA_8BYTE, INS_FLAGS_DONT_CARE, IF_T2_VFP2, INS_FLAGS_NOT_SET)]
    [TestCase(INS_vmov, REG_F2, REG_F0, EA_4BYTE, INS_FLAGS_DONT_CARE, IF_T2_VFP2, INS_FLAGS_NOT_SET)]
    [TestCase(INS_vmov_i2f, REG_F1, REG_R3, EA_4BYTE, INS_FLAGS_DONT_CARE, IF_T2_VMOVS, INS_FLAGS_NOT_SET)]
    [TestCase(INS_sxtb, REG_R2, REG_R3, EA_4BYTE, INS_FLAGS_NOT_SET, IF_T1_E, INS_FLAGS_NOT_SET)]
    public static void RegisterMoveRecordingUsesNativeThumbFormats(
        instruction ins, regNumber dstReg, regNumber srcReg, emitAttr attr, insFlags flags,
        Emitter.insFormat expectedFormat, insFlags expectedFlags)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((_, codeGen) =>
        {
            Assert.That(
                codeGen.Emitter.emitIns_Mov(ins, attr, dstReg, srcReg, canSkip: false, flags: flags),
                Is.True);

            var descriptor = LastInstruction(codeGen.Emitter)
                ?? throw new AssertionException("No register move was recorded.");
            Assert.That(descriptor.idIns(), Is.EqualTo(ins));
            Assert.That(descriptor.idInsFmt(), Is.EqualTo(expectedFormat));
            Assert.That(descriptor.idInsFlags(), Is.EqualTo(expectedFlags));
            Assert.That(descriptor.idReg1(), Is.EqualTo(dstReg));
            Assert.That(descriptor.idReg2(), Is.EqualTo(srcReg));
        });
    }

    [Test]
    public static void RegisterMoveRecordingHonorsElisionRequests()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((_, codeGen) =>
        {
            Assert.That(
                codeGen.Emitter.emitIns_Mov(
                    INS_mov, EA_4BYTE, REG_R2, REG_R2, canSkip: true, flags: INS_FLAGS_NOT_SET),
                Is.False);
            Assert.That(LastInstruction(codeGen.Emitter), Is.Null);
        });
    }

    [TestCase(INS_cmp, REG_R3, REG_R4, IF_T1_E, INS_FLAGS_SET)]
    [TestCase(INS_clz, REG_R8, REG_R9, IF_T2_C10, INS_FLAGS_NOT_SET)]
    public static void RegisterRegisterInstructionRecordingUsesThumbFormats(
        instruction ins, regNumber reg1, regNumber reg2, Emitter.insFormat expectedFormat, insFlags expectedFlags)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((_, codeGen) =>
        {
            RecordArm32Instructions(() => codeGen.Emitter.emitIns_R_R(ins, EA_4BYTE, reg1, reg2));

            var descriptor = LastInstruction(codeGen.Emitter)
                ?? throw new AssertionException("No register-register instruction was recorded.");
            Assert.That(descriptor.idIns(), Is.EqualTo(ins));
            Assert.That(descriptor.idInsFmt(), Is.EqualTo(expectedFormat));
            Assert.That(descriptor.idInsFlags(), Is.EqualTo(expectedFlags));
            Assert.That(descriptor.idReg1(), Is.EqualTo(reg1));
            Assert.That(descriptor.idReg2(), Is.EqualTo(reg2));
        });
    }

    [Test]
    public static void FlagsAwareRegisterRegisterRecordingPreservesRequestedFlags()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((_, codeGen) =>
        {
            RecordArm32Instructions(() =>
                codeGen.Emitter.emitIns_R_R(INS_cmp, EA_4BYTE, REG_R3, REG_R4, INS_FLAGS_SET));

            var descriptor = LastInstruction(codeGen.Emitter)
                ?? throw new AssertionException("No flags-aware register-register instruction was recorded.");
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_cmp));
            Assert.That(descriptor.idInsFmt(), Is.EqualTo(IF_T1_E));
            Assert.That(descriptor.idInsFlags(), Is.EqualTo(INS_FLAGS_SET));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R3));
            Assert.That(descriptor.idReg2(), Is.EqualTo(REG_R4));
        });
    }

    [TestCase(INS_add, REG_R0, REG_R1, REG_R2, IF_T1_H, INS_FLAGS_SET)]
    [TestCase(INS_vadd, REG_F0, REG_F1, REG_F2, IF_T2_VFP3, INS_FLAGS_NOT_SET)]
    public static void ThreeRegisterInstructionRecordingUsesThumbFormats(
        instruction ins, regNumber reg1, regNumber reg2, regNumber reg3,
        Emitter.insFormat expectedFormat, insFlags expectedFlags)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((_, codeGen) =>
        {
            RecordArm32Instructions(() =>
                codeGen.Emitter.emitIns_R_R_R(ins, EA_4BYTE, reg1, reg2, reg3));

            var descriptor = LastInstruction(codeGen.Emitter)
                ?? throw new AssertionException("No three-register instruction was recorded.");
            Assert.That(descriptor.idIns(), Is.EqualTo(ins));
            Assert.That(descriptor.idInsFmt(), Is.EqualTo(expectedFormat));
            Assert.That(descriptor.idInsFlags(), Is.EqualTo(expectedFlags));
            Assert.That(descriptor.idReg1(), Is.EqualTo(reg1));
            Assert.That(descriptor.idReg2(), Is.EqualTo(reg2));
            Assert.That(descriptor.idReg3(), Is.EqualTo(reg3));
        });
    }

    [TestCase(INS_add, REG_R0, REG_R1, REG_R2, 1, INS_FLAGS_SET, INS_OPTS_LSL, IF_T2_C0, INS_FLAGS_SET)]
    [TestCase(INS_ldr, REG_R0, REG_R1, REG_R2, 0, INS_FLAGS_NOT_SET, INS_OPTS_NONE, IF_T1_H, INS_FLAGS_NOT_SET)]
    public static void ThreeRegisterImmediateInstructionRecordingPreservesShiftAndFormat(
        instruction ins, regNumber reg1, regNumber reg2, regNumber reg3, int immediate, insFlags flags,
        insOpts opt, Emitter.insFormat expectedFormat, insFlags expectedFlags)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((_, codeGen) =>
        {
            RecordArm32Instructions(() =>
                codeGen.Emitter.emitIns_R_R_R_I(ins, EA_4BYTE, reg1, reg2, reg3, immediate, flags, opt));

            var descriptor = LastInstruction(codeGen.Emitter)
                ?? throw new AssertionException("No three-register-immediate instruction was recorded.");
            Assert.That(descriptor.idIns(), Is.EqualTo(ins));
            Assert.That(descriptor.idInsFmt(), Is.EqualTo(expectedFormat));
            Assert.That(codeGen.Emitter.emitGetInsSC(descriptor), Is.EqualTo((nint)immediate));
            Assert.That(descriptor.idInsFlags(), Is.EqualTo(expectedFlags));
            Assert.That(descriptor.idInsOpt(), Is.EqualTo(opt));
            Assert.That(descriptor.idReg1(), Is.EqualTo(reg1));
            Assert.That(descriptor.idReg2(), Is.EqualTo(reg2));
            Assert.That(descriptor.idReg3(), Is.EqualTo(reg3));
        });
    }

    [TestCase(INS_smull, IF_T2_F1)]
    [TestCase(INS_umull, IF_T2_F1)]
    [TestCase(INS_smlal, IF_T2_F1)]
    [TestCase(INS_umlal, IF_T2_F1)]
    [TestCase(INS_mla, IF_T2_F2)]
    [TestCase(INS_mls, IF_T2_F2)]
    public static void FourRegisterMultiplyInstructionRecordingUsesNativeThumbFormats(
        instruction ins, Emitter.insFormat expectedFormat)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((_, codeGen) =>
        {
            RecordArm32Instructions(() =>
                codeGen.Emitter.emitIns_R_R_R_R(ins, EA_4BYTE, REG_R0, REG_R1, REG_R2, REG_R3));

            var descriptor = LastInstruction(codeGen.Emitter)
                ?? throw new AssertionException("No four-register instruction was recorded.");
            Assert.That(descriptor.idIns(), Is.EqualTo(ins));
            Assert.That(descriptor.idInsFmt(), Is.EqualTo(expectedFormat));
            Assert.That(descriptor.idInsFlags(), Is.EqualTo(INS_FLAGS_NOT_SET));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R0));
            Assert.That(descriptor.idReg2(), Is.EqualTo(REG_R1));
            Assert.That(descriptor.idReg3(), Is.EqualTo(REG_R2));
            Assert.That(descriptor.idReg4(), Is.EqualTo(REG_R3));
        });
    }

    [Test]
    public static void LargeLoadStoreImmediateStopsAtReservedRegisterDependency()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((_, codeGen) =>
        {
            var failure = Assert.Throws<FatalJitException>(() =>
                codeGen.Emitter.emitIns_R_R_I(
                    INS_ldr, EA_4BYTE, REG_R0, REG_R1, 0x1000, INS_FLAGS_NOT_SET));

            Assert.That(failure?.Message, Is.EqualTo("ARM32 reserved-register selection is not ported."));
        });
    }

    [TestCase(INS_ldr, false)]
    [TestCase(INS_str, true)]
    public static void LocalStackLoadAndStoreRecordingPreservesThumbOffset(
        instruction ins, bool store)
    {
        WithLocalStackCodeGen((_, codeGen) =>
        {
            RecordArm32Instructions(() =>
            {
                if (store)
                {
                    codeGen.Emitter.emitIns_S_R(ins, EA_4BYTE, REG_R3, 0, 0);
                }
                else
                {
                    codeGen.Emitter.emitIns_R_S(ins, EA_4BYTE, REG_R3, 0, 0);
                }
            });

            var descriptor = LastInstruction(codeGen.Emitter)
                ?? throw new AssertionException("No local-stack instruction was recorded.");
            Assert.That(descriptor.idIns(), Is.EqualTo(ins));
            Assert.That(descriptor.idInsFmt(), Is.EqualTo(IF_T2_H0));
            Assert.That(descriptor.idInsFlags(), Is.EqualTo(INS_FLAGS_NOT_SET));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R3));
            Assert.That(descriptor.idReg2(), Is.EqualTo(REG_SPBASE));
            Assert.That(codeGen.Emitter.emitGetInsSC(descriptor), Is.EqualTo((nint)(-16)));
        });
    }

    [TestCase(TYP_BYREF)]
    [TestCase(TYP_I_IMPL)]
    public static void LocalAddressesRecordTheTargetRegisterAndOffset(var_types type)
    {
        WithLocalStackCodeGen((_, codeGen) =>
        {
            var tree = new GenTreeLclFld(GT_LCL_ADDR, type, 0, 12)
            {
                RegNum = REG_R0,
            };

            RecordArm32Instructions(() => codeGen.genCodeForLclAddr(tree));
            var descriptor = LastInstruction(codeGen.Emitter)
                ?? throw new AssertionException("No local-address instruction was recorded.");
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_add));
            Assert.That(descriptor.idInsFmt(), Is.EqualTo(IF_T1_J2));
            Assert.That(descriptor.idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R0));
            Assert.That(descriptor.idReg2(), Is.EqualTo(REG_SPBASE));
            Assert.That(descriptor.idIsLclVar(), Is.True);
            Assert.That(descriptor.idAddr().iiaLclVar.lvaVarNum(), Is.Zero);
            Assert.That(descriptor.idAddr().iiaLclVar.lvaOffset(), Is.EqualTo(12u));
            Assert.That(codeGen.Emitter.emitGetInsSC(descriptor), Is.EqualTo((nint)28));
        }, stackOffset: 16);
    }

    [Test]
    public static void LargeLocalStackLoadStopsAtReservedRegisterDependency()
    {
        WithLocalStackCodeGen((_, codeGen) =>
        {
            var failure = Assert.Throws<FatalJitException>(() =>
                codeGen.Emitter.emitIns_R_S(INS_ldr, EA_4BYTE, REG_R3, 0, 0));

            Assert.That(failure?.Message, Is.EqualTo("ARM32 reserved-register selection is not ported."));
        }, stackOffset: -0x1000);
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void RegisterAddressInstructionsRouteThroughThumbRecorders(bool store)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((_, codeGen) =>
        {
            RecordArm32Instructions(() =>
            {
                if (store)
                {
                    codeGen.Emitter.emitIns_AR_R(INS_str, EA_4BYTE, REG_R3, REG_R1, 4);
                }
                else
                {
                    codeGen.Emitter.emitIns_R_AR(INS_ldr, EA_4BYTE, REG_R3, REG_R1, 4);
                }
            });

            var descriptor = LastInstruction(codeGen.Emitter)
                ?? throw new AssertionException("No register/address instruction was recorded.");
            Assert.That(descriptor.idIns(), Is.EqualTo(store ? INS_str : INS_ldr));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R3));
            Assert.That(descriptor.idReg2(), Is.EqualTo(REG_R1));
            Assert.That(codeGen.Emitter.emitGetInsSC(descriptor), Is.EqualTo((nint)4));
        });
    }

    [Test]
    public static void IndexedAddressInstructionsPreserveRegisterAndShiftOperands()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((_, codeGen) =>
        {
            RecordArm32Instructions(() =>
                codeGen.Emitter.emitIns_R_ARX(INS_ldr, EA_4BYTE, REG_R0, REG_R1, REG_R2, 1, 0));

            var descriptor = LastInstruction(codeGen.Emitter)
                ?? throw new AssertionException("No indexed-address instruction was recorded.");
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_ldr));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R0));
            Assert.That(descriptor.idReg2(), Is.EqualTo(REG_R1));
            Assert.That(descriptor.idReg3(), Is.EqualTo(REG_R2));
            Assert.That(descriptor.idInsOpt(), Is.EqualTo(INS_OPTS_LSL));
            Assert.That(codeGen.Emitter.emitGetInsSC(descriptor), Is.EqualTo((nint)0));
        });
    }

    [Test]
    public static void UnscaledIndexedAddressLoadPreservesTheNoShiftForm()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((_, codeGen) =>
        {
            RecordArm32Instructions(() =>
                codeGen.Emitter.emitIns_R_ARR(INS_ldr, EA_4BYTE, REG_R0, REG_R1, REG_R2, 0));

            var descriptor = LastInstruction(codeGen.Emitter)
                ?? throw new AssertionException("No unscaled indexed-address instruction was recorded.");
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_ldr));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R0));
            Assert.That(descriptor.idReg2(), Is.EqualTo(REG_R1));
            Assert.That(descriptor.idReg3(), Is.EqualTo(REG_R2));
            Assert.That(descriptor.idInsOpt(), Is.EqualTo(INS_OPTS_NONE));
        });
    }

    [Test]
    public static void BitFieldClearCombinesTheLeastAndMostSignificantBits()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((_, codeGen) =>
        {
            RecordArm32Instructions(() =>
                codeGen.Emitter.emitIns_R_I_I(INS_bfc, EA_4BYTE, REG_R5, 8, 8, INS_FLAGS_NOT_SET));

            var descriptor = LastInstruction(codeGen.Emitter)
                ?? throw new AssertionException("No bit-field instruction was recorded.");
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_bfc));
            Assert.That(descriptor.idInsFmt(), Is.EqualTo(IF_T2_D1));
            Assert.That(codeGen.Emitter.emitGetInsSC(descriptor), Is.EqualTo((nint)271));
            Assert.That(descriptor.idInsFlags(), Is.EqualTo(INS_FLAGS_NOT_SET));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R5));
        }, captureAssertions: true);
    }

    [Test]
    public static void RelocatableThumbMoveRetainsTheHostAddress()
    {
        const nuint address = 0x12345678;

        ArmCalleeSavedRegisterTests.WithCodeGen((_, codeGen) =>
        {
            RecordArm32Instructions(() =>
                codeGen.Emitter.emitIns_MovRelocatableImmediate(
                    INS_movw, EA_HANDLE_CNS_RELOC, REG_R4, address));

            var descriptor = LastInstruction(codeGen.Emitter)
                ?? throw new AssertionException("No relocatable instruction was recorded.");
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_movw));
            Assert.That(descriptor.idInsFmt(), Is.EqualTo(IF_T2_N3));
            Assert.That(descriptor.idInsFlags(), Is.EqualTo(INS_FLAGS_NOT_SET));
            Assert.That((nuint)RelocationValue(codeGen.Emitter, descriptor), Is.EqualTo(address));
        }, captureAssertions: true);
    }

    [TestCase(INS_dmb, IF_T2_B, 15, 15)]
    [TestCase(INS_push, IF_T1_L1, 12, 48)]
    [TestCase(INS_pop, IF_T2_I1, 0x4001, 5)]
    public static void ImmediateOnlyInstructionRecordingPreservesThumbFormatAndMask(
        instruction ins, Emitter.insFormat expectedFormat, int immediate, int expectedEncoding)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((_, codeGen) =>
        {
            RecordArm32Instructions(() => codeGen.Emitter.emitIns_I(ins, EA_4BYTE, immediate));

            var descriptor = LastInstruction(codeGen.Emitter)
                ?? throw new AssertionException("No immediate-only instruction was recorded.");
            Assert.That(descriptor.idIns(), Is.EqualTo(ins));
            Assert.That(descriptor.idInsFmt(), Is.EqualTo(expectedFormat));
            Assert.That(descriptor.idInsSize(), Is.EqualTo(
                expectedFormat < IF_T2_A ? ISZ_16BIT : ISZ_32BIT));
            Assert.That(codeGen.Emitter.emitGetInsSC(descriptor), Is.EqualTo((nint)expectedEncoding));
        });
    }

    [Test]
    public static void SingleHighRegisterPushUsesThumb2RegisterEncoding()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((_, codeGen) =>
        {
            RecordArm32Instructions(() => codeGen.Emitter.emitIns_I(INS_push, EA_4BYTE, 1 << (int)REG_R8));

            var descriptor = LastInstruction(codeGen.Emitter)
                ?? throw new AssertionException("No high-register push was recorded.");
            Assert.That(descriptor.idInsFmt(), Is.EqualTo(IF_T2_E2));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R8));
        });
    }

    [TestCase(INS_rsb, REG_R7, IF_T1_E, INS_FLAGS_SET)]
    [TestCase(INS_rsb, REG_R8, IF_T2_L0, INS_FLAGS_NOT_SET)]
    [TestCase(INS_mvn, REG_R7, IF_T1_E, INS_FLAGS_SET)]
    [TestCase(INS_mvn, REG_R8, IF_T2_C1, INS_FLAGS_NOT_SET)]
    public static void SingleRegisterRsbAndMvnUseNativeThumbFormats(
        instruction ins, regNumber reg, Emitter.insFormat expectedFormat, insFlags expectedFlags)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((_, codeGen) =>
        {
            RecordArm32Instructions(() => codeGen.Emitter.emitIns_R(ins, EA_4BYTE, reg));

            var descriptor = LastInstruction(codeGen.Emitter)
                ?? throw new AssertionException("No single-register instruction was recorded.");
            Assert.That(descriptor.idIns(), Is.EqualTo(ins));
            Assert.That(descriptor.idInsFmt(), Is.EqualTo(expectedFormat));
            Assert.That(descriptor.idInsFlags(), Is.EqualTo(expectedFlags));
            Assert.That(descriptor.idReg1(), Is.EqualTo(reg));
            Assert.That(descriptor.idReg2(), Is.EqualTo(reg));
        });
    }

    private static void RecordArm32Instructions(TestDelegate action)
    {
#if DEBUG
        try
        {
            action();
        }
        catch (FatalJitException failure) when (failure.Result == CorJitResult.CORJIT_SKIPPED)
        {
        }
#else
        action();
#endif
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? LastInstruction(Emitter emitter);

    private static void WithLocalStackCodeGen(Action<Compiler, CodeGen> action, int stackOffset = -16)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.compFuncInfos = [new FuncInfoDsc { funKind = FuncKind.FUNC_ROOT }];
            compiler.compFuncInfoCount = 1;
            compiler.compCurrFuncIdx = 0;
            compiler.fgFuncletsCreated = true;
            compiler.lvaCount = 1;
            compiler.lvaTable =
            [
                new LclVarDsc
                {
                    Type = TYP_INT,
                    lvOnFrame = true,
                    lvFramePointerBased = false,
                    StackOffset = stackOffset,
                },
            ];
            compiler.lvaDoneFrameLayout = Compiler.REGALLOC_FRAME_LAYOUT;
            compiler.lvaOutgoingArgSpaceVar = BAD_VAR_NUM;
            codeGen.IsFramePointerUsed = false;
            action(compiler, codeGen);
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitGetInsRelocValue")]
    private static extern byte* RelocationValue(Emitter emitter, Emitter.instrDesc instruction);

    [Test]
    public static void NonLocalJumpRetainsTheNativeUnsupportedBoundary()
    {
        WithCodeGen((_, codeGen) =>
        {
            var value = new GenTree(GT_FTN_ENTRY, TYP_I_IMPL);
            var tree = new GenTreeUnOp(GT_NONLOCAL_JMP, TYP_VOID, value);

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genNonLocalJmp(tree));

            Assert.That(failure?.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
        });
    }

    [Test]
    public static void FunctionEntryRetainsTheNativeUnsupportedBoundary()
    {
        WithCodeGen((_, codeGen) =>
        {
            var tree = new GenTree(GT_FTN_ENTRY, TYP_I_IMPL);

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genFtnEntry(tree));

            Assert.That(failure?.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
        });
    }

    private static void WithCodeGen(Action<Compiler, CodeGen> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.compCurLife = VarSetOps.MakeEmpty(compiler);
        JitTls.Compiler = compiler;
        try
        {
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            action(compiler, codeGen);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}

[NonParallelizable]
internal static unsafe class Arm32CkfiniteCodeGenTests
{
    [TestCase(NI_System_Math_Abs)]
    [TestCase(NI_System_Math_Sqrt)]
    public static void FloatingMathIntrinsicsUseArm32UnaryEmitters(NamedIntrinsic intrinsic)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((_, codeGen) =>
        {
            var source = new GenTreePhysReg(REG_F0, TYP_FLOAT) { RegNum = REG_F0 };
            var tree = new GenTreeIntrinsic(TYP_FLOAT, source, intrinsic, methodHandle: null)
            {
                RegNum = REG_F1,
            };

            RecordArm32Instructions(() => codeGen.genIntrinsic(tree));

            var descriptor = LastInstruction(codeGen.Emitter)
                ?? throw new AssertionException("No floating-point intrinsic instruction was recorded.");
            Assert.That(descriptor.idIns(), Is.EqualTo(
                intrinsic == NI_System_Math_Abs ? INS_vabs : INS_vsqrt));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_F1));
            Assert.That(descriptor.idReg2(), Is.EqualTo(REG_F0));
            Assert.That(descriptor.idOpSize(), Is.EqualTo(EA_4BYTE));
        });
    }

    [TestCase(NI_PRIMITIVE_SaturateToInt8, INS_ssat, 7)]
    [TestCase(NI_PRIMITIVE_SaturateToInt16, INS_ssat, 15)]
    [TestCase(NI_PRIMITIVE_SaturateToUInt8, INS_usat, 8)]
    [TestCase(NI_PRIMITIVE_SaturateToUInt16, INS_usat, 16)]
    public static void SaturationIntrinsicsRetainOpcodeAndWidth(
        NamedIntrinsic intrinsic, instruction expectedInstruction, int expectedEncoding)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((_, codeGen) =>
        {
            var source = new GenTreePhysReg(REG_R3, TYP_INT) { RegNum = REG_R3 };
            var tree = new GenTreeIntrinsic(TYP_INT, source, intrinsic, methodHandle: null)
            {
                RegNum = REG_R2,
            };

            RecordArm32Instructions(() => codeGen.genIntrinsic(tree));

            var descriptor = LastInstruction(codeGen.Emitter)
                ?? throw new AssertionException("No saturation instruction was recorded.");
            Assert.That(descriptor.idIns(), Is.EqualTo(expectedInstruction));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R2));
            Assert.That(descriptor.idReg2(), Is.EqualTo(REG_R3));
            Assert.That(codeGen.Emitter.emitGetInsSC(descriptor), Is.EqualTo((nint)expectedEncoding));
        });
    }

    [TestCase(INS_bfi, 1, 8, 40)]
    [TestCase(INS_bfi, 0, 32, 31)]
    [TestCase(INS_sbfx, 23, 8, 743)]
    [TestCase(INS_ubfx, 20, 11, 650)]
    [TestCase(INS_ssat, 0, 1, 0)]
    [TestCase(INS_ssat, 0, 32, 31)]
    [TestCase(INS_usat, 0, 0, 0)]
    [TestCase(INS_usat, 0, 31, 31)]
    public static void TwoRegisterTwoImmediateRecordingPreservesThumb2Encoding(
        instruction ins, int imm1, int imm2, int encoded)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((_, codeGen) =>
        {
            RecordArm32Instructions(() =>
                codeGen.Emitter.emitIns_R_R_I_I(ins, EA_4BYTE, REG_R2, REG_R3, imm1, imm2));

            var descriptor = LastInstruction(codeGen.Emitter)
                ?? throw new AssertionException("No two-immediate instruction was recorded.");
            Assert.That(descriptor.idIns(), Is.EqualTo(ins));
            Assert.That(descriptor.idInsFmt(), Is.EqualTo(IF_T2_D0));
            Assert.That(descriptor.idInsSize(), Is.EqualTo(ISZ_32BIT));
            Assert.That(descriptor.idInsFlags(), Is.EqualTo(INS_FLAGS_NOT_SET));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R2));
            Assert.That(descriptor.idReg2(), Is.EqualTo(REG_R3));
            Assert.That(codeGen.Emitter.emitGetInsSC(descriptor), Is.EqualTo((nint)encoded));
#if !DEBUG
            Assert.That(GroupSize(codeGen.Emitter), Is.EqualTo(4));
#endif
        });
    }

    private static void RecordArm32Instructions(TestDelegate action)
    {
#if DEBUG
        try
        {
            action();
        }
        catch (FatalJitException failure) when (failure.Result == CorJitResult.CORJIT_SKIPPED)
        {
        }
#else
        action();
#endif
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? LastInstruction(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGsize")]
    private static extern ref int GroupSize(Emitter emitter);
}
#endif
