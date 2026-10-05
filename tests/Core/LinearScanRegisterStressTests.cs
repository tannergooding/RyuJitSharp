// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG && TARGET_AMD64
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.regMask;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class LinearScanRegisterStressTests
{
    [Test]
    public static void SmallIntegerSetUsesTheTargetAbi()
    {
        LinearScanLocalCandidatesTests.WithCandidates(1, (_, allocator) => {
            StressMask(allocator) = 3;
            var candidates = SRBM_R12 | SRBM_RDI;
#if UNIX_AMD64_ABI
            var expected = SRBM_R12;
#else
            var expected = SRBM_RDI;
#endif
            Assert.That(StressLimit(allocator, null, TYP_INT, candidates), Is.EqualTo(expected));
        });
    }

    [Test]
    public static void SmallFloatingPointSetUsesTheConfiguredRegisters()
    {
        LinearScanLocalCandidatesTests.WithCandidates(1, (_, allocator) => {
            StressMask(allocator) = 3;
            var candidates = SRBM_XMM3 | SRBM_XMM6;

            Assert.That(StressLimit(allocator, null, TYP_FLOAT, candidates), Is.EqualTo(SRBM_XMM6));
        });
    }

    [Test]
    public static void UpperSimdSetUsesHighVectorRegisters()
    {
        LinearScanLocalCandidatesTests.WithCandidates(1, (_, allocator) => {
            StressMask(allocator) = 0x2000;
            var candidates = SRBM_XMM15 | SRBM_XMM16;

            Assert.That(StressLimit(allocator, null, TYP_FLOAT, candidates), Is.EqualTo(SRBM_XMM16));
        });
    }

    [Test]
    public static void ExtendedGprSetUsesHighIntegerRegisters()
    {
        LinearScanLocalCandidatesTests.WithCandidates(1, (_, allocator) => {
            StressMask(allocator) = 0x4000;
            var candidates = SRBM_R15 | SRBM_R16;

            Assert.That(StressLimit(allocator, null, TYP_INT, candidates), Is.EqualTo(SRBM_R16));
        });
    }

    [Test]
    public static void RequiredBusyCandidateRetainsTheOriginalMask()
    {
        LinearScanLocalCandidatesTests.WithCandidates(1, (compiler, allocator) => {
            IntCalleeTrash(compiler) = SRBM_RAX;
            StressMask(allocator) = 2;
            BusyUntilKill(allocator) = new regMaskTP(SRBM_RAX);
            var reference = new RefPosition(1, 2, null, RefType.RefTypeUse)
            {
                minRegCandidateCount = 1,
            };
            var candidates = SRBM_RAX | SRBM_RBX;

            Assert.That(StressLimit(allocator, reference, TYP_INT, candidates), Is.EqualTo(candidates));

            reference.setRegOptional(true);
            Assert.That(StressLimit(allocator, reference, TYP_INT, candidates), Is.EqualTo(SRBM_RAX));
        });
    }

    [Test]
    public static void MinimumCountAndFixedReferencePreserveRequiredCandidates()
    {
        LinearScanLocalCandidatesTests.WithCandidates(1, (compiler, allocator) => {
            IntCalleeTrash(compiler) = SRBM_RAX;
            StressMask(allocator) = 2;
            var reference = new RefPosition(1, 2, null, RefType.RefTypeUse)
            {
                registerAssignment = SRBM_RBX,
                minRegCandidateCount = 2,
            };
            var candidates = SRBM_RAX | SRBM_RBX;

            Assert.That(StressLimit(allocator, reference, TYP_INT, candidates), Is.EqualTo(candidates));

            reference.minRegCandidateCount = 1;
            reference.isFixedRegRef = true;
            Assert.That(StressLimit(allocator, reference, TYP_INT, candidates), Is.EqualTo(candidates));
        });
    }

    [Test]
    public static void EditAndContinueBypassesCalleeLimitButNotFixedReference()
    {
        LinearScanLocalCandidatesTests.WithCandidates(1, (compiler, allocator) => {
            compiler.opts.compDbgEnC = true;
            StressMask(allocator) = 1;
            var reference = new RefPosition(1, 2, null, RefType.RefTypeUse)
            {
                registerAssignment = SRBM_RBX,
                isFixedRegRef = true,
                minRegCandidateCount = 1,
            };

            Assert.That(StressLimit(allocator, reference, TYP_INT, SRBM_RAX), Is.EqualTo(SRBM_RAX | SRBM_RBX));
        });
    }

    [Test]
    public static void SmallSetDoesNotRemoveMaskRegisterCandidates()
    {
        LinearScanLocalCandidatesTests.WithCandidates(1, (_, allocator) => {
            StressMask(allocator) = 3;
            Assert.That(StressLimit(allocator, null, TYP_MASK, SRBM_K1 | SRBM_K2),
                Is.EqualTo(SRBM_K1 | SRBM_K2));
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "stressLimitRegs")]
    private static extern regMask StressLimit(
        LinearScan allocator, RefPosition? reference, var_types type, regMask candidates);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_lsraStressMask")]
    private static extern ref int StressMask(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_regsBusyUntilKill")]
    private static extern ref regMaskTP BusyUntilKill(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmIntCalleeTrash")]
    private static extern ref regMask IntCalleeTrash(Compiler compiler);
}
#endif
