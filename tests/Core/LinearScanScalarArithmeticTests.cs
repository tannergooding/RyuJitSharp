// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.LsraGlobals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.regMask;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LinearScanScalarArithmeticTests
{
    [TestCase(GT_LSH, true, false, false)]
    [TestCase(GT_ROR, false, false, true)]
    [TestCase(GT_RSH, false, true, false)]
    [TestCase(GT_RSH, false, true, true)]
    [TestCase(GT_RSH, false, true, false, false)]
    public static void ShiftUsesTheAppropriateCountEncoding(
        genTreeOps operation, bool immediate, bool avx2, bool setsFlags, bool minopts = true)
    {
        WithAllocator((compiler, allocator) => {
            if (avx2)
            {
                EnableAvx2(compiler);
            }

            var value = compiler.gtNewIconNode(TYP_INT, 0x1234);
            var count = compiler.gtNewIconNode(TYP_INT, 3);
            count.IsContained = immediate;
            ReferenceBuildLocation(allocator) = 2;
            var valueDef = BuildDef(allocator, value, SRBM_NONE, 0);
            var countDef = immediate ? null : BuildDef(allocator, count, SRBM_NONE, 0);
            var shift = compiler.gtNewBinaryNode(operation, TYP_INT, value, count);
            if (setsFlags)
            {
                shift.Flags |= GTF_SET_FLAGS;
            }

            ReferenceBuildLocation(allocator) = 4;
            Assert.That(BuildShiftRotate(allocator, shift), Is.EqualTo(immediate ? 1 : 2));
            Assert.That(valueDef.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(countDef?.nextRefPosition?.delayRegFree,
                Is.EqualTo(immediate ? null : (!avx2 || setsFlags)));
            if (!immediate && (!avx2 || setsFlags))
            {
                Assert.That(countDef?.nextRefPosition?.registerAssignment, Is.EqualTo(SRBM_RCX));
                Assert.That(allocator.refPositions.Exists(reference =>
                    (reference.refType is RefType.RefTypeKill) &&
                    (reference.registerAssignment == SRBM_RCX)), Is.True);
                Assert.That(TargetPreferredUse(allocator), Is.SameAs(valueDef.nextRefPosition));
            }
            else if (avx2)
            {
                Assert.That(TargetPreferredUse(allocator), Is.Null);
            }

            Assert.That(allocator.refPositions[^1].treeNode, Is.SameAs(shift));
        }, minopts);
    }

    [TestCase(GT_ROL, false)]
    [TestCase(GT_ROL, true)]
    [TestCase(GT_ROR, false)]
    [TestCase(GT_ROR, true)]
    public static void LongImmediateRotateRestrictsApxRegistersOnlyWithoutEvex(genTreeOps operation, bool evex)
    {
        WithAllocator((compiler, allocator) => {
            EnableAvx2(compiler);
            ApxSupported(allocator) = true;
            EvexSupported(allocator) = evex;
            AvailableIntRegs(allocator) |= SRBM_R16;

            var source = compiler.gtNewIconNode(TYP_LONG, 123);
            var count = compiler.gtNewIconNode(TYP_INT, 13);
            count.IsContained = true;
            ReferenceBuildLocation(allocator) = 2;
            var sourceDef = BuildDef(allocator, source, SRBM_NONE, 0);
            var rotate = compiler.gtNewBinaryNode(operation, TYP_LONG, source, count);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildShiftRotate(allocator, rotate), Is.EqualTo(1));
            var candidates = evex ? AvailableIntRegs(allocator) : LowGprRegs(allocator);
            Assert.That(sourceDef.nextRefPosition?.registerAssignment, Is.EqualTo(candidates));
            Assert.That(TargetPreferredUse(allocator), Is.SameAs(sourceDef.nextRefPosition));
            Assert.That(allocator.refPositions[^1].treeNode, Is.SameAs(rotate));
            Assert.That(allocator.refPositions[^1].registerAssignment, Is.EqualTo(candidates));
            Assert.That(allocator.refPositions.Exists(reference =>
                reference.refType is RefType.RefTypeKill), Is.False);
        });
    }

    [TestCase(GT_LSH, false)]
    [TestCase(GT_ROR, true)]
    public static void ContainedVariableShiftUsesAndKillsRcxWithoutDefiningAResult(
        genTreeOps operation, bool avx2)
    {
        WithAllocator((compiler, allocator) => {
            if (avx2)
            {
                EnableAvx2(compiler);
            }

            var source = compiler.gtNewIconNode(TYP_INT, 123);
            var count = compiler.gtNewIconNode(TYP_INT, 3);
            ReferenceBuildLocation(allocator) = 2;
            var sourceDef = BuildDef(allocator, source, SRBM_NONE, 0);
            var countDef = BuildDef(allocator, count, SRBM_NONE, 0);
            var shift = compiler.gtNewBinaryNode(operation, TYP_INT, source, count);
            shift.IsContained = true;
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildShiftRotate(allocator, shift), Is.EqualTo(2));
            Assert.That(TargetPreferredUse(allocator), Is.SameAs(sourceDef.nextRefPosition));
            Assert.That(countDef.nextRefPosition?.registerAssignment, Is.EqualTo(SRBM_RCX));
            Assert.That(countDef.nextRefPosition?.delayRegFree, Is.False);
            Assert.That(allocator.refPositions.Exists(reference =>
                (reference.refType is RefType.RefTypeKill) &&
                (reference.registerAssignment == SRBM_RCX)), Is.True);
            Assert.That(allocator.refPositions.Exists(reference =>
                ReferenceEquals(reference.treeNode, shift) &&
                (reference.refType is RefType.RefTypeDef)), Is.False);
        });
    }

    [TestCase(GT_DIV, SRBM_RAX)]
    [TestCase(GT_UDIV, SRBM_RAX)]
    [TestCase(GT_MOD, SRBM_RDX)]
    [TestCase(GT_UMOD, SRBM_RDX)]
    public static void DivisionUsesDividendAndResultRegisters(genTreeOps operation, regMask resultRegister)
    {
        WithAllocator((compiler, allocator) => {
            var dividend = compiler.gtNewIconNode(TYP_INT, 101);
            var divisor = compiler.gtNewIconNode(TYP_INT, 7);
            ReferenceBuildLocation(allocator) = 2;
            var dividendDef = BuildDef(allocator, dividend, SRBM_NONE, 0);
            var divisorDef = BuildDef(allocator, divisor, SRBM_NONE, 0);
            var division = compiler.gtNewBinaryNode(operation, TYP_INT, dividend, divisor);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildModDiv(allocator, division), Is.EqualTo(2));
            Assert.That(dividendDef.nextRefPosition?.registerAssignment, Is.EqualTo(SRBM_RAX));
            Assert.That(TargetPreferredUse(allocator), Is.SameAs(dividendDef.nextRefPosition));
            Assert.That(divisorDef.nextRefPosition?.delayRegFree, Is.True);
            Assert.That(divisorDef.nextRefPosition?.registerAssignment & (SRBM_RAX | SRBM_RDX),
                Is.EqualTo(SRBM_NONE));
            Assert.That(allocator.refPositions[^1].registerAssignment, Is.EqualTo(resultRegister));
            Assert.That(allocator.refPositions[^1].refType, Is.EqualTo(RefType.RefTypeDef));
            Assert.That(allocator.refPositions.Exists(reference =>
                (reference.refType is RefType.RefTypeKill) &&
                (reference.registerAssignment == (SRBM_RAX | SRBM_RDX))), Is.True);
        });
    }

    [TestCase(GT_MUL, false, false, SRBM_NONE, 1)]
    [TestCase(GT_MUL, true, false, SRBM_RAX, 1)]
    [TestCase(GT_MULHI, false, false, SRBM_RDX, 2)]
    [TestCase(GT_MULHI, true, true, SRBM_NONE, 2)]
    public static void MultiplicationPreservesEncodingAndKillRequirements(
        genTreeOps operation, bool unsigned, bool avx2, regMask resultCandidates, int expectedSources)
    {
        WithAllocator((compiler, allocator) => {
            if (avx2)
            {
                EnableAvx2(compiler);
            }

            var left = compiler.gtNewIconNode(TYP_INT, 17);
            var right = compiler.gtNewIconNode(TYP_INT, 5);
            right.IsContained = operation is GT_MUL;
            ReferenceBuildLocation(allocator) = 2;
            var leftDef = BuildDef(allocator, left, SRBM_NONE, 0);
            var rightDef = right.IsContained ? null : BuildDef(allocator, right, SRBM_NONE, 0);
            var multiply = compiler.gtNewBinaryNode(operation, TYP_INT, left, right);
            if (unsigned)
            {
                multiply.Flags |= GTF_UNSIGNED;
            }

            if (operation is GT_MUL && unsigned)
            {
                multiply.Flags |= GTF_OVERFLOW;
            }

            ReferenceBuildLocation(allocator) = 4;
            Assert.That(BuildMul(allocator, multiply), Is.EqualTo(expectedSources));
            Assert.That(leftDef.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(rightDef?.nextRefPosition?.refType,
                Is.EqualTo(right.IsContained ? null : RefType.RefTypeUse));
            Assert.That(allocator.refPositions[^1].registerAssignment,
                Is.EqualTo(resultCandidates == SRBM_NONE ? AvailableIntRegs(allocator) : resultCandidates));
        });
    }

    [Test]
    public static void MulxWithContainedMemoryForcesItsRegisterOperandIntoRdx()
    {
        WithAllocator((compiler, allocator) => {
            EnableAvx2(compiler);
            var left = compiler.gtNewIconNode(TYP_INT, 17);
            var address = compiler.gtNewIconNode(TYP_I_IMPL, 0x1000);
            ReferenceBuildLocation(allocator) = 2;
            var leftDef = BuildDef(allocator, left, SRBM_NONE, 0);
            var addressDef = BuildDef(allocator, address, SRBM_NONE, 0);
            var memory = compiler.gtNewIndir(TYP_INT, address);
            memory.IsContained = true;
            var multiply = compiler.gtNewBinaryNode(GT_MULHI, TYP_INT, left, memory);
            multiply.Flags |= GTF_UNSIGNED;
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildMul(allocator, multiply), Is.EqualTo(2));
            Assert.That(leftDef.nextRefPosition?.registerAssignment, Is.EqualTo(SRBM_RDX));
            Assert.That(addressDef.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(allocator.refPositions[^1].refType, Is.EqualTo(RefType.RefTypeDef));
        });
    }

    [TestCase(GT_DIV)]
    [TestCase(GT_MUL)]
    public static void FloatingArithmeticUsesTheSimpleNodeBuilder(genTreeOps operation)
    {
        WithAllocator((compiler, allocator) => {
            var left = compiler.gtNewDconNode(TYP_DOUBLE, 17.0);
            var right = compiler.gtNewDconNode(TYP_DOUBLE, 5.0);
            ReferenceBuildLocation(allocator) = 2;
            var leftDef = BuildDef(allocator, left, SRBM_NONE, 0);
            var rightDef = BuildDef(allocator, right, SRBM_NONE, 0);
            var arithmetic = compiler.gtNewBinaryNode(operation, TYP_DOUBLE, left, right);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(operation is GT_DIV
                ? BuildModDiv(allocator, arithmetic)
                : BuildMul(allocator, arithmetic), Is.EqualTo(2));
            Assert.That(leftDef.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(rightDef.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(allocator.refPositions[^1].treeNode, Is.SameAs(arithmetic));
            Assert.That(allocator.refPositions[^1].refType, Is.EqualTo(RefType.RefTypeDef));
            Assert.That(allocator.refPositions.Exists(reference =>
                reference.refType is RefType.RefTypeKill), Is.False);
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildShiftRotate")]
    private static extern int BuildShiftRotate(LinearScan allocator, GenTree tree);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildModDiv")]
    private static extern int BuildModDiv(LinearScan allocator, GenTree tree);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildMul")]
    private static extern int BuildMul(LinearScan allocator, GenTree tree);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildDef")]
    private static extern RefPosition BuildDef(LinearScan allocator, GenTree tree, regMask candidates, int multiRegIndex);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildPhysRegRecords")]
    private static extern void BuildPhysRegRecords(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_referenceBuildLocation")]
    private static extern ref uint ReferenceBuildLocation(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_targetPreferredUse")]
    private static extern ref RefPosition? TargetPreferredUse(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_availableIntRegs")]
    private static extern ref regMask AvailableIntRegs(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_lowGprRegs")]
    private static extern ref regMask LowGprRegs(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_apxIsSupported")]
    private static extern ref bool ApxSupported(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_evexIsSupported")]
    private static extern ref bool EvexSupported(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllInt")]
    private static extern ref regMask CompilerAllIntRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllFloat")]
    private static extern ref regMask CompilerAllFloatRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllMask")]
    private static extern ref regMask CompilerAllMaskRegs(Compiler compiler);

    private static void EnableAvx2(Compiler compiler)
    {
        compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_AVX2);
        compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_AVX2);
        compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_AVX2);
    }

    private static void WithAllocator(Action<Compiler, LinearScan> action, bool minopts = true)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(minopts);
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
