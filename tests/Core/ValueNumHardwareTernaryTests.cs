// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if FEATURE_HW_INTRINSICS && (TARGET_XARCH || TARGET_ARM64 || TARGET_WASM)
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using ValueNum = System.Int32;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.var_types;
using static RyuJitSharp.VNFunc;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ValueNumHardwareTernaryTests
{
    [TestCase(TYP_BYTE)]
    [TestCase(TYP_SHORT)]
    [TestCase(TYP_INT)]
    [TestCase(TYP_LONG)]
    [TestCase(TYP_FLOAT)]
    [TestCase(TYP_DOUBLE)]
    public static void ConditionalSelectionPreservesRawBitsAndNativeInterningOrder(var_types baseType)
    {
        WithStore((compiler, store) =>
        {
            var reference = new ValueNumStore(compiler);
            var mask = SelectionMask();
            var first = SelectionFirst();
            var second = SelectionSecond();
            var inputs = InternSelection(store, baseType, mask, first, second);
            var referenceInputs = InternSelection(reference, baseType, mask, first, second);
            var tree = SelectionTree(SelectionId, baseType, TYP_SIMD16);
            var result = store.EvalHWIntrinsicFunTernary(tree, SelectionFunc,
                inputs.Mask, inputs.First, inputs.Second, inputs.Type);

            _ = reference.VNZeroForType(TYP_SIMD16);
            _ = reference.VNForSimd16Con(simd16_t.AllBitsSet);
            var trueBits = new byte[16];
            var falseBits = new byte[16];
            var expected = new byte[16];
            for (var index = 0; index < expected.Length; index++)
            {
                trueBits[index] = (byte)(first[index] & mask[index]);
                falseBits[index] = (byte)(second[index] & ~mask[index]);
                expected[index] = (byte)(trueBits[index] | falseBits[index]);
            }
            _ = reference.VNForGenericCon(TYP_SIMD16, trueBits);
            _ = reference.VNForGenericCon(TYP_SIMD16, falseBits);
            var expectedVN = reference.VNForGenericCon(TYP_SIMD16, expected);

            Assert.That(inputs, Is.EqualTo(referenceInputs));
            Assert.That(VectorBytes(store, result), Is.EqualTo(expected));
            Assert.That(result, Is.EqualTo(expectedVN));
            Assert.That(store.VNForExpr(null, TYP_SIMD16),
                Is.EqualTo(reference.VNForExpr(null, TYP_SIMD16)));
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public static void ConditionalSelectionRetainsNativeShortcutOrdering(int scenario)
    {
        WithStore((_, store) =>
        {
            var first = store.VNForGenericCon(TYP_SIMD16, SelectionFirst());
            var second = scenario >= 2 ? first : store.VNForGenericCon(TYP_SIMD16, SelectionSecond());
            var mask = scenario switch
            {
                0 => store.VNZeroForType(TYP_SIMD16),
                1 => store.VNForSimd16Con(simd16_t.AllBitsSet),
                2 => store.VNForExpr(null, TYP_SIMD16),
                _ => store.VNForGenericCon(TYP_SIMD16, SelectionMask()),
            };
            var type = store.VNForSimdType(16, TYP_FLOAT);
            var tree = SelectionTree(SelectionId, TYP_FLOAT, TYP_SIMD16);
            var result = store.EvalHWIntrinsicFunTernary(tree, SelectionFunc, mask, first, second, type);

            Assert.That(result, Is.EqualTo(scenario == 0 ? second : first));
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public static void NonconstantSelectionRetainsAllOrderedSymbolicArguments(int unknownOperand)
    {
        WithStore((_, store) =>
        {
            var inputs = InternSelection(store, TYP_INT, SelectionMask(), SelectionFirst(), SelectionSecond());
            var mask = unknownOperand == 0 ? store.VNForExpr(null, TYP_SIMD16) : inputs.Mask;
            var first = unknownOperand == 1 ? store.VNForExpr(null, TYP_SIMD16) : inputs.First;
            var second = unknownOperand == 2 ? store.VNForExpr(null, TYP_SIMD16) : inputs.Second;
            var tree = SelectionTree(SelectionId, TYP_INT, TYP_SIMD16);
            var result = store.EvalHWIntrinsicFunTernary(tree, SelectionFunc,
                mask, first, second, inputs.Type);

            AssertSymbolic(store, result, SelectionFunc, mask, first, second, inputs.Type);
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
    public static void IntegralWithElementTruncatesOnlyTheSelectedLaneAndPreservesAllOtherBytes(var_types baseType)
    {
        WithStore((_, store) =>
        {
            var value = varTypeIsLong(baseType)
                ? unchecked((long)0xFEDCBA9876543210UL)
                : unchecked((int)0x89ABCDEFU);
            var scalar = varTypeIsLong(baseType) ? store.VNForLongCon(value) : store.VNForIntCon((int)value);
            foreach (var type in FixedTypes)
            {
                var bytes = new byte[type.Size];
                bytes.AsSpan().Fill(0xA5);
                var input = store.VNForGenericCon(type, bytes);
                var index = (type.Size / baseType.Size) - 1;
                var indexVN = store.VNForIntCon(index);
                var typeVN = store.VNForSimdType(type.Size, baseType);
                var scalarType = varTypeIsLong(baseType) ? TYP_LONG : TYP_INT;
                var tree = WithElementTree(type, baseType, scalarType);
                var result = store.EvalHWIntrinsicFunTernary(tree, VNF_HWI_Vector_WithElement,
                    input, indexVN, scalar, typeVN);
                var expected = (byte[])bytes.Clone();
                BitConverter.GetBytes(value).AsSpan(0, baseType.Size)
                    .CopyTo(expected.AsSpan(index * baseType.Size));

                Assert.That(VectorBytes(store, result), Is.EqualTo(expected));
                Assert.That(VectorBytes(store, input), Is.EqualTo(bytes));
                Assert.That(result, Is.EqualTo(store.VNForGenericCon(type, expected)));
            }
        });
    }

    [TestCase(TYP_FLOAT, 0x80000000UL, 0x80000000UL)]
    [TestCase(TYP_FLOAT, 0x7F800001UL, 0x7FC00001UL)]
    [TestCase(TYP_DOUBLE, 0x8000000000000000UL, 0x8000000000000000UL)]
    [TestCase(TYP_DOUBLE, 0x7FF8000000000123UL, 0x7FF8000000000123UL)]
    public static void FloatingWithElementPreservesNativeConversionAndUnselectedPayloads(
        var_types baseType, ulong bits, ulong expectedBits)
    {
        WithStore((_, store) =>
        {
            var scalar = baseType == TYP_FLOAT
                ? store.VNForFloatCon(BitConverter.Int32BitsToSingle(unchecked((int)bits)))
                : store.VNForDoubleCon(BitConverter.Int64BitsToDouble(unchecked((long)bits)));
            foreach (var type in FixedTypes)
            {
                var bytes = new byte[type.Size];
                bytes.AsSpan().Fill(0xA5);
                var input = store.VNForGenericCon(type, bytes);
                var typeVN = store.VNForSimdType(type.Size, baseType);
                var tree = WithElementTree(type, baseType, baseType);
                var result = store.EvalHWIntrinsicFunTernary(tree, VNF_HWI_Vector_WithElement,
                    input, store.VNForIntCon(0), scalar, typeVN);
                var expected = (byte[])bytes.Clone();
                BitConverter.GetBytes(expectedBits).AsSpan(0, baseType.Size).CopyTo(expected);

                Assert.That(VectorBytes(store, result), Is.EqualTo(expected));
                Assert.That(VectorBytes(store, input), Is.EqualTo(bytes));
            }
        });
    }

    [TestCase(TYP_INT, -1)]
    [TestCase(TYP_INT, int.MinValue)]
    [TestCase(TYP_INT, 4)]
    [TestCase(TYP_DOUBLE, -1)]
    [TestCase(TYP_DOUBLE, int.MinValue)]
    [TestCase(TYP_DOUBLE, 2)]
    public static void OutOfRangeWithElementRetainsNativeUnsignedRejection(var_types baseType, int index)
    {
        WithStore((_, store) =>
        {
            var input = store.VNZeroForType(TYP_SIMD16);
            var indexVN = store.VNForIntCon(index);
            var value = baseType == TYP_DOUBLE ? store.VNForDoubleCon(1.0) : store.VNForIntCon(1);
            var typeVN = store.VNForSimdType(16, baseType);
            var tree = WithElementTree(TYP_SIMD16, baseType, baseType);
            var result = store.EvalHWIntrinsicFunTernary(tree, VNF_HWI_Vector_WithElement,
                input, indexVN, value, typeVN);

            AssertSymbolic(store, result, VNF_HWI_Vector_WithElement, input, indexVN, value, typeVN);
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public static void NonconstantWithElementRetainsAllOrderedSymbolicArguments(int unknownOperand)
    {
        WithStore((_, store) =>
        {
            var input = unknownOperand == 0 ? store.VNForExpr(null, TYP_SIMD16) : store.VNZeroForType(TYP_SIMD16);
            var index = unknownOperand == 1 ? store.VNForExpr(null, TYP_INT) : store.VNForIntCon(0);
            var value = unknownOperand == 2 ? store.VNForExpr(null, TYP_INT) : store.VNForIntCon(123);
            var typeVN = store.VNForSimdType(16, TYP_INT);
            var tree = WithElementTree(TYP_SIMD16, TYP_INT, TYP_INT);
            var result = store.EvalHWIntrinsicFunTernary(tree, VNF_HWI_Vector_WithElement,
                input, index, value, typeVN);

            AssertSymbolic(store, result, VNF_HWI_Vector_WithElement, input, index, value, typeVN);
        });
    }

#if TARGET_XARCH
    [TestCase(NI_X86Base_BlendVariable, VNF_HWI_X86Base_BlendVariable, 0)]
    [TestCase(NI_X86Base_BlendVariable, VNF_HWI_X86Base_BlendVariable, 1)]
    [TestCase(NI_X86Base_BlendVariable, VNF_HWI_X86Base_BlendVariable, 2)]
    [TestCase(NI_X86Base_BlendVariable, VNF_HWI_X86Base_BlendVariable, 3)]
    [TestCase(NI_AVX_BlendVariable, VNF_HWI_AVX_BlendVariable, 0)]
    [TestCase(NI_AVX_BlendVariable, VNF_HWI_AVX_BlendVariable, 1)]
    [TestCase(NI_AVX_BlendVariable, VNF_HWI_AVX_BlendVariable, 2)]
    [TestCase(NI_AVX_BlendVariable, VNF_HWI_AVX_BlendVariable, 3)]
    [TestCase(NI_AVX2_BlendVariable, VNF_HWI_AVX2_BlendVariable, 0)]
    [TestCase(NI_AVX2_BlendVariable, VNF_HWI_AVX2_BlendVariable, 1)]
    [TestCase(NI_AVX2_BlendVariable, VNF_HWI_AVX2_BlendVariable, 2)]
    [TestCase(NI_AVX2_BlendVariable, VNF_HWI_AVX2_BlendVariable, 3)]
    [TestCase(NI_AVX512_BlendVariableMask, VNF_HWI_AVX512_BlendVariableMask, 0)]
    [TestCase(NI_AVX512_BlendVariableMask, VNF_HWI_AVX512_BlendVariableMask, 1)]
    [TestCase(NI_AVX512_BlendVariableMask, VNF_HWI_AVX512_BlendVariableMask, 2)]
    [TestCase(NI_AVX512_BlendVariableMask, VNF_HWI_AVX512_BlendVariableMask, 3)]
    public static void XarchBlendRetainsNativeShortcutAndFallbackOrdering(NamedIntrinsic id, VNFunc func, int scenario)
    {
        WithStore((_, store) =>
        {
            var first = store.VNForGenericCon(TYP_SIMD16, SelectionFirst());
            var second = scenario >= 2 ? first : store.VNForGenericCon(TYP_SIMD16, SelectionSecond());
            var maskType = id == NI_AVX512_BlendVariableMask ? TYP_MASK : TYP_SIMD16;
            var partialMask = default(simdmask_t);
            partialMask.u64[0] = 0b_1010;
            var mask = scenario switch
            {
                0 => store.VNZeroForType(maskType),
                1 when maskType == TYP_MASK => store.VNForSimdMaskCon(simdmask_t.AllBitsSet(4)),
                1 => store.VNForSimd16Con(simd16_t.AllBitsSet),
                3 when maskType == TYP_MASK => store.VNForSimdMaskCon(partialMask),
                3 => store.VNForGenericCon(TYP_SIMD16, SelectionMask()),
                _ => store.VNForExpr(null, maskType),
            };
            var typeVN = store.VNForSimdType(16, TYP_INT);
            var maskNode = maskType == TYP_MASK
                ? (GenTree)new GenTreeMskCon(default)
                : new GenTreeVecCon(TYP_SIMD16);
            var tree = new GenTreeHWIntrinsic(TYP_SIMD16, id, TYP_INT, 16,
                new GenTreeVecCon(TYP_SIMD16), new GenTreeVecCon(TYP_SIMD16), maskNode);
            var result = store.EvalHWIntrinsicFunTernary(tree, func, first, second, mask, typeVN);

            if (scenario == 3)
            {
                AssertSymbolic(store, result, func, first, second, mask, typeVN);
            }
            else
            {
                Assert.That(result, Is.EqualTo(scenario == 0 ? first : second));
            }
        });
    }
#endif

#if TARGET_ARM64
    [TestCase(TYP_BYTE)]
    [TestCase(TYP_SHORT)]
    [TestCase(TYP_INT)]
    [TestCase(TYP_LONG)]
    [TestCase(TYP_FLOAT)]
    [TestCase(TYP_DOUBLE)]
    public static void SveSelectionUsesByteSpacedPredicateBitsAndNativeInterningOrder(var_types baseType)
    {
        WithStore((compiler, store) =>
        {
            var reference = new ValueNumStore(compiler);
            var mask = default(simdmask_t);
            mask.u64[0] = 0xA55A;
            var first = SelectionFirst();
            var second = SelectionSecond();
            var maskVN = store.VNForSimdMaskCon(mask);
            var firstVN = store.VNForGenericCon(TYP_SIMD16, first);
            var secondVN = store.VNForGenericCon(TYP_SIMD16, second);
            var typeVN = store.VNForSimdType(16, baseType);
            var referenceMask = reference.VNForSimdMaskCon(mask);
            _ = reference.VNForGenericCon(TYP_SIMD16, first);
            _ = reference.VNForGenericCon(TYP_SIMD16, second);
            _ = reference.VNForSimdType(16, baseType);
            var tree = SelectionTree(NI_Sve_ConditionalSelect, baseType, TYP_MASK);
            var result = store.EvalHWIntrinsicFunTernary(tree, VNF_HWI_Sve_ConditionalSelect,
                maskVN, firstVN, secondVN, typeVN);

            _ = reference.VNZeroForType(TYP_SIMD16);
            _ = reference.VNForSimd16Con(simd16_t.AllBitsSet);
            var vectorMask = new byte[16];
            var trueBits = new byte[16];
            var falseBits = new byte[16];
            var expected = new byte[16];
            for (var index = 0; index < expected.Length; index++)
            {
                var laneStart = index - (index % baseType.Size);
                vectorMask[index] = (mask.RawBits & (1UL << laneStart)) != 0 ? (byte)0xFF : (byte)0;
                trueBits[index] = (byte)(first[index] & vectorMask[index]);
                falseBits[index] = (byte)(second[index] & ~vectorMask[index]);
                expected[index] = (byte)(trueBits[index] | falseBits[index]);
            }
            _ = reference.VNForGenericCon(TYP_SIMD16, vectorMask);
            _ = reference.VNForGenericCon(TYP_SIMD16, trueBits);
            _ = reference.VNForGenericCon(TYP_SIMD16, falseBits);
            var expectedVN = reference.VNForGenericCon(TYP_SIMD16, expected);

            Assert.That(maskVN, Is.EqualTo(referenceMask));
            Assert.That(VectorBytes(store, result), Is.EqualTo(expected));
            Assert.That(result, Is.EqualTo(expectedVN));
            Assert.That(store.VNForExpr(null, TYP_SIMD16),
                Is.EqualTo(reference.VNForExpr(null, TYP_SIMD16)));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void SveUnrepresentableScalableMaskRetainsSymbolicSelection(bool allBits)
    {
        WithStore((_, store) =>
        {
            var mask = store.VNForSimdMaskScalableCon(allBits ? simdmaskscalable_t.AllBitsSet : simdmaskscalable_t.Zero);
            var first = store.VNForGenericCon(TYP_SIMD16, SelectionFirst());
            var second = store.VNForGenericCon(TYP_SIMD16, SelectionSecond());
            var typeVN = store.VNForSimdType(16, TYP_INT);
            var tree = SelectionTree(NI_Sve_ConditionalSelect, TYP_INT, TYP_MASK);
            var result = store.EvalHWIntrinsicFunTernary(tree, VNF_HWI_Sve_ConditionalSelect,
                mask, first, second, typeVN);

            AssertSymbolic(store, result, VNF_HWI_Sve_ConditionalSelect, mask, first, second, typeVN);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void SveEqualOperandsShortcutOnlyWithANonconstantPredicate(bool constant)
    {
        WithStore((_, store) =>
        {
            var mask = constant
                ? store.VNForSimdMaskScalableCon(simdmaskscalable_t.Zero)
                : store.VNForExpr(null, TYP_MASK);
            var value = store.VNForGenericCon(TYP_SIMD16, SelectionFirst());
            var typeVN = store.VNForSimdType(16, TYP_INT);
            var tree = SelectionTree(NI_Sve_ConditionalSelect, TYP_INT, TYP_MASK);
            var result = store.EvalHWIntrinsicFunTernary(tree, VNF_HWI_Sve_ConditionalSelect,
                mask, value, value, typeVN);

            if (constant)
            {
                AssertSymbolic(store, result, VNF_HWI_Sve_ConditionalSelect, mask, value, value, typeVN);
            }
            else
            {
                Assert.That(result, Is.EqualTo(value));
            }
        });
    }
#endif

    [Test]
    public static void OtherTernaryIntrinsicRetainsItsOrderedSymbolicFunction()
    {
        WithStore((_, store) =>
        {
#if TARGET_XARCH
            var intrinsicId = NI_AVX2_MultiplyAdd;
            var intrinsicFunc = VNF_HWI_AVX2_MultiplyAdd;
            var baseType = TYP_FLOAT;
#elif TARGET_ARM64
            var intrinsicId = NI_AdvSimd_FusedMultiplyAdd;
            var intrinsicFunc = VNF_HWI_AdvSimd_FusedMultiplyAdd;
            var baseType = TYP_FLOAT;
#else
            var intrinsicId = NI_PackedSimd_BitwiseSelect;
            var intrinsicFunc = VNF_HWI_PackedSimd_BitwiseSelect;
            var baseType = TYP_INT;
#endif
            var first = store.VNForGenericCon(TYP_SIMD16, SelectionFirst());
            var second = store.VNForGenericCon(TYP_SIMD16, SelectionSecond());
            var third = store.VNZeroForType(TYP_SIMD16);
            var typeVN = store.VNForSimdType(16, baseType);
            var tree = new GenTreeHWIntrinsic(TYP_SIMD16, intrinsicId, baseType, 16,
                new GenTreeVecCon(TYP_SIMD16), new GenTreeVecCon(TYP_SIMD16), new GenTreeVecCon(TYP_SIMD16));
            var result = store.EvalHWIntrinsicFunTernary(tree, intrinsicFunc,
                first, second, third, typeVN);

            AssertSymbolic(store, result, intrinsicFunc, first, second, third, typeVN);
        });
    }

    private static NamedIntrinsic SelectionId =>
#if TARGET_ARM64
        NI_AdvSimd_BitwiseSelect;
#else
        NI_Vector_ConditionalSelect;
#endif

    private static VNFunc SelectionFunc =>
#if TARGET_ARM64
        VNF_HWI_AdvSimd_BitwiseSelect;
#else
        VNF_HWI_Vector_ConditionalSelect;
#endif

    private static byte[] SelectionMask() =>
    [
        0x0F, 0xF0, 0x33, 0xCC, 0x55, 0xAA, 0x69, 0x96,
        0x00, 0xFF, 0x81, 0x7E, 0x24, 0x42, 0x18, 0xE7,
    ];

    private static byte[] SelectionFirst() =>
    [
        0x45, 0x23, 0x81, 0x7F, 0x00, 0x00, 0x00, 0x80,
        0x89, 0xAB, 0xC1, 0x7F, 0x00, 0x00, 0x00, 0x00,
    ];

    private static byte[] SelectionSecond() =>
    [
        0x00, 0x00, 0x00, 0x80, 0x89, 0xAB, 0xC1, 0xFF,
        0x45, 0x23, 0x81, 0x7F, 0x00, 0x00, 0x00, 0x00,
    ];

    private static (ValueNum Mask, ValueNum First, ValueNum Second, ValueNum Type) InternSelection(
        ValueNumStore store, var_types baseType, byte[] mask, byte[] first, byte[] second)
    {
        var maskVN = store.VNForGenericCon(TYP_SIMD16, mask);
        var firstVN = store.VNForGenericCon(TYP_SIMD16, first);
        var secondVN = store.VNForGenericCon(TYP_SIMD16, second);
        var typeVN = store.VNForSimdType(16, baseType);

        return (maskVN, firstVN, secondVN, typeVN);
    }

    private static GenTreeHWIntrinsic SelectionTree(NamedIntrinsic id, var_types baseType, var_types maskType)
    {
        var mask = maskType == TYP_MASK
            ? (GenTree)new GenTreeMskCon(default)
            : new GenTreeVecCon(TYP_SIMD16);

        return new GenTreeHWIntrinsic(TYP_SIMD16, id, baseType, 16,
            mask, new GenTreeVecCon(TYP_SIMD16), new GenTreeVecCon(TYP_SIMD16));
    }

    private static GenTreeHWIntrinsic WithElementTree(var_types type, var_types baseType, var_types scalarType)
    {
        return new GenTreeHWIntrinsic(type, NI_Vector_WithElement, baseType, (byte)type.Size,
            new GenTreeVecCon(type), new GenTreeLclVar(TYP_INT, 0), new GenTreeLclVar(scalarType, 1));
    }

    private static void AssertSymbolic(ValueNumStore store, ValueNum result, VNFunc func,
        ValueNum first, ValueNum second, ValueNum third, ValueNum type)
    {
        var app = new VNFuncApp();
        Assert.That(store.GetVNFunc(result, ref app), Is.True);
        Assert.That(app.Func, Is.EqualTo(func));
        Assert.That(app.GetArg(0), Is.EqualTo(first));
        Assert.That(app.GetArg(1), Is.EqualTo(second));
        Assert.That(app.GetArg(2), Is.EqualTo(third));
        Assert.That(app.GetArg(3), Is.EqualTo(type));
        Assert.That(store.VNForFunc(TYP_SIMD16, func, first, second, third, type), Is.EqualTo(result));
    }

    private static byte[] VectorBytes(ValueNumStore store, ValueNum value)
    {
        var vector = store.GetConstantSimd(value);

        return vector.AsSpan<byte>()[..store.TypeOfVN(value).Size].ToArray();
    }

    private static var_types[] FixedTypes =>
    [
        TYP_SIMD8, TYP_SIMD12, TYP_SIMD16,
#if TARGET_XARCH
        TYP_SIMD32, TYP_SIMD64,
#endif
    ];

    private static void WithStore(Action<Compiler, ValueNumStore> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        JitTls.Compiler = compiler;
        try
        {
            action(compiler, new ValueNumStore(compiler));
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
#endif
