// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Linq;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class FatalJitExceptionMessageTests
{
    [Test]
    public static void RootedFatalMessagesKeepStableCodesAndAreOptional()
    {
        const string switchName = "RyuJitSharp.DETAILED_FATAL_ERROR_MESSAGES";
        var hadOriginalValue = AppContext.TryGetSwitch(switchName, out var originalValue);
        var unspecifiedMessage = new FatalJitException().Message;
        var writeBarrierForm = (GCInfo.WriteBarrierForm)int.MaxValue;
        var sectionType = (Emitter.dataSection.sectionType)int.MaxValue;
        var blockKind = (GenTreeBlk.BlkOpKind)int.MaxValue;
        var stackArgumentKind = unchecked((GenTreePutArgStk.Kind)(-1));
        var missingRegisterBankType = var_types.TYP_INT;
        var missingRegisterBankMessage = $"LSRA has no register bank for {missingRegisterBankType}.";
        var unexpectedReferenceKind = RefType.RefTypeUse;
        var unexpectedReferenceKindMessage = $"Unexpected reference kind during register resolution: {unexpectedReferenceKind}.";
        var cases = new (FatalJitErrorCode ErrorCode, string Message, Action Throw)[]
        {
            (
                FatalJitErrorCode.UnsupportedWriteBarrierForm,
                $"No write-barrier helper exists for {writeBarrierForm}.",
                () => FatalJitException.ThrowFatalJitException(
                    FatalJitErrorCode.UnsupportedWriteBarrierForm,
                    Configuration.DETAILED_FATAL_ERROR_MESSAGES ? writeBarrierForm : default)),
            (
                FatalJitErrorCode.UnexpectedDataSectionType,
                $"Unexpected data-section type {sectionType}.",
                () => FatalJitException.ThrowFatalJitException(
                    FatalJitErrorCode.UnexpectedDataSectionType,
                    Configuration.DETAILED_FATAL_ERROR_MESSAGES ? sectionType : default)),
            (
                FatalJitErrorCode.UnexpectedDataSectionElementSize,
                $"Unexpected data-section element size {uint.MaxValue}.",
                () => FatalJitException.ThrowFatalJitException(
                    FatalJitErrorCode.UnexpectedDataSectionElementSize,
                    Configuration.DETAILED_FATAL_ERROR_MESSAGES ? uint.MaxValue : default)),
            (
                FatalJitErrorCode.UnsupportedInitializationBlockKind,
                $"Unsupported initialization block kind {blockKind}.",
                () => FatalJitException.ThrowFatalJitException(
                    FatalJitErrorCode.UnsupportedInitializationBlockKind,
                    Configuration.DETAILED_FATAL_ERROR_MESSAGES ? blockKind : default)),
            (
                FatalJitErrorCode.UnsupportedCopyBlockKind,
                $"Unsupported copy block kind {blockKind}.",
                () => FatalJitException.ThrowFatalJitException(
                    FatalJitErrorCode.UnsupportedCopyBlockKind,
                    Configuration.DETAILED_FATAL_ERROR_MESSAGES ? blockKind : default)),
            (
                FatalJitErrorCode.UnsupportedStackArgumentKind,
                $"Unsupported stack argument kind {stackArgumentKind}.",
                () => FatalJitException.ThrowFatalJitException(
                    FatalJitErrorCode.UnsupportedStackArgumentKind,
                    Configuration.DETAILED_FATAL_ERROR_MESSAGES ? stackArgumentKind : default)),
            (
                FatalJitErrorCode.MissingRegisterBank,
                missingRegisterBankMessage,
                () => FatalJitException.ThrowFatalJitException(
                    FatalJitErrorCode.MissingRegisterBank,
                    Configuration.DETAILED_FATAL_ERROR_MESSAGES ? missingRegisterBankType : default)),
            (
                FatalJitErrorCode.UnexpectedReferenceKind,
                unexpectedReferenceKindMessage,
                () => FatalJitException.ThrowFatalJitException(
                    FatalJitErrorCode.UnexpectedReferenceKind,
                    Configuration.DETAILED_FATAL_ERROR_MESSAGES ? unexpectedReferenceKind : default)),
        };

        try
        {
            AppContext.SetSwitch(switchName, false);
            foreach (var (errorCode, message, throwException) in cases)
            {
                var exception = Assert.Throws<FatalJitException>(() => throwException())!;
                Assert.That(exception.Result, Is.EqualTo(CorJitResult.CORJIT_INTERNALERROR));
                Assert.That(exception.ErrorCode, Is.EqualTo(errorCode));
                if (TestConfiguration.IsDebug)
                {
                    Assert.That(exception.Message, Does.Contain(message));
                }
                else
                {
                    Assert.That(exception.Message, Does.Not.Contain(message));
                }
            }

            foreach (var errorCode in Enum.GetValues<FatalJitErrorCode>())
            {
                if (errorCode == FatalJitErrorCode.Unspecified)
                {
                    continue;
                }

                var exception = Assert.Throws<FatalJitException>(
                    () => FatalJitException.ThrowFatalJitException(errorCode))!;
                Assert.That(exception.Result, Is.EqualTo(CorJitResult.CORJIT_INTERNALERROR));
                Assert.That(exception.ErrorCode, Is.EqualTo(errorCode));
                if (TestConfiguration.IsDebug)
                {
                    Assert.That(exception.Message, Is.Not.EqualTo(unspecifiedMessage));
                }
                else
                {
                    Assert.That(exception.Message, Is.EqualTo($"RJS-{(int)errorCode:D4}"));
                }
            }

            AppContext.SetSwitch(switchName, true);
            foreach (var (errorCode, message, throwException) in cases)
            {
                var exception = Assert.Throws<FatalJitException>(() => throwException())!;
                Assert.That(exception.Result, Is.EqualTo(CorJitResult.CORJIT_INTERNALERROR));
                Assert.That(exception.ErrorCode, Is.EqualTo(errorCode));
                Assert.That(exception.Message, Does.Contain(message));
            }

            foreach (var errorCode in Enum.GetValues<FatalJitErrorCode>())
            {
                if (errorCode == FatalJitErrorCode.Unspecified)
                {
                    continue;
                }

                var exception = ThrowAndCapture(CorJitResult.CORJIT_INTERNALERROR, errorCode);
                Assert.That(exception.Result, Is.EqualTo(CorJitResult.CORJIT_INTERNALERROR));
                Assert.That(exception.ErrorCode, Is.EqualTo(errorCode));
                Assert.That(exception.Message, Is.Not.EqualTo(unspecifiedMessage));
            }

            var resultCases = new[]
            {
                (CorJitResult.CORJIT_INTERNALERROR, FatalJitErrorCode.InvalidABIClassifierKind),
                (CorJitResult.CORJIT_INTERNALERROR, FatalJitErrorCode.EHToVMClauseOrderHasNotBeenInitialized),
                (CorJitResult.CORJIT_SKIPPED, FatalJitErrorCode.EHToVMClauseOrderHasNotBeenInitialized),
                (CorJitResult.CORJIT_IMPLLIMITATION, FatalJitErrorCode.OnlySIMDORAndXOROperationsAreSupported),
                (CorJitResult.CORJIT_RECOVERABLEERROR, FatalJitErrorCode.UnexpectedInlineDecision),
            };
            foreach (var (result, errorCode) in resultCases)
            {
                var exception = ThrowAndCapture(result, errorCode);
                Assert.That(exception.Result, Is.EqualTo(result));
                Assert.That(exception.ErrorCode, Is.EqualTo(errorCode));
                Assert.That(exception.Message, Is.Not.EqualTo(unspecifiedMessage));
            }

            var skippedException = ThrowAndCapture(
                CorJitResult.CORJIT_SKIPPED,
                FatalJitErrorCode.UnresolvedRemovableJumpTarget);
            Assert.That(
                skippedException.Message,
                Is.EqualTo("A removable jump requires a resolved basic-block label cookie."));

            var internalErrorException = ThrowAndCapture(
                CorJitResult.CORJIT_INTERNALERROR,
                FatalJitErrorCode.DataSectionOutputMissingChunksOrSections);
            Assert.That(
                internalErrorException.Message,
                Is.EqualTo("Data-section output requires allocated chunks and nonempty sections."));
        }
        finally
        {
            AppContext.SetSwitch(switchName, hadOriginalValue && originalValue);
        }
    }

    [Test]
    public static void ErrorCodesDoNotIncludeMessageFreeOrLineSpecificIdentifiers()
    {
        var names = Enum.GetNames<FatalJitErrorCode>();

        Assert.That(
            names.Any(name => name.StartsWith("NoMessage", StringComparison.Ordinal)),
            Is.False);
        Assert.That(
            names.Any(name => System.Text.RegularExpressions.Regex.IsMatch(
                name,
                "Line[0-9]+$",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant)),
            Is.False);
    }

    private static FatalJitException ThrowAndCapture(CorJitResult result, FatalJitErrorCode errorCode)
        => Assert.Throws<FatalJitException>(
            () => FatalJitException.ThrowFatalJitException(result, errorCode))!;
}
