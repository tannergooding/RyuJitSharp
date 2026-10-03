// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_X86
namespace RyuJitSharp;

public sealed partial class Target
{
    public static readonly string TgtCpuName = "x86";

    public static readonly ArgOrder TgtArgOrder = ARG_ORDER_L2R;

    public static readonly ArgOrder TgtUnmanagedArgOrder = ARG_ORDER_R2L;
}
#endif
