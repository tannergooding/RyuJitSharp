// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if DEBUG && TARGET_AMD64
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class LinearScanRegisterStateCompletionTests
{
    [TestCase(TYP_FLOAT, REG_XMM0, REG_XMM31)]
    [TestCase(TYP_FLOAT, REG_XMM31, REG_XMM0)]
    [TestCase(TYP_DOUBLE, REG_XMM0, REG_XMM31)]
    [TestCase(TYP_DOUBLE, REG_XMM31, REG_XMM0)]
    public static void BoundaryRotationSelectsAndWrapsTheSignBitRegister(
        var_types type, regNumber target, regNumber expected)
    {
        LinearScanMinimalCandidatesTests.WithCompiler(compiler =>
        {
            var allocator = new LinearScan(compiler);
            var candidates = SRBM_XMM0 | SRBM_XMM31;
            AvailableFloatRegisters(allocator) = candidates;
            AvailableDoubleRegisters(allocator) = candidates;
            StressMask(allocator) = 0x200;
            var interval = new Interval(type, candidates);

            Assert.That((long)SRBM_XMM31, Is.EqualTo(long.MinValue));
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

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_lsraStressMask")]
    private static extern ref int StressMask(LinearScan allocator);
}
#endif
