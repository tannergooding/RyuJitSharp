// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
namespace RyuJitSharp;

// Distinguishes encoding variants that cannot be determined solely by register usage.
public enum insScalableOpts : uint
{
    INS_SCALABLE_OPTS_NONE,
    INS_SCALABLE_OPTS_WIDE,
    INS_SCALABLE_OPTS_WITH_SIMD_SCALAR,
    INS_SCALABLE_OPTS_PREDICATE_MERGE,
    INS_SCALABLE_OPTS_WITH_PREDICATE_PAIR,
    INS_SCALABLE_OPTS_VL_2X,
    INS_SCALABLE_OPTS_VL_4X,
    INS_SCALABLE_OPTS_LSL_N,
    INS_SCALABLE_OPTS_MOD_N,
    INS_SCALABLE_OPTS_WITH_VECTOR_PAIR,
    INS_SCALABLE_OPTS_IMM_BITMASK,
    INS_SCALABLE_OPTS_IMM_FIRST,
}
#endif
