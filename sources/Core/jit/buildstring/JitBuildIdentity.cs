// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

internal static class JitBuildIdentity
{
    internal static string Format(string buildCompiler, string targetOS, string targetArchitecture, bool withNativePgo)
    {
        var pgoMarker = withNativePgo ? "(with native PGO)" : "(without native PGO)";
        return $"RyuJIT built by {buildCompiler} targeting {targetOS}-{targetArchitecture} {pgoMarker}";
    }
}
