// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public readonly partial struct HWIntrinsicInfo
{
    public static bool IsSveConditionalSelect(NamedIntrinsic id) =>
        Configuration.TARGET_ARM64 && Configuration.FEATURE_HW_INTRINSICS &&
            (id is NI_Sve_ConditionalSelect or NI_Sve_ConditionalSelect_Predicates);

    public static bool IsEmbeddedMaskedOperation(NamedIntrinsic id) =>
        Configuration.TARGET_ARM64 && ((lookupFlags(id) & HW_Flag_EmbeddedMaskedOperation) != 0);

    public static bool IsExplicitMaskedOperation(NamedIntrinsic id) =>
        Configuration.TARGET_ARM64 && ((lookupFlags(id) & HW_Flag_ExplicitMaskedOperation) != 0);

    public static bool IsOptionalEmbeddedMaskedOperation(NamedIntrinsic id) =>
        Configuration.TARGET_ARM64 && ((lookupFlags(id) & HW_Flag_OptionalEmbeddedMaskedOperation) != 0);

    public static bool IsLowMaskedOperation(NamedIntrinsic id) =>
        Configuration.TARGET_ARM64 && ((lookupFlags(id) & HW_Flag_LowMaskedOperation) != 0);

    public static bool IsLowVectorOperation(NamedIntrinsic id) =>
        Configuration.TARGET_ARM64 && ((lookupFlags(id) & HW_Flag_LowVectorOperation) != 0);

    public static bool NeedsConsecutiveRegisters(NamedIntrinsic id) =>
        Configuration.TARGET_ARM64 && ((lookupFlags(id) & HW_Flag_NeedsConsecutiveRegisters) != 0);

    public static bool IsZeroingMaskedOperation(NamedIntrinsic id) =>
        Configuration.TARGET_ARM64 && ((lookupFlags(id) & HW_Flag_ZeroingMaskedOperation) != 0);

    public static bool IsReduceOperation(NamedIntrinsic id) =>
        Configuration.TARGET_ARM64 && ((lookupFlags(id) & HW_Flag_ReduceOperation) != 0);
}
