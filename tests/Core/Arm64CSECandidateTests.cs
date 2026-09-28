// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64 && FEATURE_HW_INTRINSICS
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.HWIntrinsicCategory;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class Arm64CSECandidateTests
{
    [TestCase(NI_AdvSimd_Add, HW_Category_SIMD, true)]
    [TestCase(NI_AdvSimd_MultiplyBySelectedScalar, HW_Category_SIMDByIndexedElement, true)]
    [TestCase(NI_AdvSimd_ShiftLeftLogical, HW_Category_ShiftLeftByImmediate, true)]
    [TestCase(NI_AdvSimd_ShiftRightLogical, HW_Category_ShiftRightByImmediate, true)]
    [TestCase(NI_ArmBase_LeadingZeroCount, HW_Category_Scalar, true)]
    [TestCase(NI_Vector_Create, HW_Category_Helper, true)]
    [TestCase(NI_AdvSimd_LoadAndInsertScalar, HW_Category_MemoryLoad, false)]
    [TestCase(NI_AdvSimd_StoreSelectedScalar, HW_Category_MemoryStore, false)]
    [TestCase(NI_ArmBase_Yield, HW_Category_Special, false)]
    public static void IntrinsicCategoriesFollowNativeEligibility(
        NamedIntrinsic intrinsic, HWIntrinsicCategory category, bool expected)
    {
        CSELiveAcrossCallCostTests.WithCompiler(compiler =>
        {
            var tree = Candidate(compiler, intrinsic);
            var heuristic = new CSE_Heuristic(compiler);

            Assert.That(HWIntrinsicInfo.lookupCategory(intrinsic), Is.EqualTo(category));
            Assert.That(heuristic.CanConsiderTree(tree, isReturn: false), Is.EqualTo(expected));
        });
    }

    [TestCase((GenTreeFlags)0, 0u, false, false)]
    [TestCase(GTF_DONT_CSE, 5u, false, false)]
    [TestCase(GTF_ASG, 5u, false, false)]
    [TestCase((GenTreeFlags)0, 5u, true, false)]
    [TestCase((GenTreeFlags)0, 5u, false, true)]
    public static void AdmittedIntrinsicsRetainCostSideEffectAndValueNumberExclusions(
        GenTreeFlags flags, uint cost, bool constant, bool expected)
    {
        CSELiveAcrossCallCostTests.WithCompiler(compiler =>
        {
            var tree = Candidate(compiler, NI_ArmBase_LeadingZeroCount);
            tree.Flags |= flags;
            tree.SetCosts(cost, cost);
            if (constant)
            {
                var store = compiler.vnStore ?? throw new AssertionException("Missing fixture value-number store.");
                tree._vnPair.Conservative = store.VNForIntCon(1);
            }

            var heuristic = new CSE_Heuristic(compiler);

            Assert.That(heuristic.CanConsiderTree(tree, isReturn: false), Is.EqualTo(expected));
        });
    }

    private static GenTreeHWIntrinsic Candidate(Compiler compiler, NamedIntrinsic intrinsic)
    {
        compiler.lvaTable = [
            new LclVarDsc { Type = TYP_SIMD16 },
            new LclVarDsc { Type = TYP_INT },
            new LclVarDsc { Type = TYP_BYREF },
        ];
        compiler.lvaCount = 3;
        var vector = compiler.gtNewLclvNode(TYP_SIMD16, 0);
        var scalar = compiler.gtNewLclvNode(TYP_INT, 1);
        var address = compiler.gtNewLclvNode(TYP_BYREF, 2);
        var index = compiler.gtNewIconNode(TYP_INT, 1);
        GenTree[] operands = intrinsic switch
        {
            NI_AdvSimd_Add => [vector, compiler.gtNewLclvNode(TYP_SIMD16, 0)],
            NI_AdvSimd_MultiplyBySelectedScalar => [vector, compiler.gtNewLclvNode(TYP_SIMD16, 0), index],
            NI_AdvSimd_ShiftLeftLogical or NI_AdvSimd_ShiftRightLogical => [vector, index],
            NI_ArmBase_LeadingZeroCount or NI_Vector_Create => [scalar],
            NI_AdvSimd_LoadAndInsertScalar => [vector, index, address],
            NI_AdvSimd_StoreSelectedScalar => [address, vector, index],
            NI_ArmBase_Yield => [],
            _ => throw new AssertionException($"Unexpected fixture intrinsic: {intrinsic}"),
        };
        var type = intrinsic switch
        {
            NI_ArmBase_LeadingZeroCount => TYP_INT,
            NI_AdvSimd_StoreSelectedScalar or NI_ArmBase_Yield => TYP_VOID,
            _ => TYP_SIMD16,
        };
        var size = type == TYP_INT || intrinsic == NI_ArmBase_Yield ? (byte)0 : (byte)16;
        var tree = new GenTreeHWIntrinsic(type, intrinsic, TYP_INT, size, operands);
        tree.SetCosts(5, 5);
        compiler.vnStore = new ValueNumStore(compiler);
        if (type != TYP_VOID)
        {
            tree._vnPair.SetBoth(compiler.vnStore.VNForExpr(null, type));
        }

        return tree;
    }
}
#endif
