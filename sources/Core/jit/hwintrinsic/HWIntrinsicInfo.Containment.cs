// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public readonly partial struct HWIntrinsicInfo
{
#if FEATURE_HW_INTRINSICS
    public static bool SupportsContainment(NamedIntrinsic id)
    {
#if TARGET_XARCH
        return (lookupFlags(id) & HW_Flag_NoContainment) == 0;
#elif TARGET_ARM64 || TARGET_WASM
        return (lookupFlags(id) & HW_Flag_SupportsContainment) != 0;
#else
        throw new System.NotImplementedException("Hardware-intrinsic containment is not ported for this target.");
#endif
    }

    public static bool IsVectorCreateScalar(NamedIntrinsic id) => id is NI_Vector_CreateScalar;

    public static bool IsVectorCreateScalarUnsafe(NamedIntrinsic id) => id is NI_Vector_CreateScalarUnsafe;

    public static bool IsFmaIntrinsic(NamedIntrinsic id) => (lookupFlags(id) & HW_Flag_FmaIntrinsic) != 0;

#if TARGET_XARCH
    public static bool CopiesUpperBits(NamedIntrinsic id) => (lookupFlags(id) & HW_Flag_CopyUpperBits) != 0;

    public static bool IsPermuteVar2x(NamedIntrinsic id) => (lookupFlags(id) & HW_Flag_PermuteVar2x) != 0;
#endif
#endif
}
