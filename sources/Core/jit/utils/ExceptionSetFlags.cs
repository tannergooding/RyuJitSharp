// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

[Flags]
public enum ExceptionSetFlags : byte
{
    None = 0,
    OverflowException = 1 << 0,
    DivideByZeroException = 1 << 1,
    ArithmeticException = 1 << 2,
    NullReferenceException = 1 << 3,
    IndexOutOfRangeException = 1 << 4,
    UnknownException = 1 << 5,
}
