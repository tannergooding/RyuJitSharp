// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_HW_INTRINSICS && TARGET_ARM64
namespace RyuJitSharp;

public readonly partial struct HWIntrinsicInfo
{
    public static bool IsSveConditionalSelect(NamedIntrinsic id) =>
        id is NI_Sve_ConditionalSelect or NI_Sve_ConditionalSelect_Predicates;

    public static bool IsEmbeddedMaskedOperation(NamedIntrinsic id) =>
        (lookupFlags(id) & HW_Flag_EmbeddedMaskedOperation) != 0;

    public static bool IsZeroingMaskedOperation(NamedIntrinsic id) =>
        (lookupFlags(id) & HW_Flag_ZeroingMaskedOperation) != 0;

    public static bool IsReduceOperation(NamedIntrinsic id) =>
        (lookupFlags(id) & HW_Flag_ReduceOperation) != 0;
}
#endif
