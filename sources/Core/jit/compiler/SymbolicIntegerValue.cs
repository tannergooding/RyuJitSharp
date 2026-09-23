// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.

namespace RyuJitSharp;

public enum SymbolicIntegerValue
{
    LongMin,
    IntMin,
    ShortMin,
    ByteMin,
    Zero,
    One,
    ByteMax,
    UByteMax,
    ShortMax,
    UShortMax,
    ArrayLenMax,
    IntMax,
    UIntMax,
    LongMax,
}
