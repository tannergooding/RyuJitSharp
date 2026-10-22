// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Collections.Generic;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace RyuJitSharp;

internal static class JitExceptionHelpers
{
    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowArgumentException(FatalJitErrorCode errorCode)
        => throw new ArgumentException(FatalJitException.GetOptionalFailureMessage(errorCode));

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowArgumentException(FatalJitArgument parameterName)
        => throw new ArgumentException(
            message: null,
            paramName: FatalJitException.GetOptionalArgumentName(parameterName));

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowArgumentException(FatalJitErrorCode errorCode, FatalJitArgument parameterName)
        => throw new ArgumentException(
            FatalJitException.GetOptionalFailureMessage(errorCode),
            FatalJitException.GetOptionalArgumentName(parameterName));

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowArgumentExceptionWithMessage<T0>(FatalJitErrorCode errorCode, T0 argument0)
        => throw new ArgumentException(FatalJitException.GetOptionalFailureMessage(errorCode, argument0));

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowArgumentExceptionWithMessage<T0, T1>(
        FatalJitErrorCode errorCode,
        T0 argument0,
        T1 argument1)
        => throw new ArgumentException(FatalJitException.GetOptionalFailureMessage(errorCode, argument0, argument1));

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowArgumentExceptionWithParameterNameAndMessage<T0>(
        FatalJitErrorCode errorCode,
        FatalJitArgument parameterName,
        T0 argument0)
        => throw new ArgumentException(
            FatalJitException.GetOptionalFailureMessage(errorCode, argument0),
            FatalJitException.GetOptionalArgumentName(parameterName));

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowArgumentExceptionWithParameterNameAndMessage<T0, T1>(
        FatalJitErrorCode errorCode,
        FatalJitArgument parameterName,
        T0 argument0,
        T1 argument1)
        => throw new ArgumentException(
            FatalJitException.GetOptionalFailureMessage(errorCode, argument0, argument1),
            FatalJitException.GetOptionalArgumentName(parameterName));

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowArgumentNullException(FatalJitArgument parameterName)
        => throw new ArgumentNullException(FatalJitException.GetOptionalArgumentName(parameterName));

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowArgumentNullException(FatalJitErrorCode errorCode, FatalJitArgument parameterName)
        => throw new ArgumentNullException(
            FatalJitException.GetOptionalArgumentName(parameterName),
            FatalJitException.GetOptionalFailureMessage(errorCode));

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowArgumentNullExceptionWithMessage<T0>(
        FatalJitErrorCode errorCode,
        FatalJitArgument parameterName,
        T0 argument0)
        => throw new ArgumentNullException(
            FatalJitException.GetOptionalArgumentName(parameterName),
            FatalJitException.GetOptionalFailureMessage(errorCode, argument0));

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowArgumentNullExceptionWithMessage<T0, T1>(
        FatalJitErrorCode errorCode,
        FatalJitArgument parameterName,
        T0 argument0,
        T1 argument1)
        => throw new ArgumentNullException(
            FatalJitException.GetOptionalArgumentName(parameterName),
            FatalJitException.GetOptionalFailureMessage(errorCode, argument0, argument1));

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowArgumentOutOfRangeException(FatalJitArgument parameterName)
        => throw new ArgumentOutOfRangeException(FatalJitException.GetOptionalArgumentName(parameterName));

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowArgumentOutOfRangeException(FatalJitErrorCode errorCode, FatalJitArgument parameterName)
        => throw new ArgumentOutOfRangeException(
            FatalJitException.GetOptionalArgumentName(parameterName),
            FatalJitException.GetOptionalFailureMessage(errorCode));

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowArgumentOutOfRangeExceptionWithMessage<T0>(
        FatalJitErrorCode errorCode,
        FatalJitArgument parameterName,
        T0 argument0)
        => throw new ArgumentOutOfRangeException(
            FatalJitException.GetOptionalArgumentName(parameterName),
            FatalJitException.GetOptionalFailureMessage(errorCode, argument0));

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowArgumentOutOfRangeExceptionWithMessage<T0, T1>(
        FatalJitErrorCode errorCode,
        FatalJitArgument parameterName,
        T0 argument0,
        T1 argument1)
        => throw new ArgumentOutOfRangeException(
            FatalJitException.GetOptionalArgumentName(parameterName),
            FatalJitException.GetOptionalFailureMessage(errorCode, argument0, argument1));

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowFormatException(FatalJitErrorCode errorCode)
        => throw new FormatException(FatalJitException.GetOptionalFailureMessage(errorCode));

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowFormatException<T0>(FatalJitErrorCode errorCode, T0 argument0)
        => throw new FormatException(FatalJitException.GetOptionalFailureMessage(errorCode, argument0));

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowFormatException<T0, T1>(
        FatalJitErrorCode errorCode,
        T0 argument0,
        T1 argument1)
        => throw new FormatException(FatalJitException.GetOptionalFailureMessage(errorCode, argument0, argument1));

    [DoesNotReturn]
    [StackTraceHidden]
    [SuppressMessage("Reliability", "CA2201:Do not raise reserved exception types", Justification = "Preserve the JIT's invalid-index exception contract.")]
    internal static void ThrowIndexOutOfRangeException(FatalJitErrorCode errorCode)
        => throw new IndexOutOfRangeException(FatalJitException.GetOptionalFailureMessage(errorCode));

    [DoesNotReturn]
    [StackTraceHidden]
    [SuppressMessage("Reliability", "CA2201:Do not raise reserved exception types", Justification = "Preserve the JIT's invalid-index exception contract.")]
    internal static void ThrowIndexOutOfRangeException()
        => throw new IndexOutOfRangeException();

    [DoesNotReturn]
    [StackTraceHidden]
    [SuppressMessage("Reliability", "CA2201:Do not raise reserved exception types", Justification = "Preserve the JIT's invalid-index exception contract.")]
    internal static void ThrowIndexOutOfRangeException<T0>(FatalJitErrorCode errorCode, T0 argument0)
        => throw new IndexOutOfRangeException(FatalJitException.GetOptionalFailureMessage(errorCode, argument0));

    [DoesNotReturn]
    [StackTraceHidden]
    [SuppressMessage("Reliability", "CA2201:Do not raise reserved exception types", Justification = "Preserve the JIT's invalid-index exception contract.")]
    internal static void ThrowIndexOutOfRangeException<T0, T1>(
        FatalJitErrorCode errorCode,
        T0 argument0,
        T1 argument1)
        => throw new IndexOutOfRangeException(FatalJitException.GetOptionalFailureMessage(errorCode, argument0, argument1));

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowInvalidOperationException(FatalJitErrorCode errorCode)
        => throw new InvalidOperationException(FatalJitException.GetOptionalFailureMessage(errorCode));

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowInvalidOperationException()
        => throw new InvalidOperationException();

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowInvalidOperationException<T0>(FatalJitErrorCode errorCode, T0 argument0)
        => throw new InvalidOperationException(FatalJitException.GetOptionalFailureMessage(errorCode, argument0));

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowInvalidOperationException<T0, T1>(
        FatalJitErrorCode errorCode,
        T0 argument0,
        T1 argument1)
        => throw new InvalidOperationException(FatalJitException.GetOptionalFailureMessage(errorCode, argument0, argument1));

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowKeyNotFoundException(FatalJitErrorCode errorCode)
        => throw new KeyNotFoundException(FatalJitException.GetOptionalFailureMessage(errorCode));

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowKeyNotFoundException()
        => throw new KeyNotFoundException();

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowKeyNotFoundException<T0>(FatalJitErrorCode errorCode, T0 argument0)
        => throw new KeyNotFoundException(FatalJitException.GetOptionalFailureMessage(errorCode, argument0));

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowKeyNotFoundException<T0, T1>(
        FatalJitErrorCode errorCode,
        T0 argument0,
        T1 argument1)
        => throw new KeyNotFoundException(FatalJitException.GetOptionalFailureMessage(errorCode, argument0, argument1));

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowNotImplementedException(FatalJitErrorCode errorCode)
        => throw new NotImplementedException(FatalJitException.GetOptionalFailureMessage(errorCode));

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowNotImplementedException<T0>(FatalJitErrorCode errorCode, T0 argument0)
        => throw new NotImplementedException(FatalJitException.GetOptionalFailureMessage(errorCode, argument0));

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowNotImplementedException<T0, T1>(
        FatalJitErrorCode errorCode,
        T0 argument0,
        T1 argument1)
        => throw new NotImplementedException(FatalJitException.GetOptionalFailureMessage(errorCode, argument0, argument1));

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowNotSupportedException(FatalJitErrorCode errorCode)
        => throw new NotSupportedException(FatalJitException.GetOptionalFailureMessage(errorCode));

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowNotSupportedException<T0>(FatalJitErrorCode errorCode, T0 argument0)
        => throw new NotSupportedException(FatalJitException.GetOptionalFailureMessage(errorCode, argument0));

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowNotSupportedException<T0, T1>(
        FatalJitErrorCode errorCode,
        T0 argument0,
        T1 argument1)
        => throw new NotSupportedException(FatalJitException.GetOptionalFailureMessage(errorCode, argument0, argument1));

    [DoesNotReturn]
    [StackTraceHidden]
    [SuppressMessage("Reliability", "CA2201:Do not raise reserved exception types", Justification = "Preserve the native disassembler's allocation-failure contract.")]
    internal static void ThrowOutOfMemoryException(FatalJitErrorCode errorCode)
        => throw new OutOfMemoryException(FatalJitException.GetOptionalFailureMessage(errorCode));

    [DoesNotReturn]
    [StackTraceHidden]
    [SuppressMessage("Reliability", "CA2201:Do not raise reserved exception types", Justification = "Preserve the native disassembler's allocation-failure contract.")]
    internal static void ThrowOutOfMemoryException()
        => throw new OutOfMemoryException();

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowPlatformNotSupportedException(FatalJitErrorCode errorCode)
        => throw new PlatformNotSupportedException(FatalJitException.GetOptionalFailureMessage(errorCode));

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowPlatformNotSupportedException<T0>(FatalJitErrorCode errorCode, T0 argument0)
        => throw new PlatformNotSupportedException(FatalJitException.GetOptionalFailureMessage(errorCode, argument0));

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowPlatformNotSupportedException<T0, T1>(
        FatalJitErrorCode errorCode,
        T0 argument0,
        T1 argument1)
        => throw new PlatformNotSupportedException(FatalJitException.GetOptionalFailureMessage(errorCode, argument0, argument1));

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowUnreachableException(FatalJitErrorCode errorCode)
        => throw new UnreachableException(FatalJitException.GetOptionalFailureMessage(errorCode));

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowUnreachableException()
        => throw new UnreachableException();

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowUnreachableException<T0>(FatalJitErrorCode errorCode, T0 argument0)
        => throw new UnreachableException(FatalJitException.GetOptionalFailureMessage(errorCode, argument0));

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowUnreachableException<T0, T1>(
        FatalJitErrorCode errorCode,
        T0 argument0,
        T1 argument1)
        => throw new UnreachableException(FatalJitException.GetOptionalFailureMessage(errorCode, argument0, argument1));
}
