// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.HWIntrinsicCategory;

namespace RyuJitSharp;

public enum HWIntrinsicCategory : byte
{
#if TARGET_XARCH
    // Simple SIMD intrinsics
    // - take Vector128/256<T> parameters
    // - return a Vector128/256<T>
    // - the codegen of overloads can be determined by intrinsicID and base type of returned vector
    HW_Category_SimpleSIMD,

    // IMM intrinsics
    // - some SIMD intrinsics requires immediate value (i.e. imm8) to generate instruction
    HW_Category_IMM,

    // Scalar intrinsics
    // - operate over general purpose registers, like crc32, lzcnt, popcnt, etc.
    HW_Category_Scalar,

    // SIMD scalar
    // - operate over vector registers(XMM), but just compute on the first element
    HW_Category_SIMDScalar,

    // Memory access intrinsics
    // - e.g., Avx.Load, Avx.Store, Sse.LoadAligned
    HW_Category_MemoryLoad,
    HW_Category_MemoryStore,

    // Helper intrinsics
    // - do not directly correspond to a instruction, such as Avx.SetAllVector256
    HW_Category_Helper,

    // Special intrinsics
    // - have to be addressed specially
    HW_Category_Special
#elif TARGET_ARM64
    // Most of the Arm64 intrinsic fall into SIMD category:
    // - vector or scalar intrinsics that operate on one-or-many SIMD registers
    HW_Category_SIMD,

    // Scalar intrinsics operate on general purpose registers (e.g. cls, clz, rbit)
    HW_Category_Scalar,

    // Memory access intrinsics
    HW_Category_MemoryLoad,
    HW_Category_MemoryStore,

    // These are Arm64 that share some features in a given category (e.g. immediate operand value range)
    HW_Category_ShiftLeftByImmediate,
    HW_Category_ShiftRightByImmediate,
    HW_Category_SIMDByIndexedElement,

    // Helper intrinsics
    // - do not directly correspond to a instruction, such as Vector64.AllBitsSet
    HW_Category_Helper,

    // Special intrinsics
    // - have to be addressed specially
    HW_Category_Special
#else
#error Unsupported platform
#endif
}
