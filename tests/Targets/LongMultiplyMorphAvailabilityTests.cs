// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if !TARGET_64BIT && !TARGET_WASM
using System.Reflection;
using NUnit.Framework;

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
}
#endif
