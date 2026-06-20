// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial class GenTree
{
    private static ReadOnlySpan<HandleKindFlag> s_handleKindFlags => [
        HKF_INVARIANT, // GTF_ICON_SCOPE_HDL
        HKF_INVARIANT, // GTF_ICON_CLASS_HDL
        HKF_INVARIANT, // GTF_ICON_METHOD_HDL
        HKF_INVARIANT, // GTF_ICON_FIELD_HDL
        0, // GTF_ICON_STATIC_HDL
        HKF_INVARIANT | HKF_NONNULL, // GTF_ICON_STR_HDL
        0, // GTF_ICON_OBJ_HDL
        HKF_INVARIANT, // GTF_ICON_CONST_PTR
        0, // GTF_ICON_GLOBAL_PTR
        HKF_INVARIANT, // GTF_ICON_VARG_HDL
        0, // GTF_ICON_PINVKI_HDL
        HKF_INVARIANT, // GTF_ICON_TOKEN_HDL
        HKF_INVARIANT, // GTF_ICON_TLS_HDL
        0, // GTF_ICON_FTN_ADDR
        HKF_INVARIANT, // GTF_ICON_CIDMID_HDL
        0, // GTF_ICON_BBC_PTR
        0, // GTF_ICON_STATIC_BOX_PTR
        0, // GTF_ICON_FIELD_SEQ
        HKF_INVARIANT | HKF_NONNULL, // GTF_ICON_STATIC_ADDR_PTR
        HKF_INVARIANT, // GTF_ICON_SECREL_OFFSET
        HKF_INVARIANT, // GTF_ICON_TLSGD_OFFSET
    ];

    public GenTreeUnOp AsUnOp()
    {
        assert(_oper.IsSimple);
        assert(this is GenTreeUnOp);
        return Unsafe.As<GenTreeUnOp>(this);
    }
    public GenTreeOp AsOp()
    {
        assert(_oper.IsBinary);
        assert(this is GenTreeOp);
        return Unsafe.As<GenTreeOp>(this);
    }
    public GenTreeVal AsVal()
    {
        assert(_oper is GT_JMP or GT_RECORD_ASYNC_RESUME or GT_ASYNC_RESUME_INFO);
        assert(this is GenTreeVal);
        return Unsafe.As<GenTreeVal>(this);
    }
    public GenTreeIntConCommon AsIntConCommon()
    {
        assert(_oper is GT_CNS_INT or GT_CNS_LNG);
        assert(this is GenTreeIntConCommon);
        return Unsafe.As<GenTreeIntConCommon>(this);
    }
    public GenTreeIntCon AsIntCon()
    {
        assert(_oper is GT_CNS_INT);
        assert(this is GenTreeIntCon);
        return Unsafe.As<GenTreeIntCon>(this);
    }
    public GenTreeLngCon AsLngCon()
    {
        assert(_oper is GT_CNS_LNG);
        assert(this is GenTreeLngCon);
        return Unsafe.As<GenTreeLngCon>(this);
    }
    public GenTreeDblCon AsDblCon()
    {
        assert(_oper is GT_CNS_DBL);
        assert(this is GenTreeDblCon);
        return Unsafe.As<GenTreeDblCon>(this);
    }
    public GenTreeStrCon AsStrCon()
    {
        assert(_oper is GT_CNS_STR);
        assert(this is GenTreeStrCon);
        return Unsafe.As<GenTreeStrCon>(this);
    }
#if FEATURE_SIMD
    public GenTreeVecCon AsVecCon()
    {
        assert(_oper is GT_CNS_VEC);
        assert(this is GenTreeVecCon);
        return Unsafe.As<GenTreeVecCon>(this);
    }
#endif
#if FEATURE_MASKED_HW_INTRINSICS
    public GenTreeMskCon AsMskCon()
    {
        assert(_oper is GT_CNS_MSK);
        assert(this is GenTreeMskCon);
        return Unsafe.As<GenTreeMskCon>(this);
    }
#endif
    public GenTreeLclVarCommon AsLclVarCommon()
    {
        assert(_oper is GT_LCL_VAR or GT_LCL_FLD or GT_PHI_ARG or GT_STORE_LCL_VAR or GT_STORE_LCL_FLD or GT_LCL_ADDR);
        assert(this is GenTreeLclVarCommon);
        return Unsafe.As<GenTreeLclVarCommon>(this);
    }
    public GenTreeLclVar AsLclVar()
    {
        assert(_oper is GT_LCL_VAR or GT_STORE_LCL_VAR);
        assert(this is GenTreeLclVar);
        return Unsafe.As<GenTreeLclVar>(this);
    }
    public GenTreeLclFld AsLclFld()
    {
        assert(_oper is GT_LCL_FLD or GT_STORE_LCL_FLD or GT_LCL_ADDR);
        assert(this is GenTreeLclFld);
        return Unsafe.As<GenTreeLclFld>(this);
    }
    public GenTreeCast AsCast()
    {
        assert(_oper is GT_CAST);
        assert(this is GenTreeCast);
        return Unsafe.As<GenTreeCast>(this);
    }
    public GenTreeBox AsBox()
    {
        assert(_oper is GT_BOX);
        assert(this is GenTreeBox);
        return Unsafe.As<GenTreeBox>(this);
    }
    public GenTreeFieldAddr AsFieldAddr()
    {
        assert(_oper is GT_FIELD_ADDR);
        assert(this is GenTreeFieldAddr);
        return Unsafe.As<GenTreeFieldAddr>(this);
    }
    public GenTreeCall AsCall()
    {
        assert(_oper is GT_CALL);
        assert(this is GenTreeCall);
        return Unsafe.As<GenTreeCall>(this);
    }
    public GenTreeFieldList AsFieldList()
    {
        assert(_oper is GT_FIELD_LIST);
        assert(this is GenTreeFieldList);
        return Unsafe.As<GenTreeFieldList>(this);
    }
    public GenTreeColon AsColon()
    {
        assert(_oper is GT_COLON);
        assert(this is GenTreeColon);
        return Unsafe.As<GenTreeColon>(this);
    }
    public GenTreeFptrVal AsFptrVal()
    {
        assert(_oper is GT_FTN_ADDR);
        assert(this is GenTreeFptrVal);
        return Unsafe.As<GenTreeFptrVal>(this);
    }
    public GenTreeIntrinsic AsIntrinsic()
    {
        assert(_oper is GT_INTRINSIC);
        assert(this is GenTreeIntrinsic);
        return Unsafe.As<GenTreeIntrinsic>(this);
    }
    public GenTreeIndexAddr AsIndexAddr()
    {
        assert(_oper is GT_INDEX_ADDR);
        assert(this is GenTreeIndexAddr);
        return Unsafe.As<GenTreeIndexAddr>(this);
    }
#if FEATURE_HW_INTRINSICS
    public GenTreeMultiOp AsMultiOp()
    {
        assert(_oper is GT_HWINTRINSIC);
        assert(this is GenTreeMultiOp);
        return Unsafe.As<GenTreeMultiOp>(this);
    }
#endif
    public GenTreeBoundsChk AsBoundsChk()
    {
        assert(_oper is GT_BOUNDS_CHECK);
        assert(this is GenTreeBoundsChk);
        return Unsafe.As<GenTreeBoundsChk>(this);
    }
    public GenTreeArrCommon AsArrCommon()
    {
        assert(_oper is GT_ARR_LENGTH or GT_MDARR_LENGTH or GT_MDARR_LOWER_BOUND);
        assert(this is GenTreeArrCommon);
        return Unsafe.As<GenTreeArrCommon>(this);
    }
    public GenTreeArrLen AsArrLen()
    {
        assert(_oper is GT_ARR_LENGTH);
        assert(this is GenTreeArrLen);
        return Unsafe.As<GenTreeArrLen>(this);
    }
    public GenTreeMDArr AsMDArr()
    {
        assert(_oper is GT_MDARR_LENGTH or GT_MDARR_LOWER_BOUND);
        assert(this is GenTreeMDArr);
        return Unsafe.As<GenTreeMDArr>(this);
    }
    public GenTreeArrElem AsArrElem()
    {
        assert(_oper is GT_ARR_ELEM);
        assert(this is GenTreeArrElem);
        return Unsafe.As<GenTreeArrElem>(this);
    }
    public GenTreeRetExpr AsRetExpr()
    {
        assert(_oper is GT_RET_EXPR);
        assert(this is GenTreeRetExpr);
        return Unsafe.As<GenTreeRetExpr>(this);
    }
    public GenTreeILOffset AsILOffset()
    {
        assert(_oper is GT_IL_OFFSET);
        assert(this is GenTreeILOffset);
        return Unsafe.As<GenTreeILOffset>(this);
    }
    public GenTreeCopyOrReload AsCopyOrReload()
    {
        assert(_oper is GT_COPY or GT_RELOAD);
        assert(this is GenTreeCopyOrReload);
        return Unsafe.As<GenTreeCopyOrReload>(this);
    }
    public GenTreeAddrMode AsAddrMode()
    {
        assert(_oper is GT_LEA);
        assert(this is GenTreeAddrMode);
        return Unsafe.As<GenTreeAddrMode>(this);
    }
    public GenTreeQmark AsQmark()
    {
        assert(_oper is GT_QMARK);
        assert(this is GenTreeQmark);
        return Unsafe.As<GenTreeQmark>(this);
    }
    public GenTreePhiArg AsPhiArg()
    {
        assert(_oper is GT_PHI_ARG);
        assert(this is GenTreePhiArg);
        return Unsafe.As<GenTreePhiArg>(this);
    }
    public GenTreePhi AsPhi()
    {
        assert(_oper is GT_PHI);
        assert(this is GenTreePhi);
        return Unsafe.As<GenTreePhi>(this);
    }
    public GenTreeIndir AsIndir()
    {
        assert(_oper is GT_IND or GT_NULLCHECK or GT_BLK or GT_STORE_BLK or GT_LOCKADD or GT_XAND or GT_XORR or GT_XADD or GT_XCHG or GT_CMPXCHG or GT_STOREIND);
        assert(this is GenTreeIndir);
        return Unsafe.As<GenTreeIndir>(this);
    }
    public GenTreeBlk AsBlk()
    {
        assert(_oper is GT_BLK or GT_STORE_BLK);
        assert(this is GenTreeBlk);
        return Unsafe.As<GenTreeBlk>(this);
    }
    public GenTreeStoreInd AsStoreInd()
    {
        assert(_oper is GT_STOREIND);
        assert(this is GenTreeStoreInd);
        return Unsafe.As<GenTreeStoreInd>(this);
    }
    public GenTreeCmpXchg AsCmpXchg()
    {
        assert(_oper is GT_CMPXCHG);
        assert(this is GenTreeCmpXchg);
        return Unsafe.As<GenTreeCmpXchg>(this);
    }
#if TARGET_ARM64
    public GenTreeConditional AsConditional()
    {
        assert(_oper is GT_SELECT or GT_SELECT_INC or GT_SELECT_INV or GT_SELECT_NEG);
        assert(this is GenTreeConditional);
        return Unsafe.As<GenTreeConditional>(this);
    }
#else
    public GenTreeConditional AsConditional()
    {
        assert(_oper is GT_SELECT);
        assert(this is GenTreeConditional);
        return Unsafe.As<GenTreeConditional>(this);
    }
#endif
    public GenTreePutArgStk AsPutArgStk()
    {
        assert(_oper is GT_PUTARG_STK);
        assert(this is GenTreePutArgStk);
        return Unsafe.As<GenTreePutArgStk>(this);
    }
    public GenTreePhysReg AsPhysReg()
    {
        assert(_oper is GT_PHYSREG);
        assert(this is GenTreePhysReg);
        return Unsafe.As<GenTreePhysReg>(this);
    }
#if FEATURE_HW_INTRINSICS
    public GenTreeHWIntrinsic AsHWIntrinsic()
    {
        assert(_oper is GT_HWINTRINSIC);
        assert(this is GenTreeHWIntrinsic);
        return Unsafe.As<GenTreeHWIntrinsic>(this);
    }
#endif
    public GenTreeAllocObj AsAllocObj()
    {
        assert(_oper is GT_ALLOCOBJ);
        assert(this is GenTreeAllocObj);
        return Unsafe.As<GenTreeAllocObj>(this);
    }
    public GenTreeRuntimeLookup AsRuntimeLookup()
    {
        assert(_oper is GT_RUNTIMELOOKUP);
        assert(this is GenTreeRuntimeLookup);
        return Unsafe.As<GenTreeRuntimeLookup>(this);
    }
    public GenTreeArrAddr AsArrAddr()
    {
        assert(_oper is GT_ARR_ADDR);
        assert(this is GenTreeArrAddr);
        return Unsafe.As<GenTreeArrAddr>(this);
    }
    public GenTreeCC AsCC()
    {
        assert(_oper is GT_JCC or GT_SETCC);
        assert(this is GenTreeCC);
        return Unsafe.As<GenTreeCC>(this);
    }
#if TARGET_ARM64 || TARGET_AMD64
    public GenTreeCCMP AsCCMP()
    {
        assert(_oper is GT_CCMP);
        assert(this is GenTreeCCMP);
        return Unsafe.As<GenTreeCCMP>(this);
    }
#endif
#if TARGET_ARM64
    public GenTreeOpCC AsOpCC()
    {
        assert(_oper is GT_SELECTCC or GT_SELECT_INCCC or GT_JCMP or GT_JTEST or GT_SELECT_INVCC or GT_SELECT_NEGCC);
        assert(this is GenTreeOpCC);
        return Unsafe.As<GenTreeOpCC>(this);
    }
#else
    public GenTreeOpCC AsOpCC()
    {
        assert(_oper is GT_SELECTCC or GT_JCMP or GT_JTEST);
        assert(this is GenTreeOpCC);
        return Unsafe.As<GenTreeOpCC>(this);
    }
#endif
#if !TARGET_64BIT
    public GenTreeMultiRegOp AsMultiRegOp()
    {
        assert(_oper is GT_MUL_LONG);
        assert(this is GenTreeMultiRegOp);
        return Unsafe.As<GenTreeMultiRegOp>(this);
    }
#endif
}
