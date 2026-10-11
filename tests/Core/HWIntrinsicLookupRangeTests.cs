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
}
#endif
