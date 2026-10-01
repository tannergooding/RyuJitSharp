// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static unsafe class CompilerLocalMetadataTests
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

    [TestCase(false)]
    [TestCase(true)]
    public static void ScopeExitsPrecedeEqualOffsetEntriesAcrossCursorResumption(bool incremental)
    {
        var compiler = NewScopes(
            new() { vsdLifeBeg = 0, vsdLifeEnd = 4 },
            new() { vsdLifeBeg = 4, vsdLifeEnd = 8 });
        nint[] events = [];
        if (incremental)
        {
            compiler.compProcessScopesUntil(0, ref events, &EnterScope, &ExitScope);
            Assert.That(events[0], Is.EqualTo((nint)1));
            compiler.compProcessScopesUntil(4, ref events, &EnterScope, &ExitScope);
            Assert.That(events[0], Is.EqualTo((nint)3));
        }

        compiler.compProcessScopesUntil(8, ref events, &EnterScope, &ExitScope);
        compiler.compProcessScopesUntil(8, ref events, &EnterScope, &ExitScope);

        nint[] expected = [1, -1, 2, -2];
        Assert.That(events.Skip(1).Take((int)events[0]), Is.EqualTo(expected));
        Assert.That(compiler.compNextEnterScopeIndex, Is.EqualTo(2));
        Assert.That(compiler.compNextExitScopeIndex, Is.EqualTo(2));
        Assert.That(compiler.info.compVarScopes[0].vsdVarNum, Is.EqualTo(200));
        Assert.That(compiler.info.compVarScopes[1].vsdVarNum, Is.EqualTo(201));
    }

    [Test]
    public static void SkippedNestedScopesKeepTheNativeCallbackSequence()
    {
        var compiler = NewScopes(
            new() { vsdLifeBeg = 0, vsdLifeEnd = 8 },
            new() { vsdLifeBeg = 1, vsdLifeEnd = 4 },
            new() { vsdLifeBeg = 4, vsdLifeEnd = 6 });
        nint[] events = [];

        compiler.compProcessScopesUntil(8, ref events, &EnterScope, &ExitScope);

        nint[] expected = [1, 2, -2, 3, -3, -1];
        Assert.That(events.Skip(1).Take((int)events[0]), Is.EqualTo(expected));
        Assert.That(compiler.compNextEnterScopeIndex, Is.EqualTo(3));
        Assert.That(compiler.compNextExitScopeIndex, Is.EqualTo(3));
    }

    [Test]
    public static void OffsetZeroScopesEnterBeforeTheirZeroLengthExit()
    {
        var compiler = NewScopes(new VarScopeDsc { vsdLifeBeg = 0, vsdLifeEnd = 0 });
        nint[] events = [];

        compiler.compProcessScopesUntil(0, ref events, &EnterScope, &ExitScope);

        nint[] expected = [1, -1];
        Assert.That(events.Skip(1).Take((int)events[0]), Is.EqualTo(expected));
    }

    private static Compiler NewScopes(params VarScopeDsc[] scopes)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info.compVarScopes = scopes;
        compiler.info.compVarScopesCount = scopes.Length;
        for (var index = 0; index < scopes.Length; index++)
        {
            scopes[index].vsdLVnum = index;
            scopes[index].vsdVarNum = index;
        }
        compiler.compInitScopeLists();

        return compiler;
    }

    private static void EnterScope(Compiler compiler, ref nint[] events, ref VarScopeDsc scope)
    {
        RecordScope(compiler, ref events, ref scope, entering: true);
    }

    private static void ExitScope(Compiler compiler, ref nint[] events, ref VarScopeDsc scope)
    {
        RecordScope(compiler, ref events, ref scope, entering: false);
    }

    private static void RecordScope(Compiler compiler, ref nint[] events, ref VarScopeDsc scope, bool entering)
    {
        Assert.That(Unsafe.AreSame(ref scope, ref compiler.info.compVarScopes[scope.vsdLVnum]), Is.True);
        if (events.Length == 0)
        {
            events = new nint[16];
        }
        var index = (int)events[0];
        events[index + 1] = entering ? scope.vsdLVnum + 1 : -(scope.vsdLVnum + 1);
        events[0] = index + 1;
        scope.vsdVarNum += 100;
    }
}
