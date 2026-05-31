// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace RyuJitSharp;

public partial class Compiler
{
    // TODO: Port Compiler.gtMarkColonCond
    // public static unsafe fgWalkPreFn gtMarkColonCond;

    // TODO: Port Compiler.gtClearColonCond
    // public static unsafe fgWalkPreFn gtClearColonCond;

    /// <summary>see if storing a ref type value to an array can skip the array store covariance check.</summary>
    /// <param name="value">tree producing the value to store</param>
    /// <param name="array">tree representing the array to store to</param>
    /// <returns>true if the store does not require a covariance check.</returns>
    public unsafe bool gtCanSkipCovariantStoreCheck(GenTree value, GenTree array)
    {
        // We should only call this when optimizing.
        assert(opts.OptimizationEnabled);

        // Check for store to same array, ie. arrLcl[i] = arrLcl[j]
        if ((value.Oper is GT_IND))
        {
            var valueAddr = value.AsIndir().Addr;

            if ((valueAddr.Oper is GT_INDEX_ADDR) && (array.Oper is GT_LCL_VAR))
            {
                var valueArray = valueAddr.AsIndexAddr().Arr;

                if (valueArray.Oper is GT_LCL_VAR)
                {
                    var valueArrayLcl = valueArray.AsLclVar().LclNum;
                    var arrayLcl = array.AsLclVar().LclNum;

                    if ((valueArrayLcl == arrayLcl) && !lvaGetDesc(arrayLcl).IsAddressExposed)
                    {
                        JITDUMP("\nstelem of ref from same array: skipping covariant store check\n");
                        return true;
                    }
                }
            }
        }

        // Check for store of NULL.
        if (value.Oper is GT_CNS_INT)
        {
            assert(value.Type is TYP_REF);
            var intCon = value.AsIntCon();

            if (intCon.IconVal is 0)
            {
                JITDUMP("\nstelem of null: skipping covariant store check\n");
                return true;
            }

            // Non-0 const refs can only occur with frozen objects
            assert(intCon.IsIconHandle(GTF_ICON_OBJ_HDL));
        }

        // Try and get a class handle for the array
        if (value.Type is not TYP_REF)
        {
            return false;
        }

        var arrayHandle = gtGetClassHandle(array, out var arrayIsExact, out var arrayIsNonNull);

        if (arrayHandle == NO_CLASS_HANDLE)
        {
            return false;
        }

        // There are some methods in corelib where we're storing to an array but the IL
        // doesn't reflect this (see SZArrayHelper). Avoid.
        var attribs = info.compCompHnd->getClassAttribs(arrayHandle);

        if ((attribs & CORINFO_FLG_ARRAY) is 0)
        {
            return false;
        }

        CORINFO_CLASS_HANDLE arrayElementHandle = null;
        var arrayElemType = info.compCompHnd->getChildType(arrayHandle, &arrayElementHandle);

        // Verify array type handle is really an array of ref type
        assert(arrayElemType == CORINFO_TYPE_CLASS);

        // Check for exactly object[]
        if (arrayIsExact && (arrayElementHandle == impObjectClass))
        {
            JITDUMP("\nstelem to (exact) object[]: skipping covariant store check\n");
            return true;
        }

        var arrayTypeIsSealed = info.compCompHnd->isExactType(arrayElementHandle);

        if ((!arrayIsExact && !arrayTypeIsSealed) || (arrayElementHandle == NO_CLASS_HANDLE))
        {
            // Bail out if we don't know array's exact type
            return false;
        }

        var valueHandle = gtGetClassHandle(value, out var valueIsExact, out var valueIsNonNull);

        // Array's type is sealed and equals to value's type
        if (arrayTypeIsSealed && (valueHandle == arrayElementHandle))
        {
            JITDUMP("\nstelem to T[] with T exact: skipping covariant store check\n");
            return true;
        }

        // Array's type is not sealed but we know its exact type
        if (arrayIsExact && (valueHandle != NO_CLASS_HANDLE) && (info.compCompHnd->compareTypesForCast(valueHandle, arrayElementHandle) == TypeCompareState.Must))
        {
            JITDUMP("\nstelem to T[] with T exact: skipping covariant store check\n");
            return true;
        }

        return false;
    }

    /// <summary>Clones the given tree value and returns a copy of the given tree.</summary>
    /// <param name="tree"></param>
    /// <param name="complexOK">When false, the cloning is only done provided the tree is not too complex(whatever that may mean);</param>
    /// <returns><c>null</c> is returned if the tree cannot be cloned</returns>
    /// <remarks>Note that there is the function <see cref="gtCloneExpr(GenTree?)" /> which does a more complete job if you can't handle this function failing.</remarks>
    public unsafe GenTree? gtClone(GenTree tree, bool complexOK = false)
    {
        var copy = null as GenTree;

        switch (tree.Oper)
        {
            case GT_LCL_VAR:
            {
                copy = gtCloneLclVar(tree.AsLclVar());
                break;
            }

            case GT_LCL_FLD:
            {
                copy = gtCloneLclFld(tree.AsLclFld());
                break;
            }

            case GT_LCL_ADDR:
            {
                var lclFld = tree.AsLclFld();

                if (complexOK || (lclFld.LclOffs is not 0))
                {
                    copy = gtCloneLclAddr(tree.AsLclFld());
                }
                break;
            }

            case GT_FTN_ADDR:
            {
                copy = gtCloneFtnAddr(tree.AsFptrVal());
                break;
            }

            case GT_CNS_INT:
            {
                copy = gtCloneCnsInt(tree.AsIntCon());
                break;
            }

            case GT_CNS_LNG:
                copy = gtCloneCnsLng(tree.AsLngCon());
                break;

            case GT_CNS_DBL:
            {
                copy = gtCloneCnsDbl(tree.AsDblCon());
                break;
            }

#if FEATURE_SIMD
            case GT_CNS_VEC:
            {
                copy = gtCloneCnsVec(tree.AsVecCon());
                break;
            }
#endif

#if FEATURE_MASKED_HW_INTRINSICS
            case GT_CNS_MSK:
            {
                copy = gtCloneCnsMsk(tree.AsMskCon());
                break;
            }
#endif

            default:
            {
                if (!complexOK)
                {
                    break;
                }

                if (tree.Oper.IsLoad)
                {
                    var indir = tree.AsIndir();
                    var indirAddr = indir.Addr;

                    var fldObjCopy = null as GenTree;

                    if (indirAddr.Oper is GT_FIELD_ADDR)
                    {
                        var fieldAddr = indirAddr.AsFieldAddr();

                        if (fieldAddr.IsInstance)
                        {
                            fldObjCopy = gtClone(fieldAddr.FldObj, complexOK: false);

                            if (fldObjCopy is null)
                            {
                                break;
                            }
                        }

                        var fieldAddrCopy = gtNewFieldAddrNode(fieldAddr.Type, fldObjCopy, fieldAddr.FldHnd, fieldAddr.FldOffset);

                        fieldAddrCopy.MayOverlap = fieldAddr.MayOverlap;
                        fieldAddrCopy.IsSpanLength = fieldAddr.IsSpanLength;

#if FEATURE_READYTORUN
                        fieldAddrCopy.FieldLookup = fieldAddr.FieldLookup;
#endif

                        var indirCopy = (tree.Oper is GT_BLK) ? gtNewBlkIndir(fieldAddrCopy, tree.AsBlk().Layout) : gtNewIndir(tree.Type, fieldAddrCopy);
                        impAnnotateFieldIndir(indirCopy);
                        copy = indirCopy;
                    }
                }
                else if (tree.Oper is GT_ADD or GT_SUB)
                {
                    var op = tree.AsOp();

                    var op1 = op.Op1;
                    var op2 = op.Op2;

                    if (op1.Oper.IsLeaf && op2.Oper.IsLeaf)
                    {
                        op1 = gtClone(op1);

                        if (op1 is null)
                        {
                            break;
                        }

                        op2 = gtClone(op2);

                        if (op2 is null)
                        {
                            break;
                        }

                        copy = gtNewBinaryNode(tree.Oper, tree.Type, op1, op2);
                    }
                }
                break;
            }
        }

        if (copy is not null)
        {
            copy.Flags |= (tree.Flags & ~GTF_NODE_MASK);

#if DEBUG
            copy._debugFlags |= (tree._debugFlags & ~GTF_DEBUG_NODE_MASK);
#endif
        }
        return copy;
    }

    public GenTreeDblCon gtCloneCnsDbl(GenTreeDblCon dblCon)
    {
        return gtNewDconNode(dblCon.Type, dblCon.DconVal);
    }

    public GenTreeIntCon gtCloneCnsInt(GenTreeIntCon intCon)
    {
        var intConCopy = null as GenTreeIntCon;

        if (intCon.IsIconHandle())
        {
            intConCopy = gtNewIconHandleNode(intCon.IconVal, intCon.Flags, intCon.FieldSeq);
        }
        else
        {
            intConCopy = new GenTreeIntCon(intCon.Type, intCon.IconVal, intCon.FieldSeq);
        }

        intConCopy.CompileTimeHandle = intCon.CompileTimeHandle;
#if DEBUG
        intConCopy.TargetHandle = intCon.TargetHandle;
#endif

        return intConCopy;
    }

    public GenTreeIntConCommon gtCloneCnsLng(GenTreeLngCon lngCon)
    {
        return gtNewLconNode(lngCon.LconValue);
    }

#if FEATURE_MASKED_HW_INTRINSICS
    public GenTreeMskCon gtCloneCnsMsk(GenTreeMskCon mskCon)
    {
        return gtNewMskConNode(mskCon.SimdMaskVal);
    }
#endif

#if FEATURE_SIMD
    public GenTreeVecCon gtCloneCnsVec(GenTreeVecCon vecCon)
    {
        var vecConClone = gtNewVconNode(vecCon.Type);
        vecConClone.SimdVal = vecCon.SimdVal;
        return vecConClone;
    }
#endif

    /// <summary>Create a copy of `tree`</summary>
    /// <param name="tree">GenTree to create a copy of</param>
    /// <returns>A copy of the given tree.</returns>
    [return: NotNullIfNotNull(nameof(tree))]
    public unsafe GenTree? gtCloneExpr(GenTree? tree)
    {
        if (tree is null)
        {
            return null;
        }

        var oper = tree.Oper;
        var copy = null as GenTree;

        if (oper.IsLeaf)
        {
            copy = gtCloneLeaf(this, tree);
        }
        else if (oper.IsBinary)
        {
            copy = gtCloneBinary(this, tree.AsOp());
        }
        else if (oper.IsUnary)
        {
            copy = gtCloneUnary(this, tree.AsUnOp());
        }
        else
        {
            copy = gtCloneSpecial(this, tree);
        }

        assert(copy.Oper == oper);
        assert(copy.Type == tree.Type);

        // A cloned tree gets the original's Value number pair
        copy._vnPair = tree._vnPair;
        copy.Flags = tree.Flags;

#if DEBUG
        // Non-node debug flags should be propagated from 'tree' to 'copy'
        copy._debugFlags |= (tree._debugFlags & ~GTF_DEBUG_NODE_MASK);
#endif

        // Make sure to copy back fields that may have been initialized

        copy.CopyCostsRaw(tree);
        copy.CopyReg(tree);

        return copy;

        static GenTreeOp gtCloneBinary(Compiler compiler, GenTreeOp tree)
        {
            var oper = tree.Oper;
            var copy = null as GenTreeOp;

            switch (oper)
            {
                case GT_INTRINSIC:
                {
                    var intrinsic = tree.AsIntrinsic();
                    copy = new GenTreeIntrinsic(
                        intrinsic.Type,
                        compiler.gtCloneExpr(intrinsic.Op1),
                        compiler.gtCloneExpr(intrinsic.Op2),
                        intrinsic.IntrinsicName,
                        intrinsic.MethodHandle
                    ) {
#if FEATURE_READYTORUN
                        EntryPoint = intrinsic.EntryPoint,
#endif
                    };
                    break;
                }

                case GT_BOUNDS_CHECK:
                {
                    var boundsChk = tree.AsBoundsChk();
                    copy = new GenTreeBoundsChk(
                        compiler.gtCloneExpr(boundsChk.Index),
                        compiler.gtCloneExpr(boundsChk.ArrayLength),
                        boundsChk.ThrowKind) {
                        InxType = boundsChk.InxType,
                    };
                    break;
                }

                case GT_STOREIND:
                {
                    var storeInd = tree.AsStoreInd();
                    copy = new GenTreeStoreInd(
                        storeInd.Type,
                        compiler.gtCloneExpr(storeInd.Addr),
                        compiler.gtCloneExpr(storeInd.Data)) {
                        RmwStatus = storeInd.RmwStatus
                    };
                    break;
                }

                case GT_STORE_BLK:
                {
                    var blk = tree.AsBlk();
                    copy = new GenTreeBlk(
                        blk.Type,
                        compiler.gtCloneExpr(blk.Addr),
                        compiler.gtCloneExpr(blk.Data),
                        blk.Layout
                    );
                    break;
                }

                case GT_QMARK:
                {
                    var qmark = tree.AsQmark();
                    copy = new GenTreeQmark(
                        qmark.Type,
                        compiler.gtCloneExpr(qmark.Cond),
                        compiler.gtCloneExpr(qmark.Colon).AsColon(),
                        qmark.ThenNodeLikelihood
                    );
                    break;
                }

                case GT_COLON:
                {
                    var colon = tree.AsColon();
                    copy = compiler.gtNewColonNode(
                        colon.Type,
                        compiler.gtCloneExpr(colon.ThenNode),
                        compiler.gtCloneExpr(colon.ElseNode)
                    );
                    break;
                }

                case GT_INDEX_ADDR:
                {
                    var indexAddr = tree.AsIndexAddr();
                    copy = new GenTreeIndexAddr(
                        compiler.gtCloneExpr(indexAddr.Arr),
                        compiler.gtCloneExpr(indexAddr.Index),
                        indexAddr.ElemType,
                        indexAddr.StructElemClass,
                        indexAddr.ElemSize,
                        indexAddr.LenOffset,
                        indexAddr.ElemOffset,
                        indexAddr.IsBoundsChecked
                    );
                    break;
                }

                case GT_LEA:
                {
                    var addrMode = tree.AsAddrMode();
                    copy = new GenTreeAddrMode(
                        addrMode.Type,
                        compiler.gtCloneExpr(addrMode.BaseAddress),
                        compiler.gtCloneExpr(addrMode.Index),
                        addrMode.Scale,
                        addrMode.Offset
                    );
                    break;
                }

#if !TARGET_64BIT
                case GT_MUL_LONG:
                {
                    var multiRegOp = tree.AsMultiRegOp();

                    var multiRegOpCopy = new GenTreeMultiRegOp(
                        oper,
                        multiRegOp.Type,
                        compiler.gtCloneExpr(multiRegOp.Op1),
                        compiler.gtCloneExpr(multiRegOp.Op2)
                    );
                    multiRegOpCopy.CopyOtherRegs(multiRegOp);

                    copy = multiRegOpCopy;
                    break;
                }
#endif

                case GT_JCMP:
                case GT_JTEST:
                case GT_SELECTCC:
#if TARGET_ARM64
                case GT_SELECT_INCCC:
                case GT_SELECT_INVCC:
                case GT_SELECT_NEGCC:
#endif
                {
                    var opCC = tree.AsOpCC();
                    copy = new GenTreeOpCC(
                        oper,
                        opCC.Type,
                        opCC.Condition,
                        opCC.Op1,
                        opCC.Op2
                    );
                    break;
                }

                case GT_CCMP:
                {
                    var ccmp = tree.AsCCMP();
                    copy = new GenTreeCCMP(
                        ccmp.Type,
                        ccmp.Condition,
                        compiler.gtCloneExpr(ccmp.Op1),
                        compiler.gtCloneExpr(ccmp.Op2),
                        ccmp.FlagsVal
                    );
                    break;
                }

                default:
                {
                    assert(!oper.IsExOp);
                    copy = compiler.gtNewBinaryNode(
                        oper,
                        tree.Type,
                        compiler.gtCloneExpr(tree.Op1),
                        compiler.gtCloneExpr(tree.Op2)
                    );
                    break;
                }
            }

            return copy;
        }

        static GenTreeCall gtCloneCall(Compiler compiler, GenTreeCall tree)
        {
            var copy = new GenTreeCall(tree.Type);

            copy._args.InternalCopyFrom(compiler, tree._args);

#if DEBUG || TARGET_WASM
            // The call sig comes from the EE and doesn't change throughout the compilation process, meaning
            // we only really need one physical copy of it. Therefore a shallow pointer copy will suffice.
            // (Note that this still holds even if the tree we are cloning was created by an inlinee compiler,
            // because the inlinee still uses the inliner's memory allocator anyway.)
            copy._callSig = tree._callSig;
#endif
            // TailCallInfo or AsyncInfo or UnmgdCallConv
            copy._anonymous1 = tree._anonymous1;

#if FEATURE_MULTIREG_RET
            copy._returnTypeDesc = tree._returnTypeDesc;
            copy.CopyOtherRegs(tree);
#endif

            copy._callMoreFlags = tree._callMoreFlags;

            // _callType and _returnType
            copy._bitfield = tree._bitfield;

            copy._inlineInfoCount = tree._inlineInfoCount;
            copy._retClsHnd = tree._retClsHnd;

            // StubCallStubAddr or InitCldHnd or CastHelperILOffset
            copy._anonymous2 = tree._anonymous2;

            // InlineCandidateInfo or InlineCandidateInfoList or HandleHistogramProfileCandidateInfo
            copy._anonymous3 = tree._anonymous3;

            // CompileTimeHelperArgumentHandle or DirectCallAddress
            copy._anonymous4 = tree._anonymous4;

            copy._callCookie = tree._callCookie;

            copy._lateDevirtualizationInfo = tree._lateDevirtualizationInfo;
            copy._controlExpr = compiler.gtCloneExpr(tree._controlExpr);
            copy._callMethHnd = tree._callMethHnd;

            copy._addr = tree._addr;

#if FEATURE_READYTORUN
            copy._entryPoint = tree._entryPoint;
#endif

#if DEBUG
            copy._callDebugFlags = tree._callDebugFlags;
            copy._inlineObservation = tree._inlineObservation;
            copy._rawILOffset = tree._rawILOffset;
#endif

            copy._inlineContext = tree._inlineContext;

            // We keep track of the number of no return calls, so if we've cloned one of these, update the tracking.
            if (tree.IsNoReturn)
            {
                assert(copy.IsNoReturn);
                compiler.setMethodHasNoReturnCalls();
            }
            return copy;
        }

        static GenTree gtCloneLeaf(Compiler compiler, GenTree tree)
        {
            var oper = tree.Oper;
            var copy = null as GenTree;

            switch (oper)
            {
                case GT_LCL_VAR:
                {
                    copy = compiler.gtCloneLclVar(tree.AsLclVar());
                    break;
                }

                case GT_LCL_FLD:
                {
                    copy = compiler.gtCloneLclFld(tree.AsLclFld());
                    break;
                }

                case GT_LCL_ADDR:
                {
                    copy = compiler.gtCloneLclAddr(tree.AsLclFld());
                    break;
                }

                case GT_CATCH_ARG:
                case GT_ASYNC_CONTINUATION:
                case GT_LABEL:
                case GT_GCPOLL:
                case GT_FTN_ENTRY:
                case GT_NOP:
                case GT_NO_OP:
#if SWIFT_SUPPORT
                case GT_SWIFT_ERROR:
#endif
#if TARGET_WASM
                case GT_WASM_THROW_REF:
#endif
                {
                    copy = new GenTree(oper, tree.Type);
                    break;
                }

                case GT_JMP:
                case GT_ASYNC_RESUME_INFO:
                case GT_RECORD_ASYNC_RESUME:
                {
                    var val = tree.AsVal();
                    copy = new GenTreeVal(oper, val.Type, val.Val1);
                    break;
                }

                case GT_FTN_ADDR:
                {
                    copy = compiler.gtCloneFtnAddr(tree.AsFptrVal());
                    break;
                }

                case GT_RET_EXPR:
                {
                    // GT_RET_EXPR is unique node, that contains a link to a gtInlineCandidate node,
                    // that is part of another statement. We cannot clone both here and cannot
                    // create another GT_RET_EXPR that points to the same gtInlineCandidate.
                    NO_WAY("Cloning of GT_RET_EXPR node not supported");
                    break;
                }

                case GT_CNS_INT:
                {
                    copy = compiler.gtCloneCnsInt(tree.AsIntCon());
                    break;
                }

                case GT_CNS_LNG:
                {
                    copy = compiler.gtCloneCnsLng(tree.AsLngCon());
                    break;
                }

                case GT_CNS_DBL:
                {
                    copy = compiler.gtCloneCnsDbl(tree.AsDblCon());
                    break;
                }

                case GT_CNS_STR:
                {
                    var strCon = tree.AsStrCon();
                    copy = compiler.gtNewSconNode(strCon.SconCpx, strCon.ScpHnd);
                    break;
                }

#if FEATURE_SIMD
                case GT_CNS_VEC:
                {
                    copy = compiler.gtCloneCnsVec(tree.AsVecCon());
                    break;
                }
#endif

#if (FEATURE_MASKED_HW_INTRINSICS)
                case GT_CNS_MSK:
                {
                    copy = compiler.gtCloneCnsMsk(tree.AsMskCon());
                    break;
                }
#endif

                case GT_MEMORYBARRIER:
                {
                    copy = gtNewMemoryBarrierNode();
                    break;
                }

                default:
                {
                    // GT_PHI_ARG
                    // GT_JCC
                    // GT_SETCC
                    // GT_START_NONGC
                    // GT_START_PREEMPTYGC
                    // GT_PROF_HOOK
                    // GT_WASM_JEXCEPT
                    // GT_JMPTABLE
                    // GT_PHYSREG
                    // GT_IL_OFFSET
                    NO_WAY("Cloning of node not supported");
                    break;
                }
            }
            return copy;
        }

        static GenTree gtCloneSpecial(Compiler compiler, GenTree tree)
        {
            var oper = tree.Oper;
            var copy = null as GenTree;

            switch (oper)
            {
                case GT_PHI:
                {
                    var phi = tree.AsPhi();

                    var phiCopy = new GenTreePhi(phi.Type);
                    var firstUse = phi.FirstUse;

                    if (firstUse is not null)
                    {
                        firstUse = new GenTreePhi.Use(
                            compiler.gtCloneExpr(firstUse.Node),
                            firstUse.Next
                        );

                        var prevUse = firstUse;

                        for (var use = firstUse.Next; use is not null; use = use.Next)
                        {
                            use = new GenTreePhi.Use(
                                compiler.gtCloneExpr(use.Node),
                                use.Next
                            );

                            prevUse.Next = use;
                            prevUse = use;
                        }
                    }
                    phiCopy.FirstUse = firstUse;

                    copy = phiCopy;
                    break;
                }

                case GT_CMPXCHG:
                {
                    var cmpXchg = tree.AsCmpXchg();
                    copy = new GenTreeCmpXchg(
                        cmpXchg.Type,
                        compiler.gtCloneExpr(cmpXchg.Addr),
                        compiler.gtCloneExpr(cmpXchg.Data),
                        compiler.gtCloneExpr(cmpXchg.Comparand)
                    );
                    break;
                }

                case GT_SELECT:
#if TARGET_ARM64
                case GT_SELECT_INC:
                case GT_SELECT_INV:
                case GT_SELECT_NEG:
#endif
                {
                    var conditional = tree.AsConditional();
                    copy = new GenTreeConditional(
                        oper,
                        conditional.Type,
                        compiler.gtCloneExpr(conditional.Cond),
                        compiler.gtCloneExpr(conditional.Op1),
                        compiler.gtCloneExpr(conditional.Op2)
                    );
                    break;
                }

#if FEATURE_HW_INTRINSICS
                case GT_HWINTRINSIC:
                {
                    var hwintrinsic = tree.AsHWIntrinsic();
                    var operands = hwintrinsic.Operands.ToArray();

                    for (var i = 0; i < operands.Length; i++)
                    {
                        operands[i] = compiler.gtCloneExpr(operands[i]);
                    }

                    var hwintrinsicCopy = new GenTreeHWIntrinsic(
                        hwintrinsic.Type,
                        hwintrinsic.HWIntrinsicId,
                        hwintrinsic.simdBaseType,
                        hwintrinsic.simdSize,
                        operands
                    );

                    if (hwintrinsic.IsUserCall)
                    {
                        hwintrinsicCopy.MethodHandle = hwintrinsic.MethodHandle;
#if FEATURE_READYTORUN
                        hwintrinsicCopy.EntryPoint = hwintrinsic.EntryPoint;
#endif
                    }
                    hwintrinsicCopy.AuxiliaryType = hwintrinsic.AuxiliaryType;
                    hwintrinsicCopy.CopyOtherRegs(hwintrinsic);

                    copy = hwintrinsicCopy;
                    break;
                }
#endif

                case GT_ARR_ELEM:
                {
                    var arrElem = tree.AsArrElem();
                    var arrInds = arrElem.ArrInds.ToArray();

                    for (var i = 0; i < arrInds.Length; i++)
                    {
                        arrInds[i] = compiler.gtCloneExpr(arrInds[i]);
                    }

                    copy = new GenTreeArrElem(
                        arrElem.Type,
                        compiler.gtCloneExpr(arrElem.ArrObj),
                        arrElem.ArrElemSize,
                        arrInds
                    );
                    break;
                }

                case GT_CALL:
                {
                    var call = tree.AsCall();

                    // We can't safely clone calls that have GT_RET_EXPRs via gtCloneExpr.
                    // You must use gtCloneCandidateCall for these calls (and then do appropriate other fixup)
                    if (call.IsInlineCandidate || call.IsGuardedDevirtualizationCandidate)
                    {
                        NO_WAY("Cloning of calls with associated GT_RET_EXPR nodes is not supported");
                    }

                    copy = gtCloneCall(compiler, call);
                    break;
                }

                case GT_FIELD_LIST:
                {
                    var fieldList = tree.AsFieldList();
                    var fieldListCopy = new GenTreeFieldList();

                    foreach (var use in fieldList.Uses)
                    {
                        var useCopy = new GenTreeFieldList.Use(
                            compiler.gtCloneExpr(use.Node),
                            use.Offset,
                            use.Type
                        );
                        fieldListCopy.Uses.AddUse(useCopy);
                    }

                    copy = fieldListCopy;
                    break;
                }

                default:
                {
#if DEBUG
                    compiler.gtDispTree(tree);
#endif
                    NO_WAY("unexpected special operator");
                    break;
                }
            }

            return copy;
        }

        static GenTreeUnOp gtCloneUnary(Compiler compiler, GenTreeUnOp tree)
        {
            var oper = tree.Oper;
            var copy = null as GenTreeUnOp;

            switch (oper)
            {
                case GT_STORE_LCL_VAR:
                {
                    // Remember that the local node has been cloned. The flag will be set on 'copy' as well.
                    var lclVar = tree.AsLclVar();
                    lclVar.Flags |= GTF_VAR_MOREUSES;

                    var lclVarCopy = compiler.gtNewStoreLclVarNode(
                        lclVar.LclNum,
                        compiler.gtCloneExpr(lclVar.Data)
                    );
                    lclVarCopy.CopyOtherRegs(lclVar);

                    copy = lclVarCopy;
                    break;
                }

                case GT_STORE_LCL_FLD:
                {
                    // Remember that the local node has been cloned. The flag will be set on 'copy' as well.
                    var lclFld = tree.AsLclFld();
                    lclFld.Flags |= GTF_VAR_MOREUSES;

                    assert(lclFld.Layout is not null);
                    copy = new GenTreeLclFld(
                        lclFld.Type,
                        lclFld.LclNum,
                        lclFld.LclOffs,
                        compiler.gtCloneExpr(lclFld.Data),
                        lclFld.Layout
                    );
                    break;
                }

                case GT_CAST:
                {
                    var cast = tree.AsCast();
                    copy = compiler.gtNewCastNode(
                        cast.Type,
                        compiler.gtCloneExpr(cast.CastOp),
                        cast.IsUnsigned,
                        cast.CastType
                    );
                    break;
                }

                case GT_IND:
                case GT_NULLCHECK:
                {
                    var indir = tree.AsIndir();
                    copy = new GenTreeIndir(
                        oper,
                        indir.Type,
                        compiler.gtCloneExpr(indir.Addr)
                    );
                    break;
                }

                case GT_BLK:
                {
                    var blk = tree.AsBlk();
                    copy = new GenTreeBlk(
                        blk.Type,
                        compiler.gtCloneExpr(blk.Addr),
                        blk.Layout
                    );
                    break;
                }

                case GT_ARR_LENGTH:
                {
                    var arrLen = tree.AsArrLen();
                    copy = compiler.gtNewArrLen(
                        arrLen.Type,
                        compiler.gtCloneExpr(arrLen.ArrRef),
                        arrLen.ArrLenOffset
                    );
                    break;
                }

                case GT_MDARR_LENGTH:
                {
                    var mdArr = tree.AsMDArr();
                    copy = compiler.gtNewMDArrLen(
                        compiler.gtCloneExpr(mdArr.ArrRef),
                        mdArr.Dim,
                        mdArr.Rank
                    );
                    break;
                }

                case GT_MDARR_LOWER_BOUND:
                {
                    var mdArr = tree.AsMDArr();
                    copy = compiler.gtNewMDArrLowerBound(
                        compiler.gtCloneExpr(mdArr.ArrRef),
                        mdArr.Dim,
                        mdArr.Rank
                    );
                    break;
                }

                case GT_FIELD_ADDR:
                {
                    copy = compiler.gtCloneFieldAddr(tree.AsFieldAddr());
                    break;
                }

                case GT_ALLOCOBJ:
                {
                    var allocObj = tree.AsAllocObj();
                    var allocObjCopy = compiler.gtNewAllocObjNode(
                        allocObj.Type,
                        compiler.gtCloneExpr(allocObj.Op1),
                        allocObj.NewHelper,
                        allocObj.NewHelperHasSideEffects,
                        allocObj.ClsHnd
                    );

#if FEATURE_READYTORUN
                    allocObjCopy.EntryPoint = allocObj.EntryPoint;
#endif

                    copy = allocObjCopy;
                    break;
                }

                case GT_BOX:
                {
                    // Remember that the box node has been cloned. The flag will be set on 'copy' as well.
                    var box = tree.AsBox();
                    box.WasCloned = true;

                    copy = new GenTreeBox(
                        box.Type,
                        compiler.gtCloneExpr(box.BoxOp),
                        box.DefStmtWhenInlinedBoxValue,
                        box.CopyStmtWhenInlinedBoxValue
                    );
                    break;
                }

                case GT_RUNTIMELOOKUP:
                {
                    var runtimeLookup = tree.AsRuntimeLookup();
                    copy = compiler.gtNewRuntimeLookup(
                        compiler.gtCloneExpr(runtimeLookup.Op1),
                        runtimeLookup.Handle,
                        runtimeLookup.HandleType
                    );
                    break;
                }

                case GT_ARR_ADDR:
                {
                    var arrAddr = tree.AsArrAddr();
                    copy = new GenTreeArrAddr(
                        compiler.gtCloneExpr(arrAddr.Addr),
                        arrAddr.ElemType,
                        arrAddr.ElemClassHandle,
                        arrAddr.FirstElemOffset
                    );
                    break;
                }

                case GT_PUTARG_STK:
                {
                    var putArgStk = tree.AsPutArgStk();
                    var call = putArgStk.Call;

                    copy = new GenTreePutArgStk(
                        putArgStk.Type,
                        compiler.gtCloneExpr(putArgStk.Op1),
                        (call is not null) ? gtCloneCall(compiler, call) : null,
                        putArgStk.ArgOffset,
                        putArgStk.StackByteSize,
                        putArgStk.PutInIncomingArgArea
                    );
                    break;
                }

                case GT_COPY:
                case GT_RELOAD:
                {
                    var copyOrReload = tree.AsCopyOrReload();

                    var copyOrRealodCopy = new GenTreeCopyOrReload(
                        oper,
                        copyOrReload.Type,
                        compiler.gtCloneExpr(copyOrReload.Op1)
                    );
                    copyOrRealodCopy.CopyOtherRegs(copyOrReload);

                    copy = copyOrRealodCopy;
                    break;
                }

                default:
                {
                    copy = compiler.gtNewUnaryNode(
                        oper,
                        tree.Type,
                        compiler.gtCloneExpr(tree.Op1)
                    );
                    break;
                }
            }

            return copy;
        }
    }

    public unsafe GenTreeFieldAddr gtCloneFieldAddr(GenTreeFieldAddr fieldAddr)
    {
        var fieldAddrCopy = gtNewFieldAddrNode(
            fieldAddr.Type,
            gtCloneExpr(fieldAddr.FldObj),
            fieldAddr.FldHnd,
            fieldAddr.FldOffset
        );

        fieldAddrCopy.MayOverlap = fieldAddr.MayOverlap;
        fieldAddrCopy.IsSpanLength = fieldAddr.IsSpanLength;

#if FEATURE_READYTORUN
        fieldAddrCopy.FieldLookup = fieldAddr.FieldLookup;
#endif

        return fieldAddrCopy;
    }

    public unsafe GenTreeFptrVal gtCloneFtnAddr(GenTreeFptrVal fptrVal)
    {
        var fptrValCopy = gtNewFptrValNode(fptrVal.Type, fptrVal.FptrMethod);

        fptrValCopy.FptrDelegateTarget = fptrVal.FptrDelegateTarget;
#if FEATURE_READYTORUN
        fptrValCopy.EntryPoint = fptrVal.EntryPoint;
#endif

        return fptrValCopy;
    }

    public GenTreeLclFld gtCloneLclAddr(GenTreeLclFld lclFld)
    {
        return gtNewLclAddrNode(lclFld.Type, lclFld.LclNum, lclFld.LclOffs, lclFld.Layout);
    }

    public GenTreeLclFld gtCloneLclFld(GenTreeLclFld lclFld)
    {
        // Remember that the local node has been cloned. The flag will be set on 'copy' as well.
        lclFld.Flags |= GTF_VAR_MOREUSES;

        var lclFldCopy = gtNewLclFldNode(lclFld.Type, lclFld.LclNum, lclFld.LclOffs, lclFld.Layout);
        lclFldCopy.SsaNum = lclFld.SsaNum;
        return lclFldCopy;
    }

    public GenTreeLclVar gtCloneLclVar(GenTreeLclVar lclVar)
    {
        // Remember that the local node has been cloned. The flag will be set on 'copy' as well.
        lclVar.Flags |= GTF_VAR_MOREUSES;

        var lclVarCopy = gtNewLclvNode(lclVar.Type, lclVar.LclNum, lclVar.LclIlOffs);
        lclVarCopy.SsaNum = lclVar.SsaNum;
        lclVarCopy.CopyOtherRegs(lclVar);

        return lclVarCopy;
    }

    /// <summary>alk a tree collecting a bit set of exceptions the tree may throw.</summary>
    /// <param name="tree">tree to examine</param>
    /// <returns>Bit set of exceptions the tree may throw.</returns>
    public ExceptionSetFlags gtCollectExceptions(GenTree tree)
    {
        var walker = new ExceptionsWalker(this);
        _ = walker.WalkTree(ref tree, user: null);

        assert(((tree.Flags & GTF_EXCEPT) is 0) || (walker.Flags is not ExceptionSetFlags.None));
        return walker.Flags;
    }

#if DEBUG
    public void gtDispRange(LIR.ReadOnlyRange range)
    {
        foreach (var node in range)
        {
            gtDispLIRNode(node);
        }
    }

    public void gtDispClassLayout(ClassLayout layout, var_types type)
    {
        assert(layout is not null);

        if (layout.IsBlockLayout)
        {
            jitprintf($"<{layout.Size}>");
        }
        else if (varTypeIsSimd(type))
        {
            jitprintf($"<{layout.ShortClassName}>");
        }
        else
        {
            jitprintf($"<{layout.ShortClassName}, {layout.Size}>");
        }
    }

    public void gtDispLclVar(int lclNum, bool padForBiggestDisp = true)
    {
        var name = gtGetLclVarName(lclNum);

        if (name.Length == 0)
        {
            return;
        }

        jitprintf(name);

        if (padForBiggestDisp && (name.Length < LONGEST_COMMON_LCL_VAR_DISPLAY_LENGTH))
        {
            jitprintf(new string(' ', int.Max(0, LONGEST_COMMON_LCL_VAR_DISPLAY_LENGTH - name.Length)));
        }
    }

    public void gtDispLclVarStructType(int lclNum)
    {
        ref var varDsc = ref lvaGetDesc(lclNum);
        var type = varDsc.Type;

        if (type is TYP_STRUCT)
        {
            var layout = varDsc.Layout;
            assert(layout is not null);
            gtDispClassLayout(layout, type);
        }
    }

    public void gtDispLIRNode(GenTree node, string prefixMsg = "")
    {
        var indentStack = new IndentStack(this);
        var prefixIndent = 0;

        if (prefixMsg is not null)
        {
            prefixIndent = prefixMsg.Length;
        }

        var nodeIsCall = node.Oper.IsCall;

        // Visit operands
        var operandArc = IIArcTop;

        foreach (var operand in node.Operands)
        {
            if (!operand.IsValue)
            {
                // Either of these situations may happen with calls.
                continue;
            }

            if (nodeIsCall)
            {
                var call = node.AsCall();

                if (operand == call.ControlExpr)
                {
                    DisplayOperand(operand, "control expr", operandArc, ref indentStack, prefixIndent);
                }
                else
                {
                    var curArg = call.Args.FindByNode(operand);
                    assert(curArg is not null);

                    var message = (operand == curArg.EarlyNode) ? gtGetArgMsg(call, curArg) : gtGetLateArgMsg(call, curArg);
                    DisplayOperand(operand, message, operandArc, ref indentStack, prefixIndent);
                }
            }
            else
            {
                DisplayOperand(operand, "", operandArc, ref indentStack, prefixIndent);
            }

            operandArc = IIArc;
        }

        // Visit the operator

        if (prefixMsg is not null)
        {
            jitprintf(prefixMsg);
        }

        const bool topOnly = true;
        const bool isLIR   = true;
        gtDispTree(node, ref indentStack, null, topOnly, isLIR);

        static void DisplayOperand(GenTree operand, string message, IndentInfo operandArc, ref IndentStack indentStack, int prefixIndent)
        {
            assert(operand is not null);
            assert(message is not null);

            if (prefixIndent is not 0)
            {
                jitprintf(new string(' ', prefixIndent));
            }

            // 60 spaces for alignment
            jitprintf(new string(' ', 60));

            indentStack.Push(operandArc);
            indentStack.Print();
            _ = indentStack.Pop();
            operandArc = IIArc;

            jitprintf($"  t{operand.TreeId,-5} {operand.Type.Name,-6} {message}\n");
        }
    }

    public void gtDispStmt(Statement stmt, string? msg = null)
    {
        if (msg is not null)
        {
            jitprintf($"{msg} ");
        }
        jitprintf($"{FMT_STMT(stmt.Id)} ( ");

        ref readonly var di = ref stmt.DebugInfo;

        // For statements in the root we display just the location without the inline context info.
        if ((di.InlineContext is null) || di.InlineContext.IsRoot)
        {
            di.Location.Dump();
        }
        else
        {
            di.Dump(recurse: false);
        }
        jitprintf(" ... ");

        var lastILOffs = stmt.LastILOffset;

        if (lastILOffs == BAD_IL_OFFSET)
        {
            jitprintf("???");
        }
        else
        {
            jitprintf($"0x{lastILOffs:X3}");
        }

        jitprintf(" )");

        if (di.GetParent(out var par))
        {
            jitprintf(" <- ");
            par.Dump(recurse: true);
        }
        jitprintf("\n");

        gtDispTree(stmt.RootNode);
    }

    public void gtDispTree(GenTree tree, string? msg = null, bool topOnly = false, bool isLIR = false)
    {
        var indentStack = new IndentStack(this);
        gtDispTree(tree, ref indentStack, msg, topOnly, isLIR);
    }

    public void gtDispTree(GenTree tree, ref IndentStack indentStack, string? msg = null, bool topOnly = false, bool isLIR = false)
    {
        // TODO: Port Compiler.gtDispTree
    }

    public void gtDispTreeRange(LIR.Range containingRange, GenTree tree)
    {
        gtDispRange(containingRange.GetTreeRangeWithFlags(tree, out _, out _));
    }
#endif

    /// <summary>Extracts side effects from the given expression.</summary>
    /// <param name="expr">the expression tree to extract side effects from</param>
    /// <param name="list">reference to a (possibly null) node</param>
    /// <param name="flags">side effect flags to be considered</param>
    /// <param name="ignoreRoot">ignore side effects on the expression root node</param>
    /// <remarks>
    ///   <para>list is modified such that the original list is executed after all side effects that were extracted.</para>
    ///   <para>The original side effect execution order is preserved.</para>
    /// </remarks>
    public void gtExtractSideEffList(GenTree expr, ref GenTree? list, GenTreeFlags flags = GTF_SIDE_EFFECT, bool ignoreRoot = false)
    {
        var sideEffectExtractor = new SideEffectExtractor(this, flags);

        if (ignoreRoot)
        {
            foreach (ref var operand in expr.UseEdges)
            {
                _ = sideEffectExtractor.WalkTree(ref operand, user: null);
            }
        }
        else
        {
            _ = sideEffectExtractor.WalkTree(ref expr, user: null);
        }

        if (list is not null)
        {
            sideEffectExtractor.Append(list);
        }
        list = sideEffectExtractor.Result;
    }

#if DEBUG
    public string gtGetArgMsg(GenTreeCall call, CallArg arg)
    {
        var stringBuilder = new StringBuilder();
        _ = gtPrintArgPrefix(stringBuilder, call, arg);

        if (arg.LateNode is not null)
        {
            _ = stringBuilder.Append(" setup");
        }
        else if (call.Args.IsAbiInformationDetermined)
        {
            _ = gtPrintABILocation(stringBuilder, arg.AbiInfo);
        }
        return stringBuilder.ToString();
    }

    public string gtGetLateArgMsg(GenTreeCall call, CallArg arg)
    {
        assert(arg.LateNode is not null);
        var stringBuilder = new StringBuilder();

        _ = gtPrintArgPrefix(stringBuilder, call, arg);
        _ = gtPrintABILocation(stringBuilder, arg.AbiInfo);

        return stringBuilder.ToString();
    }

    public StringBuilder gtPrintABILocation(StringBuilder stringBuilder, in AbiPassingInformation abiInfo)
    {
        var firstReg = REG_NA;
        var lastReg  = REG_NA;

        foreach (ref readonly var segment in abiInfo.Segments)
        {
            if (segment.IsPassedInRegister)
            {
#if HAS_FIXED_REGISTER_SET
                var regMsk = segment.RegisterMask;

                while (regMsk != RBM_NONE)
                {
                    var regIdx = int.TrailingZeroCount(regMsk);
                    var reg = (regNumber)(regIdx + segment.RegisterMaskBase);
                    regMsk &= ~(1 << regIdx);

                    if (firstReg == REG_NA)
                    {
                        firstReg = reg;
                        lastReg  = reg;
                    }
                    else if (REG_NEXT(lastReg) == reg)
                    {
                        lastReg = reg;
                    }
                    else
                    {
                        PrintRegs(stringBuilder, firstReg, lastReg);
                        firstReg = reg;
                        lastReg  = reg;
                    }
                }
#else
                var reg = segment.Register;

                if (firstReg == REG_NA)
                {
                    firstReg = reg;
                    lastReg  = reg;
                }
                else if (REG_NEXT(lastReg) == reg)
                {
                    lastReg = reg;
                }
                else
                {
                    PrintRegs(firstReg, lastReg, stringBuilder);
                    firstReg = reg;
                    lastReg  = reg;
                }
#endif
            }
            else
            {
                PrintRegs(stringBuilder, firstReg, lastReg);

#if FEATURE_FIXED_OUT_ARGS
                _ = stringBuilder.Append(CultureInfo.InvariantCulture, $" out+{segment.StackOffset:X2}");
#else
                _ = stringBuilder.Append(" STK");
#endif
            }
        }

        PrintRegs(stringBuilder, firstReg, lastReg);
        return stringBuilder;

        static void PrintRegs(StringBuilder stringBuilder, regNumber firstReg, regNumber lastReg)
        {
            if (firstReg == REG_NA)
            {
                return;
            }

            var printSeparately = firstReg == lastReg;

#if TARGET_XARCH
            // No numeric arg regs, always print separately
            printSeparately = true;
#endif

            if (printSeparately)
            {
                var reg = firstReg;

                while (true)
                {
                    _ = stringBuilder.Append(CultureInfo.InvariantCulture, $" {reg.Name}");

                    if (reg == lastReg)
                    {
                        break;
                    }
                    reg = REG_NEXT(reg);
                }
            }
            else
            {
                // Numeric arg regs, print as a range
                _ = stringBuilder.Append(CultureInfo.InvariantCulture, $" {firstReg.Name}{(REG_NEXT(firstReg) == lastReg ? ' ' : '-')}{lastReg.Name}");
            }

            firstReg = REG_NA;
            lastReg = REG_NA;
        }
    }

    public StringBuilder gtPrintArgPrefix(StringBuilder stringBuilder, GenTreeCall call, CallArg arg)
    {
        var wellKnownName = gtGetWellKnownArgNameForArgMsg(arg.WellKnownArg);

        if (wellKnownName.Length != 0)
        {
            _ = stringBuilder.Append(wellKnownName);
        }
        else
        {
            var argNum = call.Args.GetIndex(arg);
            _ = stringBuilder.Append(CultureInfo.InvariantCulture, $"arg{argNum}");
        }
        return stringBuilder;
    }

    public string gtGetWellKnownArgNameForArgMsg(WellKnownArg arg) => arg switch {
        WellKnownArg.ThisPointer => "this",
        WellKnownArg.VarArgsCookie => "va cookie",
        WellKnownArg.InstParam => "gctx",
        WellKnownArg.AsyncContinuation => "async",
        WellKnownArg.RetBuffer => "retbuf",
        WellKnownArg.PInvokeFrame => "pinv frame",
        WellKnownArg.WrapperDelegateCell => "wrap cell",
        WellKnownArg.ShiftLow => "shift low",
        WellKnownArg.ShiftHigh => "shift high",
        WellKnownArg.VirtualStubCell => "vsd cell",
        WellKnownArg.PInvokeCookie => "pinv cookie",
        WellKnownArg.PInvokeTarget => "pinv tgt",
        WellKnownArg.R2RIndirectionCell => "r2r cell",
        WellKnownArg.ValidateIndirectCallTarget => "cfg tgt",
        WellKnownArg.DispatchIndirectCallTarget => "cfg tgt",
        WellKnownArg.SwiftError => "swift error",
        WellKnownArg.SwiftSelf => "swift self",
        WellKnownArg.X86TailCallSpecialArg => "tail call",
        WellKnownArg.StackArrayLocal => "&lcl arr",
        WellKnownArg.RuntimeMethodHandle => "meth hnd",
        WellKnownArg.AsyncExecutionContext => "exec ctx",
        WellKnownArg.AsyncSynchronizationContext => "sync ctx",
        WellKnownArg.WasmShadowStackPointer => "wasm sp",
        WellKnownArg.WasmPortableEntryPoint => "wasm pep",
        _ => "",
    };
#endif

    /// <summary>Check if a tree contains a node matching the specified predicate. Descend only into subtrees with the specified flags set on them (can be GTF_EMPTY to descend into all nodes).</summary>
    /// <param name="tree">The tree</param>
    /// <param name="predicate">Predicate that the call must match</param>
    /// <param name="requiredFlagsToDescendIntoTree">Flags that must be set on the node to descend into it (GTF_EMPTY to descend into all nodes)</param>
    /// <returns>Node matching the predicate, or null if no such node was found.</returns>
    public GenTree? gtFindNodeInTree(GenTree tree, Func<GenTree, bool> predicate, GenTreeFlags requiredFlagsToDescendIntoTree)
    {
        if ((tree.Flags & requiredFlagsToDescendIntoTree) != requiredFlagsToDescendIntoTree)
        {
            return null;
        }

        var findNodeVisitor = new FindNodeVisitor(predicate, requiredFlagsToDescendIntoTree);
        _ = findNodeVisitor.WalkTree(ref tree, user: null);
        return findNodeVisitor.Result;
    }

    public GenTree gtFoldExpr(GenTree tree)
    {
        // TODO: Port gtFoldExpr
        return tree;
    }

    public GenTree gtFoldExprCall(GenTreeCall tree)
    {
        // TODO: Port gtFoldExprCall
        return tree;
    }

    /// <summary>Can any side-effects be observed externally, say by a caller method?</summary>
    /// <param name="flags"></param>
    /// <returns></returns>
    /// <remarks>
    ///   <para>For stores, only stores to global memory can be observed externally, whereas simple stores to local variables can not.</para>
    ///   <para>Be careful when using this inside a "try" protected region as the order of stores to local variables would need to be preserved wrt side effects if the variables are alive on entry to the handler region. In such cases, even stores to locals will have to be restricted.</para>
    /// </remarks>
    public static bool GTF_GLOBALLY_VISIBLE_SIDE_EFFECTS(GenTreeFlags flags)
    {
        return ((flags & (GTF_CALL | GTF_EXCEPT)) is not 0) || ((flags & (GTF_ASG | GTF_GLOB_REF)) == (GTF_ASG | GTF_GLOB_REF));
    }

    /// <summary>find class handle for elements of an array of ref types</summary>
    /// <param name="array">array to find handle for</param>
    /// <returns>null if element class handle is unknown, otherwise the class handle.</returns>
    public unsafe CORINFO_CLASS_HANDLE gtGetArrayElementClassHandle(GenTree array)
    {
        var arrayClassHnd = gtGetClassHandle(array, out var isArrayExact, out var isArrayNonNull);

        if (arrayClassHnd is not null)
        {
            // We know the class of the reference
            var attribs = info.compCompHnd->getClassAttribs(arrayClassHnd);

            if ((attribs & CORINFO_FLG_ARRAY) != 0)
            {
                // We know for sure it is an array
                CORINFO_CLASS_HANDLE elemClassHnd;
                var arrayElemType = info.compCompHnd->getChildType(arrayClassHnd, &elemClassHnd);

                if (arrayElemType == CORINFO_TYPE_CLASS)
                {
                    // We know it is an array of ref types
                    return elemClassHnd;
                }
            }
        }
        return null;
    }

    /// <summary>find class handle for a ref type</summary>
    /// <param name="tree">tree to find handle for</param>
    /// <param name="isExact">whether handle is exact type</param>
    /// <param name="isNonNull">whether tree value is known not to be null</param>
    /// <returns>The class handle or <c>null</c> if it is unknown</returns>
    public unsafe CORINFO_CLASS_HANDLE gtGetClassHandle(GenTree tree, out bool isExact, out bool isNonNull)
    {
        // Set default values for our out params.
        isNonNull = false;
        isExact = false;

        // Bail out if the tree is not a ref type.
        if (tree.Type is not TYP_REF)
        {
            return NO_CLASS_HANDLE;
        }

        // Tunnel through commas.
        var obj = tree.EffectiveVal;
        var objOp = obj.Oper;
        var objClass = NO_CLASS_HANDLE;

        switch (objOp)
        {
            case GT_COMMA:
            {
                // gtEffectiveVal above means we shouldn't see commas here.
                NO_WAY("unexpected GT_COMMA");
                break;
            }

            case GT_LCL_VAR:
            {
                // For locals, pick up type info from the local table.
                var objLcl = obj.AsLclVar().LclNum;

                objClass = lvaTable[objLcl].lvClassHnd;
                isExact = lvaTable[objLcl].lvClassIsExact;
                break;
            }

            case GT_CNS_INT:
            {
                var intCon = obj.AsIntCon();

                if (intCon.IsIconHandle(GTF_ICON_OBJ_HDL))
                {
                    objClass = info.compCompHnd->getObjectType((CORINFO_OBJECT_HANDLE)(intCon.IconValue));

                    if (objClass != NO_CLASS_HANDLE)
                    {
                        // if we managed to get a class handle it's definitely not null
                        isNonNull = true;
                        isExact = true;
                    }
                }
                break;
            }

            case GT_RET_EXPR:
            {
                // If we see a RET_EXPR, recurse through to examine the return value expression.
                var retExpr = obj.AsRetExpr().InlineCandidate;
                objClass = gtGetClassHandle(retExpr, out isExact, out isNonNull);
                break;
            }

            case GT_CALL:
            {
                var call = obj.AsCall();

                if (call.IsSpecialIntrinsic())
                {
                    var ni = lookupNamedIntrinsic(call._callMethHnd);

                    if ((ni == NI_System_Array_Clone) || (ni == NI_System_Object_MemberwiseClone))
                    {
                        var thisArg = call.Args.ThisArg;
                        assert(thisArg is not null);

                        objClass = gtGetClassHandle(thisArg.Node, out isExact, out isNonNull);
                        break;
                    }

                    var specialObjClass = impGetSpecialIntrinsicExactReturnType(call);

                    if (specialObjClass is not null)
                    {
                        objClass = specialObjClass;
                        isExact = true;
                        isNonNull = true;
                        break;
                    }
                }

                if (call.IsInlineCandidate && !call.IsGuardedDevirtualizationCandidate)
                {
                    // For inline candidates, we've already cached the return
                    // type class handle in the inline info (for GDV candidates,
                    // this data is valid only for a correct guess, so we cannot
                    // use it).
                    var inlInfo = call.SingleInlineCandidateInfo;
                    assert(inlInfo is not null);

                    // Grab it as our first cut at a return type.
                    assert(inlInfo.methInfo.args.retType == CORINFO_TYPE_CLASS);
                    objClass = inlInfo.methInfo.args.retTypeClass;

                    // If the method is shared, the above may not capture
                    // the most precise return type information (that is,
                    // it may represent a shared return type and as such,
                    // have instances of __Canon). See if we can use the
                    // context to get at something more definite.
                    //
                    // For now, we do this here on demand rather than when
                    // processing the call, but we could/should apply
                    // similar sharpening to the argument and local types
                    // of the inlinee.
                    if (eeIsSharedInst(objClass))
                    {
                        var context = inlInfo.exactContextHandle;

                        if (context is not null)
                        {
                            var exactClass = eeGetClassFromContext(context);

                            // Grab the signature in this context.
                            eeGetMethodSig(call._callMethHnd, out var sigInfo, exactClass);
                            assert(sigInfo.retType == CORINFO_TYPE_CLASS);
                            objClass = sigInfo.retTypeClass;
                        }
                    }
                }
                else if (call._callType == CT_USER_FUNC)
                {
                    // For user calls, we can fetch the approximate return
                    // type info from the method handle. Unfortunately
                    // we've lost the exact context, so this is the best
                    // we can do for now.

                    var method = call._callMethHnd;
                    eeGetMethodSig(method, out var sigInfo, owner: null);

                    if (sigInfo.retType == CORINFO_TYPE_VOID)
                    {
                        // This is a constructor call.
                        var methodFlags = info.compCompHnd->getMethodAttribs(method);
                        assert((methodFlags & CORINFO_FLG_CONSTRUCTOR) != 0);
                        objClass = info.compCompHnd->getMethodClass(method);
                        isExact = true;
                        isNonNull = true;
                    }
                    else
                    {
                        assert(sigInfo.retType == CORINFO_TYPE_CLASS);
                        objClass = sigInfo.retTypeClass;
                    }
                }
                else if (call.IsHelperCall())
                {
                    objClass = gtGetHelperCallClassHandle(call, out isExact, out isNonNull);
                }

                break;
            }

            case GT_INTRINSIC:
            {
                var intrinsic = obj.AsIntrinsic();

                if (intrinsic.IntrinsicName == NI_System_Object_GetType)
                {
                    var runtimeType = info.compCompHnd->getBuiltinClass(CLASSID_RUNTIME_TYPE);
                    assert(runtimeType != NO_CLASS_HANDLE);

                    objClass = runtimeType;
                    isNonNull = true;
                }
                break;
            }

            case GT_CNS_STR:
            {
                // For literal strings, we know the class and that the value is not null.
                objClass = impStringClass;
                isExact = true;
                isNonNull = true;
                break;
            }

            case GT_IND:
            {
                var indir = obj.AsIndir();

                var indirBase = indir.Base;
                assert(indirBase is not null);

                // indir(lcl_var_addr) -. lcl
                //
                // This comes up during constrained callvirt on ref types.
                //
                if (indirBase.IsLclVarAddr)
                {
                    var objLcl = indirBase.AsLclVarCommon().LclNum;
                    ref var lvaDsc = ref lvaTable[objLcl];

                    objClass = lvaDsc.lvClassHnd;
                    isExact = lvaDsc.lvClassIsExact;
                }
                else if (indirBase.Oper is GT_INDEX_ADDR or GT_ARR_ELEM)
                {
                    // indir(arr_elem(...)) . array element type

                    if (indirBase.Oper is GT_INDEX_ADDR)
                    {
                        objClass = gtGetArrayElementClassHandle(indirBase.AsIndexAddr().Arr);
                    }
                    else
                    {
                        objClass = gtGetArrayElementClassHandle(indirBase.AsArrElem().ArrObj);
                    }
                }
                else if (indirBase.Oper is GT_ADD)
                {
                    // TODO-VNTypes: use "IsFieldAddr" here instead.

                    // This could be a static field access.
                    //
                    // See if op1 is a static field base helper call
                    // and if so, op2 will have the field info.

                    var indirBaseOp = indirBase.AsOp();

                    var op1 = indirBaseOp.Op1;
                    var op2 = indirBaseOp.Op2;

                    if (op2.Oper.IsCnsIntOrI)
                    {
                        var intCon = op2.AsIntCon();
                        var fieldSeq = intCon.FieldSeq;

                        if ((fieldSeq is not null) && (fieldSeq.Offset == intCon.IconValue))
                        {
                            // No benefit to calling gtGetFieldClassHandle here, as
                            // the exact field being accessed can vary.
                            var fieldHnd = fieldSeq.FieldHandle;
                            var fieldOwner = NO_CLASS_HANDLE;

                            // fieldOwner helps us to get a more exact field class for instance fields
                            if (!fieldSeq.IsStaticField)
                            {
                                fieldOwner = gtGetClassHandle(op1, out var objIsExact, out var objIsNonNull);
                            }

                            if (eeGetFieldType(fieldHnd, out var fieldClass, fieldOwner) == TYP_REF)
                            {
                                objClass = fieldClass;
                            }
                        }
                    }
                }
                else if (indirBase.Oper.IsCnsIntOrI)
                {
                    var intCon = indirBase.AsIntCon();

                    if (intCon.IsIconHandle(GTF_ICON_CONST_PTR) || intCon.IsIconHandle(GTF_ICON_STATIC_HDL))
                    {
                        // Check if we have IND(ICON_HANDLE) that represents a static field
                        var fldSeq = intCon.FieldSeq;

                        if ((fldSeq is not null) && (fldSeq.Offset == intCon.IconValue))
                        {
                            var fldHandle = fldSeq.FieldHandle;
                            objClass = gtGetFieldClassHandle(fldHandle, out isExact, out isNonNull);
                        }
                    }
                }
                else if (indirBase.Oper is GT_FIELD_ADDR)
                {
                    objClass = gtGetFieldClassHandle(indirBase.AsFieldAddr().FldHnd, out isExact, out isNonNull);
                }
                break;
            }

            case GT_BOX:
            {
                // Box should just wrap a local var reference which has
                // the type we're looking for. Also box only represents a
                // non-nullable value type so result cannot be null.
                var box = obj.AsBox();

                var boxTemp = box.BoxOp;
                assert(boxTemp.Oper.IsLocal);

                var boxTempLcl = boxTemp.AsLclVar().LclNum;
                ref var lvaDsc = ref lvaTable[boxTempLcl];
                objClass = lvaDsc.lvClassHnd;
                isExact = lvaDsc.lvClassIsExact;
                isNonNull = true;
                break;
            }

            default:
            {
                break;
            }
        }

        if ((objClass == NO_CLASS_HANDLE) && (vnStore is not null))
        {
            // Try VN if we haven't found a class handle yet
            objClass = vnStore.GetObjectType(tree._vnPair.Conservative, out isExact, out isNonNull);
        }

        if ((objClass != NO_CLASS_HANDLE) && !isExact && (JitConfig[ConfigInteger.JitEnableExactDevirtualization] != 0))
        {
            CORINFO_CLASS_HANDLE exactClass;

            if (info.compCompHnd->getExactClasses(objClass, 1, &exactClass) == 1)
            {
                isExact = true;
                objClass = exactClass;
            }
            else
            {
                isExact = info.compCompHnd->isExactType(objClass);
            }
        }
        return objClass;
    }

    /// <summary>find class handle for a field</summary>
    /// <param name="fieldHnd">field handle for field in question</param>
    /// <param name="isExact">true if type is known exactly</param>
    /// <param name="isNonNull">true if field value is not null</param>
    /// <returns>null if helper call result is not a ref class, or the class handle is unknown, otherwise the class handle.</returns>
    /// <remarks>May examine runtime state of static field instances.</remarks>
    public unsafe CORINFO_CLASS_HANDLE gtGetFieldClassHandle(CORINFO_FIELD_HANDLE fieldHnd, out bool isExact, out bool isNonNull)
    {
        isExact = false;
        isNonNull = false;

        var fieldClass = NO_CLASS_HANDLE;
        var fieldCorType = info.compCompHnd->getFieldType(fieldHnd, &fieldClass);

        if (fieldCorType == CORINFO_TYPE_CLASS)
        {
            // Optionally, look at the actual type of the field's value
            var queryForCurrentClass = true;

#if DEBUG
            queryForCurrentClass = JitConfig[ConfigInteger.JitQueryCurrentStaticFieldClass] > 0;
#endif

            if (queryForCurrentClass)
            {
#if DEBUG
                if (verbose || (JitConfig[ConfigInteger.EnableExtraSuperPmiQueries] != 0))
                {
                    JITDUMP($"\nQuerying runtime about current class of field {eeGetFieldName(fieldHnd, true)} (declared as {eeGetClassName(fieldClass)})\n");
                }
#endif

                // Is this a fully initialized init-only static field?
                //
                // Note we're not asking for speculative results here, yet.
                var currentClass = info.compCompHnd->getStaticFieldCurrentClass(fieldHnd);

                if (currentClass != NO_CLASS_HANDLE)
                {
                    // Yes! We know the class exactly and can rely on this to always be true.
                    fieldClass = currentClass;

                    isExact = true;
                    isNonNull = true;

#if DEBUG
                    if (verbose || (JitConfig[ConfigInteger.EnableExtraSuperPmiQueries] != 0))
                    {
                        JITDUMP($"Runtime reports field is init-only and initialized and has class {eeGetClassName(fieldClass)}\n");
                    }
#endif
                }
                else
                {
                    JITDUMP("Field's current class not available\n");
                }
                return fieldClass;
            }
        }
        return NO_CLASS_HANDLE;
    }

    /// <summary>find the compile time class handle from a helper call argument tree</summary>
    /// <param name="tree">tree that passes the handle to the helper</param>
    /// <returns>The compile time class handle if known.</returns>
    public unsafe CORINFO_CLASS_HANDLE gtGetHelperArgClassHandle(GenTree tree)
    {
        var result = NO_CLASS_HANDLE;

        if (tree.Oper.IsCnsIntOrI && (tree.Type is TYP_I_IMPL))
        {
            // The handle could be a literal constant
            var intCon = tree.AsIntCon();

            assert(intCon.IsIconHandle(GTF_ICON_CLASS_HDL));
            result = (CORINFO_CLASS_HANDLE)(intCon.CompileTimeHandle);
        }
        else if (tree.Oper is GT_RUNTIMELOOKUP)
        {
            // Or the result of a runtime lookup
            result = tree.AsRuntimeLookup().ClassHandle;
        }
        else if (tree.Oper is GT_IND)
        {
            // Or something reached indirectly

            // The handle indirs we are looking for will be marked as non-faulting.
            // Certain others (eg from refanytype) may not be.
            if ((tree.Flags & GTF_IND_NONFAULTING) != 0)
            {
                var handleTreeInternal = tree.AsUnOp().Op1;

                if (handleTreeInternal.Oper.IsCnsIntOrI && (handleTreeInternal.Type is TYP_I_IMPL))
                {
                    // These handle constants should be class handles.
                    var intCon = handleTreeInternal.AsIntCon();

                    assert(intCon.IsIconHandle(GTF_ICON_CLASS_HDL));
                    result = (CORINFO_CLASS_HANDLE)(intCon.CompileTimeHandle);
                }
            }
        }

        return result;
    }

    /// <summary>find the compile time method handle from a helper call argument tree</summary>
    /// <param name="tree">tree that passes the handle to the helper</param>
    /// <returns>The compile time method handle, if known.</returns>
    public unsafe CORINFO_METHOD_HANDLE gtGetHelperArgMethodHandle(GenTree tree)
    {
        var result = NO_METHOD_HANDLE;

        // The handle could be a literal constant
        if (tree.Oper.IsCnsIntOrI && (tree.Type is TYP_I_IMPL))
        {
            var intCon = tree.AsIntCon();
            assert(intCon.IsIconHandle(GTF_ICON_METHOD_HDL));
            result = (CORINFO_METHOD_HANDLE)(intCon.CompileTimeHandle);
        }
        // Or the result of a runtime lookup
        else if (tree.Oper is GT_RUNTIMELOOKUP)
        {
            result = tree.AsRuntimeLookup().MethodHandle;
        }
        // Or something reached indirectly
        else if (tree.Oper is GT_IND)
        {
            // The handle indirs we are looking for will be marked as non-faulting.
            // Certain others (eg from refanytype) may not be.
            if ((tree.Flags & GTF_IND_NONFAULTING) != 0)
            {
                var handleTreeInternal = tree.AsUnOp().Op1;

                if (handleTreeInternal.Oper.IsCnsIntOrI && (handleTreeInternal.Type is TYP_I_IMPL))
                {
                    // These handle constants should be method handles.
                    var intCon = handleTreeInternal.AsIntCon();

                    assert(intCon.IsIconHandle(GTF_ICON_METHOD_HDL));
                    result = (CORINFO_METHOD_HANDLE)(intCon.CompileTimeHandle);
                }
            }
        }

        return result;
    }

    /// <summary>find class handle for return value of a helper call</summary>
    /// <param name="call">helper call to examine</param>
    /// <param name="isExact">true if type is known exactly</param>
    /// <param name="isNonNull">true if return value is not null</param>
    /// <returns>null if helper call result is not a ref class, or the class handle is unknown, otherwise the class handle.</returns>
    public unsafe CORINFO_CLASS_HANDLE gtGetHelperCallClassHandle(GenTreeCall call, out bool isExact, out bool isNonNull)
    {
        assert(call.IsHelperCall());

        isNonNull = false;
        isExact = false;

        CORINFO_CLASS_HANDLE objClass = null;
        var helper = eeGetHelperNum(call._callMethHnd);

        switch (helper)
        {
            case CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPE:
            case CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPE_MAYBENULL:
            {
                // Note for some runtimes these helpers return exact types.
                // But in those cases the types are also sealed, so there's no need to claim exactness here.

                var runtimeType = info.compCompHnd->getBuiltinClass(CLASSID_RUNTIME_TYPE);
                assert(runtimeType != NO_CLASS_HANDLE);

                objClass = runtimeType;
                isNonNull = helper is CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPE;
                break;
            }

            case CORINFO_HELP_BOX:
            case CORINFO_HELP_BOX_NULLABLE:
            {
                var arg = call.Args.GetUserArgByIndex(0);
                assert(arg is not null);

                var typeArg = arg.Node;

                if (typeArg.Oper.IsCnsIntOrI)
                {
                    var intCon = typeArg.AsIntCon();

                    if (intCon.IsIconHandle(GTF_ICON_CLASS_HDL))
                    {
                        var isNullableHelper = helper is CORINFO_HELP_BOX_NULLABLE;
                        objClass = gtGetHelperArgClassHandle(typeArg);

                        if ((objClass != NO_CLASS_HANDLE) && isNullableHelper)
                        {
                            // Nullable<T> is boxed as just T (via CORINFO_HELP_BOX_NULLABLE)
                            objClass = info.compCompHnd->getTypeForBox(objClass);
                        }

                        if (objClass != NO_CLASS_HANDLE)
                        {
                            // CORINFO_HELP_BOX_NULLABLE may return null
                            // CORINFO_HELP_BOX always returns non-null
                            isNonNull = !isNullableHelper;
                            isExact = true;
                        }
                    }
                }
                break;
            }

            case CORINFO_HELP_CHKCASTCLASS:
            case CORINFO_HELP_CHKCASTANY:
            case CORINFO_HELP_CHKCASTARRAY:
            case CORINFO_HELP_CHKCASTINTERFACE:
            case CORINFO_HELP_CHKCASTCLASS_SPECIAL:
            case CORINFO_HELP_ISINSTANCEOFINTERFACE:
            case CORINFO_HELP_ISINSTANCEOFARRAY:
            case CORINFO_HELP_ISINSTANCEOFCLASS:
            case CORINFO_HELP_ISINSTANCEOFANY:
            {
                // Fetch the class handle from the helper call arglist
                var arg = call.Args.GetArgByIndex(0);
                assert(arg is not null);

                var typeArg = arg.Node;
                var castHnd = gtGetHelperArgClassHandle(typeArg);

                // We generally assume the type being cast to is the best type
                // for the result, unless it is an interface type.
                //
                // TODO-CQ: when we have default interface methods then
                // this might not be the best assumption. A similar issue arises when
                // typing the temp in impCastClassOrIsInstToTree, when we
                // expand the cast inline.
                if (castHnd is not null)
                {
                    var attrs = info.compCompHnd->getClassAttribs(castHnd);

                    if ((attrs & CORINFO_FLG_INTERFACE) != 0)
                    {
                        castHnd = null;
                    }
                }

                // If we don't have a good estimate for the type we can use the
                // type from the value being cast instead.
                if (castHnd is null)
                {
                    var valueArg = call.Args.GetArgByIndex(1);
                    assert(valueArg is not null);

                    var valueNode = valueArg.Node;
                    castHnd = gtGetClassHandle(valueNode, out isExact, out isNonNull);
                }

                // We don't know at jit time if the cast will succeed or fail, but if it
                // fails at runtime then an exception is thrown for cast helpers, or the
                // result is set null for instance helpers.
                //
                // So it safe to claim the result has the cast type.
                // Note we don't know for sure that it is exactly this type.
                if (castHnd is not null)
                {
                    objClass = castHnd;
                }
                break;
            }

            case CORINFO_HELP_NEWARR_1_DIRECT:
            case CORINFO_HELP_NEWARR_1_MAYBEFROZEN:
            case CORINFO_HELP_NEWARR_1_PTR:
            case CORINFO_HELP_NEWARR_1_VC:
            case CORINFO_HELP_NEWARR_1_ALIGN8:
            case CORINFO_HELP_READYTORUN_NEWARR_1:
            {
                var arrayHnd = (CORINFO_CLASS_HANDLE)(call._compileTimeHelperArgumentHandle);

                if (arrayHnd != NO_CLASS_HANDLE)
                {
                    objClass = arrayHnd;
                    isExact = true;
                    isNonNull = true;
                }
                break;
            }

            default:
            {
                break;
            }
        }

        return objClass;
    }

#if DEBUG
    /// <summary>Get the local var name</summary>
    /// <param name="lclNum"></param>
    /// <returns></returns>
    public string gtGetLclVarName(int lclNum)
    {
        gtGetLclVarNameInfo(lclNum, out var ilKind, out var ilName, out var ilNum);

        if (ilName.Length != 0)
        {
            return $"V{lclNum:D2} {ilName}";
        }
        else if (ilKind.Length != 0)
        {
            return $"V{lclNum:D2} {ilKind}{ilNum}";
        }
        else
        {
            return $"V{lclNum:D2}";
        }
    }

    public void gtGetLclVarNameInfo(int lclNum, out string ilKind, out string ilName, out int ilNum)
    {
        var kind = "";
        var name = "";

        var num = compMap2ILvarNum(lclNum);

        if (num == ICorDebugInfo.RETBUF_ILNUM)
        {
            name = "RetBuf";
        }
        else if (num == ICorDebugInfo.VARARGS_HND_ILNUM)
        {
            name = "VarArgHandle";
        }
        else if (num == ICorDebugInfo.TYPECTXT_ILNUM)
        {
            name = "TypeCtx";
        }
        else if (num == ICorDebugInfo.UNKNOWN_ILNUM)
        {
            if (lclNumIsTrueCSE(lclNum))
            {
                kind = "cse";
                num  = lclNum - optCSEstart;
            }
#if TARGET_ARM64
            else if (lclNum == lvaFfrRegister)
            {
                // We introduce this LclVar in lowering, hence special case the printing of
                // it instead of handling it in "rationalizer" below.
                ilName = "FFReg";
            }
#endif
            else if (lclNum >= optCSEstart)
            {
                // Currently any new LclVar's introduced after the CSE phase
                // are believed to be created by the "rationalizer" that is what is meant by the "rat" prefix.
                kind = "rat";
                num  = lclNum - (optCSEstart + optCSEcount);
            }
            else if (lclNum == info.compLvFrameListRoot)
            {
                name = "FramesRoot";
            }
            else if (lclNum == lvaInlinedPInvokeFrameVar)
            {
                name = "PInvokeFrame";
            }
            else if (lclNum == lvaGSSecurityCookie)
            {
                name = "GsCookie";
            }
            else if (lclNum == lvaRetAddrVar)
            {
                name = "ReturnAddress";
            }
#if FEATURE_FIXED_OUT_ARGS
            else if (lclNum == lvaOutgoingArgSpaceVar)
            {
                name = "OutArgs";
            }
#endif
#if JIT32_GCENCODER
            else if (lclNum == lvaLocAllocSPvar)
            {
                ilName = "LocAllocSP";
            }
#endif
            else if (lclNum == lvaAsyncContinuationArg)
            {
                name = "AsyncCont";
            }
#if TARGET_WASM
            else if (lclNum == lvaWasmSpArg)
            {
                ilName = "SP";
            }
#endif
            else
            {
                kind = "tmp";

                if (compIsForInlining)
                {
                    num = lclNum - impInlineInfo.InlinerCompiler.info.compLocalsCount;
                }
                else
                {
                    num = lclNum - info.compLocalsCount;
                }
            }
        }
        else if (lclNum < (compIsForInlining ? impInlineInfo.InlinerCompiler.info.compArgsCount : info.compArgsCount))
        {
            if ((num is 0) && !info.compIsStatic)
            {
                name = "this";
            }
            else
            {
                kind = "arg";
            }
        }
        else
        {
            if (!lvaTable[lclNum].lvIsStructField)
            {
                kind = "loc";
            }
            if (compIsForInlining)
            {
                num -= impInlineInfo.InlinerCompiler.info.compILargsCount;
            }
            else
            {
                num -= info.compILargsCount;
            }
        }

        ilKind = kind;
        ilName = name;
        ilNum  = num;
    }
#endif

    public static var_types gtGetTypeForIconFlags(GenTreeFlags flags) => (flags == GTF_ICON_OBJ_HDL) ? TYP_REF : TYP_I_IMPL;

    public bool gtHasCatchArg(GenTree tree)
    {
        if ((tree.Flags & GTF_ORDER_SIDEEFF) is not 0)
        {
            var visitor = new FindCatchArgVisitor();

            if (visitor.WalkTree(ref tree, user: null) == WALK_ABORT)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Check if this tree contains locals with lvHasLdAddrOp or IsAddressExposed flags set. Does a full tree walk.</summary>
    /// <param name="tree">The tree</param>
    /// <returns>True if any sub tree is such a local.</returns>
    public bool gtHasLocalsWithAddrOp(GenTree tree)
    {
        var visitor = new LocalsWithAddrOpVisitor(this);
        return visitor.WalkTree(ref tree, user: null) == WALK_ABORT;
    }

    /// <summary>Find out whether the given tree contains a local.</summary>
    /// <param name="tree">tree to find the local in</param>
    /// <param name="lclNum">the local's number</param>
    /// <returns>Whether "tree" has any local nodes that refer to the local.</returns>
    public static bool gtHasRef(GenTree? tree, int lclNum)
    {
        if (tree is null)
        {
            return false;
        }

        var oper = tree.Oper;

        if (oper.IsLeaf)
        {
            if (oper.IsAnyLocal && (tree.AsLclVarCommon().LclNum == lclNum))
            {
                return true;
            }

            if (oper is GT_RET_EXPR)
            {
                return gtHasRef(tree.AsRetExpr().InlineCandidate, lclNum);
            }
            return false;
        }

        if (oper.IsUnary)
        {
            if (oper.IsLocalStore && (tree.AsLclVarCommon().LclNum == lclNum))
            {
                return true;
            }
            return gtHasRef(tree.AsUnOp().Op1, lclNum);
        }

        if (oper.IsBinary)
        {
            var op = tree.AsOp();
            return gtHasRef(op.Op1, lclNum) || gtHasRef(op.Op2, lclNum);
        }

        var result = false;

        _ = tree.VisitOperands((operand) => {
            if (gtHasRef(operand, lclNum))
            {
                result = true;
                return GenTree.VisitResult.Abort;
            }
            return GenTree.VisitResult.Continue;
        });
        return result;
    }

    /// <summary>Initialize an indirection node.</summary>
    /// <param name="indir">The indirection node</param>
    /// <param name="indirFlags">The indirection flags</param>
    /// <remarks>Sets side effect flags based on the passed-in indirection flags.</remarks>
    public void gtInitializeIndirNode(GenTreeIndir indir, GenTreeFlags indirFlags)
    {
        assert(varTypeIsI(indir.Addr.Type.ActualType));
        assert((indirFlags & ~GTF_IND_FLAGS) == GTF_EMPTY);

        indir.Flags |= indirFlags;
        indir.SetIndirExceptionFlags(this);

        if ((indirFlags & GTF_IND_INVARIANT) is 0)
        {
            indir.Flags |= GTF_GLOB_REF;
        }

        if ((indirFlags & GTF_IND_VOLATILE) is not 0)
        {
            indir.HasOrderingSideEffect = true;
        }
    }

    /// <summary>Initialize a store node.</summary>
    /// <param name="store">The store node</param>
    /// <param name="value">The value to store</param>
    /// <remarks>Common initialization for all STORE nodes. Marks simd locals as "used in a HW intrinsic".</remarks>
    public void gtInitializeStoreNode(GenTree store, GenTree value)
    {
        // TODO-ASG: add asserts that the types match here.
        assert(store.Data == value);

#if FEATURE_SIMD
        if (varTypeIsSimdOrMask(value.Type))
        {
            // TODO-ASG: delete this zero-diff quirk.
            if (!value.Oper.IsCall || !value.AsCall().ShouldHaveRetBufArg)
            {
                // We want to track simd stores as being intrinsics since they are
                // functionally simd `mov` instructions and are more efficient when
                // we don't promote, particularly when it occurs due to inlining.
                SetOpLclRelatedToSimdIntrinsic(store);
                SetOpLclRelatedToSimdIntrinsic(value);
            }
        }
#endif
    }

    private bool gtIsAsyncCall(GenTree tree)
    {
        if (tree.Oper.IsCall && tree.AsCall().IsAsync)
        {
            return true;
        }

        if ((tree.Oper is GT_RET_EXPR) && gtTreeContainsAsyncCall(tree.AsRetExpr().InlineCandidate))
        {
            return true;
        }
        return false;
    }

    // Return true if call is a recursive call; return false otherwise.
    // Note when inlining, this looks for calls back to the root method.
    public unsafe bool gtIsRecursiveCall(GenTreeCall call, bool useInlineRoot = true)
    {
        return (call._callType is not CT_INDIRECT) && gtIsRecursiveCall(call._callMethHnd, useInlineRoot);
    }

    public unsafe bool gtIsRecursiveCall(CORINFO_METHOD_HANDLE callMethodHandle, bool useInlineRoot = true)
    {
        return callMethodHandle == (useInlineRoot ? impInlineRoot.info.compMethodHnd : info.compMethodHnd);
    }

    private static bool gtIsTailCall(GenTree tree)
    {
        if (tree.Oper.IsCall)
        {
            var call = tree.AsCall();
            return call.CanTailCall || call.IsTailCall;
        }
        return false;
    }

    /// <summary>see if tree is constructing a RuntimeType from a handle </summary>
    /// <param name="call">tree to examine</param>
    /// <returns>True if so</returns>
    public bool gtIsTypeHandleToRuntimeTypeHelper(GenTreeCall call)
    {
        return call.IsHelperCall(CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPE) ||
               call.IsHelperCall(CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPE_MAYBENULL);
    }

    public bool gtIsTypeHandleToRuntimeTypeHandleHelper(GenTreeCall call)
        => gtIsTypeHandleToRuntimeTypeHandleHelper(call, out _);

    /// <summary>see if tree is constructing a RuntimeTypeHandle from a handle</summary>
    /// <param name="call">tree to examine</param>
    /// <param name="helper">optional pointer to a variable that receives the type of the helper</param>
    /// <returns>True if so</returns>
    public bool gtIsTypeHandleToRuntimeTypeHandleHelper(GenTreeCall call, out CorInfoHelpFunc helper)
    {
        if (call.IsHelperCall(CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPEHANDLE))
        {
            helper = CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPEHANDLE;
            return true;
        }
        else if (call.IsHelperCall(CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPEHANDLE_MAYBENULL))
        {
            helper = CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPEHANDLE_MAYBENULL;
            return true;
        }
        else
        {
            helper = CORINFO_HELP_UNDEF;
            return false;
        }
    }

    /// <summary>A little helper to create an object allocation node.</summary>
    /// <param name="type">Tree return type (e.g. TYP_REF)</param>
    /// <param name="op1">Node containing an address of VtablePtr</param>
    /// <param name="newHelper">Value returned by ICorJitInfo.getNewHelper</param>
    /// <param name="newHelperHasSideEffects">True iff allocation helper has side effects</param>
    /// <param name="clsHnd">Corresponding class handle</param>
    /// <returns>Returns GT_ALLOCOBJ node that will be later morphed into an allocation helper call or local variable allocation on the stack.</returns>
    public unsafe GenTreeAllocObj gtNewAllocObjNode(var_types type, GenTree op1, CorInfoHelpFunc newHelper, bool newHelperHasSideEffects, CORINFO_CLASS_HANDLE clsHnd)
    {
        return new GenTreeAllocObj(type, op1, newHelper, newHelperHasSideEffects, clsHnd);
    }

    /// <summary>Helper to create an object allocation node.</summary>
    /// <param name="resolvedToken">Resolved token for the object being allocated</param>
    /// <param name="callerHandle"></param>
    /// <param name="useParent">true iff the token represents a child of the object's class</param>
    /// <returns>Returns GT_ALLOCOBJ node that will be later morphed into an allocation helper call or local variable allocation on the stack.</returns>
    /// <remarks>Node creation can fail for inlinees when the type described by pResolvedToken can't be represented in jitted code. If this happens, this method will return null.</remarks>
    public unsafe GenTreeAllocObj? gtNewAllocObjNode(in CORINFO_RESOLVED_TOKEN resolvedToken, CORINFO_METHOD_HANDLE callerHandle, bool useParent)
    {
        var usingReadyToRunHelper = false;
        var helper = CORINFO_HELP_UNDEF;
        var opHandle = impTokenToHandle(resolvedToken, mustRestoreHandle: true, useParent);

#if FEATURE_READYTORUN
        var lookup = new CORINFO_CONST_LOOKUP();

        if (IsAot)
        {
            helper = CORINFO_HELP_READYTORUN_NEW;

            fixed (CORINFO_RESOLVED_TOKEN* pResolvedToken = &resolvedToken)
            {
                usingReadyToRunHelper = info.compCompHnd->getReadyToRunHelper(pResolvedToken, helper, callerHandle, &lookup);
            }
        }
#endif

        if (!usingReadyToRunHelper && (opHandle is null))
        {
            // We must be backing out of an inline.
            assert(compDonotInline);
            return null;
        }

        bool helperHasSideEffects;
        var helperTemp = info.compCompHnd->getNewHelper(resolvedToken.hClass, &helperHasSideEffects);

        if (!usingReadyToRunHelper)
        {
            helper = helperTemp;
        }

        assert(opHandle is not null);

        // TODO: ReadyToRun: When generic dictionary lookups are necessary, replace the lookup call
        // and the newfast call with a single call to a dynamic R2R cell that will:
        //      1) Load the context
        //      2) Perform the generic dictionary lookup and caching, and generate the appropriate stub
        //      3) Allocate and return the new object for boxing
        // Reason: performance (today, we'll always use the slow helper for the R2R generics case)

        var allocObj = gtNewAllocObjNode(TYP_REF, opHandle, helper, helperHasSideEffects, resolvedToken.hClass);

#if FEATURE_READYTORUN
        if (usingReadyToRunHelper)
        {
            assert(lookup.addr is not null);
            allocObj.EntryPoint = lookup;
        }
#endif

        return allocObj;
    }

    public unsafe GenTreeIndexAddr gtNewArrayIndexAddr(GenTree arrayOp, GenTree indexOp, var_types elemType, CORINFO_CLASS_HANDLE elemClassHandle)
    {
        return gtNewIndexAddr(arrayOp, indexOp, elemType, elemClassHandle, OFFSETOF__CORINFO_Array__data, OFFSETOF__CORINFO_Array__length);
    }

    /// <summary>Helper to create an array length node.</summary>
    /// <param name="typ">Type of the node</param>
    /// <param name="arrayOp">Array node</param>
    /// <param name="lenOffset">Offset of the length field</param>
    /// <returns>New GT_ARR_LENGTH node</returns>
    public GenTreeArrLen gtNewArrLen(var_types typ, GenTree arrayOp, int lenOffset)
    {
        // Unlike MD arrays, this is not set in the importer
        optMethodFlags |= OMF_HAS_ARRAYREF;

        var arrLen = new GenTreeArrLen(typ, arrayOp, lenOffset);
        arrLen.SetIndirExceptionFlags(this);
        return arrLen;
    }

    public GenTreeOp gtNewBinaryNode(genTreeOps oper, var_types type, GenTree op1, GenTree op2)
    {
        return new GenTreeOp(oper, type, op1, op2);
    }

    /// <summary>Create a struct indirection node.</summary>
    /// <param name="addr">Address of the indirection</param>
    /// <param name="layout">The struct layout</param>
    /// <param name="indirFlags">Indirection flags</param>
    /// <returns>The created GT_BLK node.</returns>
    public GenTreeBlk gtNewBlkIndir(GenTree addr, ClassLayout layout, GenTreeFlags indirFlags = GTF_EMPTY)
    {
        assert(layout.Type == TYP_STRUCT);
        var blkNode = new GenTreeBlk(TYP_STRUCT, addr, layout);

        gtInitializeIndirNode(blkNode, indirFlags);
        return blkNode;
    }

    public unsafe GenTreeCall gtNewCallNode(var_types type, gtCallTypes callType, CORINFO_METHOD_HANDLE callHnd, in DebugInfo di = default)
    {
        var node = new GenTreeCall(type.ActualType) {
            _callType = callType,
            _returnType = type,
            _callMethHnd = callHnd,
#if DEBUG
            _rawILOffset = BAD_IL_OFFSET,
#endif
            _inlineContext = compInlineContext,
        };
        node.Flags |= (GTF_CALL | GTF_GLOB_REF);

#if UNIX_X86_ABI
        if (callType is CT_INDIRECT or CT_HELPER)
        {
            node.Flags |= GTF_CALL_POP_ARGS;
        }
#endif

        if (callType is not CT_INDIRECT)
        {            
            node.ClearInlineInfo();
        }

        // Spec: Managed Retval sequence points needs to be generated while generating debug info for debuggable code.
        //
        // Implementation note: if not generating MRV info genCallSite2ILOffsetMap will be NULL and
        // codegen will pass DebugInfo() to emitter, which will cause emitter
        // not to emit IP mapping entry.
        if (opts.compDbgCode && opts.compDbgInfo && di.IsValid)
        {
            // Managed Retval - IL offset of the call.  This offset is used to emit a
            // CALL_INSTRUCTION type sequence point while emitting corresponding native call.
            //
            // TODO-Cleanup:
            // a) (Opt) We need not store this offset if the method doesn't return a
            // value.  Rather it can be made BAD_IL_OFFSET to prevent a sequence
            // point being emitted.
            //
            // b) (Opt) Add new sequence points only if requested by debugger through
            // a new boundary type - ICorDebugInfo.BoundaryTypes

            genCallSite2DebugInfoMap ??= [];
            genCallSite2DebugInfoMap.Add(node, di);
        }

        // Initialize gtOtherRegs
        node.ClearOtherRegs();

#if !TARGET_64BIT
        if (varTypeIsLong(node.Type))
        {
            // Initialize Return type descriptor of call node
            assert(node._returnType == node.Type);
            node.InitializeLongReturnType();
        }
#endif

        return node;
    }

    public GenTreeCast gtNewCastNode(var_types type, GenTree op, bool fromUnsigned, var_types castType)
    {
        return new GenTreeCast(type, op, fromUnsigned, castType);
    }

    public GenTreeColon gtNewColonNode(var_types type, GenTree thenNode, GenTree elseNode)
    {
        return new GenTreeColon(type, thenNode, elseNode);
    }

    public GenTreeOp gtNewCommaNode(var_types type, GenTree op1, GenTree op2)
    {
        return new GenTreeOp(GT_COMMA, type, op1, op2);
    }

    public GenTreeDblCon gtNewDconNode(var_types type, double value)
    {
        return new GenTreeDblCon(type, value);
    }

    public GenTreeIntCon gtNewFalse()
    {
        return gtNewIconNode(TYP_INT, 0);
    }

    public unsafe GenTreeFieldAddr gtNewFieldAddrNode(var_types type, CORINFO_FIELD_HANDLE fldHnd, int offset = 0)
    {
        return gtNewFieldAddrNode(type, obj: null, fldHnd, offset);
    }

    /// <summary>Create a new GT_FIELD_ADDR node.</summary>
    /// <param name="type">type for the address node</param>
    /// <param name="fldHnd">the field handle</param>
    /// <param name="obj">the instance, an address</param>
    /// <param name="offset">the field offset</param>
    /// <returns>The created node.</returns>
    public unsafe GenTreeFieldAddr gtNewFieldAddrNode(var_types type, GenTree? obj, CORINFO_FIELD_HANDLE fldHnd, int offset = 0)
    {
        // TODO-ADDR: consider creating a variant of this which would skip various
        // no-op constructs (such as struct fields with zero offsets), and fold
        // others (LCL_VAR_ADDR + FIELD_ADDR => LCL_FLD_ADDR).

        assert(varTypeIsI(type.ActualType));
        var fieldAddr = new GenTreeFieldAddr(type, obj, fldHnd, offset);

        // If "obj" is the address of a local, note that a field of that struct local has been accessed.
        if ((obj is not null) && obj.IsLclVarAddr)
        {
            ref var varDsc = ref lvaGetDesc(obj.AsLclVarCommon().LclNum);
            varDsc.lvFieldAccessed = true;
        }

        if ((obj is not null) && fgAddrCouldBeNull(obj))
        {
            fieldAddr.Flags |= GTF_EXCEPT;
        }
        return fieldAddr;
    }

    public unsafe GenTreeFieldAddr gtNewFieldAddrNode(GenTree obj, CORINFO_FIELD_HANDLE fldHnd, int offset)
    {
        return gtNewFieldAddrNode(varTypeIsGC(obj.Type) ? TYP_BYREF : TYP_I_IMPL, obj, fldHnd, offset);
    }

    public static unsafe GenTreeFptrVal gtNewFptrValNode(var_types type, CORINFO_METHOD_HANDLE fptrMethod)
    {
        return new GenTreeFptrVal(type, fptrMethod);
    }

    /// <summary>Create an IR node representing a constant value of any type.</summary>
    /// <param name="type">The primitive type. For small types the constant will be zero/sign-extended and a TYP_INT node will be returned.</param>
    /// <param name="cnsVal">Pointer to data</param>
    /// <returns>An IR node representing the constant.</returns>
    public unsafe GenTree gtNewGenericCon(var_types type, ReadOnlySpan<byte> cnsVal)
    {
        switch (type)
        {
            case TYP_BYTE:
            {
                assert(cnsVal.Length == sizeof(sbyte));
                var val = Unsafe.ReadUnaligned<sbyte>(in cnsVal[0]);
                return gtNewIconNode(TYP_INT, val);
            }

            case TYP_UBYTE:
            {
                assert(cnsVal.Length == sizeof(byte));
                var val = Unsafe.ReadUnaligned<byte>(in cnsVal[0]);
                return gtNewIconNode(TYP_INT, val);
            }

            case TYP_SHORT:
            {
                assert(cnsVal.Length == sizeof(short));
                var val = Unsafe.ReadUnaligned<short>(in cnsVal[0]);
                return gtNewIconNode(TYP_INT, val);
            }

            case TYP_USHORT:
            {
                assert(cnsVal.Length == sizeof(ushort));
                var val = Unsafe.ReadUnaligned<ushort>(in cnsVal[0]);
                return gtNewIconNode(TYP_INT, val);
            }

            case TYP_INT:
            {
                assert(cnsVal.Length == sizeof(int));
                var val = Unsafe.ReadUnaligned<int>(in cnsVal[0]);
                return gtNewIconNode(TYP_INT, val);
            }

            case TYP_LONG:
            {
                assert(cnsVal.Length == sizeof(long));
                var val = Unsafe.ReadUnaligned<long>(in cnsVal[0]);
                return gtNewLconNode(val);
            }

            case TYP_FLOAT:
            {
                assert(cnsVal.Length == sizeof(float));
                var val = Unsafe.ReadUnaligned<float>(in cnsVal[0]);
                return gtNewDconNode(TYP_FLOAT, val);
            }

            case TYP_DOUBLE:
            {
                assert(cnsVal.Length == sizeof(double));
                var val = Unsafe.ReadUnaligned<double>(in cnsVal[0]);
                return gtNewDconNode(TYP_DOUBLE, val);
            }

            case TYP_REF:
            {
                assert(cnsVal.Length == sizeof(nint));
                var val = Unsafe.ReadUnaligned<nint>(in cnsVal[0]);
                return (val is 0) ? gtNewNull() : gtNewIconEmbObjHndNode((CORINFO_OBJECT_HANDLE)(val));
            }

#if FEATURE_SIMD
            case TYP_SIMD8:
            case TYP_SIMD12:
            case TYP_SIMD16:
#if TARGET_XARCH
            case TYP_SIMD32:
            case TYP_SIMD64:
#endif
            {
                assert(cnsVal.Length == type.Size);
                var vecCon = gtNewVconNode(type);

                Unsafe.CopyBlockUnaligned(ref vecCon.SimdVal.u8[0], in cnsVal[0], type.Size);
                return vecCon;
            }
#endif

            default:
            {
                unreached();
                return null;
            }
        }
    }

    /// <summary>Helper to create a call helper node.</summary>
    /// <param name="type">Type of the node</param>
    /// <param name="helper">Call helper</param>
    /// <param name="args">Call args (struct args not supported)</param>
    /// <returns>New CT_HELPER node</returns>
    public unsafe GenTreeCall gtNewHelperCallNode(var_types type, CorInfoHelpFunc helper, params ReadOnlySpan<GenTree> args)
    {
        var result = gtNewCallNode(type, CT_HELPER, eeFindHelper(helper));

        if (!helper.NoThrow)
        {
            result.Flags |= GTF_EXCEPT;

            if (helper.AlwaysThrow)
            {
                setCallDoesNotReturn(result);
            }
        }
#if DEBUG
        // Helper calls are never candidates.
        result._inlineObservation = InlineObservation.CALLSITE_IS_CALL_TO_HELPER;
#endif

        ref var callArgs = ref result.Args;

        for (var i = args.Length - 1; i >= 0; i--)
        {
            var arg = args[i];

            var callArg = NewCallArg.CreateForPrimitive(arg);
            callArgs.PushFront(callArg);

            result.Flags |= (arg.Flags & GTF_ALL_EFFECT);
        }
        return result;
    }

    public unsafe GenTree gtNewIconEmbClsHndNode(CORINFO_CLASS_HANDLE clsHnd)
    {
        void* pEmbedClsHnd;
        var embedClsHnd = info.compCompHnd->embedClassHandle(clsHnd, &pEmbedClsHnd);

        assert((embedClsHnd is not null) != (pEmbedClsHnd is not null));
        return gtNewIconEmbHndNode(embedClsHnd, pEmbedClsHnd, GTF_ICON_CLASS_HDL, clsHnd);
    }

    /// <summary>Create a tree that computes a constant lookup.</summary>
    /// <param name="lookup">The lookup</param>
    /// <param name="flags">The handle kind of the computed value</param>
    /// <param name="compileTimeHandle">The compile-time handle of the computed value</param>
    /// <returns>"CNS_INT" or "IND(CNS_INT)" that computes "pLookup".</returns>
    public unsafe GenTree gtNewIconEmbHndNode(in CORINFO_CONST_LOOKUP lookup, GenTreeFlags flags, void* compileTimeHandle)
    {
        assert(lookup.accessType is not IAT_PPVALUE and not IAT_RELPVALUE);

        CORINFO_GENERIC_HANDLE handle = null;
        void* pIndirection = null;

        if (lookup.accessType == IAT_VALUE)
        {
            handle = lookup.handle;
        }
        else if (lookup.accessType == IAT_PVALUE)
        {
            pIndirection = lookup.addr;
        }
        return gtNewIconEmbHndNode(handle, pIndirection, flags, compileTimeHandle);
    }

    public unsafe GenTree gtNewIconEmbHndNode(void* value, void* pValue, GenTreeFlags flags, void* compileTimeHandle)
    {
        // Allocates a integer constant entry that represents a HANDLE to something.
        // It may not be allowed to embed HANDLEs directly into the JITed code (for eg,
        // as arguments to JIT helpers). Get a corresponding value that can be embedded.
        // If the handle needs to be accessed via an indirection, pValue points to it.

        GenTreeIntCon iconNode;
        GenTree handleNode;

        if (value is not null)
        {
            // When 'value' is non-null, pValue is required to be null
            assert(pValue is null);

            // use 'value' to construct an integer constant node
            iconNode = gtNewIconHandleNode(unchecked((nint)(value)), flags);

            // 'value' is the handle
            handleNode = iconNode;
        }
        else
        {
            // When 'value' is null, pValue is required to be non-null
            assert(pValue is not null);

            // use 'pValue' to construct an integer constant node
            iconNode = gtNewIconHandleNode(unchecked((nint)(pValue)), flags);

            // 'pValue' is an address of a location that contains the handle, construct the indirection of 'pValue'.
            handleNode = gtNewIndir(TYP_I_IMPL, iconNode, GTF_IND_NONFAULTING | GTF_IND_INVARIANT);
        }

        iconNode.CompileTimeHandle = unchecked((nint)(compileTimeHandle));

#if DEBUG
        if (iconNode.IsIconHandle(GTF_ICON_CLASS_HDL, GTF_ICON_METHOD_HDL, GTF_ICON_FTN_ADDR))
        {
            iconNode.TargetHandle = unchecked((nint)(compileTimeHandle));
        }

        if (iconNode.IsIconHandle(GTF_ICON_OBJ_HDL))
        {
            iconNode.TargetHandle = (value is not null) ? unchecked((nint)(value)) : unchecked((nint)(pValue));
        }
#endif

        return handleNode;
    }

    public unsafe GenTree gtNewIconEmbFldHndNode(CORINFO_FIELD_HANDLE fldHnd)
    {
        void* pEmbedFldHnd;
        var embedFldHnd = (void*)info.compCompHnd->embedFieldHandle(fldHnd, &pEmbedFldHnd);

        assert((embedFldHnd is not null) != (pEmbedFldHnd is not null));
        return gtNewIconEmbHndNode(embedFldHnd, pEmbedFldHnd, GTF_ICON_FIELD_HDL, fldHnd);
    }

    public unsafe GenTree gtNewIconEmbMethHndNode(CORINFO_METHOD_HANDLE methHnd)
    {
        void* pEmbedMethHnd;
        var embedMethHnd = (void*)info.compCompHnd->embedMethodHandle(methHnd, &pEmbedMethHnd);

        assert((embedMethHnd is not null) != (pEmbedMethHnd is not null));
        return gtNewIconEmbHndNode(embedMethHnd, pEmbedMethHnd, GTF_ICON_METHOD_HDL, methHnd);
    }

    public unsafe GenTree gtNewIconEmbObjHndNode(CORINFO_OBJECT_HANDLE objHnd)
        => gtNewIconEmbHndNode(objHnd, pValue: null, GTF_ICON_OBJ_HDL, compileTimeHandle: null);

    public unsafe GenTree gtNewIconEmbScpHndNode(CORINFO_MODULE_HANDLE scpHnd)
    {
        void* pEmbedScpHnd;
        var embedScpHnd = info.compCompHnd->embedModuleHandle(scpHnd, &pEmbedScpHnd);

        assert((embedScpHnd is not null) != (pEmbedScpHnd is not null));
        return gtNewIconEmbHndNode(embedScpHnd, pEmbedScpHnd, GTF_ICON_SCOPE_HDL, scpHnd);
    }

    public GenTreeIntCon gtNewIconHandleNode(nint value, GenTreeFlags flags, FieldSeq? fields = null)
    {
        assert((flags & GTF_ICON_HDL_MASK) is not 0);
        var node = new GenTreeIntCon(gtGetTypeForIconFlags(flags), value, fields);

        node.Flags |= flags;
        return node;
    }

    public GenTreeIntCon gtNewIconNode(var_types type, nint value)
    {
        return new GenTreeIntCon(type, value);
    }

    public GenTreeIntCon gtNewIconNode(nint value, FieldSeq? fields)
    {
        return new GenTreeIntCon(TYP_I_IMPL, value, fields);
    }

    public unsafe GenTreeCall gtNewIndCallNode(var_types type, GenTree? addr, in DebugInfo di = default)
    {
        var call = gtNewCallNode(type, CT_INDIRECT, callHnd: null, di);
        call._addr = addr;
        return call;
    }

    public unsafe GenTreeIndexAddr gtNewIndexAddr(GenTree arrayOp, GenTree indexOp, var_types elemType, CORINFO_CLASS_HANDLE elemClassHandle, int firstElemOffset, int lengthOffset)
    {
        var elemSize = (elemType is TYP_STRUCT) ? info.compCompHnd->getClassSize(elemClassHandle) : elemType.Size;

#if DEBUG
        var boundsCheck = JitConfig[ConfigInteger.JitSkipArrayBoundCheck] is not 1;
#else
        var boundsCheck = true;
#endif

        return new GenTreeIndexAddr(arrayOp, indexOp, elemType, elemClassHandle, elemSize, lengthOffset, firstElemOffset, boundsCheck);
    }

    public unsafe GenTreeIndir gtNewIndexIndir(GenTreeIndexAddr indexAddr)
    {
        if (indexAddr.ElemType is TYP_STRUCT)
        {
            return gtNewBlkIndir(indexAddr, typGetObjLayout(indexAddr.StructElemClass));
        }
        else
        {
            return gtNewIndir(indexAddr.ElemType, indexAddr);
        }
    }

    /// <summary>Create an indirection node</summary>
    /// <param name="typ">Type of the node</param>
    /// <param name="addr">Address of the indirection</param>
    /// <param name="indirFlags">Indirection flags</param>
    /// <returns>The created GT_IND node.</returns>
    public GenTreeIndir gtNewIndir(var_types typ, GenTree addr, GenTreeFlags indirFlags = GTF_EMPTY)
    {
        var indir = new GenTreeIndir(GT_IND, typ, addr);
        gtInitializeIndirNode(indir, indirFlags);
        return indir;
    }

    /// <summary>Creates an indirection GenTree node of a constant handle</summary>
    /// <param name="indType">The type returned by the indirection node</param>
    /// <param name="addr">The constant address to read from</param>
    /// <param name="iconFlags">The GTF_ICON flag value that specifies the kind of handle that we have</param>
    /// <returns>Returns a GT_IND node representing value at the address provided by 'addr'</returns>
    /// <remarks>
    ///   <para>The GT_IND node is marked as non-faulting.</para>
    ///   <para>If the indirection is not invariant, we also mark the indNode as GTF_GLOB_REF.</para>
    /// </remarks>
    public GenTreeIndir gtNewIndOfIconHandleNode(var_types indType, nint addr, GenTreeFlags iconFlags)
    {
        var addrNode = gtNewIconHandleNode(addr, iconFlags);

        // This indirection won't cause an exception.
        var indirFlags = GTF_IND_NONFAULTING;

        if (GenTree.HandleKindDataIsInvariant(iconFlags))
        {
            indirFlags |= GTF_IND_INVARIANT;
        }

        if (GenTree.HandleKindDataIsNotNull(iconFlags))
        {
            indirFlags |= GTF_IND_NONNULL;
        }
        return gtNewIndir(indType, addrNode, indirFlags);
    }

    public GenTreeRetExpr gtNewInlineCandidateReturnExpr(GenTreeCall inlineCandidate, var_types type)
    {
        // GT_RET_EXPR node eventually might be turned back into GT_CALL (when inlining is aborted for example).
        // Therefore it should carry the GTF_CALL flag so that all the rules about spilling can apply to it as well.
        // For example, impImportLeave or CEE_POP need to spill GT_RET_EXPR before empty the evaluation stack.

        var node = new GenTreeRetExpr(type, inlineCandidate);
        node.Flags |= GTF_CALL;
        return node;
    }

    public GenTreeLclFld gtNewLclAddrNode(var_types type, int lclNum, ushort lclOffs, ClassLayout? layout = null)
    {
        return new GenTreeLclFld(GT_LCL_ADDR, type, lclNum, lclOffs, layout);
    }

    public GenTreeLclFld gtNewLclFldNode(var_types type, int lclNum, ushort lclOffs, ClassLayout? layout = null)
    {
        return new GenTreeLclFld(GT_LCL_FLD, type, lclNum, lclOffs, layout);
    }

    public GenTreeLclVar gtNewLclVarNode(var_types type, int lclNum)
    {
        ref var varDsc = ref lvaGetDesc(lclNum);

        if (type == TYP_UNDEF)
        {
            type = varDsc.Type;

            if (varDsc.lvNormalizeOnLoad)
            {
                type = type.ActualType;
            }
        }

        var  lclVar = gtNewLclvNode(type, lclNum);

        if (varDsc.IsAddressExposed)
        {
            lclVar.Flags |= GTF_GLOB_REF;
        }
        return lclVar;
    }

    public GenTreeLclVar gtNewLclvNode(var_types type, int lclNum, IL_OFFSET offs = BAD_IL_OFFSET)
    {
        assert(type != TYP_VOID);

        // We need to ensure that all struct values are normalized.
        // It might be nice to assert this in general, but we have stores of int to long.
        if (varTypeIsStruct(type))
        {
            // Make an exception for implicit by-ref parameters during global morph, since
            // their lvType has been updated to byref but their appearances have not yet all
            // been rewritten and so may have struct type still.
            ref var varDsc = ref lvaGetDesc(lclNum);

            var simd12ToSimd16Widening = false;
#if FEATURE_SIMD
            // We can additionally have a simd12 that was widened to a simd16, generally as part of lowering
            simd12ToSimd16Widening = (type is TYP_SIMD16) && (varDsc.Type == TYP_SIMD12);
#endif
            assert((type == varDsc.Type) || simd12ToSimd16Widening ||
                   (lvaIsImplicitByRefLocal(lclNum) && fgGlobalMorph && (varDsc.Type == TYP_BYREF)));
        }

        // We cannot have assert lnum < lvaCount because the inliner uses this function to add temporaries
        return new GenTreeLclVar(type, lclNum, offs);
    }

    public GenTreeLclFld gtNewLclVarAddrNode(var_types type, int lclNum)
    {
        return gtNewLclAddrNode(type, lclNum, lclOffs: 0);
    }

    public GenTreeIntConCommon gtNewLconNode(long value)
    {
#if TARGET_64BIT
        return new GenTreeIntCon(TYP_LONG, (nint)(value));
#else
        return new GenTreeLngCon(value);
#endif
    }

    public GenTreeUnOp gtNewLoadValueNode(var_types type, GenTree addr, GenTreeFlags indirFlags = GTF_EMPTY)
    {
        return gtNewLoadValueNode(type, addr, layout: null, indirFlags);
    }

    public GenTreeUnOp gtNewLoadValueNode(GenTree addr, ClassLayout layout, GenTreeFlags indirFlags = GTF_EMPTY)
    {
        return gtNewLoadValueNode(layout.Type, addr, layout, indirFlags);
    }

    /// <summary>Return a node that represents a loaded value.</summary>
    /// <param name="type">Type to load</param>
    /// <param name="addr">Struct layout to load</param>
    /// <param name="layout">The address</param>
    /// <param name="indirFlags">Indirection flags</param>
    /// <returns>A "BLK/IND" node, or "LCL_VAR" if "addr" points to a compatible local.</returns>
    public GenTreeUnOp gtNewLoadValueNode(var_types type, GenTree addr, ClassLayout? layout, GenTreeFlags indirFlags = GTF_EMPTY)
    {
        assert((indirFlags & ~GTF_IND_FLAGS) is 0);

        if (((indirFlags & GTF_IND_VOLATILE) is 0) && addr.IsLclVarAddr)
        {
            var lclNum = addr.AsLclFld().LclNum;
            ref var varDsc = ref lvaGetDesc(lclNum);

            if (varDsc.Type == type)
            {
                if (type is not TYP_STRUCT)
                {
                    return gtNewLclvNode(type, lclNum);
                }
                else
                {
                    assert(layout is not null);
                    assert(varDsc.Layout is not null);

                    if (layout.CanAssignFrom(varDsc.Layout))
                    {
                        return gtNewLclvNode(type, lclNum);
                    }
                }
            }
        }

        if (type == TYP_STRUCT)
        {
            assert(layout is not null);
            return gtNewBlkIndir(addr, layout, indirFlags);
        }
        else
        {
            return gtNewIndir(type, addr, indirFlags);
        }
    }

    /// <summary>Helper to create an MD array length node.</summary>
    /// <param name="arrayOp">Array node</param>
    /// <param name="dim">MD array dimension of interest</param>
    /// <param name="rank">MD array rank</param>
    /// <returns>New GT_MDARR_LENGTH node</returns>
    public GenTreeMDArr gtNewMDArrLen(GenTree arrayOp, int dim, int rank)
    {
        // Should have been set in the importer.
        assert((optMethodFlags & OMF_HAS_MDARRAYREF) is not 0);

        var mdarr = new GenTreeMDArr(GT_MDARR_LENGTH, arrayOp, dim, rank);
        mdarr.SetIndirExceptionFlags(this);
        return mdarr;
    }

    /// <summary>Helper to create an MD array lower bound node.</summary>
    /// <param name="arrayOp">Array node</param>
    /// <param name="dim">MD array dimension of interest</param>
    /// <param name="rank">MD array rank</param>
    /// <returns>New GT_MDARR_LOWER_BOUND node</returns>
    public GenTreeMDArr gtNewMDArrLowerBound(GenTree arrayOp, int dim, int rank)
    {
        // Should have been set in the importer.
        assert((optMethodFlags & OMF_HAS_MDARRAYREF) is not 0);

        var mdarr = new GenTreeMDArr(GT_MDARR_LOWER_BOUND, arrayOp, dim, rank);
        mdarr.SetIndirExceptionFlags(this);
        return mdarr;
    }

    public static GenTree gtNewMemoryBarrierNode()
    {
        return new GenTree(GT_MEMORYBARRIER, TYP_VOID);
    }

    /// <summary>Create a memory barrier node</summary>
    /// <param name="barrierKind">the kind of barrer we are creating</param>
    /// <returns>The created GT_MEMORYBARRIER node.</returns>
    public GenTree gtNewMemoryBarrierNode(BarrierKind barrierKind)
    {
        var tree = gtNewMemoryBarrierNode();
        tree.Flags |= (GTF_GLOB_REF | GTF_ASG);

        if (barrierKind is BARRIER_LOAD_ONLY)
        {
            tree.Flags |= GTF_MEMORYBARRIER_LOAD;
        }
        else if (barrierKind is BARRIER_STORE_ONLY)
        {
            tree.Flags |= GTF_MEMORYBARRIER_STORE;
        }
        return tree;
    }

    public GenTreeIndir gtNewMethodTableLookup(GenTree obj, bool onStack = false)
    {
        assert(onStack || (obj.Type is TYP_REF));
        return gtNewIndir(TYP_I_IMPL, obj, GTF_IND_INVARIANT | GTF_IND_NONNULL);
    }

#if FEATURE_MASKED_HW_INTRINSICS
    public GenTreeMskCon gtNewMskConNode(simdmask_t simdMaskVal)
    {
        return new GenTreeMskCon(simdMaskVal);
    }
#endif

    /// <summary>Create (and check for) a "nothing" node, i.e. a node that doesn't produce any code.</summary>
    /// <returns></returns>
    /// <remarks>We currently use a "GT_NOP" node of type void for this purpose.</remarks>
    public GenTree gtNewNothingNode() => new GenTree(GT_NOP, TYP_VOID);

    public GenTreeIntCon gtNewNull()
    {
        return gtNewIconNode(TYP_REF, 0);
    }

    /// <summary>Helper to create a null check node.</summary>
    /// <param name="addr">Address to null check</param>
    /// <returns>New GT_NULLCHECK node</returns>
    public GenTreeIndir gtNewNullCheck(GenTree addr)
    {
        assert(fgAddrCouldBeNull(addr));
        optMethodFlags |= OMF_HAS_NULLCHECK;

        var nullCheck = new GenTreeIndir(GT_NULLCHECK, TYP_BYTE, addr);
        nullCheck.Flags |= GTF_EXCEPT;
        return nullCheck;
    }

    public GenTree gtNewOneConNode(var_types type, var_types simdBaseType = TYP_UNDEF)
    {
        switch (type)
        {
            case TYP_INT:
            case TYP_UINT:
            {
                return gtNewIconNode(TYP_INT, 1);
            }

            case TYP_LONG:
            case TYP_ULONG:
            {
                return gtNewLconNode(1);
            }

            case TYP_FLOAT:
            case TYP_DOUBLE:
            {
                return gtNewDconNode(type, 1.0);
            }

#if FEATURE_SIMD
            case TYP_SIMD8:
            case TYP_SIMD12:
            case TYP_SIMD16:
#if TARGET_XARCH
            case TYP_SIMD32:
            case TYP_SIMD64:
#endif
            {
                var vecCon = gtNewVconNode(type);

                if (varTypeIsFloating(simdBaseType))
                {
                    vecCon.EvaluateBroadcastInPlace(simdBaseType, 1);
                }
                else
                {
                    vecCon.EvaluateBroadcastInPlace(simdBaseType,1.0);
                }
                return vecCon;
            }
#endif

            default:
            {
                unreached();
                return null;
            }
        }
    }

    public GenTreeQmark gtNewQmarkNode(var_types type, GenTree cond, GenTreeColon colon)
    {
        assert(!compQmarkRationalized, "QMARKs are illegal to create after QMARK-rationalization");
        compQmarkUsed = true;
        return new GenTreeQmark(type, cond, colon);
    }

    public unsafe GenTree? gtNewRefComField(GenTree? objPtr, in CORINFO_RESOLVED_TOKEN resolvedToken, CORINFO_ACCESS_FLAGS access, in CORINFO_FIELD_INFO fieldInfo, var_types lclTyp, GenTree? value)
    {
        assert(fieldInfo.fieldAccessor is CORINFO_FIELD_INSTANCE_HELPER or CORINFO_FIELD_INSTANCE_ADDR_HELPER or CORINFO_FIELD_STATIC_ADDR_HELPER);

        // Arguments in reverse order
        var args = default(InlineArray4<GenTree>);
        var nArgs = 0;

        // If we can't access it directly, we need to call a helper function
        var helperType = TYP_BYREF;
        var structType = fieldInfo.structType;

        if (fieldInfo.fieldAccessor == CORINFO_FIELD_INSTANCE_HELPER)
        {
            if ((access & CORINFO_ACCESS_SET) is not 0)
            {
                assert(value is not null);

                if (lclTyp is TYP_DOUBLE)
                {
                    if (value.Type is TYP_FLOAT)
                    {
                        value = gtNewCastNode(TYP_DOUBLE, value, fromUnsigned: false, TYP_DOUBLE);
                    }
                }
                else if (lclTyp is TYP_FLOAT)
                {
                    if (value.Type is TYP_DOUBLE)
                    {
                        value = gtNewCastNode(TYP_FLOAT, value, fromUnsigned: false, TYP_FLOAT);
                    }
                }

                args[nArgs++] = value;
                helperType = TYP_VOID;
            }
            else if ((access & CORINFO_ACCESS_GET) is not 0)
            {
                helperType = lclTyp;
            }
        }

        var fieldHnd = impTokenToHandle(resolvedToken);

        if (fieldHnd is null)
        {
            return null;
        }

        args[nArgs++] = fieldHnd;

        // If it's a static field, we shouldn't have an object node
        // If it's an instance field, we have an object node
        assert((fieldInfo.fieldAccessor is not CORINFO_FIELD_STATIC_ADDR_HELPER) ^ (objPtr is null));

        if (objPtr is not null)
        {
            args[nArgs++] = objPtr;
        }

        var call = gtNewHelperCallNode(helperType.ActualType, fieldInfo.helper);

        for (var i = 0; i < nArgs; i++)
        {
            call.Args.PushFront(NewCallArg.CreateForPrimitive(args[i]));
            call.Flags |= (args[i].Flags & GTF_ALL_EFFECT);
        }

#if FEATURE_MULTIREG_RET
        if (varTypeIsStruct(call.Type))
        {
            call.InitializeStructReturnType(this, structType, call.UnmanagedCallConv);
        }
#endif

        GenTree result = call;

        if (fieldInfo.fieldAccessor is CORINFO_FIELD_INSTANCE_HELPER)
        {
            if (((access & CORINFO_ACCESS_GET) is not 0) && varTypeIsSmall(lclTyp))
            {
                // The helper does not extend the small return types.
                result = gtNewCastNode(lclTyp.ActualType, result, fromUnsigned: false, lclTyp);
            }
        }
        else if ((access & CORINFO_ACCESS_ADDRESS) is 0)
        {
            // OK, now do the indirection
            lclTyp = TypeHandleToVarType(fieldInfo.fieldType, structType, out var layout);
            assert(layout is not null);

            if ((access & CORINFO_ACCESS_SET) is not 0)
            {
                assert(value is not null);

                result = (lclTyp == TYP_STRUCT) ? gtNewStoreBlkNode(result, value, layout)
                                                : gtNewStoreIndNode(lclTyp, result, value);

                if (varTypeIsStruct(lclTyp))
                {
                    result = impStoreStruct(result, CHECK_SPILL_ALL);
                }
            }
            else
            {
                assert((access & CORINFO_ACCESS_GET) is not 0);
                result = (lclTyp == TYP_STRUCT) ? gtNewBlkIndir(result, layout) : gtNewIndir(lclTyp, result);
            }
        }

        return result;
    }

    /// <summary>Helper to create a runtime lookup node</summary>
    /// <param name="tree">tree for the lookup</param>
    /// <param name="hnd">generic handle being looked up</param>
    /// <param name="hndTyp">type of the generic handle</param>
    /// <returns>New GenTreeRuntimeLookup node.</returns>
    public unsafe GenTreeRuntimeLookup gtNewRuntimeLookup(GenTree tree, CORINFO_GENERIC_HANDLE hnd, CorInfoGenericHandleType hndTyp)
    {
        return new GenTreeRuntimeLookup(tree, hnd, hndTyp);
    }

    /// <summary>Helper to create a runtime lookup call helper node.</summary>
    /// <param name="runtimeLookup">Call helper</param>
    /// <param name="ctxTree">Type of the node</param>
    /// <param name="compileTimeHandle">Call args</param>
    /// <returns>New CT_HELPER node</returns>
    public unsafe GenTreeCall gtNewRuntimeLookupHelperCallNode(in CORINFO_RUNTIME_LOOKUP runtimeLookup, GenTree ctxTree, void* compileTimeHandle)
    {
#if FEATURE_READYTORUN
        if (IsAot && (runtimeLookup.indirections == CORINFO_USEHELPER))
        {
            var call = gtNewHelperCallNode(TYP_I_IMPL, runtimeLookup.helper, ctxTree);
            call._entryPoint = runtimeLookup.helperEntryPoint;
            return call;
        }
#endif

        // Call the helper
        // - Setup argNode with the pointer to the signature returned by the lookup
        var argNode = gtNewIconEmbHndNode(runtimeLookup.signature, pValue: null, GTF_ICON_GLOBAL_PTR, compileTimeHandle);
        var helperCall = gtNewHelperCallNode(TYP_I_IMPL, runtimeLookup.helper, ctxTree, argNode);

        // No need to perform CSE/hoisting for signature node - it is expected to end up in a rarely-taken block after
        // "Expand runtime lookups" phase.
        argNode.Flags |= GTF_DONT_CSE;

        // Leave a note that this method has runtime lookups we might want to expand (nullchecks, size checks) later.
        // We can also consider marking current block as a runtime lookup holder to improve TP for Tier0
        impInlineRoot.MethodHasExpRuntimeLookup = true;

        var signatureToLookupInfoMap = impInlineRoot.SignatureToLookupInfoMap;

        if (!signatureToLookupInfoMap.ContainsKey(runtimeLookup.signature))
        {
            JITDUMP($"Registering {(nuint)(runtimeLookup.signature):X} in SignatureToLookupInfoMap\n");
            signatureToLookupInfoMap[runtimeLookup.signature] = runtimeLookup;
        }
        return helperCall;
    }

    public unsafe GenTreeStrCon gtNewSconNode(int cpx, CORINFO_MODULE_HANDLE scpHandle)
    {
        return new GenTreeStrCon(cpx, scpHandle);
    }

    public Statement gtNewStmt(GenTree expr)
    {
        return new Statement(expr, compStatementID++);
    }

    public Statement gtNewStmt(GenTree expr, in DebugInfo di)
    {
        var statement = gtNewStmt(expr);
        statement.SetDebugInfo(di);
        return statement;
    }

    /// <summary>Create an indirect struct store node.</summary>
    /// <param name="addr">Destination address</param>
    /// <param name="value">Value to store</param>
    /// <param name="layout">The struct layout</param>
    /// <param name="indirFlags">Indirection flags</param>
    /// <returns></returns>
    /// <remarks>The created GT_STORE_BLK node.</remarks>
    public GenTreeBlk gtNewStoreBlkNode(GenTree addr, GenTree value, ClassLayout layout, GenTreeFlags indirFlags = GTF_EMPTY)
    {
        assert((indirFlags & GTF_IND_INVARIANT) is 0);
        assert(value.Oper.IsInitVal || layout.CanAssignFrom(value.GetLayout(this)));

        var storeBlk = new GenTreeBlk(TYP_STRUCT, addr, value, layout);
        storeBlk.Flags |= GTF_ASG;

        gtInitializeIndirNode(storeBlk, indirFlags);
        gtInitializeStoreNode(storeBlk, value);

        return storeBlk;
    }

    /// <summary>Create an indirect store node.</summary>
    /// <param name="type">Type of the store</param>
    /// <param name="addr">Destination address</param>
    /// <param name="value">Value to store</param>
    /// <param name="indirFlags">Indirection flags</param>
    /// <returns>The created GT_STOREIND node.</returns>
    public GenTreeStoreInd gtNewStoreIndNode(var_types type, GenTree addr, GenTree value, GenTreeFlags indirFlags = GTF_EMPTY)
    {
        assert(((indirFlags & GTF_IND_INVARIANT) is 0) && (type is not TYP_STRUCT));

        var storeInd = new GenTreeStoreInd(type, addr, value);
        storeInd.Flags |= GTF_ASG;

        gtInitializeIndirNode(storeInd, indirFlags);
        gtInitializeStoreNode(storeInd, value);

        return storeInd;
    }

    /// <summary>Create a local field store node.</summary>
    /// <param name="type">Type of the store</param>
    /// <param name="lclNum">Number of the local being stored to</param>
    /// <param name="offset">Offset of the store</param>
    /// <param name="value">Value to store</param>
    /// <param name="layout">Struct layout of the store</param>
    /// <returns>The created STORE_LCL_FLD node.</returns>
    public GenTreeLclFld gtNewStoreLclFldNode(var_types type, int lclNum, ushort offset, GenTree value, ClassLayout? layout)
    {
        assert((type is TYP_STRUCT) == (layout is not null));

        var storeLclFld = new GenTreeLclFld(type, lclNum, offset, value, layout);
        storeLclFld.Flags |= (GTF_VAR_DEF | GTF_ASG);

        if (storeLclFld.IsPartial(this))
        {
            storeLclFld.Flags |= GTF_VAR_USEASG;
        }
        if (lvaGetDesc(lclNum).IsAddressExposed)
        {
            storeLclFld.Flags |= GTF_GLOB_REF;
        }
        gtInitializeStoreNode(storeLclFld, value);

        return storeLclFld;
    }

    public GenTreeLclFld gtNewStoreLclFldNode(var_types type, int lclNum, ushort offset, GenTree value)
    {
        return gtNewStoreLclFldNode(type, lclNum, offset, value, (type == TYP_STRUCT) ? value.GetLayout(this) : null);
    }

    /// <summary>Create a local store node.</summary>
    /// <param name="lclNum">Number of the local being stored to</param>
    /// <param name="value">Value to store</param>
    /// <returns>The created STORE_LCL_VAR node.</returns>
    public GenTreeLclVar gtNewStoreLclVarNode(int lclNum, GenTree value)
    {
        ref var varDsc = ref lvaGetDesc(lclNum);
        var type = varDsc.Type;

        if (varDsc.lvNormalizeOnLoad)
        {
            type = type.ActualType;
        }

        var store = new GenTreeLclVar(type, lclNum, value);
        store.Flags |= (GTF_VAR_DEF | GTF_ASG);

        if (varDsc.IsAddressExposed)
        {
            store.Flags |= GTF_GLOB_REF;
        }

        gtInitializeStoreNode(store, value);
        return store;
    }

    public GenTree gtNewStoreValueNode(var_types type, GenTree addr, GenTree value, GenTreeFlags indirFlags = GTF_EMPTY)
    {
        return gtNewStoreValueNode(type, addr, value, layout: null, indirFlags: indirFlags);
    }

    public GenTree gtNewStoreValueNode(GenTree addr, GenTree value, ClassLayout layout, GenTreeFlags indirFlags = GTF_EMPTY)
    {
        return gtNewStoreValueNode(layout.Type, addr, value, layout, indirFlags);
    }

    /// <summary>Return a node that represents a store.</summary>
    /// <param name="type">Type to store</param>
    /// <param name="addr">Destination address</param>
    /// <param name="value">Value to store</param>
    /// <param name="layout">Struct layout for the store</param>
    /// <param name="indirFlags">Indirection flags</param>
    /// <returns>A "STORE_BLK/STORE_IND" node, or "STORE_LCL_VAR" if "addr" points to a compatible local.</returns>
    public GenTree gtNewStoreValueNode(var_types type, GenTree addr, GenTree value, ClassLayout? layout, GenTreeFlags indirFlags = GTF_EMPTY)
    {
        assert((type is not TYP_STRUCT) || (layout is not null));

        if (((indirFlags & GTF_IND_VOLATILE) is 0) && addr.IsLclVarAddr)
        {
            var lclNum = addr.AsLclFld().LclNum;
            ref var varDsc = ref lvaGetDesc(lclNum);

            if (varDsc.Type == type)
            {
                if (type is not TYP_STRUCT)
                {
                    return gtNewStoreLclVarNode(lclNum, value);
                }
                else
                {
                    assert(layout is not null);
                    assert(varDsc.Layout is not null);

                    if (varDsc.Layout.CanAssignFrom(layout))
                    {
                        return gtNewStoreLclVarNode(lclNum, value);
                    }
                }
            }
        }

        if (type is TYP_STRUCT)
        {
            assert(layout is not null);
            return gtNewStoreBlkNode(addr, value, layout, indirFlags);
        }
        else
        {
            return gtNewStoreIndNode(type, addr, value, indirFlags);
        }
    }

    public GenTree gtNewTempStore(int lclNum, GenTree val, int curLevel = CHECK_SPILL_NONE, in DebugInfo di = default, BasicBlock? block = null)
        => gtNewTempStore(lclNum, val, ref Unsafe.NullRef<Statement>(), curLevel, di, block);

    /// <summary>Create a store of the given value to a temp.</summary>
    /// <param name="lclNum">local number for a compiler temp</param>
    /// <param name="val">value to store to the temp</param>
    /// <param name="afterStmt">statement to insert any additional statements after</param>
    /// <param name="curLevel">stack level to spill at (importer-only)</param>
    /// <param name="di">debug info for new statements</param>
    /// <param name="block">block to insert any additional statements in</param>
    /// <returns>Normally a new store node. However may return a nop node if val is simply a reference to the temp.</returns>
    /// <remarks>
    ///   <para>Self-stores may be represented via NOPs.</para>
    ///   <para>May update the type of the temp, if it was previously unknown.</para>
    ///   <para>May set compFloatingPointUsed.</para>
    /// </remarks>
    public GenTree gtNewTempStore(int lclNum, GenTree val, ref Statement afterStmt, int curLevel = CHECK_SPILL_NONE, in DebugInfo di = default, BasicBlock? block = null)
    {
        var oper = val.Oper;
        var valType = val.Type;
        var valLclNum = BAD_VAR_NUM;

        if (oper is GT_LCL_VAR)
        {
            var lclVar = val.AsLclVar();
            valLclNum = lclVar.LclNum;

            if (valLclNum == lclNum)
            {
                // Self-assignment is a nop.
                return gtNewNothingNode();
            }
        }

        ref var varDsc = ref lvaGetDesc(lclNum);
        var dstTyp = varDsc.Type;

        if ((dstTyp is TYP_I_IMPL) && (valType is TYP_BYREF))
        {
            impBashVarAddrsToI(val);
        }

        if (valLclNum != BAD_VAR_NUM)
        {
            ref var lvaDsc = ref lvaGetDesc(valLclNum);

            if (lvaDsc.lvNormalizeOnLoad)
            {
                valType = lvaDsc.Type;
                val.Type = valType;
            }
        }

        if (dstTyp == TYP_UNDEF)
        {
            // If the variable's lvType is not yet set then set it here
            dstTyp = valType.ActualType;
            varDsc.Type = dstTyp;

            if (dstTyp == TYP_STRUCT)
            {
                var layout = val.GetLayout(this);
                assert(layout is not null);
                lvaSetStruct(lclNum, layout, unsafeValueClsCheck: false);
            }
        }

#if DEBUG
        // Make sure the actual types match.
        if (valType.ActualType != dstTyp.ActualType)
        {
            // Plus some other exceptions that are apparently legal:
            // - TYP_REF or BYREF = TYP_I_IMPL
            var ok = false;

            if (varTypeIsGC(dstTyp) && (valType is TYP_I_IMPL))
            {
                ok = true;
            }
            else if ((dstTyp is TYP_I_IMPL) && (valType is TYP_BYREF))
            {
                // - TYP_I_IMPL = TYP_BYREF
                ok = true;
            }
            else if ((JitConfig[ConfigInteger.JitObjectStackAllocation] != 0) && (dstTyp is TYP_BYREF) && (valType is TYP_REF))
            {
                // - TYP_BYREF = TYP_REF when object stack allocation is enabled
                ok = true;
            }
            else if ((dstTyp is TYP_STRUCT) && (valType is TYP_INT))
            {
                assert(oper.IsInitVal);
                ok = true;
            }

            if (!ok)
            {
                gtDispTree(val);
                NO_WAY("Incompatible types for gtNewTempStore");
            }
        }
#endif

        // Added this NO_WAY for runtime\issue 44895, to protect against silent bad codegen
        if ((dstTyp is TYP_STRUCT) && (valType is TYP_REF))
        {
            NO_WAY("Incompatible types for gtNewTempStore");
        }

        // Floating Point stores can be created during inlining
        // see "Zero init inlinee locals:" in fgInlinePrependStatements
        // thus we may need to set compFloatingPointUsed to true here.
        if (!varTypeUsesIntReg(dstTyp))
        {
            compFloatingPointUsed = true;
        }

        var store = gtNewStoreLclVarNode(lclNum, val);

        // TODO-ASG: delete this zero-diff quirk. Requires some forward substitution work.
        store.Type = dstTyp;

        if (varTypeIsStruct(dstTyp) && !oper.IsInitVal)
        {
            var result = impStoreStruct(store, ref afterStmt, curLevel, di, block);
            assert(result.Oper.IsLocalStore);
            store = result.AsLclVar();
        }
        return store;
    }

    public GenTreeIntCon gtNewTrue()
    {
        return gtNewIconNode(TYP_INT, 1);
    }

    public GenTreeUnOp gtNewUnaryNode(genTreeOps oper, var_types type, GenTree? op1)
    {
        return new GenTreeUnOp(oper, type, op1);
    }

#if FEATURE_SIMD
    public GenTreeVecCon gtNewVconNode(var_types type)
    {
        return new GenTreeVecCon(type);
    }
#endif

    /// <summary>Helper to create a virtual function lookup helper node.</summary>
    /// <param name="type">Type of the node</param>
    /// <param name="helper">Call helper</param>
    /// <param name="thisPtr">'this' argument</param>
    /// <param name="methHnd">Runtime method handle argument</param>
    /// <param name="clsHnd">Class handle argument</param>
    /// <returns>New CT_HELPER node</returns>
    public unsafe GenTreeCall gtNewVirtualFunctionLookupHelperCallNode(var_types type, CorInfoHelpFunc helper, GenTree thisPtr, GenTree methHnd, GenTree? clsHnd = null)
    {
        var result = gtNewCallNode(type, CT_HELPER, eeFindHelper(helper));

        if (!helper.NoThrow)
        {
            result.Flags |= GTF_EXCEPT;

            if (helper.AlwaysThrow)
            {
                setCallDoesNotReturn(result);
            }
        }

#if DEBUG
        // Helper calls are never candidates.
        result._inlineObservation = InlineObservation.CALLSITE_IS_CALL_TO_HELPER;
#endif

        assert(methHnd is not null);

        result.Args.PushFront(NewCallArg.CreateForPrimitive(methHnd).WithWellKnownArg(WellKnownArg.RuntimeMethodHandle));
        result.Flags |= methHnd.Flags & GTF_ALL_EFFECT;

        if (clsHnd is not null)
        {
            result.Args.PushFront(NewCallArg.CreateForPrimitive(clsHnd));
            result.Flags |= clsHnd.Flags & GTF_ALL_EFFECT;
        }

        assert(thisPtr is not null);

        result.Args.PushFront(NewCallArg.CreateForPrimitive(thisPtr).WithWellKnownArg(WellKnownArg.ThisPointer));
        result.Flags |= thisPtr.Flags & GTF_ALL_EFFECT;

        return result;
    }

    public GenTree gtNewZeroConNode(var_types type)
    {
#if FEATURE_SIMD
        if (varTypeIsSimd(type))
        {
            return gtNewVconNode(type);
        }
#endif

        type = type.ActualType;

        switch (type)
        {
            case TYP_INT:
            case TYP_REF:
            case TYP_BYREF:
            {
                return gtNewIconNode(type, 0);
            }

            case TYP_LONG:
            {
                return gtNewLconNode(0);
            }

            case TYP_FLOAT:
            case TYP_DOUBLE:
            {
                return gtNewDconNode(type, 0.0);
            }

            default:
            {
                unreached();
                return null;
            }
        }
    }

    /// <summary>Return true if the given node (excluding children trees) contains side effects.</summary>
    /// <param name="node"></param>
    /// <param name="flags"></param>
    /// <param name="ignoreCctors"></param>
    /// <returns></returns>
    /// <remarks>
    ///   <para>Note that it does not recurse, and children need to be handled separately.</para>
    ///   <para>It may return false even if the node has GTF_SIDE_EFFECT (because of its children).</para>
    /// </remarks>
    public bool gtNodeHasSideEffects(GenTree node, GenTreeFlags flags, bool ignoreCctors = false)
    {
        if ((flags & GTF_ASG) != 0)
        {
            if (node.RequiresAsgFlag)
            {
                return true;
            }
        }

        // Are there only GTF_CALL side effects remaining? (and no other side effect kinds)
        if ((flags & GTF_CALL) != 0)
        {
            var potentialCall = node;

            while (potentialCall.Oper is GT_RET_EXPR)
            {
                // We need to preserve return expressions where the underlying call has side effects.
                // Otherwise early folding can result in us dropping the call.
                potentialCall = potentialCall.AsRetExpr().InlineCandidate;
            }

            if (potentialCall.Oper is GT_CALL)
            {
                var call = potentialCall.AsCall();
                var ignoreExceptions = (flags & GTF_EXCEPT) == 0;
                return call.HasSideEffects(this, ignoreExceptions, ignoreCctors);
            }
        }

        if ((flags & GTF_EXCEPT) != 0)
        {
            if (node.MayThrow(this))
            {
                return true;
            }
        }

        // Expressions declared as CSE by (e.g.) hoisting code are considered to have relevant side effects (if we care about GTF_MAKE_CSE).
        return ((flags & GTF_MAKE_CSE) != 0) && ((node.Flags & GTF_MAKE_CSE) != 0);
    }

    /// <inheritdoc cref="gtPeelOffsets(ref GenTree, out long, out FieldSeq)" />
    public void gtPeelOffsets(ref GenTree addr, out target_ssize_t offset)
        => gtPeelOffsets(ref addr, out offset, out _);

    /// <summary>Peel all ADD(addr, CNS_INT(x)) nodes off the specified address node and return the base node and sum of offsets peeled.</summary>
    /// <param name="addr">The address node.</param>
    /// <param name="offset">The sum of offset peeled such that ADD(addr, offset) is equivalent to the original addr.</param>
    /// <param name="fldSeq">The combined field sequence for all the peeled offsets.</param>
    public void gtPeelOffsets(ref GenTree addr, out target_ssize_t offset, out FieldSeq? fldSeq)
    {
        assert(addr.Type is TYP_I_IMPL or TYP_BYREF or TYP_REF);

        offset = 0;
        fldSeq = null;

        while (true)
        {
            if ((addr.Oper is GT_ADD) && !addr.HasOverflowCheck)
            {
                var addrOp = addr.AsOp();

                var op1 = addrOp.Op1;
                var op2 = addrOp.Op2;

                if (op2.Oper.IsCnsIntOrI && (op2.Type is TYP_I_IMPL))
                {
                    var intCon = op2.AsIntCon();

                    if (!intCon.IsIconHandle())
                    {
                        offset += intCon.IconValue;

                        assert(_fieldSeqStore is not null);
                        fldSeq = _fieldSeqStore.Append(fldSeq, intCon.FieldSeq);

                        addr = op1;
                        continue;
                    }
                }

                if (op1.Oper.IsCnsIntOrI && (op1.Type is TYP_I_IMPL))
                {
                    var intCon = op1.AsIntCon();

                    if (!intCon.IsIconHandle())
                    {
                        offset += intCon.IconValue;

                        assert(_fieldSeqStore is not null);
                        fldSeq = _fieldSeqStore.Append(intCon.FieldSeq, fldSeq);

                        addr = op2;
                        continue;
                    }
                }

                break;
            }
            else if (addr.Oper is GT_LEA)
            {
                var addrMode = addr.AsAddrMode();

                if (addrMode.HasIndex)
                {
                    break;
                }
                offset += addrMode.Offset;

                assert(addrMode.BaseAddress is not null);
                addr = addrMode.BaseAddress;
            }
            else
            {
                break;
            }
        }
    }

    public GenTree gtReverseCond(GenTree tree)
    {
        var oper = tree.Oper;

        if (oper.IsCompare)
        {
            tree.AsOp().ReverseRelop();
        }
        else if (oper is GT_JCC or GT_SETCC)
        {
            var cc = tree.AsCC();
            cc.Condition = GenCondition.Reverse(cc.Condition);
        }
        else if (oper is GT_JCMP or GT_JTEST)
        {
            var opCC = tree.AsOpCC();
            opCC.Condition = GenCondition.Reverse(opCC.Condition);
        }
        else if (oper.IsIntegralConst)
        {
            var con = tree.AsIntConCommon();
            con.IntegralValue = con.IsIntegralConst(0) ? 1 : 0;
        }
        else
        {
            tree = gtNewBinaryNode(GT_EQ, TYP_INT, tree, gtNewZeroConNode(TYP_INT));
        }
        return tree;
    }

    /// <summary>Given a tree, figure out the order in which its sub-operands should be evaluated.</summary>
    /// <param name="tree"></param>
    /// <returns>Returns the Sethi 'complexity' estimate for this tree (the higher the number, the higher is the tree's resources requirement).</returns>
    /// <remarks>
    ///   <para>If the second operand of a binary operator is more expensive than the first operand, then try to swap the operand trees.Updates the GTF_REVERSE_OPS bit if necessary in this case.</para>
    /// </remarks>
    public int gtSetEvalOrder(GenTree tree)
    {
        // This function sets:
        //   1. GetCostEx() to the execution complexity estimate
        //   2. GetCostSz() to the code size estimate
        //   3. Sometimes sets GTF_ADDRMODE_NO_CSE on nodes in the tree.

        if (opts.OptimizationDisabled)
        {
            return gtSetEvalOrderMinOpts(tree);
        }

        // TODO: Port Compiler.gtSetEvalOrder
        return 0;
    }

    /// <summary>A MinOpts specific version of gtSetEvalOrder. We don't need to set costs, but we're looking for opportunities to swap operands.</summary>
    /// <param name="tree">The tree for which we are setting the evaluation order.</param>
    /// <returns>the Sethi 'complexity' estimate for this tree (the higher the number, the higher is the tree's resources requirement)</returns>
    public int gtSetEvalOrderMinOpts(GenTree tree)
    {
        // TODO: Port Compiler.gtSetEvalOrderMinOpts
        return 0;
    }

    /// <summary>A wrapper for gtSetEvalOrder and gtComputeFPlvls</summary>
    /// <param name="stmt"></param>
    /// <remarks>Necessary because the FP levels may need to be re-computed if we reverse operands</remarks>
    public void gtSetStmtInfo(Statement stmt) => gtSetEvalOrder(stmt.RootNode);

    /// <summary>Converts an annotated token into an icon flags (so that we will later be able to tell the type of the handle that will be embedded in the icon node)</summary>
    /// <param name="token"></param>
    /// <returns></returns>
    public GenTreeFlags gtTokenToIconFlags(int token) => TypeFromToken(token) switch {
        mdtTypeRef => GTF_ICON_CLASS_HDL,
        mdtTypeDef => GTF_ICON_CLASS_HDL,
        mdtTypeSpec => GTF_ICON_CLASS_HDL,
        mdtMethodDef => GTF_ICON_METHOD_HDL,
        mdtFieldDef => GTF_ICON_FIELD_HDL,
        _ => GTF_ICON_TOKEN_HDL,
    };

    /// <summary>Check if a tree contains any async call.</summary>
    /// <param name="tree">The tree to check</param>
    /// <returns>True if any node in the tree is an async call, false otherwise.</returns>
    public bool gtTreeContainsAsyncCall(GenTree tree)
    {
        if (!compIsAsync)
        {
            return false;
        }
        return gtFindNodeInTree(tree, gtIsAsyncCall, GTF_CALL) is not null;
    }

    /// <summary>Check if a tree contains any tail call or tail call candidate.</summary>
    /// <param name="tree">The tree</param>
    /// <returns>true if any node in the tree is a tail call or tail call candidate; false otherwise.</returns>
    /// <remarks>While tail calls are generally expected to be top level nodes we do allow some other shapes of calls to be tail calls, including some cascading trivial assignments and casts. This function does a tree walk to check if any sub tree is a tail call.</remarks>
    public bool gtTreeContainsTailCall(GenTree tree)
    {
        return gtFindNodeInTree(tree, gtIsTailCall, GTF_CALL) is not null;
    }

    public bool gtTreeHasSideEffects(GenTree tree, GenTreeFlags flags, bool ignoreCctors = false)
    {
        // These are the side effect flags that we care about for this tree
        var sideEffectFlags = tree.Flags & flags;

        // Does this tree have any Side-effect flags set that we care about?
        if (sideEffectFlags == 0)
        {
            // no it doesn't..
            return false;
        }

        if ((sideEffectFlags is GTF_CALL) && tree.Oper.IsCall && tree.AsCall().IsHelperCall())
        {
            // Generally all trees that contain GT_CALL nodes are considered to have side-effects.
            // However, for some pure helper calls we lie about this.
            if (gtNodeHasSideEffects(tree, flags, ignoreCctors))
            {
                return true;
            }

            // The GTF_CALL may be contributed by an operand, so check for that.
            var hasCallInOperand = false;

            _ = tree.VisitOperands((tree) => {
                if (gtTreeHasSideEffects(tree, GTF_CALL, ignoreCctors))
                {
                    hasCallInOperand = true;
                    return GenTree.VisitResult.Abort;
                }
                return GenTree.VisitResult.Continue;
            });

            return hasCallInOperand;
        }

        return true;
    }

    /// <summary>Given an unused value type box, try and remove the upstream allocation and unnecessary parts of the copy.</summary>
    /// <param name="box">the box node to optimize</param>
    /// <param name="options">controls whether and how trees are modified</param>
    /// <returns>A tree representing the original value to box, if removal is successful/possible (but see note). null if removal fails.</returns>
    /// <remarks>
    ///   <para>Value typed box gets special treatment because it has associated side effects that can be removed if the box result is not used.</para>
    ///   <para>By default (options == BR_REMOVE_AND_NARROW) this method will try and remove unnecessary trees and will try and reduce remaining operations to the minimal set, possibly narrowing the width of loads from the box source if it is a struct.</para>
    ///   <para>To perform a trial removal, pass BR_DONT_REMOVE. This can be useful to determine if this optimization should only be performed if some other conditions hold true.</para>
    ///   <para>To remove but not alter the access to the box source, pass BR_REMOVE_BUT_NOT_NARROW.</para>
    ///   <para>To remove and return the tree for the type handle used for the boxed newobj, pass BR_REMOVE_BUT_NOT_NARROW_WANT_TYPE_HANDLE. This can be useful when the only part of the box that is "live" is its type.</para>
    ///   <para>If removal fails, it is possible that a subsequent pass may be able to optimize.  Blocking side effects may now be minimized (null or bounds checks might have been removed) or might be better known (inline return placeholder updated with the actual return expression). So the box is perhaps best left as is to help trigger this re-examination.</para>
    /// </remarks>
    public unsafe GenTree? gtTryRemoveBoxUpstreamEffects(GenTreeBox box, BoxRemovalOptions options = BR_REMOVE_AND_NARROW)
    {
        assert(box.IsBoxedValue);

        // grab related parts for the optimization
        var allocStmt = box.DefStmtWhenInlinedBoxValue;
        var copyStmt = box.CopyStmtWhenInlinedBoxValue;

        JITDUMP($"gtTryRemoveBoxUpstreamEffects: {((options == BR_DONT_REMOVE) ? "checking if it is possible" : "attempting")} of BOX (valuetype) [{box.TreeId:D6}] (assign/newobj {FMT_STMT(allocStmt.Id)} copy {FMT_STMT(copyStmt.Id)}\n");

        // If we don't recognize the form of the store, bail.
        var boxLclDef = allocStmt.RootNode;

        if (boxLclDef.Oper is not GT_STORE_LCL_VAR)
        {
            JITDUMP($" bailing; unexpected alloc def op {boxLclDef.Oper}\n");
            return null;
        }

        // If this box is no longer single-use, bail.
        if (box.WasCloned)
        {
            JITDUMP(" bailing; unsafe to remove box that has been cloned\n");
            return null;
        }

        // If we're eventually going to return the type handle, remember it now.
        var boxTypeHandle = null as GenTree;

        if ((options == BR_REMOVE_AND_NARROW_WANT_TYPE_HANDLE) || (options == BR_DONT_REMOVE_WANT_TYPE_HANDLE))
        {
            var defSrc = boxLclDef.AsLclVar().Data;
            var defSrcOper = defSrc.Oper;

            // Allocation may be via AllocObj or via helper call, depending
            // on when this is invoked and whether the jit is using AllocObj
            // for R2R allocations.
            if (defSrcOper == GT_ALLOCOBJ)
            {
                var allocObj = defSrc.AsAllocObj();
                boxTypeHandle = allocObj.AsOp().Op1;
            }
            else if (defSrcOper == GT_CALL)
            {
                var newobjCall = defSrc.AsCall();

                // In R2R expansions the handle may not be an explicit operand to the helper,
                // so we can't remove the box.
                if (newobjCall.Args.IsEmpty)
                {
                    assert(newobjCall.IsHelperCall(CORINFO_HELP_READYTORUN_NEW));
                    JITDUMP(" bailing; newobj via R2R helper\n");
                    return null;
                }

                var callArg = newobjCall.Args.GetArgByIndex(0);
                assert(callArg is not null);
                boxTypeHandle = callArg.Node;
            }
            else
            {
                unreached();
            }

            assert(boxTypeHandle is not null);
        }

        // If we don't recognize the form of the copy, bail.
        var copy = copyStmt.RootNode;

        if (copy.Oper is not GT_STOREIND and not GT_STORE_BLK)
        {
            // GT_RET_EXPR is a tolerable temporary failure.
            // The jit will revisit this optimization after
            // inlining is done.
            if (copy.Oper is GT_RET_EXPR)
            {
                JITDUMP($" bailing; must wait for replacement of copy {copy.Oper}\n");
            }
            else
            {
                // Anything else is a missed case we should figure out how to handle.
                // One known case is GT_COMMAs enclosing the store we are looking for.
                JITDUMP($" bailing; unexpected copy op {copy.Oper}\n");
            }
            return null;
        }

        // If the copy is a struct copy, make sure we know how to isolate any source side effects.
        var copySrc = copy.Data;

        // If the copy source is from a pending inline, wait for it to resolve.
        if (copySrc.Oper is GT_RET_EXPR)
        {
            JITDUMP($" bailing; must wait for replacement of copy source {copySrc.Oper}\n");
            return null;
        }

        var hasSrcSideEffect = false;
        var isStructCopy = false;

        if (gtTreeHasSideEffects(copySrc, GTF_SIDE_EFFECT))
        {
            hasSrcSideEffect = true;

            if (varTypeIsStruct(copySrc.Type))
            {
                isStructCopy = true;

                if (copySrc.Oper is not GT_IND and not GT_BLK)
                {
                    // We don't know how to handle other cases, yet.
                    JITDUMP($" bailing; unexpected copy source struct op with side effect {copySrc.Oper}\n");
                    return null;
                }
            }
        }

        // If this was a trial removal, we're done.
        if (options is BR_DONT_REMOVE)
        {
            return copySrc;
        }

        if (options is BR_DONT_REMOVE_WANT_TYPE_HANDLE)
        {
            return boxTypeHandle;
        }

        // Otherwise, proceed with the optimization.
        //
        // Change the store expression to a NOP.
        JITDUMP($"\nBashing NEWOBJ [{boxLclDef.TreeId:D6}] to NOP\n");
        allocStmt.RootNode = gtNewNothingNode();
        DEBUG_DESTROY_NODE(boxLclDef);

        // Change the copy expression so it preserves key
        // source side effects.
        JITDUMP($"\nBashing COPY [{copy.TreeId:D6}]");

        if (!hasSrcSideEffect)
        {
            // If there were no copy source side effects just bash the copy to a NOP.
            JITDUMP(" to NOP; no source side effects.\n");
            copyStmt.RootNode = gtNewNothingNode();
            DEBUG_DESTROY_NODE(copy);
        }
        else if (!isStructCopy)
        {
            // For scalar types, go ahead and produce the
            // value as the copy is fairly cheap and likely
            // the optimizer can trim things down to just the
            // minimal side effect parts.
            copyStmt.RootNode = copySrc;
            JITDUMP($" to scalar read via [{copySrc.TreeId:D6}]\n");
        }
        else
        {
            // For struct types read the first byte of the source struct; there's
            // no need to read the entire thing, and no place to put it.
            assert(copySrc.Oper.IsLoad);
            copyStmt.RootNode = copySrc;

            if (options is BR_REMOVE_AND_NARROW or BR_REMOVE_AND_NARROW_WANT_TYPE_HANDLE)
            {
                JITDUMP($" to read first byte of struct via modified [{copySrc.TreeId:D6}]\n");

                if (copySrc.Oper is GT_IND)
                {
                    copySrc.Type = TYP_BYTE;
                }
                else
                {
                    var indir = gtNewIndir(TYP_BYTE, copySrc.AsIndir().Addr, copySrc.Flags);
                    copy.DataRef = indir;
                    copySrc = indir;
                    DEBUG_DESTROY_NODE(copySrc);
                }
            }
            else
            {
                JITDUMP($" to read entire struct via modified [{copySrc.TreeId:D6}]\n");
            }
        }

        if (fgNodeThreading == NodeThreading.AllTrees)
        {
            fgSetStmtSeq(allocStmt);
            fgSetStmtSeq(copyStmt);
        }

        // Box effects were successfully optimized.

        if (options == BR_REMOVE_AND_NARROW_WANT_TYPE_HANDLE)
        {
            return boxTypeHandle;
        }
        else
        {
            return copySrc;
        }
    }

    public GenTree gtUnusedValNode(GenTree expr)
    {
        return gtNewCommaNode(TYP_VOID, expr, gtNewNothingNode());
    }

    /// <summary>Update the side effects based on the node operation.</summary>
    /// <param name="tree">Tree to update the side effects on</param>
    /// <remarks>
    ///   <para>This method currently only updates GTF_EXCEPT, GTF_ASG, and GTF_CALL flags.</para>
    ///   <para>The other side effect flags may remain unnecessarily (conservatively) set.</para>
    ///   <para>The caller of this method is expected to update the flags based on the children's flags.</para>
    /// </remarks>
    public void gtUpdateNodeOperSideEffects(GenTree tree)
    {
        var oper = tree.Oper;
        var flags = tree.Flags;

        if (tree.MayThrow(this))
        {
            flags |= GTF_EXCEPT;
        }
        else
        {
            flags &= ~GTF_EXCEPT;

            if (oper.IsIndirOrArrMetaData)
            {
                flags |= GTF_IND_NONFAULTING;
            }
        }

        if (tree.RequiresAsgFlag)
        {
            flags |= GTF_ASG;
        }
        else
        {
            flags &= ~GTF_ASG;
        }

        if (tree.RequiresCallFlag(this))
        {
            flags |= GTF_CALL;
        }
        else
        {
            flags &= ~GTF_CALL;
        }

        tree.Flags = flags;
    }

    /// <summary>Update the side effects based on the node operation and children's side efects.</summary>
    /// <param name="tree">Tree to update the side effects on</param>
    /// <remarks>
    ///   <para>This method currently only updates GTF_EXCEPT, GTF_ASG, and GTF_CALL flags.</para>
    ///   <para>The other side effect flags may remain unnecessarily (conservatively) set.</para>
    /// </remarks>
    public void gtUpdateNodeSideEffects(GenTree tree)
    {
        gtUpdateNodeOperSideEffects(tree);
        _ = tree.VisitOperands((operand) => {
            tree.Flags |= (operand.Flags & GTF_ALL_EFFECT);
            return GenTree.VisitResult.Continue;
        });
    }
}
