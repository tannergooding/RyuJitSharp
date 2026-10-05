// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if !TARGET_64BIT && !TARGET_WASM
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LongMultiplyMorphAvailabilityTests
{
    [TestCase("fgMorphLongMul")]
    [TestCase("fgRecognizeAndMorphLongMul")]
    public static void Shared32BitMorphMethodsArePresentWithNativeSignatures(string methodName)
    {
        var method = typeof(Compiler).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new AssertionException($"{methodName} is not available for this target.");

        Assert.That(method.ReturnType, Is.EqualTo(typeof(GenTreeOp)));
        var parameters = method.GetParameters();
        Assert.That(parameters, Has.Length.EqualTo(1));
        Assert.That(parameters[0].ParameterType, Is.EqualTo(typeof(GenTreeOp)));
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    public static void MorphRecognizesValidLongMultiply(bool zeroExtend, bool constantSecondOperand)
    {
        WithCompiler(compiler =>
        {
            var firstValue = compiler.gtNewLclvNode(TYP_INT, 0);
            var first = new GenTreeCast(TYP_LONG, firstValue, zeroExtend, TYP_LONG);
            GenTree second = constantSecondOperand
                ? compiler.gtNewIconNode(TYP_LONG, 7)
                : new GenTreeCast(
                    TYP_LONG,
                    compiler.gtNewLclvNode(TYP_INT, 1),
                    zeroExtend,
                    TYP_LONG);
            var multiply = new GenTreeOp(GT_MUL, TYP_LONG, first, second);
            first.CanCse = true;
            second.CanCse = true;

            var result = compiler.fgMorphTree(multiply);

            Assert.That(result, Is.SameAs(multiply));
            Assert.That(multiply.Is64RsltMul, Is.True);
            Assert.That(multiply.IsUnsigned, Is.EqualTo(zeroExtend));
            Assert.That(multiply.Op1, Is.SameAs(first));
            Assert.That(multiply.Op2, Is.SameAs(second));
            Assert.That(first.CanCse, Is.False);
            Assert.That(second.CanCse, Is.False);
        });
    }

    [Test]
    public static void MorphSwapsAConstantFirstOperandBeforeRecognizingLongMultiply()
    {
        WithCompiler(compiler =>
        {
            var constant = compiler.gtNewIconNode(TYP_LONG, 7);
            var cast = new GenTreeCast(
                TYP_LONG,
                compiler.gtNewLclvNode(TYP_INT, 0),
                false,
                TYP_LONG);
            var multiply = new GenTreeOp(GT_MUL, TYP_LONG, constant, cast);

            var result = compiler.fgMorphTree(multiply);

            Assert.That(result, Is.SameAs(multiply));
            Assert.That(multiply.Is64RsltMul, Is.True);
            Assert.That(multiply.Op1, Is.SameAs(cast));
            Assert.That(multiply.Op2, Is.SameAs(constant));
        });
    }

#if DEBUG
    [TestCase(false, false, false)]
    [TestCase(true, false, false)]
    [TestCase(false, true, true)]
    [TestCase(true, true, false)]
    public static void DebugCheckLongMulAcceptsValidCastAndConstantForms(
        bool unsigned,
        bool constantSecondOperand,
        bool negativeConstant)
    {
        WithCompiler(_ =>
        {
            var firstValue = new GenTreeIntCon(TYP_INT, 1);
            var first = new GenTreeCast(TYP_LONG, firstValue, unsigned, TYP_LONG);
            GenTree second = constantSecondOperand
                ? new GenTreeIntCon(TYP_LONG, negativeConstant ? -1 : 1)
                : new GenTreeCast(TYP_LONG, new GenTreeIntCon(TYP_INT, 2), unsigned, TYP_LONG);
            var multiply = new GenTreeOp(GT_MUL, TYP_LONG, first, second) { IsUnsigned = unsigned };
            multiply.Set64RsltMul();

            Assert.DoesNotThrow(multiply.DebugCheckLongMul);
        });
    }
#endif

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.opts.compFlags |= Globals.CLFLG_TREETRANS;
        compiler.fgGlobalMorph = true;
        compiler.lvaTable = [
            new LclVarDsc { Type = TYP_INT },
            new LclVarDsc { Type = TYP_INT },
        ];
        compiler.lvaCount = 2;
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
