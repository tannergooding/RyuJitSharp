// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class CompilerLocalMetadataTests
{
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
        Assume.That(TestConfiguration.IsDebug);

        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info.compVarScopes =
        [
            new() { vsdVarNum = 1, vsdLifeBeg = 0, vsdLifeEnd = 4 },
            new() { vsdVarNum = 1, vsdLifeBeg = 2, vsdLifeEnd = 8 },
            new() { vsdVarNum = 2, vsdLifeBeg = 0, vsdLifeEnd = 8 },
            new() { vsdVarNum = 3, vsdLifeBeg = 0, vsdLifeEnd = 4 },
        ];
        compiler.SetVarScopeName(0, "first");
        compiler.SetVarScopeName(1, "second");
        compiler.SetVarScopeName(2, "other");
        compiler.SetVarScopeName(3, "outside count");
        compiler.info.compVarScopesCount = 3;

        Assert.That(compiler.compLocalVarName(variable, offset), Is.EqualTo(expected));
    }

    [Test]
    public static void AFirstMatchingNullNameDoesNotFallThroughToLaterScopes()
    {
        Assume.That(TestConfiguration.IsDebug);

        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info.compVarScopes =
        [
            new() { vsdVarNum = 0, vsdLifeBeg = 0, vsdLifeEnd = 4 },
            new() { vsdVarNum = 0, vsdLifeBeg = 0, vsdLifeEnd = 4 },
        ];
        compiler.SetVarScopeName(1, "later");
        compiler.info.compVarScopesCount = 2;

        Assert.That(compiler.compLocalVarName(0, 1), Is.Null);
    }

    [Test]
    public static void VariableScopeNamesUseCompilerDebugData()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));

        if (TestConfiguration.IsDebug)
        {
            compiler.SetVarScopeName(2, "local");

            Assert.That(compiler.GetVarScopeName(2), Is.EqualTo("local"));
            Assert.That(Compiler.DebugData.Get(compiler).VarScopeNames![2], Is.EqualTo("local"));
        }
        else
        {
            Assert.That(compiler.GetVarScopeName(2), Is.Null);
            _ = Assert.Throws<InvalidOperationException>(() => compiler.SetVarScopeName(2, "release misuse"));
        }
    }
}
