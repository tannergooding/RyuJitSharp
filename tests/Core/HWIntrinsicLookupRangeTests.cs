// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.NamedIntrinsic;

namespace RyuJitSharp.UnitTests;

#if FEATURE_HW_INTRINSICS && FEATURE_SIMD
internal static unsafe class HWIntrinsicLookupRangeTests
{
    [Test]
    public static void LookupUsesGeneratedIsaRangesAndVectorAliases()
    {
        CORINFO_SIG_INFO sig = default;
        sig.numArgs = 2;

        Assert.That(HWIntrinsicInfo.LookupId(&sig, InstructionSet_Vector, "op_Addition"u8), Is.EqualTo(NI_Vector_op_Addition));

#if TARGET_XARCH
        Assert.That(HWIntrinsicInfo.LookupId(&sig, InstructionSet_AVX2, "Add"u8), Is.EqualTo(NI_AVX2_Add));
#elif TARGET_ARM64
        Assert.That(HWIntrinsicInfo.LookupId(&sig, InstructionSet_AdvSimd, "Add"u8), Is.EqualTo(NI_AdvSimd_Add));
#elif TARGET_WASM
        Assert.That(HWIntrinsicInfo.LookupId(&sig, InstructionSet_PackedSimd, "Add"u8), Is.EqualTo(NI_PackedSimd_Add));
#else
#error Unsupported target with hardware-intrinsic and SIMD support.
#endif
    }

    [Test]
    public static void MetadataQueriesUseConfiguredTargetRows()
    {
        if (Configuration.TARGET_XARCH)
        {
            Assert.That(HWIntrinsicInfo.GetMultiRegCount(NI_X86Base_DivRem), Is.EqualTo(2));
            Assert.That(HWIntrinsicInfo.HasImmediateOperand(NI_AVX2_InsertVector128), Is.True);
            Assert.That(HWIntrinsicInfo.IsMaybeCommutative(NI_AVX_Min), Is.True);
            Assert.That(HWIntrinsicInfo.NeedsNormalizeSmallTypeToInt(NI_AVX2_InsertVector128), Is.True);
            Assert.That(HWIntrinsicInfo.AvxOnlyCompatible(NI_Vector_AsInt32), Is.True);
            Assert.That(HWIntrinsicInfo.MaybeMemoryLoad(NI_AVX2_GatherVector256), Is.True);
        }
        else if (Configuration.TARGET_ARM64)
        {
            Assert.That(HWIntrinsicInfo.GetMultiRegCount(NI_AdvSimd_Load3xVector64AndUnzip), Is.EqualTo(3));
            Assert.That(HWIntrinsicInfo.HasImmediateOperand(NI_AdvSimd_ShiftLeftLogical), Is.True);
            Assert.That(HWIntrinsicInfo.IsMaybeCommutative(NI_AdvSimd_Add), Is.False);
            Assert.That(HWIntrinsicInfo.NeedsNormalizeSmallTypeToInt(NI_AdvSimd_Add), Is.False);
            Assert.That(HWIntrinsicInfo.AvxOnlyCompatible(NI_AdvSimd_Add), Is.False);
            Assert.That(HWIntrinsicInfo.MaybeMemoryLoad(NI_AdvSimd_Load3xVector64AndUnzip), Is.False);
        }
        else if (Configuration.TARGET_WASM)
        {
            Assert.That(HWIntrinsicInfo.HasImmediateOperand(NI_PackedSimd_ExtractScalar), Is.True);
            Assert.That(HWIntrinsicInfo.IsMaybeCommutative(NI_PackedSimd_Add), Is.False);
            Assert.That(HWIntrinsicInfo.NeedsNormalizeSmallTypeToInt(NI_PackedSimd_Add), Is.False);
            Assert.That(HWIntrinsicInfo.AvxOnlyCompatible(NI_PackedSimd_Add), Is.False);
            Assert.That(HWIntrinsicInfo.MaybeMemoryStore(NI_PackedSimd_Store), Is.False);
        }
        else
        {
            Assert.Fail("The hardware-intrinsic test is running on an unsupported target.");
        }
    }
}
#endif
