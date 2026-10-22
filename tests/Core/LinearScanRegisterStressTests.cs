// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

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
        AssumeStressModeSupported();
        LinearScanLocalCandidatesTests.WithCandidates(1, (_, allocator) => {
            TestConfiguration.SetLinearScanStressMask(allocator, 3);
            var candidates = SRBM_X64_R12 | SRBM_X64_RDI;
            var expected = Configuration.UNIX_AMD64_ABI ? SRBM_X64_R12 : SRBM_X64_RDI;
            Assert.That(StressLimit(allocator, null, TYP_INT, candidates), Is.EqualTo(expected));
        });
    }

    [Test]
    public static void SmallFloatingPointSetUsesTheConfiguredRegisters()
    {
        AssumeStressModeSupported();
        LinearScanLocalCandidatesTests.WithCandidates(1, (_, allocator) => {
            TestConfiguration.SetLinearScanStressMask(allocator, 3);
            var candidates = SRBM_X64_XMM3 | SRBM_X64_XMM6;

            Assert.That(StressLimit(allocator, null, TYP_FLOAT, candidates), Is.EqualTo(SRBM_X64_XMM6));
        });
    }

    [Test]
    public static void UpperSimdSetUsesHighVectorRegisters()
    {
        AssumeStressModeSupported();
        LinearScanLocalCandidatesTests.WithCandidates(1, (_, allocator) => {
            TestConfiguration.SetLinearScanStressMask(allocator, 0x2000);
            var candidates = SRBM_X64_XMM15 | SRBM_X64_XMM16;

            Assert.That(StressLimit(allocator, null, TYP_FLOAT, candidates), Is.EqualTo(SRBM_X64_XMM16));
        });
    }

    [Test]
    public static void ExtendedGprSetUsesHighIntegerRegisters()
    {
        AssumeStressModeSupported();
        LinearScanLocalCandidatesTests.WithCandidates(1, (_, allocator) => {
            TestConfiguration.SetLinearScanStressMask(allocator, 0x4000);
            var candidates = SRBM_X64_R15 | SRBM_X64_R16;

            Assert.That(StressLimit(allocator, null, TYP_INT, candidates), Is.EqualTo(SRBM_X64_R16));
        });
    }

    [Test]
    public static void RequiredBusyCandidateRetainsTheOriginalMask()
    {
        AssumeStressModeSupported();
        LinearScanLocalCandidatesTests.WithCandidates(1, (compiler, allocator) => {
            IntCalleeTrash(compiler) = SRBM_X64_RAX;
            TestConfiguration.SetLinearScanStressMask(allocator, 2);
            BusyUntilKill(allocator) = new regMaskTP(SRBM_X64_RAX);
            var reference = new RefPosition(1, 2, null, RefType.RefTypeUse);
            TestConfiguration.SetMinimumRegisterCandidateCount(reference, 1);
            var candidates = SRBM_X64_RAX | SRBM_X64_RBX;

            Assert.That(StressLimit(allocator, reference, TYP_INT, candidates), Is.EqualTo(candidates));

            reference.setRegOptional(true);
            Assert.That(StressLimit(allocator, reference, TYP_INT, candidates), Is.EqualTo(SRBM_X64_RAX));
        });
    }

    [Test]
    public static void MinimumCountAndFixedReferencePreserveRequiredCandidates()
    {
        AssumeStressModeSupported();
        LinearScanLocalCandidatesTests.WithCandidates(1, (compiler, allocator) => {
            IntCalleeTrash(compiler) = SRBM_X64_RAX;
            TestConfiguration.SetLinearScanStressMask(allocator, 2);
            var reference = new RefPosition(1, 2, null, RefType.RefTypeUse)
            {
                registerAssignment = SRBM_X64_RBX,
            };
            TestConfiguration.SetMinimumRegisterCandidateCount(reference, 2);
            var candidates = SRBM_X64_RAX | SRBM_X64_RBX;

            Assert.That(StressLimit(allocator, reference, TYP_INT, candidates), Is.EqualTo(candidates));

            TestConfiguration.SetMinimumRegisterCandidateCount(reference, 1);
            reference.isFixedRegRef = true;
            Assert.That(StressLimit(allocator, reference, TYP_INT, candidates), Is.EqualTo(candidates));
        });
    }

    [Test]
    public static void EditAndContinueBypassesCalleeLimitButNotFixedReference()
    {
        AssumeStressModeSupported();
        LinearScanLocalCandidatesTests.WithCandidates(1, (compiler, allocator) => {
            compiler.opts.compDbgEnC = true;
            TestConfiguration.SetLinearScanStressMask(allocator, 1);
            var reference = new RefPosition(1, 2, null, RefType.RefTypeUse)
            {
                registerAssignment = SRBM_X64_RBX,
                isFixedRegRef = true,
            };
            TestConfiguration.SetMinimumRegisterCandidateCount(reference, 1);

            Assert.That(StressLimit(allocator, reference, TYP_INT, SRBM_X64_RAX), Is.EqualTo(SRBM_X64_RAX | SRBM_X64_RBX));
        });
    }

    [Test]
    public static void SmallSetDoesNotRemoveMaskRegisterCandidates()
    {
        AssumeStressModeSupported();
        LinearScanLocalCandidatesTests.WithCandidates(1, (_, allocator) => {
            TestConfiguration.SetLinearScanStressMask(allocator, 3);
            Assert.That(StressLimit(allocator, null, TYP_MASK, SRBM_X64_K1 | SRBM_X64_K2),
                Is.EqualTo(SRBM_X64_K1 | SRBM_X64_K2));
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "stressLimitRegs")]
    private static extern regMask StressLimit(
        LinearScan allocator, RefPosition? reference, var_types type, regMask candidates);

[UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_regsBusyUntilKill")]
    private static extern ref regMaskTP BusyUntilKill(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmIntCalleeTrash")]
    private static extern ref regMask IntCalleeTrash(Compiler compiler);

    private static void AssumeStressModeSupported()
    {
        Assume.That(TestConfiguration.IsDebug && Configuration.TARGET_AMD64);
    }
}
