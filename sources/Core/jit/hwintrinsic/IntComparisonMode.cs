// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public enum IntComparisonMode : byte
{
    Equal = 0,
    LessThan = 1,
    LessThanOrEqual = 2,
    False = 3,
    NotEqual = 4,
    GreaterThanOrEqual = 5,
    GreaterThan = 6,
    True = 7,

    NotGreaterThanOrEqual = LessThan,
    NotGreaterThan = LessThanOrEqual,
    NotLessThan = GreaterThanOrEqual,
    NotLessThanOrEqual = GreaterThan,
}
