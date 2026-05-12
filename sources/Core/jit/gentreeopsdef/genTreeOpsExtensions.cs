// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public static class genTreeOpsExtensions
{
    private static ReadOnlySpan<GenTreeOperKind> s_kinds => [
        GTK_SPECIAL,                                // GT_NONE
        GTK_SPECIAL,                                // GT_PHI
        GTK_LEAF,                                   // GT_PHI_ARG
        GTK_LEAF,                                   // GT_LCL_VAR
        GTK_LEAF,                                   // GT_LCL_FLD
        GTK_UNOP|GTK_EXOP|GTK_NOVALUE|GTK_STORE,    // GT_STORE_LCL_VAR
        GTK_UNOP|GTK_EXOP|GTK_NOVALUE|GTK_STORE,    // GT_STORE_LCL_FLD
        GTK_LEAF,                                   // GT_LCL_ADDR
        GTK_LEAF,                                   // GT_CATCH_ARG
        GTK_LEAF,                                   // GT_ASYNC_CONTINUATION
        GTK_LEAF,                                   // GT_LABEL
        GTK_LEAF|GTK_NOVALUE,                       // GT_JMP
        GTK_LEAF,                                   // GT_FTN_ADDR
        GTK_LEAF,                                   // GT_RET_EXPR
        GTK_LEAF|GTK_NOVALUE,                       // GT_GCPOLL
        GTK_LEAF,                                   // GT_ASYNC_RESUME_INFO
        GTK_LEAF,                                   // GT_FTN_ENTRY
        GTK_LEAF,                                   // GT_CNS_INT
        GTK_LEAF,                                   // GT_CNS_LNG
        GTK_LEAF,                                   // GT_CNS_DBL
        GTK_LEAF,                                   // GT_CNS_STR

#if FEATURE_SIMD
        GTK_LEAF,                                   // GT_CNS_VEC
#endif

#if FEATURE_MASKED_HW_INTRINSICS
        GTK_LEAF,                                   // GT_CNS_MSK
#endif

        GTK_LEAF,                                   // GT_NOP
        GTK_BINOP|GTK_EXOP,                         // GT_INTRINSIC
        GTK_UNOP|GTK_NOVALUE,                       // GT_KEEPALIVE
        GTK_UNOP|GTK_EXOP,                          // GT_CAST
        GTK_UNOP,                                   // GT_BITCAST
        GTK_UNOP,                                   // GT_CKFINITE
        GTK_UNOP,                                   // GT_LCLHEAP
        GTK_BINOP|GTK_EXOP|GTK_NOVALUE,             // GT_BOUNDS_CHECK
        GTK_LEAF|GTK_NOVALUE,                       // GT_MEMORYBARRIER
        GTK_BINOP|GTK_NOVALUE,                      // GT_LOCKADD
        GTK_BINOP,                                  // GT_XAND
        GTK_BINOP,                                  // GT_XORR
        GTK_BINOP,                                  // GT_XADD
        GTK_BINOP,                                  // GT_XCHG
        GTK_SPECIAL,                                // GT_CMPXCHG
        GTK_UNOP,                                   // GT_IND
        GTK_BINOP|GTK_EXOP|GTK_NOVALUE|GTK_STORE,   // GT_STOREIND
        GTK_UNOP|GTK_EXOP,                          // GT_BLK
        GTK_BINOP|GTK_EXOP|GTK_NOVALUE|GTK_STORE,   // GT_STORE_BLK
        GTK_UNOP|GTK_NOVALUE,                       // GT_NULLCHECK
        GTK_UNOP|GTK_EXOP,                          // GT_ARR_LENGTH
        GTK_UNOP|GTK_EXOP,                          // GT_MDARR_LENGTH
        GTK_UNOP|GTK_EXOP,                          // GT_MDARR_LOWER_BOUND
        GTK_UNOP|GTK_EXOP,                          // GT_FIELD_ADDR
        GTK_UNOP|GTK_EXOP,                          // GT_ALLOCOBJ
        GTK_UNOP,                                   // GT_INIT_VAL
        GTK_UNOP|GTK_EXOP,                          // GT_BOX
        GTK_UNOP|GTK_EXOP,                          // GT_RUNTIMELOOKUP
        GTK_UNOP|GTK_EXOP,                          // GT_ARR_ADDR
        GTK_UNOP,                                   // GT_BSWAP
        GTK_UNOP,                                   // GT_BSWAP16
        GTK_UNOP,                                   // GT_LZCNT
        GTK_UNOP|GTK_NOVALUE,                       // GT_NONLOCAL_JMP
        GTK_UNOP,                                   // GT_NOT
        GTK_UNOP,                                   // GT_NEG
        GTK_BINOP,                                  // GT_OR
        GTK_BINOP,                                  // GT_XOR
        GTK_BINOP,                                  // GT_AND
        GTK_BINOP|GTK_COMMUTE,                      // GT_LSH
        GTK_BINOP|GTK_COMMUTE,                      // GT_RSH
        GTK_BINOP,                                  // GT_RSZ
        GTK_BINOP,                                  // GT_ROL
        GTK_BINOP,                                  // GT_ROR
        GTK_BINOP,                                  // GT_ADD
        GTK_BINOP|GTK_COMMUTE,                      // GT_SUB
        GTK_BINOP|GTK_COMMUTE,                      // GT_MUL
        GTK_BINOP|GTK_COMMUTE,                      // GT_DIV
        GTK_BINOP,                                  // GT_MOD
        GTK_BINOP,                                  // GT_UDIV
        GTK_BINOP,                                  // GT_UMOD
        GTK_BINOP,                                  // GT_EQ
        GTK_BINOP,                                  // GT_NE
        GTK_BINOP,                                  // GT_LT
        GTK_BINOP,                                  // GT_LE
        GTK_BINOP,                                  // GT_GE
        GTK_BINOP,                                  // GT_GT
        GTK_BINOP,                                  // GT_TEST_EQ
        GTK_BINOP,                                  // GT_TEST_NE

#if TARGET_XARCH
        GTK_BINOP,                                  // GT_BITTEST_EQ
        GTK_BINOP,                                  // GT_BITTEST_NE
#endif

        GTK_SPECIAL,                                // GT_SELECT
        GTK_BINOP,                                  // GT_COMMA
        GTK_BINOP|GTK_EXOP,                         // GT_QMARK
        GTK_BINOP,                                  // GT_COLON
        GTK_BINOP|GTK_EXOP,                         // GT_INDEX_ADDR
        GTK_BINOP|GTK_EXOP,                         // GT_LEA

#if TARGET_32BIT
        GTK_BINOP,                                  // GT_LONG
        GTK_BINOP,                                  // GT_ADD_LO
        GTK_BINOP,                                  // GT_ADD_HI
        GTK_BINOP|GTK_COMMUTE,                      // GT_SUB_LO
        GTK_BINOP|GTK_COMMUTE,                      // GT_SUB_HI
        GTK_BINOP|GTK_COMMUTE,                      // GT_LSH_HI
        GTK_BINOP,                                  // GT_RSH_LO
#endif

#if FEATURE_HW_INTRINSICS
        GTK_SPECIAL,                                // GT_HWINTRINSIC
#endif

        GTK_UNOP,                                   // GT_INC_SATURATE
        GTK_BINOP,                                  // GT_MULHI

#if TARGET_32BIT || TARGET_ARM64
        GTK_BINOP|GTK_COMMUTE,                      // GT_MUL_LONG
#endif

        GTK_BINOP|GTK_COMMUTE,                      // GT_AND_NOT
        GTK_BINOP,                                  // GT_OR_NOT
        GTK_BINOP,                                  // GT_XOR_NOT

#if TARGET_ARM64
        GTK_BINOP,                                  // GT_BFIZ
#endif

        GTK_BINOP|GTK_NOVALUE,                      // GT_CMP
        GTK_BINOP|GTK_NOVALUE,                      // GT_TEST

#if TARGET_XARCH
        GTK_BINOP|GTK_NOVALUE,                      // GT_BT
#endif

        GTK_BINOP|GTK_NOVALUE,                      // GT_JCMP
        GTK_BINOP|GTK_NOVALUE,                      // GT_JTEST
        GTK_LEAF|GTK_NOVALUE,                       // GT_JCC
        GTK_LEAF,                                   // GT_SETCC
        GTK_BINOP,                                  // GT_SELECTCC

#if TARGET_ARM64 || TARGET_AMD64
        GTK_BINOP|GTK_NOVALUE,                      // GT_CCMP
#endif

#if TARGET_ARM64
        GTK_BINOP,                                  // GT_SELECT_INCCC
        GTK_BINOP,                                  // GT_SELECT_INVCC
        GTK_BINOP,                                  // GT_SELECT_NEGCC
        GTK_SPECIAL,                                // GT_SELECT_INC
        GTK_SPECIAL,                                // GT_SELECT_INV
        GTK_SPECIAL,                                // GT_SELECT_NEG
#endif

#if TARGET_RISCV64
        GTK_BINOP,                                  // GT_SH1ADD
        GTK_BINOP,                                  // GT_SH1ADD_UW
        GTK_BINOP,                                  // GT_SH2ADD
        GTK_BINOP,                                  // GT_SH2ADD_UW
        GTK_BINOP,                                  // GT_SH3ADD
        GTK_BINOP,                                  // GT_SH3ADD_UW
        GTK_BINOP,                                  // GT_ADD_UW
        GTK_BINOP,                                  // GT_SLLI_UW
        GTK_BINOP,                                  // GT_BIT_SET
        GTK_BINOP,                                  // GT_BIT_CLEAR
        GTK_BINOP,                                  // GT_BIT_INVERT
#endif

        GTK_UNOP|GTK_NOVALUE,                       // GT_JTRUE
        GTK_SPECIAL,                                // GT_ARR_ELEM
        GTK_SPECIAL,                                // GT_CALL
        GTK_SPECIAL,                                // GT_FIELD_LIST
        GTK_UNOP|GTK_NOVALUE,                       // GT_RETURN
        GTK_UNOP|GTK_NOVALUE,                       // GT_SWITCH
        GTK_LEAF|GTK_NOVALUE,                       // GT_NO_OP
        GTK_UNOP|GTK_NOVALUE,                       // GT_RETURN_SUSPEND
        GTK_BINOP|GTK_NOVALUE,                      // GT_PATCHPOINT
        GTK_UNOP|GTK_NOVALUE,                       // GT_PATCHPOINT_FORCED
        GTK_LEAF|GTK_NOVALUE,                       // GT_START_NONGC
        GTK_LEAF|GTK_NOVALUE,                       // GT_START_PREEMPTGC
        GTK_LEAF|GTK_NOVALUE,                       // GT_PROF_HOOK
        GTK_UNOP|GTK_NOVALUE,                       // GT_RETFILT

#if SWIFT_SUPPORT
        GTK_LEAF,                                   // GT_SWIFT_ERROR
        GTK_BINOP|GTK_NOVALUE,                      // GT_SWIFT_ERROR_RET
#endif

#if TARGET_WASM
        GTK_LEAF|GTK_NOVALUE,                       // GT_WASM_JEXCEPT
        GTK_LEAF|GTK_NOVALUE,                       // GT_WASM_THROW_REF
#endif

        GTK_LEAF,                                   // GT_JMPTABLE
        GTK_BINOP|GTK_NOVALUE,                      // GT_SWITCH_TABLE
        GTK_LEAF,                                   // GT_PHYSREG
        GTK_UNOP|GTK_NOVALUE,                       // GT_RETURNTRAP
        GTK_UNOP,                                   // GT_PUTARG_REG
        GTK_UNOP|GTK_NOVALUE,                       // GT_PUTARG_STK
        GTK_BINOP|GTK_NOVALUE,                      // GT_SWAP
        GTK_UNOP,                                   // GT_COPY
        GTK_UNOP,                                   // GT_RELOAD
        GTK_LEAF|GTK_NOVALUE,                       // GT_IL_OFFSET
        GTK_LEAF|GTK_NOVALUE,                       // GT_RECORD_ASYNC_RESUME
    ];

#if DEBUG
    private static ReadOnlySpan<GenTreeDebugOperKind> s_debugKinds => [
        DBK_NONE,                   // GT_NONE
        DBK_NONE,                   // GT_PHI
        DBK_NONE,                   // GT_PHI_ARG
        DBK_NONE,                   // GT_LCL_VAR
        DBK_NONE,                   // GT_LCL_FLD
        DBK_NONE,                   // GT_STORE_LCL_VAR
        DBK_NONE,                   // GT_STORE_LCL_FLD
        DBK_NONE,                   // GT_LCL_ADDR
        DBK_NONE,                   // GT_CATCH_ARG
        DBK_NONE,                   // GT_ASYNC_CONTINUATION
        DBK_NONE,                   // GT_LABEL
        DBK_NONE,                   // GT_JMP
        DBK_NONE,                   // GT_FTN_ADDR
        DBK_NOTLIR,                 // GT_RET_EXPR
        DBK_NOTLIR,                 // GT_GCPOLL
        DBK_NONE,                   // GT_ASYNC_RESUME_INFO
        DBK_NONE,                   // GT_FTN_ENTRY
        DBK_NONE,                   // GT_CNS_INT
        DBK_NONE,                   // GT_CNS_LNG
        DBK_NONE,                   // GT_CNS_DBL
        DBK_NONE,                   // GT_CNS_STR

#if FEATURE_SIMD
        DBK_NONE,                   // GT_CNS_VEC
#endif

#if FEATURE_MASKED_HW_INTRINSICS
        DBK_NONE,                   // GT_CNS_MSK
#endif

        DBK_NOCONTAIN,              // GT_NOP
        DBK_NONE,                   // GT_INTRINSIC
        DBK_NONE,                   // GT_KEEPALIVE
        DBK_NONE,                   // GT_CAST
        DBK_NONE,                   // GT_BITCAST
        DBK_NOCONTAIN,              // GT_CKFINITE
        DBK_NOCONTAIN,              // GT_LCLHEAP
        DBK_NONE,                   // GT_BOUNDS_CHECK
        DBK_NONE,                   // GT_MEMORYBARRIER
        DBK_NOTHIR,                 // GT_LOCKADD
        DBK_NONE,                   // GT_XAND
        DBK_NONE,                   // GT_XORR
        DBK_NONE,                   // GT_XADD
        DBK_NONE,                   // GT_XCHG
        DBK_NONE,                   // GT_CMPXCHG
        DBK_NONE,                   // GT_IND
        DBK_NONE,                   // GT_STOREIND
        DBK_NONE,                   // GT_BLK
        DBK_NONE,                   // GT_STORE_BLK
        DBK_NONE,                   // GT_NULLCHECK
        DBK_NONE,                   // GT_ARR_LENGTH
        DBK_NONE,                   // GT_MDARR_LENGTH
        DBK_NONE,                   // GT_MDARR_LOWER_BOUND
        DBK_NOTLIR,                 // GT_FIELD_ADDR
        DBK_NOTLIR,                 // GT_ALLOCOBJ
        DBK_NONE,                   // GT_INIT_VAL
        DBK_NOTLIR,                 // GT_BOX
        DBK_NOTLIR,                 // GT_RUNTIMELOOKUP
        DBK_NOTLIR,                 // GT_ARR_ADDR
        DBK_NONE,                   // GT_BSWAP
        DBK_NONE,                   // GT_BSWAP16
        DBK_NONE,                   // GT_LZCNT
        DBK_NONE,                   // GT_NONLOCAL_JMP
        DBK_NONE,                   // GT_NOT
        DBK_NONE,                   // GT_NEG
        DBK_NONE,                   // GT_OR
        DBK_NONE,                   // GT_XOR
        DBK_NONE,                   // GT_AND
        DBK_NONE,                   // GT_LSH
        DBK_NONE,                   // GT_RSH
        DBK_NONE,                   // GT_RSZ
        DBK_NONE,                   // GT_ROL
        DBK_NONE,                   // GT_ROR
        DBK_NONE,                   // GT_ADD
        DBK_NONE,                   // GT_SUB
        DBK_NONE,                   // GT_MUL
        DBK_NONE,                   // GT_DIV
        DBK_NONE,                   // GT_MOD
        DBK_NONE,                   // GT_UDIV
        DBK_NONE,                   // GT_UMOD
        DBK_NONE,                   // GT_EQ
        DBK_NONE,                   // GT_NE
        DBK_NONE,                   // GT_LT
        DBK_NONE,                   // GT_LE
        DBK_NONE,                   // GT_GE
        DBK_NONE,                   // GT_GT
        DBK_NOTHIR,                 // GT_TEST_EQ
        DBK_NOTHIR,                 // GT_TEST_NE

#if TARGET_XARCH
        DBK_NOTHIR,                 // GT_BITTEST_EQ
        DBK_NOTHIR,                 // GT_BITTEST_NE
#endif

        DBK_NONE,                   // GT_SELECT
        DBK_NOTLIR,                 // GT_COMMA
        DBK_NOTLIR,                 // GT_QMARK
        DBK_NOTLIR,                 // GT_COLON
        DBK_NONE,                   // GT_INDEX_ADDR
        DBK_NOTHIR,                 // GT_LEA

#if TARGET_32BIT
        DBK_NOTHIR,                 // GT_LONG
        DBK_NOTHIR,                 // GT_ADD_LO
        DBK_NOTHIR,                 // GT_ADD_HI
        DBK_NOTHIR,                 // GT_SUB_LO
        DBK_NOTHIR,                 // GT_SUB_HI
        DBK_NOTHIR,                 // GT_LSH_HI
        DBK_NOTHIR,                 // GT_RSH_LO
#endif

#if FEATURE_HW_INTRINSICS
        DBK_NONE,                   // GT_HWINTRINSIC
#endif

        DBK_NOTHIR,                 // GT_INC_SATURATE
        DBK_NOTHIR,                 // GT_MULHI

#if TARGET_32BIT || TARGET_ARM64
        DBK_NOTHIR,                 // GT_MUL_LONG
#endif

        DBK_NOTHIR,                 // GT_AND_NOT
        DBK_NOTHIR,                 // GT_OR_NOT
        DBK_NOTHIR,                 // GT_XOR_NOT

#if TARGET_ARM64
        DBK_NOTHIR,                 // GT_BFIZ
#endif

        DBK_NOTHIR,                 // GT_CMP
        DBK_NOTHIR,                 // GT_TEST

#if TARGET_XARCH
        DBK_NOTHIR,                 // GT_BT
#endif

        DBK_NOTHIR,                 // GT_JCMP
        DBK_NOTHIR,                 // GT_JTEST
        DBK_NOTHIR,                 // GT_JCC
        DBK_NOTHIR,                 // GT_SETCC
        DBK_NOTHIR,                 // GT_SELECTCC

#if TARGET_ARM64 || TARGET_AMD64
        DBK_NOTHIR,                 // GT_CCMP
#endif

#if TARGET_ARM64
        DBK_NOTHIR,                 // GT_SELECT_INCCC
        DBK_NOTHIR,                 // GT_SELECT_INVCC
        DBK_NOTHIR,                 // GT_SELECT_NEGCC
        DBK_NOTHIR,                 // GT_SELECT_INC
        DBK_NOTHIR,                 // GT_SELECT_INV
        DBK_NOTHIR,                 // GT_SELECT_NEG
#endif

#if TARGET_RISCV64
        DBK_NOTHIR,                 // GT_SH1ADD
        DBK_NOTHIR,                 // GT_SH1ADD_UW
        DBK_NOTHIR,                 // GT_SH2ADD
        DBK_NOTHIR,                 // GT_SH2ADD_UW
        DBK_NOTHIR,                 // GT_SH3ADD
        DBK_NOTHIR,                 // GT_SH3ADD_UW
        DBK_NOTHIR,                 // GT_ADD_UW
        DBK_NOTHIR,                 // GT_SLLI_UW
        DBK_NOTHIR,                 // GT_BIT_SET
        DBK_NOTHIR,                 // GT_BIT_CLEAR
        DBK_NOTHIR,                 // GT_BIT_INVERT
#endif

        DBK_NONE,                   // GT_JTRUE
        DBK_NOTLIR,                 // GT_ARR_ELEM
        DBK_NOCONTAIN,              // GT_CALL
        DBK_NONE,                   // GT_FIELD_LIST
        DBK_NONE,                   // GT_RETURN
        DBK_NONE,                   // GT_SWITCH
        DBK_NONE,                   // GT_NO_OP
        DBK_NONE,                   // GT_RETURN_SUSPEND
        DBK_NONE,                   // GT_PATCHPOINT
        DBK_NONE,                   // GT_PATCHPOINT_FORCED
        DBK_NOTHIR,                 // GT_START_NONGC
        DBK_NOTHIR,                 // GT_START_PREEMPTGC
        DBK_NOTHIR,                 // GT_PROF_HOOK
        DBK_NONE,                   // GT_RETFILT

#if SWIFT_SUPPORT
        DBK_NONE,                   // GT_SWIFT_ERROR
        DBK_NONE,                   // GT_SWIFT_ERROR_RET
#endif

#if TARGET_WASM
        DBK_NOTHIR,                 // GT_WASM_JEXCEPT
        DBK_NOTHIR,                 // GT_WASM_THROW_REF
#endif

        DBK_NOCONTAIN|DBK_NOTHIR,   // GT_JMPTABLE
        DBK_NOTHIR,                 // GT_SWITCH_TABLE
        DBK_NOTHIR,                 // GT_PHYSREG
        DBK_NOTHIR,                 // GT_RETURNTRAP
        DBK_NOTHIR,                 // GT_PUTARG_REG
        DBK_NOTHIR,                 // GT_PUTARG_STK
        DBK_NOTHIR,                 // GT_SWAP
        DBK_NOTHIR,                 // GT_COPY
        DBK_NOTHIR,                 // GT_RELOAD
        DBK_NOTHIR,                 // GT_IL_OFFSET
        DBK_NOTHIR,                 // GT_RECORD_ASYNC_RESUME
    ];
#endif

#if MEASURE_NODE_SIZE
    private static readonly string[] s_structNames = [
        "",                             // GT_NONE
        nameof(GenTreePhi),             // GT_PHI
        nameof(GenTreePhiArg),          // GT_PHI_ARG
        nameof(GenTreeLclVar),          // GT_LCL_VAR
        nameof(GenTreeLclFld),          // GT_LCL_FLD
        nameof(GenTreeLclVar),          // GT_STORE_LCL_VAR
        nameof(GenTreeLclFld),          // GT_STORE_LCL_FLD
        nameof(GenTreeLclFld),          // GT_LCL_ADDR
        nameof(GenTree),                // GT_CATCH_ARG
        nameof(GenTree),                // GT_ASYNC_CONTINUATION
        nameof(GenTree),                // GT_LABEL
        nameof(GenTreeVal),             // GT_JMP
        nameof(GenTreeFptrVal),         // GT_FTN_ADDR
        nameof(GenTreeRetExpr),         // GT_RET_EXPR
        nameof(GenTree),                // GT_GCPOLL
        nameof(GenTreeVal),             // GT_ASYNC_RESUME_INFO
        nameof(GenTree),                // GT_FTN_ENTRY
        nameof(GenTreeIntCon),          // GT_CNS_INT
        nameof(GenTreeLngCon),          // GT_CNS_LNG
        nameof(GenTreeDblCon),          // GT_CNS_DBL
        nameof(GenTreeStrCon),          // GT_CNS_STR

#if FEATURE_SIMD
        nameof(GenTreeVecCon),          // GT_CNS_VEC
#endif

#if FEATURE_MASKED_HW_INTRINSICS
        nameof(GenTreeMskCon),          // GT_CNS_MSK
#endif

        nameof(GenTree),                // GT_NOP
        nameof(GenTreeIntrinsic),       // GT_INTRINSIC
        nameof(GenTree),                // GT_KEEPALIVE
        nameof(GenTreeCast),            // GT_CAST
        nameof(GenTreeOp),              // GT_BITCAST
        nameof(GenTreeOp),              // GT_CKFINITE
        nameof(GenTreeOp),              // GT_LCLHEAP
        nameof(GenTreeBoundsChk),       // GT_BOUNDS_CHECK
        nameof(GenTree),                // GT_MEMORYBARRIER
        nameof(GenTreeOp),              // GT_LOCKADD
        nameof(GenTreeOp),              // GT_XAND
        nameof(GenTreeOp),              // GT_XORR
        nameof(GenTreeOp),              // GT_XADD
        nameof(GenTreeOp),              // GT_XCHG
        nameof(GenTreeCmpXchg),         // GT_CMPXCHG
        nameof(GenTreeIndir),           // GT_IND
        nameof(GenTreeStoreInd),        // GT_STOREIND
        nameof(GenTreeBlk),             // GT_BLK
        nameof(GenTreeBlk),             // GT_STORE_BLK
        nameof(GenTreeIndir),           // GT_NULLCHECK
        nameof(GenTreeArrLen),          // GT_ARR_LENGTH
        nameof(GenTreeMDArr),           // GT_MDARR_LENGTH
        nameof(GenTreeMDArr),           // GT_MDARR_LOWER_BOUND
        nameof(GenTreeFieldAddr),       // GT_FIELD_ADDR
        nameof(GenTreeAllocObj),        // GT_ALLOCOBJ
        nameof(GenTreeOp),              // GT_INIT_VAL
        nameof(GenTreeBox),             // GT_BOX
        nameof(GenTreeRuntimeLookup),   // GT_RUNTIMELOOKUP
        nameof(GenTreeArrAddr),         // GT_ARR_ADDR
        nameof(GenTreeOp),              // GT_BSWAP
        nameof(GenTreeOp),              // GT_BSWAP16
        nameof(GenTreeOp),              // GT_LZCNT
        nameof(GenTreeOp),              // GT_NONLOCAL_JMP
        nameof(GenTreeOp),              // GT_NOT
        nameof(GenTreeOp),              // GT_NEG
        nameof(GenTreeOp),              // GT_OR
        nameof(GenTreeOp),              // GT_XOR
        nameof(GenTreeOp),              // GT_AND
        nameof(GenTreeOp),              // GT_LSH
        nameof(GenTreeOp),              // GT_RSH
        nameof(GenTreeOp),              // GT_RSZ
        nameof(GenTreeOp),              // GT_ROL
        nameof(GenTreeOp),              // GT_ROR
        nameof(GenTreeOp),              // GT_ADD
        nameof(GenTreeOp),              // GT_SUB
        nameof(GenTreeOp),              // GT_MUL
        nameof(GenTreeOp),              // GT_DIV
        nameof(GenTreeOp),              // GT_MOD
        nameof(GenTreeOp),              // GT_UDIV
        nameof(GenTreeOp),              // GT_UMOD
        nameof(GenTreeOp),              // GT_EQ
        nameof(GenTreeOp),              // GT_NE
        nameof(GenTreeOp),              // GT_LT
        nameof(GenTreeOp),              // GT_LE
        nameof(GenTreeOp),              // GT_GE
        nameof(GenTreeOp),              // GT_GT
        nameof(GenTreeOp),              // GT_TEST_EQ
        nameof(GenTreeOp),              // GT_TEST_NE

#if TARGET_XARCH
        nameof(GenTreeOp),              // GT_BITTEST_EQ
        nameof(GenTreeOp),              // GT_BITTEST_NE
#endif

        nameof(GenTreeConditional),     // GT_SELECT
        nameof(GenTreeOp),              // GT_COMMA
        nameof(GenTreeQmark),           // GT_QMARK
        nameof(GenTreeColon),           // GT_COLON
        nameof(GenTreeIndexAddr),       // GT_INDEX_ADDR
        nameof(GenTreeAddrMode),        // GT_LEA

#if TARGET_32BIT
        nameof(GenTreeOp),              // GT_LONG
        nameof(GenTreeOp),              // GT_ADD_LO
        nameof(GenTreeOp),              // GT_ADD_HI
        nameof(GenTreeOp),              // GT_SUB_LO
        nameof(GenTreeOp),              // GT_SUB_HI
        nameof(GenTreeOp),              // GT_LSH_HI
        nameof(GenTreeOp),              // GT_RSH_LO
#endif

#if FEATURE_HW_INTRINSICS
        nameof(GenTreeHWIntrinsic),     // GT_HWINTRINSIC
#endif

        nameof(GenTreeOp),              // GT_INC_SATURATE
        nameof(GenTreeOp),              // GT_MULHI

#if TARGET_32BIT
        nameof(GenTreeMultiRegOp),      // GT_MUL_LONG
#elif  TARGET_ARM64
        nameof(GenTreeOp),              // GT_MUL_LONG
#endif

        nameof(GenTreeOp),              // GT_AND_NOT
        nameof(GenTreeOp),              // GT_OR_NOT
        nameof(GenTreeOp),              // GT_XOR_NOT

#if TARGET_ARM64
        nameof(GenTreeOp),              // GT_BFIZ
#endif

        nameof(GenTreeOp),              // GT_CMP 
        nameof(GenTreeOp),              // GT_TEST 

#if TARGET_XARCH
        nameof(GenTreeOp),              // GT_BT
#endif

        nameof(GenTreeOpCC),            // GT_JCMP
        nameof(GenTreeOpCC),            // GT_JTEST
        nameof(GenTreeCC),              // GT_JCC
        nameof(GenTreeCC),              // GT_SETCC
        nameof(GenTreeOpCC),            // GT_SELECTCC

#if TARGET_ARM64 || TARGET_AMD64
        nameof(GenTreeCCMP),            // GT_CCMP
#endif

#if TARGET_ARM64
        nameof(GenTreeOpCC),            // GT_SELECT_INCCC
        nameof(GenTreeOpCC),            // GT_SELECT_INVCC
        nameof(GenTreeOpCC),            // GT_SELECT_NEGCC
        nameof(GenTreeOpConditional),   // GT_SELECT_INC
        nameof(GenTreeOpConditional),   // GT_SELECT_INV
        nameof(GenTreeOpConditional),   // GT_SELECT_NEG
#endif

#if TARGET_RISCV64
        nameof(GenTreeOp),              // GT_SH1ADD
        nameof(GenTreeOp),              // GT_SH1ADD_UW
        nameof(GenTreeOp),              // GT_SH2ADD
        nameof(GenTreeOp),              // GT_SH2ADD_UW
        nameof(GenTreeOp),              // GT_SH3ADD
        nameof(GenTreeOp),              // GT_SH3ADD_UW
        nameof(GenTreeOp),              // GT_ADD_UW
        nameof(GenTreeOp),              // GT_SLLI_UW
        nameof(GenTreeOp),              // GT_BIT_SET
        nameof(GenTreeOp),              // GT_BIT_CLEAR
        nameof(GenTreeOp),              // GT_BIT_INVERT
#endif

        nameof(GenTreeOp),              // GT_JTRUE
        nameof(GenTreeArrElem),         // GT_ARR_ELEM
        nameof(GenTreeCall),            // GT_CALL
        nameof(GenTreeFieldList),       // GT_FIELD_LIST
        nameof(GenTreeOp),              // GT_RETURN
        nameof(GenTreeOp),              // GT_SWITCH
        nameof(GenTree),                // GT_NO_OP
        nameof(GenTreeOp),              // GT_RETURN_SUSPEND
        nameof(GenTreeOp),              // GT_PATCHPOINT
        nameof(GenTreeOp),              // GT_PATCHPOINT_FORCED
        nameof(GenTree),                // GT_START_NONGC
        nameof(GenTree),                // GT_START_PREEMPTGC
        nameof(GenTree),                // GT_PROF_HOOK
        nameof(GenTreeOp),              // GT_RETFILT

#if SWIFT_SUPPORT
        nameof(GenTree),                // GT_SWIFT_ERROR
        nameof(GenTreeOp),              // GT_SWIFT_ERROR_RET
#endif

#if TARGET_WASM
        nameof(GenTree),                // GT_WASM_JEXCEPT
        nameof(GenTree),                // GT_WASM_THROW_REF
#endif

        nameof(GenTree),                // GT_JMPTABLE
        nameof(GenTreeOp),              // GT_SWITCH_TABLE
        nameof(GenTreePhysReg),         // GT_PHYSREG
        nameof(GenTreeOp),              // GT_RETURNTRAP
        nameof(GenTreeOp),              // GT_PUTARG_REG
        nameof(GenTreePutArgStk),       // GT_PUTARG_STK
        nameof(GenTreeOp),              // GT_SWAP
        nameof(GenTreeCopyOrReload),    // GT_COPY
        nameof(GenTreeCopyOrReload),    // GT_RELOAD
        nameof(GenTreeILOffset),        // GT_IL_OFFSET
        nameof(GenTreeVal),             // GT_RECORD_ASYNC_RESUME
    ];
#endif

    extension(genTreeOps oper)
    {
        public bool ConsumesFlags
        {
            get
            {
#if TARGET_ARM64
                assert(AreContiguous(GT_JCC, GT_SETCC, GT_SELECTCC, GT_CCMP, GT_SELECT_INCCC, GT_SELECT_INVCC, GT_SELECT_NEGCC));
                return oper is >= GT_JCC and <= GT_SELECT_NEGCC;
#elif TARGET_AMD64
                assert(AreContiguous(GT_JCC, GT_SETCC, GT_SELECTCC, GT_CCMP));
                return oper is >= GT_JCC and <= GT_CCMP;
#elif TARGET_32BIT
                assert(AreContiguous(GT_JCC, GT_SETCC, GT_SELECTCC));
                return oper is (>= GT_JCC and <= GT_SELECTCC) or GT_ADD_HI or GT_SUB_HI;
#else
                assert(AreContiguous(GT_JCC, GT_SETCC, GT_SELECTCC));
                return oper is >= GT_JCC and <= GT_SELECTCC;
#endif

            }
        }

#if DEBUG
        public GenTreeDebugOperKind DebugKind
        {
            get
            {
                assert(s_debugKinds.Length == (int)(GT_COUNT));
                return s_debugKinds[(int)(oper)];
            }
        }
#endif

        public bool IsAddrMode => oper is GT_LEA;

        public bool IsAnyLocal
        {
            get
            {
                assert(AreContiguous(GT_PHI_ARG, GT_LCL_VAR, GT_LCL_FLD, GT_STORE_LCL_VAR, GT_STORE_LCL_FLD, GT_LCL_ADDR));
                return oper is >= GT_PHI_ARG and <= GT_LCL_ADDR;
            }
        }

        public bool IsArrLength => oper is GT_ARR_LENGTH or GT_MDARR_LENGTH;

        /// <summary>Is this an access of an SZ array length, MD array length, or MD array lower bounds?</summary>
        /// <remarks>Valid oper kinds for <see cref="GenTreeArrCommon" />.</remarks>
        public bool IsArrMetadata
        {
            get
            {
                assert(AreContiguous(GT_ARR_LENGTH, GT_MDARR_LENGTH, GT_MDARR_LOWER_BOUND));
                return oper is >= GT_ARR_LENGTH and <= GT_MDARR_LOWER_BOUND;
            }
        }

        public bool IsAtomic
        {
            get
            {
                assert(AreContiguous(GT_LOCKADD, GT_XAND, GT_XORR, GT_XADD, GT_XCHG, GT_CMPXCHG));
                return oper is >= GT_LOCKADD and <= GT_CMPXCHG;
            }
        }

        public bool IsBinary => (oper.Kind & GTK_BINOP) != 0;

        public bool IsBlk
        {
            get
            {
                var result = oper is GT_BLK or GT_STORE_BLK;
                assert(result == ((oper is GT_BLK) || oper.IsStoreBlk));
                return result;
            }
        }

        public bool IsCall => oper is GT_CALL;

        public bool IsCC => oper is GT_JCC or GT_SETCC;

        /// <summary>Oper is a compare that generates a cmp instruction (as opposed to a test instruction).</summary>
        public bool IsCmpCompare
        {
            get
            {
                assert(AreContiguous(GT_EQ, GT_NE, GT_LT, GT_LE, GT_GE, GT_GT));
                return oper is >= GT_EQ and <= GT_GT;
            }
        }

        public bool IsCnsFltOrDbl => oper is GT_CNS_DBL;

        public bool IsCnsIntOrI => oper is GT_CNS_INT;

#if FEATURE_MASKED_HW_INTRINSICS
        public bool IsCnsMsk => oper is GT_CNS_MSK;
#else
        public bool IsCnsMsk => false;
#endif

#if FEATURE_SIMD
        public bool IsCnsVec => oper is GT_CNS_VEC;
#else
        public bool IsCnsVec => false;
#endif

        public bool IsCommutative => (oper.Kind & GTK_COMMUTE) != 0;

        public bool IsCompare
        {
            get
            {
                // Note that only GT_EQ to GT_GT are HIR nodes, GT_TEST and GT_BITTEST nodes are backend nodes only.
#if TARGET_XARCH
                assert(AreContiguous(GT_EQ, GT_NE, GT_LT, GT_LE, GT_GE, GT_GT, GT_TEST_EQ, GT_TEST_NE, GT_BITTEST_EQ, GT_BITTEST_NE));
                return oper is >= GT_EQ and <= GT_BITTEST_NE;
#else
                assert(AreContiguous(GT_EQ, GT_NE, GT_LT, GT_LE, GT_GE, GT_GT, GT_TEST_EQ, GT_TEST_NE));
                return oper is >= GT_EQ and <= GT_TEST_NE;
#endif
            }
        }

        public bool IsConditional => oper is GT_SELECT;

        public bool IsConditionalJump
        {
            get
            {
#if TARGET_WASM
                assert(AreContiguous(GT_JCMP, GT_JTEST, GT_JCC));
                return oper is GT_JTRUE or (>= GT_JCMP and <= GT_JCC) or GT_WASM_JEXCEPT;
#else
                assert(AreContiguous(GT_JCMP, GT_JTEST, GT_JCC));
                return oper is GT_JTRUE or (>= GT_JCMP and <= GT_JCC);
#endif
            }
        }

        public bool IsConst
        {
            get
            {
#if FEATURE_MASKED_HW_INTRINSICS
                assert(AreContiguous(GT_CNS_INT, GT_CNS_LNG, GT_CNS_DBL, GT_CNS_STR, GT_CNS_VEC, GT_CNS_MSK));
                return oper is >= GT_CNS_INT and <= GT_CNS_MSK;
#elif FEATURE_SIMD
                assert(AreContiguous(GT_CNS_INT, GT_CNS_LNG, GT_CNS_DBL, GT_CNS_STR, GT_CNS_VEC));
                return oper is >= GT_CNS_INT and <= GT_CNS_VEC;
#else
                assert(AreContiguous(GT_CNS_INT, GT_CNS_LNG, GT_CNS_DBL, GT_CNS_STR));
                return oper is >= GT_CNS_INT and <= GT_CNS_STR;
#endif
            }
        }

        public bool IsCopyOrReload => oper is GT_COPY or GT_RELOAD;

        public bool IsExOp => (oper.Kind & GTK_EXOP) != 0;

        public bool IsFieldList => oper is GT_FIELD_LIST;

#if FEATURE_HW_INTRINSICS
        public bool IsHWIntrinsic => oper is GT_HWINTRINSIC;
#else
        public bool IsHWIntrinsic => false;
#endif

        public bool IsIndir
        {
            get
            {
                assert(AreContiguous(GT_LOCKADD, GT_XAND, GT_XORR, GT_XADD, GT_XCHG, GT_CMPXCHG, GT_IND, GT_STOREIND, GT_BLK, GT_STORE_BLK, GT_NULLCHECK));
                return oper is >= GT_LOCKADD and <= GT_NULLCHECK;
            }
        }

        public bool IsIndirOrArrMetaData
        {
            get
            {
                assert(AreContiguous(GT_LOCKADD, GT_XAND, GT_XORR, GT_XADD, GT_XCHG, GT_CMPXCHG, GT_IND, GT_STOREIND, GT_BLK, GT_STORE_BLK, GT_NULLCHECK, GT_ARR_LENGTH, GT_MDARR_LENGTH, GT_MDARR_LOWER_BOUND));
                return oper is >= GT_LOCKADD and <= GT_MDARR_LOWER_BOUND;
            }
        }

        public bool IsInitVal => oper is GT_INIT_VAL;

#if TARGET_32BIT
        public bool IsIntegralConst => oper is GT_CNS_INT or GT_CNS_LNG;
#else
        public bool IsIntegralConst => oper is GT_CNS_INT;
#endif

        public bool IsLclField => oper is GT_LCL_FLD or GT_STORE_LCL_FLD;

        public bool IsLeaf => (oper.Kind & GTK_LEAF) != 0;

        public bool IsLoad => oper is GT_IND or GT_BLK;

        public bool IsLocal
        {
            get
            {
                assert(AreContiguous(GT_PHI_ARG, GT_LCL_VAR, GT_LCL_FLD, GT_STORE_LCL_VAR, GT_STORE_LCL_FLD));
                return oper is >= GT_PHI_ARG and <= GT_STORE_LCL_FLD;
            }
        }

        public bool IsLocalField => oper is GT_LCL_FLD or GT_STORE_LCL_FLD or GT_LCL_ADDR;

        public bool IsLocalRead
        {
            get
            {
                assert(AreContiguous(GT_PHI_ARG, GT_LCL_VAR, GT_LCL_FLD));
                var result = oper is >= GT_PHI_ARG and <= GT_LCL_FLD;

                assert(result == (oper.IsLocal && !oper.IsLocalStore));
                return result;
            }
        }

        public bool IsLocalStore => oper is GT_STORE_LCL_VAR or GT_STORE_LCL_FLD;

#if TARGET_32BIT
        public bool IsLong => oper is GT_CNS_LONG;
#else
        public bool IsLong => false;
#endif

        public bool IsMdArr => oper is GT_MDARR_LENGTH or GT_MDARR_LOWER_BOUND;

#if TARGET_32BIT || TARGET_ARM64
        public bool IsMul => oper is GT_MUL or GT_MULHI or GT_MUL_LONG;
#else
        public bool IsMul => oper is GT_MUL or GT_MULHI;
#endif

#if FEATURE_HW_INTRINSICS
        public bool IsMultiOp => oper is GT_HWINTRINSIC;
#else
        public bool IsMultiOp => false;
#endif

#if TARGET_32BIT
        public bool IsMultiRegOp => oper is GT_MUL_LONG;
#else
        public bool IsMultiRegOp => false;
#endif

        public bool IsNonPhiLocal
        {
            get
            {
                assert(AreContiguous(GT_LCL_VAR, GT_LCL_FLD, GT_STORE_LCL_VAR, GT_STORE_LCL_FLD));
                var result = oper is >= GT_LCL_VAR and <= GT_STORE_LCL_FLD;

                assert(result == (oper.IsLocal && (oper is not GT_PHI_ARG)));
                return result;
            }
        }

        public bool IsPutArg => oper is GT_PUTARG_REG or GT_PUTARG_STK;

        public bool IsPutArgReg => oper is GT_PUTARG_REG;

        public bool IsPutArgStk => oper is GT_PUTARG_STK;

#if TARGET_XARCH
        public bool IsRmwMemOp
        {
            get
            {
                assert(AreContiguous(GT_NOT, GT_NEG, GT_OR, GT_XOR, GT_AND, GT_LSH, GT_RSH, GT_RSZ, GT_ROL, GT_ROR, GT_ADD, GT_SUB));
                return oper is >= GT_NOT and <= GT_SUB;
            }
        }
#endif

        public bool IsRotate => oper is GT_ROL or GT_ROR;

        public bool IsScalarLocal => oper is GT_LCL_VAR or GT_STORE_LCL_VAR;

        public bool IsShift
        {
            get
            {
                assert(AreContiguous(GT_LSH, GT_RSH, GT_RSZ));
                return oper is >= GT_LSH and <= GT_RSZ;
            }
        }

#if TARGET_32BIT
        public bool IsShiftLong => oper is GT_LSH_HI or GT_RSH_LO;
#else
        public bool IsShiftLong => false;
#endif

        public bool IsShiftOrRotate
        {
            get
            {
#if TARGET_32BIT
                assert(AreContiguous(GT_LSH, GT_RSH, GT_RSZ, GT_ROL, GT_ROR));
                var result = oper is (>= GT_LSH and <= GT_ROR) or GT_LSH_HI or GT_RSH_LO;

                assert(result == (oper.IsShift || oper.IsRotate || oper.IsShiftLong));
                return result;
#else
                assert(AreContiguous(GT_LSH, GT_RSH, GT_RSZ, GT_ROL, GT_ROR));
                var result = oper is >= GT_LSH and <= GT_ROR;

                assert(result == (oper.IsShift || oper.IsRotate));
                return result;
#endif
            }
        }

        public bool IsSimple => (oper.Kind & GTK_SMPOP) != 0;

        public bool IsSpecial => (oper.Kind & GTK_KINDMASK) == GTK_SPECIAL;

        public bool IsSsaDef => oper is GT_STORE_LCL_VAR or GT_STORE_LCL_FLD or GT_CALL;

        public bool IsStore => (oper.Kind & GTK_STORE) != 0;

        public bool IsStoreBlk => oper is GT_STORE_BLK;

        /// <summary>This returns true only for GT_IND and GT_STOREIND, and is used in contexts where a "true" indirection is expected (i.e. either a load to or a store from a single register).</summary>
        /// <remarks><see cref="get_IsIndir"/> returns true also for indirection nodes such as GT_BLK, etc. as well as GT_NULLCHECK.</remarks>
        public bool IsTrueIndir => oper is GT_IND or GT_STOREIND;

        public bool IsUnary => (oper.Kind & GTK_UNOP) != 0;

        public GenTreeOperKind Kind
        {
            get
            {
                assert(s_kinds.Length == (int)(GT_COUNT));
                return s_kinds[(int)(oper)];
            }
        }

        public bool MayOverflow
        {
            get
            {
#if TARGET_32BIT
                assert(AreContiguous(GT_ADD, GT_SUB, GT_MUL));
                return oper is (>= GT_ADD and <= GT_MUL) or GT_CAST or GT_ADD_HI or GT_SUB_HI;
#else
                assert(AreContiguous(GT_ADD, GT_SUB, GT_MUL));
                return oper is (>= GT_ADD and <= GT_MUL) or GT_CAST;
#endif
            }
        }

#if MEASURE_NODE_SIZE
        public string StructName
        {
            get
            {
                assert(s_structNames.Length == (int)(GT_COUNT));
                return s_structNames[(int)(oper)];
            }
        }
#endif
    }
}
