// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using NUnit.Framework;
using static RyuJitSharp.NamedIntrinsic;

namespace RyuJitSharp.UnitTests;

internal static class Arm64IntrinsicMetadataTests
{
    [TestCase(NI_Sve_ConditionalExtractAfterLastActiveElement, NI_Sve_ConditionalExtractAfterLastActiveElementScalar)]
    [TestCase(NI_Sve_ConditionalExtractLastActiveElement, NI_Sve_ConditionalExtractLastActiveElementScalar)]
    [TestCase(NI_Sve_SaturatingDecrementBy16BitElementCount, NI_Sve_SaturatingDecrementBy16BitElementCountScalar)]
    [TestCase(NI_Sve_SaturatingDecrementBy32BitElementCount, NI_Sve_SaturatingDecrementBy32BitElementCountScalar)]
    [TestCase(NI_Sve_SaturatingDecrementBy64BitElementCount, NI_Sve_SaturatingDecrementBy64BitElementCountScalar)]
    [TestCase(NI_Sve_SaturatingIncrementBy16BitElementCount, NI_Sve_SaturatingIncrementBy16BitElementCountScalar)]
    [TestCase(NI_Sve_SaturatingIncrementBy32BitElementCount, NI_Sve_SaturatingIncrementBy32BitElementCountScalar)]
    [TestCase(NI_Sve_SaturatingIncrementBy64BitElementCount, NI_Sve_SaturatingIncrementBy64BitElementCountScalar)]
    public static void ScalarInputVariantMapsToItsScalarIntrinsic(
        NamedIntrinsic id, NamedIntrinsic expected)
    {
        Assert.That(HWIntrinsicInfo.GetScalarInputVariant(id), Is.EqualTo(expected));
    }

    [TestCase(NI_AdvSimd_Insert, 3, 1, -1)]
    [TestCase(NI_AdvSimd_Arm64_InsertSelectedScalar, 4, 2, 0)]
    [TestCase(NI_Sve_SaturatingDecrementBy16BitElementCount, 3, 1, 0)]
    [TestCase(NI_Sve_MultiplyAddRotateComplexBySelectedScalar, 5, 0, 1)]
    [TestCase(NI_AdvSimd_Add, 2, 0, -1)]
    public static void ImmediateOperandPositionsMatchNativeSignatureOrder(
        NamedIntrinsic id, int argumentCount, int first, int second)
    {
        HWIntrinsicInfo.GetImmOpsPositions(id, argumentCount, out var actualFirst, out var actualSecond);
        Assert.That((actualFirst, actualSecond), Is.EqualTo((first, second)));
    }
}
#endif
