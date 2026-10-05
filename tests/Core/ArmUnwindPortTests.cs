// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM && DEBUG
using NUnit.Framework;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

internal static class ArmUnwindPortTests
{
    [TestCase(0xFFFFFFFFu, 28u, 4u, 15u)]
    [TestCase(0xFFFFFFFFu, 0u, 18u, 0x3FFFFu)]
    [TestCase(0x80000000u, 31u, 1u, 1u)]
    [TestCase(0x12345678u, 16u, 8u, 0x34u)]
    [TestCase(0x00000003u, 0u, 2u, 3u)]
    public static void HeaderBitExtractionPreservesUnsignedFields(
        uint word, uint start, uint length, uint expected)
    {
        Assert.That(ExtractBits(word, start, length), Is.EqualTo(expected));
    }
}
#endif
