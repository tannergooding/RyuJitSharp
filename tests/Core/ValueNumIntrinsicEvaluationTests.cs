// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ValueNumIntrinsicEvaluationTests
{
    [TestCase(NamedIntrinsic.NI_PRIMITIVE_LeadingZeroCount, 0, 32)]
    [TestCase(NamedIntrinsic.NI_PRIMITIVE_TrailingZeroCount, 0, 32)]
    [TestCase(NamedIntrinsic.NI_PRIMITIVE_PopCount, -1, 32)]
    [TestCase(NamedIntrinsic.NI_PRIMITIVE_PopCount, 0x7F, 7)]
    public static void IntegerBitCountsFoldConstants(NamedIntrinsic intrinsic, int input, int expected)
    {
        WithStore(store => {
            var result = store.EvalMathFuncUnary(TYP_INT, intrinsic, store.VNForIntCon(input));
            Assert.That(store.GetConstantInt32(result), Is.EqualTo(expected));
        });
    }

    [Test]
    public static void FloatMathFoldsConstantsAndPreservesSignedZero()
    {
        WithStore(store => {
            var value = store.EvalMathFuncUnary(TYP_DOUBLE, NamedIntrinsic.NI_System_Math_Round,
                store.VNForDoubleCon(-0.25));
            Assert.That(BitConverter.DoubleToInt64Bits(store.GetConstantDouble(value)),
                Is.EqualTo(BitConverter.DoubleToInt64Bits(-0.0)));

            var power = store.EvalMathFuncBinary(TYP_FLOAT, NamedIntrinsic.NI_System_Math_Pow,
                store.VNForFloatCon(2.0f), store.VNForFloatCon(3.0f));
            Assert.That(store.GetConstantSingle(power), Is.EqualTo(8.0f));
        });
    }

    [Test]
    public static void IntegralExponentHandlesZeroNaNAndInfinity()
    {
        WithStore(store => {
            Assert.That(store.GetConstantInt32(store.EvalMathFuncUnary(TYP_INT, NI_System_Math_ILogB,
                store.VNForDoubleCon(-0.0))), Is.EqualTo(int.MinValue));
            Assert.That(store.GetConstantInt32(store.EvalMathFuncUnary(TYP_INT, NI_System_Math_ILogB,
                store.VNForFloatCon(float.NaN))), Is.EqualTo(int.MaxValue));
            Assert.That(store.GetConstantInt32(store.EvalMathFuncUnary(TYP_INT, NI_System_Math_ILogB,
                store.VNForDoubleCon(double.PositiveInfinity))), Is.EqualTo(int.MaxValue));
        });
    }

    [Test]
    public static void SymbolicMathAndSimdTypePreserveFunctionAndOperandOrder()
    {
        WithStore(store => {
            var input = store.VNForExpr(null, TYP_DOUBLE);
            var result = store.EvalMathFuncUnary(TYP_DOUBLE, NamedIntrinsic.NI_System_Math_Sqrt, input);
            var app = new VNFuncApp();
            Assert.That(store.GetVNFunc(result, ref app), Is.True);
            Assert.That(app.Func, Is.EqualTo(VNFunc.VNF_Sqrt));
            Assert.That(app.GetArg(0), Is.EqualTo(input));

            var simd = store.VNForSimdType(32, TYP_INT);
            Assert.That(store.GetVNFunc(simd, ref app), Is.True);
            Assert.That(app.Func, Is.EqualTo(VNFunc.VNF_SimdType));
            Assert.That(store.GetConstantInt32(app.GetArg(0)), Is.EqualTo(32));
            Assert.That(store.GetConstantInt32(app.GetArg(1)), Is.EqualTo((int)TYP_INT));
        });
    }

    [Test]
    public static void AotOnlyFoldsTargetImplementedMath()
    {
        WithStore(store => {
            var constant = store.VNForDoubleCon(2.0);
            var power = store.EvalMathFuncBinary(TYP_DOUBLE, NI_System_Math_Pow, constant, constant);
            Assert.That(store.IsVNConstant(power), Is.False);
            var app = new VNFuncApp();
            Assert.That(store.GetVNFunc(power, ref app), Is.True);
            Assert.That(app.Func, Is.EqualTo(VNFunc.VNF_Pow));
            Assert.That(app.GetArg(0), Is.EqualTo(constant));
            Assert.That(app.GetArg(1), Is.EqualTo(constant));

            var absolute = store.EvalMathFuncUnary(TYP_DOUBLE, NI_System_Math_Abs,
                store.VNForDoubleCon(-2.0));
            Assert.That(store.GetConstantDouble(absolute), Is.EqualTo(2.0));
        }, aot: true);
    }

    [Test]
    public static void HardwareUnaryFoldsBitCountsAndExtractsScalar()
    {
        WithStore(store => {
            var scalar = new GenTreeLclVar(TYP_LONG, 0);
            var count = new GenTreeHWIntrinsic(TYP_LONG, NI_AVX2_X64_LeadingZeroCount,
                TYP_LONG, 0, scalar);
            var type = store.VNForSimdType(0, TYP_LONG);
            var result = store.EvalHWIntrinsicFunUnary(count, VNFunc.VNF_HWI_AVX2_X64_LeadingZeroCount,
                store.VNForLongCon(1), type);
            Assert.That(store.GetConstantInt64(result), Is.EqualTo(63));

            var vector = default(simd16_t);
            vector.i32[0] = -12;
            vector.i32[1] = 91;
            var extract = new GenTreeHWIntrinsic(TYP_INT, NI_Vector_ToScalar, TYP_INT, 16,
                new GenTreeVecCon(TYP_SIMD16));
            var element = store.EvalHWIntrinsicFunUnary(extract, VNFunc.VNF_HWI_Vector_ToScalar,
                store.VNForSimd16Con(vector), store.VNForSimdType(16, TYP_INT));
            Assert.That(store.GetConstantInt32(element), Is.EqualTo(-12));
        });
    }

    [Test]
    public static void HardwareBitScanZeroRemainsSymbolic()
    {
        WithStore(store => {
            var scan = new GenTreeHWIntrinsic(TYP_INT, NI_X86Base_BitScanForward, TYP_INT, 0,
                new GenTreeLclVar(TYP_INT, 0));
            var zero = store.VNForIntCon(0);
            var type = store.VNForSimdType(0, TYP_INT);
            var result = store.EvalHWIntrinsicFunUnary(scan, VNFunc.VNF_HWI_X86Base_BitScanForward,
                zero, type);
            var app = new VNFuncApp();
            Assert.That(store.GetVNFunc(result, ref app), Is.True);
            Assert.That(app.Func, Is.EqualTo(VNFunc.VNF_HWI_X86Base_BitScanForward));
            Assert.That(app.GetArg(0), Is.EqualTo(zero));
            Assert.That(app.GetArg(1), Is.EqualTo(type));
        });
    }

    [Test]
    public static void HardwareBinaryFoldsVectorAndPreservesOutOfRangeGetElement()
    {
        WithStore(store => {
            var first = default(simd16_t);
            first.i32[0] = 11;
            first.i32[1] = 22;
            var second = default(simd16_t);
            second.i32[0] = 2;
            second.i32[1] = 3;
            var firstVN = store.VNForSimd16Con(first);
            var secondVN = store.VNForSimd16Con(second);
            var vector = new GenTreeVecCon(TYP_SIMD16);
            var add = new GenTreeHWIntrinsic(TYP_SIMD16, NI_X86Base_Add, TYP_INT, 16, vector, vector);
            var simdTypeVN = store.VNForSimdType(16, TYP_INT);
            var sumVN = store.EvalHWIntrinsicFunBinary(add, VNFunc.VNF_HWI_X86Base_Add,
                firstVN, secondVN, simdTypeVN);
            var sum = store.GetConstantSimd16(sumVN);
            Assert.That(sum.i32[0], Is.EqualTo(13));
            Assert.That(sum.i32[1], Is.EqualTo(25));

            var get = new GenTreeHWIntrinsic(TYP_INT, NI_Vector_GetElement, TYP_INT, 16,
                vector, new GenTreeLclVar(TYP_INT, 0));
            var outOfRange = store.EvalHWIntrinsicFunBinary(get, VNFunc.VNF_HWI_Vector_GetElement,
                firstVN, store.VNForIntCon(4), simdTypeVN);
            var app = new VNFuncApp();
            Assert.That(store.GetVNFunc(outOfRange, ref app), Is.True);
            Assert.That(app.Func, Is.EqualTo(VNFunc.VNF_HWI_Vector_GetElement));
            Assert.That(app.GetArg(1), Is.EqualTo(store.VNForIntCon(4)));
        });
    }

    [Test]
    public static void HardwareBinaryConvertsVectorShiftCountsAndFoldsMasks()
    {
        WithStore(store => {
            var vector = new GenTreeVecCon(TYP_SIMD16);
            var shiftTree = new GenTreeHWIntrinsic(TYP_SIMD16, NI_X86Base_ShiftLeftLogical,
                TYP_INT, 16, vector, vector);
            var first = default(simd16_t);
            first.i32[0] = 17;
            var shift = default(simd16_t);
            shift.u64[0] = 33;
            var result = store.EvalHWIntrinsicFunBinary(shiftTree, VNFunc.VNF_HWI_X86Base_ShiftLeftLogical,
                store.VNForSimd16Con(first), store.VNForSimd16Con(shift),
                store.VNForSimdType(16, TYP_INT));
            Assert.That(store.GetConstantSimd16(result).i32[0], Is.Zero);

#if FEATURE_MASKED_HW_INTRINSICS
            var mask = new GenTreeMskCon(default);
            var maskTree = new GenTreeHWIntrinsic(TYP_MASK, NI_AVX512_AndMask, TYP_INT, 16, mask, mask);
            var left = default(simdmask_t);
            left.u64[0] = 0b_1011;
            var right = default(simdmask_t);
            right.u64[0] = 0b_0110;
            var maskResult = store.EvalHWIntrinsicFunBinary(maskTree, VNFunc.VNF_HWI_AVX512_AndMask,
                store.VNForSimdMaskCon(left), store.VNForSimdMaskCon(right),
                store.VNForSimdType(16, TYP_INT));
            Assert.That(store.GetConstantSimdMask(maskResult).RawBits, Is.EqualTo(0b_0010L));
#endif
        });
    }

    [Test]
    public static void HardwareTernaryFoldsConditionalSelectionAndElementReplacement()
    {
        WithStore(store => {
            var vector = new GenTreeVecCon(TYP_SIMD16);
            var mask = default(simd16_t);
            mask.i32[0] = -1;
            var first = default(simd16_t);
            first.i32[0] = 9;
            var second = default(simd16_t);
            second.i32[0] = 18;
            second.i32[1] = 27;
            var maskVN = store.VNForSimd16Con(mask);
            var firstVN = store.VNForSimd16Con(first);
            var secondVN = store.VNForSimd16Con(second);
            var typeVN = store.VNForSimdType(16, TYP_INT);
            var select = new GenTreeHWIntrinsic(TYP_SIMD16, NI_Vector_ConditionalSelect, TYP_INT, 16,
                vector, vector, vector);
            var selected = store.EvalHWIntrinsicFunTernary(select, VNFunc.VNF_HWI_Vector_ConditionalSelect,
                maskVN, firstVN, secondVN, typeVN);
            Assert.That(store.GetConstantSimd16(selected).i32[0], Is.EqualTo(9));
            Assert.That(store.GetConstantSimd16(selected).i32[1], Is.EqualTo(27));

            var with = new GenTreeHWIntrinsic(TYP_SIMD16, NI_Vector_WithElement, TYP_INT, 16,
                vector, new GenTreeLclVar(TYP_INT, 0), new GenTreeLclVar(TYP_INT, 1));
            var replaced = store.EvalHWIntrinsicFunTernary(with, VNFunc.VNF_HWI_Vector_WithElement,
                secondVN, store.VNForIntCon(1), store.VNForIntCon(42), typeVN);
            Assert.That(store.GetConstantSimd16(replaced).i32[0], Is.EqualTo(18));
            Assert.That(store.GetConstantSimd16(replaced).i32[1], Is.EqualTo(42));

            var unknownMask = store.VNForExpr(null, TYP_SIMD16);
            var symbolic = store.EvalHWIntrinsicFunTernary(select, VNFunc.VNF_HWI_Vector_ConditionalSelect,
                unknownMask, firstVN, secondVN, typeVN);
            var app = new VNFuncApp();
            Assert.That(store.GetVNFunc(symbolic, ref app), Is.True);
            Assert.That(app.GetArg(0), Is.EqualTo(unknownMask));
            Assert.That(app.GetArg(1), Is.EqualTo(firstVN));
            Assert.That(app.GetArg(2), Is.EqualTo(secondVN));
            Assert.That(app.GetArg(3), Is.EqualTo(typeVN));
        });
    }

    private static void WithStore(Action<ValueNumStore> action, bool aot = false)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        if (aot)
        {
            flags.Set(JitFlags.JIT_FLAG_AOT);
        }
        compiler.opts.SetMinOpts(true);
        JitTls.Compiler = compiler;
        try
        {
            action(new ValueNumStore(compiler));
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
