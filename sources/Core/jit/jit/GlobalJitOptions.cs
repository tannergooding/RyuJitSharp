// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static class GlobalJitOptions
{
#if FEATURE_HFA
#if CONFIGURABLE_ARM_ABI
    // These are safe to have globals as they cannot change once initialized within the process.
    public static static int compUseSoftFPConfigured;

    public static static bool compFeatureHfa;
#else
    public static bool compFeatureHfa => true;
#endif
#else
    public static bool compFeatureHfa => false;
#endif
}
