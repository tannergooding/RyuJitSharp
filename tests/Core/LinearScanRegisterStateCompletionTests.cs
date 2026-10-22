// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class LinearScanRegisterStateCompletionTests
{
    [TestCase(TYP_FLOAT, REG_X64_XMM0, REG_X64_XMM31)]
    [TestCase(TYP_FLOAT, REG_X64_XMM31, REG_X64_XMM0)]
    [TestCase(TYP_DOUBLE, REG_X64_XMM0, REG_X64_XMM31)]
    [TestCase(TYP_DOUBLE, REG_X64_XMM31, REG_X64_XMM0)]
    public static void BoundaryRotationSelectsAndWrapsTheSignBitRegister(
        var_types type, regNumber target, regNumber expected)
    {
        if (!Configuration.DEBUG || !Configuration.TARGET_AMD64)
        {
            Assert.Ignore("This register-state test requires Debug AMD64.");
        }

        LinearScanMinimalCandidatesTests.WithCompiler(compiler =>
        {
            var allocator = new LinearScan(compiler);
            var candidates = SRBM_X64_XMM0 | SRBM_X64_XMM31;
            AvailableFloatRegisters(allocator) = candidates;
            AvailableDoubleRegisters(allocator) = candidates;
            TestConfiguration.SetLinearScanStressMask(allocator, 0x200);
            var interval = new Interval(type, candidates);

            Assert.That((long)SRBM_X64_XMM31, Is.EqualTo(long.MinValue));
            Assert.That(Rotate(allocator, interval, target, new regMaskTP(candidates)), Is.EqualTo(expected));
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "rotateBlockStartLocation")]
    private static extern regNumber Rotate(
        LinearScan allocator, Interval interval, regNumber target, regMaskTP available);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_availableFloatRegs")]
    private static extern ref regMask AvailableFloatRegisters(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_availableDoubleRegs")]
    private static extern ref regMask AvailableDoubleRegisters(LinearScan allocator);

}
