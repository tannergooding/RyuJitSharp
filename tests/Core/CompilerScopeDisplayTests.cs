// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if DEBUG
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class CompilerScopeDisplayTests
{
    [Test]
    public static void EmptyScopeListsPrintBothTitlesWithoutTheColumnHeader()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));

        var output = CodeGenLifeTransitionTests.Capture(compiler.compDispScopeLists);

        var newline = Environment.NewLine;
        Assert.That(output, Is.EqualTo(
            $"Local variable scopes = 0{newline}" +
            $"Sorted by enter scope:{newline}" +
            $"Sorted by exit scope:{newline}"));
        Assert.That(compiler.info.compVarScopesCount, Is.Zero);
        Assert.That(compiler.compNextEnterScopeIndex, Is.Zero);
        Assert.That(compiler.compNextExitScopeIndex, Is.Zero);
    }

    [TestCase(0, 0, "ABCDEFGHIJK", "ABCDEFGHIJK")]
    [TestCase(1, 2, "", "          ")]
    [TestCase(2, 1, "\u00E9", "        \u00E9")]
    [TestCase(3, 3, "ABCDEFGHIJK", "ABCDEFGHIJK")]
    public static void PopulatedListsPreserveNativeWidthsOrderAndCursorMarkers(
        int nextEnter, int nextExit, string name, string paddedName)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info.compVarScopes =
        [
            new() { vsdVarNum = 12, vsdLVnum = 5, vsdName = "alpha", vsdLifeBeg = 0x120, vsdLifeEnd = 0x300 },
            new() { vsdVarNum = 0x123, vsdLVnum = 10, vsdLifeBeg = 8, vsdLifeEnd = 0x200 },
            new() { vsdVarNum = -1, vsdLVnum = 0xBC, vsdName = name, vsdLifeBeg = 0xFF, vsdLifeEnd = 0x180 },
        ];
        compiler.info.compVarScopesCount = 3;
        int[] enterIndices = [2, 1, 0];
        int[] exitIndices = [0, 2, 1];
        EnterIndices(compiler) = enterIndices;
        ExitIndices(compiler) = exitIndices;
        compiler.compNextEnterScopeIndex = nextEnter;
        compiler.compNextExitScopeIndex = nextExit;
        var scopes = compiler.info.compVarScopes;
        var originalScopes = (VarScopeDsc[])scopes.Clone();

        var output = CodeGenLifeTransitionTests.Capture(compiler.compDispScopeLists);

        var newline = Environment.NewLine;
        string[] enterRows =
        [
            $" 0: \tFFFFFFFFh \tBCh \t{paddedName} \t0FFh   \t180h",
            " 1: \t123h \t0Ah \t   UNKNOWN \t008h   \t200h",
            " 2: \t0Ch \t05h \t     alpha \t120h   \t300h",
        ];
        string[] exitRows =
        [
            " 0: \t0Ch \t05h \t     alpha \t120h   \t300h",
            $" 1: \tFFFFFFFFh \tBCh \t{paddedName} \t0FFh   \t180h",
            " 2: \t123h \t0Ah \t   UNKNOWN \t008h   \t200h",
        ];
        var expected = $"Local variable scopes = 3{newline}" +
            $"    \tVarNum \tLVNum \t      Name \tBeg \tEnd{newline}" +
            $"Sorted by enter scope:{newline}";
        for (var index = 0; index < enterRows.Length; index++)
        {
            expected += enterRows[index] + (nextEnter == index ? " <-- next enter scope" : "") + newline;
        }
        expected += $"Sorted by exit scope:{newline}";
        for (var index = 0; index < exitRows.Length; index++)
        {
            expected += exitRows[index] + (nextExit == index ? " <-- next exit scope" : "") + newline;
        }

        Assert.That(output, Is.EqualTo(expected));
        Assert.That(compiler.info.compVarScopes, Is.SameAs(scopes));
        Assert.That(scopes, Is.EqualTo(originalScopes));
        Assert.That(EnterIndices(compiler), Is.SameAs(enterIndices).And.EqualTo((int[])[2, 1, 0]));
        Assert.That(ExitIndices(compiler), Is.SameAs(exitIndices).And.EqualTo((int[])[0, 2, 1]));
        Assert.That(compiler.info.compVarScopesCount, Is.EqualTo(3));
        Assert.That(compiler.compNextEnterScopeIndex, Is.EqualTo(nextEnter));
        Assert.That(compiler.compNextExitScopeIndex, Is.EqualTo(nextExit));
        Assert.That(Unsafe.AreSame(ref scopes[2], ref compiler.compEnterScopeList(0)), Is.True);
        Assert.That(Unsafe.AreSame(ref scopes[0], ref compiler.compExitScopeList(0)), Is.True);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "compEnterScopeIndices")]
    private static extern ref int[] EnterIndices(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "compExitScopeIndices")]
    private static extern ref int[] ExitIndices(Compiler compiler);
}
#endif
