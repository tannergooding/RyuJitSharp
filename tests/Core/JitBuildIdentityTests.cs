// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class JitBuildIdentityTests
{
    [TestCase("MSVC 194444444", "win", "x64", true,
        "RyuJIT built by MSVC 194444444 targeting win-x64 (with native PGO)")]
    [TestCase("Clang 19.1.0", "unix", "arm64", false,
        "RyuJIT built by Clang 19.1.0 targeting unix-arm64 (without native PGO)")]
    public static void FormatMatchesPinnedBuildIdentity(
        string buildCompiler,
        string targetOS,
        string targetArchitecture,
        bool withNativePgo,
        string expected)
    {
        Assert.That(JitBuildIdentity.Format(buildCompiler, targetOS, targetArchitecture, withNativePgo),
            Is.EqualTo(expected));
    }
}
