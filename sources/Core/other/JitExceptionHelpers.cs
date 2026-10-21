// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace RyuJitSharp;

internal static class JitExceptionHelpers
{
    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowInvalidOperationException(string? detailedMessage)
        => throw new InvalidOperationException(detailedMessage);

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowInvalidOperationException(string failureId, string? detailedMessage)
    {
        throw new InvalidOperationException(detailedMessage ?? failureId);
    }

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowArgumentException(string? detailedMessage)
        => throw new ArgumentException(detailedMessage);

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowUnreachableException(string failureId, string? detailedMessage)
    {
        throw new UnreachableException(detailedMessage ?? failureId);
    }

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowUnreachableException()
        => throw new UnreachableException();

    [DoesNotReturn]
    [StackTraceHidden]
    [SuppressMessage("Usage", "CA2201", Justification = "Preserve the compiler's bounds-failure exception contract.")]
    internal static void ThrowIndexOutOfRangeException()
    {
        throw new IndexOutOfRangeException();
    }
}
