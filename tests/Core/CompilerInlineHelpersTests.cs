// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class CompilerInlineHelpersTests
{
    [Test]
    public static void DebugDestroySupportsMultipleNodes()
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previousCompiler = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitTls.Compiler = compiler;

        try
        {
            var first = new GenTreeIntCon(TYP_INT, 1);
            var second = new GenTreeIntCon(TYP_INT, 2);

            Globals.DEBUG_DESTROY_NODE(first, second);
#if DEBUG
            Assert.That(first.Oper, Is.EqualTo(GT_COUNT));
            Assert.That(second.Oper, Is.EqualTo(GT_COUNT));
#else
            Assert.That(first.Oper, Is.EqualTo(GT_CNS_INT));
            Assert.That(second.Oper, Is.EqualTo(GT_CNS_INT));
#endif
        }
        finally
        {
            JitTls.Compiler = previousCompiler;
        }
    }
}
