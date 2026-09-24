// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class ConditionCodeTests
{
    [TestCase(GT_SETCC, TYP_INT, GenCondition.NE)]
    [TestCase(GT_SETCC, TYP_LONG, GenCondition.UGE)]
    [TestCase(GT_JCC, TYP_VOID, GenCondition.FNEU)]
    [TestCase(GT_JCC, TYP_VOID, GenCondition.P)]
    public static void ConstructorAndFactoryRetainConditionOpcodeAndType(
        genTreeOps oper, var_types type, GenCondition.CodeKind code)
    {
        WithCompiler(compiler => {
            var condition = new GenCondition(code);
            var direct = new GenTreeCC(oper, type, condition);
            var allocated = compiler.gtNewCC(oper, type, condition);

            Assert.That(direct.Condition.Code, Is.EqualTo(code));
            Assert.That(allocated.Condition.Code, Is.EqualTo(code));
            Assert.That(direct.Oper, Is.EqualTo(oper));
            Assert.That(allocated.Oper, Is.EqualTo(oper));
            Assert.That(direct.Type, Is.EqualTo(type));
            Assert.That(allocated.Type, Is.EqualTo(type));
            Assert.That(allocated, Is.Not.SameAs(direct));
            Assert.That(allocated.Prev, Is.Null);
            Assert.That(allocated.Next, Is.Null);
        });
    }

    [TestCase(GT_SETCC, GenCondition.EQ, GenCondition.NE)]
    [TestCase(GT_JCC, GenCondition.FNEU, GenCondition.FEQ)]
    public static void ReverseConditionUsesTheFactoryInputWithoutChangingOtherNodes(
        genTreeOps oper, GenCondition.CodeKind code, GenCondition.CodeKind reversedCode)
    {
        WithCompiler(compiler => {
            var type = oper is GT_JCC ? TYP_VOID : TYP_INT;
            var condition = new GenCondition(code);
            var node = compiler.gtNewCC(oper, type, condition);
            var independent = compiler.gtNewCC(oper, type, condition);
            var flags = node.Flags;

            var reversed = compiler.gtReverseCond(node);

            Assert.That(reversed, Is.SameAs(node));
            Assert.That(node.Condition.Code, Is.EqualTo(reversedCode));
            Assert.That(independent.Condition.Code, Is.EqualTo(code));
            Assert.That(independent, Is.Not.SameAs(node));
            Assert.That(node.Oper, Is.EqualTo(oper));
            Assert.That(node.Type, Is.EqualTo(type));
            Assert.That(node.Flags, Is.EqualTo(flags));
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
