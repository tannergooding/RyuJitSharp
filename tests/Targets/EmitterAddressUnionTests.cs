// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root.
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using NUnit.Framework;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

internal static class EmitterAddressUnionTests
{
    [Test]
    public static void InstructionCountTagAndSignedValueArePreserved()
    {
        var address = default(Emitter.instrDesc.idAddrUnion);
        Assert.That(address.iiaHasInstrCount(), Is.False);

        foreach (var count in new[] { -9, 0, 9 })
        {
            address.iiaSetInstrCount(count);

            Assert.That(address.iiaHasInstrCount(), Is.True);
            Assert.That(address.iiaGetInstrCount(), Is.EqualTo(count));
        }
    }

    [Test]
    public static void CodeAlignmentPredicateUsesTheTargetBoundary()
    {
        Assert.That(Emitter.IsCodeAligned(0), Is.True);
        Assert.That(Emitter.IsCodeAligned(CODE_ALIGN), Is.True);
        Assert.That(Emitter.IsCodeAligned(unchecked(CODE_ALIGN + 1)),
            Is.EqualTo(CODE_ALIGN == 1));
        Assert.That(Emitter.IsCodeAligned(0u), Is.True);
        Assert.That(Emitter.IsCodeAligned(unchecked((uint)CODE_ALIGN + 1)),
            Is.EqualTo(CODE_ALIGN == 1));
    }

#if TARGET_LOONGARCH64 || TARGET_RISCV64
    [TestCase(0u)]
    [TestCase(0x12345678u)]
    [TestCase(uint.MaxValue)]
    public static void EncodedInstructionRoundTrips(uint encodedInstruction)
    {
        var address = default(Emitter.instrDesc.idAddrUnion);
        address.iiaSetInstrEncode(encodedInstruction);

        Assert.That(address.iiaGetInstrEncode(), Is.EqualTo(encodedInstruction));
    }
#endif

#if TARGET_LOONGARCH64
    [TestCase(int.MinValue)]
    [TestCase(-4)]
    [TestCase(0)]
    [TestCase(int.MaxValue)]
    public static void JumpOffsetRoundTrips(int offset)
    {
        var address = default(Emitter.instrDesc.idAddrUnion);
        address.iiaSetJmpOffset(offset);

        Assert.That(address.iiaGetJmpOffset(), Is.EqualTo(offset));
    }
#endif
}
