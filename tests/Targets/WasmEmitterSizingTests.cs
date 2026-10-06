// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See License.md in the repository root.

#if TARGET_WASM
using NUnit.Framework;
using static RyuJitSharp.instruction;

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

    [TestCase(INS_local_get, 0x20u, 0, Emitter.insFormat.IF_ULEB128)]
    [TestCase(INS_i32_add, 0x6Au, 0, Emitter.insFormat.IF_OPCODE)]
    [TestCase(INS_i8x16_shuffle, 13u, 0xFD, Emitter.insFormat.IF_V128)]
    public static void WasmInstructionTablesMatchNativeEncodings(
        instruction ins, uint expectedOpcode, byte expectedPrefix, Emitter.insFormat expectedFormat)
    {
        Assert.That(Emitter.GetInsOpcode(ins), Is.EqualTo(expectedOpcode));
        Assert.That(Emitter.GetOpcodePrefix(ins), Is.EqualTo(expectedPrefix));
        Assert.That(Emitter.emitInsFormat(ins), Is.EqualTo(expectedFormat));
    }

    [TestCase(1, 16)]
    [TestCase(2, 8)]
    [TestCase(4, 4)]
    [TestCase(8, 2)]
    public static void WasmVectorLaneRangesMatchElementWidths(byte elementSize, byte laneCount)
    {
        Assert.That(Emitter.isValidSimdElemSize(elementSize), Is.True);
        Assert.That(Emitter.isValidVectorIndex(elementSize, unchecked((byte)(laneCount - 1))), Is.True);
        Assert.That(Emitter.isValidVectorIndex(elementSize, laneCount), Is.False);
    }

    [TestCase(0)]
    [TestCase(3)]
    [TestCase(16)]
    public static void InvalidWasmVectorElementWidthsAreRejected(byte elementSize)
    {
        Assert.That(Emitter.isValidSimdElemSize(elementSize), Is.False);
    }
}
#endif
