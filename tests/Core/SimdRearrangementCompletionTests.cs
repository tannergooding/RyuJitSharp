// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if FEATURE_HW_INTRINSICS
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
#if TARGET_XARCH
using static RyuJitSharp.CORINFO_InstructionSet;
#endif
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
#if TARGET_XARCH || TARGET_ARM64
using static RyuJitSharp.genTreeOps;
#endif
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class SimdRearrangementCompletionTests
{
#if TARGET_XARCH
    [TestCase(TYP_INT, false, 0x88)]
    [TestCase(TYP_UINT, true, 0xDD)]
    [TestCase(TYP_FLOAT, false, 0x88)]
    [TestCase(TYP_FLOAT, true, 0xDD)]
    public static void Unzip128UsesNativeFourByteLaneControl(var_types baseType, bool odd, int control)
    {
        WithCompiler(compiler =>
        {
            var left = Local(compiler, TYP_SIMD16);
            var right = Local(compiler, TYP_SIMD16);
            var result = compiler.gtNewSimdUnzipNode(TYP_SIMD16, left, right, baseType, 16, odd).AsHWIntrinsic();
            Assert.That(result.HWIntrinsicId, Is.EqualTo(NI_X86Base_Shuffle));
            Assert.That(result.SimdBaseType, Is.EqualTo(TYP_FLOAT));
            Assert.That(result.GetOp(1), Is.SameAs(left));
            Assert.That(result.GetOp(2), Is.SameAs(right));
            Assert.That(result.GetOp(3).AsIntCon().IconValue, Is.EqualTo((nint)control));
        });
    }

    [TestCase((byte)32, TYP_BYTE, false, NI_AVX512v2_PermuteVar32x8x2)]
    [TestCase((byte)32, TYP_USHORT, true, NI_AVX512_PermuteVar16x16x2)]
    [TestCase((byte)32, TYP_INT, false, NI_AVX512_PermuteVar8x32x2)]
    [TestCase((byte)32, TYP_DOUBLE, true, NI_AVX512_PermuteVar4x64x2)]
    [TestCase((byte)64, TYP_UBYTE, true, NI_AVX512v2_PermuteVar64x8x2)]
    [TestCase((byte)64, TYP_SHORT, false, NI_AVX512_PermuteVar32x16x2)]
    [TestCase((byte)64, TYP_FLOAT, true, NI_AVX512_PermuteVar16x32x2)]
    [TestCase((byte)64, TYP_ULONG, false, NI_AVX512_PermuteVar8x64x2)]
    public static void WideUnzipPreservesEverySelectorAndBothOperandIdentities(
        byte size, var_types baseType, bool odd, NamedIntrinsic intrinsic)
    {
        WithCompiler(compiler =>
        {
            Enable(compiler, InstructionSet_AVX512);
            Enable(compiler, InstructionSet_AVX512v2);
            var type = Compiler.GetSimdTypeForSize(size);
            var left = Local(compiler, type);
            var right = Local(compiler, type);
            var result = compiler.gtNewSimdUnzipNode(type, left, right, baseType, size, odd).AsHWIntrinsic();
            Assert.That(result.HWIntrinsicId, Is.EqualTo(intrinsic));
            Assert.That(result.GetOp(1), Is.SameAs(left));
            Assert.That(result.GetOp(3), Is.SameAs(right));
            var count = size / baseType.Size;
            for (var index = 0; index < count; index++)
            {
                var expected = index < (count / 2)
                    ? (odd ? 1 : 0) + (2 * index)
                    : count + (odd ? 1 : 0) + (2 * (index - (count / 2)));
                Assert.That(result.GetOp(2).GetIntegralVectorConstElement(index, baseType),
                    Is.EqualTo(unchecked((ulong)expected)));
            }
        });
    }

    [TestCase(false, 0x88)]
    [TestCase(true, 0xDD)]
    public static void RecursiveUnzipExtractsEachSourceBeforeCombiningItsSelectedLanes(bool odd, int control)
    {
        WithCompiler(compiler =>
        {
            Enable(compiler, InstructionSet_AVX2);
            var left = Local(compiler, TYP_SIMD32);
            var right = Local(compiler, TYP_SIMD32);
            var result = compiler.gtNewSimdUnzipNode(TYP_SIMD32, left, right, TYP_INT, 32, odd).AsHWIntrinsic();
            Assert.That(result.HWIntrinsicId, Is.EqualTo(NI_Vector_WithUpper));
            var lower = result.GetOp(1).AsHWIntrinsic().GetOp(1).AsHWIntrinsic();
            var upper = result.GetOp(2).AsHWIntrinsic();
            foreach (var half in new[] { lower, upper })
            {
                Assert.That(half.HWIntrinsicId, Is.EqualTo(NI_X86Base_Shuffle));
                Assert.That(half.GetOp(1).AsHWIntrinsic().HWIntrinsicId, Is.EqualTo(NI_Vector_GetLower));
                Assert.That(half.GetOp(2).AsHWIntrinsic().HWIntrinsicId, Is.EqualTo(NI_Vector_GetUpper));
                Assert.That(half.GetOp(3).AsIntCon().IconValue, Is.EqualTo((nint)control));
            }
            Assert.That(lower.GetOp(1).AsHWIntrinsic().GetOp(1), Is.SameAs(left));
            Assert.That(upper.GetOp(1).AsHWIntrinsic().GetOp(1), Is.SameAs(right));
            Assert.That(lower.GetOp(2).AsHWIntrinsic().GetOp(1), Is.Not.SameAs(left));
            Assert.That(lower.GetOp(2).AsHWIntrinsic().GetOp(1).AsLclVar().LclNum, Is.EqualTo(left.LclNum));
        });
    }

    [TestCase(TYP_INT, NI_X86Base_Shuffle)]
    [TestCase(TYP_UINT, NI_X86Base_Shuffle)]
    [TestCase(TYP_FLOAT, NI_X86Base_Shuffle)]
    [TestCase(TYP_LONG, NI_X86Base_Shuffle)]
    [TestCase(TYP_DOUBLE, NI_X86Base_Shuffle)]
    public static void Reverse128KeepsNativeLanePermutationAndSourceEvaluation(var_types baseType,
        NamedIntrinsic intrinsic)
    {
        WithCompiler(compiler =>
        {
            var source = Local(compiler, TYP_SIMD16);
            var result = compiler.gtNewSimdReverseNode(TYP_SIMD16, source, baseType, 16).AsHWIntrinsic();
            Assert.That(result.HWIntrinsicId, Is.EqualTo(intrinsic));
            var control = baseType.Size == 4 ? 0x1B : 1;
            Assert.That(result.GetOp(result.Operands.Length).AsIntCon().IconValue, Is.EqualTo((nint)control));
            Assert.That(CountReferences(result, source), Is.EqualTo(1));
        });
    }
#endif

#if TARGET_XARCH || TARGET_ARM64
    [TestCase(false)]
    [TestCase(true)]
    public static void SingleLaneUnzipSequencesBothEffectsAndReturnsOnlyTheRequestedValue(bool odd)
    {
        WithCompiler(compiler =>
        {
            var leftEffect = compiler.gtNewStoreLclVarNode(Local(compiler, TYP_SIMD8).LclNum,
                compiler.gtNewVconNode(TYP_SIMD8));
            var leftLocal = Local(compiler, TYP_SIMD8);
            var left = compiler.gtNewBinaryNode(GT_COMMA, TYP_SIMD8, leftEffect, leftLocal);
            var right = compiler.gtNewStoreLclVarNode(Local(compiler, TYP_SIMD8).LclNum,
                compiler.gtNewVconNode(TYP_SIMD8));
            var result = compiler.gtNewSimdUnzipNode(TYP_SIMD8, left, right, TYP_LONG, 8, odd);
            Assert.That(result.Oper, Is.EqualTo(GT_COMMA));
            Assert.That(CountReferences(result, leftEffect), Is.EqualTo(1));
            Assert.That(CountReferences(result, right), Is.EqualTo(1));
            if (!odd)
            {
                var capture = result.AsOp().Op1.AsOp();
                Assert.That(capture.Oper, Is.EqualTo(GT_COMMA));
                Assert.That(capture.Op1, Is.SameAs(left));
                var hoisted = capture.Op1.AsOp();
                Assert.That(hoisted.Oper, Is.EqualTo(GT_COMMA));
                Assert.That(hoisted.Op1, Is.SameAs(leftEffect));
                Assert.That(hoisted.Op2.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
                Assert.That(capture.Op2.Oper, Is.EqualTo(GT_LCL_VAR));
                var store = hoisted.Op2.AsLclVarCommon();
                Assert.That(store.Data, Is.SameAs(leftLocal));
                Assert.That(CountReferences(result, leftLocal), Is.EqualTo(1));
                Assert.That(capture.Op2.AsLclVarCommon().LclNum, Is.EqualTo(store.LclNum));
                var effects = result.AsOp().Op2.AsOp();
                Assert.That(effects.Oper, Is.EqualTo(GT_COMMA));
                Assert.That(effects.Op1, Is.SameAs(right));
                Assert.That(effects.Op2.Oper, Is.EqualTo(GT_LCL_VAR));
                Assert.That(effects.Op2.AsLclVarCommon().LclNum, Is.EqualTo(store.LclNum));
                Assert.That(effects.Op2, Is.Not.SameAs(capture.Op2));
            }
            else
            {
                Assert.That(result.AsOp().Op1, Is.SameAs(leftEffect));
                var effects = result.AsOp().Op2.AsOp();
                Assert.That(effects.Op1, Is.SameAs(right));
                Assert.That(effects.Op2.IsVectorZero, Is.True);
            }
        });
    }

    [Test]
    public static void SingleLaneReverseReturnsTheOriginalOperandEvenWithEffects()
    {
        WithCompiler(compiler =>
        {
            var source = compiler.gtNewBinaryNode(GT_COMMA, TYP_SIMD8,
                compiler.gtNewStoreLclVarNode(Local(compiler, TYP_SIMD8).LclNum, compiler.gtNewVconNode(TYP_SIMD8)),
                Local(compiler, TYP_SIMD8));
            Assert.That(compiler.gtNewSimdReverseNode(TYP_SIMD8, source, TYP_LONG, 8), Is.SameAs(source));
        });
    }
#endif

#if TARGET_ARM64
    [TestCase(false, NI_AdvSimd_Arm64_UnzipEven)]
    [TestCase(true, NI_AdvSimd_Arm64_UnzipOdd)]
    public static void Arm64UnzipUsesNativeHalfSelection(bool odd, NamedIntrinsic intrinsic)
    {
        WithCompiler(compiler =>
        {
            var left = Local(compiler, TYP_SIMD16);
            var right = Local(compiler, TYP_SIMD16);
            var result = compiler.gtNewSimdUnzipNode(TYP_SIMD16, left, right, TYP_SHORT, 16, odd).AsHWIntrinsic();
            Assert.That(result.HWIntrinsicId, Is.EqualTo(intrinsic));
            Assert.That(result.GetOp(1), Is.SameAs(left));
            Assert.That(result.GetOp(2), Is.SameAs(right));
        });
    }

    [TestCase(TYP_INT)]
    [TestCase(TYP_UINT)]
    [TestCase(TYP_FLOAT)]
    public static void Arm64EightByteReverseUsesNativeInt64Reversal(var_types baseType)
    {
        WithCompiler(compiler =>
        {
            var source = Local(compiler, TYP_SIMD8);
            var result = compiler.gtNewSimdReverseNode(TYP_SIMD8, source, baseType, 8).AsHWIntrinsic();
            Assert.That(result.HWIntrinsicId, Is.EqualTo(NI_AdvSimd_ReverseElement32));
            Assert.That(result.SimdBaseType, Is.EqualTo(TYP_LONG));
            Assert.That(result.GetOp(1), Is.SameAs(source));
        });
    }

    [Test]
    public static void Arm64GeneralReverseUsesTheNativeByteLookup()
    {
        WithCompiler(compiler =>
        {
            var source = Local(compiler, TYP_SIMD16);
            var result = compiler.gtNewSimdReverseNode(TYP_SIMD16, source, TYP_INT, 16).AsHWIntrinsic();
            Assert.That(result.HWIntrinsicId, Is.EqualTo(NI_AdvSimd_Arm64_VectorTableLookup));
            Assert.That(result.SimdBaseType, Is.EqualTo(TYP_BYTE));
            Assert.That(result.GetOp(1), Is.SameAs(source));
            for (var index = 0; index < 16; index++)
            {
                var expected = ((3 - (index / 4)) * 4) + (index % 4);
                Assert.That(result.GetOp(2).AsVecCon().SimdVal.u8[index], Is.EqualTo((byte)expected));
            }
        });
    }
#endif

#if TARGET_WASM
    [TestCase(false)]
    [TestCase(true)]
    public static void WasmUnzipComposesTheExactEvenOrOddByteSelectors(bool odd)
    {
        WithCompiler(compiler =>
        {
            var left = Local(compiler, TYP_SIMD16);
            var right = Local(compiler, TYP_SIMD16);
            var result = compiler.gtNewSimdUnzipNode(TYP_SIMD16, left, right, TYP_SHORT, 16, odd)
                .AsHWIntrinsic();
            Assert.That(result.HWIntrinsicId, Is.EqualTo(NI_PackedSimd_Or));
            Assert.That(result.SimdBaseType, Is.EqualTo(TYP_SHORT));
            var first = result.GetOp(1).AsHWIntrinsic();
            var second = result.GetOp(2).AsHWIntrinsic();
            Assert.That(first.HWIntrinsicId, Is.EqualTo(NI_PackedSimd_Swizzle));
            Assert.That(second.HWIntrinsicId, Is.EqualTo(NI_PackedSimd_Swizzle));
            Assert.That(first.GetOp(1), Is.SameAs(left));
            Assert.That(second.GetOp(1), Is.SameAs(right));
            for (var index = 0; index < 16; index++)
            {
                var selector = (byte)(((index % 8) / 2 * 4) + (odd ? 2 : 0) + (index % 2));
                Assert.That(first.GetOp(2).AsVecCon().SimdVal.u8[index],
                    Is.EqualTo(index < 8 ? selector : (byte)0xFF));
                Assert.That(second.GetOp(2).AsVecCon().SimdVal.u8[index],
                    Is.EqualTo(index >= 8 ? selector : (byte)0xFF));
            }
        });
    }

    [Test]
    public static void WasmReverseUsesTheNativeByteSwizzle()
    {
        WithCompiler(compiler =>
        {
            var source = Local(compiler, TYP_SIMD16);
            var result = compiler.gtNewSimdReverseNode(TYP_SIMD16, source, TYP_INT, 16).AsHWIntrinsic();
            Assert.That(result.HWIntrinsicId, Is.EqualTo(NI_PackedSimd_Swizzle));
            Assert.That(result.SimdBaseType, Is.EqualTo(TYP_BYTE));
            Assert.That(result.GetOp(1), Is.SameAs(source));
            for (var index = 0; index < 16; index++)
            {
                var expected = ((3 - (index / 4)) * 4) + (index % 4);
                Assert.That(result.GetOp(2).AsVecCon().SimdVal.u8[index], Is.EqualTo((byte)expected));
            }
        });
    }
#endif

#if TARGET_XARCH || TARGET_ARM64
    private static int CountReferences(GenTree tree, GenTree value)
    {
        var count = ReferenceEquals(tree, value) ? 1 : 0;
        foreach (var operand in tree.Operands)
        {
            count += CountReferences(operand, value);
        }

        return count;
    }
#endif

    private static GenTreeLclVar Local(Compiler compiler, var_types type)
    {
        var index = compiler.lvaCount++;
        compiler.lvaTable[index] = new LclVarDsc { Type = type };

        return compiler.gtNewLclvNode(type, index);
    }

#if TARGET_XARCH
    private static void Enable(Compiler compiler, CORINFO_InstructionSet instructionSet)
    {
        compiler.opts.compSupportsISA.AddInstructionSet(instructionSet);
        compiler.opts.compSupportsISAReported.AddInstructionSet(instructionSet);
        compiler.opts.compSupportsISAExactly.AddInstructionSet(instructionSet);
    }
#endif

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
#endif
