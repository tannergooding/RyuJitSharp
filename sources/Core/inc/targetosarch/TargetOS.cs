// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public struct TargetOS
{
#if TARGET_WINDOWS
    public const bool IsWindows = true;

    public const bool IsUnix = false;

    public const bool IsApplePlatform = false;
#elif TARGET_UNIX
    public const bool IsWindows = false;

    public const bool IsUnix = true;

#if TARGET_UNIX_ANYOS
    public static bool OSSettingConfigured;

    public static bool IsApplePlatform;
#elif TARGET_APPLE
    public const bool IsApplePlatform = true;
#else
    public const bool IsApplePlatform = false;
#endif
#else
    public static bool OSSettingConfigured;
    
    public static bool IsWindows;
    
    public static bool IsUnix;
    
    public static bool IsApplePlatform;
#endif
}
