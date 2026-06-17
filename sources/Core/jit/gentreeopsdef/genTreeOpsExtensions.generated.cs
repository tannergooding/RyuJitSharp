// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public static partial class genTreeOpsExtensions
{
    private static ReadOnlySpan<GenTreeOperKind> s_kinds => [
        GTK_SPECIAL, // GT_NONE
        GTK_SPECIAL, // GT_PHI
        GTK_LEAF, // GT_PHI_ARG
        GTK_LEAF, // GT_LCL_VAR
        GTK_LEAF, // GT_LCL_FLD
        GTK_UNOP | GTK_EXOP | GTK_NOVALUE | GTK_STORE, // GT_STORE_LCL_VAR
        GTK_UNOP | GTK_EXOP | GTK_NOVALUE | GTK_STORE, // GT_STORE_LCL_FLD
        GTK_LEAF, // GT_LCL_ADDR
        GTK_LEAF, // GT_CATCH_ARG
        GTK_LEAF, // GT_ASYNC_CONTINUATION
        GTK_LEAF, // GT_LABEL
        GTK_LEAF | GTK_NOVALUE, // GT_JMP
        GTK_LEAF, // GT_FTN_ADDR
        GTK_LEAF, // GT_RET_EXPR
        GTK_LEAF | GTK_NOVALUE, // GT_GCPOLL
        GTK_LEAF, // GT_ASYNC_RESUME_INFO
        GTK_LEAF, // GT_FTN_ENTRY
        GTK_LEAF, // GT_CNS_INT
        GTK_LEAF, // GT_CNS_LNG
        GTK_LEAF, // GT_CNS_DBL
        GTK_LEAF, // GT_CNS_STR
#if FEATURE_SIMD
        GTK_LEAF, // GT_CNS_VEC
#endif
#if FEATURE_MASKED_HW_INTRINSICS
        GTK_LEAF, // GT_CNS_MSK
#endif
        GTK_UNOP, // GT_NOT
        GTK_LEAF, // GT_NOP
        GTK_UNOP, // GT_NEG
        GTK_BINOP | GTK_EXOP, // GT_INTRINSIC
        GTK_UNOP | GTK_NOVALUE, // GT_KEEPALIVE
        GTK_UNOP | GTK_EXOP, // GT_CAST
        GTK_UNOP, // GT_BITCAST
        GTK_UNOP, // GT_CKFINITE
        GTK_UNOP, // GT_LCLHEAP
        GTK_BINOP | GTK_EXOP | GTK_NOVALUE, // GT_BOUNDS_CHECK
        GTK_LEAF | GTK_NOVALUE, // GT_MEMORYBARRIER
        GTK_BINOP | GTK_NOVALUE, // GT_LOCKADD
        GTK_BINOP, // GT_XAND
        GTK_BINOP, // GT_XORR
        GTK_BINOP, // GT_XADD
        GTK_BINOP, // GT_XCHG
        GTK_SPECIAL, // GT_CMPXCHG
        GTK_UNOP, // GT_IND
        GTK_BINOP | GTK_EXOP | GTK_NOVALUE | GTK_STORE, // GT_STOREIND
        GTK_UNOP | GTK_EXOP, // GT_BLK
        GTK_BINOP | GTK_EXOP | GTK_NOVALUE | GTK_STORE, // GT_STORE_BLK
        GTK_UNOP | GTK_NOVALUE, // GT_NULLCHECK
        GTK_UNOP | GTK_EXOP, // GT_ARR_LENGTH
        GTK_UNOP | GTK_EXOP, // GT_MDARR_LENGTH
        GTK_UNOP | GTK_EXOP, // GT_MDARR_LOWER_BOUND
        GTK_UNOP | GTK_EXOP, // GT_FIELD_ADDR
        GTK_UNOP | GTK_EXOP, // GT_ALLOCOBJ
        GTK_UNOP, // GT_INIT_VAL
        GTK_UNOP | GTK_EXOP, // GT_BOX
        GTK_UNOP | GTK_EXOP, // GT_RUNTIMELOOKUP
        GTK_UNOP | GTK_EXOP, // GT_ARR_ADDR
        GTK_UNOP, // GT_BSWAP
        GTK_UNOP, // GT_BSWAP16
        GTK_UNOP, // GT_LZCNT
        GTK_UNOP | GTK_NOVALUE, // GT_NONLOCAL_JMP
        GTK_BINOP | GTK_COMMUTE, // GT_ADD
        GTK_BINOP, // GT_SUB
        GTK_BINOP | GTK_COMMUTE, // GT_MUL
        GTK_BINOP, // GT_DIV
        GTK_BINOP, // GT_MOD
        GTK_BINOP, // GT_UDIV
        GTK_BINOP, // GT_UMOD
        GTK_BINOP | GTK_COMMUTE, // GT_OR
        GTK_BINOP | GTK_COMMUTE, // GT_XOR
        GTK_BINOP | GTK_COMMUTE, // GT_AND
        GTK_BINOP, // GT_LSH
        GTK_BINOP, // GT_RSH
        GTK_BINOP, // GT_RSZ
        GTK_BINOP, // GT_ROL
        GTK_BINOP, // GT_ROR
        GTK_BINOP, // GT_EQ
        GTK_BINOP, // GT_NE
        GTK_BINOP, // GT_LT
        GTK_BINOP, // GT_LE
        GTK_BINOP, // GT_GE
        GTK_BINOP, // GT_GT
        GTK_BINOP, // GT_TEST_EQ
        GTK_BINOP, // GT_TEST_NE
#if TARGET_XARCH
        GTK_BINOP, // GT_BITTEST_EQ
        GTK_BINOP, // GT_BITTEST_NE
#endif
        GTK_SPECIAL, // GT_SELECT
        GTK_BINOP, // GT_COMMA
        GTK_BINOP | GTK_EXOP, // GT_QMARK
        GTK_BINOP, // GT_COLON
        GTK_BINOP | GTK_EXOP, // GT_INDEX_ADDR
        GTK_BINOP | GTK_EXOP, // GT_LEA
#if !TARGET_64BIT
        GTK_BINOP, // GT_LONG
        GTK_BINOP | GTK_COMMUTE, // GT_ADD_LO
        GTK_BINOP | GTK_COMMUTE, // GT_ADD_HI
        GTK_BINOP, // GT_SUB_LO
        GTK_BINOP, // GT_SUB_HI
        GTK_BINOP, // GT_LSH_HI
        GTK_BINOP, // GT_RSH_LO
#endif
#if FEATURE_HW_INTRINSICS
        GTK_SPECIAL, // GT_HWINTRINSIC
#endif
        GTK_UNOP, // GT_INC_SATURATE
        GTK_BINOP | GTK_COMMUTE, // GT_MULHI
#if !TARGET_64BIT
        GTK_BINOP | GTK_COMMUTE, // GT_MUL_LONG
#elif TARGET_ARM64
        GTK_BINOP | GTK_COMMUTE, // GT_MUL_LONG
#endif
        GTK_BINOP, // GT_AND_NOT
        GTK_BINOP, // GT_OR_NOT
        GTK_BINOP, // GT_XOR_NOT
#if TARGET_ARM64
        GTK_BINOP, // GT_BFIZ
#endif
        GTK_BINOP | GTK_NOVALUE, // GT_CMP
        GTK_BINOP | GTK_NOVALUE, // GT_TEST
#if TARGET_XARCH
        GTK_BINOP | GTK_NOVALUE, // GT_BT
#endif
        GTK_BINOP | GTK_NOVALUE, // GT_JCMP
        GTK_BINOP | GTK_NOVALUE, // GT_JTEST
        GTK_LEAF | GTK_NOVALUE, // GT_JCC
        GTK_LEAF, // GT_SETCC
        GTK_BINOP, // GT_SELECTCC
#if TARGET_ARM64 || TARGET_AMD64
        GTK_BINOP | GTK_NOVALUE, // GT_CCMP
#endif
#if TARGET_ARM64
        GTK_SPECIAL, // GT_SELECT_INC
        GTK_BINOP, // GT_SELECT_INCCC
        GTK_SPECIAL, // GT_SELECT_INV
        GTK_BINOP, // GT_SELECT_INVCC
        GTK_SPECIAL, // GT_SELECT_NEG
        GTK_BINOP, // GT_SELECT_NEGCC
#endif
#if TARGET_RISCV64
        GTK_BINOP, // GT_SH1ADD
        GTK_BINOP, // GT_SH1ADD_UW
        GTK_BINOP, // GT_SH2ADD
        GTK_BINOP, // GT_SH2ADD_UW
        GTK_BINOP, // GT_SH3ADD
        GTK_BINOP, // GT_SH3ADD_UW
        GTK_BINOP, // GT_ADD_UW
        GTK_BINOP, // GT_SLLI_UW
        GTK_BINOP, // GT_BIT_SET
        GTK_BINOP, // GT_BIT_CLEAR
        GTK_BINOP, // GT_BIT_INVERT
#endif
        GTK_UNOP | GTK_NOVALUE, // GT_JTRUE
        GTK_SPECIAL, // GT_ARR_ELEM
        GTK_SPECIAL, // GT_CALL
        GTK_SPECIAL, // GT_FIELD_LIST
        GTK_UNOP | GTK_NOVALUE, // GT_RETURN
        GTK_UNOP | GTK_NOVALUE, // GT_SWITCH
        GTK_LEAF | GTK_NOVALUE, // GT_NO_OP
        GTK_UNOP | GTK_NOVALUE, // GT_RETURN_SUSPEND
        GTK_BINOP | GTK_NOVALUE, // GT_PATCHPOINT
        GTK_UNOP | GTK_NOVALUE, // GT_PATCHPOINT_FORCED
        GTK_LEAF | GTK_NOVALUE, // GT_START_NONGC
        GTK_LEAF | GTK_NOVALUE, // GT_START_PREEMPTGC
        GTK_LEAF | GTK_NOVALUE, // GT_PROF_HOOK
        GTK_UNOP | GTK_NOVALUE, // GT_RETFILT
        GTK_LEAF, // GT_SWIFT_ERROR
        GTK_BINOP | GTK_NOVALUE, // GT_SWIFT_ERROR_RET
        GTK_LEAF | GTK_NOVALUE, // GT_WASM_JEXCEPT
        GTK_LEAF | GTK_NOVALUE, // GT_WASM_THROW_REF
        GTK_LEAF, // GT_JMPTABLE
        GTK_BINOP | GTK_NOVALUE, // GT_SWITCH_TABLE
        GTK_LEAF, // GT_PHYSREG
        GTK_UNOP | GTK_NOVALUE, // GT_RETURNTRAP
        GTK_UNOP, // GT_PUTARG_REG
        GTK_UNOP | GTK_NOVALUE, // GT_PUTARG_STK
        GTK_BINOP | GTK_NOVALUE, // GT_SWAP
        GTK_UNOP, // GT_COPY
        GTK_UNOP, // GT_RELOAD
        GTK_LEAF | GTK_NOVALUE, // GT_IL_OFFSET
        GTK_LEAF | GTK_NOVALUE, // GT_RECORD_ASYNC_RESUME
    ];

#if DEBUG
    private static ReadOnlySpan<GenTreeDebugOperKind> s_debugKinds => [
        DBK_NONE, // GT_NONE
        DBK_NONE, // GT_PHI
        DBK_NONE, // GT_PHI_ARG
        DBK_NONE, // GT_LCL_VAR
        DBK_NONE, // GT_LCL_FLD
        DBK_NONE, // GT_STORE_LCL_VAR
        DBK_NONE, // GT_STORE_LCL_FLD
        DBK_NONE, // GT_LCL_ADDR
        DBK_NONE, // GT_CATCH_ARG
        DBK_NONE, // GT_ASYNC_CONTINUATION
        DBK_NONE, // GT_LABEL
        DBK_NONE, // GT_JMP
        DBK_NONE, // GT_FTN_ADDR
        DBK_NOTLIR, // GT_RET_EXPR
        DBK_NOTLIR, // GT_GCPOLL
        DBK_NONE, // GT_ASYNC_RESUME_INFO
        DBK_NONE, // GT_FTN_ENTRY
        DBK_NONE, // GT_CNS_INT
        DBK_NONE, // GT_CNS_LNG
        DBK_NONE, // GT_CNS_DBL
        DBK_NONE, // GT_CNS_STR
#if FEATURE_SIMD
        DBK_NONE, // GT_CNS_VEC
#endif
#if FEATURE_MASKED_HW_INTRINSICS
        DBK_NONE, // GT_CNS_MSK
#endif
        DBK_NONE, // GT_NOT
        DBK_NOCONTAIN, // GT_NOP
        DBK_NONE, // GT_NEG
        DBK_NONE, // GT_INTRINSIC
        DBK_NONE, // GT_KEEPALIVE
        DBK_NONE, // GT_CAST
        DBK_NONE, // GT_BITCAST
        DBK_NOCONTAIN, // GT_CKFINITE
        DBK_NOCONTAIN, // GT_LCLHEAP
        DBK_NONE, // GT_BOUNDS_CHECK
        DBK_NONE, // GT_MEMORYBARRIER
        DBK_NOTHIR, // GT_LOCKADD
        DBK_NONE, // GT_XAND
        DBK_NONE, // GT_XORR
        DBK_NONE, // GT_XADD
        DBK_NONE, // GT_XCHG
        DBK_NONE, // GT_CMPXCHG
        DBK_NONE, // GT_IND
        DBK_NONE, // GT_STOREIND
        DBK_NONE, // GT_BLK
        DBK_NONE, // GT_STORE_BLK
        DBK_NONE, // GT_NULLCHECK
        DBK_NONE, // GT_ARR_LENGTH
        DBK_NONE, // GT_MDARR_LENGTH
        DBK_NONE, // GT_MDARR_LOWER_BOUND
        DBK_NOTLIR, // GT_FIELD_ADDR
        DBK_NOTLIR, // GT_ALLOCOBJ
        DBK_NONE, // GT_INIT_VAL
        DBK_NOTLIR, // GT_BOX
        DBK_NOTLIR, // GT_RUNTIMELOOKUP
        DBK_NOTLIR, // GT_ARR_ADDR
        DBK_NONE, // GT_BSWAP
        DBK_NONE, // GT_BSWAP16
        DBK_NONE, // GT_LZCNT
        DBK_NONE, // GT_NONLOCAL_JMP
        DBK_NONE, // GT_ADD
        DBK_NONE, // GT_SUB
        DBK_NONE, // GT_MUL
        DBK_NONE, // GT_DIV
        DBK_NONE, // GT_MOD
        DBK_NONE, // GT_UDIV
        DBK_NONE, // GT_UMOD
        DBK_NONE, // GT_OR
        DBK_NONE, // GT_XOR
        DBK_NONE, // GT_AND
        DBK_NONE, // GT_LSH
        DBK_NONE, // GT_RSH
        DBK_NONE, // GT_RSZ
        DBK_NONE, // GT_ROL
        DBK_NONE, // GT_ROR
        DBK_NONE, // GT_EQ
        DBK_NONE, // GT_NE
        DBK_NONE, // GT_LT
        DBK_NONE, // GT_LE
        DBK_NONE, // GT_GE
        DBK_NONE, // GT_GT
        DBK_NOTHIR, // GT_TEST_EQ
        DBK_NOTHIR, // GT_TEST_NE
#if TARGET_XARCH
        DBK_NOTHIR, // GT_BITTEST_EQ
        DBK_NOTHIR, // GT_BITTEST_NE
#endif
        DBK_NONE, // GT_SELECT
        DBK_NOTLIR, // GT_COMMA
        DBK_NOTLIR, // GT_QMARK
        DBK_NOTLIR, // GT_COLON
        DBK_NONE, // GT_INDEX_ADDR
        DBK_NOTHIR, // GT_LEA
#if !TARGET_64BIT
        DBK_NOTHIR, // GT_LONG
        DBK_NOTHIR, // GT_ADD_LO
        DBK_NOTHIR, // GT_ADD_HI
        DBK_NOTHIR, // GT_SUB_LO
        DBK_NOTHIR, // GT_SUB_HI
        DBK_NOTHIR, // GT_LSH_HI
        DBK_NOTHIR, // GT_RSH_LO
#endif
#if FEATURE_HW_INTRINSICS
        DBK_NONE, // GT_HWINTRINSIC
#endif
        DBK_NOTHIR, // GT_INC_SATURATE
        DBK_NOTHIR, // GT_MULHI
#if !TARGET_64BIT
        DBK_NOTHIR, // GT_MUL_LONG
#elif TARGET_ARM64
        DBK_NOTHIR, // GT_MUL_LONG
#endif
        DBK_NOTHIR, // GT_AND_NOT
        DBK_NOTHIR, // GT_OR_NOT
        DBK_NOTHIR, // GT_XOR_NOT
#if TARGET_ARM64
        DBK_NOTHIR, // GT_BFIZ
#endif
        DBK_NOTHIR, // GT_CMP
        DBK_NOTHIR, // GT_TEST
#if TARGET_XARCH
        DBK_NOTHIR, // GT_BT
#endif
        DBK_NOTHIR, // GT_JCMP
        DBK_NOTHIR, // GT_JTEST
        DBK_NOTHIR, // GT_JCC
        DBK_NOTHIR, // GT_SETCC
        DBK_NOTHIR, // GT_SELECTCC
#if TARGET_ARM64 || TARGET_AMD64
        DBK_NOTHIR, // GT_CCMP
#endif
#if TARGET_ARM64
        DBK_NOTHIR, // GT_SELECT_INC
        DBK_NOTHIR, // GT_SELECT_INCCC
        DBK_NOTHIR, // GT_SELECT_INV
        DBK_NOTHIR, // GT_SELECT_INVCC
        DBK_NOTHIR, // GT_SELECT_NEG
        DBK_NOTHIR, // GT_SELECT_NEGCC
#endif
#if TARGET_RISCV64
        DBK_NOTHIR, // GT_SH1ADD
        DBK_NOTHIR, // GT_SH1ADD_UW
        DBK_NOTHIR, // GT_SH2ADD
        DBK_NOTHIR, // GT_SH2ADD_UW
        DBK_NOTHIR, // GT_SH3ADD
        DBK_NOTHIR, // GT_SH3ADD_UW
        DBK_NOTHIR, // GT_ADD_UW
        DBK_NOTHIR, // GT_SLLI_UW
        DBK_NOTHIR, // GT_BIT_SET
        DBK_NOTHIR, // GT_BIT_CLEAR
        DBK_NOTHIR, // GT_BIT_INVERT
#endif
        DBK_NONE, // GT_JTRUE
        DBK_NOTLIR, // GT_ARR_ELEM
        DBK_NOCONTAIN, // GT_CALL
        DBK_NONE, // GT_FIELD_LIST
        DBK_NONE, // GT_RETURN
        DBK_NONE, // GT_SWITCH
        DBK_NONE, // GT_NO_OP
        DBK_NONE, // GT_RETURN_SUSPEND
        DBK_NONE, // GT_PATCHPOINT
        DBK_NONE, // GT_PATCHPOINT_FORCED
        DBK_NOTHIR, // GT_START_NONGC
        DBK_NOTHIR, // GT_START_PREEMPTGC
        DBK_NOTHIR, // GT_PROF_HOOK
        DBK_NONE, // GT_RETFILT
        DBK_NONE, // GT_SWIFT_ERROR
        DBK_NONE, // GT_SWIFT_ERROR_RET
        DBK_NOTHIR, // GT_WASM_JEXCEPT
        DBK_NOTHIR, // GT_WASM_THROW_REF
        DBK_NOCONTAIN | DBK_NOTHIR, // GT_JMPTABLE
        DBK_NOTHIR, // GT_SWITCH_TABLE
        DBK_NOTHIR, // GT_PHYSREG
        DBK_NOTHIR, // GT_RETURNTRAP
        DBK_NOTHIR, // GT_PUTARG_REG
        DBK_NOTHIR, // GT_PUTARG_STK
        DBK_NOTHIR, // GT_SWAP
        DBK_NOTHIR, // GT_COPY
        DBK_NOTHIR, // GT_RELOAD
        DBK_NOTHIR, // GT_IL_OFFSET
        DBK_NOTHIR, // GT_RECORD_ASYNC_RESUME
    ];

    private static readonly string[] s_names = [
        "NONE", // GT_NONE
        "PHI", // GT_PHI
        "PHI_ARG", // GT_PHI_ARG
        "LCL_VAR", // GT_LCL_VAR
        "LCL_FLD", // GT_LCL_FLD
        "STORE_LCL_VAR", // GT_STORE_LCL_VAR
        "STORE_LCL_FLD", // GT_STORE_LCL_FLD
        "LCL_ADDR", // GT_LCL_ADDR
        "CATCH_ARG", // GT_CATCH_ARG
        "ASYNC_CONTINUATION", // GT_ASYNC_CONTINUATION
        "LABEL", // GT_LABEL
        "JMP", // GT_JMP
        "FTN_ADDR", // GT_FTN_ADDR
        "RET_EXPR", // GT_RET_EXPR
        "GCPOLL", // GT_GCPOLL
        "ASYNC_RESUME_INFO", // GT_ASYNC_RESUME_INFO
        "FTN_ENTRY", // GT_FTN_ENTRY
        "CNS_INT", // GT_CNS_INT
        "CNS_LNG", // GT_CNS_LNG
        "CNS_DBL", // GT_CNS_DBL
        "CNS_STR", // GT_CNS_STR
#if FEATURE_SIMD
        "CNS_VEC", // GT_CNS_VEC
#endif
#if FEATURE_MASKED_HW_INTRINSICS
        "CNS_MSK", // GT_CNS_MSK
#endif
        "NOT", // GT_NOT
        "NOP", // GT_NOP
        "NEG", // GT_NEG
        "INTRINSIC", // GT_INTRINSIC
        "KEEPALIVE", // GT_KEEPALIVE
        "CAST", // GT_CAST
        "BITCAST", // GT_BITCAST
        "CKFINITE", // GT_CKFINITE
        "LCLHEAP", // GT_LCLHEAP
        "BOUNDS_CHECK", // GT_BOUNDS_CHECK
        "MEMORYBARRIER", // GT_MEMORYBARRIER
        "LOCKADD", // GT_LOCKADD
        "XAND", // GT_XAND
        "XORR", // GT_XORR
        "XADD", // GT_XADD
        "XCHG", // GT_XCHG
        "CMPXCHG", // GT_CMPXCHG
        "IND", // GT_IND
        "STOREIND", // GT_STOREIND
        "BLK", // GT_BLK
        "STORE_BLK", // GT_STORE_BLK
        "NULLCHECK", // GT_NULLCHECK
        "ARR_LENGTH", // GT_ARR_LENGTH
        "MDARR_LENGTH", // GT_MDARR_LENGTH
        "MDARR_LOWER_BOUND", // GT_MDARR_LOWER_BOUND
        "FIELD_ADDR", // GT_FIELD_ADDR
        "ALLOCOBJ", // GT_ALLOCOBJ
        "INIT_VAL", // GT_INIT_VAL
        "BOX", // GT_BOX
        "RUNTIMELOOKUP", // GT_RUNTIMELOOKUP
        "ARR_ADDR", // GT_ARR_ADDR
        "BSWAP", // GT_BSWAP
        "BSWAP16", // GT_BSWAP16
        "LZCNT", // GT_LZCNT
        "NONLOCAL_JMP", // GT_NONLOCAL_JMP
        "ADD", // GT_ADD
        "SUB", // GT_SUB
        "MUL", // GT_MUL
        "DIV", // GT_DIV
        "MOD", // GT_MOD
        "UDIV", // GT_UDIV
        "UMOD", // GT_UMOD
        "OR", // GT_OR
        "XOR", // GT_XOR
        "AND", // GT_AND
        "LSH", // GT_LSH
        "RSH", // GT_RSH
        "RSZ", // GT_RSZ
        "ROL", // GT_ROL
        "ROR", // GT_ROR
        "EQ", // GT_EQ
        "NE", // GT_NE
        "LT", // GT_LT
        "LE", // GT_LE
        "GE", // GT_GE
        "GT", // GT_GT
        "TEST_EQ", // GT_TEST_EQ
        "TEST_NE", // GT_TEST_NE
#if TARGET_XARCH
        "BITTEST_EQ", // GT_BITTEST_EQ
        "BITTEST_NE", // GT_BITTEST_NE
#endif
        "SELECT", // GT_SELECT
        "COMMA", // GT_COMMA
        "QMARK", // GT_QMARK
        "COLON", // GT_COLON
        "INDEX_ADDR", // GT_INDEX_ADDR
        "LEA", // GT_LEA
#if !TARGET_64BIT
        "LONG", // GT_LONG
        "ADD_LO", // GT_ADD_LO
        "ADD_HI", // GT_ADD_HI
        "SUB_LO", // GT_SUB_LO
        "SUB_HI", // GT_SUB_HI
        "LSH_HI", // GT_LSH_HI
        "RSH_LO", // GT_RSH_LO
#endif
#if FEATURE_HW_INTRINSICS
        "HWINTRINSIC", // GT_HWINTRINSIC
#endif
        "INC_SATURATE", // GT_INC_SATURATE
        "MULHI", // GT_MULHI
#if !TARGET_64BIT
        "MUL_LONG", // GT_MUL_LONG
#elif TARGET_ARM64
        "MUL_LONG", // GT_MUL_LONG
#endif
        "AND_NOT", // GT_AND_NOT
        "OR_NOT", // GT_OR_NOT
        "XOR_NOT", // GT_XOR_NOT
#if TARGET_ARM64
        "BFIZ", // GT_BFIZ
#endif
        "CMP", // GT_CMP
        "TEST", // GT_TEST
#if TARGET_XARCH
        "BT", // GT_BT
#endif
        "JCMP", // GT_JCMP
        "JTEST", // GT_JTEST
        "JCC", // GT_JCC
        "SETCC", // GT_SETCC
        "SELECTCC", // GT_SELECTCC
#if TARGET_ARM64 || TARGET_AMD64
        "CCMP", // GT_CCMP
#endif
#if TARGET_ARM64
        "SELECT_INC", // GT_SELECT_INC
        "SELECT_INCCC", // GT_SELECT_INCCC
        "SELECT_INV", // GT_SELECT_INV
        "SELECT_INVCC", // GT_SELECT_INVCC
        "SELECT_NEG", // GT_SELECT_NEG
        "SELECT_NEGCC", // GT_SELECT_NEGCC
#endif
#if TARGET_RISCV64
        "SH1ADD", // GT_SH1ADD
        "SH1ADD_UW", // GT_SH1ADD_UW
        "SH2ADD", // GT_SH2ADD
        "SH2ADD_UW", // GT_SH2ADD_UW
        "SH3ADD", // GT_SH3ADD
        "SH3ADD_UW", // GT_SH3ADD_UW
        "ADD_UW", // GT_ADD_UW
        "SLLI_UW", // GT_SLLI_UW
        "BIT_SET", // GT_BIT_SET
        "BIT_CLEAR", // GT_BIT_CLEAR
        "BIT_INVERT", // GT_BIT_INVERT
#endif
        "JTRUE", // GT_JTRUE
        "ARR_ELEM", // GT_ARR_ELEM
        "CALL", // GT_CALL
        "FIELD_LIST", // GT_FIELD_LIST
        "RETURN", // GT_RETURN
        "SWITCH", // GT_SWITCH
        "NO_OP", // GT_NO_OP
        "RETURN_SUSPEND", // GT_RETURN_SUSPEND
        "PATCHPOINT", // GT_PATCHPOINT
        "PATCHPOINT_FORCED", // GT_PATCHPOINT_FORCED
        "START_NONGC", // GT_START_NONGC
        "START_PREEMPTGC", // GT_START_PREEMPTGC
        "PROF_HOOK", // GT_PROF_HOOK
        "RETFILT", // GT_RETFILT
        "SWIFT_ERROR", // GT_SWIFT_ERROR
        "SWIFT_ERROR_RET", // GT_SWIFT_ERROR_RET
        "WASM_JEXCEPT", // GT_WASM_JEXCEPT
        "WASM_THROW_REF", // GT_WASM_THROW_REF
        "JMPTABLE", // GT_JMPTABLE
        "SWITCH_TABLE", // GT_SWITCH_TABLE
        "PHYSREG", // GT_PHYSREG
        "RETURNTRAP", // GT_RETURNTRAP
        "PUTARG_REG", // GT_PUTARG_REG
        "PUTARG_STK", // GT_PUTARG_STK
        "SWAP", // GT_SWAP
        "COPY", // GT_COPY
        "RELOAD", // GT_RELOAD
        "IL_OFFSET", // GT_IL_OFFSET
        "RECORD_ASYNC_RESUME", // GT_RECORD_ASYNC_RESUME
    ];
#endif

#if DEBUG
    private static readonly Type[] s_structTypes = [
        typeof(char), // GT_NONE
        typeof(GenTreePhi), // GT_PHI
        typeof(GenTreePhiArg), // GT_PHI_ARG
        typeof(GenTreeLclVar), // GT_LCL_VAR
        typeof(GenTreeLclFld), // GT_LCL_FLD
        typeof(GenTreeLclVar), // GT_STORE_LCL_VAR
        typeof(GenTreeLclFld), // GT_STORE_LCL_FLD
        typeof(GenTreeLclFld), // GT_LCL_ADDR
        typeof(GenTree), // GT_CATCH_ARG
        typeof(GenTree), // GT_ASYNC_CONTINUATION
        typeof(GenTree), // GT_LABEL
        typeof(GenTreeVal), // GT_JMP
        typeof(GenTreeFptrVal), // GT_FTN_ADDR
        typeof(GenTreeRetExpr), // GT_RET_EXPR
        typeof(GenTree), // GT_GCPOLL
        typeof(GenTreeVal), // GT_ASYNC_RESUME_INFO
        typeof(GenTree), // GT_FTN_ENTRY
        typeof(GenTreeIntCon), // GT_CNS_INT
        typeof(GenTreeLngCon), // GT_CNS_LNG
        typeof(GenTreeDblCon), // GT_CNS_DBL
        typeof(GenTreeStrCon), // GT_CNS_STR
#if FEATURE_SIMD
        typeof(GenTreeVecCon), // GT_CNS_VEC
#endif
#if FEATURE_MASKED_HW_INTRINSICS
        typeof(GenTreeMskCon), // GT_CNS_MSK
#endif
        typeof(GenTreeOp), // GT_NOT
        typeof(GenTree), // GT_NOP
        typeof(GenTreeOp), // GT_NEG
        typeof(GenTreeIntrinsic), // GT_INTRINSIC
        typeof(GenTree), // GT_KEEPALIVE
        typeof(GenTreeCast), // GT_CAST
        typeof(GenTreeOp), // GT_BITCAST
        typeof(GenTreeOp), // GT_CKFINITE
        typeof(GenTreeOp), // GT_LCLHEAP
        typeof(GenTreeBoundsChk), // GT_BOUNDS_CHECK
        typeof(GenTree), // GT_MEMORYBARRIER
        typeof(GenTreeOp), // GT_LOCKADD
        typeof(GenTreeOp), // GT_XAND
        typeof(GenTreeOp), // GT_XORR
        typeof(GenTreeOp), // GT_XADD
        typeof(GenTreeOp), // GT_XCHG
        typeof(GenTreeCmpXchg), // GT_CMPXCHG
        typeof(GenTreeIndir), // GT_IND
        typeof(GenTreeStoreInd), // GT_STOREIND
        typeof(GenTreeBlk), // GT_BLK
        typeof(GenTreeBlk), // GT_STORE_BLK
        typeof(GenTreeIndir), // GT_NULLCHECK
        typeof(GenTreeArrLen), // GT_ARR_LENGTH
        typeof(GenTreeMDArr), // GT_MDARR_LENGTH
        typeof(GenTreeMDArr), // GT_MDARR_LOWER_BOUND
        typeof(GenTreeFieldAddr), // GT_FIELD_ADDR
        typeof(GenTreeAllocObj), // GT_ALLOCOBJ
        typeof(GenTreeOp), // GT_INIT_VAL
        typeof(GenTreeBox), // GT_BOX
        typeof(GenTreeRuntimeLookup), // GT_RUNTIMELOOKUP
        typeof(GenTreeArrAddr), // GT_ARR_ADDR
        typeof(GenTreeOp), // GT_BSWAP
        typeof(GenTreeOp), // GT_BSWAP16
        typeof(GenTreeOp), // GT_LZCNT
        typeof(GenTreeOp), // GT_NONLOCAL_JMP
        typeof(GenTreeOp), // GT_ADD
        typeof(GenTreeOp), // GT_SUB
        typeof(GenTreeOp), // GT_MUL
        typeof(GenTreeOp), // GT_DIV
        typeof(GenTreeOp), // GT_MOD
        typeof(GenTreeOp), // GT_UDIV
        typeof(GenTreeOp), // GT_UMOD
        typeof(GenTreeOp), // GT_OR
        typeof(GenTreeOp), // GT_XOR
        typeof(GenTreeOp), // GT_AND
        typeof(GenTreeOp), // GT_LSH
        typeof(GenTreeOp), // GT_RSH
        typeof(GenTreeOp), // GT_RSZ
        typeof(GenTreeOp), // GT_ROL
        typeof(GenTreeOp), // GT_ROR
        typeof(GenTreeOp), // GT_EQ
        typeof(GenTreeOp), // GT_NE
        typeof(GenTreeOp), // GT_LT
        typeof(GenTreeOp), // GT_LE
        typeof(GenTreeOp), // GT_GE
        typeof(GenTreeOp), // GT_GT
        typeof(GenTreeOp), // GT_TEST_EQ
        typeof(GenTreeOp), // GT_TEST_NE
#if TARGET_XARCH
        typeof(GenTreeOp), // GT_BITTEST_EQ
        typeof(GenTreeOp), // GT_BITTEST_NE
#endif
        typeof(GenTreeConditional), // GT_SELECT
        typeof(GenTreeOp), // GT_COMMA
        typeof(GenTreeQmark), // GT_QMARK
        typeof(GenTreeColon), // GT_COLON
        typeof(GenTreeIndexAddr), // GT_INDEX_ADDR
        typeof(GenTreeAddrMode), // GT_LEA
#if !TARGET_64BIT
        typeof(GenTreeOp), // GT_LONG
        typeof(GenTreeOp), // GT_ADD_LO
        typeof(GenTreeOp), // GT_ADD_HI
        typeof(GenTreeOp), // GT_SUB_LO
        typeof(GenTreeOp), // GT_SUB_HI
        typeof(GenTreeOp), // GT_LSH_HI
        typeof(GenTreeOp), // GT_RSH_LO
#endif
#if FEATURE_HW_INTRINSICS
        typeof(GenTreeHWIntrinsic), // GT_HWINTRINSIC
#endif
        typeof(GenTreeOp), // GT_INC_SATURATE
        typeof(GenTreeOp), // GT_MULHI
#if !TARGET_64BIT
        typeof(GenTreeMultiRegOp), // GT_MUL_LONG
#elif TARGET_ARM64
        typeof(GenTreeOp), // GT_MUL_LONG
#endif
        typeof(GenTreeOp), // GT_AND_NOT
        typeof(GenTreeOp), // GT_OR_NOT
        typeof(GenTreeOp), // GT_XOR_NOT
#if TARGET_ARM64
        typeof(GenTreeOp), // GT_BFIZ
#endif
        typeof(GenTreeOp), // GT_CMP
        typeof(GenTreeOp), // GT_TEST
#if TARGET_XARCH
        typeof(GenTreeOp), // GT_BT
#endif
        typeof(GenTreeOpCC), // GT_JCMP
        typeof(GenTreeOpCC), // GT_JTEST
        typeof(GenTreeCC), // GT_JCC
        typeof(GenTreeCC), // GT_SETCC
        typeof(GenTreeOpCC), // GT_SELECTCC
#if TARGET_ARM64 || TARGET_AMD64
        typeof(GenTreeCCMP), // GT_CCMP
#endif
#if TARGET_ARM64
        typeof(GenTreeConditional), // GT_SELECT_INC
        typeof(GenTreeOpCC), // GT_SELECT_INCCC
        typeof(GenTreeConditional), // GT_SELECT_INV
        typeof(GenTreeOpCC), // GT_SELECT_INVCC
        typeof(GenTreeConditional), // GT_SELECT_NEG
        typeof(GenTreeOpCC), // GT_SELECT_NEGCC
#endif
#if TARGET_RISCV64
        typeof(GenTreeOp), // GT_SH1ADD
        typeof(GenTreeOp), // GT_SH1ADD_UW
        typeof(GenTreeOp), // GT_SH2ADD
        typeof(GenTreeOp), // GT_SH2ADD_UW
        typeof(GenTreeOp), // GT_SH3ADD
        typeof(GenTreeOp), // GT_SH3ADD_UW
        typeof(GenTreeOp), // GT_ADD_UW
        typeof(GenTreeOp), // GT_SLLI_UW
        typeof(GenTreeOp), // GT_BIT_SET
        typeof(GenTreeOp), // GT_BIT_CLEAR
        typeof(GenTreeOp), // GT_BIT_INVERT
#endif
        typeof(GenTreeOp), // GT_JTRUE
        typeof(GenTreeArrElem), // GT_ARR_ELEM
        typeof(GenTreeCall), // GT_CALL
        typeof(GenTreeFieldList), // GT_FIELD_LIST
        typeof(GenTreeOp), // GT_RETURN
        typeof(GenTreeOp), // GT_SWITCH
        typeof(GenTree), // GT_NO_OP
        typeof(GenTreeOp), // GT_RETURN_SUSPEND
        typeof(GenTreeOp), // GT_PATCHPOINT
        typeof(GenTreeOp), // GT_PATCHPOINT_FORCED
        typeof(GenTree), // GT_START_NONGC
        typeof(GenTree), // GT_START_PREEMPTGC
        typeof(GenTree), // GT_PROF_HOOK
        typeof(GenTreeOp), // GT_RETFILT
        typeof(GenTree), // GT_SWIFT_ERROR
        typeof(GenTreeOp), // GT_SWIFT_ERROR_RET
        typeof(GenTree), // GT_WASM_JEXCEPT
        typeof(GenTree), // GT_WASM_THROW_REF
        typeof(GenTree), // GT_JMPTABLE
        typeof(GenTreeOp), // GT_SWITCH_TABLE
        typeof(GenTreePhysReg), // GT_PHYSREG
        typeof(GenTreeOp), // GT_RETURNTRAP
        typeof(GenTreeOp), // GT_PUTARG_REG
        typeof(GenTreePutArgStk), // GT_PUTARG_STK
        typeof(GenTreeOp), // GT_SWAP
        typeof(GenTreeCopyOrReload), // GT_COPY
        typeof(GenTreeCopyOrReload), // GT_RELOAD
        typeof(GenTreeILOffset), // GT_IL_OFFSET
        typeof(GenTreeVal), // GT_RECORD_ASYNC_RESUME
    ];
#endif
}