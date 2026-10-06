// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64 && FEATURE_HW_INTRINSICS
using NUnit.Framework;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class Arm64HardwareMorphTests
{
    [Test]
    public static void AdvSimdAddConstantsAreReassociated()
    {
        CSELiveAcrossCallCostTests.WithCompiler(compiler => {
            var local = new GenTreeLclVar(TYP_SIMD16, 0);
            var first = new GenTreeVecCon(TYP_SIMD16);
            var second = new GenTreeVecCon(TYP_SIMD16);
            first.EvaluateBroadcastInPlace(TYP_INT, 3L);
            second.EvaluateBroadcastInPlace(TYP_INT, 4L);
            var inner = compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_AdvSimd_Add, TYP_INT, 16, local, first);
            var outer = compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_AdvSimd_Add, TYP_INT, 16, inner, second);

            Assert.That(compiler.fgOptimizeHWIntrinsicAssociative(outer), Is.SameAs(outer));
            Assert.That(outer.GetOp(1), Is.SameAs(local));
            Assert.That(outer.GetOp(2), Is.SameAs(first));
            for (var lane = 0; lane < 4; lane++)
            {
                Assert.That(first.SimdVal.i32[lane], Is.EqualTo(7));
            }
        });
    }
}
#endif
