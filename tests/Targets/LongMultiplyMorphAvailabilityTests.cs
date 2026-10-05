// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if !TARGET_64BIT && !TARGET_WASM
using System.Reflection;
using NUnit.Framework;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class LongMultiplyMorphAvailabilityTests
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
        var firstValue = new GenTreeIntCon(TYP_INT, 1);
        var first = new GenTreeCast(TYP_LONG, firstValue, unsigned, TYP_LONG);
        GenTree second = constantSecondOperand
            ? new GenTreeIntCon(TYP_LONG, negativeConstant ? -1 : 1)
            : new GenTreeCast(TYP_LONG, new GenTreeIntCon(TYP_INT, 2), unsigned, TYP_LONG);
        var multiply = new GenTreeOp(GT_MUL, TYP_LONG, first, second) { IsUnsigned = unsigned };
        multiply.Set64RsltMul();

        Assert.DoesNotThrow(multiply.DebugCheckLongMul);
    }
#endif
}
#endif
