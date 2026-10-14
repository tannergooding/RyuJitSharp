// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class GenTreeOpTests
{
    [Test]
    public static void GetOp2IfPresentReturnsTheSecondOperandWhenPresent()
    {
        SsaLivenessTests.WithCompiler(2, compiler => {
            var op1 = compiler.gtNewIconNode(TYP_INT, 1);
            var op2 = compiler.gtNewIconNode(TYP_INT, 2);
            var binary = new GenTreeOp(GT_ADD, TYP_INT, op1, op2);
            var unary = new GenTreeUnOp(GT_NEG, TYP_INT, op1);

            Assert.That(binary.GetOp2IfPresent(), Is.SameAs(op2));
            Assert.That(unary.GetOp2IfPresent(), Is.Null);
        });
    }
}
