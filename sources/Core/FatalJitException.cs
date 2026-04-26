// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed class FatalJitException : SystemException
{
    public FatalJitException() : this(message: null, innerException: null)
    {
    }

    public FatalJitException(string? message) : this(message, innerException: null)
    {
    }

    public FatalJitException(string? message, Exception? innerException) : base(message, innerException)
    {
        HResult = FATAL_JIT_EXCEPTION;
    }
}
