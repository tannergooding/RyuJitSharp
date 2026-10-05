// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if FEATURE_HW_INTRINSICS && TARGET_ARM64
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using static RyuJitSharp.UnitTests.Arm64HardwareIntrinsicCodegenTests;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class Arm64HardwareIntrinsicImmediateTests
{
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(31)]
    public static void ContainedShiftEmitsOnlyOneCase(int shift)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var node = new GenTreeHWIntrinsic(TYP_SIMD16, NI_AdvSimd_ShiftLeftLogical, TYP_INT, 16,
                Operand(REG_V1, TYP_SIMD16), Immediate(shift)) { RegNum = REG_V0 };
            EnableIsa(compiler, node);
            codeGen.genHWIntrinsic(node);

            var id = SingleDescriptor(codeGen);
            Assert.That(id.idIns(), Is.EqualTo(INS_shl));
            Assert.That(id.idOpSize(), Is.EqualTo(EA_16BYTE));
            Assert.That(id.idInsOpt(), Is.EqualTo(INS_OPTS_4S));
            Assert.That(id.idReg1(), Is.EqualTo(REG_V0));
            Assert.That(id.idReg2(), Is.EqualTo(REG_V1));
            Assert.That(id.idSmallCns(), Is.EqualTo(shift));
        });
    }

    [TestCase(1)]
    [TestCase(32)]
    public static void RightShiftRetainsNonzeroNativeEndpoints(int shift)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var node = new GenTreeHWIntrinsic(TYP_SIMD16, NI_AdvSimd_ShiftRightLogical, TYP_UINT, 16,
                Operand(REG_V1, TYP_SIMD16), Immediate(shift)) { RegNum = REG_V0 };
            EnableIsa(compiler, node);
            codeGen.genHWIntrinsic(node);

            var id = SingleDescriptor(codeGen);
            Assert.That(id.idIns(), Is.EqualTo(INS_ushr));
            Assert.That(id.idSmallCns(), Is.EqualTo(shift));
        });
    }

    [Test]
    public static void NonconstantShiftTerminatesAtRealUnportedAddressRecorderBeforeCaseEmission()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var immediate = Operand(REG_R12, TYP_INT);
            var node = new GenTreeHWIntrinsic(TYP_SIMD16, NI_AdvSimd_ShiftLeftLogical, TYP_INT, 16,
                Operand(REG_V1, TYP_SIMD16), immediate) { RegNum = REG_V0 };
            EnableIsa(compiler, node);
            codeGen.InternalRegisters.Add(node, regMaskTP.CreateFromRegNum(REG_R13, REG_R13.SingleTypeMask));

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genHWIntrinsic(node)) ??
                throw new AssertionException("Missing address-recorder dependency failure.");
            Assert.That(failure.Message, Is.EqualTo("ARM64 emitIns_R_L recording is not ported."));
            Assert.That(Descriptors(codeGen), Is.Empty);
        });
    }

    [Test]
    public static void TwoCaseIndexedImmediateRecordsConditionalBranchWithoutAnInternalAddressRegister()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var node = new GenTreeHWIntrinsic(TYP_LONG, NI_AdvSimd_Extract, TYP_LONG, 16,
                Operand(REG_V1, TYP_SIMD16), Operand(REG_R12, TYP_INT)) { RegNum = REG_R0 };
            EnableIsa(compiler, node);

            codeGen.genHWIntrinsic(node);

            var descriptors = AllDescriptors(codeGen.Emitter, compiler);
            Assert.That(descriptors, Is.Not.Empty);
            var branch = (Emitter.instrDescJmp)descriptors[0];
            Assert.That(branch.idIns(), Is.EqualTo(INS_cbnz));
            Assert.That(branch.idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(branch.idReg1(), Is.EqualTo(REG_R12));
            Assert.That(branch.idjTarget, Is.Not.Null);
        });
    }

    private static List<Emitter.instrDesc> AllDescriptors(Emitter emitter, Compiler compiler)
    {
        var descriptors = new List<Emitter.instrDesc>();
        var firstGroup = FirstGroup(emitter);
        while ((firstGroup is not null) && (firstGroup.igInsCnt == 0))
        {
            firstGroup = firstGroup.igNext;
        }
        var firstInstructionGroup = firstGroup ?? throw new AssertionException("Missing instruction group.");
        Walk(emitter, new emitLocation(firstInstructionGroup),
            (descriptor, _) => descriptors.Add(descriptor), compiler);

        return descriptors;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitWalkIDs")]
    private static extern void Walk(Emitter emitter, emitLocation location,
        Action<Emitter.instrDesc, Compiler> process, Compiler context);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitIGlist")]
    private static extern ref insGroup? FirstGroup(Emitter emitter);

    [TestCase(1, 0)]
    [TestCase(16, 31)]
    public static void ConstantCountArgumentsReachTypedPatternRecorderWithoutFakeEmission(int scale, int pattern)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var node = new GenTreeHWIntrinsic(TYP_SIMD, NI_Sve_SaturatingIncrementBy32BitElementCount,
                TYP_INT, 0, Operand(REG_V1, TYP_SIMD), Immediate(scale), Immediate(pattern)) {
                RegNum = REG_V1
            };
            EnableIsa(compiler, node);

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genHWIntrinsic(node)) ??
                throw new AssertionException("Missing pattern-recorder dependency failure.");
            Assert.That(failure.Message, Is.EqualTo("ARM64 emitIns_R_R_PATTERN_I recording is not ported."));
            Assert.That(Descriptors(codeGen), Is.Empty);
        });
    }

    [Test]
    public static void CombinedDynamicCountPreservesOperandMutationBeforeAddressBoundary()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var node = new GenTreeHWIntrinsic(TYP_SIMD, NI_Sve_SaturatingIncrementBy32BitElementCount,
                TYP_INT, 0, Operand(REG_V1, TYP_SIMD), Operand(REG_R12, TYP_INT), Operand(REG_R14, TYP_INT)) {
                RegNum = REG_V1
            };
            EnableIsa(compiler, node);
            codeGen.InternalRegisters.Add(node, regMaskTP.CreateFromRegNum(REG_R13, REG_R13.SingleTypeMask));

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genHWIntrinsic(node)) ??
                throw new AssertionException("Missing address-recorder dependency failure.");
            Assert.That(failure.Message, Is.EqualTo("ARM64 emitIns_R_L recording is not ported."));
            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(3));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_sub));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_R12));
            Assert.That(descriptors[1].idReg1(), Is.EqualTo(REG_R14));
            Assert.That(descriptors[2].idIns(), Is.EqualTo(INS_orr));
            Assert.That(descriptors[2].idReg1(), Is.EqualTo(REG_R12));
            Assert.That(descriptors[2].idReg2(), Is.EqualTo(REG_R12));
            Assert.That(descriptors[2].idReg3(), Is.EqualTo(REG_R14));
        });
    }

    [TestCase(NI_Sve2_MultiplyAddRotateComplexBySelectedScalar, TYP_SHORT)]
    [TestCase(NI_Sve2_DotProductRotateComplexBySelectedIndex, TYP_BYTE)]
    [TestCase(NI_Sve2_MultiplyAddRotateComplexBySelectedScalar, TYP_INT)]
    public static void CombinedComplexArgumentsKeepBothMutationsBeforeAddressBoundary(
        NamedIntrinsic intrinsic, var_types baseType)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var node = new GenTreeHWIntrinsic(TYP_SIMD, intrinsic, baseType, 0,
                Operand(REG_V1, TYP_SIMD), Operand(REG_V2, TYP_SIMD), Operand(REG_V3, TYP_SIMD),
                Operand(REG_R12, TYP_INT), Operand(REG_R14, TYP_INT)) { RegNum = REG_V1 };
            EnableIsa(compiler, node);
            codeGen.InternalRegisters.Add(node, regMaskTP.CreateFromRegNum(REG_R13, REG_R13.SingleTypeMask));

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genHWIntrinsic(node)) ??
                throw new AssertionException("Missing address-recorder dependency failure.");
            Assert.That(failure.Message, Is.EqualTo("ARM64 emitIns_R_L recording is not ported."));
            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(2));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_R14));
            Assert.That(descriptors[1].idIns(), Is.EqualTo(INS_orr));
            Assert.That(descriptors[1].idReg1(), Is.EqualTo(REG_R12));
        });
    }
}
#endif
