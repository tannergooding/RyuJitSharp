// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static class GlobalJitOptions
{
    // These are safe to have globals as they cannot change once initialized within the process.
    public static int compUseSoftFPConfigured;

    private static bool s_compFeatureHfa;

    public static bool compFeatureHfa
    {
        get
        {
            return Configuration.CONFIGURABLE_ARM_ABI ? s_compFeatureHfa : Configuration.FEATURE_HFA;
        }

        set
        {
            if (Configuration.CONFIGURABLE_ARM_ABI)
            {
                s_compFeatureHfa = value;
            }
        }
    }
}
