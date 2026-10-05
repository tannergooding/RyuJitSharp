// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm32CodeGenUnsupportedNodesTests
{
    [Test]
    public static void NonLocalJumpRetainsTheNativeUnsupportedBoundary()
    {
        WithCodeGen((_, codeGen) =>
        {
            var value = new GenTree(GT_FTN_ENTRY, TYP_I_IMPL);
            var tree = new GenTreeUnOp(GT_NONLOCAL_JMP, TYP_VOID, value);

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genNonLocalJmp(tree));

            Assert.That(failure?.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
        });
    }

    [Test]
    public static void FunctionEntryRetainsTheNativeUnsupportedBoundary()
    {
        WithCodeGen((_, codeGen) =>
        {
            var tree = new GenTree(GT_FTN_ENTRY, TYP_I_IMPL);

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genFtnEntry(tree));

            Assert.That(failure?.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
        });
    }

    private static void WithCodeGen(Action<Compiler, CodeGen> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.compCurLife = VarSetOps.MakeEmpty(compiler);
        JitTls.Compiler = compiler;
        try
        {
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            action(compiler, codeGen);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
#endif
