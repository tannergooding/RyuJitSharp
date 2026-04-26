// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.Target;

namespace RyuJitSharp;

public sealed partial class Target
{
    public static string? s_tgtCPUName;

    public static string s_tgtPlatformName()
    {
        return TargetOS.IsWindows? "Windows" : "Unix";
    }

    public static readonly ArgOrder g_tgtArgOrder;

    public static readonly ArgOrder g_tgtUnmanagedArgOrder;
}
