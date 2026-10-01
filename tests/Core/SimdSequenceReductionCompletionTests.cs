// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if FEATURE_HW_INTRINSICS && (TARGET_ARM64 || TARGET_WASM)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;
#if TARGET_ARM64
using static RyuJitSharp.SimdScalableKind;
#else
using static RyuJitSharp.GenTreeFlags;
#endif

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class SimdSequenceReductionCompletionTests
{
    [Test]
    public static void FixedIntegralIndicesAndSequencesWrapAtElementWidth(
        [Values(TYP_BYTE, TYP_UBYTE, TYP_SHORT, TYP_USHORT, TYP_INT, TYP_UINT, TYP_LONG, TYP_ULONG)]
        var_types baseType,
#if TARGET_ARM64
        [Values((byte)8, (byte)16)] byte size)
#else
        [Values((byte)16)] byte size)
#endif
    {
        WithCompiler(compiler =>
        {
            var type = Compiler.GetSimdTypeForSize(size);
            var indices = compiler.gtNewSimdGetIndicesNode(type, baseType, size);
            var sequence = compiler.gtNewSimdCreateSequenceNode(type, Scalar(compiler, baseType, -1),
                Scalar(compiler, baseType, 3), baseType, size);
            var unsignedType = Compiler.getUnsignedSimdBaseType(baseType);
            var mask = baseType.Size == 8 ? ulong.MaxValue : (1UL << (baseType.Size * 8)) - 1;
            for (var index = 0; index < size / baseType.Size; index++)
            {
                Assert.That(indices.GetIntegralVectorConstElement(index, unsignedType), Is.EqualTo((ulong)index));
                Assert.That(sequence.GetIntegralVectorConstElement(index, unsignedType),
                    Is.EqualTo(unchecked((ulong)(-1L + (3L * index))) & mask));
            }
        });
    }

    [Test]
    public static void FixedFloatingSequencesRetainPrecisionSignedZeroAndNonfiniteArithmetic(
        [Values(TYP_FLOAT, TYP_DOUBLE)] var_types baseType,
#if TARGET_ARM64
        [Values((byte)8, (byte)16)] byte size,
#else
        [Values((byte)16)] byte size,
#endif
        [Values(0, 1, 2, 3)] int scenario)
    {
        WithCompiler(compiler =>
        {
            var type = Compiler.GetSimdTypeForSize(size);
            var start = scenario switch
            {
                0 => 16777217.0,
                1 => BitConverter.Int64BitsToDouble(unchecked((long)0x8000_0000_0000_0000UL)),
                2 => BitConverter.Int64BitsToDouble(0x7FF8_0000_0000_1234),
                _ => 0.0,
            };
            var step = scenario switch
            {
                0 => 0.1,
                1 => start,
                2 => 1.0,
                _ => double.PositiveInfinity,
            };
            var indices = compiler.gtNewSimdGetIndicesNode(type, baseType, size).AsVecCon();
            var sequence = compiler.gtNewSimdCreateSequenceNode(type, compiler.gtNewDconNode(baseType, start),
                compiler.gtNewDconNode(baseType, step), baseType, size).AsVecCon();

            for (var index = 0; index < size / baseType.Size; index++)
            {
                if (baseType == TYP_FLOAT)
                {
                    var expected = (index * (float)step) + (float)start;
                    Assert.That(indices.SimdVal.f32[index], Is.EqualTo((float)index));
                    Assert.That(sequence.SimdVal.u32[index],
                        Is.EqualTo(unchecked((uint)BitConverter.SingleToInt32Bits(expected))));
                }
                else
                {
                    var expected = (index * step) + start;
                    Assert.That(indices.SimdVal.f64[index], Is.EqualTo((double)index));
                    Assert.That(sequence.SimdVal.u64[index],
                        Is.EqualTo(unchecked((ulong)BitConverter.DoubleToInt64Bits(expected))));
                }
            }
        });
    }

    [Test]
    public static void VariableStepCapturesStartBeforeOverwritingIt()
    {
        WithCompiler(compiler =>
        {
            var start = Local(compiler, TYP_INT);
            var overwrite = compiler.gtNewStoreLclVarNode(start.LclNum, compiler.gtNewIconNode(TYP_INT, 17));
            var step = compiler.gtNewBinaryNode(GT_COMMA, TYP_INT, overwrite, compiler.gtNewIconNode(TYP_INT, 3));
            var result = compiler.gtNewSimdCreateSequenceNode(TYP_SIMD16, start, step, TYP_INT, 16).AsHWIntrinsic();
            var nodes = Nodes(result).ToArray();
            var capture = nodes.Single(node => node.Oper == GT_STORE_LCL_VAR
                && ReferenceEquals(node.AsLclVarCommon().Data, start));
            var temporary = capture.AsLclVarCommon().LclNum;
            Assert.That(Array.IndexOf(nodes, capture), Is.LessThan(Array.IndexOf(nodes, overwrite)));
            Assert.That(Nodes(result.GetOp(2)).Any(node => node.Oper == GT_LCL_VAR
                && node.AsLclVarCommon().LclNum == temporary), Is.True);
        });
    }

#if TARGET_ARM64
    [Test]
    public static void ScalableIndicesUseIntegralSequencesAndFloatingConversions(
        [Values(TYP_BYTE, TYP_UBYTE, TYP_SHORT, TYP_USHORT, TYP_INT, TYP_UINT, TYP_LONG, TYP_ULONG,
            TYP_FLOAT, TYP_DOUBLE)] var_types baseType)
    {
        WithCompiler(compiler =>
        {
            var result = compiler.gtNewSimdGetIndicesNode(TYP_SIMD, baseType, 0);
            if (baseType is TYP_FLOAT or TYP_DOUBLE)
            {
                var conversion = result.AsHWIntrinsic();
                Assert.That(conversion.HWIntrinsicId,
                    Is.EqualTo(baseType == TYP_FLOAT ? NI_Sve_ConvertToSingle : NI_Sve_ConvertToDouble));
                Assert.That(conversion.SimdBaseType, Is.EqualTo(baseType == TYP_FLOAT ? TYP_INT : TYP_LONG));
                Assert.That(conversion.Type, Is.EqualTo(TYP_SIMD));
                Assert.That(conversion.SimdSize, Is.Zero);
                result = conversion.GetOp(1);
                baseType = baseType == TYP_FLOAT ? TYP_INT : TYP_LONG;
            }

            var value = result.AsVecCon().SimdScalableVal;
            Assert.That(value.BaseType, Is.EqualTo(baseType));
            Assert.That(value.Kind, Is.EqualTo(SimdScalableSequence));
            Assert.That(value.Index.u64[0], Is.Zero);
            Assert.That(value.Step.u64[0], Is.EqualTo(1UL));
        });
    }

    [Test]
    public static void ScalableIntegralSequencesPreserveConstantMaskingOrNativeOperandOrder(
        [Values(TYP_BYTE, TYP_UBYTE, TYP_SHORT, TYP_USHORT, TYP_INT, TYP_UINT, TYP_LONG, TYP_ULONG)]
        var_types baseType,
        [Values(false, true)] bool constantStart,
        [Values(false, true)] bool constantStep)
    {
        WithCompiler(compiler =>
        {
            var scalarType = baseType.Size == 8 ? TYP_LONG : TYP_INT;
            var start = constantStart ? Scalar(compiler, baseType, -1) : Local(compiler, scalarType);
            var step = constantStep ? Scalar(compiler, baseType, 3) : Local(compiler, scalarType);
            var result = compiler.gtNewSimdCreateSequenceNode(TYP_SIMD, start, step, baseType, 0);
            if (constantStart && constantStep)
            {
                var value = result.AsVecCon().SimdScalableVal;
                var mask = baseType.Size == 8 ? ulong.MaxValue : (1UL << (baseType.Size * 8)) - 1;
                Assert.That(value.BaseType, Is.EqualTo(baseType));
                Assert.That(value.Kind, Is.EqualTo(SimdScalableSequence));
                Assert.That(value.Index.u64[0], Is.EqualTo(mask));
                Assert.That(value.Step.u64[0], Is.EqualTo(3UL));
            }
            else
            {
                var intrinsic = result.AsHWIntrinsic();
                Assert.That(intrinsic.HWIntrinsicId, Is.EqualTo(NI_Vector_CreateSequence));
                Assert.That(intrinsic.SimdBaseType, Is.EqualTo(baseType));
                Assert.That(intrinsic.SimdSize, Is.Zero);
                Assert.That(intrinsic.GetOp(1), Is.SameAs(start));
                Assert.That(intrinsic.GetOp(2), Is.SameAs(step));
            }
            Assert.That(result.Type, Is.EqualTo(TYP_SIMD));
        });
    }

    [Test]
    public static void ScalableFloatingSequencesRetainConvertedIndicesMultiplyThenAdd(
        [Values(TYP_FLOAT, TYP_DOUBLE)] var_types baseType,
        [Values(false, true)] bool constants)
    {
        WithCompiler(compiler =>
        {
            GenTree start = constants ? compiler.gtNewDconNode(baseType, -0.0) : Local(compiler, baseType);
            GenTree step = constants ? compiler.gtNewDconNode(baseType, 0.0) : Local(compiler, baseType);
            var result = compiler.gtNewSimdCreateSequenceNode(TYP_SIMD, start, step, baseType, 0);
            var conversion = baseType == TYP_FLOAT ? NI_Sve_ConvertToSingle : NI_Sve_ConvertToDouble;
            Assert.That(result.Type, Is.EqualTo(TYP_SIMD));
            Assert.That(result.Oper, Is.EqualTo(GT_HWINTRINSIC));
            Assert.That(Nodes(result).Any(node => node.Oper == GT_HWINTRINSIC
                && node.AsHWIntrinsic().HWIntrinsicId == conversion), Is.True);
            Assert.That(result.AsHWIntrinsic().GetOp(1).Oper, Is.EqualTo(GT_HWINTRINSIC));
            Assert.That(result.AsHWIntrinsic().GetOp(1).AsHWIntrinsic().GetOp(1).AsHWIntrinsic().HWIntrinsicId,
                Is.EqualTo(conversion));
        });
    }

    [Test]
    public static void Arm64SumUsesNativeReductionWidthsAndPairwiseGrouping(
        [Values(TYP_BYTE, TYP_UBYTE, TYP_SHORT, TYP_USHORT, TYP_INT, TYP_UINT, TYP_LONG, TYP_ULONG,
            TYP_FLOAT, TYP_DOUBLE)] var_types baseType,
        [Values((byte)8, (byte)16)] byte size)
    {
        WithCompiler(compiler =>
        {
            var value = compiler.gtNewVconNode(Compiler.GetSimdTypeForSize(size));
            const ulong bits = 0x8000_0000_7FC0_1234;
            value.SimdVal.u64[0] = bits;
            var result = compiler.gtNewSimdSumNode(baseType.ActualType, value, baseType, size).AsHWIntrinsic();
            Assert.That(result.HWIntrinsicId, Is.EqualTo(NI_Vector_ToScalar));
            Assert.That(result.Type, Is.EqualTo(baseType.ActualType));
            Assert.That(result.SimdBaseType, Is.EqualTo(baseType));
            Assert.That(result.SimdSize, Is.EqualTo(baseType == TYP_FLOAT ? size : 8));

            if ((size == 8) && (baseType is TYP_LONG or TYP_ULONG or TYP_DOUBLE))
            {
                Assert.That(result.GetOp(1), Is.SameAs(value));
            }
            else
            {
                var expected = (baseType, size) switch
                {
                    (TYP_INT or TYP_UINT, 8) => NI_AdvSimd_AddPairwise,
                    (TYP_FLOAT, 16) => NI_AdvSimd_Arm64_AddPairwise,
                    (TYP_FLOAT or TYP_LONG or TYP_ULONG or TYP_DOUBLE, _) => NI_AdvSimd_Arm64_AddPairwiseScalar,
                    _ => NI_AdvSimd_Arm64_AddAcross,
                };
                Assert.That(result.GetOp(1).AsHWIntrinsic().HWIntrinsicId, Is.EqualTo(expected));
                if ((baseType == TYP_FLOAT) && (size == 16))
                {
                    Assert.That(Nodes(result).Count(node => node.Oper == GT_HWINTRINSIC
                        && node.AsHWIntrinsic().HWIntrinsicId == expected), Is.EqualTo(2));
                }
            }
            Assert.That(value.SimdVal.u64[0], Is.EqualTo(bits));
        });
    }
#else
    [Test]
    public static void WasmSumPreservesIncreasingStrideMasksAndStoreBeforeReload(
        [Values(TYP_BYTE, TYP_UBYTE, TYP_SHORT, TYP_USHORT, TYP_INT, TYP_UINT, TYP_LONG, TYP_ULONG,
            TYP_FLOAT, TYP_DOUBLE)] var_types baseType)
    {
        WithCompiler(compiler =>
        {
            var source = compiler.gtNewVconNode(TYP_SIMD16);
            const ulong bits = 0x8000_0000_7FC0_1234;
            source.SimdVal.u64[0] = bits;
            _ = AssertWasmReduction(compiler, source, baseType);
            Assert.That(source.SimdVal.u64[0], Is.EqualTo(bits));
        });
    }

    [Test]
    public static void WasmSumCapturesAnEffectfulSourceOnceBeforeReloadingIt()
    {
        WithCompiler(compiler =>
        {
            var source = new GenTreeIndir(GT_IND, TYP_SIMD16, Local(compiler, TYP_BYREF));
            source.Flags |= GTF_EXCEPT;
            var result = AssertWasmReduction(compiler, source, TYP_FLOAT);
            Assert.That(Nodes(result).Count(node => ReferenceEquals(node, source)), Is.EqualTo(1));
        });
    }

    private static GenTree AssertWasmReduction(Compiler compiler, GenTree source, var_types baseType)
    {
        var result = compiler.gtNewSimdSumNode(baseType.ActualType, source, baseType, 16).AsHWIntrinsic();
        Assert.That(result.HWIntrinsicId, Is.EqualTo(NI_Vector_ToScalar));
        Assert.That(result.Type, Is.EqualTo(baseType.ActualType));
        Assert.That(result.SimdBaseType, Is.EqualTo(baseType));
        Assert.That(result.SimdSize, Is.EqualTo(16));
        var current = result.GetOp(1);
        var count = 16 / baseType.Size;
        for (var stride = count / 2; stride >= 1; stride /= 2)
        {
            var addition = current.AsHWIntrinsic();
            Assert.That(addition.HWIntrinsicId, Is.EqualTo(NI_PackedSimd_Add));
            Assert.That(addition.SimdBaseType, Is.EqualTo(baseType));
            var shuffle = addition.GetOp(1).AsHWIntrinsic();
            Assert.That(shuffle.HWIntrinsicId, Is.EqualTo(NI_PackedSimd_Swizzle));
            var selectors = shuffle.GetOp(2).AsVecCon();
            for (var index = 0; index < count; index++)
            {
                for (var offset = 0; offset < baseType.Size; offset++)
                {
                    var expected = index + stride < count
                        ? (byte)(((index + stride) * baseType.Size) + offset) : byte.MaxValue;
                    Assert.That(selectors.SimdVal.u8[(index * baseType.Size) + offset], Is.EqualTo(expected));
                }
            }

            current = shuffle.GetOp(1);
            if (current.Oper == GT_COMMA)
            {
                var capture = current.AsOp().Op1;
                Assert.That(capture.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
                var reload = current.AsOp().Op2;
                Assert.That(reload.Oper, Is.EqualTo(GT_LCL_VAR));
                Assert.That(addition.GetOp(2).Oper, Is.EqualTo(GT_LCL_VAR));
                Assert.That(reload.AsLclVarCommon().LclNum, Is.EqualTo(capture.AsLclVarCommon().LclNum));
                Assert.That(addition.GetOp(2).AsLclVarCommon().LclNum,
                    Is.EqualTo(capture.AsLclVarCommon().LclNum));
                current = capture.AsLclVarCommon().Data;
            }
        }
        Assert.That(current, Is.SameAs(source));

        return result;
    }
#endif

    private static GenTree Scalar(Compiler compiler, var_types baseType, long value)
    {
        return baseType.Size == 8 ? compiler.gtNewLconNode(value) : compiler.gtNewIconNode(TYP_INT, (nint)value);
    }

    private static GenTreeLclVar Local(Compiler compiler, var_types type)
    {
        var index = compiler.lvaCount++;
        compiler.lvaTable[index] = new LclVarDsc { Type = type };

        return compiler.gtNewLclvNode(type, index);
    }

    private static IEnumerable<GenTree> Nodes(GenTree tree)
    {
        yield return tree;
        foreach (var operand in tree.Operands)
        {
            foreach (var node in Nodes(operand))
            {
                yield return node;
            }
        }
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
#endif
