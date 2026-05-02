// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.genTreeOps;

namespace RyuJitSharp;

public enum genTreeOps
{
    GT_NONE,

    GT_PHI,

    GT_PHI_ARG,

    GT_LCL_VAR,

    GT_LCL_FLD,

    GT_STORE_LCL_VAR,

    GT_STORE_LCL_FLD,

    GT_LCL_ADDR,

    GT_CATCH_ARG,

    GT_ASYNC_CONTINUATION,

    GT_LABEL,

    GT_JMP,

    GT_FTN_ADDR,

    GT_RET_EXPR,

    GT_GCPOLL,

    GT_ASYNC_RESUME_INFO,

    GT_CNS_INT,

    GT_CNS_LNG,

    GT_CNS_DBL,

    GT_CNS_STR,

#if FEATURE_SIMD
    GT_CNS_VEC,
#endif

#if FEATURE_MASKED_HW_INTRINSICS
    GT_CNS_MSK,
#endif

    GT_NOP,

    GT_INTRINSIC,

    GT_KEEPALIVE,

    GT_CAST,

    GT_BITCAST,

    GT_CKFINITE,

    GT_LCLHEAP,

    GT_BOUNDS_CHECK,

    GT_MEMORYBARRIER,

    GT_LOCKADD,

    GT_XAND,

    GT_XORR,

    GT_XADD,

    GT_XCHG,

    GT_CMPXCHG,

    GT_IND,

    GT_STOREIND,

    GT_BLK,

    GT_STORE_BLK,

    GT_NULLCHECK,

    GT_ARR_LENGTH,

    GT_MDARR_LENGTH,

    GT_MDARR_LOWER_BOUND,

    GT_FIELD_ADDR,

    GT_ALLOCOBJ,

    GT_INIT_VAL,

    GT_BOX,

    GT_RUNTIMELOOKUP,

    GT_ARR_ADDR,

    GT_BSWAP,

    GT_BSWAP16,

    GT_LZCNT,

    GT_NOT,

    GT_NEG,

    GT_OR,

    GT_XOR,

    GT_AND,

    GT_LSH,

    GT_RSH,

    GT_RSZ,

    GT_ROL,

    GT_ROR,

    GT_ADD,

    GT_SUB,

    GT_MUL,

    GT_DIV,

    GT_MOD,

    GT_UDIV,

    GT_UMOD,

    GT_EQ,

    GT_NE,

    GT_LT,

    GT_LE,

    GT_GE,

    GT_GT,

    GT_TEST_EQ,

    GT_TEST_NE,

#if TARGET_XARCH
    GT_BITTEST_EQ,

    GT_BITTEST_NE,
#endif

    GT_SELECT,

    GT_COMMA,

    GT_QMARK,

    GT_COLON,

    GT_INDEX_ADDR,

    GT_LEA,

#if !TARGET_64BIT
    GT_LONG,

    GT_ADD_LO,

    GT_ADD_HI,

    GT_SUB_LO,

    GT_SUB_HI,

    GT_LSH_HI,

    GT_RSH_LO,
#endif

#if FEATURE_HW_INTRINSICS
    GT_HWINTRINSIC,
#endif

    GT_INC_SATURATE,

    GT_MULHI,

#if !TARGET_64BIT || TARGET_ARM64
    GT_MUL_LONG,
#endif

    GT_AND_NOT,

    GT_OR_NOT,

    GT_XOR_NOT,

#if TARGET_ARM64
    GT_BFIZ,
#endif

    GT_CMP,

    GT_TEST,

#if TARGET_XARCH
    GT_BT,
#endif

    GT_JCMP,

    GT_JTEST,

    GT_JCC,

    GT_SETCC,

    GT_SELECTCC,

#if TARGET_ARM64 || TARGET_AMD64
    GT_CCMP,
#endif

#if TARGET_ARM64
    GT_SELECT_INC,

    GT_SELECT_INCCC,

    GT_SELECT_INV,

    GT_SELECT_INVCC,

    GT_SELECT_NEG,

    GT_SELECT_NEGCC,
#endif

#if TARGET_RISCV64
    GT_SH1ADD,

    GT_SH1ADD_UW,

    GT_SH2ADD,

    GT_SH2ADD_UW,

    GT_SH3ADD,

    GT_SH3ADD_UW,

    GT_ADD_UW,

    GT_SLLI_UW,

    GT_BIT_SET,

    GT_BIT_CLEAR,

    GT_BIT_INVERT,
#endif

    GT_JTRUE,

    GT_ARR_ELEM,

    GT_CALL,

    GT_FIELD_LIST,

    GT_RETURN,

    GT_SWITCH,

    GT_NO_OP,

    GT_RETURN_SUSPEND,

    GT_START_NONGC,

    GT_START_PREEMPTGC,

    GT_PROF_HOOK,

    GT_RETFILT,

#if SWIFT_SUPPORT
    GT_SWIFT_ERROR,

    GT_SWIFT_ERROR_RET,
#endif

#if TARGET_WASM
    GT_WASM_JEXCEPT,

    GT_WASM_THROW_REF,
#endif

    GT_JMPTABLE,

    GT_SWITCH_TABLE,

    GT_PHYSREG,

    GT_RETURNTRAP,

    GT_PUTARG_REG,

    GT_PUTARG_STK,

    GT_SWAP,

    GT_COPY,

    GT_RELOAD,

    GT_IL_OFFSET,

    GT_RECORD_ASYNC_RESUME,

    GT_COUNT,

#if TARGET_64BIT
    // GT_CNS_NATIVELONG is the gtOper symbol for GT_CNS_LNG or GT_CNS_INT, depending on the target.
    // For the 64-bit targets we will only use GT_CNS_INT as it used to represent all the possible sizes
    GT_CNS_NATIVELONG = GT_CNS_INT,
#else
    // For the 32-bit targets we use a GT_CNS_LNG to hold a 64-bit integer constant and GT_CNS_INT for all others.
    // In the future when we retarget the JIT for x86 we should consider eliminating GT_CNS_LNG
    GT_CNS_NATIVELONG = GT_CNS_LNG,
#endif
}
