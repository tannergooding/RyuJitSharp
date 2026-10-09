// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class CompilerLocalMetadataTests
{
#if DEBUG
    [TestCase(1, 0, "first")]
    [TestCase(1, 3, "first")]
    [TestCase(1, 4, "second")]
    [TestCase(1, 7, "second")]
    [TestCase(1, 8, null)]
    [TestCase(1, -1, null)]
    [TestCase(2, 1, "other")]
    [TestCase(3, 1, null)]
    public static void LocalNamesUseOriginalOrderAndHalfOpenLifetimes(int variable, int offset, string? expected)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info.compVarScopes =
        [
            new() { vsdVarNum = 1, vsdLifeBeg = 0, vsdLifeEnd = 4, vsdName = "first" },
            new() { vsdVarNum = 1, vsdLifeBeg = 2, vsdLifeEnd = 8, vsdName = "second" },
            new() { vsdVarNum = 2, vsdLifeBeg = 0, vsdLifeEnd = 8, vsdName = "other" },
            new() { vsdVarNum = 3, vsdLifeBeg = 0, vsdLifeEnd = 4, vsdName = "outside count" },
        ];
        compiler.info.compVarScopesCount = 3;

        Assert.That(compiler.compLocalVarName(variable, offset), Is.EqualTo(expected));
    }

    [Test]
    public static void AFirstMatchingNullNameDoesNotFallThroughToLaterScopes()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info.compVarScopes =
        [
            new() { vsdVarNum = 0, vsdLifeBeg = 0, vsdLifeEnd = 4 },
            new() { vsdVarNum = 0, vsdLifeBeg = 0, vsdLifeEnd = 4, vsdName = "later" },
        ];
        compiler.info.compVarScopesCount = 2;

        Assert.That(compiler.compLocalVarName(0, 1), Is.Null);
    }
#endif

}
