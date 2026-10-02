// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if FEATURE_HW_INTRINSICS
using NUnit.Framework;
using static RyuJitSharp.NamedIntrinsic;

namespace RyuJitSharp.UnitTests;

internal static class HWIntrinsicCodegenClassificationTests
{
    [TestCase(NI_Vector_Abs, false, true, false)]
    [TestCase(NI_Vector_Create, false, true, false)]
    [TestCase(NI_Vector_GetElement, true, true, true)]
    [TestCase(NI_Vector_op_Equality, false, true, false)]
#if TARGET_XARCH
    [TestCase(NI_AVX_Add, true, false, false)]
    [TestCase(NI_X86Base_Pause, true, false, false)]
    [TestCase(NI_X86Base_AndNot, true, true, false)]
    [TestCase(NI_Vector_GetLower, true, false, true)]
    [TestCase(NI_Vector_CreateScalar, true, true, true)]
    [TestCase(NI_X86Base_DivRem, true, true, true)]
    [TestCase(NI_X86Base_CompareScalarOrderedEqual, true, false, false)]
    [TestCase(NI_X86Base_CompareScalarOrderedNotEqual, true, false, true)]
#endif

    public static void GeneratedMetadataKeepsCodegenEligibilityImportAndCodegenDispatchIndependent(
        NamedIntrinsic id, bool requiresCodegen, bool specialImport, bool specialCodegen)
    {
        Assert.That(HWIntrinsicInfo.RequiresCodegen(id), Is.EqualTo(requiresCodegen));
        Assert.That(HWIntrinsicInfo.HasSpecialImport(id), Is.EqualTo(specialImport));
        Assert.That(HWIntrinsicInfo.HasSpecialCodegen(id), Is.EqualTo(specialCodegen));
    }

#if TARGET_ARM64
    [TestCase(NI_Sve_Abs, true)]
    [TestCase(NI_AdvSimd_Add, false)]
    public static void ScalableFlagTracksTheIntrinsicMetadata(NamedIntrinsic id, bool expected)
    {
        Assert.That(HWIntrinsicInfo.IsScalable(id), Is.EqualTo(expected));
    }
#endif
}
#endif
