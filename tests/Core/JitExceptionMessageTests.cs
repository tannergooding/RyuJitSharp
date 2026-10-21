// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Diagnostics;
using NUnit.Framework;
using static RyuJitSharp.genTreeOps;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class JitExceptionMessageTests
{
    [Test]
    public static void TernaryFailureKeepsItsTypeAndOptionalDetail()
    {
        const string switchName = "RyuJitSharp.DETAILED_FATAL_ERROR_MESSAGES";
        var hadOriginalValue = AppContext.TryGetSwitch(switchName, out var originalValue);
        var operation = unchecked((genTreeOps)int.MaxValue);
        var detailedMessage = $"Unexpected ternary-logic operation: {operation}.";

        try
        {
            AppContext.SetSwitch(switchName, false);
            var exception = Assert.Throws<InvalidOperationException>(
                () => TernaryLogicInfo.GetTernaryControlByte(operation, 0, 0));
            Assert.That(exception!.GetType(), Is.EqualTo(typeof(InvalidOperationException)));
            Assert.That(
                exception.Message,
                Is.EqualTo(Configuration.DETAILED_FATAL_ERROR_MESSAGES ? detailedMessage : "RJS-TL-001"));

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

    [Test]
    public static void UnreachableFailureKeepsItsTypeAndOptionalDetail()
    {
        const string switchName = "RyuJitSharp.DETAILED_FATAL_ERROR_MESSAGES";
        var hadOriginalValue = AppContext.TryGetSwitch(switchName, out var originalValue);

        try
        {
            AppContext.SetSwitch(switchName, false);
            var exception = Assert.Throws<UnreachableException>(
                () => JitExceptionHelpers.ThrowUnreachableException(
                    "RJS-VN-002",
                    Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "Detailed failure." : null));
            Assert.That(exception!.GetType(), Is.EqualTo(typeof(UnreachableException)));
            Assert.That(
                exception!.Message,
                Is.EqualTo(Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "Detailed failure." : "RJS-VN-002"));

            AppContext.SetSwitch(switchName, true);
            exception = Assert.Throws<UnreachableException>(
                () => JitExceptionHelpers.ThrowUnreachableException(
                    "RJS-VN-002",
                    Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "Detailed failure." : null));
            Assert.That(exception!.Message, Is.EqualTo("Detailed failure."));
        }
        finally
        {
            AppContext.SetSwitch(switchName, hadOriginalValue && originalValue);
        }
    }
}
