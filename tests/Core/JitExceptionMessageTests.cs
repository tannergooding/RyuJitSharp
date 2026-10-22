// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using static RyuJitSharp.genTreeOps;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class JitExceptionMessageTests
{
    [Test]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "This test reflects over helper signatures and is not part of a trimmed application.")]
    public static void ThrowHelpersAreVoidAndDoNotAcceptExceptionInstancesOrMessages()
    {
        AssertThrowHelpers(typeof(JitExceptionHelpers));
        AssertThrowHelpers(typeof(FatalJitException));
    }

    private static void AssertThrowHelpers(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.NonPublicMethods | DynamicallyAccessedMemberTypes.PublicMethods)]
        Type helperType)
    {
        var methods = helperType.GetMethods(
                BindingFlags.DeclaredOnly | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name.StartsWith("Throw", StringComparison.Ordinal))
            .ToArray();

        Assert.That(methods, Is.Not.Empty, helperType.Name);

        foreach (var method in methods)
        {
            Assert.That(method.ReturnType, Is.EqualTo(typeof(void)), method.ToString());
            Assert.That(
                method.IsDefined(typeof(DoesNotReturnAttribute), inherit: false),
                Is.True,
                method.ToString());
            var parameters = method.GetParameters();
            var hasErrorCode = parameters.Any(parameter =>
                parameter.ParameterType == typeof(FatalJitErrorCode));
            var allowsNoCode = !method.IsGenericMethodDefinition &&
                parameters.All(parameter =>
                    parameter.ParameterType == typeof(FatalJitArgument) ||
                    parameter.ParameterType == typeof(CorJitResult));
            Assert.That(hasErrorCode || allowsNoCode, Is.True, method.ToString());
            Assert.That(
                parameters.Any(parameter =>
                    typeof(Exception).IsAssignableFrom(parameter.ParameterType)),
                Is.False,
                method.ToString());
            Assert.That(
                parameters.Any(parameter => parameter.ParameterType == typeof(string)),
                Is.False,
                method.ToString());
        }
    }

    [Test]
    public static void NoMessageThrowHelpersDoNotAssignErrorCodesOrRootArgumentNames()
    {
        const string switchName = "RyuJitSharp.DETAILED_FATAL_ERROR_MESSAGES";
        var hadOriginalValue = AppContext.TryGetSwitch(switchName, out var originalValue);

        try
        {
            AppContext.SetSwitch(switchName, false);
            var exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => JitExceptionHelpers.ThrowArgumentOutOfRangeException(FatalJitArgument.Base))!;
            Assert.That(
                exception.ParamName,
                Is.EqualTo(Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "base" : null));
            Assert.That(exception.Message, Does.Not.Contain("RJS-"));

            var fatalException = Assert.Throws<FatalJitException>(
                () => FatalJitException.ThrowFatalJitException(CorJitResult.CORJIT_SKIPPED))!;
            Assert.That(fatalException.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
            Assert.That(fatalException.ErrorCode, Is.EqualTo(FatalJitErrorCode.Unspecified));
            Assert.That(fatalException.Message, Is.EqualTo(new FatalJitException().Message));

            AppContext.SetSwitch(switchName, true);
            exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => JitExceptionHelpers.ThrowArgumentOutOfRangeException(FatalJitArgument.Base))!;
            Assert.That(exception.ParamName, Is.EqualTo("base"));
        }
        finally
        {
            AppContext.SetSwitch(switchName, hadOriginalValue && originalValue);
        }
    }

    [Test]
    public static void ThrowHelpersUseErrorIdsAndOptInDetailedMessages()
    {
        const string switchName = "RyuJitSharp.DETAILED_FATAL_ERROR_MESSAGES";
        var hadOriginalValue = AppContext.TryGetSwitch(switchName, out var originalValue);
        const string detailedMessage = "SIMD ternary logic requires xarch hardware intrinsics.";

        try
        {
            AppContext.SetSwitch(switchName, false);
            var exception = Assert.Throws<PlatformNotSupportedException>(
                () => JitExceptionHelpers.ThrowPlatformNotSupportedException(
                    FatalJitErrorCode.SIMDTernaryLogicRequiresXarchHardwareIntrinsics));
            Assert.That(
                exception!.Message,
                Is.EqualTo(Configuration.DETAILED_FATAL_ERROR_MESSAGES ? detailedMessage : "RJS-1049"));

            AppContext.SetSwitch(switchName, true);
            exception = Assert.Throws<PlatformNotSupportedException>(
                () => JitExceptionHelpers.ThrowPlatformNotSupportedException(
                    FatalJitErrorCode.SIMDTernaryLogicRequiresXarchHardwareIntrinsics));
            Assert.That(exception!.Message, Is.EqualTo(detailedMessage));
        }
        finally
        {
            AppContext.SetSwitch(switchName, hadOriginalValue && originalValue);
        }
    }

    [Test]
    public static void FatalJitThrowHelperPreservesResultAndErrorCode()
    {
        var result = CorJitResult.CORJIT_SKIPPED;
        const FatalJitErrorCode errorCode = FatalJitErrorCode.InvalidConstantTernaryLogicOperation;

        var exception = Assert.Throws<FatalJitException>(
            () => FatalJitException.ThrowFatalJitException(result, errorCode));

        Assert.That(exception!.Result, Is.EqualTo(result));
        Assert.That(exception.ErrorCode, Is.EqualTo(errorCode));
        Assert.That(
            exception.Message,
            Is.EqualTo(
                Configuration.DETAILED_FATAL_ERROR_MESSAGES
                    ? "Invalid constant ternary-logic operation."
                    : "RJS-1840"));
    }

    [Test]
    public static void TernaryFailureKeepsItsTypeAndOptionalDetail()
    {
        const string switchName = "RyuJitSharp.DETAILED_FATAL_ERROR_MESSAGES";
        var operation = unchecked((genTreeOps)int.MaxValue);
        var detailedMessage = $"Unexpected ternary-logic operation: {operation}.";
        var hadOriginalValue = AppContext.TryGetSwitch(switchName, out var originalValue);

        try
        {
            AppContext.SetSwitch(switchName, false);
            var exception = Assert.Throws<InvalidOperationException>(
                () => TernaryLogicInfo.GetTernaryControlByte(operation, 0, 0));
            Assert.That(exception!.GetType(), Is.EqualTo(typeof(InvalidOperationException)));
            Assert.That(
                exception.Message,
                Is.EqualTo(Configuration.DETAILED_FATAL_ERROR_MESSAGES ? detailedMessage : "RJS-1839"));

            AppContext.SetSwitch(switchName, true);
            exception = Assert.Throws<InvalidOperationException>(
                () => TernaryLogicInfo.GetTernaryControlByte(operation, 0, 0));
            Assert.That(exception!.Message, Is.EqualTo(detailedMessage));
        }
        finally
        {
            AppContext.SetSwitch(switchName, hadOriginalValue && originalValue);
        }
    }
}
