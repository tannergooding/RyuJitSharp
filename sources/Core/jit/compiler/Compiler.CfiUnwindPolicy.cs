// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.

namespace RyuJitSharp;

public partial class Compiler
{
    internal bool generateCFIUnwindCodes()
    {
#if FEATURE_CFI_SUPPORT
        return TargetOS.IsUnix && IsTargetAbi(CORINFO_NATIVEAOT_ABI);
#else
        return false;
#endif
    }
}
