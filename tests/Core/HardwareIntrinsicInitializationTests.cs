// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;
using NUnit.Framework;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class HardwareIntrinsicInitializationTests
{
    [TestCase(FloatComparisonMode.OrderedEqualNonSignaling, NI_X86Base_CompareEqual, NI_AVX_CompareEqual, NI_X86Base_CompareScalarEqual, NI_AVX512_CompareEqualMask)]
    [TestCase(FloatComparisonMode.OrderedGreaterThanSignaling, NI_X86Base_CompareGreaterThan, NI_AVX_CompareGreaterThan, NI_X86Base_CompareScalarGreaterThan, NI_AVX512_CompareGreaterThanMask)]
    [TestCase(FloatComparisonMode.OrderedGreaterThanOrEqualSignaling, NI_X86Base_CompareGreaterThanOrEqual, NI_AVX_CompareGreaterThanOrEqual, NI_X86Base_CompareScalarGreaterThanOrEqual, NI_AVX512_CompareGreaterThanOrEqualMask)]
    [TestCase(FloatComparisonMode.OrderedLessThanSignaling, NI_X86Base_CompareLessThan, NI_AVX_CompareLessThan, NI_X86Base_CompareScalarLessThan, NI_AVX512_CompareLessThanMask)]
    [TestCase(FloatComparisonMode.OrderedLessThanOrEqualSignaling, NI_X86Base_CompareLessThanOrEqual, NI_AVX_CompareLessThanOrEqual, NI_X86Base_CompareScalarLessThanOrEqual, NI_AVX512_CompareLessThanOrEqualMask)]
    [TestCase(FloatComparisonMode.UnorderedNotEqualNonSignaling, NI_X86Base_CompareNotEqual, NI_AVX_CompareNotEqual, NI_X86Base_CompareScalarNotEqual, NI_AVX512_CompareNotEqualMask)]
    [TestCase(FloatComparisonMode.UnorderedNotGreaterThanSignaling, NI_X86Base_CompareNotGreaterThan, NI_AVX_CompareNotGreaterThan, NI_X86Base_CompareScalarNotGreaterThan, NI_AVX512_CompareNotGreaterThanMask)]
    [TestCase(FloatComparisonMode.UnorderedNotGreaterThanOrEqualSignaling, NI_X86Base_CompareNotGreaterThanOrEqual, NI_AVX_CompareNotGreaterThanOrEqual, NI_X86Base_CompareScalarNotGreaterThanOrEqual, NI_AVX512_CompareNotGreaterThanOrEqualMask)]
    [TestCase(FloatComparisonMode.UnorderedNotLessThanSignaling, NI_X86Base_CompareNotLessThan, NI_AVX_CompareNotLessThan, NI_X86Base_CompareScalarNotLessThan, NI_AVX512_CompareNotLessThanMask)]
    [TestCase(FloatComparisonMode.UnorderedNotLessThanOrEqualSignaling, NI_X86Base_CompareNotLessThanOrEqual, NI_AVX_CompareNotLessThanOrEqual, NI_X86Base_CompareScalarNotLessThanOrEqual, NI_AVX512_CompareNotLessThanOrEqualMask)]
    [TestCase(FloatComparisonMode.OrderedNonSignaling, NI_X86Base_CompareOrdered, NI_AVX_CompareOrdered, NI_X86Base_CompareScalarOrdered, NI_AVX512_CompareOrderedMask)]
    [TestCase(FloatComparisonMode.UnorderedNonSignaling, NI_X86Base_CompareUnordered, NI_AVX_CompareUnordered, NI_X86Base_CompareScalarUnordered, NI_AVX512_CompareUnorderedMask)]
    public static void FloatComparisonNormalizationPreservesWidthAndScalarMaskForms(FloatComparisonMode comparison,
        NamedIntrinsic vector128, NamedIntrinsic vector256, NamedIntrinsic scalar, NamedIntrinsic mask)
    {
        FloatComparisonMode[] modes = [comparison, (FloatComparisonMode)((byte)comparison ^ 16)];
        foreach (var mode in modes)
        {
            Assert.That(HWIntrinsicInfo.lookupIdForFloatComparisonMode(NI_AVX_Compare, mode, TYP_FLOAT, 16), Is.EqualTo(vector128));
            Assert.That(HWIntrinsicInfo.lookupIdForFloatComparisonMode(NI_AVX_Compare, mode, TYP_DOUBLE, 32), Is.EqualTo(vector256));
            Assert.That(HWIntrinsicInfo.lookupIdForFloatComparisonMode(NI_AVX_CompareScalar, mode, TYP_DOUBLE, 16), Is.EqualTo(scalar));
            Assert.That(HWIntrinsicInfo.lookupIdForFloatComparisonMode(NI_AVX512_CompareMask, mode, TYP_FLOAT, 64), Is.EqualTo(mask));
            Assert.That(HWIntrinsicInfo.lookupIdForFloatComparisonMode(NI_AVX512_CompareMask, mode, TYP_DOUBLE, 16), Is.EqualTo(mask));
        }
    }

    [TestCase(FloatComparisonMode.UnorderedEqualNonSignaling)]
    [TestCase(FloatComparisonMode.OrderedNotEqualNonSignaling)]
    [TestCase(FloatComparisonMode.OrderedFalseNonSignaling)]
    [TestCase(FloatComparisonMode.UnorderedTrueNonSignaling)]
    public static void OtherComparisonModesKeepOriginalIntrinsic(FloatComparisonMode comparison)
    {
        FloatComparisonMode[] modes = [comparison, (FloatComparisonMode)((byte)comparison ^ 16)];
        NamedIntrinsic[] intrinsics = [NI_AVX_Compare, NI_AVX_CompareScalar, NI_AVX512_CompareMask];
        foreach (var mode in modes)
        {
            foreach (var intrinsic in intrinsics)
            {
                Assert.That(HWIntrinsicInfo.lookupIdForFloatComparisonMode(intrinsic, mode, TYP_FLOAT, 16), Is.EqualTo(intrinsic));
            }
        }
    }

    [TestCase(NI_AVX2_ShiftLeftLogicalVariable, true)]
    [TestCase(NI_AVX2_ShiftRightArithmeticVariable, true)]
    [TestCase(NI_AVX2_ShiftRightLogicalVariable, true)]
    [TestCase(NI_AVX512_ShiftLeftLogicalVariable, true)]
    [TestCase(NI_AVX512_ShiftRightArithmeticVariable, true)]
    [TestCase(NI_AVX512_ShiftRightLogicalVariable, true)]
    [TestCase(NI_X86Base_ShiftLeftLogical, false)]
    [TestCase(NI_AVX2_ShiftLeftLogical, false)]
    [TestCase(NI_AVX512_ShiftRightLogical, false)]
    [TestCase(NI_X86Base_Add, false)]
    public static void VariableShiftRecognitionExcludesUniformShiftCounts(NamedIntrinsic id, bool expected)
    {
        Assert.That(HWIntrinsicInfo.IsVariableShift(id), Is.EqualTo(expected));
    }

    [Test]
    public static void IntrinsicIdChangePreservesUnspecifiedOperandsAndMetadata()
    {
        WithCompiler(compiler => {
            var left = new GenTreeVecCon(TYP_SIMD16);
            var right = new GenTreeVecCon(TYP_SIMD16);
            var replacement = new GenTreeVecCon(TYP_SIMD16);
            var node = compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_X86Base_Add, TYP_INT, 16, left, right);
            node.AuxiliaryType = TYP_LONG;
            node.Flags |= GTF_ORDER_SIDEEFF;
            var flags = node.Flags;
            node.ChangeHWIntrinsicId(NI_X86Base_Xor, replacement);
            Assert.That(node.HWIntrinsicId, Is.EqualTo(NI_X86Base_Xor));
            Assert.That(node.GetOp(1), Is.SameAs(replacement));
            Assert.That(node.GetOp(2), Is.SameAs(right));
            node.ChangeHWIntrinsicId(NI_X86Base_Or);
            Assert.That(node.HWIntrinsicId, Is.EqualTo(NI_X86Base_Or));
            Assert.That(node.GetOp(1), Is.SameAs(replacement));
            Assert.That(node.GetOp(2), Is.SameAs(right));
            Assert.That(node.Flags, Is.EqualTo(flags));
            Assert.That(node.Type, Is.EqualTo(TYP_SIMD16));
            Assert.That(node.SimdBaseType, Is.EqualTo(TYP_INT));
            Assert.That(node.SimdSize, Is.EqualTo(16));
            Assert.That(node.AuxiliaryType, Is.EqualTo(TYP_LONG));
        });
    }

    [TestCase(1, TYP_SIMD16)]
    [TestCase(4, TYP_SIMD16)]
    [TestCase(8, TYP_SIMD32)]
    [TestCase(16, TYP_SIMD64)]
    public static void IntrinsicResetReplacesAllOperandsAndPreservesFlags(int count, var_types type)
    {
        WithCompiler(compiler => {
            var initial = compiler.gtNewIconNode(TYP_INT, 0);
            var node = compiler.gtNewSimdHWIntrinsicNode(type, NI_Vector_Create, TYP_INT, type.Size, initial);
            node.Flags |= GTF_ORDER_SIDEEFF;
            node.AuxiliaryType = TYP_FLOAT;
            var flags = node.Flags;
            var operands = new GenTree[count];
            for (var index = 0; index < count; index++)
            {
                operands[index] = compiler.gtNewIconNode(TYP_INT, index + 1);
            }

            node.ResetHWIntrinsicId(NI_Vector_Create, operands);
            Assert.That(node.Operands.Length, Is.EqualTo(count));
            for (var index = 0; index < count; index++)
            {
                Assert.That(node.GetOp(index + 1), Is.SameAs(operands[index]));
            }

            node.ResetHWIntrinsicId(NI_Vector_CreateScalarUnsafe, initial);
            Assert.That(node.HWIntrinsicId, Is.EqualTo(NI_Vector_CreateScalarUnsafe));
            Assert.That(node.Operands.Length, Is.EqualTo(1));
            Assert.That(node.GetOp(1), Is.SameAs(initial));
            Assert.That(node.Flags, Is.EqualTo(flags));
            Assert.That(node.Type, Is.EqualTo(type));
            Assert.That(node.SimdBaseType, Is.EqualTo(TYP_INT));
            Assert.That(node.SimdSize, Is.EqualTo(type.Size));
            Assert.That(node.AuxiliaryType, Is.EqualTo(TYP_FLOAT));
        });
    }

    [Test]
    public static void IntrinsicResetDoesNotReinitializeIntrinsicSideEffects()
    {
        WithCompiler(compiler => {
            var node = compiler.gtNewScalarHWIntrinsicNode(TYP_VOID, NI_X86Base_LoadFence);
            var flags = node.Flags;
            node.ResetHWIntrinsicId(NI_X86Base_Pause);
            Assert.That(node.HWIntrinsicId, Is.EqualTo(NI_X86Base_Pause));
            Assert.That(node.Operands.Length, Is.Zero);
            Assert.That(node.Flags, Is.EqualTo(flags));
            Assert.That(node.Flags & GTF_CALL, Is.EqualTo(GTF_EMPTY));
        });
    }

    [TestCase(TYP_BYTE)]
    [TestCase(TYP_UBYTE)]
    [TestCase(TYP_SHORT)]
    [TestCase(TYP_USHORT)]
    [TestCase(TYP_INT)]
    [TestCase(TYP_UINT)]
    [TestCase(TYP_LONG)]
    [TestCase(TYP_ULONG)]
    [TestCase(TYP_FLOAT)]
    [TestCase(TYP_DOUBLE)]
    public static void ConstantPerElementMaskRecognitionUsesRawLaneBits(var_types baseType)
    {
        WithCompiler(compiler => {
            var value = new GenTreeVecCon(TYP_SIMD16) {
                SimdVal = simd64_t.AllBitsSet,
            };
            value.SimdVal.AsSpan<byte>()[16..].Fill(0xA5);
            value.SimdVal.AsSpan<byte>()[..baseType.Size].Clear();
            Assert.That(value.IsVectorPerElementMask(compiler, baseType, 16), Is.True);
            Assert.That(value.IsVectorPerElementMask(compiler, baseType, 8), Is.False);
            value.SimdVal.u8[baseType.Size - 1] = 0x80;
            Assert.That(value.IsVectorPerElementMask(compiler, baseType, 16), Is.False);
            Assert.That(compiler.gtNewIconNode(TYP_INT, 0).IsVectorPerElementMask(compiler, baseType, 16), Is.False);
        });
    }

    [TestCase(TYP_USHORT, TYP_BYTE, true)]
    [TestCase(TYP_BYTE, TYP_USHORT, false)]
    [TestCase(TYP_FLOAT, TYP_INT, true)]
    [TestCase(TYP_INT, TYP_DOUBLE, false)]
    [TestCase(TYP_DOUBLE, TYP_UBYTE, true)]
    public static void LocalPerElementMaskRecognitionUsesAnnotatedWidth(var_types storedType, var_types queriedType, bool expected)
    {
        WithCompiler(compiler => {
            compiler.lvaTable = [new LclVarDsc { Type = TYP_SIMD16 }];
            compiler.lvaCount = 1;
            var local = new GenTreeLclVar(TYP_SIMD16, 0);
            Assert.That(local.IsVectorPerElementMask(compiler, queriedType, 16), Is.False);
            compiler.lvaGetDesc(0).SetIsVectorPerElementMask(storedType);
            Assert.That(local.IsVectorPerElementMask(compiler, queriedType, 16), Is.EqualTo(expected));
        });
    }

    [TestCase(NI_X86Base_And, true)]
    [TestCase(NI_X86Base_AndNot, true)]
    [TestCase(NI_X86Base_Or, true)]
    [TestCase(NI_X86Base_Xor, true)]
    [TestCase(NI_X86Base_Add, false)]
    public static void IntrinsicPerElementMaskRecognitionRequiresCompatibleOperands(NamedIntrinsic id, bool expected)
    {
        WithCompiler(compiler => {
            var zero = new GenTreeVecCon(TYP_SIMD16);
            var comparison = compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_X86Base_CompareEqual, TYP_USHORT, 16, zero, zero);
            Assert.That(comparison.IsVectorPerElementMask(compiler, TYP_BYTE, 16), Is.True);
            Assert.That(comparison.IsVectorPerElementMask(compiler, TYP_INT, 16), Is.False);
            var expression = compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16, id, TYP_USHORT, 16, comparison, zero);
            Assert.That(expression.IsVectorPerElementMask(compiler, TYP_USHORT, 16), Is.EqualTo(expected));
            zero.SimdVal.u8[0] = 1;
            Assert.That(expression.IsVectorPerElementMask(compiler, TYP_USHORT, 16), Is.False);
            comparison.SimdSize = 32;
            Assert.That(comparison.IsVectorPerElementMask(compiler, TYP_USHORT, 16), Is.False);
        });
    }

    [TestCase(TYP_SIMD8, TYP_UBYTE)]
    [TestCase(TYP_SIMD8, TYP_DOUBLE)]
    [TestCase(TYP_SIMD12, TYP_FLOAT)]
    [TestCase(TYP_SIMD16, TYP_INT)]
    [TestCase(TYP_SIMD16, TYP_USHORT)]
    [TestCase(TYP_SIMD32, TYP_LONG)]
    [TestCase(TYP_SIMD64, TYP_BYTE)]
    [TestCase(TYP_SIMD64, TYP_ULONG)]
    public static void ConstantVectorToMaskFoldingPreservesInputsAndDefersFinalization(var_types type, var_types baseType)
    {
        WithCompiler(compiler => {
            var value = new GenTreeVecCon(type) {
                SimdVal = simd64_t.AllBitsSet,
            };
            value.SimdVal.AsSpan<byte>()[..type.Size].Clear();
            value.SimdVal.u8[baseType.Size - 1] = 0x80;
            value.SimdVal.u8[type.Size - 1] = 0x80;
            var before = value.SimdVal;
            var conversion = compiler.gtNewSimdCvtVectorToMaskNode(TYP_MASK, value, baseType, type.Size).AsHWIntrinsic();
            Assert.That(compiler.compMaskConvertUsed, Is.True);
            Assert.That(conversion.IsConvertVectorToMask, Is.True);
            Assert.That(conversion.IsConvertMaskToVector, Is.False);
            Assert.That(conversion.Type, Is.EqualTo(TYP_MASK));
            Assert.That(conversion.SimdBaseType, Is.EqualTo(baseType));
            Assert.That(conversion.SimdSize, Is.EqualTo(type.Size));

            var result = compiler.gtFoldExprConvertVecCnsToMask(conversion, value).AsMskCon();
            Assert.That(result.SimdMaskVal.u64[0], Is.EqualTo(1UL | (1UL << ((type.Size / baseType.Size) - 1))));
            Assert.That(result.Type, Is.EqualTo(TYP_MASK));
            Assert.That(conversion.GetOp(1), Is.SameAs(value));
            Assert.That(value.SimdVal, Is.EqualTo(before));
#if DEBUG
            Assert.That(result.WasMorphed, Is.False);
#endif
            var secondResult = compiler.gtFoldExprConvertVecCnsToMask(conversion, value);
            Assert.That(secondResult, Is.Not.SameAs(result));
            var expanded = compiler.gtNewSimdCvtMaskToVectorNode(type, result, baseType, type.Size);
            Assert.That(expanded.IsConvertMaskToVector, Is.True);
            Assert.That(expanded.IsConvertVectorToMask, Is.False);
            Assert.That(value.IsConvertMaskToVector, Is.False);
            Assert.That(value.IsConvertVectorToMask, Is.False);
        });
    }

    [TestCase(1, 1UL)]
    [TestCase(8, 0xFFUL)]
    [TestCase(16, 0xFFFFUL)]
    [TestCase(32, 0xFFFFFFFFUL)]
    [TestCase(64, ulong.MaxValue)]
    public static void MaskAllBitsSetHonorsElementCount(int count, ulong expected)
    {
        var value = simdmask_t.AllBitsSet(count);
        Assert.That(value.u64[0], Is.EqualTo(expected));
        Assert.That(value.IsAllBitsSet, Is.EqualTo(count == 64));
    }

    [TestCase(TYP_DOUBLE, 16, 0UL, ulong.MaxValue)]
    [TestCase(TYP_INT, 16, 0x100UL, ulong.MaxValue)]
    [TestCase(TYP_FLOAT, 16, 0xFEUL, 1UL)]
    [TestCase(TYP_BYTE, 16, 0xFFFEUL, 1UL)]
    [TestCase(TYP_SHORT, 64, 0xFFFFFFFEUL, 1UL)]
    [TestCase(TYP_UBYTE, 64, ulong.MaxValue, 0UL)]
    public static void UnaryMaskEvaluationUsesNativeWidthsAndNormalization(var_types type, int size, ulong value, ulong expected)
    {
        WithCompiler(_ => {
            simdmask_t mask = default;
            mask.u64[0] = value;
            var node = new GenTreeMskCon(mask);
            node.EvaluateUnaryInPlace(GT_NOT, scalar: false, type, size);
            Assert.That(node.SimdMaskVal.u64[0], Is.EqualTo(expected));
            node.SimdMaskVal = mask;
            node.EvaluateUnaryInPlace(GT_NOT, scalar: true, type, size);
            Assert.That(node.SimdMaskVal.u64[0], Is.EqualTo(expected));
        });
    }

    [TestCase(GT_AND, 0xFFUL, 0x1FFUL, ulong.MaxValue)]
    [TestCase(GT_AND, 0x100UL, 0x100UL, 0UL)]
    [TestCase(GT_AND_NOT, 0xFUL, 0xAUL, 0x5UL)]
    [TestCase(GT_OR, 0xAAUL, 0x55UL, ulong.MaxValue)]
    [TestCase(GT_XOR, 0xAAUL, 0x55UL, ulong.MaxValue)]
    [TestCase(GT_XOR, ulong.MaxValue, 0xFEUL, 1UL)]
    public static void BinaryMaskEvaluationIgnoresScalarAndInactiveBits(genTreeOps oper, ulong left, ulong right, ulong expected)
    {
        WithCompiler(_ => {
            simdmask_t mask = default;
            mask.u64[0] = left;
            var node = new GenTreeMskCon(mask);
            mask.u64[0] = right;
            var other = new GenTreeMskCon(mask);
            node.EvaluateBinaryInPlace(oper, scalar: false, TYP_FLOAT, 16, other);
            Assert.That(node.SimdMaskVal.u64[0], Is.EqualTo(expected));
            node.SimdMaskVal.u64[0] = left;
            node.EvaluateBinaryInPlace(oper, scalar: true, TYP_FLOAT, 16, other);
            Assert.That(node.SimdMaskVal.u64[0], Is.EqualTo(expected));
            Assert.That(other.SimdMaskVal.u64[0], Is.EqualTo(right));
            node.EvaluateBinaryInPlace(GT_XOR, scalar: false, TYP_FLOAT, 16, node);
            Assert.That(node.IsZero, Is.True);
        });
    }

    [TestCase(TYP_BYTE, 8)]
    [TestCase(TYP_UBYTE, 64)]
    [TestCase(TYP_SHORT, 16)]
    [TestCase(TYP_USHORT, 32)]
    [TestCase(TYP_INT, 16)]
    [TestCase(TYP_UINT, 64)]
    [TestCase(TYP_FLOAT, 12)]
    [TestCase(TYP_LONG, 16)]
    [TestCase(TYP_ULONG, 32)]
    [TestCase(TYP_DOUBLE, 64)]
    public static void MaskVectorConversionsPreserveLaneOrderAndInactiveBytes(var_types type, int size)
    {
        simdmask_t mask = default;
        mask.u64[0] = 0xAAAAAAAAAAAAAAAA;
        var value = simd64_t.AllBitsSet;
        Globals.EvaluateSimdCvtMaskToVector(type, value.AsSpan<byte>()[..size], mask);
        for (var index = 0; index < size; index++)
        {
            Assert.That(value.u8[index], Is.EqualTo(((index / type.Size) & 1) != 0 ? byte.MaxValue : 0));
        }

        Assert.That(value.AsSpan<byte>()[size..].IndexOfAnyExcept(byte.MaxValue), Is.EqualTo(-1));
        simdmask_t result = default;
        Globals.EvaluateSimdCvtVectorToMask(type, ref result, value.AsSpan<byte>()[..size]);
        Assert.That(result.u64[0], Is.EqualTo(mask.u64[0] & (ulong)simdmask_t.GetBitMask(size / type.Size)));
        Globals.EvaluateExtractMSB(type, ref result, value.AsSpan<byte>()[..size]);
        Assert.That(result.u64[0], Is.EqualTo(mask.u64[0] & (ulong)simdmask_t.GetBitMask(size / type.Size)));
        value = simd64_t.AllBitsSet;
        Globals.EvaluateSimdCvtVectorToMask(type, ref result, value.AsSpan<byte>()[..size]);
        Assert.That(result.i64[0], Is.EqualTo(simdmask_t.GetBitMask(size / type.Size)));
    }

    [Test]
    public static void VectorToMaskUsesSignBitsRatherThanNonzeroFloatingLanes()
    {
        simd64_t value = default;
        value.u32[0] = 0x7F800123;
        value.u32[1] = 0xFF800123;
        value.u32[2] = 0x80000000;
        value.u32[3] = 1;
        simdmask_t result = default;
        Globals.EvaluateSimdCvtVectorToMask(TYP_FLOAT, ref result, value.AsSpan<byte>()[..16]);
        Assert.That(result.u64[0], Is.EqualTo(6UL));
        Assert.That(value.u32[0], Is.EqualTo(0x7F800123u));
        Assert.That(value.u32[1], Is.EqualTo(0xFF800123u));
    }

    [TestCase(TYP_INT, GT_ADD, int.MaxValue, 1L, int.MinValue)]
    [TestCase(TYP_UBYTE, GT_ADD, 255L, 1L, 0L)]
    [TestCase(TYP_BYTE, GT_MUL, 127L, 2L, -2L)]
    [TestCase(TYP_BYTE, GT_DIV, -128L, -1L, -128L)]
    [TestCase(TYP_USHORT, GT_SUB, 0L, 1L, 65535L)]
    [TestCase(TYP_LONG, GT_SUB, long.MinValue, 1L, long.MaxValue)]
    [TestCase(TYP_INT, GT_AND_NOT, 12L, 10L, 4L)]
    [TestCase(TYP_INT, GT_OR_NOT, 12L, 10L, -3L)]
    [TestCase(TYP_INT, GT_XOR_NOT, 12L, 10L, -7L)]
    [TestCase(TYP_INT, GT_LT, -1L, 0L, -1L)]
    [TestCase(TYP_UINT, GT_LT, -1L, 0L, 0L)]
    [TestCase(TYP_UBYTE, GT_EQ, 1L, 1L, 255L)]
    [TestCase(TYP_LONG, GT_GT, -1L, 0L, 0L)]
    [TestCase(TYP_ULONG, GT_GT, -1L, 0L, -1L)]
    [TestCase(TYP_INT, GT_LSH, 1L, 32L, 0L)]
    [TestCase(TYP_UINT, GT_LSH, 1L, -1L, 0L)]
    [TestCase(TYP_LONG, GT_LSH, 1L, 0x100000000L, 0L)]
    [TestCase(TYP_BYTE, GT_RSZ, -128L, 1L, 64L)]
    [TestCase(TYP_SHORT, GT_RSZ, -32768L, 1L, 16384L)]
    [TestCase(TYP_INT, GT_RSZ, -1L, 32L, 0L)]
    [TestCase(TYP_LONG, GT_RSZ, -1L, -1L, 0L)]
    [TestCase(TYP_INT, GT_RSH, -1L, 33L, -1L)]
    [TestCase(TYP_UINT, GT_RSH, -1L, 32L, 0L)]
    [TestCase(TYP_BYTE, GT_RSH, -128L, -1L, -1L)]
    [TestCase(TYP_UBYTE, GT_ROL, 0x81L, -1L, 0xC0L)]
    [TestCase(TYP_SHORT, GT_ROL, 1L, 16L, 1L)]
    [TestCase(TYP_ULONG, GT_ROR, 1L, 1L, long.MinValue)]
    public static void BinaryIntegerEvaluationPreservesLaneAndShiftSemantics(var_types type, genTreeOps oper,
        long left, long right, long expected)
    {
        WithCompiler(_ => {
            var node = new GenTreeVecCon(TYP_SIMD16);
            var other = new GenTreeVecCon(TYP_SIMD16);
            node.EvaluateBroadcastInPlace(type, left);
            other.EvaluateBroadcastInPlace(type, right);
            var originalOther = other.SimdVal;
            node.EvaluateBinaryInPlace(oper, scalar: false, type, other);
            for (var index = 0; index < 16 / type.Size; index++)
            {
                Assert.That(node.GetElementIntegral(type, index), Is.EqualTo(expected));
            }

            Assert.That(other.SimdVal, Is.EqualTo(originalOther));
        });
    }

    [TestCase(TYP_SIMD8, false)]
    [TestCase(TYP_SIMD12, false)]
    [TestCase(TYP_SIMD16, false)]
    [TestCase(TYP_SIMD32, false)]
    [TestCase(TYP_SIMD64, false)]
    [TestCase(TYP_SIMD16, true)]
    public static void BinaryEvaluationPreservesScalarLanesPaddingAndAliasedInputs(var_types type, bool scalar)
    {
        WithCompiler(_ => {
            var node = new GenTreeVecCon(type) {
                SimdVal = simd64_t.AllBitsSet,
            };
            for (var index = 0; index < type.Size / sizeof(int); index++)
            {
                node.SimdVal.i32[index] = index + 1;
            }

            var before = node.SimdVal;
            node.EvaluateBinaryInPlace(GT_ADD, scalar, TYP_INT, node);
            for (var index = 0; index < type.Size / sizeof(int); index++)
            {
                var expected = scalar && (index != 0) ? before.i32[index] : before.i32[index] * 2;
                Assert.That(node.SimdVal.i32[index], Is.EqualTo(expected));
            }

            Assert.That(node.SimdVal.AsSpan<byte>()[type.Size..].SequenceEqual(before.AsSpan<byte>()[type.Size..]), Is.True);
        });
    }

    [TestCase(GT_EQ, double.NaN, 1.0, false)]
    [TestCase(GT_NE, double.NaN, 1.0, true)]
    [TestCase(GT_LT, double.NaN, 1.0, false)]
    [TestCase(GT_GT, 2.0, 1.0, true)]
    [TestCase(GT_GE, -0.0, 0.0, true)]
    [TestCase(GT_LE, 0.0, -0.0, true)]
    public static void BinaryFloatingComparisonsProduceExactLaneMasks(genTreeOps oper, double left, double right, bool expected)
    {
        WithCompiler(_ => {
            var node = new GenTreeVecCon(TYP_SIMD16);
            var other = new GenTreeVecCon(TYP_SIMD16);
            node.EvaluateBroadcastInPlace(TYP_FLOAT, left);
            other.EvaluateBroadcastInPlace(TYP_FLOAT, right);
            node.EvaluateBinaryInPlace(oper, scalar: false, TYP_FLOAT, other);
            Assert.That(node.SimdVal.u32[0], Is.EqualTo(expected ? uint.MaxValue : 0));
            Assert.That(node.SimdVal.u32[3], Is.EqualTo(expected ? uint.MaxValue : 0));
            node.EvaluateBroadcastInPlace(TYP_DOUBLE, left);
            other.EvaluateBroadcastInPlace(TYP_DOUBLE, right);
            node.EvaluateBinaryInPlace(oper, scalar: false, TYP_DOUBLE, other);
            Assert.That(node.SimdVal.u64[0], Is.EqualTo(expected ? ulong.MaxValue : 0));
            Assert.That(node.SimdVal.u64[1], Is.EqualTo(expected ? ulong.MaxValue : 0));
        });
    }

    [TestCase(GT_ADD, -0.0, -0.0, -0.0)]
    [TestCase(GT_MUL, -0.0, 1.0, -0.0)]
    [TestCase(GT_SUB, 1.5, 0.5, 1.0)]
    [TestCase(GT_DIV, 1.0, 0.0, double.PositiveInfinity)]
    public static void BinaryFloatingArithmeticPreservesBits(genTreeOps oper, double left, double right, double expected)
    {
        WithCompiler(_ => {
            var node = new GenTreeVecCon(TYP_SIMD16);
            var other = new GenTreeVecCon(TYP_SIMD16);
            node.EvaluateBroadcastInPlace(TYP_DOUBLE, left);
            other.EvaluateBroadcastInPlace(TYP_DOUBLE, right);
            node.EvaluateBinaryInPlace(oper, scalar: false, TYP_DOUBLE, other);
            Assert.That(node.SimdVal.i64[0], Is.EqualTo(BitConverter.DoubleToInt64Bits(expected)));
            node.EvaluateBroadcastInPlace(TYP_FLOAT, left);
            other.EvaluateBroadcastInPlace(TYP_FLOAT, right);
            node.EvaluateBinaryInPlace(oper, scalar: false, TYP_FLOAT, other);
            Assert.That(node.SimdVal.i32[0], Is.EqualTo(BitConverter.SingleToInt32Bits((float)expected)));
        });
    }

    [TestCase(TYP_FLOAT)]
    [TestCase(TYP_DOUBLE)]
    public static void BinaryFloatingBitwiseOperationsPreserveSignalingNaNBits(var_types type)
    {
        WithCompiler(_ => {
            var node = new GenTreeVecCon(TYP_SIMD16);
            var other = new GenTreeVecCon(TYP_SIMD16);
            if (type == TYP_FLOAT)
            {
                node.SimdVal.u32[0] = 0x7F800123;
                other.SimdVal.u32[0] = 0x80000000;
            }
            else
            {
                node.SimdVal.u64[0] = 0x7FF0000000000123;
                other.SimdVal.u64[0] = 0x8000000000000000;
            }

            Assert.That(Globals.IsBinaryBitwiseOperation(GT_XOR), Is.True);
            node.EvaluateBinaryInPlace(GT_XOR, scalar: false, type, other);
            if (type == TYP_FLOAT)
            {
                Assert.That(node.SimdVal.u32[0], Is.EqualTo(0xFF800123u));
            }
            else
            {
                Assert.That(node.SimdVal.u64[0], Is.EqualTo(0xFFF0000000000123ul));
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ExplicitBinaryEvaluationWidthDoesNotOverwriteOtherLanes(bool scalar)
    {
        simd64_t left = default;
        simd64_t right = default;
        var result = simd64_t.AllBitsSet;
        left.i32[0] = 1;
        left.i32[1] = 2;
        left.i32[2] = 3;
        right.i32[0] = 4;
        right.i32[1] = 5;
        Globals.EvaluateBinarySimd(GT_ADD, scalar, TYP_INT, result.AsSpan<byte>()[..16],
            left.AsSpan<byte>()[..16], right.AsSpan<byte>()[..16], 8);
        Assert.That(result.i32[0], Is.EqualTo(5));
        Assert.That(result.i32[1], Is.EqualTo(scalar ? 2 : 7));
        Assert.That(result.i32[2], Is.EqualTo(scalar ? 3 : -1));
        Assert.That(result.i32[4], Is.EqualTo(-1));
    }

    [TestCase(TYP_SIMD8, false)]
    [TestCase(TYP_SIMD12, false)]
    [TestCase(TYP_SIMD16, false)]
    [TestCase(TYP_SIMD32, false)]
    [TestCase(TYP_SIMD64, false)]
    [TestCase(TYP_SIMD16, true)]
    public static void UnaryEvaluationPreservesUpperLanesAndInactiveStorage(var_types type, bool scalar)
    {
        WithCompiler(_ => {
            var node = new GenTreeVecCon(type) {
                SimdVal = simd64_t.AllBitsSet,
            };
            for (var index = 0; index < type.Size / sizeof(int); index++)
            {
                node.SimdVal.i32[index] = int.MinValue + index;
            }

            var before = node.SimdVal;
            Assert.That(node.TryEvaluateUnaryInPlace(GT_NEG, scalar, TYP_INT), Is.True);
            for (var index = 0; index < type.Size / sizeof(int); index++)
            {
                var expected = scalar && (index != 0) ? before.i32[index] : unchecked(-before.i32[index]);
                Assert.That(node.SimdVal.i32[index], Is.EqualTo(expected));
            }

            Assert.That(node.SimdVal.AsSpan<byte>()[type.Size..].SequenceEqual(before.AsSpan<byte>()[type.Size..]), Is.True);
        });
    }

    [TestCase(TYP_BYTE, -128L, -128L)]
    [TestCase(TYP_UBYTE, 1L, 255L)]
    [TestCase(TYP_SHORT, -32768L, -32768L)]
    [TestCase(TYP_USHORT, 1L, 65535L)]
    [TestCase(TYP_INT, -2147483648L, -2147483648L)]
    [TestCase(TYP_UINT, 1L, 4294967295L)]
    [TestCase(TYP_LONG, long.MinValue, long.MinValue)]
    [TestCase(TYP_ULONG, 1L, -1L)]
    public static void UnaryIntegerNegationWrapsAtLaneWidth(var_types type, long input, long expected)
    {
        WithCompiler(_ => {
            var node = new GenTreeVecCon(TYP_SIMD16);
            node.EvaluateBroadcastInPlace(type, input);
            Assert.That(node.TryEvaluateUnaryInPlace(GT_NEG, scalar: false, type), Is.True);
            Assert.That(node.GetElementIntegral(type, 0), Is.EqualTo(expected));
            Assert.That(node.GetElementIntegral(type, (16 / type.Size) - 1), Is.EqualTo(expected));
        });
    }

    [TestCase(TYP_INT, 0L, 32L)]
    [TestCase(TYP_UINT, 0x40000000L, 1L)]
    [TestCase(TYP_INT, -1L, 0L)]
    [TestCase(TYP_LONG, 0L, 64L)]
    [TestCase(TYP_ULONG, 1L, 63L)]
    [TestCase(TYP_LONG, long.MinValue, 0L)]
    public static void UnaryLeadingZeroCountUsesTheLaneWidth(var_types type, long input, long expected)
    {
        WithCompiler(_ => {
            var node = new GenTreeVecCon(TYP_SIMD16);
            node.EvaluateBroadcastInPlace(type, input);
            Assert.That(node.TryEvaluateUnaryInPlace(GT_LZCNT, scalar: false, type), Is.True);
            Assert.That(node.GetElementIntegral(type, 0), Is.EqualTo(expected));
        });
    }

    [TestCase(TYP_FLOAT, GT_NOT)]
    [TestCase(TYP_DOUBLE, GT_NOT)]
    [TestCase(TYP_FLOAT, GT_LZCNT)]
    [TestCase(TYP_DOUBLE, GT_LZCNT)]
    public static void UnaryFloatingBitwiseOperationsDoNotQuietSignalingNaNs(var_types type, genTreeOps oper)
    {
        WithCompiler(_ => {
            var node = new GenTreeVecCon(TYP_SIMD16);
            if (type == TYP_FLOAT)
            {
                node.SimdVal.u32[0] = 0x7F800123;
                node.SimdVal.u32[1] = 0x80000000;
            }
            else
            {
                node.SimdVal.u64[0] = 0x7FF0000000000123;
                node.SimdVal.u64[1] = 0x8000000000000000;
            }

            var before = node.SimdVal;
            Assert.That(Globals.IsUnaryBitwiseOperation(oper), Is.True);
            Assert.That(node.TryEvaluateUnaryInPlace(oper, scalar: true, type), Is.True);
            if (type == TYP_FLOAT)
            {
                Assert.That(node.SimdVal.u32[0], Is.EqualTo(oper == GT_NOT ? ~before.u32[0] : 1u));
            }
            else
            {
                Assert.That(node.SimdVal.u64[0], Is.EqualTo(oper == GT_NOT ? ~before.u64[0] : 1ul));
            }

            Assert.That(node.SimdVal.AsSpan<byte>()[type.Size..16].SequenceEqual(before.AsSpan<byte>()[type.Size..16]), Is.True);
        });
    }

    [TestCase(TYP_FLOAT)]
    [TestCase(TYP_DOUBLE)]
    public static void UnaryFloatingNegationPreservesPayloadsAndSignedZero(var_types type)
    {
        WithCompiler(_ => {
            var node = new GenTreeVecCon(TYP_SIMD16);
            if (type == TYP_FLOAT)
            {
                node.SimdVal.u32[0] = 0x7F800123;
                node.SimdVal.u32[1] = 0x80000000;
            }
            else
            {
                node.SimdVal.u64[0] = 0x7FF0000000000123;
                node.SimdVal.u64[1] = 0x8000000000000000;
            }

            Assert.That(Globals.IsUnaryBitwiseOperation(GT_NEG), Is.False);
            Assert.That(node.TryEvaluateUnaryInPlace(GT_NEG, scalar: false, type), Is.True);
            if (type == TYP_FLOAT)
            {
                Assert.That(node.SimdVal.u32[0], Is.EqualTo(0xFF800123u));
                Assert.That(node.SimdVal.u32[1], Is.Zero);
            }
            else
            {
                Assert.That(node.SimdVal.u64[0], Is.EqualTo(0xFFF0000000000123ul));
                Assert.That(node.SimdVal.u64[1], Is.Zero);
            }
        });
    }

    [TestCase(NI_AVX2_And, TYP_INT, GT_AND, false)]
    [TestCase(NI_AVX512_NotMask, TYP_INT, GT_NOT, false)]
    [TestCase(NI_X86Base_Xor, TYP_FLOAT, GT_XOR, false)]
    [TestCase(NI_AVX512_OrMask, TYP_LONG, GT_OR, false)]
    [TestCase(NI_AVX2_AndNotVector, TYP_INT, GT_AND_NOT, false)]
    [TestCase(NI_AVX_Add, TYP_DOUBLE, GT_ADD, false)]
    [TestCase(NI_X86Base_AddScalar, TYP_FLOAT, GT_ADD, true)]
    [TestCase(NI_X86Base_DivideScalar, TYP_DOUBLE, GT_DIV, true)]
    [TestCase(NI_AVX512_MultiplyLow, TYP_INT, GT_MUL, false)]
    [TestCase(NI_X86Base_Multiply, TYP_INT, GT_NONE, false)]
    [TestCase(NI_X86Base_Multiply, TYP_FLOAT, GT_MUL, false)]
    [TestCase(NI_AVX512_MultiplyScalar, TYP_DOUBLE, GT_MUL, true)]
    [TestCase(NI_AVX512_RotateLeftVariable, TYP_INT, GT_ROL, false)]
    [TestCase(NI_AVX512_RotateRight, TYP_LONG, GT_ROR, false)]
    [TestCase(NI_AVX2_ShiftLeftLogicalVariable, TYP_INT, GT_LSH, false)]
    [TestCase(NI_AVX2_ShiftRightArithmeticVariable, TYP_INT, GT_RSH, false)]
    [TestCase(NI_X86Base_ShiftRightLogical, TYP_INT, GT_RSZ, false)]
    [TestCase(NI_X86Base_SubtractScalar, TYP_DOUBLE, GT_SUB, true)]
    [TestCase(NI_X86Base_CompareScalarEqual, TYP_DOUBLE, GT_EQ, true)]
    [TestCase(NI_AVX512_CompareGreaterThanMask, TYP_INT, GT_GT, false)]
    [TestCase(NI_X86Base_CompareScalarGreaterThanOrEqual, TYP_FLOAT, GT_GE, true)]
    [TestCase(NI_AVX2_CompareLessThan, TYP_INT, GT_LT, false)]
    [TestCase(NI_X86Base_CompareScalarLessThanOrEqual, TYP_DOUBLE, GT_LE, true)]
    [TestCase(NI_X86Base_CompareScalarNotEqual, TYP_FLOAT, GT_NE, true)]
    [TestCase(NI_Vector_Create, TYP_INT, GT_NONE, false)]
    public static void IntrinsicOperationsPreserveTypeGatesAndScalarFlags(NamedIntrinsic id, var_types type,
        genTreeOps expected, bool expectedScalar)
    {
        Assert.That(GenTreeHWIntrinsic.GetOperForHWIntrinsicId(id, type, out var scalar), Is.EqualTo(expected));
        Assert.That(scalar, Is.EqualTo(expectedScalar));
    }

    [TestCase(NI_X86Base_Subtract, TYP_INT, false, false, GT_NEG)]
    [TestCase(NI_X86Base_Subtract, TYP_DOUBLE, false, false, GT_SUB)]
    [TestCase(NI_X86Base_Xor, TYP_INT, true, false, GT_NOT)]
    [TestCase(NI_X86Base_Xor, TYP_FLOAT, false, true, GT_NEG)]
    [TestCase(NI_X86Base_Xor, TYP_DOUBLE, false, true, GT_NEG)]
    [TestCase(NI_X86Base_Xor, TYP_DOUBLE, false, false, GT_XOR)]
    public static void EffectiveOperationsRecognizeOnlyNativePatterns(NamedIntrinsic id, var_types type,
        bool allBits, bool negativeZero, genTreeOps expected)
    {
        WithCompiler(_ => {
            var constant = new GenTreeVecCon(TYP_SIMD16);
            if (allBits)
            {
                constant.SimdVal = simd64_t.AllBitsSet;
            }
            else if (negativeZero)
            {
                constant.EvaluateBroadcastInPlace(type, -0.0);
            }

            var other = new GenTreeLclVar(TYP_SIMD16, 0);
            var subtraction = id == NI_X86Base_Subtract;
            var node = new GenTreeHWIntrinsic(TYP_SIMD16, id, type, 16,
                subtraction ? constant : other, subtraction ? other : constant);
            Assert.That(node.GetOperForHWIntrinsicId(out var originalScalar), Is.EqualTo(subtraction ? GT_SUB : GT_XOR));
            Assert.That(originalScalar, Is.False);
            Assert.That(node.GetOperForHWIntrinsicId(out var scalar, getEffectiveOp: true), Is.EqualTo(expected));
            Assert.That(scalar, Is.False);
            Assert.That(node.OperIsBitwiseHWIntrinsic(), Is.EqualTo(!subtraction));
        });
    }

    [TestCase(TYP_FLOAT)]
    [TestCase(TYP_DOUBLE)]
    [TestCase(TYP_INT)]
    [TestCase(TYP_LONG)]
    public static void LaneIdentityQueriesDistinguishNegativeZero(var_types type)
    {
        WithCompiler(_ => {
            var node = new GenTreeVecCon(TYP_SIMD16);
            Assert.That(node.IsScalarZero(type), Is.True);
            Assert.That(node.IsScalarOne(type), Is.False);
            if (type is TYP_FLOAT or TYP_DOUBLE)
            {
                node.SetElementFloating(type, 0, -0.0);
                Assert.That(node.IsScalarZero(type), Is.False);
                node.SetElementFloating(type, 0, 1.0);
                node.SetElementFloating(type, 1, double.NaN);
                Assert.That(node.IsElementOne(type, 1), Is.False);
            }
            else
            {
                node.SetElementIntegral(type, 0, 1);
                node.SetElementIntegral(type, 1, -1);
            }

            Assert.That(node.IsScalarZero(type), Is.False);
            Assert.That(node.IsScalarOne(type), Is.True);
            Assert.That(node.IsElementZero(type, 1), Is.False);
        });
    }

    [TestCase(GT_AND, true)]
    [TestCase(GT_AND_NOT, true)]
    [TestCase(GT_OR, true)]
    [TestCase(GT_OR_NOT, true)]
    [TestCase(GT_XOR, true)]
    [TestCase(GT_XOR_NOT, true)]
    [TestCase(GT_NOT, true)]
    [TestCase(GT_NEG, false)]
    [TestCase(GT_EQ, false)]
    public static void BitwiseClassificationMatchesNativeOperators(genTreeOps oper, bool expected)
    {
        Assert.That(GenTreeHWIntrinsic.OperIsBitwiseHWIntrinsic(oper), Is.EqualTo(expected));
    }

    [TestCase(NI_Vector_Create, 8)]
    [TestCase(NI_Vector_Create, 12)]
    [TestCase(NI_Vector_Create, 16)]
    [TestCase(NI_Vector_Create, 32)]
    [TestCase(NI_Vector_Create, 64)]
    [TestCase(NI_Vector_CreateScalar, 16)]
    [TestCase(NI_Vector_CreateScalarUnsafe, 16)]
    public static void CreationConstantsPreserveScalarAndUpperLanePolicy(NamedIntrinsic id, byte size)
    {
        WithCompiler(compiler => {
            var type = size switch {
                8 => TYP_SIMD8,
                12 => TYP_SIMD12,
                16 => TYP_SIMD16,
                32 => TYP_SIMD32,
                64 => TYP_SIMD64,
                _ => throw new ArgumentOutOfRangeException(nameof(size)),
            };
            var node = new GenTreeHWIntrinsic(type, id, TYP_FLOAT, size, compiler.gtNewDconNode(TYP_FLOAT, -0.0));
            var value = simd64_t.AllBitsSet;
            Assert.That(GenTreeVecCon.IsHWIntrinsicCreateConstant(node, ref value), Is.True);
            for (var index = 0; index < 16; index++)
            {
                var filled = (index < size / sizeof(float)) && ((index == 0) || (id != NI_Vector_CreateScalar));
                Assert.That(value.i32[index], Is.EqualTo(filled ? int.MinValue : 0));
            }
        });
    }

    [TestCase(TYP_BYTE, 0x180L, 0x80UL)]
    [TestCase(TYP_UBYTE, -1L, 0xFFUL)]
    [TestCase(TYP_SHORT, 0x18000L, 0x8000UL)]
    [TestCase(TYP_USHORT, -1L, 0xFFFFUL)]
    [TestCase(TYP_INT, 0x180000000L, 0x80000000UL)]
    [TestCase(TYP_UINT, -1L, 0xFFFFFFFFUL)]
    [TestCase(TYP_LONG, long.MinValue, 0x8000000000000000UL)]
    [TestCase(TYP_ULONG, -1L, ulong.MaxValue)]
    public static void IntegralCreationTruncatesLaneBits(var_types type, long input, ulong expected)
    {
        WithCompiler(compiler => {
            var constant = compiler.gtNewIconNode(type is TYP_LONG or TYP_ULONG ? TYP_LONG : Globals.TYP_I_IMPL, (nint)input);
            var node = new GenTreeHWIntrinsic(TYP_SIMD16, NI_Vector_Create, type, 16, constant);
            simd64_t value = default;
            Assert.That(GenTreeVecCon.IsHWIntrinsicCreateConstant(node, ref value), Is.True);
            for (var index = 0; index < 16; index++)
            {
                var shift = index % type.Size * 8;
                Assert.That(value.u8[index], Is.EqualTo((byte)(expected >> shift)));
            }
        });
    }

    [Test]
    public static void CreationConstantsRetainLaneOrderAndNaNPayloads()
    {
        WithCompiler(compiler => {
            const long nanBits = unchecked((long)0xFFF8000000000123);
            var node = new GenTreeHWIntrinsic(TYP_SIMD16, NI_Vector_Create, TYP_DOUBLE, 16,
                compiler.gtNewDconNode(TYP_DOUBLE, BitConverter.Int64BitsToDouble(nanBits)),
                compiler.gtNewDconNode(TYP_DOUBLE, -0.0));
            simd64_t value = default;
            Assert.That(GenTreeVecCon.IsHWIntrinsicCreateConstant(node, ref value), Is.True);
            Assert.That(value.i64[0], Is.EqualTo(nanBits));
            Assert.That(value.i64[1], Is.EqualTo(long.MinValue));
        });
    }

    [Test]
    public static void CreationConstantsRoundSinglesAndLeaveUnknownLanesZero()
    {
        WithCompiler(compiler => {
            var node = new GenTreeHWIntrinsic(TYP_SIMD16, NI_Vector_Create, TYP_FLOAT, 16,
                compiler.gtNewDconNode(TYP_FLOAT, 16777217.0), compiler.gtNewLclvNode(TYP_FLOAT, 0),
                compiler.gtNewDconNode(TYP_FLOAT, -0.0), compiler.gtNewDconNode(TYP_FLOAT, 42.0));
            var value = simd64_t.AllBitsSet;
            Assert.That(GenTreeVecCon.IsHWIntrinsicCreateConstant(node, ref value), Is.False);
            Assert.That(value.f32[0], Is.EqualTo(16777216.0f));
            Assert.That(value.i32[1], Is.Zero);
            Assert.That(value.i32[2], Is.EqualTo(int.MinValue));
            Assert.That(value.f32[3], Is.EqualTo(42.0f));
            Assert.That(value.i64[2], Is.Zero);
        });
    }

    [Test]
    public static void NonCreationDoesNotOverwriteTheOutput()
    {
        WithCompiler(_ => {
            var node = new GenTreeHWIntrinsic(TYP_SIMD16, NI_X86Base_Add, TYP_INT, 16,
                new GenTreeLclVar(TYP_SIMD16, 0), new GenTreeLclVar(TYP_SIMD16, 1));
            var value = simd64_t.AllBitsSet;
            Assert.That(GenTreeVecCon.IsHWIntrinsicCreateConstant(node, ref value), Is.False);
            Assert.That(value.IsAllBitsSet, Is.True);
        });
    }

    [TestCase(NI_X86Base_Add, 2, GTF_EMPTY, false)]
    [TestCase(NI_X86Base_LoadAlignedVector128, 1, GTF_GLOB_REF | GTF_EXCEPT, false)]
    [TestCase(NI_X86Base_StoreAligned, 2, GTF_ASG | GTF_GLOB_REF | GTF_EXCEPT, true)]
    [TestCase(NI_X86Base_LoadFence, 0, GTF_ASG | GTF_GLOB_REF, true)]
    [TestCase(NI_X86Base_MemoryFence, 0, GTF_ASG | GTF_GLOB_REF, true)]
    [TestCase(NI_X86Base_StoreFence, 0, GTF_ASG | GTF_GLOB_REF, true)]
    [TestCase(NI_X86Serialize_Serialize, 0, GTF_ASG | GTF_GLOB_REF, true)]
    [TestCase(NI_X86Base_Pause, 0, GTF_CALL | GTF_GLOB_REF, false)]
    [TestCase(NI_X86Base_Prefetch0, 1, GTF_CALL | GTF_GLOB_REF, false)]
    [TestCase(NI_X86Base_Prefetch1, 1, GTF_CALL | GTF_GLOB_REF, false)]
    [TestCase(NI_X86Base_Prefetch2, 1, GTF_CALL | GTF_GLOB_REF, false)]
    [TestCase(NI_X86Base_PrefetchNonTemporal, 1, GTF_CALL | GTF_GLOB_REF, false)]
    [TestCase(NI_Vector_op_Division, 2, GTF_EXCEPT, false)]
    public static void ConstructorSetsNativeIdentityAndEffects(NamedIntrinsic id, int argCount, GenTreeFlags effects, bool storeOrBarrier)
    {
        WithCompiler(_ => {
            var operands = new GenTree[argCount];

            for (var index = 0; index < operands.Length; index++)
            {
                var address = (id is NI_X86Base_LoadAlignedVector128 or NI_X86Base_StoreAligned or
                    NI_X86Base_Prefetch0 or NI_X86Base_Prefetch1 or NI_X86Base_Prefetch2 or NI_X86Base_PrefetchNonTemporal) && index == 0;
                operands[index] = new GenTreeLclVar(address ? TYP_BYREF : TYP_SIMD16, index);
            }

            var vectorResult = id is NI_X86Base_Add or NI_X86Base_LoadAlignedVector128 or NI_Vector_op_Division;
            var vectorSize = (vectorResult || id is NI_X86Base_StoreAligned) ? (byte)16 : (byte)0;
            var intrinsic = new GenTreeHWIntrinsic(vectorResult ? TYP_SIMD16 : TYP_VOID, id, TYP_INT, vectorSize, operands);
            Assert.That(intrinsic.HWIntrinsicId, Is.EqualTo(id));
            Assert.That(intrinsic.Flags & GTF_ALL_EFFECT, Is.EqualTo(effects));
            Assert.That(intrinsic.IsMemoryStoreOrBarrier, Is.EqualTo(storeOrBarrier));
            Assert.That(intrinsic.RequiresCallFlag(), Is.EqualTo((effects & GTF_CALL) != 0));
        });
    }

    [TestCase(TYP_BYTE, TYP_INT)]
    [TestCase(TYP_UBYTE, TYP_UINT)]
    [TestCase(TYP_SHORT, TYP_INT)]
    [TestCase(TYP_USHORT, TYP_UINT)]
    [TestCase(TYP_INT, TYP_INT)]
    [TestCase(TYP_FLOAT, TYP_FLOAT)]
    public static void LoadConstructionNormalizesOnlySmallBaseTypes(var_types input, var_types expected)
    {
        WithCompiler(_ => {
            var intrinsic = new GenTreeHWIntrinsic(TYP_SIMD16, NI_X86Base_LoadAlignedVector128, input, 16,
                new GenTreeLclVar(TYP_BYREF, 0));
            Assert.That(intrinsic.SimdBaseType, Is.EqualTo(expected));
        });
    }

    [Test]
    public static void VariableArityAndInheritedEffectsArePreserved()
    {
        WithCompiler(compiler => {
            var call = compiler.gtNewCallNode(TYP_INT, gtCallTypes.CT_USER_FUNC, null);
            var intrinsic = new GenTreeHWIntrinsic(TYP_SIMD16, NI_Vector_Create, TYP_INT, 16, call);
            Assert.That(HWIntrinsicInfo.lookupNumArgs(NI_Vector_Create), Is.EqualTo(-1));
            Assert.That(HWIntrinsicInfo.lookupNumArgs(NI_X86Base_Add), Is.EqualTo(2));
            Assert.That(intrinsic.Flags & GTF_ALL_EFFECT, Is.EqualTo(call.Flags & GTF_ALL_EFFECT));
            Assert.That(intrinsic.RequiresCallFlag(), Is.False);
            Assert.That(intrinsic.HWIntrinsicId, Is.EqualTo(NI_Vector_Create));
            Assert.That(HWIntrinsicInfo.IsInvalidNodeId(NI_Vector_op_Addition), Is.True);
            Assert.That(HWIntrinsicInfo.IsInvalidNodeId(NI_X86Base_Add), Is.False);
        });
    }

    [Test]
    public static void ChangingIdentityNormalizesTypesWithoutReinitializingEffects()
    {
        WithCompiler(_ => {
            var intrinsic = new GenTreeHWIntrinsic(TYP_SIMD16, NI_Vector_CreateScalarUnsafe, TYP_UBYTE, 16,
                new GenTreeLclVar(TYP_INT, 0));
            var effects = intrinsic.Flags & GTF_ALL_EFFECT;
            intrinsic.SetOp(1, new GenTreeLclVar(TYP_BYREF, 1));
            intrinsic.SetHWIntrinsicId(NI_X86Base_LoadAlignedVector128);
            Assert.That(intrinsic.HWIntrinsicId, Is.EqualTo(NI_X86Base_LoadAlignedVector128));
            Assert.That(intrinsic.SimdBaseType, Is.EqualTo(TYP_UINT));
            Assert.That(intrinsic.Flags & GTF_ALL_EFFECT, Is.EqualTo(effects));
        });
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitTls.Compiler = compiler;

        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
