// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if FEATURE_HW_INTRINSICS && TARGET_XARCH
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class HardwareComparisonLoweringTests
{
    [TestCase(false, GT_EQ, GenCondition.EQ)]
    [TestCase(false, GT_NE, GenCondition.NE)]
    [TestCase(true, GT_EQ, GenCondition.C)]
    [TestCase(true, GT_NE, GenCondition.NC)]
    public static void IntegerZeroAndAllBitsSetUsePtestAndCorrectCondition(
        bool allBitsSet, genTreeOps operation, GenCondition.CodeKind condition)
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = TYP_SIMD16;
            var value = compiler.gtNewLclvNode(TYP_SIMD16, 0);
            var constant = new GenTreeVecCon(TYP_SIMD16);
            if (allBitsSet)
            {
                constant.SimdVal.i32[0] = -1;
                constant.SimdVal.i32[1] = -1;
                constant.SimdVal.i32[2] = -1;
                constant.SimdVal.i32[3] = -1;
            }
            var comparison = new GenTreeHWIntrinsic(TYP_INT,
                operation is GT_EQ ? NI_Vector_op_Equality : NI_Vector_op_Inequality,
                TYP_INT, 16, value, constant);
            var consumer = new GenTreeUnOp(GT_NEG, TYP_INT, comparison);
            var block = NewBlock(value, constant, comparison, consumer);

            _ = LowerHWIntrinsicCmpOp(NewLowering(compiler, block), comparison, operation);

            Assert.That(comparison.HWIntrinsicId, Is.EqualTo(NI_X86Base_PTEST));
            Assert.That(comparison.Type, Is.EqualTo(TYP_VOID));
            Assert.That(comparison.Flags.HasFlag(GenTreeFlags.GTF_SET_FLAGS), Is.True);
            Assert.That(consumer.Op1.AsCC().Condition.Code, Is.EqualTo(condition));
            if (allBitsSet)
            {
                Assert.That(comparison.GetOp(2), Is.SameAs(constant));
            }
            else
            {
                Assert.That(constant.Next, Is.Null);
                Assert.That(comparison.GetOp(1).Oper, Is.EqualTo(GT_LCL_VAR));
                Assert.That(comparison.GetOp(2).Oper, Is.EqualTo(GT_LCL_VAR));
            }
        });
    }

    [TestCase(TYP_INT, 0xFFFF, GT_EQ)]
    [TestCase(TYP_INT, 0xFFFF, GT_NE)]
    [TestCase(TYP_FLOAT, 0xF, GT_EQ)]
    [TestCase(TYP_FLOAT, 0xF, GT_NE)]
    public static void LegacyCompareReplacesWholeNodeAndPreservesOwningUse(
        var_types baseType, int expectedMask, genTreeOps operation)
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = TYP_SIMD16;
            compiler.lvaTable[1].Type = TYP_SIMD16;
            var first = compiler.gtNewLclvNode(TYP_SIMD16, 0);
            var second = compiler.gtNewLclvNode(TYP_SIMD16, 1);
            var comparison = new GenTreeHWIntrinsic(TYP_INT,
                operation is GT_EQ ? NI_Vector_op_Equality : NI_Vector_op_Inequality,
                baseType, 16, first, second);
            var consumer = new GenTreeUnOp(GT_NEG, TYP_INT, comparison);
            var block = NewBlock(first, second, comparison, consumer);

            _ = LowerHWIntrinsicCmpOp(NewLowering(compiler, block), comparison, operation);

            Assert.That(comparison.Next, Is.Null);
            Assert.That(comparison.Prev, Is.Null);
            var cc = consumer.Op1.AsCC();
            Assert.That(cc.Condition.Code, Is.EqualTo(operation is GT_EQ ? GenCondition.EQ : GenCondition.NE));
            var rewrittenComparison = cc.Prev!.AsOp();
            Assert.That(rewrittenComparison.Oper, Is.EqualTo(operation));
            Assert.That(rewrittenComparison.Type, Is.EqualTo(TYP_VOID));
            Assert.That(rewrittenComparison.Op1.AsHWIntrinsic().HWIntrinsicId,
                Is.EqualTo(NI_X86Base_MoveMask));
            Assert.That(rewrittenComparison.Op2.AsIntConCommon().IconValue,
                Is.EqualTo((nint)expectedMask));
            Assert.That(rewrittenComparison.Op1.AsHWIntrinsic().GetOp(1).AsHWIntrinsic().HWIntrinsicId,
                Is.EqualTo(NI_X86Base_CompareEqual));
        }, supportsAvx512: false);
    }

    [TestCase(TYP_INT, GT_EQ, NI_AVX512_CompareNotEqualMask, GenCondition.EQ)]
    [TestCase(TYP_INT, GT_NE, NI_AVX512_CompareNotEqualMask, GenCondition.NE)]
    [TestCase(TYP_BYTE, GT_EQ, NI_AVX512_CompareEqualMask, GenCondition.C)]
    [TestCase(TYP_BYTE, GT_NE, NI_AVX512_CompareNotEqualMask, GenCondition.NE)]
    public static void EvexComparisonUsesPartialOrFullMaskCondition(
        var_types baseType, genTreeOps operation, NamedIntrinsic expectedIntrinsic,
        GenCondition.CodeKind expectedCondition)
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = TYP_SIMD16;
            compiler.lvaTable[1].Type = TYP_SIMD16;
            var first = compiler.gtNewLclvNode(TYP_SIMD16, 0);
            var second = compiler.gtNewLclvNode(TYP_SIMD16, 1);
            var comparison = new GenTreeHWIntrinsic(TYP_INT,
                operation is GT_EQ ? NI_Vector_op_Equality : NI_Vector_op_Inequality,
                baseType, 16, first, second);
            var consumer = new GenTreeUnOp(GT_NEG, TYP_INT, comparison);
            var block = NewBlock(first, second, comparison, consumer);

            _ = LowerHWIntrinsicCmpOp(NewLowering(compiler, block), comparison, operation);

            Assert.That(comparison.HWIntrinsicId, Is.EqualTo(expectedIntrinsic));
            Assert.That(comparison.Type, Is.EqualTo(TYP_MASK));
            var cc = consumer.Op1.AsCC();
            Assert.That(cc.Condition.Code, Is.EqualTo(expectedCondition));
            var kortest = cc.Prev!.AsHWIntrinsic();
            Assert.That(kortest.HWIntrinsicId, Is.EqualTo(NI_AVX512_KORTEST));
            Assert.That(kortest.GetOp(1), Is.SameAs(comparison));
        });
    }

    [TestCase(TYP_INT, 16, false, GT_EQ, 0x3UL, 0x3UL, GenCondition.EQ)]
    [TestCase(TYP_INT, 16, false, GT_NE, 0x3UL, 0x3UL, GenCondition.NE)]
    [TestCase(TYP_INT, 16, true, GT_EQ, 0x3UL, 0xCUL, GenCondition.EQ)]
    [TestCase(TYP_INT, 16, true, GT_NE, 0x3UL, 0xCUL, GenCondition.NE)]
    [TestCase(TYP_BYTE, 16, true, GT_EQ, 0x5UL, 0x5UL, GenCondition.C)]
    [TestCase(TYP_BYTE, 16, true, GT_NE, 0x5UL, 0x5UL, GenCondition.NC)]
    public static void EvexMaskComparisonReusesMaskAndPreservesItsCondition(
        var_types baseType, byte size, bool allBitsSet, genTreeOps cmpOp,
        ulong originalMask, ulong expectedMask, GenCondition.CodeKind expectedCondition)
    {
        WithCompiler(compiler => {
            simdmask_t maskBits = default;
            maskBits.u64[0] = originalMask;
            var mask = new GenTreeMskCon(maskBits);
            var conversion = new GenTreeHWIntrinsic(TYP_SIMD16, NI_AVX512_ConvertMaskToVector,
                baseType, size, mask);
            var constant = new GenTreeVecCon(TYP_SIMD16);
            if (allBitsSet)
            {
                constant.SimdVal.i32[0] = -1;
                constant.SimdVal.i32[1] = -1;
                constant.SimdVal.i32[2] = -1;
                constant.SimdVal.i32[3] = -1;
            }
            var comparison = new GenTreeHWIntrinsic(TYP_INT, NI_Vector_op_Equality,
                baseType, size, conversion, constant);
            var consumer = new GenTreeUnOp(GT_NEG, TYP_INT, comparison);
            var block = NewBlock(mask, conversion, constant, comparison, consumer);
            var lowering = NewLowering(compiler, block);

            _ = LowerHWIntrinsicCmpOpEvex(lowering, comparison, cmpOp, baseType,
                baseType, TYP_SIMD16, size, conversion, mask, constant,
                new GenCondition(cmpOp is GT_EQ ? GenCondition.EQ : GenCondition.NE));

            Assert.That(mask.SimdMaskVal.u64[0], Is.EqualTo(expectedMask));
            Assert.That(conversion.Next, Is.Null);
            Assert.That(constant.Next, Is.Null);
            Assert.That(comparison.Next, Is.Null);
            var cc = consumer.Op1.AsCC();
            Assert.That(cc.Oper, Is.EqualTo(GT_SETCC));
            Assert.That(cc.Condition.Code, Is.EqualTo(expectedCondition));
            var kortest = cc.Prev!.AsHWIntrinsic();
            Assert.That(kortest.HWIntrinsicId, Is.EqualTo(NI_AVX512_KORTEST));
            Assert.That(kortest.GetOp(1), Is.SameAs(mask));
            Assert.That(kortest.Type, Is.EqualTo(TYP_VOID));
        });
    }

    private static BasicBlock NewBlock(params GenTree[] nodes)
    {
        var block = new BasicBlock(null, null);
        block.MakeLir(null, null);
        foreach (var node in nodes)
        {
            block.InsertAtEnd(node);
        }
        return block;
    }

    private static Lowering NewLowering(Compiler compiler, BasicBlock block)
    {
        var lowering = new Lowering(compiler, new LinearScan(compiler));
        LoweringBlock(lowering) = block;
        return lowering;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? LoweringBlock(Lowering lowering);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerHWIntrinsicCmpOpEvex")]
    private static extern GenTree? LowerHWIntrinsicCmpOpEvex(Lowering lowering,
        GenTreeHWIntrinsic node, genTreeOps cmpOp, var_types baseType, var_types maskBaseType,
        var_types simdType, byte size, GenTree first, GenTree firstMask, GenTree? second,
        GenCondition condition);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerHWIntrinsicCmpOp")]
    private static extern GenTree? LowerHWIntrinsicCmpOp(
        Lowering lowering, GenTreeHWIntrinsic node, genTreeOps operation);

    private static void WithCompiler(Action<Compiler> action, bool supportsAvx512 = true)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        if (supportsAvx512)
        {
            compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_AVX512);
            compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_AVX512);
            compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_AVX512);
        }
        compiler.fgNodeThreading = NodeThreading.LIR;
        compiler.lvaTable = [new LclVarDsc(), new LclVarDsc()];
        compiler.lvaCount = 2;
        JitTls.Compiler = compiler;
        compiler.codeGen = new CodeGen(compiler);
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
#endif
