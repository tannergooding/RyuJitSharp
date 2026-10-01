// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_HW_INTRINSICS && TARGET_WASM
using System;

namespace RyuJitSharp;

public partial class Compiler
{
    private GenTree gtNewSimdWasmTwoSourceShuffleNode(var_types type, GenTree op1, GenTree op2,
        ReadOnlySpan<uint> selectors, var_types simdBaseType, byte simdSize)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Wasm two-source SIMD shuffle construction is not yet ported.");
    }
}
#endif
