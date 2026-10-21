// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
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
        const string missingRegisterBankMessage = "LSRA has no register bank for test register type.";
        const string unexpectedReferenceKindMessage = "Unexpected reference kind during register resolution: test-kind.";
        var cases = new (FatalJitErrorCode ErrorCode, string Message, Action Throw)[]
        {
            (
                FatalJitErrorCode.UnsupportedWriteBarrierForm,
                $"No write-barrier helper exists for {writeBarrierForm}.",
                () => FatalJitException.ThrowFatalJitException(
                    FatalJitErrorCode.UnsupportedWriteBarrierForm,
                    Configuration.DETAILED_FATAL_ERROR_MESSAGES
                        ? $"No write-barrier helper exists for {writeBarrierForm}."
                        : null)),
            (
                FatalJitErrorCode.UnexpectedDataSectionType,
                $"Unexpected data-section type {sectionType}.",
                () => FatalJitException.ThrowFatalJitException(
                    FatalJitErrorCode.UnexpectedDataSectionType,
                    Configuration.DETAILED_FATAL_ERROR_MESSAGES
                        ? $"Unexpected data-section type {sectionType}."
                        : null)),
            (
                FatalJitErrorCode.UnexpectedDataSectionElementSize,
                $"Unexpected data-section element size {uint.MaxValue}.",
                () => FatalJitException.ThrowFatalJitException(
                    FatalJitErrorCode.UnexpectedDataSectionElementSize,
                    Configuration.DETAILED_FATAL_ERROR_MESSAGES
                        ? $"Unexpected data-section element size {uint.MaxValue}."
                        : null)),
            (
                FatalJitErrorCode.UnsupportedInitializationBlockKind,
                $"Unsupported initialization block kind {blockKind}.",
                () => FatalJitException.ThrowFatalJitException(
                    FatalJitErrorCode.UnsupportedInitializationBlockKind,
                    Configuration.DETAILED_FATAL_ERROR_MESSAGES
                        ? $"Unsupported initialization block kind {blockKind}."
                        : null)),
            (
                FatalJitErrorCode.UnsupportedCopyBlockKind,
                $"Unsupported copy block kind {blockKind}.",
                () => FatalJitException.ThrowFatalJitException(
                    FatalJitErrorCode.UnsupportedCopyBlockKind,
                    Configuration.DETAILED_FATAL_ERROR_MESSAGES
                        ? $"Unsupported copy block kind {blockKind}."
                        : null)),
            (
                FatalJitErrorCode.UnsupportedStackArgumentKind,
                $"Unsupported stack argument kind {stackArgumentKind}.",
                () => FatalJitException.ThrowFatalJitException(
                    FatalJitErrorCode.UnsupportedStackArgumentKind,
                    Configuration.DETAILED_FATAL_ERROR_MESSAGES
                        ? $"Unsupported stack argument kind {stackArgumentKind}."
                        : null)),
            (
                FatalJitErrorCode.MissingRegisterBank,
                missingRegisterBankMessage,
                () => FatalJitException.ThrowFatalJitException(
                    FatalJitErrorCode.MissingRegisterBank,
                    Configuration.DETAILED_FATAL_ERROR_MESSAGES
                        ? missingRegisterBankMessage
                        : null)),
            (
                FatalJitErrorCode.UnexpectedReferenceKind,
                unexpectedReferenceKindMessage,
                () => FatalJitException.ThrowFatalJitException(
                    FatalJitErrorCode.UnexpectedReferenceKind,
                    Configuration.DETAILED_FATAL_ERROR_MESSAGES
                        ? unexpectedReferenceKindMessage
                        : null)),
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

            for (var errorCodeValue = (int)FatalJitErrorCode.BoundJumpMissingTarget;
                errorCodeValue <= (int)FatalJitErrorCode.UnexpectedReferenceKind;
                errorCodeValue++)
            {
                var errorCode = (FatalJitErrorCode)errorCodeValue;
                Assert.That(Enum.IsDefined(errorCode), Is.True);
                var result = errorCode is FatalJitErrorCode.UnresolvedRemovableJumpTarget
                    or FatalJitErrorCode.UnsupportedRexWxInstruction
                    ? CorJitResult.CORJIT_SKIPPED
                    : CorJitResult.CORJIT_INTERNALERROR;
                var exception = Assert.Throws<FatalJitException>(
                    () => FatalJitException.ThrowFatalJitException(result, errorCode))!;
                Assert.That(exception.Result, Is.EqualTo(result));
                Assert.That(exception.ErrorCode, Is.EqualTo(errorCode));
                if (TestConfiguration.IsDebug)
                {
                    Assert.That(exception.Message, Is.Not.EqualTo(unspecifiedMessage));
                }
                else
                {
                    Assert.That(exception.Message, Is.EqualTo(unspecifiedMessage));
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

            for (var errorCodeValue = (int)FatalJitErrorCode.BoundJumpMissingTarget;
                errorCodeValue <= (int)FatalJitErrorCode.UnexpectedReferenceKind;
                errorCodeValue++)
            {
                var errorCode = (FatalJitErrorCode)errorCodeValue;
                var result = errorCode is FatalJitErrorCode.UnresolvedRemovableJumpTarget
                    or FatalJitErrorCode.UnsupportedRexWxInstruction
                    ? CorJitResult.CORJIT_SKIPPED
                    : CorJitResult.CORJIT_INTERNALERROR;
                var exception = ThrowAndCapture(result, errorCode);
                Assert.That(exception.Result, Is.EqualTo(result));
                Assert.That(exception.ErrorCode, Is.EqualTo(errorCode));
                Assert.That(exception.Message, Is.Not.EqualTo(unspecifiedMessage));
            }

            var resultCases = new[]
            {
                (CorJitResult.CORJIT_INTERNALERROR, FatalJitErrorCode.DirectFailureMessage077),
                (CorJitResult.CORJIT_INTERNALERROR, FatalJitErrorCode.DirectFailureMessage079),
                (CorJitResult.CORJIT_SKIPPED, FatalJitErrorCode.DirectFailureMessage079),
                (CorJitResult.CORJIT_IMPLLIMITATION, FatalJitErrorCode.DirectFailureMessage198),
                (CorJitResult.CORJIT_RECOVERABLEERROR, FatalJitErrorCode.DirectFailureMessage239),
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

    private static FatalJitException ThrowAndCapture(CorJitResult result, FatalJitErrorCode errorCode)
        => Assert.Throws<FatalJitException>(
            () => FatalJitException.ThrowFatalJitException(result, errorCode))!;
}
