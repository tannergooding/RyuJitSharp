// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARMARCH
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.insGroupPlaceholderType;

namespace RyuJitSharp.UnitTests;

internal static unsafe class Arm64EmitterFragmentTests
{
    [Test]
    public static void FunctionEndRecognizesFragmentAndFuncletBoundaries()
    {
        var (compiler, emitter) = CreateEmitter();
        var first = Group();
        var ordinary = Group();
        var funclet = Group(InsGroupFlags.FuncletProlog);
        var placeholder = Group(InsGroupFlags.Placeholder);
        placeholder.igPhData = new insPlaceholderGroupData { igPhType = IGPT_FUNCLET_PROLOG };

        first.igNext = ordinary;
        ordinary.igNext = funclet;
        funclet.igNext = placeholder;

#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previousCompiler = JitTls.Compiler;
        JitTls.Compiler = compiler;
        try
        {
            Assert.That(emitter.emitIsFuncEnd(new emitLocation(first)), Is.False);
            Assert.That(emitter.emitIsFuncEnd(new emitLocation(first), new emitLocation(ordinary)), Is.True);
            Assert.That(emitter.emitIsFuncEnd(new emitLocation(ordinary)), Is.True);
            Assert.That(emitter.emitIsFuncEnd(new emitLocation(funclet)), Is.True);
            Assert.That(emitter.emitIsFuncEnd(new emitLocation(placeholder)), Is.True);
        }
        finally
        {
            JitTls.Compiler = previousCompiler;
        }
    }

    [Test]
    public static void SplittingDefersAdjacentPrologsAndSkipsAnEmptyFinalGroup()
    {
        var (compiler, emitter) = CreateEmitter();
        insGroup[] groups =
        [
            Group(InsGroupFlags.Prolog, 40),
            Group(InsGroupFlags.Prolog, 40),
            Group(InsGroupFlags.None, 40),
            Group(InsGroupFlags.None, 40),
            Group(InsGroupFlags.None, 0),
        ];
        Link(groups);
        FirstGroup(emitter) = groups[0];
        List<insGroup> reported = [];

#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previousCompiler = JitTls.Compiler;
        JitTls.Compiler = compiler;
        try
        {
            emitter.emitSplit(null, null, 60, null,
                (_, location) => reported.Add(location.GetIG() ?? throw new AssertionException("Missing split group.")));

            insGroup[] expected = [groups[2], groups[3]];
            Assert.That(reported, Is.EqualTo(expected));
        }
        finally
        {
            JitTls.Compiler = previousCompiler;
        }
    }

    [Test]
    public static void SplittingHonorsTheRequestedStartAndEndGroups()
    {
        var (compiler, emitter) = CreateEmitter();
        insGroup[] groups = [Group(40), Group(40), Group(40), Group(40)];
        Link(groups);
        FirstGroup(emitter) = groups[0];
        List<insGroup> reported = [];

#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previousCompiler = JitTls.Compiler;
        JitTls.Compiler = compiler;
        try
        {
            emitter.emitSplit(new emitLocation(groups[1]), new emitLocation(groups[3]), 40, null,
                (_, location) => reported.Add(location.GetIG() ?? throw new AssertionException("Missing split group.")));

            insGroup[] expected = [groups[2]];
            Assert.That(reported, Is.EqualTo(expected));
        }
        finally
        {
            JitTls.Compiler = previousCompiler;
        }
    }

    private static (Compiler Compiler, Emitter Emitter) CreateEmitter()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.lvaTrackedCount = 1;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        var emitter = new Emitter(new CodeGen(compiler));
        emitter.emitBegCG(compiler, default);
        emitter.Init();
        emitter.emitBegFN(false
#if DEBUG
            , true
#endif
            );

        return (compiler, emitter);
    }

    private static insGroup Group(InsGroupFlags flags = InsGroupFlags.None, ushort size = 0)
    {
        var group = new insGroup { igFlags = flags, igSize = size };
#if DEBUG
        group.igSelf = group;
#endif
        return group;
    }

    private static insGroup Group(ushort size)
    {
        return Group(InsGroupFlags.None, size);
    }

    private static void Link(insGroup[] groups)
    {
        for (var index = 0; index < groups.Length - 1; index++)
        {
            groups[index].igNext = groups[index + 1];
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitIGlist")]
    private static extern ref insGroup? FirstGroup(Emitter emitter);
}
#endif
