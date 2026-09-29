// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64 && FEATURE_HW_INTRINSICS
using System;
using NUnit.Framework;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class Arm64HWIntrinsicLayoutTests
{
    [TestCase(NI_AdvSimd_Arm64_LoadPairScalarVector64, 16)]
    [TestCase(NI_AdvSimd_Arm64_LoadPairScalarVector64NonTemporal, 16)]
    [TestCase(NI_AdvSimd_Arm64_LoadPairVector64, 16)]
    [TestCase(NI_AdvSimd_Arm64_LoadPairVector64NonTemporal, 16)]
    [TestCase(NI_AdvSimd_Load2xVector64AndUnzip, 16)]
    [TestCase(NI_AdvSimd_Load2xVector64, 16)]
    [TestCase(NI_AdvSimd_LoadAndInsertScalarVector64x2, 16)]
    [TestCase(NI_AdvSimd_LoadAndReplicateToVector64x2, 16)]
    [TestCase(NI_AdvSimd_Arm64_LoadPairVector128, 32)]
    [TestCase(NI_AdvSimd_Arm64_LoadPairVector128NonTemporal, 32)]
    [TestCase(NI_AdvSimd_Arm64_Load2xVector128AndUnzip, 32)]
    [TestCase(NI_AdvSimd_Arm64_Load2xVector128, 32)]
    [TestCase(NI_AdvSimd_Load4xVector64, 32)]
    [TestCase(NI_AdvSimd_Load4xVector64AndUnzip, 32)]
    [TestCase(NI_AdvSimd_LoadAndReplicateToVector64x4, 32)]
    [TestCase(NI_AdvSimd_Arm64_LoadAndReplicateToVector128x2, 32)]
    [TestCase(NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x2, 32)]
    [TestCase(NI_AdvSimd_LoadAndInsertScalarVector64x4, 32)]
    [TestCase(NI_AdvSimd_Load3xVector64AndUnzip, 24)]
    [TestCase(NI_AdvSimd_Load3xVector64, 24)]
    [TestCase(NI_AdvSimd_LoadAndInsertScalarVector64x3, 24)]
    [TestCase(NI_AdvSimd_LoadAndReplicateToVector64x3, 24)]
    [TestCase(NI_AdvSimd_Arm64_Load3xVector128AndUnzip, 48)]
    [TestCase(NI_AdvSimd_Arm64_Load3xVector128, 48)]
    [TestCase(NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x3, 48)]
    [TestCase(NI_AdvSimd_Arm64_LoadAndReplicateToVector128x3, 48)]
    [TestCase(NI_AdvSimd_Arm64_Load4xVector128AndUnzip, 64)]
    [TestCase(NI_AdvSimd_Arm64_Load4xVector128, 64)]
    [TestCase(NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x4, 64)]
    [TestCase(NI_AdvSimd_Arm64_LoadAndReplicateToVector128x4, 64)]
    public static void FixedAggregateLayoutsMatchNativeSizes(NamedIntrinsic intrinsic, int size)
    {
        CSELiveAcrossCallCostTests.WithCompiler(compiler => {
            var node = NewIntrinsic(intrinsic);
            var expected = compiler.typGetBlkLayout(size);

            Assert.That(node.GetLayout(compiler), Is.SameAs(expected));
            Assert.That(((GenTree)node).GetLayout(compiler), Is.SameAs(expected));
            Assert.That(expected.Size, Is.EqualTo(size));
        });
    }

    [TestCase(NI_Sve_Load2xVectorAndUnzip)]
    [TestCase(NI_Sve_Load3xVectorAndUnzip)]
    [TestCase(NI_Sve_Load4xVectorAndUnzip)]
    public static void SveAggregateLayoutsReachRuntimeVectorLengthDependency(NamedIntrinsic intrinsic)
    {
        CSELiveAcrossCallCostTests.WithCompiler(compiler => {
            var node = NewIntrinsic(intrinsic);
            var exception = Assert.Throws<NotImplementedException>(() => node.GetLayout(compiler));

            Assert.That(exception!.Message, Does.Contain(nameof(Compiler.getRuntimeVectorTByteLength)));
        });
    }

    private static GenTreeHWIntrinsic NewIntrinsic(NamedIntrinsic intrinsic)
    {
        var operands = new GenTree[HWIntrinsicInfo.lookupNumArgs(intrinsic)];
        for (var index = 0; index < operands.Length; index++)
        {
            operands[index] = new GenTreeLclVar(TYP_INT, index);
        }

        return new GenTreeHWIntrinsic(TYP_STRUCT, intrinsic, TYP_INT, 0, operands);
    }
}
#endif
