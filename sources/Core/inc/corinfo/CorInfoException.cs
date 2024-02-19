// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.CorInfoException;

namespace RyuJitSharp;

/// <summary>This enumeration is passed to <c>InternalThrow</c>.</summary>
public enum CorInfoException
{
    CORINFO_NullReferenceException,

    CORINFO_DivideByZeroException,

    CORINFO_InvalidCastException,

    CORINFO_IndexOutOfRangeException,

    CORINFO_OverflowException,

    CORINFO_SynchronizationLockException,

    CORINFO_ArrayTypeMismatchException,

    CORINFO_RankException,

    CORINFO_ArgumentNullException,

    CORINFO_ArgumentException,

    CORINFO_Exception_Count,
}
