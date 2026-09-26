// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class SimdImportConstructorTests
{
    [TestCase((byte)16, NI_X86Base_Ceiling, NI_X86Base_Floor, NI_X86Base_RoundToNearestInteger)]
    [TestCase((byte)32, NI_AVX_Ceiling, NI_AVX_Floor, NI_AVX_RoundToNearestInteger)]
    [TestCase((byte)64, NI_AVX512_RoundScale, NI_AVX512_RoundScale, NI_AVX512_RoundScale)]
    public static void RoundingUsesNativeWidthAndControl(byte size, NamedIntrinsic ceil,
        NamedIntrinsic floor, NamedIntrinsic round)
    {
        WithCompiler(compiler =>
        {
            var type = Compiler.GetSimdTypeForSize(size);
            var value = Local(compiler, type);
            var ceiling = compiler.gtNewSimdCeilNode(type, value, TYP_FLOAT, size).AsHWIntrinsic();
            var flooring = compiler.gtNewSimdFloorNode(type, value, TYP_FLOAT, size).AsHWIntrinsic();
            var rounding = compiler.gtNewSimdRoundNode(type, value, TYP_FLOAT, size).AsHWIntrinsic();
            Assert.That(ceiling.HWIntrinsicId, Is.EqualTo(ceil));
            Assert.That(flooring.HWIntrinsicId, Is.EqualTo(floor));
            Assert.That(rounding.HWIntrinsicId, Is.EqualTo(round));
            if (size == 64)
            {
                Assert.That((int)ceiling.GetOp(2).AsIntCon().IconValue,
                    Is.EqualTo((int)FloatRoundingMode.ToPositiveInfinity));
                Assert.That((int)flooring.GetOp(2).AsIntCon().IconValue,
                    Is.EqualTo((int)FloatRoundingMode.ToNegativeInfinity));
                Assert.That((int)rounding.GetOp(2).AsIntCon().IconValue,
                    Is.EqualTo((int)FloatRoundingMode.ToNearestInteger));
            }
        });
    }

    [TestCase((byte)16, false, NI_X86Base_LoadAlignedVector128NonTemporal)]
    [TestCase((byte)32, false, NI_AVX_LoadAlignedVector256)]
    [TestCase((byte)32, true, NI_AVX2_LoadAlignedVector256NonTemporal)]
    [TestCase((byte)64, false, NI_AVX512_LoadAlignedVector512NonTemporal)]
    public static void MemoryConstructorsPreserveAlignmentTemporalFlagsAndOrder(byte size,
        bool avx2, NamedIntrinsic expectedLoad)
    {
        WithCompiler(compiler =>
        {
            if (avx2)
            {
                Enable(compiler, InstructionSet_AVX2);
            }
            var type = Compiler.GetSimdTypeForSize(size);
            var address = Local(compiler, TYP_BYREF);
            var value = Local(compiler, type);
            Assert.That(compiler.gtNewSimdLoadNode(type, address, TYP_FLOAT, size).Oper,
                Is.EqualTo(GT_IND));
            var aligned = compiler.gtNewSimdLoadAlignedNode(type, address, TYP_FLOAT, size)
                .AsHWIntrinsic();
            Assert.That(aligned.HWIntrinsicId, Is.EqualTo(size switch
            {
                64 => NI_AVX512_LoadAlignedVector512,
                32 => NI_AVX_LoadAlignedVector256,
                _ => NI_X86Base_LoadAlignedVector128,
            }));
            var temporal = compiler.gtNewSimdLoadNonTemporalNode(type, address, TYP_FLOAT, size)
                .AsHWIntrinsic();
            Assert.That(temporal.HWIntrinsicId, Is.EqualTo(expectedLoad));
            Assert.That(temporal.SimdBaseType,
                Is.EqualTo((size == 32 && !avx2) ? TYP_FLOAT : TYP_INT));
            Assert.That(compiler.gtNewSimdStoreNode(address, value, TYP_FLOAT, size).Oper,
                Is.EqualTo(GT_STOREIND));
            var store = compiler.gtNewSimdStoreAlignedNode(address, value, TYP_FLOAT, size)
                .AsHWIntrinsic();
            Assert.That(store.Type, Is.EqualTo(TYP_VOID));
            Assert.That(store.GetOp(1), Is.SameAs(address));
            Assert.That(store.GetOp(2), Is.SameAs(value));
            Assert.That(compiler.gtNewSimdStoreNonTemporalNode(address, value, TYP_FLOAT, size)
                .AsHWIntrinsic().HWIntrinsicId, Is.EqualTo(size switch
                {
                    64 => NI_AVX512_StoreAlignedNonTemporal,
                    32 => NI_AVX_StoreAlignedNonTemporal,
                    _ => NI_X86Base_StoreAlignedNonTemporal,
                }));
        });
    }

    [TestCase(TYP_FLOAT, TYP_INT, (byte)16, NI_X86Base_ConvertToVector128Int32WithTruncation)]
    [TestCase(TYP_FLOAT, TYP_INT, (byte)32, NI_AVX_ConvertToVector256Int32WithTruncation)]
    [TestCase(TYP_FLOAT, TYP_INT, (byte)64, NI_AVX512_ConvertToVector512Int32WithTruncation)]
    [TestCase(TYP_FLOAT, TYP_UINT, (byte)16, NI_AVX512_ConvertToVector128UInt32WithTruncation)]
    [TestCase(TYP_FLOAT, TYP_UINT, (byte)32, NI_AVX512_ConvertToVector256UInt32WithTruncation)]
    [TestCase(TYP_FLOAT, TYP_UINT, (byte)64, NI_AVX512_ConvertToVector512UInt32WithTruncation)]
    [TestCase(TYP_DOUBLE, TYP_LONG, (byte)16, NI_AVX512_ConvertToVector128Int64WithTruncation)]
    [TestCase(TYP_DOUBLE, TYP_LONG, (byte)32, NI_AVX512_ConvertToVector256Int64WithTruncation)]
    [TestCase(TYP_DOUBLE, TYP_LONG, (byte)64, NI_AVX512_ConvertToVector512Int64WithTruncation)]
    [TestCase(TYP_DOUBLE, TYP_ULONG, (byte)16, NI_AVX512_ConvertToVector128UInt64WithTruncation)]
    [TestCase(TYP_DOUBLE, TYP_ULONG, (byte)32, NI_AVX512_ConvertToVector256UInt64WithTruncation)]
    [TestCase(TYP_DOUBLE, TYP_ULONG, (byte)64, NI_AVX512_ConvertToVector512UInt64WithTruncation)]
    public static void NativeConversionSelectsExactSourceTargetAndWidth(var_types source,
        var_types target, byte size, NamedIntrinsic expected)
    {
        WithCompiler(compiler =>
        {
            Enable(compiler, InstructionSet_AVX512);
            var type = Compiler.GetSimdTypeForSize(size);
            var value = Local(compiler, type);
            var result = compiler.gtNewSimdCvtNativeNode(type, value, target, source, size)
                .AsHWIntrinsic();
            Assert.That(result.HWIntrinsicId, Is.EqualTo(expected));
            Assert.That(result.SimdBaseType, Is.EqualTo(source));
            Assert.That(result.GetOp(1), Is.SameAs(value));
        });
    }

    [TestCase(TYP_INT, NI_AVX10v2_ConvertToVectorInt32WithTruncatedSaturation)]
    [TestCase(TYP_UINT, NI_AVX10v2_ConvertToVectorUInt32WithTruncatedSaturation)]
    [TestCase(TYP_LONG, NI_AVX10v2_ConvertToVectorInt64WithTruncatedSaturation)]
    [TestCase(TYP_ULONG, NI_AVX10v2_ConvertToVectorUInt64WithTruncatedSaturation)]
    public static void Avx10SaturatingConversionUsesDirectIntrinsic(var_types target,
        NamedIntrinsic expected)
    {
        WithCompiler(compiler =>
        {
            Enable(compiler, InstructionSet_AVX512);
            Enable(compiler, InstructionSet_AVX10v2);
            var value = Local(compiler, TYP_SIMD16);
            var source = target is TYP_INT or TYP_UINT ? TYP_FLOAT : TYP_DOUBLE;
            var result = compiler.gtNewSimdCvtNode(TYP_SIMD16, value, target, source, 16)
                .AsHWIntrinsic();
            Assert.That(result.HWIntrinsicId, Is.EqualTo(expected));
        });
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void SaturatingConversionPreservesNativeFixupAndSignedMaximum(
        bool avx512, bool unsigned)
    {
        WithCompiler(compiler =>
        {
            if (avx512)
            {
                Enable(compiler, InstructionSet_AVX512);
            }
            var input = Local(compiler, TYP_SIMD16);
            var target = unsigned ? TYP_UINT : TYP_INT;
            var result = compiler.gtNewSimdCvtNode(TYP_SIMD16, input, target, TYP_FLOAT, 16);
            Assert.That(result.Type, Is.EqualTo(TYP_SIMD16));
            Assert.That(ContainsIntrinsic(result, NI_AVX512_Fixup), Is.EqualTo(avx512));
            Assert.That(ContainsIntrinsic(result, NI_X86Base_ConvertToVector128Int32WithTruncation),
                Is.EqualTo(!unsigned));
            if (unsigned)
            {
                Assert.That(result.AsHWIntrinsic().HWIntrinsicId,
                    Is.EqualTo(NI_AVX512_ConvertToVector128UInt32WithTruncation));
            }
            else
            {
                Assert.That(result.AsHWIntrinsic().HWIntrinsicId,
                    Is.EqualTo(NI_Vector_ConditionalSelect));
            }
        });
    }

    [TestCase(TYP_BYTE, (byte)16)]
    [TestCase(TYP_USHORT, (byte)32)]
    [TestCase(TYP_INT, (byte)64)]
    [TestCase(TYP_LONG, (byte)16)]
    [TestCase(TYP_FLOAT, (byte)32)]
    [TestCase(TYP_DOUBLE, (byte)64)]
    public static void IndicesAndConstantSequencesPreserveLaneValues(var_types baseType, byte size)
    {
        WithCompiler(compiler =>
        {
            var type = Compiler.GetSimdTypeForSize(size);
            var count = size / baseType.Size;
            var indices = compiler.gtNewSimdGetIndicesNode(type, baseType, size).AsVecCon();
            var start = Scalar(compiler, baseType, 5);
            var step = Scalar(compiler, baseType, 3);
            var sequence = compiler.gtNewSimdCreateSequenceNode(type, start, step, baseType, size)
                .AsVecCon();
            var alternating = compiler.gtNewSimdCreateAlternatingSequenceNode(type, start,
                step, baseType, size).AsVecCon();
            for (var index = 0; index < count; index++)
            {
                AssertLane(indices, baseType, index, index);
                AssertLane(sequence, baseType, index, 5 + (3 * index));
                AssertLane(alternating, baseType, index, (index & 1) == 0 ? 5 : 3);
            }
        });
    }

    [Test]
    public static void IntegerSequencesWrapAtElementWidthAndPartialStartIsAdded()
    {
        WithCompiler(compiler =>
        {
            var start = compiler.gtNewIconNode(TYP_INT, byte.MaxValue);
            var step = compiler.gtNewIconNode(TYP_INT, 3);
            var sequence = compiler.gtNewSimdCreateSequenceNode(TYP_SIMD16, start, step,
                TYP_UBYTE, 16).AsVecCon();
            AssertLane(sequence, TYP_UBYTE, 0, 255);
            AssertLane(sequence, TYP_UBYTE, 1, 2);
            var variableStart = Local(compiler, TYP_INT);
            var partial = compiler.gtNewSimdCreateSequenceNode(TYP_SIMD16, variableStart,
                step, TYP_INT, 16);
            Assert.That(partial.Type, Is.EqualTo(TYP_SIMD16));
            Assert.That(partial.Oper, Is.EqualTo(GT_HWINTRINSIC));
        });
    }

    [TestCase((byte)16, NI_X86Base_UnpackLow, NI_X86Base_UnpackHigh)]
    [TestCase((byte)32, NI_X86Base_UnpackLow, NI_X86Base_UnpackHigh)]
    public static void ZipAndUnzipPreserveNativeSelection(byte size,
        NamedIntrinsic low, NamedIntrinsic high)
    {
        WithCompiler(compiler =>
        {
            Enable(compiler, InstructionSet_AVX2);
            var type = Compiler.GetSimdTypeForSize(size);
            var left = Local(compiler, type);
            var right = Local(compiler, type);
            var zipped = compiler.gtNewSimdZipNode(type, left, right, TYP_INT, size, false);
            Assert.That(zipped, Is.Not.Null);
            if (size == 16)
            {
                Assert.That(zipped.AsHWIntrinsic().HWIntrinsicId, Is.EqualTo(low));
                Assert.That(compiler.gtNewSimdZipNode(type, left, right, TYP_INT, size, true)
                    .AsHWIntrinsic().HWIntrinsicId, Is.EqualTo(high));
                Assert.That(compiler.gtNewSimdUnzipNode(type, left, right, TYP_INT, size, true)
                    .AsHWIntrinsic().HWIntrinsicId, Is.EqualTo(NI_X86Base_Shuffle));
            }
            else
            {
                Assert.That(compiler.gtNewSimdUnzipNode(type, left, right, TYP_INT, size, false),
                    Is.Not.Null);
            }
            Assert.That(compiler.gtNewSimdReverseNode(type, left, TYP_INT, size), Is.Not.Null);
        });
    }

    [TestCase((byte)32, TYP_INT, NI_AVX512_PermuteVar8x32x2)]
    [TestCase((byte)64, TYP_BYTE, NI_AVX512v2_PermuteVar64x8x2)]
    public static void Avx512ZipAndUnzipUseInterleaveAndDeinterleaveIndices(
        byte size, var_types baseType, NamedIntrinsic intrinsic)
    {
        WithCompiler(compiler =>
        {
            Enable(compiler, InstructionSet_AVX512);
            Enable(compiler, InstructionSet_AVX512v2);
            var type = Compiler.GetSimdTypeForSize(size);
            var count = size / baseType.Size;
            var left = Local(compiler, type);
            var right = Local(compiler, type);
            var zip = compiler.gtNewSimdZipNode(type, left, right, baseType, size, false)
                .AsHWIntrinsic();
            var zipUpper = compiler.gtNewSimdZipNode(type, left, right, baseType, size, true)
                .AsHWIntrinsic();
            var unzip = compiler.gtNewSimdUnzipNode(type, left, right, baseType, size, false)
                .AsHWIntrinsic();
            var unzipOdd = compiler.gtNewSimdUnzipNode(type, left, right, baseType, size, true)
                .AsHWIntrinsic();
            foreach (var operation in new[] { zip, zipUpper, unzip, unzipOdd })
            {
                Assert.That(operation.HWIntrinsicId, Is.EqualTo(intrinsic));
                Assert.That(operation.GetOp(1), Is.SameAs(left));
                Assert.That(operation.GetOp(3), Is.SameAs(right));
            }
            foreach (var index in new[] { 0, 1, count / 2, count - 1 })
            {
                AssertLane(zip.GetOp(2).AsVecCon(), baseType, index,
                    (index / 2) + ((index & 1) != 0 ? count : 0));
                AssertLane(zipUpper.GetOp(2).AsVecCon(), baseType, index,
                    (count / 2) + (index / 2) + ((index & 1) != 0 ? count : 0));
                AssertLane(unzip.GetOp(2).AsVecCon(), baseType, index,
                    index < (count / 2) ? index * 2 : count + ((index - (count / 2)) * 2));
                AssertLane(unzipOdd.GetOp(2).AsVecCon(), baseType, index,
                    index < (count / 2) ? 1 + (index * 2) :
                        count + 1 + ((index - (count / 2)) * 2));
            }
        });
    }

    [TestCase(false, false, NI_X86Base_MoveLowToHigh)]
    [TestCase(true, true, NI_X86Base_MoveHighToLow)]
    [TestCase(true, false, NI_X86Base_Shuffle)]
    [TestCase(false, true, NI_X86Base_Shuffle)]
    public static void ConcatPreservesHalfSelectionAndReverseFlag(bool leftUpper,
        bool rightUpper, NamedIntrinsic expected)
    {
        WithCompiler(compiler =>
        {
            var left = Local(compiler, TYP_SIMD16);
            var right = Local(compiler, TYP_SIMD16);
            var result = compiler.gtNewSimdConcatNode(TYP_SIMD16, left, right, TYP_INT, 16,
                leftUpper, rightUpper).AsHWIntrinsic();
            Assert.That(result.HWIntrinsicId, Is.EqualTo(expected));
            Assert.That((result.Flags & GTF_REVERSE_OPS) != 0,
                Is.EqualTo(leftUpper && rightUpper));
            if (expected == NI_X86Base_Shuffle)
            {
                Assert.That((int)result.GetOp(3).AsIntCon().IconValue,
                    Is.EqualTo(leftUpper ? (rightUpper ? 0xEE : 0x4E) :
                        (rightUpper ? 0xE4 : 0x44)));
            }
        });
    }

    [Test]
    public static void DotFmaTernaryAndHorizontalSumUseExpectedPrimitiveShapes()
    {
        WithCompiler(compiler =>
        {
            Enable(compiler, InstructionSet_AVX2);
            Enable(compiler, InstructionSet_AVX512);
            var input = Local(compiler, TYP_SIMD16);
            Assert.That(compiler.gtNewSimdDotProdNode(TYP_SIMD16, input, input, TYP_FLOAT, 16)
                .AsHWIntrinsic().HWIntrinsicId, Is.EqualTo(NI_Vector_Dot));
            Assert.That(compiler.gtNewSimdFmaNode(TYP_SIMD16, input, input, input, TYP_FLOAT, 16)
                .AsHWIntrinsic().HWIntrinsicId, Is.EqualTo(NI_AVX2_MultiplyAdd));
            Assert.That(compiler.gtNewSimdTernaryLogicNode(TYP_SIMD16, input, input, input,
                compiler.gtNewIconNode(TYP_INT, 0xCA), TYP_INT, 16)
                .AsHWIntrinsic().HWIntrinsicId, Is.EqualTo(NI_AVX512_TernaryLogic));
            Assert.That(compiler.gtNewSimdSumNode(TYP_INT, input, TYP_INT, 16).Type,
                Is.EqualTo(TYP_INT));
            Assert.That(compiler.gtNewSimdSumNode(TYP_FLOAT, input, TYP_FLOAT, 16).Type,
                Is.EqualTo(TYP_FLOAT));
            Assert.That(compiler.gtNewSimdNarrowWithSaturationNode(TYP_SIMD16, input,
                input, TYP_INT, 16).Type, Is.EqualTo(TYP_SIMD16));
        });
    }

    [TestCase((byte)16, TYP_FLOAT, NI_X86Base_Shuffle)]
    [TestCase((byte)32, TYP_DOUBLE, NI_AVX_Permute)]
    [TestCase((byte)64, TYP_FLOAT, NI_AVX512_ExtractVector128)]
    [TestCase((byte)64, TYP_INT, NI_X86Base_ShiftRightLogical128BitLane)]
    public static void SumRetainsNativeWidthAndReductionGrouping(byte size, var_types baseType,
        NamedIntrinsic keyOperation)
    {
        WithCompiler(compiler =>
        {
            Enable(compiler, InstructionSet_AVX2);
            Enable(compiler, InstructionSet_AVX512);
            var value = Local(compiler, Compiler.GetSimdTypeForSize(size));
            var result = compiler.gtNewSimdSumNode(baseType, value, baseType, size);
            Assert.That(result.Type, Is.EqualTo(baseType));
            Assert.That(ContainsIntrinsic(result, keyOperation), Is.True);
        });
    }

    [TestCase(TYP_SHORT)]
    [TestCase(TYP_USHORT)]
    [TestCase(TYP_INT)]
    [TestCase(TYP_UINT)]
    [TestCase(TYP_LONG)]
    [TestCase(TYP_ULONG)]
    [TestCase(TYP_DOUBLE)]
    public static void SaturatingNarrowCoversAllElementWidths(var_types baseType)
    {
        WithCompiler(compiler =>
        {
            var left = Local(compiler, TYP_SIMD16);
            var right = Local(compiler, TYP_SIMD16);
            var result = compiler.gtNewSimdNarrowWithSaturationNode(TYP_SIMD16, left, right,
                baseType, 16);
            Assert.That(result.Type, Is.EqualTo(TYP_SIMD16));
        });
    }

    private static bool ContainsIntrinsic(GenTree tree, NamedIntrinsic id)
    {
        if (tree is GenTreeHWIntrinsic intrinsic && intrinsic.HWIntrinsicId == id)
        {
            return true;
        }
        foreach (var operand in tree.Operands)
        {
            if (ContainsIntrinsic(operand, id))
            {
                return true;
            }
        }
        return false;
    }

    private static GenTree Scalar(Compiler compiler, var_types type, int value)
    {
        if (type is TYP_FLOAT or TYP_DOUBLE)
        {
            return compiler.gtNewDconNode(type, value);
        }

        return type is TYP_LONG or TYP_ULONG
            ? compiler.gtNewLconNode(value)
            : compiler.gtNewIconNode(TYP_INT, value);
    }

    private static GenTreeLclVar Local(Compiler compiler, var_types type)
    {
        var index = compiler.lvaCount++;
        compiler.lvaTable[index] = new LclVarDsc { Type = type };
        return compiler.gtNewLclvNode(type, index);
    }

    private static void AssertLane(GenTreeVecCon vector, var_types type, int index, int value)
    {
        if (type is TYP_FLOAT)
        {
            Assert.That(vector.SimdVal.AsSpan<float>()[index], Is.EqualTo((float)value));
        }
        else if (type is TYP_DOUBLE)
        {
            Assert.That(vector.SimdVal.AsSpan<double>()[index], Is.EqualTo((double)value));
        }
        else
        {
            switch (type.Size)
            {
                case 1:
                {
                    Assert.That(vector.SimdVal.AsSpan<byte>()[index], Is.EqualTo(unchecked((byte)value)));
                    break;
                }

                case 2:
                {
                    Assert.That(vector.SimdVal.AsSpan<ushort>()[index], Is.EqualTo(unchecked((ushort)value)));
                    break;
                }

                case 4:
                {
                    Assert.That(vector.SimdVal.AsSpan<uint>()[index], Is.EqualTo(unchecked((uint)value)));
                    break;
                }

                default:
                {
                    Assert.That(vector.SimdVal.AsSpan<ulong>()[index], Is.EqualTo(unchecked((ulong)value)));
                    break;
                }
            }
        }
    }

    private static void Enable(Compiler compiler, CORINFO_InstructionSet instructionSet)
    {
        compiler.opts.compSupportsISA.AddInstructionSet(instructionSet);
        compiler.opts.compSupportsISAReported.AddInstructionSet(instructionSet);
        compiler.opts.compSupportsISAExactly.AddInstructionSet(instructionSet);
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var previousConfig = JitConfig;
        JitConfig = new JitConfigValues();
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.info = new Compiler.Info();
        compiler.lvaTable = new LclVarDsc[128];
        compiler.lvaCount = 2;
        compiler.lvaTable[0] = new LclVarDsc { Type = TYP_SIMD16 };
        compiler.lvaTable[1] = new LclVarDsc { Type = TYP_INT };
        JitTls.Compiler = compiler;
        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
            JitConfig = previousConfig;
        }
    }
}
