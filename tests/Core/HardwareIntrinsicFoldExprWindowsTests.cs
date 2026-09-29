// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_XARCH && FEATURE_HW_INTRINSICS
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class HardwareIntrinsicFoldExprWindowsTests
{
    [TestCase(NI_AVX2_LeadingZeroCount, TYP_INT, 26L)]
    [TestCase(NI_AVX2_X64_LeadingZeroCount, TYP_LONG, 58L)]
    [TestCase(NI_AVX2_TrailingZeroCount, TYP_INT, 1L)]
    public static void XarchConstantFoldsRemainAvailable(NamedIntrinsic intrinsicId, var_types type, long expected)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        JitTls.Compiler = compiler;

        try
        {
            var input = compiler.gtNewIconNode(type, 42);
            var intrinsic = new GenTreeHWIntrinsic(type, intrinsicId, type, 0, input);

            var result = compiler.gtFoldExpr(intrinsic);
            Assert.That(result, Is.SameAs(input));
            Assert.That(result.AsIntConCommon().IntegralValue, Is.EqualTo(expected));
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
#endif
