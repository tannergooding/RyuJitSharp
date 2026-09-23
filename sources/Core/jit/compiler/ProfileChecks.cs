// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

[Flags]
public enum ProfileChecks : uint
{
    CHECK_NONE = 0,
    CHECK_HASLIKELIHOOD = 1 << 0,
    CHECK_LIKELIHOODSUM = 1 << 1,
    CHECK_LIKELY = 1 << 2,
    CHECK_FLAGS = 1 << 3,
    RAISE_ASSERT = 1 << 4,
    CHECK_ALL_BLOCKS = 1 << 5,
}
