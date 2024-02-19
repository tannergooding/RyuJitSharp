// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public struct TargetOS
{
    public static bool OSSettingConfigured;

    public static bool IsWindows = TARGET_WINDOWS;

    public static bool IsUnix = TARGET_UNIX;

    public static bool IsApplePlatform = TARGET_OSX;
}
