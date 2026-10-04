// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See License.md in the repository root.

#if TARGET_WASM
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class WasmEmitterSizingTests
{
    [TestCase(0UL, 1)]
    [TestCase(127UL, 1)]
    [TestCase(128UL, 2)]
    [TestCase(16_383UL, 2)]
    [TestCase(16_384UL, 3)]
    [TestCase(ulong.MaxValue, 10)]
    public static void Uleb128SizesMatchSevenBitEncoding(ulong value, int expectedSize)
    {
        Assert.That(Emitter.SizeOfULEB128(value), Is.EqualTo(expectedSize));
    }

    [TestCase(0L, 1)]
    [TestCase(-1L, 1)]
    [TestCase(63L, 1)]
    [TestCase(64L, 2)]
    [TestCase(-64L, 1)]
    [TestCase(-65L, 2)]
    [TestCase(8_191L, 2)]
    [TestCase(8_192L, 3)]
    [TestCase(-8_192L, 2)]
    [TestCase(-8_193L, 3)]
    [TestCase(long.MinValue, 10)]
    [TestCase(long.MaxValue, 10)]
    public static void Sleb128SizesMatchSignedSevenBitEncoding(long value, int expectedSize)
    {
        Assert.That(Emitter.SizeOfSLEB128(value), Is.EqualTo(expectedSize));
    }

    [TestCase(WasmValueType.Invalid, 0x00)]
    [TestCase(WasmValueType.I32, 0x7F)]
    [TestCase(WasmValueType.I64, 0x7E)]
    [TestCase(WasmValueType.F32, 0x7D)]
    [TestCase(WasmValueType.F64, 0x7C)]
    [TestCase(WasmValueType.V128, 0x7B)]
    [TestCase(WasmValueType.ExnRef, 0x69)]
    public static void WasmValueTypesUseTheirNativeEncoding(WasmValueType type, byte expectedCode)
    {
        Assert.That(Emitter.GetWasmValueTypeCode(type), Is.EqualTo(expectedCode));
    }
}
#endif
