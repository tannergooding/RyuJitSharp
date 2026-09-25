// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using NUnit.Framework;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class EmitterConstantDataTests
{
    [Test]
    public static void SectionsOwnTheirBytesAndRetainInsertionOrderAndAlignment()
    {
        WithEmitter((_, emitter) =>
        {
            byte[] input = [1, 2, 3, 4];
            Assert.That(emitter.emitDataConst(input, 1, TYP_INT), Is.Zero);
            input[0] = 9;
            Assert.That(emitter.emitDataConst(input, 16, TYP_INT), Is.EqualTo(16));
            Assert.That(emitter.emitDataGenBeg(8, 4, TYP_LONG), Is.EqualTo(20));
            emitter.emitDataGenData(0, input);
            emitter.emitDataGenData(4, input);
            emitter.emitDataGenEnd();

            var first = Required(emitter.emitConsDsc.dsdList);
            var second = Required(first.dsNext);
            var third = Required(second.dsNext);
            Assert.That(first.Data, Is.EqualTo(new byte[] { 1, 2, 3, 4 }));
            Assert.That(first.dsAlignment, Is.EqualTo(1));
            Assert.That(second.dsOffset, Is.EqualTo(16));
            Assert.That(second.dsAlignment, Is.EqualTo(16));
            Assert.That(third.Data, Is.EqualTo(new byte[] { 9, 2, 3, 4, 9, 2, 3, 4 }));
            Assert.That(emitter.emitConsDsc.dsdLast, Is.SameAs(third));
            Assert.That(third.dsNext, Is.Null);
            Assert.That(emitter.emitConsDsc.dsdOffs, Is.EqualTo(28));
#if DEBUG
            Assert.That(emitter.emitDataSecCur, Is.Null);
#else
            Assert.That(emitter.emitDataSecCur, Is.SameAs(third));
#endif
        });
    }

    [Test]
    public static void PrefixReuseRequiresDeclaredAlignmentAndPromotesOnlyExactFloatingMatches()
    {
        WithEmitter((_, emitter) =>
        {
            byte[] bytes = [1, 2, 3, 4, 5, 6, 7, 8];
            Assert.That(emitter.emitDataConst(bytes, 4, TYP_LONG), Is.Zero);
            var first = Required(emitter.emitConsDsc.dsdList);
            Assert.That(emitter.emitDataConst(bytes.AsSpan(0, 4), 4, TYP_FLOAT), Is.Zero);
            Assert.That(first.dsDataType, Is.EqualTo(TYP_LONG));
            Assert.That(emitter.emitDataConst(bytes, 4, TYP_DOUBLE), Is.Zero);
            Assert.That(first.dsDataType, Is.EqualTo(TYP_DOUBLE));
            Assert.That(emitter.emitDataConst(bytes, 4, TYP_LONG), Is.Zero);
            Assert.That(first.dsDataType, Is.EqualTo(TYP_DOUBLE));
            Assert.That(emitter.emitDataConst(bytes, 8, TYP_DOUBLE), Is.EqualTo(8));
            Assert.That(emitter.emitConsDsc.dsdOffs, Is.EqualTo(16));
        });
    }

    [TestCase(false, 8u)]
    [TestCase(true, 4u)]
    public static void BlockTablesKeepBlockIdentityAndUseEmittedElementAlignment(bool relative, uint elementSize)
    {
        WithEmitter((compiler, emitter) =>
        {
            _ = emitter.emitDataConst(new byte[4], 4, TYP_INT);
            var offset = emitter.emitBBTableDataGenBeg(2, relative);
            var firstBlock = new BasicBlock(null, null);
            var secondBlock = new BasicBlock(null, null);
            emitter.emitDataGenData(0, firstBlock);
            emitter.emitDataGenData(1, secondBlock);
            emitter.emitDataGenEnd();

            var table = Required(emitter.emitConsDsc.dsdLast);
            Assert.That(offset, Is.EqualTo(elementSize));
            Assert.That(table.dsSize, Is.EqualTo(2 * elementSize));
            Assert.That(table.dsAlignment, Is.EqualTo(elementSize));
            Assert.That(table.dsDataType, Is.EqualTo(TYP_UNKNOWN));
            Assert.That(table.Blocks[0], Is.SameAs(firstBlock));
            Assert.That(table.Blocks[1], Is.SameAs(secondBlock));
            Assert.That(emitter.emitConsDsc.dsdOffs, Is.EqualTo(3 * elementSize));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void LookupExaminesExactlySixtyFiveSectionsIncludingNonDataSections(bool blockTables)
    {
        WithEmitter((compiler, emitter) =>
        {
            for (uint i = 0; i < 64; i++)
            {
                if (blockTables)
                {
                    _ = emitter.emitBBTableDataGenBeg(1, true);
                    emitter.emitDataGenData(0, new BasicBlock(null, null));
                }
                else
                {
                    _ = emitter.emitDataGenBeg(4, 4, TYP_INT);
                    emitter.emitDataGenData(0, BitConverter.GetBytes(i));
                }
                emitter.emitDataGenEnd();
            }

            var sixtyFifth = BitConverter.GetBytes(100u);
            var sixtySixth = BitConverter.GetBytes(101u);
            Assert.That(emitter.emitDataConst(sixtyFifth, 4, TYP_INT), Is.EqualTo(256));
            Assert.That(emitter.emitDataConst(sixtySixth, 4, TYP_INT), Is.EqualTo(260));
            Assert.That(emitter.emitDataGenFind(sixtyFifth, 4, TYP_INT), Is.EqualTo(256));
            Assert.That(emitter.emitDataGenFind(sixtySixth, 4, TYP_INT), Is.EqualTo(uint.MaxValue));
            Assert.That(emitter.emitDataConst(sixtySixth, 4, TYP_INT), Is.EqualTo(264));
        });
    }

    [Test]
    public static void BlockConstantsDoNotDeduplicate()
    {
        WithEmitter((_, emitter) =>
        {
            byte[] bytes = [1, 2, 3, 4];
            var first = emitter.emitBlkConst(bytes, 4, TYP_INT);
            var second = emitter.emitBlkConst(bytes, 16, TYP_INT);

            Assert.That(Compiler.eeGetJitDataOffs(first), Is.Zero);
            Assert.That(Compiler.eeGetJitDataOffs(second), Is.EqualTo(16));
            Assert.That(emitter.emitConsDsc.dsdOffs, Is.EqualTo(20));
        });
    }

    [TestCase(0x0000000000000000UL)]
    [TestCase(0x8000000000000000UL)]
    [TestCase(0x0000000000000001UL)]
    [TestCase(0x7FEFFFFFFFFFFFFFUL)]
    [TestCase(0x7FF0000000000000UL)]
    [TestCase(0xFFF0000000000000UL)]
    [TestCase(0x7FF82468A0000000UL)]
    [TestCase(0xFFF82468A0000000UL)]
    [TestCase(0x7FF0000000000001UL)]
    public static void DoubleConstantsPreserveEveryPayloadBit(ulong bits)
    {
        WithEmitter((_, emitter) =>
        {
            var handle = emitter.emitFltOrDblConst(BitConverter.UInt64BitsToDouble(bits), EA_8BYTE);
            Assert.That(Compiler.eeGetJitDataOffs(handle), Is.Zero);
            var section = Required(emitter.emitConsDsc.dsdList);
            Assert.That(BitConverter.ToUInt64(section.Data), Is.EqualTo(bits));
            Assert.That(section.dsDataType, Is.EqualTo(TYP_DOUBLE));
            Assert.That(section.dsAlignment, Is.EqualTo(8));
        });
    }

    [TestCase(0x0000000000000000UL, 0x00000000u)]
    [TestCase(0x8000000000000000UL, 0x80000000u)]
    [TestCase(0x3690000000000000UL, 0x00000000u)]
    [TestCase(0x3690000000000001UL, 0x00000001u)]
    [TestCase(0x36A0000000000000UL, 0x00000001u)]
    [TestCase(0x3FF0000010000000UL, 0x3F800000u)]
    [TestCase(0x3FF0000010000001UL, 0x3F800001u)]
    [TestCase(0x47EFFFFFE0000000UL, 0x7F7FFFFFu)]
    [TestCase(0x7FEFFFFFFFFFFFFFUL, 0x7F800000u)]
    [TestCase(0x7FF0000000000000UL, 0x7F800000u)]
    [TestCase(0xFFF0000000000000UL, 0xFF800000u)]
    [TestCase(0x7FF82468A0000000UL, 0x7FC12345u)]
    [TestCase(0xFFF82468A0000000UL, 0xFFC12345u)]
    [TestCase(0x7FF02468A0000000UL, 0x7FC12345u)]
    [TestCase(0x7FF0000000000001UL, 0x7FC00000u)]
    public static void SingleConstantsUseNativeX64RoundingAndNaNPayloadConversion(ulong bits, uint expected)
    {
        WithEmitter((_, emitter) =>
        {
            var handle = emitter.emitFltOrDblConst(BitConverter.UInt64BitsToDouble(bits), EA_4BYTE);
            Assert.That(Compiler.eeGetJitDataOffs(handle), Is.Zero);
            var section = Required(emitter.emitConsDsc.dsdList);
            Assert.That(BitConverter.ToUInt32(section.Data), Is.EqualTo(expected));
            Assert.That(section.dsDataType, Is.EqualTo(TYP_FLOAT));
            Assert.That(section.dsAlignment, Is.EqualTo(4));
        });
    }

    [Test]
    public static void FloatingConstantsDeduplicateBitsRatherThanNumericEquality()
    {
        WithEmitter((_, emitter) =>
        {
            var positiveZero = emitter.emitFltOrDblConst(0.0, EA_8BYTE);
            var negativeZero = emitter.emitFltOrDblConst(BitConverter.UInt64BitsToDouble(0x8000000000000000), EA_8BYTE);
            var nan = BitConverter.UInt64BitsToDouble(0x7FF8000000000001);
            var firstNaN = emitter.emitFltOrDblConst(nan, EA_8BYTE);
            var repeatedNaN = emitter.emitFltOrDblConst(nan, EA_8BYTE);
            var otherNaN = emitter.emitFltOrDblConst(BitConverter.UInt64BitsToDouble(0x7FF8000000000002), EA_8BYTE);

            Assert.That(Compiler.eeGetJitDataOffs(positiveZero), Is.Zero);
            Assert.That(Compiler.eeGetJitDataOffs(negativeZero), Is.EqualTo(8));
            Assert.That(Compiler.eeGetJitDataOffs(firstNaN), Is.EqualTo(16));
            Assert.That((nuint)repeatedNaN, Is.EqualTo((nuint)firstNaN));
            Assert.That(Compiler.eeGetJitDataOffs(otherNaN), Is.EqualTo(24));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void DoubleAlignmentUsesTheNativeEffectiveCodePreference(bool small)
    {
        WithEmitter((compiler, emitter) =>
        {
            compiler.opts.compCodeOpt = small ? Compiler.SMALL_CODE : Compiler.BLENDED_CODE;
            Assert.That(compiler.compCodeOpt, Is.EqualTo(Compiler.BLENDED_CODE));
            _ = emitter.emitDataConst(new byte[4], 4, TYP_INT);
            var handle = emitter.emitFltOrDblConst(1.0, EA_8BYTE);
            Assert.That(Compiler.eeGetJitDataOffs(handle), Is.EqualTo(8));
            var section = Required(emitter.emitConsDsc.dsdLast);
            Assert.That(section.dsAlignment, Is.EqualTo(8));
            Assert.That(BitConverter.ToDouble(section.Data), Is.EqualTo(1.0));
        });
    }

    [TestCase(0u)]
    [TestCase(1u)]
    [TestCase(4u)]
    [TestCase(0x0FFFFFFFu)]
    public static void TaggedHandlesRoundTripNativeOffsets(uint offset)
    {
        var handle = Compiler.eeFindJitDataOffs(offset);
        Assert.That((nuint)handle, Is.EqualTo((nuint)((offset << 2) | 1)));
        Assert.That(Compiler.eeIsJitDataOffs(handle), Is.True);
        Assert.That(Compiler.eeGetJitDataOffs(handle), Is.EqualTo(offset));
    }

    [TestCase(0UL)]
    [TestCase(2UL)]
    [TestCase(3UL)]
    [TestCase(4UL)]
    [TestCase(0x100000001UL)]
    [TestCase(0xFFFFFFFF00000001UL)]
    public static void RealPointerWidthsAndOtherTagsAreNotDataOffsets(ulong bits)
    {
        var handle = (CORINFO_FIELD_STRUCT_*)unchecked((nuint)bits);
        Assert.That(Compiler.eeIsJitDataOffs(handle), Is.False);
        Assert.That(Compiler.eeGetJitDataOffs(handle), Is.EqualTo(-1));
    }

#if !DEBUG
    [TestCase(0x10000000u, 0x10000000)]
    [TestCase(0x20000000u, -0x20000000)]
    [TestCase(0x3FFFFFFFu, -1)]
    public static void LargeTaggedOffsetsPreserveTheNativeSignedDecoder(uint offset, int decoded)
    {
        var handle = Compiler.eeFindJitDataOffs(offset);
        Assert.That(Compiler.eeIsJitDataOffs(handle), Is.True);
        Assert.That(Compiler.eeGetJitDataOffs(handle), Is.EqualTo(decoded));
    }
#endif

    private static Emitter.dataSection Required(Emitter.dataSection? section)
        => section ?? throw new AssertionException("Missing expected constant-data section.");

    private static void WithEmitter(Action<Compiler, Emitter> action)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_INT, REG_RAX, (compiler, codeGen, _) =>
            action(compiler, codeGen.Emitter));
    }
}
