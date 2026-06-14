// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.HWIntrinsicFlag;
using System;

namespace RyuJitSharp;

[Flags]
public enum HWIntrinsicFlag
{
    HW_Flag_NoFlag = 0,

    // Commutative
    // - if a binary-op intrinsic is commutative (e.g., Add, Multiply), its op1 can be contained
    HW_Flag_Commutative = 0x1,

    // NoCodeGen
    // - should be transformed in the compiler front-end, cannot reach CodeGen
    HW_Flag_NoCodeGen = 0x2,

    // The intrinsic is invalid as the ID of a gtNode
    HW_Flag_InvalidNodeId = 0x4,

    // Select base type using the first argument type
    HW_Flag_BaseTypeFromFirstArg = 0x8,

    // Select base type using the second argument type
    HW_Flag_BaseTypeFromSecondArg = 0x10,

    // Indicates compFloatingPointUsed does not need to be set.
    HW_Flag_NoFloatingPointUsed = 0x20,

    // NoJmpTable IMM
    // the imm intrinsic does not need jumptable fallback when it gets non-const argument
    HW_Flag_NoJmpTableIMM = 0x40,

    // Special codegen
    // the intrinsics need special rules in CodeGen,
    // but may be table-driven in the front-end
    HW_Flag_SpecialCodeGen = 0x80,

    // Special import
    // the intrinsics need special rules in importer,
    // but may be table-driven in the back-end
    HW_Flag_SpecialImport = 0x100,

    // The intrinsic returns result in multiple registers.
    HW_Flag_MultiReg = 0x200,

    // The below is for defining platform-specific flags
#if TARGET_XARCH
    // Full range IMM intrinsic
    // - the immediate value is valid on the full range of imm8 (0-255)
    HW_Flag_FullRangeIMM = 0x400,

    // Maybe IMM
    // the intrinsic has either imm or Vector overloads
    HW_Flag_MaybeIMM = 0x800,

    // Copy Upper bits
    // some SIMD scalar intrinsics need the semantics of copying upper bits from the source operand
    HW_Flag_CopyUpperBits = 0x1000,

    // Maybe Memory Load/Store
    // - some intrinsics may have pointer overloads but without HW_Category_MemoryLoad/HW_Category_MemoryStore
    HW_Flag_MaybeMemoryLoad  = 0x2000,
    HW_Flag_MaybeMemoryStore = 0x4000,

    // No Read/Modify/Write Semantics
    // the intrinsic doesn't have read/modify/write semantics in two/three-operand form.
    HW_Flag_NoRMWSemantics = 0x8000,

    // NoContainment
    // the intrinsic cannot be handled by containment,
    // all the intrinsic that have explicit memory load/store semantics should have this flag
    HW_Flag_NoContainment = 0x10000,

    // Returns Per-Element Mask
    // the intrinsic returns a vector containing elements that are either "all bits set" or "all bits clear"
    // this output can be used as a per-element mask
    HW_Flag_ReturnsPerElementMask = 0x20000,

    // AvxOnlyCompatible
    // the intrinsic can be used on hardware with AVX but not AVX2 support
    HW_Flag_AvxOnlyCompatible = 0x40000,

    // MaybeCommutative
    // - if a binary-op intrinsic is maybe commutative (e.g., Max or Min for float/double), its op1 can possibly be
    // contained
    HW_Flag_MaybeCommutative = 0x80000,

    // The intrinsic has no EVEX compatible form
    HW_Flag_NoEvexSemantics = 0x100000,

    // The intrinsic is an RMW intrinsic
    HW_Flag_RmwIntrinsic = 0x200000,

    // The intrinsic is a PermuteVar2x intrinsic
    HW_Flag_PermuteVar2x = 0x400000,

    // UNUSED = 0x800000,

    // The intrinsic is an embedded rounding compatible intrinsic
    HW_Flag_EmbRoundingCompatible = 0x1000000,

    // UNUSED = 0x2000000,

    // The base type of this intrinsic needs to be normalized to int/uint unless it is long/ulong.
    HW_Flag_NormalizeSmallTypeToInt = 0x4000000,
#elif TARGET_ARM64
    // The intrinsic has an immediate operand
    // - the value can be (and should be) encoded in a corresponding instruction when the operand value is constant
    HW_Flag_HasImmediateOperand = 0x400,

    // The intrinsic has read/modify/write semantics in multiple-operands form.
    HW_Flag_HasRMWSemantics = 0x800,

    // The intrinsic operates on the lower part of a SIMD register
    // - the upper part of the source registers are ignored
    // - the upper part of the destination register is zeroed
    HW_Flag_SIMDScalar = 0x1000,

    // The intrinsic supports some sort of containment analysis
    HW_Flag_SupportsContainment = 0x2000,

    // The intrinsic needs consecutive registers
    HW_Flag_NeedsConsecutiveRegisters = 0x4000,

    // The intrinsic uses scalable registers
    HW_Flag_Scalable = 0x8000,

    // Returns Per-Element Mask
    // the intrinsic returns a vector containing elements that are either "all bits set" or "all bits clear"
    // this output can be used as a per-element mask
    HW_Flag_ReturnsPerElementMask = 0x10000,

    // The intrinsic uses a mask in arg1 to select elements present in the result
    HW_Flag_ExplicitMaskedOperation = 0x20000,

    // The intrinsic uses a mask in arg1 (either explicitly, embedded or optionally embedded) to select elements present
    // in the result, and must use a low register.
    HW_Flag_LowMaskedOperation = 0x40000,

    // The intrinsic can optionally use a mask in arg1 to select elements present in the result, which is not present in
    // the API call
    HW_Flag_OptionalEmbeddedMaskedOperation = 0x80000,

    // The intrinsic uses a mask in arg1 to select elements present in the result, which is not present in the API call
    HW_Flag_EmbeddedMaskedOperation = 0x100000,

    // The intrinsic comes in both vector and scalar variants. During the import stage if the basetype is scalar,
    // then the intrinsic should be switched to a scalar only version.
    HW_Flag_HasScalarInputVariant = 0x200000,

    // The intrinsic uses a mask in arg1 to select elements present in the result, and must use a low vector register.
    HW_Flag_LowVectorOperation = 0x400000,

    // The intrinsic uses a mask in arg1 to select elements present in the result, which zeros inactive elements
    // (instead of merging).
    HW_Flag_ZeroingMaskedOperation = 0x800000,

    // The intrinsic has an overload where the base type is extracted from a ValueTuple of SIMD types
    // (HW_Flag_BaseTypeFrom{First, Second}Arg must also be set to denote the position of the ValueTuple)
    HW_Flag_BaseTypeFromValueTupleArg = 0x1000000,

    // The intrinsic is a reduce operation.
    HW_Flag_ReduceOperation = 0x2000000,

    // This intrinsic could be implemented with another intrinsic when it is operating on operands that are all of
    // type TYP_MASK, and this other intrinsic will produces a value of this type. Used in morph to convert vector
    // operations into mask operations when the intrinsic is operating on mask vectors (mainly bitwise operations).
    HW_Flag_HasAllMaskVariant = 0x4000000,

#else
#error Unsupported platform
#endif

    // The intrinsic has some barrier special side effect that should be tracked
    HW_Flag_SpecialSideEffect_Barrier = 0x8000000,

    // The intrinsic has some other special side effect that should be tracked
    HW_Flag_SpecialSideEffect_Other = 0x10000000,

    HW_Flag_SpecialSideEffectMask = (HW_Flag_SpecialSideEffect_Barrier | HW_Flag_SpecialSideEffect_Other),

    // MaybeNoJmpTable IMM
    // the imm intrinsic may not need jumptable fallback when it gets non-const argument
    HW_Flag_MaybeNoJmpTableIMM = 0x20000000,

    // The intrinsic is a FusedMultiplyAdd intrinsic
    HW_Flag_FmaIntrinsic = 0x40000000,

    HW_Flag_CanBenefitFromConstantProp = unchecked((int)(0x80000000)),
}
