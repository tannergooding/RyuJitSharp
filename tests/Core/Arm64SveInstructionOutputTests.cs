// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static unsafe class Arm64SveInstructionOutputTests
{
    [TestCase(REG_V0, 4, 0, 0u)]
    [TestCase(REG_V31, 4, 0, 31u)]
    [TestCase(REG_V31, 18, 16, 0x70000u)]
    [TestCase(REG_V31, 19, 16, 0xF0000u)]
    [TestCase(REG_V17, 9, 5, 0x220u)]
    [TestCase(REG_V31, 20, 16, 0x1F0000u)]
    public static void VectorFieldsUseTheNativeBankOffsetAndFieldMask(regNumber reg, int hi, int lo, uint expected)
    {
        Assert.That(Vector(null, reg, hi, lo), Is.EqualTo(expected));
    }

    [TestCase(REG_P0, 3, 0, 0u)]
    [TestCase(REG_P15, 3, 0, 15u)]
    [TestCase(REG_P15, 12, 10, 0x1C00u)]
    [TestCase(REG_P15, 13, 10, 0x3C00u)]
    [TestCase(REG_P15, 3, 1, 14u)]
    [TestCase(REG_P15, 2, 0, 7u)]
    public static void PredicateFieldsPreserveNativeTruncation(regNumber reg, int hi, int lo, uint expected)
    {
        Assert.That(Predicate(null, reg, hi, lo), Is.EqualTo(expected));
    }

    [TestCase(REG_R0, 4, 0, 0u)]
    [TestCase(REG_ZR, 4, 0, 31u)]
    [TestCase(REG_R19, 9, 5, 0x260u)]
    [TestCase(REG_R28, 20, 16, 0x1C0000u)]
    [TestCase(REG_R15, 17, 16, 0x30000u)]
    public static void IntegerFieldsUseTheNativeMask(regNumber reg, int hi, int lo, uint expected)
    {
        Assert.That(Integer(null, reg, hi, lo), Is.EqualTo(expected));
    }

    [TestCase(0, 20, 16, 0u)]
    [TestCase(31, 20, 16, 0x1F0000u)]
    [TestCase(127, 20, 14, 0x1FC000u)]
    [TestCase(1, 22, 22, 0x400000u)]
    [TestCase(7, 12, 10, 0x1C00u)]
    public static void UnsignedImmediateFieldsOccupyTheirExactBitRange(int imm, int hi, int lo, uint expected)
    {
        Assert.That(Unsigned(null, imm, hi, lo), Is.EqualTo(expected));
    }

    [TestCase(-16, 20, 16, 0x100000u)]
    [TestCase(-1, 20, 16, 0x1F0000u)]
    [TestCase(15, 20, 16, 0xF0000u)]
    [TestCase(-32, 10, 5, 0x400u)]
    [TestCase(31, 10, 5, 0x3E0u)]
    public static void SignedImmediateFieldsKeepTheSignBitWithoutSignExtension(int imm, int hi, int lo, uint expected)
    {
        Assert.That(Signed(null, imm, hi, lo), Is.EqualTo(expected));
    }

    [TestCase(0, 0u)]
    [TestCase(3, 0x60000u)]
    [TestCase(4, 0x400000u)]
    [TestCase(7, 0x460000u)]
    public static void SplitImmediateLeavesTheInterveningBitsClear(int imm, uint expected)
    {
        Assert.That(Split(null, imm, 22, 22, 18, 17), Is.EqualTo(expected));
    }

    [TestCase(-256, 0x200000u)]
    [TestCase(-1, 0x3F1C00u)]
    [TestCase(0, 0u)]
    [TestCase(255, 0x1F1C00u)]
    public static void SignedNineBitAddressOffsetsSplitSixHighAndThreeLowBits(int imm, uint expected)
    {
        Assert.That(SignedNine(null, imm), Is.EqualTo(expected));
    }

    [TestCase(-128, 0x1000u)]
    [TestCase(-1, 0x1FE0u)]
    [TestCase(0, 0u)]
    [TestCase(255, 0x1FE0u)]
    public static void EightBitImmediateAcceptsSignedOrUnsignedNativeRange(int imm, uint expected)
    {
        Assert.That(ImmEight(null, imm), Is.EqualTo(expected));
    }

    [TestCase(0, 0u)]
    [TestCase(1, 0x1000u)]
    [TestCase(6, 0xC00000u)]
    [TestCase(7, 0xC01000u)]
    public static void ThreeBitLookupIndexUsesBitsTwentyThreeTwentyTwoAndTwelve(int imm, uint expected)
    {
        Assert.That(UnsignedThree(null, imm), Is.EqualTo(expected));
    }

    [TestCase(-16, 4, 2, true, 0x80000u)]
    [TestCase(14, 4, 2, true, 0x70000u)]
    [TestCase(248, 5, 8, false, 0x1F0000u)]
    [TestCase(0, 5, 8, false, 0u)]
    public static void ScaledOffsetsDivideBeforeEncoding(int imm, int bits, int multiple, bool signed, uint expected)
    {
        var actual = signed
            ? SignedMultiple(null, imm, bits + 15, 16, multiple)
            : UnsignedMultiple(null, imm, bits + 15, 16, multiple);
        Assert.That(actual, Is.EqualTo(expected));
    }

    [TestCase(REG_V0, 0u)]
    [TestCase(REG_V2, 0x40u)]
    [TestCase(REG_V30, 0x3C0u)]
    public static void EvenVectorPairRegisterEncodesHalfTheBankIndex(regNumber reg, uint expected)
    {
        Assert.That(EvenVector(null, reg), Is.EqualTo(expected));
    }

    [TestCase(EA_1BYTE, 0u, 0u, 0u, 0x80000u)]
    [TestCase(EA_2BYTE, 0x400000u, 0x200000u, 0x20000u, 0x100000u)]
    [TestCase(EA_4BYTE, 0x800000u, 0x400000u, 0x40000u, 0x400000u)]
    [TestCase(EA_8BYTE, 0xC00000u, 0x600000u, 0x60000u, 0x800000u)]
    public static void ElementSizesKeepEachNativeFieldLayout(emitAttr size, uint standard, uint store,
        uint conversion, uint split)
    {
        Assert.That(ScalableSize(null, size), Is.EqualTo(standard));
        Assert.That(VectorSize(null, size), Is.EqualTo(standard));
        Assert.That(StoreSize(null, size), Is.EqualTo(store));
        Assert.That(ConversionSize(null, size), Is.EqualTo(conversion));
        Assert.That(SplitSize(null, size), Is.EqualTo(split));
        Assert.That(VlsSize(null, size), Is.EqualTo(((uint)store >> 11) & 0xC00));
    }

    [TestCase(EA_1BYTE, 0x400000u)]
    [TestCase(EA_2BYTE, 0x800000u)]
    [TestCase(EA_4BYTE, 0xC00000u)]
    public static void NarrowingUsesTheNextLargerElementSizeEncoding(emitAttr size, uint expected)
    {
        Assert.That(NarrowSize(null, size), Is.EqualTo(expected));
    }

    [TestCase(EA_4BYTE, 0u)]
    [TestCase(EA_8BYTE, 1u)]
    public static void ScalarWidthsSelectTheCorrectSingleBit(emitAttr size, uint bit)
    {
        Assert.That(SizeTwenty(null, size), Is.EqualTo(bit << 20));
        Assert.That(SizeTwentyOne(null, size), Is.EqualTo(bit << 21));
        Assert.That(SizeR(null, size), Is.EqualTo(bit << 22));
    }

    [TestCase(IF_SVE_HX_3A_B, EA_4BYTE, 0u)]
    [TestCase(IF_SVE_HX_3A_E, EA_8BYTE, 0x40000000u)]
    [TestCase(IF_SVE_IV_3A, EA_8BYTE, 0u)]
    [TestCase(IF_SVE_JI_3A_A, EA_4BYTE, 0x200000u)]
    [TestCase(IF_SVE_JI_3A_A, EA_8BYTE, 0u)]
    public static void GatherAndScatterWidthsKeepTheirOppositeSizeSelection(
        Emitter.insFormat format, emitAttr size, uint expected)
    {
        Assert.That(AddressSize(null, format, size), Is.EqualTo(expected));
    }

    [TestCase(INS_OPTS_SCALABLE_B, 0, false, 0x80000u)]
    [TestCase(INS_OPTS_SCALABLE_B, 7, false, 0xF0000u)]
    [TestCase(INS_OPTS_SCALABLE_B, 8, true, 0x80000u)]
    [TestCase(INS_OPTS_SCALABLE_H, 16, true, 0x100000u)]
    [TestCase(INS_OPTS_SCALABLE_S, 1, true, 0x5F0000u)]
    [TestCase(INS_OPTS_SCALABLE_D, 1, true, 0xDF0000u)]
    [TestCase(INS_OPTS_SCALABLE_D, 64, true, 0x800000u)]
    [TestCase(INS_OPTS_SCALABLE_D, 63, false, 0xDF0000u)]
    public static void VectorShiftCombinesTheNativeSizeAndImmediateFields(
        insOpts opt, int imm, bool right, uint expected)
    {
        Assert.That(ScalableShift(null, opt, imm, right), Is.EqualTo(expected));
    }

    [TestCase(EA_1BYTE, true, 1, 0x1E0u)]
    [TestCase(EA_1BYTE, true, 8, 0x100u)]
    [TestCase(EA_2BYTE, false, 0, 0x200u)]
    [TestCase(EA_4BYTE, true, 32, 0x400000u)]
    [TestCase(EA_8BYTE, true, 1, 0xC003E0u)]
    [TestCase(EA_8BYTE, true, 64, 0x800000u)]
    public static void PredicatedShiftSplitsTheImmediateAroundThePredicateField(
        emitAttr size, bool right, int imm, uint expected)
    {
        Assert.That(PredicatedShift(null, size, right, (nuint)imm), Is.EqualTo(expected));
    }

    [TestCase(INS_OPTS_SCALABLE_B, 15, 0x31C0000u, 0x1F0000u)]
    [TestCase(INS_OPTS_SCALABLE_H, 7, 0x1980000u, 0x1E0000u)]
    [TestCase(INS_OPTS_SCALABLE_S, 3, 0xD00000u, 0x1C0000u)]
    [TestCase(INS_OPTS_SCALABLE_D, 1, 0xC00000u, 0x180000u)]
    public static void PredicateBroadcastAndQuadwordDupKeepDifferentIndexLayouts(
        insOpts opt, int imm, uint predicate, uint quadword)
    {
        Assert.That(PredicateBroadcast(null, opt, imm), Is.EqualTo(predicate));
        Assert.That(QuadwordBroadcast(null, opt, imm), Is.EqualTo(quadword));
    }

    [TestCase(EA_1BYTE, 0, 0x10000u)]
    [TestCase(EA_1BYTE, 63, 0xDF0000u)]
    [TestCase(EA_2BYTE, 31, 0xDE0000u)]
    [TestCase(EA_4BYTE, 15, 0xDC0000u)]
    [TestCase(EA_8BYTE, 7, 0xD80000u)]
    public static void IndexedDupCombinesLaneSizeAndIndexBeforeSplitting(emitAttr size, int index, uint expected)
    {
        Assert.That(BroadcastIndex(null, size, index), Is.EqualTo(expected));
    }

    [TestCase(INS_OPTS_SCALABLE_B, 1, 7)]
    [TestCase(INS_OPTS_SCALABLE_B, 8, 0)]
    [TestCase(INS_OPTS_SCALABLE_H, 16, 0)]
    [TestCase(INS_OPTS_SCALABLE_S, 32, 0)]
    [TestCase(INS_OPTS_SCALABLE_D, 1, 63)]
    [TestCase(INS_OPTS_SCALABLE_D, 64, 0)]
    public static void RotateAndNarrowImmediatesUseWidthMinusImmediate(insOpts opt, int imm, int expected)
    {
        Assert.That(ImmediateDifference(null, imm, opt), Is.EqualTo((nint)expected));
    }

    [TestCase(0, 0u)]
    [TestCase(1, 0x10000u)]
    public static void ComplexAddRotationUsesTheEncodedValueRatherThanDegrees(int imm, uint expected)
    {
        Assert.That(Rotation90(null, imm), Is.EqualTo(expected));
    }

    [TestCase(0, 0u)]
    [TestCase(3, 0x6000u)]
    public static void ComplexMultiplyRotationUsesBitsFourteenAndThirteen(int imm, uint expected)
    {
        Assert.That(RotationAll(null, imm), Is.EqualTo(expected));
    }

    [TestCase(0, 0u)]
    [TestCase(1, 0x20u)]
    public static void SmallFloatSelectorOccupiesBitFive(int imm, uint expected)
    {
        Assert.That(SmallFloat(null, imm), Is.EqualTo(expected));
    }

    [TestCase(false, 0u)]
    [TestCase(true, 1u)]
    public static void MergeQualifierUsesItsFormatSpecificBit(bool merge, uint bit)
    {
        Assert.That(MergeSixteen(null, merge), Is.EqualTo(bit << 16));
        Assert.That(MergeFour(null, merge), Is.EqualTo(bit << 4));
    }

    [TestCase(IF_SVE_DL_2A, false, 0u)]
    [TestCase(IF_SVE_DL_2A, true, 0x400u)]
    [TestCase(IF_SVE_DY_3A, false, 0u)]
    [TestCase(IF_SVE_DY_3A, true, 0x2000u)]
    public static void VectorLengthSpecifierUsesTheDescriptorBit(
        Emitter.insFormat format, bool four, uint expected)
    {
        var id = OutputEmitter.Descriptor(format);
        id.idVectorLength4x(four);
        Assert.That(VectorLength(null, id), Is.EqualTo(expected));
    }

    [TestCase(INS_sve_ld1b, EA_1BYTE, 0u)]
    [TestCase(INS_sve_ld1h, EA_2BYTE, 0x200000u)]
    [TestCase(INS_sve_ld1sb, EA_2BYTE, 0x400000u)]
    [TestCase(INS_sve_ldnf1w, EA_4BYTE, 0u)]
    [TestCase(INS_sve_ldff1h, EA_4BYTE, 0x400000u)]
    [TestCase(INS_sve_ldff1sh, EA_4BYTE, 0x200000u)]
    [TestCase(INS_sve_ldff1w, EA_8BYTE, 0x200000u)]
    [TestCase(INS_sve_ldnf1b, EA_8BYTE, 0x600000u)]
    [TestCase(INS_sve_ldnf1sh, EA_8BYTE, 0u)]
    public static void DtypePreservesTheInstructionSpecificSignedLoadEncoding(
        instruction ins, emitAttr size, uint bits)
    {
        const uint code = 0xA5000001;
        Assert.That(Dtype(null, ins, size, code), Is.EqualTo(code | bits));
    }

    [TestCase(IF_SVE_IH_3A_F, EA_4BYTE, 0x408000u)]
    [TestCase(IF_SVE_IH_3A_F, EA_8BYTE, 0x608000u)]
    [TestCase(IF_SVE_IH_3A_F, EA_16BYTE, 0x100000u)]
    [TestCase(IF_SVE_II_4A_H, EA_4BYTE, 0x404000u)]
    [TestCase(IF_SVE_II_4A_H, EA_8BYTE, 0x604000u)]
    [TestCase(IF_SVE_II_4A_H, EA_16BYTE, 0x8000u)]
    public static void Ld1wIncludesRequiredBitsOutsideDtype(
        Emitter.insFormat format, emitAttr size, uint bits)
    {
        const uint code = 0xA5000001;
        Assert.That(DtypeWord(null, INS_sve_ld1w, format, size, code), Is.EqualTo(code | bits));
    }

    [TestCase(INS_sve_ld1rsh, IF_SVE_IC_3A_A, EA_4BYTE, 0x2000u)]
    [TestCase(INS_sve_ld1rsh, IF_SVE_IC_3A_A, EA_8BYTE, 0u)]
    [TestCase(INS_sve_ld1rw, IF_SVE_IC_3A_A, EA_4BYTE, 0x4000u)]
    [TestCase(INS_sve_ld1rw, IF_SVE_IC_3A_A, EA_8BYTE, 0x6000u)]
    [TestCase(INS_sve_ld1rh, IF_SVE_IC_3A_B, EA_2BYTE, 0x2000u)]
    [TestCase(INS_sve_ld1rh, IF_SVE_IC_3A_B, EA_4BYTE, 0x4000u)]
    [TestCase(INS_sve_ld1rh, IF_SVE_IC_3A_B, EA_8BYTE, 0x6000u)]
    [TestCase(INS_sve_ld1rsb, IF_SVE_IC_3A_B, EA_2BYTE, 0x1004000u)]
    [TestCase(INS_sve_ld1rsb, IF_SVE_IC_3A_B, EA_4BYTE, 0x1002000u)]
    [TestCase(INS_sve_ld1rsb, IF_SVE_IC_3A_B, EA_8BYTE, 0x1000000u)]
    [TestCase(INS_sve_ld1rb, IF_SVE_IC_3A_C, EA_1BYTE, 0u)]
    [TestCase(INS_sve_ld1rb, IF_SVE_IC_3A_C, EA_2BYTE, 0x2000u)]
    [TestCase(INS_sve_ld1rb, IF_SVE_IC_3A_C, EA_4BYTE, 0x4000u)]
    [TestCase(INS_sve_ld1rb, IF_SVE_IC_3A_C, EA_8BYTE, 0x6000u)]
    public static void BroadcastLoadsKeepDtypeHighLowAndSignExtensionBits(
        instruction ins, Emitter.insFormat format, emitAttr size, uint bits)
    {
        const uint code = 0xA4000001;
        Assert.That(DtypeSplit(null, ins, format, size, code), Is.EqualTo(code | bits));
    }

    [TestCase(-8)]
    [TestCase(0)]
    [TestCase(8)]
    public static void FixedWidthWriterUsesTheWritableAliasButAdvancesTheExecutableCursor(int alias)
    {
        var emitter = NewEmitter();
        emitter.writeableOffset = alias;
        byte* buffer = stackalloc byte[32];
        new Span<byte>(buffer, 32).Fill(0xA5);
        var dst = buffer + 13;
        var end = unchecked(dst + emitter.emitOutputLong(dst, 0xFE123456u));
        Assert.That((nuint)end, Is.EqualTo((nuint)(dst + 4)));
        Assert.That(new ReadOnlySpan<byte>(dst + alias, 4).ToArray(),
            Is.EqualTo(new byte[] { 0x56, 0x34, 0x12, 0xFE }));
        for (var i = 0; i < 32; i++)
        {
            if (i < 13 + alias || i >= 17 + alias)
            {
                Assert.That(buffer[i], Is.EqualTo(0xA5));
            }
        }
    }

    [TestCase(INS_sve_asr, IF_SVE_AA_3A, 0x049083E0u)]
    [TestCase(INS_sve_movprfx, IF_SVE_BI_2A, 0x0420BFE0u)]
    [TestCase(INS_sve_setffr, IF_SVE_DQ_0A, 0x252C9000u)]
    [TestCase(INS_sve_mov, IF_SVE_BV_2B, 0x05904000u)]
    [TestCase(INS_sve_mov, IF_SVE_EB_1B, 0x25B8C000u)]
    public static void OutputUsesRealEncodingTablesAndAdvancesOneInstruction(
        instruction ins, Emitter.insFormat format, uint expected)
    {
        var emitter = NewEmitter();
        var id = OutputEmitter.Descriptor(format);
        id.idIns(ins);
        id.idInsOpt(INS_OPTS_SCALABLE_S);
        id.idReg2(format is IF_SVE_AA_3A or IF_SVE_BV_2B ? REG_P0 : REG_V31);
        id.idReg3(REG_V31);
        byte* buffer = stackalloc byte[12];
        new Span<byte>(buffer, 12).Fill(0xA5);
        var end = Output(emitter, buffer + 4, id);
        Assert.That((nuint)end, Is.EqualTo((nuint)(buffer + 8)));
        Assert.That(Unsafe.ReadUnaligned<uint>(buffer + 4), Is.EqualTo(expected));
        Assert.That(new ReadOnlySpan<byte>(buffer, 4).ToArray(), Is.All.EqualTo(0xA5));
        Assert.That(new ReadOnlySpan<byte>(buffer + 8, 4).ToArray(), Is.All.EqualTo(0xA5));
    }

    [Test]
    public static void UnexpectedFormatKeepsTheReturnCursorAndBuffer()
    {
        var emitter = NewEmitter();
        var id = OutputEmitter.Descriptor(IF_EN5A);
        byte* buffer = stackalloc byte[12];
        new Span<byte>(buffer, 12).Fill(0xA5);
        byte* end = null;
#if DEBUG
        var capture = Arm64SveInstructionSanityTests.Capture(() => end = Output(emitter, buffer + 4, id));
        Assert.That(capture.Assertions, Is.EqualTo(new[] { "!\"Unexpected format\"" }));
        Assert.That(capture.Output, Is.Empty);
#else
        end = Output(emitter, buffer + 4, id);
#endif
        Assert.That((nuint)end, Is.EqualTo((nuint)(buffer + 4)));
        Assert.That(new ReadOnlySpan<byte>(buffer, 12).ToArray(), Is.All.EqualTo(0xA5));
    }

#if DEBUG
    [TestCase((emitAttr)3, 0, 0x10000u)]
    [TestCase((emitAttr)6, 0, 0x20000u)]
    [TestCase((emitAttr)12, 1, 0xC0000u)]
    public static void IndexedDupKeepsNativeLowBitSelectionAfterInvalidLaneSizeAssertion(
        emitAttr size, int index, uint expected)
    {
        uint result = 0;
        var capture = Arm64SveInstructionSanityTests.Capture(() => result = BroadcastIndex(null, size, index));
        Assert.That(capture.Assertions, Is.EqualTo(new[] { "genExactlyOneBit(value)" }));
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public static void ZeroIndexedDupLaneSizePreservesBothNativeAssertionsInOrder()
    {
        var capture = Arm64SveInstructionSanityTests.Capture(() => BroadcastIndex(null, (emitAttr)0, 0));
        Assert.That(capture.Assertions, Is.EqualTo(new[] { "genExactlyOneBit(value)", "value != 0" }));
    }

    [Test]
    public static void UnsignedOverflowAssertsThenPreservesNativeUnmaskedContinuation()
    {
        uint result = 0;
        var capture = Arm64SveInstructionSanityTests.Capture(() => result = Unsigned(null, -1, 20, 16));
        Assert.That(capture.Assertions, Is.EqualTo(new[] { "imm < imm_max", "(result >> lo) == imm" }));
        Assert.That(result, Is.EqualTo(0xFFFF0000u));
    }

    [Test]
    public static void SignedOverflowAssertsThenMasksTheField()
    {
        uint result = 0;
        var capture = Arm64SveInstructionSanityTests.Capture(() => result = Signed(null, 16, 20, 16));
        Assert.That(capture.Assertions, Is.EqualTo(new[] { "imm_min <= imm && imm < imm_max" }));
        Assert.That(result, Is.EqualTo(0x100000u));
    }

    [Test]
    public static void OddPairRegisterAssertsBeforeNativeDivision()
    {
        uint result = 0;
        var capture = Arm64SveInstructionSanityTests.Capture(() => result = EvenVector(null, REG_V31));
        Assert.That(capture.Assertions, Is.EqualTo(new[] { "ureg % 2 == 0" }));
        Assert.That(result, Is.EqualTo(0x3C0u));
    }

    [Test]
    public static void InvalidWordDtypeSizePreservesBothNativeAssertionsAndTheOriginalCode()
    {
        uint result = 0;
        var capture = Arm64SveInstructionSanityTests.Capture(
            () => result = DtypeWord(null, INS_sve_ld1w, IF_SVE_IH_3A_F, EA_1BYTE, 0xA5000001));
        Assert.That(capture.Assertions, Is.EqualTo(new[]
        {
            "!\"Invalid size for encoding dtype.\"", "!\"Invalid instruction format\"",
        }));
        Assert.That(result, Is.EqualTo(0xA5000001u));
    }

    [Test]
    public static void InvalidOutputShiftKeepsNativeAssertionOrderThroughEncoding()
    {
        var id = OutputEmitter.Descriptor(IF_SVE_GA_2A);
        var emitter = NewEmitter();
        byte* buffer = stackalloc byte[4];
        new Span<byte>(buffer, 4).Fill(0xA5);
        byte* end = null;
        var capture = Arm64SveInstructionSanityTests.Capture(() => end = Output(emitter, buffer, id));
        Assert.That(capture.Assertions, Is.EqualTo(new[]
        {
            "id.idInsOpt() == INS_OPTS_SCALABLE_H",
            "emitInsIsVectorRightShift(id.idIns())",
            "isValidVectorShiftAmount(imm, EA_4BYTE, true)",
            "encoding_found",
            "(code != BAD_CODE)",
            "(shiftAmount > 0) && (shiftAmount <= getBitWidth(size))",
            "ureg % 2 == 0",
        }));
        Assert.That((nuint)end, Is.EqualTo((nuint)(buffer + 4)));
        Assert.That(Unsafe.ReadUnaligned<uint>(buffer), Is.EqualTo(uint.MaxValue));
    }
#endif

    private static Emitter NewEmitter()
    {
        return (OutputEmitter)RuntimeHelpers.GetUninitializedObject(typeof(OutputEmitter));
    }

    private sealed class OutputEmitter(CodeGen codeGen) : Emitter(codeGen)
    {
        internal static instrDesc Descriptor(Emitter.insFormat format)
        {
            var id = new instrDescBasic();
            id.idIns(INS_sve_mov);
            id.idInsFmt(format);
            id.idOpSize(EA_SCALABLE);
            id.idInsOpt(INS_OPTS_SCALABLE_B);
            id.idReg1(REG_V0);
            id.idReg2(REG_V31);
            id.idSmallCns(0);

            return id;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitOutput_InstrSve")]
    private static extern byte* Output(Emitter emitter, byte* dst, Emitter.instrDesc id);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "insEncodeReg_V")]
    private static extern uint Vector(Emitter? emitter, regNumber reg, int hi, int lo);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "insEncodeReg_P")]
    private static extern uint Predicate(Emitter? emitter, regNumber reg, int hi, int lo);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "insEncodeReg_R")]
    private static extern uint Integer(Emitter? emitter, regNumber reg, int hi, int lo);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "insEncodeUimm")]
    private static extern uint Unsigned(Emitter? emitter, nint imm, int hi, int lo);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "insEncodeSimm")]
    private static extern uint Signed(Emitter? emitter, nint imm, int hi, int lo);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "insEncodeSplitUimm")]
    private static extern uint Split(Emitter? emitter, nint imm, int hi1, int lo1, int hi2, int lo2);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "insEncodeUimm_MultipleOf")]
    private static extern uint UnsignedMultiple(Emitter? emitter, nint imm, int hi, int lo, nint mul);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "insEncodeSimm_MultipleOf")]
    private static extern uint SignedMultiple(Emitter? emitter, nint imm, int hi, int lo, nint mul);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "insEncodeSimm9h9l_21_to_16_and_12_to_10")]
    private static extern uint SignedNine(Emitter? emitter, nint imm);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "insEncodeImm8_12_to_5")]
    private static extern uint ImmEight(Emitter? emitter, nint imm);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "insEncodeUimm3h3l_23_to_22_and_12")]
    private static extern uint UnsignedThree(Emitter? emitter, nint imm);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "insEncodeReg_V_9_to_6_Times_Two")]
    private static extern uint EvenVector(Emitter? emitter, regNumber reg);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "insEncodeSveElemsize")]
    private static extern uint ScalableSize(Emitter? emitter, emitAttr size);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "insEncodeElemsize")]
    private static extern uint VectorSize(Emitter? emitter, emitAttr size);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "insEncodeSveElemsize_22_to_21")]
    private static extern uint StoreSize(Emitter? emitter, emitAttr size);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "insEncodeSveElemsize_18_to_17")]
    private static extern uint ConversionSize(Emitter? emitter, emitAttr size);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "insEncodeSveElemsize_tszh_23_tszl_20_to_19")]
    private static extern uint SplitSize(Emitter? emitter, emitAttr size);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "insEncodeVLSElemsize")]
    private static extern uint VlsSize(Emitter? emitter, emitAttr size);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "insEncodeNarrowingSveElemsize")]
    private static extern uint NarrowSize(Emitter? emitter, emitAttr size);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "insEncodeSveElemsize_sz_20")]
    private static extern uint SizeTwenty(Emitter? emitter, emitAttr size);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "insEncodeSveElemsize_sz_21")]
    private static extern uint SizeTwentyOne(Emitter? emitter, emitAttr size);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "insEncodeSveElemsize_R_22")]
    private static extern uint SizeR(Emitter? emitter, emitAttr size);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "insEncodeSveElemsize_30_or_21")]
    private static extern uint AddressSize(Emitter? emitter, Emitter.insFormat fmt, emitAttr size);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "insEncodeSveElemsizeWithShift_tszh_tszl_imm3")]
    private static extern uint ScalableShift(Emitter? emitter, insOpts opt, nint imm, bool right);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "insEncodeSveShift_23_to_22_9_to_0")]
    private static extern uint PredicatedShift(Emitter? emitter, emitAttr size, bool right, nuint imm);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "insEncodeSveElemsize_tszh_tszl_and_imm")]
    private static extern uint PredicateBroadcast(Emitter? emitter, insOpts opt, nint imm);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "insEncodeSveElemsizeWithImmediate_i1_tsz")]
    private static extern uint QuadwordBroadcast(Emitter? emitter, insOpts opt, nint imm);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "insEncodeSveBroadcastIndex")]
    private static extern uint BroadcastIndex(Emitter? emitter, emitAttr size, nint index);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "insSveGetImmDiff")]
    private static extern nint ImmediateDifference(Emitter? emitter, nint imm, insOpts opt);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "insEncodeSveImm90_or_270_rot")]
    private static extern uint Rotation90(Emitter? emitter, nint imm);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "insEncodeSveImm0_to_270_rot")]
    private static extern uint RotationAll(Emitter? emitter, nint imm);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "insEncodeSveSmallFloatImm")]
    private static extern uint SmallFloat(Emitter? emitter, nint imm);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "insEncodePredQualifier_16")]
    private static extern uint MergeSixteen(Emitter? emitter, bool merge);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "insEncodePredQualifier_4")]
    private static extern uint MergeFour(Emitter? emitter, bool merge);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "insEncodeVectorLengthSpecifier")]
    private static extern uint VectorLength(Emitter? emitter, Emitter.instrDesc id);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "insEncodeSveElemsize_dtype")]
    private static extern uint Dtype(Emitter? emitter, instruction ins, emitAttr size, uint code);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "insEncodeSveElemsize_dtype_ld1w")]
    private static extern uint DtypeWord(Emitter? emitter, instruction ins, Emitter.insFormat fmt, emitAttr size, uint code);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "insEncodeSveElemsize_dtypeh_dtypel")]
    private static extern uint DtypeSplit(Emitter? emitter, instruction ins, Emitter.insFormat fmt, emitAttr size, uint code);
}
#endif
