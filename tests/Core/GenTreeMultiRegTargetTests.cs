// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class GenTreeMultiRegTargetTests
{
    [Test]
    public static void LongMultiplyIsClassifiedAsMultiRegisterNode()
    {
        var isMultiRegLongMultiply = Configuration.TARGET_32BIT;
        Assert.That(GT_MUL_LONG.IsMultiRegOp, Is.EqualTo(isMultiRegLongMultiply));

        if (!isMultiRegLongMultiply)
        {
            return;
        }

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
            Assert.That(multiply.IsMultiRegNode, Is.EqualTo(Configuration.FEATURE_MULTIREG_RET));
            if (Configuration.FEATURE_MULTIREG_RET)
            {
                Assert.That(multiply.GetRegisterDstCount(compiler), Is.EqualTo(2));
            }

            multiply.SetRegSpillFlagByIdx(GTF_SPILL, 1);
            var expectedSpillFlag = Configuration.FEATURE_MULTIREG_RET ? GTF_SPILL : GTF_EMPTY;
            Assert.That(multiply.GetRegSpillFlagByIdx(1), Is.EqualTo(expectedSpillFlag));
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
