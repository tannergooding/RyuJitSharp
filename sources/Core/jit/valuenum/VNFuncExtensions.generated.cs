// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public static partial class VNFuncExtensions
{
    internal static readonly ValueNumStore.VNFOpAttrib[] s_attribs = [
        ValueNumStore.GetOpAttribsForGenTree(GT_NONE, commute: false, illegalAsVNFunc: false, GT_NONE.Kind), // VNF_NONE
        ValueNumStore.GetOpAttribsForGenTree(GT_PHI, commute: false, illegalAsVNFunc: false, GT_PHI.Kind), // VNF_PHI
        ValueNumStore.GetOpAttribsForGenTree(GT_PHI_ARG, commute: false, illegalAsVNFunc: false, GT_PHI_ARG.Kind), // VNF_PHI_ARG
        ValueNumStore.GetOpAttribsForGenTree(GT_LCL_VAR, commute: false, illegalAsVNFunc: false, GT_LCL_VAR.Kind), // VNF_LCL_VAR
        ValueNumStore.GetOpAttribsForGenTree(GT_LCL_FLD, commute: false, illegalAsVNFunc: false, GT_LCL_FLD.Kind), // VNF_LCL_FLD
        ValueNumStore.GetOpAttribsForGenTree(GT_STORE_LCL_VAR, commute: false, illegalAsVNFunc: true, GT_STORE_LCL_VAR.Kind), // VNF_STORE_LCL_VAR
        ValueNumStore.GetOpAttribsForGenTree(GT_STORE_LCL_FLD, commute: false, illegalAsVNFunc: true, GT_STORE_LCL_FLD.Kind), // VNF_STORE_LCL_FLD
        ValueNumStore.GetOpAttribsForGenTree(GT_LCL_ADDR, commute: false, illegalAsVNFunc: false, GT_LCL_ADDR.Kind), // VNF_LCL_ADDR
        ValueNumStore.GetOpAttribsForGenTree(GT_CATCH_ARG, commute: false, illegalAsVNFunc: false, GT_CATCH_ARG.Kind), // VNF_CATCH_ARG
        ValueNumStore.GetOpAttribsForGenTree(GT_ASYNC_CONTINUATION, commute: false, illegalAsVNFunc: false, GT_ASYNC_CONTINUATION.Kind), // VNF_ASYNC_CONTINUATION
        ValueNumStore.GetOpAttribsForGenTree(GT_LABEL, commute: false, illegalAsVNFunc: false, GT_LABEL.Kind), // VNF_LABEL
        ValueNumStore.GetOpAttribsForGenTree(GT_JMP, commute: false, illegalAsVNFunc: false, GT_JMP.Kind), // VNF_JMP
        ValueNumStore.GetOpAttribsForGenTree(GT_FTN_ADDR, commute: false, illegalAsVNFunc: false, GT_FTN_ADDR.Kind), // VNF_FTN_ADDR
        ValueNumStore.GetOpAttribsForGenTree(GT_RET_EXPR, commute: false, illegalAsVNFunc: false, GT_RET_EXPR.Kind), // VNF_RET_EXPR
        ValueNumStore.GetOpAttribsForGenTree(GT_GCPOLL, commute: false, illegalAsVNFunc: false, GT_GCPOLL.Kind), // VNF_GCPOLL
        ValueNumStore.GetOpAttribsForGenTree(GT_ASYNC_RESUME_INFO, commute: false, illegalAsVNFunc: false, GT_ASYNC_RESUME_INFO.Kind), // VNF_ASYNC_RESUME_INFO
        ValueNumStore.GetOpAttribsForGenTree(GT_FTN_ENTRY, commute: false, illegalAsVNFunc: false, GT_FTN_ENTRY.Kind), // VNF_FTN_ENTRY
        ValueNumStore.GetOpAttribsForGenTree(GT_CNS_INT, commute: false, illegalAsVNFunc: false, GT_CNS_INT.Kind), // VNF_CNS_INT
        ValueNumStore.GetOpAttribsForGenTree(GT_CNS_LNG, commute: false, illegalAsVNFunc: false, GT_CNS_LNG.Kind), // VNF_CNS_LNG
        ValueNumStore.GetOpAttribsForGenTree(GT_CNS_DBL, commute: false, illegalAsVNFunc: false, GT_CNS_DBL.Kind), // VNF_CNS_DBL
        ValueNumStore.GetOpAttribsForGenTree(GT_CNS_STR, commute: false, illegalAsVNFunc: false, GT_CNS_STR.Kind), // VNF_CNS_STR
#if FEATURE_SIMD
        ValueNumStore.GetOpAttribsForGenTree(GT_CNS_VEC, commute: false, illegalAsVNFunc: false, GT_CNS_VEC.Kind), // VNF_CNS_VEC
#endif
#if FEATURE_MASKED_HW_INTRINSICS
        ValueNumStore.GetOpAttribsForGenTree(GT_CNS_MSK, commute: false, illegalAsVNFunc: false, GT_CNS_MSK.Kind), // VNF_CNS_MSK
#endif
        ValueNumStore.GetOpAttribsForGenTree(GT_NOT, commute: false, illegalAsVNFunc: false, GT_NOT.Kind), // VNF_NOT
        ValueNumStore.GetOpAttribsForGenTree(GT_NOP, commute: false, illegalAsVNFunc: true, GT_NOP.Kind), // VNF_NOP
        ValueNumStore.GetOpAttribsForGenTree(GT_NEG, commute: false, illegalAsVNFunc: false, GT_NEG.Kind), // VNF_NEG
        ValueNumStore.GetOpAttribsForGenTree(GT_INTRINSIC, commute: false, illegalAsVNFunc: false, GT_INTRINSIC.Kind), // VNF_INTRINSIC
        ValueNumStore.GetOpAttribsForGenTree(GT_KEEPALIVE, commute: false, illegalAsVNFunc: false, GT_KEEPALIVE.Kind), // VNF_KEEPALIVE
        ValueNumStore.GetOpAttribsForGenTree(GT_CAST, commute: false, illegalAsVNFunc: false, GT_CAST.Kind), // VNF_CAST
        ValueNumStore.GetOpAttribsForGenTree(GT_BITCAST, commute: false, illegalAsVNFunc: true, GT_BITCAST.Kind), // VNF_BITCAST
        ValueNumStore.GetOpAttribsForGenTree(GT_CKFINITE, commute: false, illegalAsVNFunc: true, GT_CKFINITE.Kind), // VNF_CKFINITE
        ValueNumStore.GetOpAttribsForGenTree(GT_LCLHEAP, commute: false, illegalAsVNFunc: true, GT_LCLHEAP.Kind), // VNF_LCLHEAP
        ValueNumStore.GetOpAttribsForGenTree(GT_BOUNDS_CHECK, commute: false, illegalAsVNFunc: true, GT_BOUNDS_CHECK.Kind), // VNF_BOUNDS_CHECK
        ValueNumStore.GetOpAttribsForGenTree(GT_MEMORYBARRIER, commute: false, illegalAsVNFunc: false, GT_MEMORYBARRIER.Kind), // VNF_MEMORYBARRIER
        ValueNumStore.GetOpAttribsForGenTree(GT_LOCKADD, commute: false, illegalAsVNFunc: true, GT_LOCKADD.Kind), // VNF_LOCKADD
        ValueNumStore.GetOpAttribsForGenTree(GT_XAND, commute: false, illegalAsVNFunc: true, GT_XAND.Kind), // VNF_XAND
        ValueNumStore.GetOpAttribsForGenTree(GT_XORR, commute: false, illegalAsVNFunc: true, GT_XORR.Kind), // VNF_XORR
        ValueNumStore.GetOpAttribsForGenTree(GT_XADD, commute: false, illegalAsVNFunc: true, GT_XADD.Kind), // VNF_XADD
        ValueNumStore.GetOpAttribsForGenTree(GT_XCHG, commute: false, illegalAsVNFunc: true, GT_XCHG.Kind), // VNF_XCHG
        ValueNumStore.GetOpAttribsForGenTree(GT_CMPXCHG, commute: false, illegalAsVNFunc: true, GT_CMPXCHG.Kind), // VNF_CMPXCHG
        ValueNumStore.GetOpAttribsForGenTree(GT_IND, commute: false, illegalAsVNFunc: true, GT_IND.Kind), // VNF_IND
        ValueNumStore.GetOpAttribsForGenTree(GT_STOREIND, commute: false, illegalAsVNFunc: true, GT_STOREIND.Kind), // VNF_STOREIND
        ValueNumStore.GetOpAttribsForGenTree(GT_BLK, commute: false, illegalAsVNFunc: true, GT_BLK.Kind), // VNF_BLK
        ValueNumStore.GetOpAttribsForGenTree(GT_STORE_BLK, commute: false, illegalAsVNFunc: true, GT_STORE_BLK.Kind), // VNF_STORE_BLK
        ValueNumStore.GetOpAttribsForGenTree(GT_NULLCHECK, commute: false, illegalAsVNFunc: true, GT_NULLCHECK.Kind), // VNF_NULLCHECK
        ValueNumStore.GetOpAttribsForGenTree(GT_ARR_LENGTH, commute: false, illegalAsVNFunc: false, GT_ARR_LENGTH.Kind), // VNF_ARR_LENGTH
        ValueNumStore.GetOpAttribsForGenTree(GT_MDARR_LENGTH, commute: false, illegalAsVNFunc: true, GT_MDARR_LENGTH.Kind), // VNF_MDARR_LENGTH
        ValueNumStore.GetOpAttribsForGenTree(GT_MDARR_LOWER_BOUND, commute: false, illegalAsVNFunc: true, GT_MDARR_LOWER_BOUND.Kind), // VNF_MDARR_LOWER_BOUND
        ValueNumStore.GetOpAttribsForGenTree(GT_FIELD_ADDR, commute: false, illegalAsVNFunc: false, GT_FIELD_ADDR.Kind), // VNF_FIELD_ADDR
        ValueNumStore.GetOpAttribsForGenTree(GT_ALLOCOBJ, commute: false, illegalAsVNFunc: false, GT_ALLOCOBJ.Kind), // VNF_ALLOCOBJ
        ValueNumStore.GetOpAttribsForGenTree(GT_INIT_VAL, commute: false, illegalAsVNFunc: true, GT_INIT_VAL.Kind), // VNF_INIT_VAL
        ValueNumStore.GetOpAttribsForGenTree(GT_BOX, commute: false, illegalAsVNFunc: true, GT_BOX.Kind), // VNF_BOX
        ValueNumStore.GetOpAttribsForGenTree(GT_RUNTIMELOOKUP, commute: false, illegalAsVNFunc: false, GT_RUNTIMELOOKUP.Kind), // VNF_RUNTIMELOOKUP
        ValueNumStore.GetOpAttribsForGenTree(GT_ARR_ADDR, commute: false, illegalAsVNFunc: true, GT_ARR_ADDR.Kind), // VNF_ARR_ADDR
        ValueNumStore.GetOpAttribsForGenTree(GT_BSWAP, commute: false, illegalAsVNFunc: false, GT_BSWAP.Kind), // VNF_BSWAP
        ValueNumStore.GetOpAttribsForGenTree(GT_BSWAP16, commute: false, illegalAsVNFunc: false, GT_BSWAP16.Kind), // VNF_BSWAP16
        ValueNumStore.GetOpAttribsForGenTree(GT_LZCNT, commute: false, illegalAsVNFunc: false, GT_LZCNT.Kind), // VNF_LZCNT
        ValueNumStore.GetOpAttribsForGenTree(GT_NONLOCAL_JMP, commute: false, illegalAsVNFunc: false, GT_NONLOCAL_JMP.Kind), // VNF_NONLOCAL_JMP
        ValueNumStore.GetOpAttribsForGenTree(GT_ADD, commute: true, illegalAsVNFunc: false, GT_ADD.Kind), // VNF_ADD
        ValueNumStore.GetOpAttribsForGenTree(GT_SUB, commute: false, illegalAsVNFunc: false, GT_SUB.Kind), // VNF_SUB
        ValueNumStore.GetOpAttribsForGenTree(GT_MUL, commute: true, illegalAsVNFunc: false, GT_MUL.Kind), // VNF_MUL
        ValueNumStore.GetOpAttribsForGenTree(GT_DIV, commute: false, illegalAsVNFunc: false, GT_DIV.Kind), // VNF_DIV
        ValueNumStore.GetOpAttribsForGenTree(GT_MOD, commute: false, illegalAsVNFunc: false, GT_MOD.Kind), // VNF_MOD
        ValueNumStore.GetOpAttribsForGenTree(GT_UDIV, commute: false, illegalAsVNFunc: false, GT_UDIV.Kind), // VNF_UDIV
        ValueNumStore.GetOpAttribsForGenTree(GT_UMOD, commute: false, illegalAsVNFunc: false, GT_UMOD.Kind), // VNF_UMOD
        ValueNumStore.GetOpAttribsForGenTree(GT_OR, commute: true, illegalAsVNFunc: false, GT_OR.Kind), // VNF_OR
        ValueNumStore.GetOpAttribsForGenTree(GT_XOR, commute: true, illegalAsVNFunc: false, GT_XOR.Kind), // VNF_XOR
        ValueNumStore.GetOpAttribsForGenTree(GT_AND, commute: true, illegalAsVNFunc: false, GT_AND.Kind), // VNF_AND
        ValueNumStore.GetOpAttribsForGenTree(GT_LSH, commute: false, illegalAsVNFunc: false, GT_LSH.Kind), // VNF_LSH
        ValueNumStore.GetOpAttribsForGenTree(GT_RSH, commute: false, illegalAsVNFunc: false, GT_RSH.Kind), // VNF_RSH
        ValueNumStore.GetOpAttribsForGenTree(GT_RSZ, commute: false, illegalAsVNFunc: false, GT_RSZ.Kind), // VNF_RSZ
        ValueNumStore.GetOpAttribsForGenTree(GT_ROL, commute: false, illegalAsVNFunc: false, GT_ROL.Kind), // VNF_ROL
        ValueNumStore.GetOpAttribsForGenTree(GT_ROR, commute: false, illegalAsVNFunc: false, GT_ROR.Kind), // VNF_ROR
        ValueNumStore.GetOpAttribsForGenTree(GT_EQ, commute: false, illegalAsVNFunc: false, GT_EQ.Kind), // VNF_EQ
        ValueNumStore.GetOpAttribsForGenTree(GT_NE, commute: false, illegalAsVNFunc: false, GT_NE.Kind), // VNF_NE
        ValueNumStore.GetOpAttribsForGenTree(GT_LT, commute: false, illegalAsVNFunc: false, GT_LT.Kind), // VNF_LT
        ValueNumStore.GetOpAttribsForGenTree(GT_LE, commute: false, illegalAsVNFunc: false, GT_LE.Kind), // VNF_LE
        ValueNumStore.GetOpAttribsForGenTree(GT_GE, commute: false, illegalAsVNFunc: false, GT_GE.Kind), // VNF_GE
        ValueNumStore.GetOpAttribsForGenTree(GT_GT, commute: false, illegalAsVNFunc: false, GT_GT.Kind), // VNF_GT
        ValueNumStore.GetOpAttribsForGenTree(GT_TEST_EQ, commute: false, illegalAsVNFunc: false, GT_TEST_EQ.Kind), // VNF_TEST_EQ
        ValueNumStore.GetOpAttribsForGenTree(GT_TEST_NE, commute: false, illegalAsVNFunc: false, GT_TEST_NE.Kind), // VNF_TEST_NE
#if TARGET_XARCH
        ValueNumStore.GetOpAttribsForGenTree(GT_BITTEST_EQ, commute: false, illegalAsVNFunc: false, GT_BITTEST_EQ.Kind), // VNF_BITTEST_EQ
        ValueNumStore.GetOpAttribsForGenTree(GT_BITTEST_NE, commute: false, illegalAsVNFunc: false, GT_BITTEST_NE.Kind), // VNF_BITTEST_NE
#endif
        ValueNumStore.GetOpAttribsForGenTree(GT_SELECT, commute: false, illegalAsVNFunc: false, GT_SELECT.Kind), // VNF_SELECT
        ValueNumStore.GetOpAttribsForGenTree(GT_COMMA, commute: false, illegalAsVNFunc: true, GT_COMMA.Kind), // VNF_COMMA
        ValueNumStore.GetOpAttribsForGenTree(GT_QMARK, commute: false, illegalAsVNFunc: true, GT_QMARK.Kind), // VNF_QMARK
        ValueNumStore.GetOpAttribsForGenTree(GT_COLON, commute: false, illegalAsVNFunc: true, GT_COLON.Kind), // VNF_COLON
        ValueNumStore.GetOpAttribsForGenTree(GT_INDEX_ADDR, commute: false, illegalAsVNFunc: false, GT_INDEX_ADDR.Kind), // VNF_INDEX_ADDR
        ValueNumStore.GetOpAttribsForGenTree(GT_LEA, commute: false, illegalAsVNFunc: false, GT_LEA.Kind), // VNF_LEA
#if !TARGET_64BIT
        ValueNumStore.GetOpAttribsForGenTree(GT_LONG, commute: false, illegalAsVNFunc: false, GT_LONG.Kind), // VNF_LONG
        ValueNumStore.GetOpAttribsForGenTree(GT_ADD_LO, commute: true, illegalAsVNFunc: false, GT_ADD_LO.Kind), // VNF_ADD_LO
        ValueNumStore.GetOpAttribsForGenTree(GT_ADD_HI, commute: true, illegalAsVNFunc: false, GT_ADD_HI.Kind), // VNF_ADD_HI
        ValueNumStore.GetOpAttribsForGenTree(GT_SUB_LO, commute: false, illegalAsVNFunc: false, GT_SUB_LO.Kind), // VNF_SUB_LO
        ValueNumStore.GetOpAttribsForGenTree(GT_SUB_HI, commute: false, illegalAsVNFunc: false, GT_SUB_HI.Kind), // VNF_SUB_HI
        ValueNumStore.GetOpAttribsForGenTree(GT_LSH_HI, commute: false, illegalAsVNFunc: false, GT_LSH_HI.Kind), // VNF_LSH_HI
        ValueNumStore.GetOpAttribsForGenTree(GT_RSH_LO, commute: false, illegalAsVNFunc: false, GT_RSH_LO.Kind), // VNF_RSH_LO
#endif
#if FEATURE_HW_INTRINSICS
        ValueNumStore.GetOpAttribsForGenTree(GT_HWINTRINSIC, commute: false, illegalAsVNFunc: false, GT_HWINTRINSIC.Kind), // VNF_HWINTRINSIC
#endif
        ValueNumStore.GetOpAttribsForGenTree(GT_INC_SATURATE, commute: false, illegalAsVNFunc: false, GT_INC_SATURATE.Kind), // VNF_INC_SATURATE
        ValueNumStore.GetOpAttribsForGenTree(GT_MULHI, commute: true, illegalAsVNFunc: false, GT_MULHI.Kind), // VNF_MULHI
#if !TARGET_64BIT
        ValueNumStore.GetOpAttribsForGenTree(GT_MUL_LONG, commute: true, illegalAsVNFunc: false, GT_MUL_LONG.Kind), // VNF_MUL_LONG
#elif TARGET_ARM64
        ValueNumStore.GetOpAttribsForGenTree(GT_MUL_LONG, commute: true, illegalAsVNFunc: false, GT_MUL_LONG.Kind), // VNF_MUL_LONG
#endif
        ValueNumStore.GetOpAttribsForGenTree(GT_AND_NOT, commute: false, illegalAsVNFunc: false, GT_AND_NOT.Kind), // VNF_AND_NOT
        ValueNumStore.GetOpAttribsForGenTree(GT_OR_NOT, commute: false, illegalAsVNFunc: false, GT_OR_NOT.Kind), // VNF_OR_NOT
        ValueNumStore.GetOpAttribsForGenTree(GT_XOR_NOT, commute: false, illegalAsVNFunc: false, GT_XOR_NOT.Kind), // VNF_XOR_NOT
#if TARGET_ARM64
        ValueNumStore.GetOpAttribsForGenTree(GT_BFIZ, commute: false, illegalAsVNFunc: false, GT_BFIZ.Kind), // VNF_BFIZ
#endif
        ValueNumStore.GetOpAttribsForGenTree(GT_CMP, commute: false, illegalAsVNFunc: false, GT_CMP.Kind), // VNF_CMP
        ValueNumStore.GetOpAttribsForGenTree(GT_TEST, commute: false, illegalAsVNFunc: false, GT_TEST.Kind), // VNF_TEST
#if TARGET_XARCH
        ValueNumStore.GetOpAttribsForGenTree(GT_BT, commute: false, illegalAsVNFunc: false, GT_BT.Kind), // VNF_BT
#endif
        ValueNumStore.GetOpAttribsForGenTree(GT_JCMP, commute: false, illegalAsVNFunc: false, GT_JCMP.Kind), // VNF_JCMP
        ValueNumStore.GetOpAttribsForGenTree(GT_JTEST, commute: false, illegalAsVNFunc: false, GT_JTEST.Kind), // VNF_JTEST
        ValueNumStore.GetOpAttribsForGenTree(GT_JCC, commute: false, illegalAsVNFunc: false, GT_JCC.Kind), // VNF_JCC
        ValueNumStore.GetOpAttribsForGenTree(GT_SETCC, commute: false, illegalAsVNFunc: false, GT_SETCC.Kind), // VNF_SETCC
        ValueNumStore.GetOpAttribsForGenTree(GT_SELECTCC, commute: false, illegalAsVNFunc: false, GT_SELECTCC.Kind), // VNF_SELECTCC
#if TARGET_ARM64 || TARGET_AMD64
        ValueNumStore.GetOpAttribsForGenTree(GT_CCMP, commute: false, illegalAsVNFunc: false, GT_CCMP.Kind), // VNF_CCMP
#endif
#if TARGET_ARM64
        ValueNumStore.GetOpAttribsForGenTree(GT_SELECT_INC, commute: false, illegalAsVNFunc: false, GT_SELECT_INC.Kind), // VNF_SELECT_INC
        ValueNumStore.GetOpAttribsForGenTree(GT_SELECT_INCCC, commute: false, illegalAsVNFunc: false, GT_SELECT_INCCC.Kind), // VNF_SELECT_INCCC
        ValueNumStore.GetOpAttribsForGenTree(GT_SELECT_INV, commute: false, illegalAsVNFunc: false, GT_SELECT_INV.Kind), // VNF_SELECT_INV
        ValueNumStore.GetOpAttribsForGenTree(GT_SELECT_INVCC, commute: false, illegalAsVNFunc: false, GT_SELECT_INVCC.Kind), // VNF_SELECT_INVCC
        ValueNumStore.GetOpAttribsForGenTree(GT_SELECT_NEG, commute: false, illegalAsVNFunc: false, GT_SELECT_NEG.Kind), // VNF_SELECT_NEG
        ValueNumStore.GetOpAttribsForGenTree(GT_SELECT_NEGCC, commute: false, illegalAsVNFunc: false, GT_SELECT_NEGCC.Kind), // VNF_SELECT_NEGCC
#endif
#if TARGET_RISCV64
        ValueNumStore.GetOpAttribsForGenTree(GT_SH1ADD, commute: false, illegalAsVNFunc: false, GT_SH1ADD.Kind), // VNF_SH1ADD
        ValueNumStore.GetOpAttribsForGenTree(GT_SH1ADD_UW, commute: false, illegalAsVNFunc: false, GT_SH1ADD_UW.Kind), // VNF_SH1ADD_UW
        ValueNumStore.GetOpAttribsForGenTree(GT_SH2ADD, commute: false, illegalAsVNFunc: false, GT_SH2ADD.Kind), // VNF_SH2ADD
        ValueNumStore.GetOpAttribsForGenTree(GT_SH2ADD_UW, commute: false, illegalAsVNFunc: false, GT_SH2ADD_UW.Kind), // VNF_SH2ADD_UW
        ValueNumStore.GetOpAttribsForGenTree(GT_SH3ADD, commute: false, illegalAsVNFunc: false, GT_SH3ADD.Kind), // VNF_SH3ADD
        ValueNumStore.GetOpAttribsForGenTree(GT_SH3ADD_UW, commute: false, illegalAsVNFunc: false, GT_SH3ADD_UW.Kind), // VNF_SH3ADD_UW
        ValueNumStore.GetOpAttribsForGenTree(GT_ADD_UW, commute: false, illegalAsVNFunc: false, GT_ADD_UW.Kind), // VNF_ADD_UW
        ValueNumStore.GetOpAttribsForGenTree(GT_SLLI_UW, commute: false, illegalAsVNFunc: false, GT_SLLI_UW.Kind), // VNF_SLLI_UW
        ValueNumStore.GetOpAttribsForGenTree(GT_BIT_SET, commute: false, illegalAsVNFunc: false, GT_BIT_SET.Kind), // VNF_BIT_SET
        ValueNumStore.GetOpAttribsForGenTree(GT_BIT_CLEAR, commute: false, illegalAsVNFunc: false, GT_BIT_CLEAR.Kind), // VNF_BIT_CLEAR
        ValueNumStore.GetOpAttribsForGenTree(GT_BIT_INVERT, commute: false, illegalAsVNFunc: false, GT_BIT_INVERT.Kind), // VNF_BIT_INVERT
#endif
        ValueNumStore.GetOpAttribsForGenTree(GT_JTRUE, commute: false, illegalAsVNFunc: true, GT_JTRUE.Kind), // VNF_JTRUE
        ValueNumStore.GetOpAttribsForGenTree(GT_ARR_ELEM, commute: false, illegalAsVNFunc: false, GT_ARR_ELEM.Kind), // VNF_ARR_ELEM
        ValueNumStore.GetOpAttribsForGenTree(GT_CALL, commute: false, illegalAsVNFunc: false, GT_CALL.Kind), // VNF_CALL
        ValueNumStore.GetOpAttribsForGenTree(GT_FIELD_LIST, commute: false, illegalAsVNFunc: false, GT_FIELD_LIST.Kind), // VNF_FIELD_LIST
        ValueNumStore.GetOpAttribsForGenTree(GT_RETURN, commute: false, illegalAsVNFunc: true, GT_RETURN.Kind), // VNF_RETURN
        ValueNumStore.GetOpAttribsForGenTree(GT_SWITCH, commute: false, illegalAsVNFunc: true, GT_SWITCH.Kind), // VNF_SWITCH
        ValueNumStore.GetOpAttribsForGenTree(GT_NO_OP, commute: false, illegalAsVNFunc: false, GT_NO_OP.Kind), // VNF_NO_OP
        ValueNumStore.GetOpAttribsForGenTree(GT_RETURN_SUSPEND, commute: false, illegalAsVNFunc: true, GT_RETURN_SUSPEND.Kind), // VNF_RETURN_SUSPEND
        ValueNumStore.GetOpAttribsForGenTree(GT_PATCHPOINT, commute: false, illegalAsVNFunc: true, GT_PATCHPOINT.Kind), // VNF_PATCHPOINT
        ValueNumStore.GetOpAttribsForGenTree(GT_PATCHPOINT_FORCED, commute: false, illegalAsVNFunc: true, GT_PATCHPOINT_FORCED.Kind), // VNF_PATCHPOINT_FORCED
        ValueNumStore.GetOpAttribsForGenTree(GT_START_NONGC, commute: false, illegalAsVNFunc: false, GT_START_NONGC.Kind), // VNF_START_NONGC
        ValueNumStore.GetOpAttribsForGenTree(GT_START_PREEMPTGC, commute: false, illegalAsVNFunc: false, GT_START_PREEMPTGC.Kind), // VNF_START_PREEMPTGC
        ValueNumStore.GetOpAttribsForGenTree(GT_PROF_HOOK, commute: false, illegalAsVNFunc: false, GT_PROF_HOOK.Kind), // VNF_PROF_HOOK
        ValueNumStore.GetOpAttribsForGenTree(GT_RETFILT, commute: false, illegalAsVNFunc: true, GT_RETFILT.Kind), // VNF_RETFILT
        ValueNumStore.GetOpAttribsForGenTree(GT_SWIFT_ERROR, commute: false, illegalAsVNFunc: false, GT_SWIFT_ERROR.Kind), // VNF_SWIFT_ERROR
        ValueNumStore.GetOpAttribsForGenTree(GT_SWIFT_ERROR_RET, commute: false, illegalAsVNFunc: true, GT_SWIFT_ERROR_RET.Kind), // VNF_SWIFT_ERROR_RET
        ValueNumStore.GetOpAttribsForGenTree(GT_WASM_JEXCEPT, commute: false, illegalAsVNFunc: false, GT_WASM_JEXCEPT.Kind), // VNF_WASM_JEXCEPT
        ValueNumStore.GetOpAttribsForGenTree(GT_WASM_THROW_REF, commute: false, illegalAsVNFunc: false, GT_WASM_THROW_REF.Kind), // VNF_WASM_THROW_REF
        ValueNumStore.GetOpAttribsForGenTree(GT_JMPTABLE, commute: false, illegalAsVNFunc: false, GT_JMPTABLE.Kind), // VNF_JMPTABLE
        ValueNumStore.GetOpAttribsForGenTree(GT_SWITCH_TABLE, commute: false, illegalAsVNFunc: false, GT_SWITCH_TABLE.Kind), // VNF_SWITCH_TABLE
        ValueNumStore.GetOpAttribsForGenTree(GT_PHYSREG, commute: false, illegalAsVNFunc: false, GT_PHYSREG.Kind), // VNF_PHYSREG
        ValueNumStore.GetOpAttribsForGenTree(GT_RETURNTRAP, commute: false, illegalAsVNFunc: false, GT_RETURNTRAP.Kind), // VNF_RETURNTRAP
        ValueNumStore.GetOpAttribsForGenTree(GT_PUTARG_REG, commute: false, illegalAsVNFunc: false, GT_PUTARG_REG.Kind), // VNF_PUTARG_REG
        ValueNumStore.GetOpAttribsForGenTree(GT_PUTARG_STK, commute: false, illegalAsVNFunc: false, GT_PUTARG_STK.Kind), // VNF_PUTARG_STK
        ValueNumStore.GetOpAttribsForGenTree(GT_SWAP, commute: false, illegalAsVNFunc: false, GT_SWAP.Kind), // VNF_SWAP
        ValueNumStore.GetOpAttribsForGenTree(GT_COPY, commute: false, illegalAsVNFunc: false, GT_COPY.Kind), // VNF_COPY
        ValueNumStore.GetOpAttribsForGenTree(GT_RELOAD, commute: false, illegalAsVNFunc: false, GT_RELOAD.Kind), // VNF_RELOAD
        ValueNumStore.GetOpAttribsForGenTree(GT_IL_OFFSET, commute: false, illegalAsVNFunc: false, GT_IL_OFFSET.Kind), // VNF_IL_OFFSET
        ValueNumStore.GetOpAttribsForGenTree(GT_RECORD_ASYNC_RESUME, commute: false, illegalAsVNFunc: false, GT_RECORD_ASYNC_RESUME.Kind), // VNF_RECORD_ASYNC_RESUME
        0, // VNF_Boundary
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: false), // VNF_MemOpaque
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: false, knownNonNull: false), // VNF_MapSelect
        ValueNumStore.GetOpAttribsForFunc(arity: 4, commute: false, knownNonNull: false), // VNF_MapStore
        ValueNumStore.GetOpAttribsForFunc(arity: 3, commute: false, knownNonNull: false), // VNF_MapPhysicalStore
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: false, knownNonNull: false), // VNF_BitCast
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: false), // VNF_ZeroObj
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: false, knownNonNull: true), // VNF_PtrToLoc
        ValueNumStore.GetOpAttribsForFunc(arity: 4, commute: false, knownNonNull: false), // VNF_PtrToArrElem
        ValueNumStore.GetOpAttribsForFunc(arity: 3, commute: false, knownNonNull: true), // VNF_PtrToStatic
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: false, knownNonNull: false), // VNF_MDArrLength
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: false, knownNonNull: false), // VNF_MDArrLowerBound
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: false), // VNF_InitVal
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: false, knownNonNull: false), // VNF_Cast
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: false, knownNonNull: false), // VNF_CastOvf
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: false, knownNonNull: false), // VNF_CastClass
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: false, knownNonNull: false), // VNF_IsInstanceOf
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: false, knownNonNull: false), // VNF_ReadyToRunCastClass
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: false, knownNonNull: false), // VNF_ReadyToRunIsInstanceOf
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: false), // VNF_TypeHandleToRuntimeType
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: false), // VNF_TypeHandleToRuntimeTypeHandle
        ValueNumStore.GetOpAttribsForFunc(arity: 3, commute: false, knownNonNull: false), // VNF_LdElemA
        ValueNumStore.GetOpAttribsForFunc(arity: 3, commute: false, knownNonNull: false), // VNF_ByrefExposedLoad
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: false, knownNonNull: false), // VNF_GetRefanyVal
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: true), // VNF_GetClassFromMethodParam
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: true), // VNF_GetSyncFromClassHandle
        ValueNumStore.GetOpAttribsForFunc(arity: 0, commute: false, knownNonNull: true), // VNF_LoopCloneChoiceAddr
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: false, knownNonNull: false), // VNF_ValWithExc
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: false, knownNonNull: false), // VNF_ExcSetCons
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: false), // VNF_NullPtrExc
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: false, knownNonNull: false), // VNF_ArithmeticExc
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: false), // VNF_OverflowExc
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: false, knownNonNull: false), // VNF_ConvOverflowExc
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: false), // VNF_DivideByZeroExc
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: false, knownNonNull: false), // VNF_IndexOutOfRangeExc
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: false, knownNonNull: false), // VNF_InvalidCastExc
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: false, knownNonNull: false), // VNF_R2RInvalidCastExc
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: false), // VNF_NewArrOverflowExc
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: false), // VNF_NewStringOverflowExc
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: false), // VNF_DynamicClassInitExc
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: false), // VNF_ThreadClassInitExc
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: false), // VNF_R2RClassInitExc
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: false, knownNonNull: false), // VNF_ClassInitGenericExc
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: false), // VNF_HelperOpaqueExc
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: false), // VNF_Abs
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: false), // VNF_Acos
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: false), // VNF_Acosh
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: false), // VNF_Asin
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: false), // VNF_Asinh
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: false), // VNF_Atan
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: false), // VNF_Atanh
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: false, knownNonNull: false), // VNF_Atan2
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: false), // VNF_Cbrt
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: false), // VNF_Ceiling
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: false), // VNF_Cos
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: false), // VNF_Cosh
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: false), // VNF_Exp
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: false), // VNF_Floor
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: false), // VNF_ILogB
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: false), // VNF_Log
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: false), // VNF_Log2
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: false), // VNF_Log10
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: false, knownNonNull: false), // VNF_Max
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: false, knownNonNull: false), // VNF_MaxMagnitude
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: false, knownNonNull: false), // VNF_MaxMagnitudeNumber
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: false, knownNonNull: false), // VNF_MaxNumber
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: false, knownNonNull: false), // VNF_Min
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: false, knownNonNull: false), // VNF_MinMagnitude
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: false, knownNonNull: false), // VNF_MinMagnitudeNumber
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: false, knownNonNull: false), // VNF_MinNumber
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: false, knownNonNull: false), // VNF_Pow
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: false), // VNF_RoundDouble
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: false), // VNF_RoundInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: false), // VNF_RoundSingle
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: false), // VNF_Sin
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: false), // VNF_Sinh
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: false), // VNF_Sqrt
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: false), // VNF_Tan
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: false), // VNF_Tanh
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: false), // VNF_Truncate
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: false), // VNF_LeadingZeroCount
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: false), // VNF_TrailingZeroCount
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: false), // VNF_PopCount
        ValueNumStore.GetOpAttribsForFunc(arity: 0, commute: false, knownNonNull: false), // VNF_ManagedThreadId
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: true), // VNF_ObjGetType
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: true), // VNF_GetGcstaticBase
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: true), // VNF_GetNongcstaticBase
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: true), // VNF_GetdynamicGcstaticBase
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: true), // VNF_GetdynamicNongcstaticBase
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: true), // VNF_GetdynamicGcstaticBaseNoctor
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: true), // VNF_GetdynamicNongcstaticBaseNoctor
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: true), // VNF_ReadyToRunStaticBaseGC
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: true), // VNF_ReadyToRunStaticBaseNonGC
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: true), // VNF_ReadyToRunStaticBaseThread
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: true), // VNF_ReadyToRunStaticBaseThreadNoctor
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: true), // VNF_ReadyToRunStaticBaseThreadNonGC
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: false, knownNonNull: true), // VNF_ReadyToRunGenericStaticBase
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: true), // VNF_GetpinnedGcstaticBase
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: true), // VNF_GetpinnedNongcstaticBase
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: true), // VNF_GetpinnedGcstaticBaseNoctor
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: true), // VNF_GetpinnedNongcstaticBaseNoctor
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: true), // VNF_GetGcthreadstaticBase
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: true), // VNF_GetNongcthreadstaticBase
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: true), // VNF_GetGcthreadstaticBaseNoctor
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: true), // VNF_GetNongcthreadstaticBaseNoctor
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: true), // VNF_GetdynamicGcthreadstaticBase
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: true), // VNF_GetdynamicNongcthreadstaticBase
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: true), // VNF_GetdynamicGcthreadstaticBaseNoctor
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: true), // VNF_GetdynamicGcthreadstaticBaseNoctorOptimized
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: true), // VNF_GetdynamicNongcthreadstaticBaseNoctor
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: true), // VNF_GetdynamicNongcthreadstaticBaseNoctorOptimized
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: true), // VNF_GetdynamicNongcthreadstaticBaseNoctorOptimized2
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: true), // VNF_GetdynamicNongcthreadstaticBaseNoctorOptimized2NoJitOpt
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: false, knownNonNull: true), // VNF_RuntimeHandleMethod
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: false, knownNonNull: true), // VNF_RuntimeHandleClass
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: false, knownNonNull: true), // VNF_ReadyToRunGenericHandle
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: true), // VNF_GetStaticAddrTLS
        ValueNumStore.GetOpAttribsForFunc(arity: 3, commute: false, knownNonNull: true), // VNF_VirtualFuncPtr
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: false, knownNonNull: true), // VNF_GVMLookupForSlot
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: false, knownNonNull: true), // VNF_ReadyToRunVirtualFuncPtr
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: false, knownNonNull: true), // VNF_JitNew
        ValueNumStore.GetOpAttribsForFunc(arity: 3, commute: false, knownNonNull: true), // VNF_JitNewArr
        ValueNumStore.GetOpAttribsForFunc(arity: 3, commute: false, knownNonNull: true), // VNF_JitNewLclArr
        ValueNumStore.GetOpAttribsForFunc(arity: 4, commute: false, knownNonNull: true), // VNF_JitNewMdArr
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: false, knownNonNull: true), // VNF_JitReadyToRunNew
        ValueNumStore.GetOpAttribsForFunc(arity: 3, commute: false, knownNonNull: true), // VNF_JitReadyToRunNewArr
        ValueNumStore.GetOpAttribsForFunc(arity: 3, commute: false, knownNonNull: true), // VNF_JitReadyToRunNewLclArr
        ValueNumStore.GetOpAttribsForFunc(arity: 3, commute: false, knownNonNull: true), // VNF_StrFastAllocate
        ValueNumStore.GetOpAttribsForFunc(arity: 3, commute: false, knownNonNull: true), // VNF_Box
        ValueNumStore.GetOpAttribsForFunc(arity: 3, commute: false, knownNonNull: false), // VNF_BoxNullable
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: false), // VNF_InvariantLoad
        ValueNumStore.GetOpAttribsForFunc(arity: 1, commute: false, knownNonNull: true), // VNF_InvariantNonNullLoad
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: false, knownNonNull: false), // VNF_Unbox
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: false, knownNonNull: false), // VNF_Unbox_TypeTest
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: false, knownNonNull: false), // VNF_LT_UN
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: false, knownNonNull: false), // VNF_LE_UN
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: false, knownNonNull: false), // VNF_GE_UN
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: false, knownNonNull: false), // VNF_GT_UN
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: true, knownNonNull: false), // VNF_ADD_OVF
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: false, knownNonNull: false), // VNF_SUB_OVF
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: true, knownNonNull: false), // VNF_MUL_OVF
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: true, knownNonNull: false), // VNF_ADD_UN_OVF
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: false, knownNonNull: false), // VNF_SUB_UN_OVF
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: true, knownNonNull: false), // VNF_MUL_UN_OVF
#if FEATURE_SIMD
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: false, knownNonNull: false), // VNF_SimdType
#endif
#if TARGET_XARCH
#if FEATURE_HW_INTRINSICS
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_Abs
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_AddSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_AndNot
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_As
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_AsByte
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_AsDouble
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_AsInt16
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_AsInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_AsInt64
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_AsNInt
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_AsNUInt
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_AsSByte
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_AsSingle
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_AsUInt16
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_AsUInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_AsUInt64
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_AsVector
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_AsVector128
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_AsVector128Unsafe
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_AsVector2
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_AsVector3
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_AsVector4
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_Ceiling
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_ConditionalSelect
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_ConvertToDouble
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_ConvertToInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_ConvertToInt32Native
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_ConvertToInt64
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_ConvertToInt64Native
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_ConvertToSingle
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_ConvertToUInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_ConvertToUInt32Native
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_ConvertToUInt64
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_ConvertToUInt64Native
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_Create
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_CreateScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_CreateScalarUnsafe
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_CreateSequence
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_Dot
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_Equals
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_EqualsAny
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_ExtractMostSignificantBits
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_Floor
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_FusedMultiplyAdd
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_GetElement
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_GreaterThan
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_GreaterThanAll
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_GreaterThanAny
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_GreaterThanOrEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_GreaterThanOrEqualAll
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_GreaterThanOrEqualAny
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_IsEvenInteger
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_IsFinite
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_IsInfinity
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_IsInteger
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_IsNaN
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_IsNegative
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_IsNegativeInfinity
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_IsNormal
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_IsOddInteger
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_IsPositive
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_IsPositiveInfinity
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_IsSubnormal
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_IsZero
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_LessThan
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_LessThanAll
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_LessThanAny
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_LessThanOrEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_LessThanOrEqualAll
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_LessThanOrEqualAny
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_LoadAligned
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_LoadAlignedNonTemporal
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_LoadUnsafe
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_Max
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_MaxMagnitude
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_MaxMagnitudeNumber
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_MaxNative
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_MaxNumber
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_Min
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_MinMagnitude
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_MinMagnitudeNumber
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_MinNative
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_MinNumber
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_MultiplyAddEstimate
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_Narrow
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_NarrowWithSaturation
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_Round
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_ShiftLeft
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_Shuffle
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_ShuffleNative
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_ShuffleNativeFallback
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_Sqrt
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_StoreAligned
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_StoreAlignedNonTemporal
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_StoreUnsafe
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_SubtractSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_Sum
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_ToScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_ToVector256
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_ToVector256Unsafe
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_ToVector512
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_Truncate
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_WidenLower
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_WidenUpper
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_WithElement
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_get_AllBitsSet
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_get_E
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_get_Epsilon
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_get_Indices
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_get_NaN
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_get_NegativeInfinity
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_get_NegativeOne
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_get_NegativeZero
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_get_One
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_get_Pi
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_get_PositiveInfinity
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_get_Tau
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_get_Zero
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_op_Addition
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_op_BitwiseAnd
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_op_BitwiseOr
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_op_Division
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_Vector128_op_Equality
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_op_ExclusiveOr
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_Vector128_op_Inequality
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_op_LeftShift
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_op_Multiply
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_op_OnesComplement
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_op_RightShift
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_op_Subtraction
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_op_UnaryNegation
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_op_UnaryPlus
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_op_UnsignedRightShift
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_Abs
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_AddSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_AndNot
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_As
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_AsByte
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_AsDouble
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_AsInt16
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_AsInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_AsInt64
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_AsNInt
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_AsNUInt
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_AsSByte
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_AsSingle
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_AsUInt16
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_AsUInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_AsUInt64
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_AsVector
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_AsVector256
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_Ceiling
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_ConditionalSelect
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_ConvertToDouble
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_ConvertToInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_ConvertToInt32Native
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_ConvertToInt64
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_ConvertToInt64Native
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_ConvertToSingle
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_ConvertToUInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_ConvertToUInt32Native
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_ConvertToUInt64
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_ConvertToUInt64Native
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_Create
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_CreateScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_CreateScalarUnsafe
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_CreateSequence
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_Dot
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_Equals
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_EqualsAny
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_ExtractMostSignificantBits
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_Floor
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_FusedMultiplyAdd
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_GetElement
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_GetLower
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_GetUpper
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_GreaterThan
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_GreaterThanAll
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_GreaterThanAny
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_GreaterThanOrEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_GreaterThanOrEqualAll
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_GreaterThanOrEqualAny
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_IsEvenInteger
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_IsFinite
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_IsInfinity
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_IsInteger
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_IsNaN
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_IsNegative
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_IsNegativeInfinity
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_IsNormal
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_IsOddInteger
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_IsPositive
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_IsPositiveInfinity
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_IsSubnormal
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_IsZero
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_LessThan
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_LessThanAll
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_LessThanAny
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_LessThanOrEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_LessThanOrEqualAll
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_LessThanOrEqualAny
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_LoadAligned
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_LoadAlignedNonTemporal
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_LoadUnsafe
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_Max
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_MaxMagnitude
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_MaxMagnitudeNumber
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_MaxNative
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_MaxNumber
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_Min
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_MinMagnitude
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_MinMagnitudeNumber
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_MinNative
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_MinNumber
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_MultiplyAddEstimate
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_Narrow
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_NarrowWithSaturation
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_Round
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_ShiftLeft
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_Shuffle
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_ShuffleNative
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_ShuffleNativeFallback
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_Sqrt
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_StoreAligned
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_StoreAlignedNonTemporal
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_StoreUnsafe
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_SubtractSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_Sum
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_ToScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_ToVector512
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_ToVector512Unsafe
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_Truncate
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_WidenLower
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_WidenUpper
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_WithElement
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_WithLower
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_WithUpper
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_get_AllBitsSet
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_get_E
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_get_Epsilon
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_get_Indices
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_get_NaN
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_get_NegativeInfinity
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_get_NegativeOne
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_get_NegativeZero
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_get_One
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_get_Pi
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_get_PositiveInfinity
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_get_Tau
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_get_Zero
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_op_Addition
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_op_BitwiseAnd
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_op_BitwiseOr
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_op_Division
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_Vector256_op_Equality
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_op_ExclusiveOr
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_Vector256_op_Inequality
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_op_LeftShift
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_op_Multiply
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_op_OnesComplement
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_op_RightShift
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_op_Subtraction
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_op_UnaryNegation
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_op_UnaryPlus
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector256_op_UnsignedRightShift
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_Abs
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_AddSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_AndNot
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_As
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_AsByte
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_AsDouble
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_AsInt16
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_AsInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_AsInt64
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_AsNInt
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_AsNUInt
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_AsSByte
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_AsSingle
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_AsUInt16
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_AsUInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_AsUInt64
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_AsVector
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_AsVector512
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_Ceiling
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_ConditionalSelect
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_ConvertToDouble
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_ConvertToInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_ConvertToInt32Native
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_ConvertToInt64
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_ConvertToInt64Native
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_ConvertToSingle
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_ConvertToUInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_ConvertToUInt32Native
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_ConvertToUInt64
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_ConvertToUInt64Native
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_Create
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_CreateScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_CreateScalarUnsafe
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_CreateSequence
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_Dot
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_Equals
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_EqualsAny
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_ExtractMostSignificantBits
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_Floor
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_FusedMultiplyAdd
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_GetElement
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_GetLower
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_GetLower128
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_GetUpper
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_GreaterThan
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_GreaterThanAll
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_GreaterThanAny
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_GreaterThanOrEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_GreaterThanOrEqualAll
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_GreaterThanOrEqualAny
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_IsEvenInteger
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_IsFinite
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_IsInfinity
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_IsInteger
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_IsNaN
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_IsNegative
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_IsNegativeInfinity
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_IsNormal
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_IsOddInteger
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_IsPositive
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_IsPositiveInfinity
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_IsSubnormal
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_IsZero
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_LessThan
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_LessThanAll
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_LessThanAny
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_LessThanOrEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_LessThanOrEqualAll
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_LessThanOrEqualAny
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_LoadAligned
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_LoadAlignedNonTemporal
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_LoadUnsafe
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_Max
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_MaxMagnitude
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_MaxMagnitudeNumber
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_MaxNative
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_MaxNumber
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_Min
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_MinMagnitude
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_MinMagnitudeNumber
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_MinNative
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_MinNumber
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_MultiplyAddEstimate
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_Narrow
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_NarrowWithSaturation
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_Round
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_ShiftLeft
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_Shuffle
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_ShuffleNative
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_ShuffleNativeFallback
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_Sqrt
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_StoreAligned
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_StoreAlignedNonTemporal
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_StoreUnsafe
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_SubtractSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_Sum
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_ToScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_Truncate
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_WidenLower
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_WidenUpper
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_WithElement
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_WithLower
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_WithUpper
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_get_AllBitsSet
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_get_E
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_get_Epsilon
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_get_Indices
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_get_NaN
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_get_NegativeInfinity
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_get_NegativeOne
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_get_NegativeZero
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_get_One
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_get_Pi
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_get_PositiveInfinity
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_get_Tau
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_get_Zero
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_op_Addition
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_op_BitwiseAnd
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_op_BitwiseOr
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_op_Division
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_Vector512_op_Equality
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_op_ExclusiveOr
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_Vector512_op_Inequality
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_op_LeftShift
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_op_Multiply
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_op_OnesComplement
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_op_RightShift
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_op_Subtraction
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_op_UnaryNegation
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_op_UnaryPlus
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector512_op_UnsignedRightShift
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_Abs
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_X86Base_Add
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_X86Base_AddSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_AddScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_AddSubtract
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_AlignRight
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_X86Base_And
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_AndNot
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_X86Base_Average
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_BitScanForward
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_BitScanReverse
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_Blend
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_BlendVariable
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_Ceiling
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_CeilingScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_X86Base_CompareEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_CompareGreaterThan
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_CompareGreaterThanOrEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_CompareLessThan
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_CompareLessThanOrEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_X86Base_CompareNotEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_CompareNotGreaterThan
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_CompareNotGreaterThanOrEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_CompareNotLessThan
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_CompareNotLessThanOrEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_CompareOrdered
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_CompareScalarEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_CompareScalarGreaterThan
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_CompareScalarGreaterThanOrEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_CompareScalarLessThan
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_CompareScalarLessThanOrEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_CompareScalarNotEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_CompareScalarNotGreaterThan
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_CompareScalarNotGreaterThanOrEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_CompareScalarNotLessThan
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_CompareScalarNotLessThanOrEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_CompareScalarOrdered
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_X86Base_CompareScalarOrderedEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_CompareScalarOrderedGreaterThan
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_CompareScalarOrderedGreaterThanOrEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_CompareScalarOrderedLessThan
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_CompareScalarOrderedLessThanOrEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_X86Base_CompareScalarOrderedNotEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_CompareScalarUnordered
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_X86Base_CompareScalarUnorderedEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_CompareScalarUnorderedGreaterThan
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_CompareScalarUnorderedGreaterThanOrEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_CompareScalarUnorderedLessThan
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_CompareScalarUnorderedLessThanOrEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_X86Base_CompareScalarUnorderedNotEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_CompareUnordered
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_ConvertScalarToVector128Double
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_ConvertScalarToVector128Int32
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_ConvertScalarToVector128Single
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_ConvertScalarToVector128UInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_ConvertToInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_ConvertToInt32WithTruncation
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_ConvertToUInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_ConvertToVector128Double
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_ConvertToVector128Int16
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_ConvertToVector128Int32
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_ConvertToVector128Int32WithTruncation
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_ConvertToVector128Int64
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_ConvertToVector128Single
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_Crc32
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_DivRem
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_Divide
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_DivideScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_DotProduct
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_Extract
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_Floor
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_FloorScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_HorizontalAdd
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_HorizontalAddSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_HorizontalSubtract
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_HorizontalSubtractSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_Insert
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_LoadAlignedVector128
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_LoadAlignedVector128NonTemporal
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_LoadAndDuplicateToVector128
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_LoadDquVector128
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_LoadFence
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_LoadHigh
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_LoadLow
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_LoadScalarVector128
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_LoadVector128
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_MaskMove
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_Max
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_MaxScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_MemoryFence
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_Min
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_MinHorizontal
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_MinScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_MoveAndDuplicate
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_MoveHighAndDuplicate
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_MoveHighToLow
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_MoveLowAndDuplicate
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_MoveLowToHigh
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_MoveMask
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_MoveScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_MultipleSumAbsoluteDifferences
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_X86Base_Multiply
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_MultiplyAddAdjacent
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_X86Base_MultiplyHigh
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_MultiplyHighRoundScale
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_X86Base_MultiplyLow
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_MultiplyScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_X86Base_Or
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_PackSignedSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_PackUnsignedSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_Pause
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_PopCount
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_Prefetch0
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_Prefetch1
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_Prefetch2
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_PrefetchNonTemporal
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_Reciprocal
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_ReciprocalScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_ReciprocalSqrt
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_ReciprocalSqrtScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_RoundCurrentDirection
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_RoundCurrentDirectionScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_RoundToNearestInteger
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_RoundToNearestIntegerScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_RoundToNegativeInfinity
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_RoundToNegativeInfinityScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_RoundToPositiveInfinity
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_RoundToPositiveInfinityScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_RoundToZero
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_RoundToZeroScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_ShiftLeftLogical
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_ShiftLeftLogical128BitLane
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_ShiftRightArithmetic
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_ShiftRightLogical
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_ShiftRightLogical128BitLane
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_Shuffle
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_ShuffleHigh
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_ShuffleLow
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_Sign
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_Sqrt
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_SqrtScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_Store
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_StoreAligned
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_StoreAlignedNonTemporal
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_StoreFence
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_StoreHigh
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_StoreLow
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_StoreNonTemporal
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_StoreScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_Subtract
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_SubtractSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_SubtractScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_SumAbsoluteDifferences
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_TestC
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_TestNotZAndNotC
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_TestZ
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_UnpackHigh
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_UnpackLow
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_X86Base_Xor
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_X86Base_X64_BigMul
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_X64_BitScanForward
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_X64_BitScanReverse
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_X64_ConvertScalarToVector128Double
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_X64_ConvertScalarToVector128Int64
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_X64_ConvertScalarToVector128Single
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_X64_ConvertScalarToVector128UInt64
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_X64_ConvertToInt64
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_X64_ConvertToInt64WithTruncation
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_X64_ConvertToUInt64
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_X64_Crc32
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_X64_DivRem
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_X64_Extract
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_X64_Insert
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_X64_PopCount
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_X64_StoreNonTemporal
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AVX_Add
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_AddSubtract
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AVX_And
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_AndNot
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_Blend
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_BlendVariable
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_BroadcastScalarToVector128
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_BroadcastScalarToVector256
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_BroadcastVector128ToVector256
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_Ceiling
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_Compare
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AVX_CompareEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_CompareGreaterThan
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_CompareGreaterThanOrEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_CompareLessThan
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_CompareLessThanOrEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AVX_CompareNotEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_CompareNotGreaterThan
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_CompareNotGreaterThanOrEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_CompareNotLessThan
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_CompareNotLessThanOrEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_CompareOrdered
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_CompareScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_CompareUnordered
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_ConvertToVector128Int32
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_ConvertToVector128Int32WithTruncation
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_ConvertToVector128Single
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_ConvertToVector256Double
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_ConvertToVector256Int32
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_ConvertToVector256Int32WithTruncation
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_ConvertToVector256Single
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_Divide
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_DotProduct
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_DuplicateEvenIndexed
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_DuplicateOddIndexed
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_ExtractVector128
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_Floor
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_HorizontalAdd
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_HorizontalSubtract
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_InsertVector128
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_LoadAlignedVector256
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_LoadDquVector256
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_LoadVector256
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_MaskLoad
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_MaskStore
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_Max
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_Min
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_MoveMask
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AVX_Multiply
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AVX_Or
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_Permute
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_Permute2x128
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_PermuteVar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_Reciprocal
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_ReciprocalSqrt
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_RoundCurrentDirection
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_RoundToNearestInteger
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_RoundToNegativeInfinity
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_RoundToPositiveInfinity
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_RoundToZero
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_Shuffle
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_Sqrt
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_Store
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_StoreAligned
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_StoreAlignedNonTemporal
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_Subtract
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_TestC
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_TestNotZAndNotC
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_TestZ
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_UnpackHigh
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_UnpackLow
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AVX_Xor
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_Abs
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AVX2_Add
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AVX2_AddSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_AlignRight
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AVX2_And
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_AndNot
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AVX2_Average
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_BitFieldExtract
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_Blend
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_BlendVariable
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_BroadcastScalarToVector128
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_BroadcastScalarToVector256
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_BroadcastVector128ToVector256
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AVX2_CompareEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_CompareGreaterThan
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_CompareLessThan
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_ConvertToInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_ConvertToUInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_ConvertToVector128Half
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_ConvertToVector128Single
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_ConvertToVector256Half
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_ConvertToVector256Int16
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_ConvertToVector256Int32
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_ConvertToVector256Int64
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_ConvertToVector256Single
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_ExtractLowestSetBit
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_ExtractVector128
        ValueNumStore.GetOpAttribsForFunc(arity: 5 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_GatherMaskVector128
        ValueNumStore.GetOpAttribsForFunc(arity: 5 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_GatherMaskVector256
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_GatherVector128
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_GatherVector256
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_GetMaskUpToLowestSetBit
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_HorizontalAdd
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_HorizontalAddSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_HorizontalSubtract
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_HorizontalSubtractSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_InsertVector128
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_LeadingZeroCount
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_LoadAlignedVector256NonTemporal
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_MaskLoad
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_MaskStore
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AVX2_Max
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AVX2_Min
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_MoveMask
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_MultipleSumAbsoluteDifferences
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AVX2_Multiply
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_MultiplyAdd
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_MultiplyAddAdjacent
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_MultiplyAddNegated
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_MultiplyAddNegatedScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_MultiplyAddScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_MultiplyAddSubtract
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AVX2_MultiplyHigh
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_MultiplyHighRoundScale
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AVX2_MultiplyLow
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_MultiplyNoFlags
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_MultiplySubtract
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_MultiplySubtractAdd
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_MultiplySubtractNegated
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_MultiplySubtractNegatedScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_MultiplySubtractScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AVX2_Or
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_PackSignedSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_PackUnsignedSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_ParallelBitDeposit
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_ParallelBitExtract
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_Permute2x128
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_Permute4x64
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_PermuteVar8x32
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_ResetLowestSetBit
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_ShiftLeftLogical
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_ShiftLeftLogical128BitLane
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_ShiftLeftLogicalVariable
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_ShiftRightArithmetic
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_ShiftRightArithmeticVariable
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_ShiftRightLogical
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_ShiftRightLogical128BitLane
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_ShiftRightLogicalVariable
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_Shuffle
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_ShuffleHigh
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_ShuffleLow
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_Sign
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_Subtract
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_SubtractSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_SumAbsoluteDifferences
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_TrailingZeroCount
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_UnpackHigh
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_UnpackLow
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AVX2_Xor
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_ZeroHighBits
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_X64_AndNot
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_X64_BitFieldExtract
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_X64_ExtractLowestSetBit
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_X64_GetMaskUpToLowestSetBit
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_X64_LeadingZeroCount
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_X64_MultiplyNoFlags
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_X64_ParallelBitDeposit
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_X64_ParallelBitExtract
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_X64_ResetLowestSetBit
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_X64_TrailingZeroCount
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_X64_ZeroHighBits
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_Abs
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_Add
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AVX512_AddSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_AddScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_AlignRight
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_AlignRight32
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_AlignRight64
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AVX512_And
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_AndNot
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AVX512_Average
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_BlendVariable
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_BroadcastPairScalarToVector128
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_BroadcastPairScalarToVector256
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_BroadcastPairScalarToVector512
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_BroadcastScalarToVector512
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_BroadcastVector128ToVector512
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_BroadcastVector256ToVector512
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_Classify
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ClassifyScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_Compare
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_CompareEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_CompareGreaterThan
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_CompareGreaterThanOrEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_CompareLessThan
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_CompareLessThanOrEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_CompareNotEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_CompareNotGreaterThan
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_CompareNotGreaterThanOrEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_CompareNotLessThan
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_CompareNotLessThanOrEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_CompareOrdered
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_CompareUnordered
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_Compress
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_CompressStore
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertScalarToVector128Double
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertScalarToVector128Single
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToInt32
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToUInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToUInt32WithTruncation
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector128Byte
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector128ByteWithSaturation
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector128Double
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector128Int16
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector128Int16WithSaturation
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector128Int32
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector128Int32WithSaturation
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector128Int64
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector128Int64WithTruncation
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector128SByte
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector128SByteWithSaturation
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector128Single
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector128UInt16
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector128UInt16WithSaturation
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector128UInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector128UInt32WithSaturation
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector128UInt32WithTruncation
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector128UInt64
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector128UInt64WithTruncation
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector256Byte
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector256ByteWithSaturation
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector256Double
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector256Int16
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector256Int16WithSaturation
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector256Int32
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector256Int32WithSaturation
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector256Int32WithTruncation
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector256Int64
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector256Int64WithTruncation
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector256SByte
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector256SByteWithSaturation
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector256Single
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector256UInt16
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector256UInt16WithSaturation
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector256UInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector256UInt32WithSaturation
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector256UInt32WithTruncation
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector256UInt64
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector256UInt64WithTruncation
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector512Double
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector512Int16
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector512Int32
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector512Int32WithTruncation
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector512Int64
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector512Int64WithTruncation
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector512Single
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector512UInt16
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector512UInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector512UInt32WithTruncation
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector512UInt64
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertToVector512UInt64WithTruncation
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_DetectConflicts
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_Divide
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_DivideScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_DuplicateEvenIndexed
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_DuplicateOddIndexed
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_Expand
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ExpandLoad
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ExtractVector128
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ExtractVector256
        ValueNumStore.GetOpAttribsForFunc(arity: 4 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_Fixup
        ValueNumStore.GetOpAttribsForFunc(arity: 4 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_FixupScalar
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_FusedMultiplyAdd
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_FusedMultiplyAddNegated
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_FusedMultiplyAddNegatedScalar
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_FusedMultiplyAddScalar
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_FusedMultiplyAddSubtract
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_FusedMultiplySubtract
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_FusedMultiplySubtractAdd
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_FusedMultiplySubtractNegated
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_FusedMultiplySubtractNegatedScalar
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_FusedMultiplySubtractScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_GetExponent
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_GetExponentScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_GetMantissa
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_GetMantissaScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_InsertVector128
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_InsertVector256
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_LeadingZeroCount
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_LoadAlignedVector512
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_LoadAlignedVector512NonTemporal
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_LoadVector512
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_MaskLoad
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_MaskLoadAligned
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_MaskStore
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_MaskStoreAligned
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_Max
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_Min
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_MoveMask
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_Multiply
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_MultiplyAddAdjacent
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AVX512_MultiplyHigh
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_MultiplyHighRoundScale
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AVX512_MultiplyLow
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_MultiplyScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AVX512_Or
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_PackSignedSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_PackUnsignedSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_Permute2x64
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_Permute4x32
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_Permute4x64
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_PermuteVar16x16
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_PermuteVar16x16x2
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_PermuteVar16x32
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_PermuteVar16x32x2
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_PermuteVar2x64
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_PermuteVar2x64x2
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_PermuteVar32x16
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_PermuteVar32x16x2
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_PermuteVar4x32
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_PermuteVar4x32x2
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_PermuteVar4x64
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_PermuteVar4x64x2
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_PermuteVar8x16
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_PermuteVar8x16x2
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_PermuteVar8x32x2
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_PermuteVar8x64
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_PermuteVar8x64x2
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_Range
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_RangeScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_Reciprocal14
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_Reciprocal14Scalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ReciprocalSqrt14
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ReciprocalSqrt14Scalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_Reduce
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ReduceScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_RotateLeft
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_RotateLeftVariable
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_RotateRight
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_RotateRightVariable
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_RoundScale
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_RoundScaleScalar
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_Scale
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ScaleScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ShiftLeftLogical
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ShiftLeftLogical128BitLane
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ShiftLeftLogicalVariable
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ShiftRightArithmetic
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ShiftRightArithmeticVariable
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ShiftRightLogical
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ShiftRightLogical128BitLane
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ShiftRightLogicalVariable
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_Shuffle
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_Shuffle2x128
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_Shuffle4x128
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ShuffleHigh
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ShuffleLow
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_Sqrt
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_SqrtScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_Store
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_StoreAligned
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_StoreAlignedNonTemporal
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_Subtract
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_SubtractSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_SubtractScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_SumAbsoluteDifferences
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_SumAbsoluteDifferencesInBlock32
        ValueNumStore.GetOpAttribsForFunc(arity: 4 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_TernaryLogic
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_UnpackHigh
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_UnpackLow
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AVX512_Xor
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_X64_ConvertScalarToVector128Double
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_X64_ConvertScalarToVector128Single
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_X64_ConvertToInt64
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_X64_ConvertToUInt64
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_X64_ConvertToUInt64WithTruncation
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512v2_MultiShift
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512v2_PermuteVar16x8
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512v2_PermuteVar16x8x2
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512v2_PermuteVar32x8
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512v2_PermuteVar32x8x2
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512v2_PermuteVar64x8
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512v2_PermuteVar64x8x2
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512v3_Compress
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512v3_CompressStore
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512v3_Expand
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512v3_ExpandLoad
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX10v2_ConvertToByteWithSaturationAndZeroExtendToInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX10v2_ConvertToByteWithTruncatedSaturationAndZeroExtendToInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX10v2_ConvertToInt32WithTruncatedSaturation
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX10v2_ConvertToSByteWithSaturationAndZeroExtendToInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX10v2_ConvertToSByteWithTruncatedSaturationAndZeroExtendToInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX10v2_ConvertToUInt32WithTruncatedSaturation
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX10v2_ConvertToVectorInt32WithTruncatedSaturation
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX10v2_ConvertToVectorInt64WithTruncatedSaturation
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX10v2_ConvertToVectorUInt32WithTruncatedSaturation
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX10v2_ConvertToVectorUInt64WithTruncatedSaturation
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX10v2_MinMax
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX10v2_MinMaxScalar
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX10v2_MoveScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX10v2_MultipleSumAbsoluteDifferences
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX10v2_StoreScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX10v2_X64_ConvertToInt64WithTruncatedSaturation
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX10v2_X64_ConvertToUInt64WithTruncatedSaturation
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX512BMM_BitMultiplyMatrix16x16WithOrReduction
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX512BMM_BitMultiplyMatrix16x16WithXorReduction
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX512BMM_ReverseBits
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVXVNNI_MultiplyWideningAndAdd
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVXVNNI_MultiplyWideningAndAddSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVXVNNIINT_MultiplyWideningAndAdd
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVXVNNIINT_MultiplyWideningAndAddSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVXVNNIINT_V512_MultiplyWideningAndAdd
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVXVNNIINT_V512_MultiplyWideningAndAddSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AES_CarrylessMultiply
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AES_Decrypt
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AES_DecryptLast
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AES_Encrypt
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AES_EncryptLast
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AES_InverseMixColumns
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AES_KeygenAssist
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AES_V256_CarrylessMultiply
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AES_V512_CarrylessMultiply
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Serialize_Serialize
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_GFNI_GaloisFieldAffineTransform
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_GFNI_GaloisFieldAffineTransformInverse
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_GFNI_GaloisFieldMultiply
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_GFNI_V256_GaloisFieldAffineTransform
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_GFNI_V256_GaloisFieldAffineTransformInverse
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_GFNI_V256_GaloisFieldMultiply
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_GFNI_V512_GaloisFieldAffineTransform
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_GFNI_V512_GaloisFieldAffineTransformInverse
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_GFNI_V512_GaloisFieldMultiply
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_COMIS
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_PTEST
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_X86Base_UCOMIS
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX_PTEST
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_AndNotVector
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX2_AndNotScalar
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_KORTEST
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_KTEST
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AVX512_PTESTM
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AVX512_PTESTNM
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AVX512_AddMask
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AVX512_AndMask
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_AndNotMask
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_BlendVariableMask
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ClassifyMask
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ClassifyScalarMask
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_CompareMask
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AVX512_CompareEqualMask
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_CompareGreaterThanMask
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_CompareGreaterThanOrEqualMask
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_CompareLessThanMask
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_CompareLessThanOrEqualMask
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AVX512_CompareNotEqualMask
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_CompareNotGreaterThanMask
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_CompareNotGreaterThanOrEqualMask
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_CompareNotLessThanMask
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_CompareNotLessThanOrEqualMask
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_CompareOrderedMask
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_CompareScalarMask
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_CompareUnorderedMask
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_CompressMask
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_CompressStoreMask
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertMaskToVector
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ConvertVectorToMask
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ExpandLoadMask
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ExpandMask
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_MaskLoadMask
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_MaskLoadAlignedMask
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_MaskStoreMask
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_MaskStoreAlignedMask
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_NotMask
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AVX512_OrMask
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ShiftLeftMask
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AVX512_ShiftRightMask
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AVX512_XorMask
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AVX512_XnorMask
#endif
#elif TARGET_ARM64
#if FEATURE_HW_INTRINSICS
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_Abs
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_AddSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_AndNot
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_As
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_AsByte
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_AsDouble
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_AsInt16
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_AsInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_AsInt64
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_AsNInt
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_AsNUInt
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_AsSByte
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_AsSingle
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_AsUInt16
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_AsUInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_AsUInt64
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_Ceiling
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_ConditionalSelect
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_ConvertToDouble
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_ConvertToInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_ConvertToInt32Native
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_ConvertToInt64
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_ConvertToInt64Native
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_ConvertToSingle
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_ConvertToUInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_ConvertToUInt32Native
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_ConvertToUInt64
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_ConvertToUInt64Native
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_Create
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_CreateScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_CreateScalarUnsafe
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_CreateSequence
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_Dot
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_Equals
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_EqualsAny
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_ExtractMostSignificantBits
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_Floor
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_FusedMultiplyAdd
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_GetElement
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_GreaterThan
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_GreaterThanAll
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_GreaterThanAny
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_GreaterThanOrEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_GreaterThanOrEqualAll
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_GreaterThanOrEqualAny
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_IsEvenInteger
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_IsFinite
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_IsInfinity
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_IsInteger
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_IsNaN
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_IsNegative
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_IsNegativeInfinity
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_IsNormal
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_IsOddInteger
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_IsPositive
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_IsPositiveInfinity
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_IsSubnormal
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_IsZero
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_LessThan
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_LessThanAll
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_LessThanAny
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_LessThanOrEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_LessThanOrEqualAll
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_LessThanOrEqualAny
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_LoadAligned
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_LoadAlignedNonTemporal
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_LoadUnsafe
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_Max
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_MaxMagnitude
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_MaxMagnitudeNumber
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_MaxNative
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_MaxNumber
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_Min
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_MinMagnitude
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_MinMagnitudeNumber
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_MinNative
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_MinNumber
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_MultiplyAddEstimate
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_Narrow
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_NarrowWithSaturation
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_Round
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_ShiftLeft
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_Shuffle
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_ShuffleNative
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_ShuffleNativeFallback
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_Sqrt
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_StoreAligned
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_StoreAlignedNonTemporal
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_StoreUnsafe
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_SubtractSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_Sum
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_ToScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_ToVector128
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_ToVector128Unsafe
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_Truncate
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_WidenLower
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_WidenUpper
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_WithElement
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_get_AllBitsSet
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_get_E
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_get_Epsilon
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_get_Indices
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_get_NaN
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_get_NegativeInfinity
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_get_NegativeOne
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_get_NegativeZero
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_get_One
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_get_Pi
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_get_PositiveInfinity
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_get_Tau
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_get_Zero
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_op_Addition
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_Vector64_op_BitwiseAnd
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_Vector64_op_BitwiseOr
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_op_Division
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_Vector64_op_Equality
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_op_ExclusiveOr
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_Vector64_op_Inequality
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_op_LeftShift
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_op_Multiply
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_op_OnesComplement
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_op_RightShift
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_op_Subtraction
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_op_UnaryNegation
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_op_UnaryPlus
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector64_op_UnsignedRightShift
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_Abs
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_AddSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_AndNot
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_As
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_AsByte
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_AsDouble
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_AsInt16
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_AsInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_AsInt64
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_AsNInt
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_AsNUInt
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_AsSByte
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_AsSingle
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_AsUInt16
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_AsUInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_AsUInt64
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_AsVector
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_AsVector128
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_AsVector128Unsafe
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_AsVector2
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_AsVector3
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_AsVector4
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_Ceiling
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_ConditionalSelect
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_ConvertToDouble
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_ConvertToInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_ConvertToInt32Native
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_ConvertToInt64
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_ConvertToInt64Native
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_ConvertToSingle
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_ConvertToUInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_ConvertToUInt32Native
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_ConvertToUInt64
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_ConvertToUInt64Native
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_Create
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_CreateScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_CreateScalarUnsafe
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_CreateSequence
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_Dot
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_Equals
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_EqualsAny
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_ExtractMostSignificantBits
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_Floor
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_FusedMultiplyAdd
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_GetElement
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_GetLower
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_GetUpper
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_GreaterThan
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_GreaterThanAll
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_GreaterThanAny
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_GreaterThanOrEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_GreaterThanOrEqualAll
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_GreaterThanOrEqualAny
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_IsEvenInteger
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_IsFinite
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_IsInfinity
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_IsInteger
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_IsNaN
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_IsNegative
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_IsNegativeInfinity
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_IsNormal
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_IsOddInteger
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_IsPositive
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_IsPositiveInfinity
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_IsSubnormal
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_IsZero
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_LessThan
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_LessThanAll
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_LessThanAny
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_LessThanOrEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_LessThanOrEqualAll
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_LessThanOrEqualAny
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_LoadAligned
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_LoadAlignedNonTemporal
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_LoadUnsafe
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_Max
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_MaxMagnitude
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_MaxMagnitudeNumber
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_MaxNative
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_MaxNumber
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_Min
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_MinMagnitude
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_MinMagnitudeNumber
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_MinNative
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_MinNumber
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_MultiplyAddEstimate
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_Narrow
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_NarrowWithSaturation
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_Round
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_ShiftLeft
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_Shuffle
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_ShuffleNative
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_ShuffleNativeFallback
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_Sqrt
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_StoreAligned
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_StoreAlignedNonTemporal
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_StoreUnsafe
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_SubtractSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_Sum
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_ToScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_Truncate
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_WidenLower
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_WidenUpper
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_WithElement
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_WithLower
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_WithUpper
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_get_AllBitsSet
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_get_E
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_get_Epsilon
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_get_Indices
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_get_NaN
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_get_NegativeInfinity
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_get_NegativeOne
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_get_NegativeZero
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_get_One
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_get_Pi
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_get_PositiveInfinity
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_get_Tau
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_get_Zero
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_op_Addition
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_Vector128_op_BitwiseAnd
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_Vector128_op_BitwiseOr
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_op_Division
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_Vector128_op_Equality
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_op_ExclusiveOr
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_Vector128_op_Inequality
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_op_LeftShift
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_op_Multiply
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_op_OnesComplement
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_op_RightShift
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_op_Subtraction
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_op_UnaryNegation
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_op_UnaryPlus
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Vector128_op_UnsignedRightShift
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Abs
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_AbsSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_AbsScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_AbsoluteCompareGreaterThan
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_AbsoluteCompareGreaterThanOrEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_AbsoluteCompareLessThan
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_AbsoluteCompareLessThanOrEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_AbsoluteDifference
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_AbsoluteDifferenceAdd
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_AbsoluteDifferenceWideningLower
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_AbsoluteDifferenceWideningLowerAndAdd
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_AbsoluteDifferenceWideningUpper
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_AbsoluteDifferenceWideningUpperAndAdd
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_Add
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_AddHighNarrowingLower
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_AddHighNarrowingUpper
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_AddPairwise
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_AddPairwiseWidening
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_AddPairwiseWideningAndAdd
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_AddPairwiseWideningAndAddScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_AddPairwiseWideningScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_AddRoundedHighNarrowingLower
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_AddRoundedHighNarrowingUpper
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_AddSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_AddSaturateScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_AddScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_AddWideningLower
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_AddWideningUpper
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_And
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_BitwiseClear
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_BitwiseSelect
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Ceiling
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_CeilingScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_CompareEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_CompareGreaterThan
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_CompareGreaterThanOrEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_CompareLessThan
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_CompareLessThanOrEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_CompareTest
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ConvertToInt32RoundAwayFromZero
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ConvertToInt32RoundAwayFromZeroScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ConvertToInt32RoundToEven
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ConvertToInt32RoundToEvenScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ConvertToInt32RoundToNegativeInfinity
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ConvertToInt32RoundToNegativeInfinityScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ConvertToInt32RoundToPositiveInfinity
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ConvertToInt32RoundToPositiveInfinityScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ConvertToInt32RoundToZero
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ConvertToInt32RoundToZeroScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ConvertToSingle
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ConvertToSingleScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ConvertToUInt32RoundAwayFromZero
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ConvertToUInt32RoundAwayFromZeroScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ConvertToUInt32RoundToEven
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ConvertToUInt32RoundToEvenScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ConvertToUInt32RoundToNegativeInfinity
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ConvertToUInt32RoundToNegativeInfinityScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ConvertToUInt32RoundToPositiveInfinity
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ConvertToUInt32RoundToPositiveInfinityScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ConvertToUInt32RoundToZero
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ConvertToUInt32RoundToZeroScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_DivideScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_DuplicateSelectedScalarToVector128
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_DuplicateSelectedScalarToVector64
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_DuplicateToVector128
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_DuplicateToVector64
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Extract
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ExtractNarrowingLower
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ExtractNarrowingSaturateLower
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ExtractNarrowingSaturateUnsignedLower
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ExtractNarrowingSaturateUnsignedUpper
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ExtractNarrowingSaturateUpper
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ExtractNarrowingUpper
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ExtractVector128
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ExtractVector64
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Floor
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_FloorScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_FusedAddHalving
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_FusedAddRoundedHalving
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_FusedMultiplyAdd
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_FusedMultiplyAddNegatedScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_FusedMultiplyAddScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_FusedMultiplySubtract
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_FusedMultiplySubtractNegatedScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_FusedMultiplySubtractScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_FusedSubtractHalving
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Insert
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_InsertScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_LeadingSignCount
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_LeadingZeroCount
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Load2xVector64
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Load2xVector64AndUnzip
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Load3xVector64
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Load3xVector64AndUnzip
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Load4xVector64
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Load4xVector64AndUnzip
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_LoadAndInsertScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_LoadAndInsertScalarVector64x2
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_LoadAndInsertScalarVector64x3
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_LoadAndInsertScalarVector64x4
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_LoadAndReplicateToVector128
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_LoadAndReplicateToVector64
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_LoadAndReplicateToVector64x2
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_LoadAndReplicateToVector64x3
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_LoadAndReplicateToVector64x4
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_LoadVector128
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_LoadVector64
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_Max
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_MaxNumber
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_MaxNumberScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_MaxPairwise
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_Min
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_MinNumber
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_MinNumberScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_MinPairwise
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_Multiply
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_MultiplyAdd
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_MultiplyAddByScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 4 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_MultiplyAddBySelectedScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_MultiplyByScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_MultiplyBySelectedScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_MultiplyBySelectedScalarWideningLower
        ValueNumStore.GetOpAttribsForFunc(arity: 4 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_MultiplyBySelectedScalarWideningLowerAndAdd
        ValueNumStore.GetOpAttribsForFunc(arity: 4 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_MultiplyBySelectedScalarWideningLowerAndSubtract
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_MultiplyBySelectedScalarWideningUpper
        ValueNumStore.GetOpAttribsForFunc(arity: 4 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_MultiplyBySelectedScalarWideningUpperAndAdd
        ValueNumStore.GetOpAttribsForFunc(arity: 4 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_MultiplyBySelectedScalarWideningUpperAndSubtract
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_MultiplyDoublingByScalarSaturateHigh
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_MultiplyDoublingBySelectedScalarSaturateHigh
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_MultiplyDoublingSaturateHigh
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_MultiplyDoublingWideningLowerAndAddSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_MultiplyDoublingWideningLowerAndSubtractSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_MultiplyDoublingWideningLowerByScalarAndAddSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_MultiplyDoublingWideningLowerByScalarAndSubtractSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 4 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_MultiplyDoublingWideningLowerBySelectedScalarAndAddSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 4 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_MultiplyDoublingWideningLowerBySelectedScalarAndSubtractSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_MultiplyDoublingWideningSaturateLower
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_MultiplyDoublingWideningSaturateLowerByScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_MultiplyDoublingWideningSaturateLowerBySelectedScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_MultiplyDoublingWideningSaturateUpper
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_MultiplyDoublingWideningSaturateUpperByScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_MultiplyDoublingWideningSaturateUpperBySelectedScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_MultiplyDoublingWideningUpperAndAddSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_MultiplyDoublingWideningUpperAndSubtractSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_MultiplyDoublingWideningUpperByScalarAndAddSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_MultiplyDoublingWideningUpperByScalarAndSubtractSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 4 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_MultiplyDoublingWideningUpperBySelectedScalarAndAddSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 4 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_MultiplyDoublingWideningUpperBySelectedScalarAndSubtractSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_MultiplyRoundedDoublingByScalarSaturateHigh
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_MultiplyRoundedDoublingBySelectedScalarSaturateHigh
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_MultiplyRoundedDoublingSaturateHigh
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_MultiplyScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_MultiplyScalarBySelectedScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_MultiplySubtract
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_MultiplySubtractByScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 4 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_MultiplySubtractBySelectedScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_MultiplyWideningLower
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_MultiplyWideningLowerAndAdd
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_MultiplyWideningLowerAndSubtract
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_MultiplyWideningUpper
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_MultiplyWideningUpperAndAdd
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_MultiplyWideningUpperAndSubtract
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Negate
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_NegateSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_NegateScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Not
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_Or
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_OrNot
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_PolynomialMultiply
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_PolynomialMultiplyWideningLower
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_PolynomialMultiplyWideningUpper
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_PopCount
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ReciprocalEstimate
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ReciprocalSquareRootEstimate
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_ReciprocalSquareRootStep
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_ReciprocalStep
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ReverseElement16
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ReverseElement32
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ReverseElement8
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_RoundAwayFromZero
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_RoundAwayFromZeroScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_RoundToNearest
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_RoundToNearestScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_RoundToNegativeInfinity
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_RoundToNegativeInfinityScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_RoundToPositiveInfinity
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_RoundToPositiveInfinityScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_RoundToZero
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_RoundToZeroScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftArithmetic
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftArithmeticRounded
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftArithmeticRoundedSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftArithmeticRoundedSaturateScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftArithmeticRoundedScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftArithmeticSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftArithmeticSaturateScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftArithmeticScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftLeftAndInsert
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftLeftAndInsertScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftLeftLogical
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftLeftLogicalSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftLeftLogicalSaturateScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftLeftLogicalSaturateUnsigned
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftLeftLogicalSaturateUnsignedScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftLeftLogicalScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftLeftLogicalWideningLower
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftLeftLogicalWideningUpper
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftLogical
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftLogicalRounded
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftLogicalRoundedSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftLogicalRoundedSaturateScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftLogicalRoundedScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftLogicalSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftLogicalSaturateScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftLogicalScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftRightAndInsert
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftRightAndInsertScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftRightArithmetic
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftRightArithmeticAdd
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftRightArithmeticAddScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftRightArithmeticNarrowingSaturateLower
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftRightArithmeticNarrowingSaturateUnsignedLower
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftRightArithmeticNarrowingSaturateUnsignedUpper
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftRightArithmeticNarrowingSaturateUpper
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftRightArithmeticRounded
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftRightArithmeticRoundedAdd
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftRightArithmeticRoundedAddScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftRightArithmeticRoundedNarrowingSaturateLower
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftRightArithmeticRoundedNarrowingSaturateUnsignedLower
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftRightArithmeticRoundedNarrowingSaturateUnsignedUpper
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftRightArithmeticRoundedNarrowingSaturateUpper
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftRightArithmeticRoundedScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftRightArithmeticScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftRightLogical
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftRightLogicalAdd
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftRightLogicalAddScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftRightLogicalNarrowingLower
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftRightLogicalNarrowingSaturateLower
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftRightLogicalNarrowingSaturateUpper
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftRightLogicalNarrowingUpper
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftRightLogicalRounded
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftRightLogicalRoundedAdd
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftRightLogicalRoundedAddScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftRightLogicalRoundedNarrowingLower
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftRightLogicalRoundedNarrowingSaturateLower
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftRightLogicalRoundedNarrowingSaturateUpper
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftRightLogicalRoundedNarrowingUpper
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftRightLogicalRoundedScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ShiftRightLogicalScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_SignExtendWideningLower
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_SignExtendWideningUpper
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_SqrtScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Store
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_StoreSelectedScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_StoreVectorAndZip
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Subtract
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_SubtractHighNarrowingLower
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_SubtractHighNarrowingUpper
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_SubtractRoundedHighNarrowingLower
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_SubtractRoundedHighNarrowingUpper
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_SubtractSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_SubtractSaturateScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_SubtractScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_SubtractWideningLower
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_SubtractWideningUpper
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_VectorTableLookup
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_VectorTableLookupExtension
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_Xor
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ZeroExtendWideningLower
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_ZeroExtendWideningUpper
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_Abs
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_AbsSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_AbsSaturateScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_AbsScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_AbsoluteCompareGreaterThan
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_AbsoluteCompareGreaterThanOrEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_AbsoluteCompareGreaterThanOrEqualScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_AbsoluteCompareGreaterThanScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_AbsoluteCompareLessThan
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_AbsoluteCompareLessThanOrEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_AbsoluteCompareLessThanOrEqualScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_AbsoluteCompareLessThanScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_AbsoluteDifference
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_AbsoluteDifferenceScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_Add
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_AddAcross
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_AddAcrossWidening
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_AddPairwise
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_AddPairwiseScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_AddSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_AddSaturateScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_Ceiling
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_CompareEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_CompareEqualScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_CompareGreaterThan
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_CompareGreaterThanOrEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_CompareGreaterThanOrEqualScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_CompareGreaterThanScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_CompareLessThan
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_CompareLessThanOrEqual
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_CompareLessThanOrEqualScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_CompareLessThanScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_CompareTest
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_CompareTestScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ConvertToDouble
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ConvertToDoubleScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ConvertToDoubleUpper
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ConvertToInt64RoundAwayFromZero
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ConvertToInt64RoundAwayFromZeroScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ConvertToInt64RoundToEven
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ConvertToInt64RoundToEvenScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ConvertToInt64RoundToNegativeInfinity
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ConvertToInt64RoundToNegativeInfinityScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ConvertToInt64RoundToPositiveInfinity
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ConvertToInt64RoundToPositiveInfinityScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ConvertToInt64RoundToZero
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ConvertToInt64RoundToZeroScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ConvertToSingleLower
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ConvertToSingleRoundToOddLower
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ConvertToSingleRoundToOddUpper
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ConvertToSingleUpper
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ConvertToUInt64RoundAwayFromZero
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ConvertToUInt64RoundAwayFromZeroScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ConvertToUInt64RoundToEven
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ConvertToUInt64RoundToEvenScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ConvertToUInt64RoundToNegativeInfinity
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ConvertToUInt64RoundToNegativeInfinityScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ConvertToUInt64RoundToPositiveInfinity
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ConvertToUInt64RoundToPositiveInfinityScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ConvertToUInt64RoundToZero
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ConvertToUInt64RoundToZeroScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_Divide
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_DuplicateSelectedScalarToVector128
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_DuplicateToVector128
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_DuplicateToVector64
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ExtractNarrowingSaturateScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ExtractNarrowingSaturateUnsignedScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_Floor
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_FusedMultiplyAdd
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_FusedMultiplyAddByScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 4 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_FusedMultiplyAddBySelectedScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 4 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_FusedMultiplyAddScalarBySelectedScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_FusedMultiplySubtract
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_FusedMultiplySubtractByScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 4 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_FusedMultiplySubtractBySelectedScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 4 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_FusedMultiplySubtractScalarBySelectedScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 4 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_InsertSelectedScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_Load2xVector128
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_Load2xVector128AndUnzip
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_Load3xVector128
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_Load3xVector128AndUnzip
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_Load4xVector128
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_Load4xVector128AndUnzip
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_LoadAndInsertScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_LoadAndInsertScalarVector128x2
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_LoadAndInsertScalarVector128x3
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_LoadAndInsertScalarVector128x4
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_LoadAndReplicateToVector128
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_LoadAndReplicateToVector128x2
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_LoadAndReplicateToVector128x3
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_LoadAndReplicateToVector128x4
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_LoadPairScalarVector64
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_LoadPairScalarVector64NonTemporal
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_LoadPairVector128
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_LoadPairVector128NonTemporal
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_LoadPairVector64
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_LoadPairVector64NonTemporal
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_Max
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_MaxAcross
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_MaxNumber
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_MaxNumberAcross
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_MaxNumberPairwise
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_MaxNumberPairwiseScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_MaxPairwise
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_MaxPairwiseScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_MaxScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_Min
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_MinAcross
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_MinNumber
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_MinNumberAcross
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_MinNumberPairwise
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_MinNumberPairwiseScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_MinPairwise
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_MinPairwiseScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_MinScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_Multiply
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_MultiplyByScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_MultiplyBySelectedScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_MultiplyDoublingSaturateHighScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_MultiplyDoublingScalarBySelectedScalarSaturateHigh
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_MultiplyDoublingWideningAndAddSaturateScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_MultiplyDoublingWideningAndSubtractSaturateScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_MultiplyDoublingWideningSaturateScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_MultiplyDoublingWideningSaturateScalarBySelectedScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 4 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_MultiplyDoublingWideningScalarBySelectedScalarAndAddSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 4 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_MultiplyDoublingWideningScalarBySelectedScalarAndSubtractSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_MultiplyExtended
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_MultiplyExtendedByScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_MultiplyExtendedBySelectedScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_MultiplyExtendedScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_MultiplyExtendedScalarBySelectedScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_MultiplyRoundedDoublingSaturateHighScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_MultiplyRoundedDoublingScalarBySelectedScalarSaturateHigh
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_MultiplyScalarBySelectedScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_Negate
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_NegateSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_NegateSaturateScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_NegateScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ReciprocalEstimate
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ReciprocalEstimateScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ReciprocalExponentScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ReciprocalSquareRootEstimate
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ReciprocalSquareRootEstimateScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ReciprocalSquareRootStep
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ReciprocalSquareRootStepScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ReciprocalStep
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ReciprocalStepScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ReverseElementBits
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_RoundAwayFromZero
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_RoundToNearest
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_RoundToNegativeInfinity
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_RoundToPositiveInfinity
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_RoundToZero
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ShiftArithmeticRoundedSaturateScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ShiftArithmeticSaturateScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ShiftLeftLogicalSaturateScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ShiftLeftLogicalSaturateUnsignedScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ShiftLogicalRoundedSaturateScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ShiftLogicalSaturateScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ShiftRightArithmeticNarrowingSaturateScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ShiftRightArithmeticNarrowingSaturateUnsignedScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ShiftRightArithmeticRoundedNarrowingSaturateScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ShiftRightArithmeticRoundedNarrowingSaturateUnsignedScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ShiftRightLogicalNarrowingSaturateScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ShiftRightLogicalRoundedNarrowingSaturateScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_Sqrt
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_Store
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_StorePair
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_StorePairNonTemporal
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_StorePairScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_StorePairScalarNonTemporal
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_StoreSelectedScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_StoreVectorAndZip
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_Subtract
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_SubtractSaturateScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_TransposeEven
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_TransposeOdd
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_UnzipEven
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_UnzipOdd
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_VectorTableLookup
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_VectorTableLookupExtension
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ZipHigh
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_AdvSimd_Arm64_ZipLow
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Aes_Decrypt
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Aes_Encrypt
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Aes_InverseMixColumns
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Aes_MixColumns
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_Aes_PolynomialMultiplyWideningLower
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: true, knownNonNull: false), // VNF_HWI_Aes_PolynomialMultiplyWideningUpper
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_ArmBase_LeadingZeroCount
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_ArmBase_ReverseElementBits
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_ArmBase_Yield
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_ArmBase_Arm64_LeadingSignCount
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_ArmBase_Arm64_LeadingZeroCount
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_ArmBase_Arm64_MultiplyHigh
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_ArmBase_Arm64_MultiplyLongAdd
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_ArmBase_Arm64_MultiplyLongNeg
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_ArmBase_Arm64_MultiplyLongSub
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_ArmBase_Arm64_ReverseElementBits
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Crc32_ComputeCrc32
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Crc32_ComputeCrc32C
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Crc32_Arm64_ComputeCrc32
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Crc32_Arm64_ComputeCrc32C
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Dp_DotProduct
        ValueNumStore.GetOpAttribsForFunc(arity: 4 + 1, commute: false, knownNonNull: false), // VNF_HWI_Dp_DotProductBySelectedQuadruplet
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Rdm_MultiplyRoundedDoublingAndAddSaturateHigh
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Rdm_MultiplyRoundedDoublingAndSubtractSaturateHigh
        ValueNumStore.GetOpAttribsForFunc(arity: 4 + 1, commute: false, knownNonNull: false), // VNF_HWI_Rdm_MultiplyRoundedDoublingBySelectedScalarAndAddSaturateHigh
        ValueNumStore.GetOpAttribsForFunc(arity: 4 + 1, commute: false, knownNonNull: false), // VNF_HWI_Rdm_MultiplyRoundedDoublingBySelectedScalarAndSubtractSaturateHigh
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Rdm_Arm64_MultiplyRoundedDoublingAndAddSaturateHighScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Rdm_Arm64_MultiplyRoundedDoublingAndSubtractSaturateHighScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 4 + 1, commute: false, knownNonNull: false), // VNF_HWI_Rdm_Arm64_MultiplyRoundedDoublingScalarBySelectedScalarAndAddSaturateHigh
        ValueNumStore.GetOpAttribsForFunc(arity: 4 + 1, commute: false, knownNonNull: false), // VNF_HWI_Rdm_Arm64_MultiplyRoundedDoublingScalarBySelectedScalarAndSubtractSaturateHigh
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sha1_FixedRotate
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sha1_HashUpdateChoose
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sha1_HashUpdateMajority
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sha1_HashUpdateParity
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sha1_ScheduleUpdate0
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sha1_ScheduleUpdate1
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sha256_HashUpdate1
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sha256_HashUpdate2
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sha256_ScheduleUpdate0
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sha256_ScheduleUpdate1
#endif
#if FEATURE_HW_INTRINSICS
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_Abs
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_AbsoluteCompareGreaterThan
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_AbsoluteCompareGreaterThanOrEqual
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_AbsoluteCompareLessThan
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_AbsoluteCompareLessThanOrEqual
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_AbsoluteDifference
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_Add
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_AddAcross
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_AddRotateComplex
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_AddSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_AddSequentialAcross
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_And
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_AndAcross
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_BitwiseClear
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_BooleanNot
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_Compact
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CompareEqual
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CompareGreaterThan
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CompareGreaterThanOrEqual
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CompareLessThan
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CompareLessThanOrEqual
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CompareNotEqualTo
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CompareUnordered
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_Compute16BitAddresses
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_Compute32BitAddresses
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_Compute64BitAddresses
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_Compute8BitAddresses
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_ConditionalExtractAfterLastActiveElement
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_ConditionalExtractAfterLastActiveElementAndReplicate
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_ConditionalExtractLastActiveElement
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_ConditionalExtractLastActiveElementAndReplicate
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_ConditionalSelect
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_ConvertToDouble
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_ConvertToInt32
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_ConvertToInt64
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_ConvertToSingle
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_ConvertToUInt32
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_ConvertToUInt64
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_Count16BitElements
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_Count32BitElements
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_Count64BitElements
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_Count8BitElements
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CreateBreakAfterMask
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CreateBreakAfterPropagateMask
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CreateBreakBeforeMask
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CreateBreakBeforePropagateMask
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CreateBreakPropagateMask
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CreateFalseMaskByte
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CreateFalseMaskDouble
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CreateFalseMaskInt16
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CreateFalseMaskInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CreateFalseMaskInt64
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CreateFalseMaskSByte
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CreateFalseMaskSingle
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CreateFalseMaskUInt16
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CreateFalseMaskUInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CreateFalseMaskUInt64
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CreateMaskForFirstActiveElement
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CreateMaskForNextActiveElement
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CreateTrueMaskByte
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CreateTrueMaskDouble
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CreateTrueMaskInt16
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CreateTrueMaskInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CreateTrueMaskInt64
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CreateTrueMaskSByte
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CreateTrueMaskSingle
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CreateTrueMaskUInt16
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CreateTrueMaskUInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CreateTrueMaskUInt64
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CreateWhileLessThanMaskByte
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CreateWhileLessThanMaskDouble
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CreateWhileLessThanMaskInt16
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CreateWhileLessThanMaskInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CreateWhileLessThanMaskInt64
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CreateWhileLessThanMaskSByte
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CreateWhileLessThanMaskSingle
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CreateWhileLessThanMaskUInt16
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CreateWhileLessThanMaskUInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CreateWhileLessThanMaskUInt64
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CreateWhileLessThanOrEqualMaskByte
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CreateWhileLessThanOrEqualMaskDouble
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CreateWhileLessThanOrEqualMaskInt16
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CreateWhileLessThanOrEqualMaskInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CreateWhileLessThanOrEqualMaskInt64
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CreateWhileLessThanOrEqualMaskSByte
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CreateWhileLessThanOrEqualMaskSingle
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CreateWhileLessThanOrEqualMaskUInt16
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CreateWhileLessThanOrEqualMaskUInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_CreateWhileLessThanOrEqualMaskUInt64
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_Divide
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_DotProduct
        ValueNumStore.GetOpAttribsForFunc(arity: 4 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_DotProductBySelectedScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_DuplicateSelectedScalarToVector
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_ExtractAfterLastActiveElement
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_ExtractAfterLastActiveElementScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_ExtractLastActiveElement
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_ExtractLastActiveElementScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_ExtractVector
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_FloatingPointExponentialAccelerator
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_FusedMultiplyAdd
        ValueNumStore.GetOpAttribsForFunc(arity: 4 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_FusedMultiplyAddBySelectedScalar
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_FusedMultiplyAddNegated
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_FusedMultiplySubtract
        ValueNumStore.GetOpAttribsForFunc(arity: 4 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_FusedMultiplySubtractBySelectedScalar
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_FusedMultiplySubtractNegated
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_GatherPrefetch16Bit
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_GatherPrefetch32Bit
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_GatherPrefetch64Bit
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_GatherPrefetch8Bit
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_GatherVector
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_GatherVectorByteZeroExtend
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_GatherVectorByteZeroExtendFirstFaulting
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_GatherVectorFirstFaulting
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_GatherVectorInt16SignExtend
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_GatherVectorInt16SignExtendFirstFaulting
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_GatherVectorInt16WithByteOffsetsSignExtend
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_GatherVectorInt16WithByteOffsetsSignExtendFirstFaulting
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_GatherVectorInt32SignExtend
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_GatherVectorInt32SignExtendFirstFaulting
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_GatherVectorInt32WithByteOffsetsSignExtend
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_GatherVectorInt32WithByteOffsetsSignExtendFirstFaulting
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_GatherVectorSByteSignExtend
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_GatherVectorSByteSignExtendFirstFaulting
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_GatherVectorUInt16WithByteOffsetsZeroExtend
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_GatherVectorUInt16WithByteOffsetsZeroExtendFirstFaulting
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_GatherVectorUInt16ZeroExtend
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_GatherVectorUInt16ZeroExtendFirstFaulting
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_GatherVectorUInt32WithByteOffsetsZeroExtend
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_GatherVectorUInt32WithByteOffsetsZeroExtendFirstFaulting
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_GatherVectorUInt32ZeroExtend
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_GatherVectorUInt32ZeroExtendFirstFaulting
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_GatherVectorWithByteOffsetFirstFaulting
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_GatherVectorWithByteOffsets
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_GetActiveElementCount
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_GetFfrByte
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_GetFfrDouble
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_GetFfrInt16
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_GetFfrInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_GetFfrInt64
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_GetFfrSByte
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_GetFfrSingle
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_GetFfrUInt16
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_GetFfrUInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_GetFfrUInt64
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_InsertIntoShiftedVector
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LeadingSignCount
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LeadingZeroCount
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_Load2xVectorAndUnzip
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_Load3xVectorAndUnzip
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_Load4xVectorAndUnzip
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVector
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVector128AndReplicateToVector
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorByteNonFaultingZeroExtendToInt16
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorByteNonFaultingZeroExtendToInt32
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorByteNonFaultingZeroExtendToInt64
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorByteNonFaultingZeroExtendToUInt16
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorByteNonFaultingZeroExtendToUInt32
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorByteNonFaultingZeroExtendToUInt64
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorByteZeroExtendFirstFaulting
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorByteZeroExtendToInt16
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorByteZeroExtendToInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorByteZeroExtendToInt64
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorByteZeroExtendToUInt16
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorByteZeroExtendToUInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorByteZeroExtendToUInt64
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorFirstFaulting
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorInt16NonFaultingSignExtendToInt32
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorInt16NonFaultingSignExtendToInt64
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorInt16NonFaultingSignExtendToUInt32
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorInt16NonFaultingSignExtendToUInt64
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorInt16SignExtendFirstFaulting
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorInt16SignExtendToInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorInt16SignExtendToInt64
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorInt16SignExtendToUInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorInt16SignExtendToUInt64
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorInt32NonFaultingSignExtendToInt64
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorInt32NonFaultingSignExtendToUInt64
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorInt32SignExtendFirstFaulting
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorInt32SignExtendToInt64
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorInt32SignExtendToUInt64
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorNonFaulting
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorNonTemporal
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorSByteNonFaultingSignExtendToInt16
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorSByteNonFaultingSignExtendToInt32
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorSByteNonFaultingSignExtendToInt64
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorSByteNonFaultingSignExtendToUInt16
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorSByteNonFaultingSignExtendToUInt32
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorSByteNonFaultingSignExtendToUInt64
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorSByteSignExtendFirstFaulting
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorSByteSignExtendToInt16
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorSByteSignExtendToInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorSByteSignExtendToInt64
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorSByteSignExtendToUInt16
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorSByteSignExtendToUInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorSByteSignExtendToUInt64
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorUInt16NonFaultingZeroExtendToInt32
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorUInt16NonFaultingZeroExtendToInt64
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorUInt16NonFaultingZeroExtendToUInt32
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorUInt16NonFaultingZeroExtendToUInt64
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorUInt16ZeroExtendFirstFaulting
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorUInt16ZeroExtendToInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorUInt16ZeroExtendToInt64
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorUInt16ZeroExtendToUInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorUInt16ZeroExtendToUInt64
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorUInt32NonFaultingZeroExtendToInt64
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorUInt32NonFaultingZeroExtendToUInt64
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorUInt32ZeroExtendFirstFaulting
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorUInt32ZeroExtendToInt64
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_LoadVectorUInt32ZeroExtendToUInt64
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_Max
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_MaxAcross
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_MaxNumber
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_MaxNumberAcross
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_Min
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_MinAcross
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_MinNumber
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_MinNumberAcross
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_Multiply
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_MultiplyAdd
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_MultiplyAddRotateComplex
        ValueNumStore.GetOpAttribsForFunc(arity: 5 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_MultiplyAddRotateComplexBySelectedScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_MultiplyBySelectedScalar
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_MultiplyExtended
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_MultiplySubtract
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_Negate
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_Not
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_Or
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_OrAcross
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_PopCount
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_Prefetch16Bit
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_Prefetch32Bit
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_Prefetch64Bit
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_Prefetch8Bit
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_ReciprocalEstimate
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_ReciprocalExponent
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_ReciprocalSqrtEstimate
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_ReciprocalSqrtStep
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_ReciprocalStep
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_ReverseBits
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_ReverseElement
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_ReverseElement16
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_ReverseElement32
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_ReverseElement8
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_RoundAwayFromZero
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_RoundToNearest
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_RoundToNegativeInfinity
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_RoundToPositiveInfinity
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_RoundToZero
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_SaturatingDecrementBy16BitElementCount
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_SaturatingDecrementBy32BitElementCount
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_SaturatingDecrementBy64BitElementCount
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_SaturatingDecrementBy8BitElementCount
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_SaturatingDecrementByActiveElementCount
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_SaturatingIncrementBy16BitElementCount
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_SaturatingIncrementBy32BitElementCount
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_SaturatingIncrementBy64BitElementCount
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_SaturatingIncrementBy8BitElementCount
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_SaturatingIncrementByActiveElementCount
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_Scale
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_Scatter
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_Scatter16BitNarrowing
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_Scatter16BitWithByteOffsetsNarrowing
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_Scatter32BitNarrowing
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_Scatter32BitWithByteOffsetsNarrowing
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_Scatter8BitNarrowing
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_Scatter8BitWithByteOffsetsNarrowing
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_ScatterWithByteOffsets
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_SetFfr
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_ShiftLeftLogical
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_ShiftRightArithmetic
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_ShiftRightArithmeticForDivide
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_ShiftRightLogical
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_SignExtend16
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_SignExtend32
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_SignExtend8
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_SignExtendWideningLower
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_SignExtendWideningUpper
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_Splice
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_Sqrt
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_StoreAndZip
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_StoreNarrowing
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_StoreNonTemporal
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_Subtract
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_SubtractSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_TestAnyTrue
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_TestFirstTrue
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_TestLastTrue
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_TransposeEven
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_TransposeOdd
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_TrigonometricMultiplyAddCoefficient
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_TrigonometricSelectCoefficient
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_TrigonometricStartingValue
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_UnzipEven
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_UnzipOdd
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_VectorTableLookup
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_Xor
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_XorAcross
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_ZeroExtend16
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_ZeroExtend32
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve_ZeroExtend8
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_ZeroExtendWideningLower
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_ZeroExtendWideningUpper
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_ZipHigh
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_ZipLow
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_AbsSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_AbsoluteDifferenceAdd
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_AbsoluteDifferenceWideningEven
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_AbsoluteDifferenceWideningLowerAndAddEven
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_AbsoluteDifferenceWideningLowerAndAddOdd
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_AbsoluteDifferenceWideningOdd
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_AddCarryWideningEven
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_AddCarryWideningOdd
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_AddHighNarrowingEven
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_AddHighNarrowingOdd
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_AddPairwise
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_AddPairwiseWideningAndAdd
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_AddRotateComplex
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_AddRoundedHighNarrowingEven
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_AddRoundedHighNarrowingOdd
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_AddSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_AddSaturateRotateComplex
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_AddWideningEven
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_AddWideningEvenOdd
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_AddWideningOdd
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_BitwiseClearXor
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_BitwiseSelect
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_BitwiseSelectLeftInverted
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_BitwiseSelectRightInverted
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_ConvertToDoubleOdd
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_ConvertToSingleEvenRoundToOdd
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_ConvertToSingleOdd
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_ConvertToSingleOddRoundToOdd
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_CountMatchingElements
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_CountMatchingElementsIn128BitSegments
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_CreateWhileGreaterThanMaskByte
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_CreateWhileGreaterThanMaskDouble
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_CreateWhileGreaterThanMaskInt16
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_CreateWhileGreaterThanMaskInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_CreateWhileGreaterThanMaskInt64
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_CreateWhileGreaterThanMaskSByte
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_CreateWhileGreaterThanMaskSingle
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_CreateWhileGreaterThanMaskUInt16
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_CreateWhileGreaterThanMaskUInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_CreateWhileGreaterThanMaskUInt64
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_CreateWhileGreaterThanOrEqualMaskByte
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_CreateWhileGreaterThanOrEqualMaskDouble
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_CreateWhileGreaterThanOrEqualMaskInt16
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_CreateWhileGreaterThanOrEqualMaskInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_CreateWhileGreaterThanOrEqualMaskInt64
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_CreateWhileGreaterThanOrEqualMaskSByte
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_CreateWhileGreaterThanOrEqualMaskSingle
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_CreateWhileGreaterThanOrEqualMaskUInt16
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_CreateWhileGreaterThanOrEqualMaskUInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_CreateWhileGreaterThanOrEqualMaskUInt64
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_CreateWhileReadAfterWriteMaskByte
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_CreateWhileReadAfterWriteMaskDouble
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_CreateWhileReadAfterWriteMaskInt16
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_CreateWhileReadAfterWriteMaskInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_CreateWhileReadAfterWriteMaskInt64
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_CreateWhileReadAfterWriteMaskSByte
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_CreateWhileReadAfterWriteMaskSingle
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_CreateWhileReadAfterWriteMaskUInt16
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_CreateWhileReadAfterWriteMaskUInt32
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_CreateWhileReadAfterWriteMaskUInt64
        ValueNumStore.GetOpAttribsForFunc(arity: 4 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_DotProductRotateComplex
        ValueNumStore.GetOpAttribsForFunc(arity: 5 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_DotProductRotateComplexBySelectedIndex
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_FusedAddHalving
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_FusedAddRoundedHalving
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_FusedSubtractHalving
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_GatherVectorByteZeroExtendNonTemporal
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_GatherVectorInt16SignExtendNonTemporal
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_GatherVectorInt16WithByteOffsetsSignExtendNonTemporal
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_GatherVectorInt32SignExtendNonTemporal
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_GatherVectorInt32WithByteOffsetsSignExtendNonTemporal
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_GatherVectorNonTemporal
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_GatherVectorSByteSignExtendNonTemporal
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_GatherVectorUInt16WithByteOffsetsZeroExtendNonTemporal
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_GatherVectorUInt16ZeroExtendNonTemporal
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_GatherVectorUInt32WithByteOffsetsZeroExtendNonTemporal
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_GatherVectorUInt32ZeroExtendNonTemporal
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_GatherVectorWithByteOffsetsNonTemporal
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_InterleavingXorEvenOdd
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_InterleavingXorOddEven
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_Log2
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_Match
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_MaxNumberPairwise
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_MaxPairwise
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_MinNumberPairwise
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_MinPairwise
        ValueNumStore.GetOpAttribsForFunc(arity: 4 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_MultiplyAddBySelectedScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 4 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_MultiplyAddRotateComplex
        ValueNumStore.GetOpAttribsForFunc(arity: 5 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_MultiplyAddRotateComplexBySelectedScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 4 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_MultiplyAddRoundedDoublingSaturateHighRotateComplex
        ValueNumStore.GetOpAttribsForFunc(arity: 5 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_MultiplyAddRoundedDoublingSaturateHighRotateComplexBySelectedScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_MultiplyBySelectedScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_MultiplyBySelectedScalarWideningEven
        ValueNumStore.GetOpAttribsForFunc(arity: 4 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_MultiplyBySelectedScalarWideningEvenAndAdd
        ValueNumStore.GetOpAttribsForFunc(arity: 4 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_MultiplyBySelectedScalarWideningEvenAndSubtract
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_MultiplyBySelectedScalarWideningOdd
        ValueNumStore.GetOpAttribsForFunc(arity: 4 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_MultiplyBySelectedScalarWideningOddAndAdd
        ValueNumStore.GetOpAttribsForFunc(arity: 4 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_MultiplyBySelectedScalarWideningOddAndSubtract
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_MultiplyDoublingBySelectedScalarSaturateHigh
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_MultiplyDoublingSaturateHigh
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_MultiplyDoublingWideningAndAddSaturateEven
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_MultiplyDoublingWideningAndAddSaturateEvenOdd
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_MultiplyDoublingWideningAndAddSaturateOdd
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_MultiplyDoublingWideningAndSubtractSaturateEven
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_MultiplyDoublingWideningAndSubtractSaturateEvenOdd
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_MultiplyDoublingWideningAndSubtractSaturateOdd
        ValueNumStore.GetOpAttribsForFunc(arity: 4 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_MultiplyDoublingWideningBySelectedScalarAndAddSaturateEven
        ValueNumStore.GetOpAttribsForFunc(arity: 4 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_MultiplyDoublingWideningBySelectedScalarAndAddSaturateOdd
        ValueNumStore.GetOpAttribsForFunc(arity: 4 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_MultiplyDoublingWideningBySelectedScalarAndSubtractSaturateEven
        ValueNumStore.GetOpAttribsForFunc(arity: 4 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_MultiplyDoublingWideningBySelectedScalarAndSubtractSaturateOdd
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_MultiplyDoublingWideningSaturateEven
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_MultiplyDoublingWideningSaturateEvenBySelectedScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_MultiplyDoublingWideningSaturateOdd
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_MultiplyDoublingWideningSaturateOddBySelectedScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_MultiplyRoundedDoublingBySelectedScalarSaturateHigh
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_MultiplyRoundedDoublingSaturateAndAddHigh
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_MultiplyRoundedDoublingSaturateAndSubtractHigh
        ValueNumStore.GetOpAttribsForFunc(arity: 4 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_MultiplyRoundedDoublingSaturateBySelectedScalarAndAddHigh
        ValueNumStore.GetOpAttribsForFunc(arity: 4 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_MultiplyRoundedDoublingSaturateBySelectedScalarAndSubtractHigh
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_MultiplyRoundedDoublingSaturateHigh
        ValueNumStore.GetOpAttribsForFunc(arity: 4 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_MultiplySubtractBySelectedScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_MultiplyWideningEven
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_MultiplyWideningEvenAndAdd
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_MultiplyWideningEvenAndSubtract
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_MultiplyWideningOdd
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_MultiplyWideningOddAndAdd
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_MultiplyWideningOddAndSubtract
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_NegateSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_NoMatch
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_PolynomialMultiply
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_PolynomialMultiplyWideningEven
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_PolynomialMultiplyWideningOdd
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_ReciprocalEstimate
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_ReciprocalSqrtEstimate
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_Scatter16BitNarrowingNonTemporal
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_Scatter16BitWithByteOffsetsNarrowingNonTemporal
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_Scatter32BitNarrowingNonTemporal
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_Scatter32BitWithByteOffsetsNarrowingNonTemporal
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_Scatter8BitNarrowingNonTemporal
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_Scatter8BitWithByteOffsetsNarrowingNonTemporal
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_ScatterNonTemporal
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_ScatterWithByteOffsetsNonTemporal
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_ShiftArithmeticRounded
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_ShiftArithmeticRoundedSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_ShiftArithmeticSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_ShiftLeftAndInsert
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_ShiftLeftLogicalSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_ShiftLeftLogicalSaturateUnsigned
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_ShiftLeftLogicalWideningEven
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_ShiftLeftLogicalWideningOdd
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_ShiftLogicalRounded
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_ShiftLogicalRoundedSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_ShiftRightAndInsert
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_ShiftRightArithmeticAdd
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_ShiftRightArithmeticNarrowingSaturateEven
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_ShiftRightArithmeticNarrowingSaturateOdd
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_ShiftRightArithmeticNarrowingSaturateUnsignedEven
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_ShiftRightArithmeticNarrowingSaturateUnsignedOdd
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_ShiftRightArithmeticRounded
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_ShiftRightArithmeticRoundedAdd
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_ShiftRightArithmeticRoundedNarrowingSaturateEven
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_ShiftRightArithmeticRoundedNarrowingSaturateOdd
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_ShiftRightArithmeticRoundedNarrowingSaturateUnsignedEven
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_ShiftRightArithmeticRoundedNarrowingSaturateUnsignedOdd
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_ShiftRightLogicalAdd
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_ShiftRightLogicalNarrowingEven
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_ShiftRightLogicalNarrowingOdd
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_ShiftRightLogicalNarrowingSaturateEven
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_ShiftRightLogicalNarrowingSaturateOdd
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_ShiftRightLogicalRounded
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_ShiftRightLogicalRoundedAdd
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_ShiftRightLogicalRoundedNarrowingEven
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_ShiftRightLogicalRoundedNarrowingOdd
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_ShiftRightLogicalRoundedNarrowingSaturateEven
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_ShiftRightLogicalRoundedNarrowingSaturateOdd
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_SubtractBorrowWideningEven
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_SubtractBorrowWideningOdd
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_SubtractHighNarrowingEven
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_SubtractHighNarrowingOdd
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_SubtractRoundedHighNarrowingEven
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_SubtractRoundedHighNarrowingOdd
        ValueNumStore.GetOpAttribsForFunc(arity: -1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_SubtractSaturate
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_SubtractWideningEven
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_SubtractWideningEvenOdd
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_SubtractWideningOdd
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_SubtractWideningOddEven
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_VectorTableLookup
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_VectorTableLookupExtension
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_Xor
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve2_XorRotateRight
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_ConditionalExtractAfterLastActiveElementScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_ConditionalExtractLastActiveElementScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_ConvertMaskToVector
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_ConvertVectorToMask
        ValueNumStore.GetOpAttribsForFunc(arity: 0 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_ConversionTrueMask
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_SaturatingDecrementBy16BitElementCountScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_SaturatingDecrementBy32BitElementCountScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_SaturatingDecrementBy64BitElementCountScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_SaturatingIncrementBy16BitElementCountScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_SaturatingIncrementBy32BitElementCountScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_SaturatingIncrementBy64BitElementCountScalar
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_StoreAndZipx2
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_StoreAndZipx3
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_StoreAndZipx4
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_And_Predicates
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_BitwiseClear_Predicates
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_Or_Predicates
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_Xor_Predicates
        ValueNumStore.GetOpAttribsForFunc(arity: 3 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_ConditionalSelect_Predicates
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_ZipHigh_Predicates
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_ZipLow_Predicates
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_UnzipEven_Predicates
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_UnzipOdd_Predicates
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_TransposeEven_Predicates
        ValueNumStore.GetOpAttribsForFunc(arity: 2 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_TransposeOdd_Predicates
        ValueNumStore.GetOpAttribsForFunc(arity: 1 + 1, commute: false, knownNonNull: false), // VNF_HWI_Sve_ReverseElement_Predicates
#endif
#elif TARGET_ARM
#elif TARGET_LOONGARCH64
#elif TARGET_RISCV64
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: true, knownNonNull: false), // VNF_MinInt
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: true, knownNonNull: false), // VNF_MaxInt
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: true, knownNonNull: false), // VNF_MinInt_UN
        ValueNumStore.GetOpAttribsForFunc(arity: 2, commute: true, knownNonNull: false), // VNF_MaxInt_UN
#elif TARGET_WASM
#else
#error Unsupported platform
#endif
    ];
}