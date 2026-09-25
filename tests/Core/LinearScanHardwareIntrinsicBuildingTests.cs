// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.Globals;
using static RyuJitSharp.LsraGlobals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.regMask;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LinearScanHardwareIntrinsicBuildingTests
{
    [Test]
    public static void ZeroOperandFenceHasNoSourcesOrResults()
    {
        WithAllocator((_, allocator) => {
            var fence = new GenTreeHWIntrinsic(
                TYP_VOID, NI_X86Base_MemoryFence, TYP_UNKNOWN, 0);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildHWIntrinsic(allocator, fence, out var destinations), Is.Zero);
            Assert.That(destinations, Is.Zero);
            Assert.That(allocator.refPositions, Is.Empty);
        });
    }

    [TestCase(NI_X86Base_DivRem)]
    [TestCase(NI_X86Base_X64_DivRem)]
    public static void DivRemUsesFixedDividendAndResultRegisters(NamedIntrinsic id)
    {
        WithAllocator((compiler, allocator) => {
            var operandType = id is NI_X86Base_DivRem ? TYP_INT : TYP_LONG;
            var low = compiler.gtNewIconNode(operandType, 5);
            var high = compiler.gtNewIconNode(operandType, 0);
            var divisor = compiler.gtNewIconNode(operandType, 3);
            ReferenceBuildLocation(allocator) = 2;
            var lowDef = BuildDef(allocator, low, SRBM_NONE, 0);
            var highDef = BuildDef(allocator, high, SRBM_NONE, 0);
            var divisorDef = BuildDef(allocator, divisor, SRBM_NONE, 0);
            var intrinsic = new GenTreeHWIntrinsic(TYP_STRUCT, id, operandType, 0, low, high, divisor);
            Assert.That(intrinsic.IsRmwHWIntrinsic(compiler), Is.True);
            Assert.That(intrinsic.IsValue, Is.True);
            Assert.That(HWIntrinsicInfo.GetMultiRegCount(id), Is.EqualTo(2));
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildHWIntrinsic(allocator, intrinsic, out var destinations), Is.EqualTo(3));
            Assert.That(destinations, Is.EqualTo(2));
            Assert.That(lowDef.nextRefPosition?.registerAssignment, Is.EqualTo(SRBM_RAX));
            Assert.That(highDef.nextRefPosition?.registerAssignment, Is.EqualTo(SRBM_RDX));
            Assert.That(divisorDef.nextRefPosition?.delayRegFree, Is.True);
            regMask[] expectedRegisters = [SRBM_RAX, SRBM_RDX];
            Assert.That(allocator.refPositions.Where(reference =>
                reference.treeNode == intrinsic && reference.refType == RefType.RefTypeDef)
                .Select(reference => reference.registerAssignment),
                Is.EqualTo(expectedRegisters));
        });
    }

    [Test]
    public static void BigMulWithContainedMemoryForcesItsOtherOperandIntoRax()
    {
        WithAllocator((compiler, allocator) => {
            var left = compiler.gtNewIconNode(TYP_LONG, 11);
            var address = compiler.gtNewIconNode(TYP_I_IMPL, 0x1000);
            var right = compiler.gtNewIndir(TYP_LONG, address);
            right.IsContained = true;
            ReferenceBuildLocation(allocator) = 2;
            var leftDef = BuildDef(allocator, left, SRBM_NONE, 0);
            var addressDef = BuildDef(allocator, address, SRBM_NONE, 0);
            var intrinsic = new GenTreeHWIntrinsic(
                TYP_STRUCT, NI_X86Base_X64_BigMul, TYP_LONG, 0, left, right);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildHWIntrinsic(allocator, intrinsic, out var destinations), Is.EqualTo(2));
            Assert.That(destinations, Is.EqualTo(2));
            Assert.That(leftDef.nextRefPosition?.registerAssignment, Is.EqualTo(SRBM_RAX));
            Assert.That(addressDef.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            regMask[] expectedRegisters = [SRBM_RAX, SRBM_RDX];
            Assert.That(allocator.refPositions.Where(reference =>
                reference.treeNode == intrinsic && reference.refType == RefType.RefTypeDef)
                .Select(reference => reference.registerAssignment),
                Is.EqualTo(expectedRegisters));
        });
    }

    [TestCase(NI_X86Base_MaskMove, SRBM_RDI, 0)]
    [TestCase(NI_AVX2_MultiplyNoFlags, SRBM_RDX, 1)]
    public static void ImplicitOperandsAndInternalUsesHaveNativeAssignments(
        NamedIntrinsic id, regMask fixedRegister, int internalTemps)
    {
        WithAllocator((compiler, allocator) => {
            GenTree first = id is NI_X86Base_MaskMove
                ? new GenTreeVecCon(TYP_SIMD16)
                : compiler.gtNewIconNode(TYP_LONG, 1);
            GenTree second = id is NI_X86Base_MaskMove
                ? new GenTreeVecCon(TYP_SIMD16)
                : compiler.gtNewIconNode(TYP_LONG, 2);
            var third = compiler.gtNewIconNode(TYP_LONG, 3);
            ReferenceBuildLocation(allocator) = 2;
            var firstDef = BuildDef(allocator, first, SRBM_NONE, 0);
            _ = BuildDef(allocator, second, SRBM_NONE, 0);
            var thirdDef = BuildDef(allocator, third, SRBM_NONE, 0);
            var intrinsic = new GenTreeHWIntrinsic(
                id is NI_X86Base_MaskMove ? TYP_VOID : TYP_LONG, id, TYP_LONG, 16, first, second, third);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildHWIntrinsic(allocator, intrinsic, out var destinations), Is.EqualTo(3));
            Assert.That(destinations, Is.EqualTo(id is NI_X86Base_MaskMove ? 0 : 1));
            Assert.That((id is NI_X86Base_MaskMove ? thirdDef : firstDef)
                .nextRefPosition?.registerAssignment, Is.EqualTo(fixedRegister));
            Assert.That(allocator.refPositions.Count(reference =>
                reference.treeNode == intrinsic && reference.refType == RefType.RefTypeDef),
                Is.EqualTo(destinations + internalTemps));
            if (internalTemps != 0)
            {
                Assert.That(thirdDef.nextRefPosition?.delayRegFree, Is.True);
                Assert.That(allocator.refPositions.Any(reference =>
                    reference.treeNode == intrinsic && reference.refType == RefType.RefTypeUse &&
                    reference.delayRegFree), Is.True);
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void VectorArithmeticConstrictsLegacyButNotEvexRegisters(bool evexIntrinsic)
    {
        WithAllocator((compiler, allocator) => {
            Enable(compiler, InstructionSet_AVX);
            Enable(compiler, InstructionSet_AVX512);
            var first = new GenTreeVecCon(TYP_SIMD16);
            var second = new GenTreeVecCon(TYP_SIMD16);
            ReferenceBuildLocation(allocator) = 2;
            var firstDef = BuildDef(allocator, first, SRBM_NONE, 0);
            var secondDef = BuildDef(allocator, second, SRBM_NONE, 0);
            var intrinsic = new GenTreeHWIntrinsic(TYP_SIMD16,
                evexIntrinsic ? NI_AVX512_Add : NI_X86Base_AddSubtract, TYP_FLOAT, 16, first, second);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildHWIntrinsic(allocator, intrinsic, out var destinations), Is.EqualTo(2));
            Assert.That(destinations, Is.EqualTo(1));
            var candidates = evexIntrinsic ? SRBM_ALLFLOAT_INIT : SRBM_LOWFLOAT;
            Assert.That(firstDef.nextRefPosition?.registerAssignment, Is.EqualTo(candidates));
            Assert.That(secondDef.nextRefPosition?.registerAssignment, Is.EqualTo(candidates));
            Assert.That(allocator.refPositions[^1].registerAssignment, Is.EqualTo(candidates));
        });
    }

    [Test]
    public static void LegacyRmwVectorOperationPrefersTheFirstOperandAndDelaysTheSecond()
    {
        WithAllocator((_, allocator) => {
            var first = new GenTreeVecCon(TYP_SIMD16);
            var second = new GenTreeVecCon(TYP_SIMD16);
            ReferenceBuildLocation(allocator) = 2;
            var firstDef = BuildDef(allocator, first, SRBM_NONE, 0);
            var secondDef = BuildDef(allocator, second, SRBM_NONE, 0);
            var intrinsic = new GenTreeHWIntrinsic(
                TYP_SIMD16, NI_X86Base_AddSubtract, TYP_FLOAT, 16, first, second);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildHWIntrinsic(allocator, intrinsic, out var destinations), Is.EqualTo(2));
            Assert.That(destinations, Is.EqualTo(1));
            Assert.That(TargetPreferredUse(allocator), Is.SameAs(firstDef.nextRefPosition));
            Assert.That(secondDef.nextRefPosition?.delayRegFree, Is.True);
        });
    }

    [Test]
    public static void FmaPrefersTheEmittedRegisterOperandsAfterContainingTheSecondOperand()
    {
        WithAllocator((compiler, allocator) => {
            Enable(compiler, InstructionSet_AVX);
            Enable(compiler, InstructionSet_AVX2);
            var first = new GenTreeVecCon(TYP_SIMD16);
            var address = compiler.gtNewIconNode(TYP_I_IMPL, 0x1000);
            var memory = compiler.gtNewIndir(TYP_SIMD16, address);
            memory.IsContained = true;
            var third = new GenTreeVecCon(TYP_SIMD16);
            ReferenceBuildLocation(allocator) = 2;
            var firstDef = BuildDef(allocator, first, SRBM_NONE, 0);
            var addressDef = BuildDef(allocator, address, SRBM_NONE, 0);
            var thirdDef = BuildDef(allocator, third, SRBM_NONE, 0);
            var intrinsic = new GenTreeHWIntrinsic(
                TYP_SIMD16, NI_AVX2_MultiplyAdd, TYP_FLOAT, 16, first, memory, third);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildHWIntrinsic(allocator, intrinsic, out var destinations), Is.EqualTo(3));
            Assert.That(destinations, Is.EqualTo(1));
            Assert.That(TargetPreferredUse(allocator), Is.SameAs(firstDef.nextRefPosition));
            Assert.That(TargetPreferredUse2(allocator), Is.SameAs(thirdDef.nextRefPosition));
            Assert.That(addressDef.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(thirdDef.nextRefPosition?.delayRegFree, Is.False);
        });
    }

    [Test]
    public static void MemoryLoadBuildsItsAddressRatherThanUsingContainedLoad()
    {
        WithAllocator((compiler, allocator) => {
            var address = compiler.gtNewIconNode(TYP_I_IMPL, 0x1000);
            ReferenceBuildLocation(allocator) = 2;
            var addressDef = BuildDef(allocator, address, SRBM_NONE, 0);
            var intrinsic = new GenTreeHWIntrinsic(
                TYP_SIMD16, NI_X86Base_LoadAlignedVector128, TYP_INT, 16, address);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildHWIntrinsic(allocator, intrinsic, out var destinations), Is.EqualTo(1));
            Assert.That(destinations, Is.EqualTo(1));
            Assert.That(addressDef.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(allocator.refPositions[^1].refType, Is.EqualTo(RefType.RefTypeDef));
        });
    }

    [Test]
    public static void DynamicShuffleImmediateReservesTwoJumpTableTemporaries()
    {
        WithAllocator((compiler, allocator) => {
            var vector = new GenTreeVecCon(TYP_SIMD16);
            var control = new GenTreeUnOp(
                genTreeOps.GT_NEG, TYP_INT, compiler.gtNewIconNode(TYP_INT, 3));
            ReferenceBuildLocation(allocator) = 2;
            _ = BuildDef(allocator, vector, SRBM_NONE, 0);
            var controlDef = BuildDef(allocator, control, SRBM_NONE, 0);
            var intrinsic = new GenTreeHWIntrinsic(
                TYP_SIMD16, NI_X86Base_Shuffle, TYP_INT, 16, vector, control);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildHWIntrinsic(allocator, intrinsic, out var destinations), Is.EqualTo(2));
            Assert.That(destinations, Is.EqualTo(1));
            Assert.That(controlDef.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(allocator.refPositions.Count(reference =>
                reference.treeNode == intrinsic && reference.refType == RefType.RefTypeDef),
                Is.EqualTo(3));
            Assert.That(allocator.refPositions.Count(reference =>
                reference.treeNode == intrinsic && reference.refType == RefType.RefTypeUse),
                Is.EqualTo(2));
        });
    }

    [Test]
    public static void VariableVectorInsertionReservesAnImplicitlyUsedSimdSpillTemp()
    {
        WithAllocator((compiler, allocator) => {
            var vector = new GenTreeVecCon(TYP_SIMD16);
            var index = new GenTreeUnOp(
                genTreeOps.GT_NEG, TYP_INT, compiler.gtNewIconNode(TYP_INT, 1));
            var scalar = compiler.gtNewIconNode(TYP_INT, 42);
            ReferenceBuildLocation(allocator) = 2;
            _ = BuildDef(allocator, vector, SRBM_NONE, 0);
            _ = BuildDef(allocator, index, SRBM_NONE, 0);
            _ = BuildDef(allocator, scalar, SRBM_NONE, 0);
            var intrinsic = new GenTreeHWIntrinsic(
                TYP_SIMD16, NI_Vector_WithElement, TYP_INT, 16, vector, index, scalar);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildHWIntrinsic(allocator, intrinsic, out var destinations), Is.EqualTo(3));
            Assert.That(destinations, Is.EqualTo(1));
            Assert.That(compiler.lvaSimdInitTempVarNum, Is.EqualTo(0));
            Assert.That(compiler.lvaTable[0].Type, Is.EqualTo(TYP_SIMD16));
            Assert.That(compiler.lvaTable[0].lvImplicitlyReferenced, Is.True);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void Avx2GatherUsesDistinctMaskAndInternalSimdTemporary(bool evexSupported)
    {
        WithAllocator((compiler, allocator) => {
            Enable(compiler, InstructionSet_AVX);
            Enable(compiler, InstructionSet_AVX2);
            ApxSupported(allocator) = true;
            EvexSupported(allocator) = evexSupported;
            var address = compiler.gtNewIconNode(TYP_I_IMPL, 0x1000);
            var indices = new GenTreeVecCon(TYP_SIMD16);
            var scale = compiler.gtNewIconNode(TYP_INT, 4);
            scale.IsContained = true;
            ReferenceBuildLocation(allocator) = 2;
            var addressDef = BuildDef(allocator, address, SRBM_NONE, 0);
            var indicesDef = BuildDef(allocator, indices, SRBM_NONE, 0);
            var intrinsic = new GenTreeHWIntrinsic(
                TYP_SIMD16, NI_AVX2_GatherVector128, TYP_INT, 16, address, indices, scale);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildHWIntrinsic(allocator, intrinsic, out var destinations), Is.EqualTo(2));
            Assert.That(destinations, Is.EqualTo(1));
            Assert.That(addressDef.nextRefPosition?.registerAssignment,
                Is.EqualTo(LowGprRegs(allocator)));
            Assert.That(indicesDef.nextRefPosition?.delayRegFree, Is.True);
            Assert.That(allocator.refPositions.Any(reference =>
                reference.treeNode == intrinsic && reference.refType == RefType.RefTypeUse &&
                reference.delayRegFree && reference.registerAssignment == SRBM_LOWFLOAT), Is.True);
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildHWIntrinsic")]
    private static extern int BuildHWIntrinsic(
        LinearScan allocator, GenTreeHWIntrinsic intrinsic, out int destinationCount);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildDef")]
    private static extern RefPosition BuildDef(
        LinearScan allocator, GenTree tree, regMask candidates, int multiRegIndex);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildPhysRegRecords")]
    private static extern void BuildPhysRegRecords(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_referenceBuildLocation")]
    private static extern ref uint ReferenceBuildLocation(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_targetPreferredUse")]
    private static extern ref RefPosition? TargetPreferredUse(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_targetPreferredUse2")]
    private static extern ref RefPosition? TargetPreferredUse2(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllInt")]
    private static extern ref regMask CompilerAllIntRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_apxIsSupported")]
    private static extern ref bool ApxSupported(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_evexIsSupported")]
    private static extern ref bool EvexSupported(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_lowGprRegs")]
    private static extern ref regMask LowGprRegs(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllFloat")]
    private static extern ref regMask CompilerAllFloatRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllMask")]
    private static extern ref regMask CompilerAllMaskRegs(Compiler compiler);

    private static void Enable(Compiler compiler, CORINFO_InstructionSet instructionSet)
    {
        compiler.opts.compSupportsISA.AddInstructionSet(instructionSet);
        compiler.opts.compSupportsISAReported.AddInstructionSet(instructionSet);
        compiler.opts.compSupportsISAExactly.AddInstructionSet(instructionSet);
    }

    private static void WithAllocator(Action<Compiler, LinearScan> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.lvaSimdInitTempVarNum = BAD_VAR_NUM;
        compiler.lvaTable = [];
        CompilerAllIntRegs(compiler) = SRBM_ALLINT_INIT;
        CompilerAllFloatRegs(compiler) = SRBM_ALLFLOAT_INIT;
        CompilerAllMaskRegs(compiler) = SRBM_ALLMASK_INIT;
        JitTls.Compiler = compiler;

        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
        codeGen.CopyRegisterInfo();
        codeGen.IsFramePointerRequired = false;
        codeGen.IsFramePointerUsed = false;
        codeGen.IsFrameRequired = false;
        codeGen.RegSet.rsClearRegsModified();
        try
        {
            var allocator = new LinearScan(compiler);
            BuildPhysRegRecords(allocator);
            action(compiler, allocator);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
