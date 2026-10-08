// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if FEATURE_MULTIREG_RET
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class GenTreeCallRegisterStateTests
{
    [Test]
    public static void CopyRegDoesNotCopyPerRegisterSpillFlags()
    {
        WithCompiler(_ => {
            var source = new GenTreeCall(TYP_STRUCT);
            var destination = new GenTreeCall(TYP_STRUCT);
            source.SetRegSpillFlagByIdx(GTF_SPILL, 1);
            destination.SetRegSpillFlagByIdx(GTF_SPILLED, 1);

            destination.CopyReg(source);

            Assert.That(destination.GetRegSpillFlagByIdx(1), Is.EqualTo(GTF_SPILLED));
        });
    }

    [Test]
    public static void CloningCallCopiesPerRegisterSpillFlags()
    {
        WithCompiler(compiler => {
            var source = new GenTreeCall(TYP_STRUCT);
            source.SetRegSpillFlagByIdx(GTF_SPILL, 1);

            var clone = (GenTreeCall)compiler.gtCloneExpr(source)!;

            Assert.That(clone.GetRegSpillFlagByIdx(1), Is.EqualTo(GTF_SPILL));
        });
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitTls.Compiler = compiler;
        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
#endif
