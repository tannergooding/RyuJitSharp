// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if FEATURE_MULTIREG_RET && TARGET_32BIT
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class GenTreeMultiRegTargetTests
{
    [Test]
    public static void LongMultiplyIsClassifiedAsMultiRegisterNode()
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitTls.Compiler = compiler;
        try
        {
            var left = new GenTree(GT_NOP, TYP_INT);
            var right = new GenTree(GT_NOP, TYP_INT);
            var multiply = new GenTreeMultiRegOp(GT_MUL_LONG, TYP_LONG, left, right);

            Assert.That(multiply.Oper.IsMultiRegOp, Is.True);
            Assert.That(multiply.IsMultiRegNode, Is.True);
            Assert.That(multiply.GetRegisterDstCount(compiler), Is.EqualTo(2));
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
#endif
