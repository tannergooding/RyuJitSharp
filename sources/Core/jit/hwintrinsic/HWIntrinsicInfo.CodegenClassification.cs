// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public readonly partial struct HWIntrinsicInfo
{
    internal static bool IsScalable(NamedIntrinsic id) =>
        Configuration.TARGET_ARM64 && ((lookupFlags(id) & HW_Flag_Scalable) != 0);

    internal static bool RequiresCodegen(NamedIntrinsic id)
    {
        var flags = lookupFlags(id);
        return (flags & (HW_Flag_NoCodeGen | HW_Flag_InvalidNodeId)) == 0;
    }

    internal static bool HasSpecialCodegen(NamedIntrinsic id)
    {
        var flags = lookupFlags(id);
        return (flags & HW_Flag_SpecialCodeGen) != 0;
    }
}
