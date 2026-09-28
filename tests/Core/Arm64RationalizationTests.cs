// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm64RationalizationTests
{
    [TestCase(TYP_UBYTE, 16, NI_AdvSimd_Arm64_MaxAcross)]
    [TestCase(TYP_UINT, 8, NI_AdvSimd_MaxPairwise)]
    public static void MaskZeroComparisonReducesAcrossLanes(
        var_types baseType, byte size, NamedIntrinsic expected)
    {
        WithRationalizer((compiler, rationalizer, block) => {
            var extract = ExtractMask(compiler, block, baseType, size);
            var zero = compiler.gtNewIconNode(TYP_INT, 0);
            var comparison = new GenTreeOp(GT_EQ, TYP_INT, extract, zero);
            block.InsertAtEnd(zero);
            block.InsertAtEnd(comparison);
            var parents = new Stack<GenTree>();
            parents.Push(comparison);
            parents.Push(extract);

            RewriteHWIntrinsic(rationalizer, ref comparison.Op1Ref, parents);

            Assert.That(comparison.Op1.Oper, Is.EqualTo(GT_CAST));
            Assert.That(extract.HWIntrinsicId, Is.EqualTo(NI_Vector_ToScalar));
            Assert.That(extract.GetOp(1).AsHWIntrinsic().HWIntrinsicId, Is.EqualTo(expected));
            Assert.That(parents.Peek(), Is.SameAs(comparison.Op1));
        });
    }

    [TestCase(NI_PRIMITIVE_PopCount, NI_AdvSimd_Arm64_AddAcross)]
    [TestCase(NI_PRIMITIVE_TrailingZeroCount, NI_AdvSimd_Arm64_MinAcross)]
    [TestCase(NI_PRIMITIVE_LeadingZeroCount, NI_AdvSimd_Arm64_MinAcross)]
    public static void MaskCountConsumersUseVectorReduction(NamedIntrinsic consumer, NamedIntrinsic expected)
    {
        WithRationalizer((compiler, rationalizer, block) => {
            var extract = ExtractMask(compiler, block, TYP_UBYTE, 16);
            GenTree root = new GenTreeIntrinsic(TYP_INT, extract, consumer, default);
            block.InsertAtEnd(root);
            var parents = new Stack<GenTree>();
            parents.Push(root);

            var changed = consumer is NI_PRIMITIVE_PopCount
                ? RewritePopCount(rationalizer, ref root, parents)
                : RewriteZeroCount(rationalizer, ref root, parents);

            Assert.That(changed, Is.True);
            Assert.That(extract.HWIntrinsicId, Is.EqualTo(NI_Vector_ToScalar));
            Assert.That(extract.GetOp(1).AsHWIntrinsic().HWIntrinsicId, Is.EqualTo(expected));
            Assert.That(root.Oper, Is.EqualTo(consumer is NI_PRIMITIVE_TrailingZeroCount ? GT_SUB : GT_CAST));
            Assert.That(parents.Peek(), Is.SameAs(root));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void MaskCountIsDeferredUntilItsScalarConsumer(bool hardwareLeadingCount)
    {
        WithRationalizer((compiler, rationalizer, block) => {
            var extract = ExtractMask(compiler, block, TYP_UINT, 8);
            GenTree root = hardwareLeadingCount
                ? new GenTreeHWIntrinsic(TYP_INT, NI_ArmBase_LeadingZeroCount, TYP_INT, 0, extract)
                : new GenTreeIntrinsic(TYP_INT, extract, NI_PRIMITIVE_PopCount, default);
            block.InsertAtEnd(root);
            var parents = new Stack<GenTree>();
            parents.Push(root);
            parents.Push(extract);

            ref var operand = ref (hardwareLeadingCount
                ? ref root.AsHWIntrinsic().GetOpRef(1)
                : ref root.AsIntrinsic().Op1Ref);
            RewriteHWIntrinsic(rationalizer, ref operand, parents);
            Assert.That(extract.HWIntrinsicId, Is.EqualTo(NI_Vector_ExtractMostSignificantBits));
            _ = parents.Pop();

            _ = RewriteNode(rationalizer, ref root, parents);
            Assert.That(root.Oper, Is.EqualTo(GT_CAST));
            Assert.That(extract.GetOp(1).AsHWIntrinsic().HWIntrinsicId,
                Is.EqualTo(hardwareLeadingCount ? NI_AdvSimd_MinPairwise : NI_AdvSimd_AddPairwise));
        });
    }

    [TestCase(TYP_BYTE, 16, TYP_USHORT)]
    [TestCase(TYP_INT, 8, TYP_INT)]
    [TestCase(TYP_LONG, 16, TYP_LONG)]
    public static void GeneralMsbExtractionUsesArm64ShiftAndSum(var_types baseType, byte size, var_types scalarType)
    {
        WithRationalizer((compiler, rationalizer, block) => {
            GenTree value = compiler.gtNewVconNode(Compiler.GetSimdTypeForSize(size));
            var extract = new GenTreeHWIntrinsic(TYP_INT, NI_Vector_ExtractMostSignificantBits,
                baseType, size, value);
            block.InsertAtEnd(value);
            block.InsertAtEnd(extract);
            GenTree root = extract;
            var parents = new Stack<GenTree>();
            parents.Push(extract);

            RewriteHWIntrinsic(rationalizer, ref root, parents);

            Assert.That(extract.HWIntrinsicId, Is.EqualTo(NI_Vector_ToScalar));
            Assert.That(extract.SimdBaseType, Is.EqualTo(scalarType));
            Assert.That(root, Is.SameAs(scalarType is TYP_INT ? extract : block.LastNode));
            Assert.That(extract.GetOp(1).Oper.IsHWIntrinsic, Is.True);
        });
    }

    [TestCase(3)]
    [TestCase(67)]
    public static void SignedDivisionShiftPatternBecomesRemainderAndClearsValueNumbers(int amount)
    {
        WithRationalizer((compiler, rationalizer, block) => {
            var left = compiler.gtNewLclvNode(TYP_INT, 0);
            var dividend = compiler.gtNewLclvNode(TYP_INT, 0);
            var divisor = compiler.gtNewIconNode(TYP_INT, 8);
            var division = new GenTreeOp(GT_DIV, TYP_INT, dividend, divisor);
            var shiftAmount = compiler.gtNewIconNode(TYP_INT, amount);
            var shift = new GenTreeOp(GT_LSH, TYP_INT, division, shiftAmount);
            GenTree root = new GenTreeOp(GT_SUB, TYP_INT, left, shift);
            root._vnPair.SetBoth(123);
            foreach (var node in new GenTree[] { left, dividend, divisor, division, shiftAmount, shift, root })
            {
                block.InsertAtEnd(node);
            }
            RewriteSubLshDiv(rationalizer, ref root);

            Assert.That(root.Oper, Is.EqualTo(GT_MOD));
            Assert.That(root._vnPair.BothDefined, Is.False);
            Assert.That(root.AsOp().Op2, Is.SameAs(divisor));
            GenTree[] expected = [left, divisor, root];
            Assert.That(new List<GenTree>(block), Is.EqualTo(expected));
        }, minopts: false);
    }

    [TestCase(-61L, true)]
    [TestCase(-1L, false)]
    [TestCase(4294967299L, true)]
    [TestCase(4294967296L, false)]
    public static void SignedDivisionShiftUsesMachineMaskedCountWithoutCheckedNarrowing(long amount, bool matches)
    {
        WithRationalizer((compiler, rationalizer, block) => {
            var left = compiler.gtNewLclvNode(TYP_LONG, 0);
            var dividend = compiler.gtNewLclvNode(TYP_LONG, 0);
            var divisor = compiler.gtNewIconNode(TYP_LONG, 8);
            var division = new GenTreeOp(GT_DIV, TYP_LONG, dividend, divisor);
            var shiftAmount = compiler.gtNewIconNode(TYP_LONG, checked((nint)amount));
            var shift = new GenTreeOp(GT_LSH, TYP_LONG, division, shiftAmount);
            GenTree root = new GenTreeOp(GT_SUB, TYP_LONG, left, shift);
            foreach (var node in new GenTree[] { left, dividend, divisor, division, shiftAmount, shift, root })
            {
                block.InsertAtEnd(node);
            }

            Assert.DoesNotThrow(() => RewriteSubLshDiv(rationalizer, ref root));
            Assert.That(root.Oper, Is.EqualTo(matches ? GT_MOD : GT_SUB));
            Assert.That(root.AsOp().Op2, Is.SameAs(matches ? divisor : shift));
        }, minopts: false);
    }

    [Test]
    public static void PhaseRewritesArm64DivisionPatternDuringHirTraversal()
    {
        WithRationalizer((compiler, rationalizer, block) => {
            compiler.fgFirstBB = compiler.fgLastBB = block;
            var left = compiler.gtNewLclvNode(TYP_INT, 0);
            var dividend = compiler.gtNewLclvNode(TYP_INT, 0);
            var divisor = compiler.gtNewIconNode(TYP_INT, 8);
            var division = new GenTreeOp(GT_DIV, TYP_INT, dividend, divisor);
            var shift = new GenTreeOp(GT_LSH, TYP_INT, division, compiler.gtNewIconNode(TYP_INT, 3));
            var subtract = new GenTreeOp(GT_SUB, TYP_INT, left, shift);
            var ret = new GenTreeUnOp(GT_RETURN, TYP_INT, subtract);
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(ret));
            _ = compiler.fgSetBlockOrder();
            compiler.mostRecentlyActivePhase = Phases.PHASE_RATIONALIZE;

            Assert.That(RunPhase(rationalizer), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(subtract.Oper, Is.EqualTo(GT_MOD));
            Assert.That(ret.Op1, Is.SameAs(subtract));
            Assert.That(block.IsLIR, Is.True);
            Assert.That(compiler.compRationalIRForm, Is.True);
        }, minopts: false, lir: false);
    }

    [Test]
    public static void PhaseRewritesComparisonMaskWithoutMaterializingBitmask()
    {
        WithRationalizer((compiler, rationalizer, block) => {
            compiler.fgFirstBB = compiler.fgLastBB = block;
            var vector = compiler.gtNewVconNode(TYP_SIMD16);
            vector.SimdVal.u64[0] = ulong.MaxValue;
            vector.SimdVal.u64[1] = ulong.MaxValue;
            var extract = new GenTreeHWIntrinsic(TYP_INT, NI_Vector_ExtractMostSignificantBits,
                TYP_UBYTE, 16, vector);
            var comparison = new GenTreeOp(GT_NE, TYP_INT, extract, compiler.gtNewIconNode(TYP_INT, 0));
            var ret = new GenTreeUnOp(GT_RETURN, TYP_INT, comparison);
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(ret));
            _ = compiler.fgSetBlockOrder();
            compiler.mostRecentlyActivePhase = Phases.PHASE_RATIONALIZE;

            Assert.That(RunPhase(rationalizer), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(comparison.Op1.Oper, Is.EqualTo(GT_CAST));
            Assert.That(extract.HWIntrinsicId, Is.EqualTo(NI_Vector_ToScalar));
            Assert.That(extract.GetOp(1).AsHWIntrinsic().HWIntrinsicId, Is.EqualTo(NI_AdvSimd_Arm64_MaxAcross));
            Assert.That(block.IsLIR, Is.True);
        }, lir: false);
    }

    [TestCase(NI_AdvSimd_Insert, 3, 1, -1)]
    [TestCase(NI_AdvSimd_Arm64_InsertSelectedScalar, 4, 2, 0)]
    [TestCase(NI_Sve_MultiplyAddRotateComplexBySelectedScalar, 5, 0, 1)]
    public static void ImmediatePositionsMatchNativeSignatureOrder(NamedIntrinsic id, int arguments, int first, int second)
    {
        HWIntrinsicInfo.GetImmOpsPositions(id, arguments, out var actualFirst, out var actualSecond);
        Assert.That((actualFirst, actualSecond), Is.EqualTo((first, second)));
    }

    [TestCase(NI_AdvSimd_ShiftLeftLogical, 16, TYP_INT, 1, 0, 31)]
    [TestCase(NI_AdvSimd_ShiftRightLogical, 16, TYP_SHORT, 1, 1, 16)]
    [TestCase(NI_Sve_MultiplyAddRotateComplexBySelectedScalar, 16, TYP_SHORT, 1, 0, 3)]
    [TestCase(NI_Sve_MultiplyAddRotateComplexBySelectedScalar, 16, TYP_SHORT, 2, 0, 1)]
    public static void ImmediateBoundsMatchTargetEncoding(NamedIntrinsic id, int size, var_types baseType,
        int number, int lower, int upper)
    {
        HWIntrinsicInfo.lookupImmBounds(id, size, baseType, number, out var actualLower, out var actualUpper);
        Assert.That((actualLower, actualUpper), Is.EqualTo((lower, upper)));
    }

    [TestCase(31, true)]
    [TestCase(32, false)]
    [TestCase(-1, false)]
    public static void UserCallImmediateStaysIntrinsicOnlyWithinArm64Bounds(int value, bool expected)
    {
        WithRationalizer((compiler, rationalizer, _) => {
            var vector = compiler.gtNewVconNode(TYP_SIMD16);
            var immediate = compiler.gtNewIconNode(TYP_INT, value);
            var intrinsic = new GenTreeHWIntrinsic(TYP_SIMD16, NI_AdvSimd_ShiftLeftLogical,
                TYP_INT, 16, vector, immediate);
            CORINFO_SIG_INFO signature = default;
            signature.numArgs = 2;

            Assert.That(CanKeepImmediate(rationalizer, intrinsic, in signature), Is.EqualTo(expected));
        });
    }

    [TestCase(16, 31, true)]
    [TestCase(0, 31, false)]
    [TestCase(16, 32, false)]
    public static void TwoImmediateIntrinsicRequiresBothBounds(int count, int pattern, bool expected)
    {
        WithRationalizer((compiler, rationalizer, _) => {
            var vector = compiler.gtNewVconNode(TYP_SIMD16);
            var countNode = compiler.gtNewIconNode(TYP_INT, count);
            var patternNode = compiler.gtNewIconNode(TYP_INT, pattern);
            var intrinsic = new GenTreeHWIntrinsic(TYP_SIMD16, NI_Sve_SaturatingIncrementBy16BitElementCount,
                TYP_INT, 16, vector, countNode, patternNode);
            CORINFO_SIG_INFO signature = default;
            signature.numArgs = 3;

            Assert.That(CanKeepImmediate(rationalizer, intrinsic, in signature), Is.EqualTo(expected));
        });
    }

    private static GenTreeHWIntrinsic ExtractMask(Compiler compiler, BasicBlock block, var_types baseType, byte size)
    {
        var type = Compiler.GetSimdTypeForSize(size);
        var mask = compiler.gtNewVconNode(type);
        mask.SimdVal.u64[0] = ulong.MaxValue;
        if (size == 16)
        {
            mask.SimdVal.u64[1] = ulong.MaxValue;
        }
        var extract = new GenTreeHWIntrinsic(TYP_INT, NI_Vector_ExtractMostSignificantBits,
            baseType, size, mask);
        block.InsertAtEnd(mask);
        block.InsertAtEnd(extract);
        return extract;
    }

    private static void WithRationalizer(Action<Compiler, Rationalizer, BasicBlock> action,
        bool minopts = true, bool lir = true)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(minopts);
        compiler.lvaTable = new LclVarDsc[4];
        compiler.lvaCount = 1;
        compiler.lvaTable[0].Type = TYP_INT;
        compiler.compHndBBtab = [];
        compiler.fgNodeThreading = lir ? NodeThreading.LIR : NodeThreading.AllTrees;
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.info.compFullName = nameof(Arm64RationalizationTests);
#endif
        JitTls.Compiler = compiler;
        try
        {
            compiler.codeGen = new CodeGen(compiler);
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            if (lir)
            {
                block.MakeLir(null, null);
            }
            var rationalizer = new Rationalizer(compiler);
            CurrentBlock(rationalizer) = block;
            action(compiler, rationalizer, block);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? CurrentBlock(Rationalizer rationalizer);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "RewriteHWIntrinsic")]
    private static extern void RewriteHWIntrinsic(Rationalizer rationalizer, ref GenTree use, Stack<GenTree> parents);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "RewriteHWIntrinsicCmpMaskExtractMsbPopCount")]
    private static extern bool RewritePopCount(Rationalizer rationalizer, ref GenTree use, Stack<GenTree> parents);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "RewriteHWIntrinsicCmpMaskExtractMsbZeroCount")]
    private static extern bool RewriteZeroCount(Rationalizer rationalizer, ref GenTree use, Stack<GenTree> parents);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "RewriteSubLshDiv")]
    private static extern void RewriteSubLshDiv(Rationalizer rationalizer, ref GenTree use);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "CanKeepHWIntrinsicImmediate")]
    private static extern bool CanKeepImmediate(Rationalizer rationalizer, GenTreeHWIntrinsic node,
        in CORINFO_SIG_INFO signature);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "RewriteNode")]
    private static extern Compiler.fgWalkResult RewriteNode(Rationalizer rationalizer,
        ref GenTree use, Stack<GenTree> parents);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "DoPhase")]
    private static extern PhaseStatus RunPhase(Rationalizer rationalizer);
}
#endif
