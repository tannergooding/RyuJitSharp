// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm64FoldExprDispatchTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void HardwareIntrinsicDispatchPreservesTier0Guard(bool tier0Disabled)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        if (tier0Disabled)
        {
            flags.Set(JitFlags.JIT_FLAG_MIN_OPT);
        }

        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(tier0Disabled);
        JitTls.Compiler = compiler;

        try
        {
            var input = compiler.gtNewIconNode(TYP_INT, 42);
            var intrinsic = new GenTreeHWIntrinsic(TYP_INT, NI_ArmBase_LeadingZeroCount, TYP_INT, 0, input);

            if (tier0Disabled)
            {
                Assert.That(compiler.gtFoldExpr(intrinsic), Is.SameAs(intrinsic));
            }
            else
            {
                Assert.Throws<NotImplementedException>(() => compiler.gtFoldExpr(intrinsic));
            }
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
#endif
