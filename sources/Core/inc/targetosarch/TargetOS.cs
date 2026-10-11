// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public struct TargetOS
{
    private static bool s_osSettingConfigured;
    private static bool s_isWindows;
    private static bool s_isUnix;
    private static bool s_isApplePlatform;

    public static bool OSSettingConfigured => s_osSettingConfigured;

    public static bool IsWindows => !Configuration.TARGET_UNIX_OS_RUNTIMEDETERMINED
        && (s_osSettingConfigured ? s_isWindows : Configuration.TARGET_WINDOWS);

    public static bool IsUnix => Configuration.TARGET_UNIX_OS_RUNTIMEDETERMINED
        || (s_osSettingConfigured ? s_isUnix : Configuration.TARGET_UNIX);

    public static bool IsApplePlatform => s_osSettingConfigured ? s_isApplePlatform : Configuration.TARGET_APPLE;

    internal static void SetTargetOS(bool isWindows, bool isUnix, bool isApplePlatform)
    {
        if (!Configuration.TARGET_OS_RUNTIMEDETERMINED)
        {
            return;
        }

        s_isApplePlatform = isApplePlatform;
        if (!Configuration.TARGET_UNIX_OS_RUNTIMEDETERMINED)
        {
            s_isUnix = isUnix;
            s_isWindows = isWindows;
        }

        s_osSettingConfigured = true;
    }
}
