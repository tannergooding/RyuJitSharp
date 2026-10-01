// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if FEATURE_HW_INTRINSICS
using System;
using System.Runtime.CompilerServices;
#if DEBUG
using System.Runtime.InteropServices;
#endif
using NUnit.Framework;
using ValueNum = System.Int32;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.var_types;
using static RyuJitSharp.VNFunc;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ValueNumHardwareEvaluationClosureTests
{
#if DEBUG
    private static int s_assertionCount;
#endif

    [TestCase(TYP_BYTE, -1L)]
    [TestCase(TYP_USHORT, 65535L)]
    [TestCase(TYP_LONG, long.MinValue)]
    [TestCase(TYP_FLOAT, 0x80000000L)]
    [TestCase(TYP_FLOAT, 0x7F800001L)]
    [TestCase(TYP_DOUBLE, long.MinValue)]
    [TestCase(TYP_DOUBLE, 0x7FF0000000000001L)]
    public static void BroadcastRetainsElementWidthAndRawFloatingBits(var_types baseType, long bits)
    {
        WithStore((_, store) =>
        {
            var bytes = BitConverter.GetBytes(bits);
            var scalar = store.VNForGenericCon(baseType, bytes.AsSpan(0, baseType.Size));
            foreach (var type in FixedTypes)
            {
                var result = store.VNBroadcastForSimdType(type, baseType, scalar);
                var expected = new byte[type.Size];
                for (var index = 0; index <= expected.Length - baseType.Size; index += baseType.Size)
                {
                    bytes.AsSpan(0, baseType.Size).CopyTo(expected.AsSpan(index));
                }
                Assert.That(VectorBytes(store, result), Is.EqualTo(expected));
                Assert.That(store.VNBroadcastForSimdType(type, baseType, scalar), Is.EqualTo(result));
            }
        });
    }

    [TestCase(TYP_BYTE)]
    [TestCase(TYP_LONG)]
    [TestCase(TYP_FLOAT)]
    [TestCase(TYP_DOUBLE)]
    public static void VectorOneInternsScalarBeforeVectorExactlyAsNative(var_types baseType)
    {
        WithStore((compiler, store) =>
        {
            var reference = new ValueNumStore(compiler);
            var oneBytes = baseType switch
            {
                TYP_FLOAT => BitConverter.GetBytes(1.0f),
                TYP_DOUBLE => BitConverter.GetBytes(1.0),
                _ => BitConverter.GetBytes(1L),
            };
            var scalar = reference.VNForGenericCon(baseType, oneBytes.AsSpan(0, baseType.Size));
            var expected = reference.VNBroadcastForSimdType(TYP_SIMD16, baseType, scalar);
            var actual = VectorOne(store, TYP_SIMD16, baseType);

            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(store.VNForGenericCon(baseType, oneBytes.AsSpan(0, baseType.Size)), Is.EqualTo(scalar));
            Assert.That(store.VNForIntCon(93), Is.EqualTo(reference.VNForIntCon(93)));
        });
    }

    [TestCase(TYP_BYTE, -128L)]
    [TestCase(TYP_UBYTE, 255L)]
    [TestCase(TYP_SHORT, -32768L)]
    [TestCase(TYP_USHORT, 65535L)]
    [TestCase(TYP_UINT, -1L)]
    [TestCase(TYP_FLOAT, 0x80000000L)]
    [TestCase(TYP_DOUBLE, 0x7FF0000000000001L)]
    public static void ScalarExtractionNormalizesIntegralTypeWithoutChangingFloatingBits(
        var_types baseType, long bits)
    {
        WithStore((_, store) =>
        {
            var bytes = BitConverter.GetBytes(bits);
            var vector = new byte[16];
            bytes.AsSpan(0, baseType.Size).CopyTo(vector);
            var input = store.VNForGenericCon(TYP_SIMD16, vector);
            var tree = new GenTreeHWIntrinsic(baseType.ActualType, NI_Vector_ToScalar,
                baseType, 16, new GenTreeVecCon(TYP_SIMD16));
            var expected = store.VNForGenericCon(baseType, bytes.AsSpan(0, baseType.Size));
            var result = store.EvalHWIntrinsicFunUnary(tree, VNF_HWI_Vector_ToScalar, input,
                store.VNForSimdType(16, baseType));

            Assert.That(result, Is.EqualTo(expected));
            Assert.That(store.TypeOfVN(result), Is.EqualTo(baseType.ActualType));
        });
    }

    [TestCase(-1)]
    [TestCase(4)]
    [TestCase(int.MinValue)]
    public static void OutOfRangeGetElementRetainsOrderedSymbolicArguments(int index)
    {
        WithStore((_, store) =>
        {
            var input = store.VNZeroForType(TYP_SIMD16);
            var indexVN = store.VNForIntCon(index);
            var typeVN = store.VNForSimdType(16, TYP_INT);
            var tree = new GenTreeHWIntrinsic(TYP_INT, NI_Vector_GetElement, TYP_INT, 16,
                new GenTreeVecCon(TYP_SIMD16), new GenTreeLclVar(TYP_INT, 0));
            var result = store.EvalHWIntrinsicFunBinary(tree, VNF_HWI_Vector_GetElement, input, indexVN, typeVN);
            var app = new VNFuncApp();

            Assert.That(store.GetVNFunc(result, ref app), Is.True);
            Assert.That(app.Func, Is.EqualTo(VNF_HWI_Vector_GetElement));
            Assert.That(app.GetArg(0), Is.EqualTo(input));
            Assert.That(app.GetArg(1), Is.EqualTo(indexVN));
            Assert.That(app.GetArg(2), Is.EqualTo(typeVN));
            Assert.That(store.EvalHWIntrinsicFunBinary(tree, VNF_HWI_Vector_GetElement, input, indexVN, typeVN),
                Is.EqualTo(result));
        });
    }

#if FEATURE_MASKED_HW_INTRINSICS
    [TestCase(TYP_BYTE)]
    [TestCase(TYP_SHORT)]
    [TestCase(TYP_INT)]
    [TestCase(TYP_LONG)]
    [TestCase(TYP_FLOAT)]
    [TestCase(TYP_DOUBLE)]
    public static void FixedMaskConversionsRetainOnlyActiveLaneBits(var_types baseType)
    {
        WithStore((_, store) =>
        {
            var mask = default(simdmask_t);
            mask.u64[0] = 0xA55AA55AA55AA55AUL;
            var input = store.VNForSimdMaskCon(mask);
            foreach (var type in FixedTypes)
            {
                var vector = MaskToVector(store, type, baseType, input);
                var result = VectorToMask(store, type, baseType, vector);
                var count = type.Size / baseType.Size;
#if TARGET_ARM64
                ulong laneMask = 0;
                for (var index = 0; index < count; index++)
                {
                    laneMask |= 1UL << (index * baseType.Size);
                }
                var expected = mask.RawBits & laneMask;
#else
                var expected = mask.RawBits & simdmask_t.GetBitMask(count);
#endif

                Assert.That(store.GetConstantSimdMask(result).RawBits, Is.EqualTo(expected));
                Assert.That(MaskToVector(store, type, baseType, result), Is.EqualTo(vector));
            }
        });
    }

    [TestCase(TYP_LONG)]
    [TestCase(TYP_DOUBLE)]
    public static void PartialFinalLaneRetainsItsTailAndDoesNotRaiseAssertions(var_types baseType)
    {
        ICorJitInfo* jitInfo = null;
#if DEBUG
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        ICorJitInfo ee = new() { lpVtbl = &vtable };
        jitInfo = &ee;
        s_assertionCount = 0;
#endif
        WithStore((_, store) =>
        {
            var mask = default(simdmask_t);
            mask.u64[0] = ulong.MaxValue;
            var bytes = new byte[12];
            bytes.AsSpan().Fill(0xA5);

            EvaluateSimdCvtMaskToVector(baseType, bytes, mask);

            Assert.That(bytes.AsSpan(0, 8).ToArray(), Is.All.EqualTo(byte.MaxValue));
            Assert.That(bytes.AsSpan(8).ToArray(), Is.All.EqualTo((byte)0xA5));
            var preserved = bytes.AsSpan().ToArray();
            var result = default(simdmask_t);
            result.u64[0] = ulong.MaxValue;
            EvaluateSimdCvtVectorToMask(baseType, ref result, bytes);
            Assert.That(result.RawBits, Is.EqualTo(1UL));
            EvaluateExtractMSB(baseType, ref result, bytes);
            Assert.That(result.RawBits, Is.EqualTo(1UL));
            Assert.That(bytes, Is.EqualTo(preserved));

            bytes.AsSpan(0, 8).Clear();
            EvaluateSimdCvtVectorToMask(baseType, ref result, bytes);
            Assert.That(result.RawBits, Is.EqualTo(0UL));
            EvaluateExtractMSB(baseType, ref result, bytes);
            Assert.That(result.RawBits, Is.EqualTo(0UL));
            Assert.That(bytes.AsSpan(8).ToArray(), Is.All.EqualTo((byte)0xA5));

            var folded = MaskToVector(store, TYP_SIMD12, baseType, store.VNForSimdMaskCon(mask));
            var foldedBytes = VectorBytes(store, folded);
            Assert.That(foldedBytes.AsSpan(0, 8).ToArray(), Is.All.EqualTo(byte.MaxValue));
            Assert.That(foldedBytes.AsSpan(8).ToArray(), Is.All.EqualTo((byte)0));
#if DEBUG
            Assert.That(s_assertionCount, Is.Zero);
#endif
        }, jitInfo);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "EvaluateSimdCvtMaskToVectorVN")]
    private static extern ValueNum MaskToVector(ValueNumStore store, var_types type, var_types baseType, ValueNum value);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "EvaluateSimdCvtVectorToMaskVN")]
    private static extern ValueNum VectorToMask(ValueNumStore store, var_types type, var_types baseType, ValueNum value);
#endif

#if TARGET_XARCH
#if FEATURE_MASKED_HW_INTRINSICS
    [TestCase(-1, TYP_INT)]
    [TestCase(int.MinValue, TYP_INT)]
    [TestCase(-1, TYP_FLOAT)]
    [TestCase(int.MinValue, TYP_FLOAT)]
    public static void NegativeWithElementRetainsOrderedSymbolicArguments(int index, var_types baseType)
    {
        WithStore((_, store) =>
        {
            var input = store.VNZeroForType(TYP_SIMD16);
            var indexVN = store.VNForIntCon(index);
            var valueVN = baseType is TYP_FLOAT ? store.VNForFloatCon(1.0f) : store.VNForIntCon(1);
            var typeVN = store.VNForSimdType(16, baseType);
            var tree = new GenTreeHWIntrinsic(TYP_SIMD16, NI_Vector_WithElement, baseType, 16,
                new GenTreeVecCon(TYP_SIMD16), new GenTreeLclVar(TYP_INT, 0), new GenTreeLclVar(baseType, 1));
            var result = store.EvalHWIntrinsicFunTernary(tree, VNF_HWI_Vector_WithElement,
                input, indexVN, valueVN, typeVN);
            var app = new VNFuncApp();

            Assert.That(store.GetVNFunc(result, ref app), Is.True);
            Assert.That(app.Func, Is.EqualTo(VNF_HWI_Vector_WithElement));
            Assert.That(app.GetArg(0), Is.EqualTo(input));
            Assert.That(app.GetArg(1), Is.EqualTo(indexVN));
            Assert.That(app.GetArg(2), Is.EqualTo(valueVN));
            Assert.That(app.GetArg(3), Is.EqualTo(typeVN));
            Assert.That(store.EvalHWIntrinsicFunTernary(tree, VNF_HWI_Vector_WithElement,
                input, indexVN, valueVN, typeVN), Is.EqualTo(result));
        });
    }
#endif

    [TestCase(NI_Vector_ToVector256, VNF_HWI_Vector_ToVector256, TYP_SIMD16, TYP_SIMD32)]
    [TestCase(NI_Vector_ToVector256Unsafe, VNF_HWI_Vector_ToVector256Unsafe, TYP_SIMD16, TYP_SIMD32)]
    [TestCase(NI_Vector_ToVector512, VNF_HWI_Vector_ToVector512, TYP_SIMD16, TYP_SIMD64)]
    [TestCase(NI_Vector_ToVector512Unsafe, VNF_HWI_Vector_ToVector512Unsafe, TYP_SIMD32, TYP_SIMD64)]
    public static void WideningCopiesOnlyTheInputAndZeroesTheRemainingBytes(
        NamedIntrinsic id, VNFunc func, var_types inputType, var_types resultType)
    {
        WithStore((_, store) =>
        {
            var bytes = new byte[inputType.Size];
            for (var index = 0; index < bytes.Length; index++)
            {
                bytes[index] = (byte)(index + 1);
            }
            var input = store.VNForGenericCon(inputType, bytes);
            var tree = new GenTreeHWIntrinsic(resultType, id, TYP_BYTE, (byte)inputType.Size,
                new GenTreeVecCon(inputType));
            var result = store.EvalHWIntrinsicFunUnary(tree, func, input,
                store.VNForSimdType(inputType.Size, TYP_BYTE));
            var expected = new byte[resultType.Size];
            bytes.CopyTo(expected, 0);

            Assert.That(VectorBytes(store, result), Is.EqualTo(expected));
        });
    }

    [TestCase(NI_X86Base_Add, VNF_HWI_X86Base_Add, 0x80000000L, false, 1)]
    [TestCase(NI_X86Base_Add, VNF_HWI_X86Base_Add, 0L, false, 0)]
    [TestCase(NI_X86Base_Subtract, VNF_HWI_X86Base_Subtract, 0L, false, 1)]
    [TestCase(NI_X86Base_Subtract, VNF_HWI_X86Base_Subtract, 0x80000000L, false, 0)]
    [TestCase(NI_X86Base_Subtract, VNF_HWI_X86Base_Subtract, 0L, true, 0)]
    [TestCase(NI_X86Base_Multiply, VNF_HWI_X86Base_Multiply, 0L, false, 0)]
    [TestCase(NI_X86Base_Multiply, VNF_HWI_X86Base_Multiply, 0x80000000L, false, 0)]
    [TestCase(NI_X86Base_Multiply, VNF_HWI_X86Base_Multiply, 0x3F800000L, true, 1)]
    [TestCase(NI_X86Base_Divide, VNF_HWI_X86Base_Divide, 0x3F800000L, false, 1)]
    [TestCase(NI_X86Base_Divide, VNF_HWI_X86Base_Divide, 0x3F800000L, true, 0)]
    [TestCase(NI_X86Base_Add, VNF_HWI_X86Base_Add, 0x7F800001L, true, 2)]
    [TestCase(NI_X86Base_Multiply, VNF_HWI_X86Base_Multiply, 0xFFC12345L, false, 2)]
    public static void FloatingIdentitiesRespectSignedZeroNaNsAndOperandOrder(
        NamedIntrinsic id, VNFunc func, long bits, bool constantFirst, int identity)
    {
        WithStore((_, store) =>
        {
            var variable = store.VNForExpr(null, TYP_SIMD16);
            var scalar = store.VNForFloatCon(BitConverter.Int32BitsToSingle(unchecked((int)bits)));
            var constant = store.VNBroadcastForSimdType(TYP_SIMD16, TYP_FLOAT, scalar);
            var left = constantFirst ? constant : variable;
            var right = constantFirst ? variable : constant;
            var typeVN = store.VNForSimdType(16, TYP_FLOAT);
            var vector = new GenTreeVecCon(TYP_SIMD16);
            var tree = new GenTreeHWIntrinsic(TYP_SIMD16, id, TYP_FLOAT, 16, vector, vector);
            var result = store.EvalHWIntrinsicFunBinary(tree, func, left, right, typeVN);

            if (identity != 0)
            {
                Assert.That(result, Is.EqualTo(identity == 1 ? variable : constant));
            }
            else
            {
                var app = new VNFuncApp();
                Assert.That(store.GetVNFunc(result, ref app), Is.True);
                Assert.That(app.Func, Is.EqualTo(func));
                Assert.That(app.GetArg(0), Is.EqualTo(left));
                Assert.That(app.GetArg(1), Is.EqualTo(right));
                Assert.That(app.GetArg(2), Is.EqualTo(typeVN));
            }
        });
    }
#endif

#if TARGET_ARM64
    [TestCase(0UL, 3, 5)]
    [TestCase(1UL, 6, 10)]
    [TestCase(0x100000001UL, 0, 0)]
    public static void WideShiftCountsNarrowWithoutWrappingAndDuplicateAcrossTheirLanes(
        ulong count, int first, int second)
    {
        WithStore((_, store) =>
        {
            var left = default(simd16_t);
            left.i32[0] = 3;
            left.i32[1] = 5;
            left.i32[2] = 7;
            left.i32[3] = 11;
            var right = default(simd16_t);
            right.u64[0] = count;
            right.u64[1] = 33;
            var vector = new GenTreeVecCon(TYP_SIMD16);
            var tree = new GenTreeHWIntrinsic(TYP_SIMD16, NI_Sve_ShiftLeftLogical, TYP_INT, 16, vector, vector)
            {
                AuxiliaryType = TYP_ULONG,
            };
            var result = store.EvalHWIntrinsicFunBinary(tree, VNF_HWI_Sve_ShiftLeftLogical,
                store.VNForSimd16Con(left), store.VNForSimd16Con(right),
                store.VNForSimdType(16, TYP_INT, TYP_ULONG));

            Assert.That(store.GetConstantSimd16(result).i32[0], Is.EqualTo(first));
            Assert.That(store.GetConstantSimd16(result).i32[1], Is.EqualTo(second));
            Assert.That(store.GetConstantSimd16(result).i32[2], Is.Zero);
            Assert.That(store.GetConstantSimd16(result).i32[3], Is.Zero);
        });
    }

    [TestCase(0L, 64)]
    [TestCase(1L, 63)]
    [TestCase(long.MinValue, 0)]
    public static void Arm64LongLeadingZeroCountReturnsAnIntValueNumber(long input, int expected)
    {
        WithStore((_, store) =>
        {
            var tree = new GenTreeHWIntrinsic(TYP_INT, NI_ArmBase_Arm64_LeadingZeroCount, TYP_LONG, 0,
                new GenTreeLclVar(TYP_LONG, 0));
            var result = store.EvalHWIntrinsicFunUnary(tree, VNF_HWI_ArmBase_Arm64_LeadingZeroCount,
                store.VNForLongCon(input), store.VNForSimdType(0, TYP_LONG));

            Assert.That(store.TypeOfVN(result), Is.EqualTo(TYP_INT));
            Assert.That(store.GetConstantInt32(result), Is.EqualTo(expected));
        });
    }

    [TestCase(false, 1L, 0x80000000L)]
    [TestCase(false, 0x80000000L, 1L)]
    [TestCase(false, 0x01234567L, 0xE6A2C480L)]
    [TestCase(true, 1L, long.MinValue)]
    [TestCase(true, long.MinValue, 1L)]
    [TestCase(true, 0x0123456789ABCDEFL, unchecked((long)0xF7B3D591E6A2C480UL))]
    public static void Arm64ReverseBitsRetainsFullUnsignedWidth(bool wide, long input, long expected)
    {
        WithStore((_, store) =>
        {
            var type = wide ? TYP_LONG : TYP_INT;
            var id = wide ? NI_ArmBase_Arm64_ReverseElementBits : NI_ArmBase_ReverseElementBits;
            var func = wide ? VNF_HWI_ArmBase_Arm64_ReverseElementBits : VNF_HWI_ArmBase_ReverseElementBits;
            var inputVN = wide ? store.VNForLongCon(input) : store.VNForIntCon(unchecked((int)input));
            var tree = new GenTreeHWIntrinsic(type, id, type, 0, new GenTreeLclVar(type, 0));
            var result = store.EvalHWIntrinsicFunUnary(tree, func, inputVN, store.VNForSimdType(0, type));

            var bits = wide ? store.GetConstantInt64(result) : unchecked((uint)store.GetConstantInt32(result));
            Assert.That(bits, Is.EqualTo(wide ? expected : unchecked((uint)expected)));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ScalableConversionRejectsFixedScalableKindMismatch(bool scalable)
    {
        WithStore((_, store) =>
        {
            var mask = scalable
                ? store.VNForSimdMaskScalableCon(simdmaskscalable_t.AllBitsSet)
                : store.VNZeroForType(TYP_MASK);
            var result = MaskToVector(store, scalable ? TYP_SIMD16 : TYP_SIMD, TYP_INT, mask);

            Assert.That(result, Is.EqualTo(ValueNumStore.NoVN));
        });
    }

    [TestCase(TYP_BYTE)]
    [TestCase(TYP_INT)]
    [TestCase(TYP_LONG)]
    public static void ScalableRepeatedMaskRoundTripsAtMatchingElementWidth(var_types baseType)
    {
        WithStore((_, store) =>
        {
            var scalar = store.VNForGenericCon(baseType, BitConverter.GetBytes(-1L).AsSpan(0, baseType.Size));
            var vector = store.VNBroadcastForSimdType(TYP_SIMD, baseType, scalar);
            var mask = VectorToMask(store, TYP_SIMD, baseType, vector);
            var result = MaskToVector(store, TYP_SIMD, baseType, mask);

            Assert.That(result, Is.EqualTo(vector));
            Assert.That(store.GetConstantSimdMaskValue(mask).IsScalable, Is.True);
        });
    }

    [Test]
    public static void ScalableMaskUnaryAndMixedBinaryRemainSymbolic()
    {
        WithStore((_, store) =>
        {
            var mask = store.VNForSimdMaskScalableCon(simdmaskscalable_t.AllBitsSet);
            var fixedMask = store.VNZeroForType(TYP_MASK);
            var typeVN = store.VNForSimdType(0, TYP_INT);
            var node = new GenTreeMskCon(default(simdmask_t));
            var unary = new GenTreeHWIntrinsic(TYP_MASK, NI_Sve_Not, TYP_INT, 0, node);
            var binary = new GenTreeHWIntrinsic(TYP_MASK, NI_Sve_And, TYP_INT, 0, node, node);
            var unaryResult = store.EvalHWIntrinsicFunUnary(unary, VNF_HWI_Sve_Not, mask, typeVN);
            var binaryResult = store.EvalHWIntrinsicFunBinary(binary, VNF_HWI_Sve_And, fixedMask, mask, typeVN);
            var app = new VNFuncApp();

            Assert.That(store.GetVNFunc(unaryResult, ref app), Is.True);
            Assert.That(app.GetArg(0), Is.EqualTo(mask));
            Assert.That(store.GetVNFunc(binaryResult, ref app), Is.True);
            Assert.That(app.GetArg(0), Is.EqualTo(fixedMask));
            Assert.That(app.GetArg(1), Is.EqualTo(mask));
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(3)]
    public static void MultiplyByScalarIgnoresAllButTheFirstRhsElement(int first)
    {
        WithStore((_, store) =>
        {
            var left = default(simd16_t);
            left.f32[0] = 7;
            left.f32[1] = 11;
            var right = default(simd8_t);
            right.f32[0] = first;
            right.f32[1] = 999;
            var tree = new GenTreeHWIntrinsic(TYP_SIMD16, NI_AdvSimd_MultiplyByScalar, TYP_FLOAT, 16,
                new GenTreeVecCon(TYP_SIMD16), new GenTreeVecCon(TYP_SIMD8));
            var result = store.EvalHWIntrinsicFunBinary(tree, VNF_HWI_AdvSimd_MultiplyByScalar,
                store.VNForSimd16Con(left), store.VNForSimd8Con(right), store.VNForSimdType(16, TYP_FLOAT));

            Assert.That(store.GetConstantSimd16(result).f32[0], Is.EqualTo(7.0f * first));
            Assert.That(store.GetConstantSimd16(result).f32[1], Is.EqualTo(11.0f * first));
        });
    }
#endif

    private static var_types[] FixedTypes =>
    [
        TYP_SIMD8, TYP_SIMD12, TYP_SIMD16,
#if TARGET_XARCH
        TYP_SIMD32, TYP_SIMD64,
#endif
    ];

    private static byte[] VectorBytes(ValueNumStore store, ValueNum value)
    {
        var vector = store.GetConstantSimd(value);
        return vector.AsSpan<byte>()[..store.TypeOfVN(value).Size].ToArray();
    }

    private static void WithStore(Action<Compiler, ValueNumStore> action, ICorJitInfo* jitInfo = null)
    {
#if DEBUG
        using var tls = new JitTls(jitInfo);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.info.compCompHnd = jitInfo;
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

#if DEBUG
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* jitInfo, byte* file, int line, byte* expression)
    {
        s_assertionCount++;
        return 0;
    }
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "VNOneForSimdType")]
    private static extern ValueNum VectorOne(ValueNumStore store, var_types type, var_types baseType);
}
#endif
