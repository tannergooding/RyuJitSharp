// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;
using System.Security.AccessControl;
using System.Text;
using System.Xml.Linq;

namespace RyuJitSharp;

public partial class Compiler
{
    // TODO: Port Compiler.gtMarkColonCond
    // public static unsafe fgWalkPreFn gtMarkColonCond;

    // TODO: Port Compiler.gtClearColonCond
    // public static unsafe fgWalkPreFn gtClearColonCond;

    /// <summary>Get the tree corresponding to the address of the retbuf that this call defines.</summary>
    /// <param name="call">The call node</param>
    /// <returns>A tree representing the address of a local.</returns>
    public GenTreeLclVarCommon? gtCallGetDefinedRetBufLclAddr(GenTreeCall call)
    {
        if (!call.IsOptimizingRetBufAsLocal)
        {
            return null;
        }

        var retBufArg = call.Args.RetBufferArg;
        assert(retBufArg is not null);

        var node = retBufArg.Node;

        switch (node.Oper)
        {
            // Get the value from putarg wrapper nodes
            case GT_PUTARG_REG:
            case GT_PUTARG_STK:
            {
                node = node.AsOp().Op1;
                break;
            }

            default:
            {
                break;
            }
        }

        // This may be called very late to check validity of LIR.
        node = node.SkipCopyOrReload;

#if DEBUG
        assert((node.Oper is GT_LCL_ADDR) && lvaGetDesc(node.AsLclVarCommon().LclNum).IsDefinedViaAddress);
#endif
        return node.AsLclVarCommon();
    }

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

    //------------------------------------------------------------------------
    // gtCanSwapOrder: 
    //
    // Arguments:
    //    firstNode  - An operand of a tree that can have GTF_REVERSE_OPS set.
    //    secondNode - The other operand of the tree.
    //
    // Return Value:
    //    Returns a boolean indicating whether it is safe to reverse the execution
    //    order of the two trees, considering any exception, global effects, or
    //    ordering constraints.
    //
    /// <summary>Returns true iff the secondNode can be swapped with firstNode.</summary>
    /// <param name="firstNode"></param>
    /// <param name="secondNode"></param>
    /// <returns></returns>
    public bool gtCanSwapOrder(GenTree firstNode, GenTree secondNode)
    {
        var canSwap = true;

        // Don't swap "CONST_HDL op CNS"
        if (firstNode.IsIconHandle() && secondNode.Oper.IsIntegralConst)
        {
            canSwap = false;
        }

        // Relative of order of global / side effects can't be swapped.

        if (optValnumCSE_phase)
        {
            canSwap = optCSE_canSwap(firstNode, secondNode);
        }

        // We cannot swap in the presence of special side effects such as GT_CATCH_ARG.

        if (canSwap && ((firstNode.Flags & GTF_ORDER_SIDEEFF) is not 0))
        {
            canSwap = false;
        }

        // When strict side effect order is disabled we allow GTF_REVERSE_OPS to be set
        // when one or both sides contains a GTF_CALL or GTF_EXCEPT.
        // Currently only the C and C++ languages allow non strict side effect order.

        var strictEffects = GTF_GLOB_EFFECT;

        if (canSwap && ((firstNode.Flags & strictEffects) is not 0))
        {
            // op1 has side efects that can't be reordered.
            // Check for some special cases where we still may be able to swap.

            if ((secondNode.Flags & strictEffects) is not 0)
            {
                // op2 has also has non reorderable side effects - can't swap.
                canSwap = false;
            }
            else
            {
                // No side effects in op2 - we can swap iff op1 has no way of modifying op2,
                // i.e. through indirect stores or calls or op2 is a constant.

                if ((firstNode.Flags & strictEffects & GTF_PERSISTENT_SIDE_EFFECTS) is not 0)
                {
                    // We have to be conservative - can swap iff op2 is constant.
                    if (!secondNode.Oper.IsInvariant)
                    {
                        canSwap = false;
                    }
                }
            }
        }
        return canSwap;
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

#if FEATURE_MASKED_HW_INTRINSICS
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
                        hwintrinsic.SimdBaseType,
                        hwintrinsic.SimdSize,
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

    public GenTreeLclVarCommon gtCloneLclVarCommon(GenTreeLclVarCommon lclVarCommon)
    {
        if (lclVarCommon.Oper is GT_LCL_VAR)
        {
            return gtCloneLclVar(lclVarCommon.AsLclVar());
        }
        else
        {
            assert(lclVarCommon.Oper is GT_LCL_FLD);
            return gtCloneLclFld(lclVarCommon.AsLclFld());
        }
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
    //------------------------------------------------------------------------
    // gtDispArgList: Dump the tree for a call arg list
    //
    // Arguments:
    //    call            - the call to dump arguments for
    //    lastCallOperand - the call's last operand (to determine the arc types)
    //    indentStack     - the specification for the current level of indentation & arcs
    //
    // Return Value:
    //    None.
    //
    public void gtDispArgList(GenTreeCall call, GenTree lastCallOperand, ref IndentStack indentStack)
    {
        foreach (var arg in call.Args.EarlyArgs)
        {
            var earlyNode = arg.EarlyNode;
            assert(earlyNode is not null);

            var buf = gtGetArgMsg(call, arg);
            gtDispChild(earlyNode, ref indentStack, (earlyNode == lastCallOperand) ? IIArcBottom : IIArc, buf, topOnly: false);
        }
    }

    /// <summary>dumps all statements inside `block`.</summary>
    /// <param name="block">the block to display statements for.</param>
    public void gtDispBlockStmts(BasicBlock block)
    {
        foreach (var stmt in block.Statements)
        {
            gtDispStmt(stmt);
            jitprintf("\n");
        }
    }

    /// <summary>Print a child node to jitstdout.</summary>
    /// <param name="child">the tree to be printed</param>
    /// <param name="indentStack">the specification for the current level of indentation &amp; arcs</param>
    /// <param name="arcType">the type of arc to use for this child</param>
    /// <param name="msg">a contextual method (i.e. from the parent) to print</param>
    /// <param name="topOnly">a boolean indicating whether to print the children, or just the top node</param>
    public void gtDispChild(GenTree child, ref IndentStack indentStack, IndentInfo arcType, string msg = "", bool topOnly = false)
    {
        indentStack.Push(arcType);
        gtDispTree(child, ref indentStack, msg, topOnly);
        _ = indentStack.Pop();
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

    public void gtDispCommonEndLine(GenTree tree)
    {
        // Utility function that prints the following node information
        //   1: The associated zero field sequence (if any)
        //   2. The register assigned to this node (if any)
        //   2. The value number assigned (if any)
        //   3. A newline character

        gtDispRegVal(tree);
        gtDispVN(tree);
        jitprintf("\n");
    }

    public unsafe void gtDispConst(GenTree tree)
    {
        assert(tree.Oper.IsConst);

        switch (tree.Oper)
        {
            case GT_CNS_INT:
            {
                var intCon = tree.AsIntCon();

                if (intCon.IsIconHandle(GTF_ICON_STR_HDL))
                {
                    jitprintf($" 0x{dspOffset(intCon.IconVal):X}[ICON_STR_HDL]");
                }
                else if (intCon.IsIconHandle(GTF_ICON_OBJ_HDL))
                {
                    eePrintObjectDescription(" ", (CORINFO_OBJECT_HANDLE)(intCon.IconValue));
                }
                else
                {
                    var iconVal = intCon.IconVal;
                    var dspIconVal = intCon.IsIconHandle() ? dspPtr(unchecked((void*)(iconVal))) : iconVal;

                    if (intCon.Type is TYP_REF)
                    {
                        if (iconVal is 0)
                        {
                            jitprintf(" null");
                        }
                        else
                        {
                            jitprintf($" 0x{dspIconVal:x}");
                        }
                    }
                    else if ((iconVal > -1000) && (iconVal < 1000))
                    {
                        jitprintf($" {dspIconVal}");
                    }
#if TARGET_64BIT
                    else if ((iconVal & unchecked((long)(0xFFFFFFFF_00000000))) is not 0)
                    {
                        if (dspIconVal >= 0)
                        {
                            jitprintf($" 0x{dspIconVal:x}");
                        }
                        else
                        {
                            jitprintf($" -0x{-dspIconVal:x}");
                        }
                    }
#endif
                    else
                    {
                        if (dspIconVal >= 0)
                        {
                            jitprintf($" 0x{dspIconVal:X}");
                        }
                        else
                        {
                            jitprintf($" -0x{-dspIconVal:X}");
                        }
                    }

                    var description = intCon.IconHandleFlag switch {
                        GTF_EMPTY => "",
                        GTF_ICON_SCOPE_HDL => " scope",
                        GTF_ICON_CLASS_HDL => " class",
                        GTF_ICON_METHOD_HDL => " method",
                        GTF_ICON_FIELD_HDL => " field",
                        GTF_ICON_STATIC_HDL => " static",
                        GTF_ICON_STR_HDL => " string",
                        GTF_ICON_OBJ_HDL => " object",
                        GTF_ICON_CONST_PTR => " const ptr",
                        GTF_ICON_GLOBAL_PTR => " global ptr",
                        GTF_ICON_VARG_HDL => " vararg",
                        GTF_ICON_PINVKI_HDL => " pinvoke",
                        GTF_ICON_TOKEN_HDL => " token",
                        GTF_ICON_TLS_HDL => " tls",
                        GTF_ICON_FTN_ADDR => " ftn",
                        GTF_ICON_CIDMID_HDL => " cid/mid",
                        GTF_ICON_BBC_PTR => " bbc",
                        GTF_ICON_STATIC_BOX_PTR => " static box ptr",
                        GTF_ICON_FIELD_SEQ => " field seq",
                        GTF_ICON_STATIC_ADDR_PTR => " static base addr cell",
                        GTF_ICON_SECREL_OFFSET => " relative offset in section",
                        GTF_ICON_TLSGD_OFFSET => " tls global dynamic offset",
                        _ => " ILLEGAL",
                    };
                    jitprintf(description);

                    // Print additional details for some handles.
                    switch (intCon.IconHandleFlag)
                    {
                        case GTF_ICON_CLASS_HDL:
                        {
                            if (!IsAot)
                            {
                                jitprintf($" {eeGetClassName((CORINFO_CLASS_HANDLE)(iconVal))}");
                            }
                            break;
                        }

                        case GTF_ICON_METHOD_HDL:
                        {
                            if (!IsAot)
                            {
                                jitprintf($" {eeGetMethodFullName((CORINFO_METHOD_HANDLE)(iconVal))}");
                            }
                            break;
                        }

                        case GTF_ICON_FIELD_HDL:
                        {
                            if (!IsAot)
                            {
                                jitprintf($" {eeGetFieldName((CORINFO_FIELD_HANDLE)(iconVal), true)}");
                            }
                            break;
                        }

                        default:
                        {
                            break;
                        }
                    }

#if FEATURE_SIMD
                    if ((tree.Flags & GTF_ICON_SIMD_COUNT) is not 0)
                    {
                        jitprintf(" vector element count");
                    }
#endif

                    if (tree.IsReuseRegVal)
                    {
                        jitprintf(" reuse reg val");
                    }
                }

                var fieldSeq = tree.AsIntCon().FieldSeq;

                if (fieldSeq is not null)
                {
                    gtDispFieldSeq(fieldSeq, tree.AsIntCon().IconValue - fieldSeq.Offset);
                }
                break;
            }

            case GT_CNS_LNG:
            {
                jitprintf($" 0x{tree.AsLngCon().LconValue:x16}");
                break;
            }

            case GT_CNS_DBL:
            {
                var dcon = tree.AsDblCon().DconVal;

                if (double.IsNegative(dcon) && (dcon == 0))
                {
                    jitprintf(" -0.00000");
                }
                else if (double.IsNaN(dcon))
                {
                    var bits = BitConverter.DoubleToInt64Bits(dcon);
                    jitprintf($" {dcon:G17}(0x{bits:x16})\n");
                }
                else
                {
                    jitprintf($" {dcon:G17}");
                }
                break;
            }

            case GT_CNS_STR:
            {
                var cnsStr = tree.AsStrCon();

                if (cnsStr.IsStringEmptyField)
                {
                    // Special case: do not call getStringLiteral for the empty string field
                    jitprintf("\"\"");
                    break;
                }

                eePrintStringLiteral(cnsStr.ScpHnd, cnsStr.SconCpx);
                break;
            }

#if FEATURE_SIMD
            case GT_CNS_VEC:
            {
                var vecCon = tree.AsVecCon();

                switch (vecCon.Type)
                {
                    case TYP_SIMD8:
                    {
                        jitprintf($"<0x{vecCon.SimdVal.u32[0]:x8}, 0x{vecCon.SimdVal.u32[1]:x8}>");
                        break;
                    }

                    case TYP_SIMD12:
                    {
                        jitprintf($"<0x{vecCon.SimdVal.u32[0]:x8}, 0x{vecCon.SimdVal.u32[1]:x8}, 0x{vecCon.SimdVal.u32[2]:x8}>");
                        break;
                    }

                    case TYP_SIMD16:
                    {
                        jitprintf($"<0x{vecCon.SimdVal.u32[0]:x8}, 0x{vecCon.SimdVal.u32[1]:x8}, 0x{vecCon.SimdVal.u32[2]:x8}, 0x{vecCon.SimdVal.u32[3]:x8}>");
                        break;
                    }

#if TARGET_XARCH
                    case TYP_SIMD32:
                    {
                        jitprintf($"<0x{vecCon.SimdVal.u64[0]:x16}, 0x{vecCon.SimdVal.u64[1]:x16}, 0x{vecCon.SimdVal.u64[2]:x16}, 0x{vecCon.SimdVal.u64[3]:x16}>");
                        break;
                    }

                    case TYP_SIMD64:
                    {
                        jitprintf($"<0x{vecCon.SimdVal.u64[0]:x16}, 0x{vecCon.SimdVal.u64[1]:x16}, 0x{vecCon.SimdVal.u64[2]:x16}, 0x{vecCon.SimdVal.u64[3]:x16}, 0x{vecCon.SimdVal.u64[4]:x16}, 0x{vecCon.SimdVal.u64[5]:x16}, 0x{vecCon.SimdVal.u64[6]:x16}, 0x{vecCon.SimdVal.u64[7]:x16}>");
                        break;
                    }
#endif

                    default:
                    {
                        unreached();
                        break;
                    }
                }
                break;
            }
#endif

#if FEATURE_MASKED_HW_INTRINSICS
            case GT_CNS_MSK:
            {
                var mskCon = tree.AsMskCon();
                jitprintf($"<0x{mskCon.SimdMaskVal.u32[0]:x8}, 0x{mskCon.SimdMaskVal.u32[1]:x8}>");
                break;
            }
#endif // FEATURE_MASKED_HW_INTRINSICS

            default:
            {
                NO_WAY("unexpected constant node");
                break;
            }
        }
    }

    /// <summary>Print out the fields in this field sequence.</summary>
    /// <param name="fieldSeq">The field sequence</param>
    /// <param name="offset">Offset of the (implicit) struct fields in the sequence</param>
    public unsafe void gtDispFieldSeq(FieldSeq fieldSeq, nint offset)
    {
        if (fieldSeq is null)
        {
            return;
        }

        jitprintf($" Fseq[{eeGetFieldName(fieldSeq.FieldHandle, includeType: false)}");
        if (offset is not 0)
        {
            jitprintf($", {offset}");
        }
        jitprintf("]");
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
            jitprintf(new string(' ', int.Max(0, LONGEST_COMMON_LCL_VAR_DISPLAY_LENGTH - name.Length) + 1));
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

    /// <summary>Print a single leaf node to jitstdout.</summary>
    /// <param name="tree">the tree to be printed</param>
    /// <param name="indentStack">the specification for the current level of indentation &amp; arcs</param>
    public unsafe void gtDispLeaf(GenTree tree, ref IndentStack indentStack)
    {
        if (tree.Oper.IsConst)
        {
            gtDispConst(tree);
            return;
        }

        switch (tree.Oper)
        {
            case GT_PHI_ARG:
            case GT_LCL_VAR:
            case GT_LCL_FLD:
            case GT_LCL_ADDR:
            {
                gtDispLocal(tree.AsLclVarCommon(), ref indentStack);
                break;
            }

            case GT_JMP:
            {
                jitprintf($" {eeGetMethodFullName((CORINFO_METHOD_HANDLE)(tree.AsVal().Val1), includeReturnType: true, includeThisSpecifier: true)}");
            }
            break;

            case GT_LABEL:
            {
                break;
            }

            case GT_FTN_ADDR:
            {
                jitprintf($" {eeGetMethodFullName((CORINFO_METHOD_HANDLE)(tree.AsFptrVal().FptrMethod), includeReturnType: true, includeThisSpecifier: true)}\n");
            }
            break;

            // Vanilla leaves. No qualifying information available. So do nothing

            case GT_NOP:
            case GT_NO_OP:
            case GT_START_NONGC:
            case GT_START_PREEMPTGC:
            case GT_PROF_HOOK:
            case GT_CATCH_ARG:
            case GT_ASYNC_CONTINUATION:
            case GT_FTN_ENTRY:
            case GT_MEMORYBARRIER:
            case GT_JMPTABLE:
#if SWIFT_SUPPORT
            case GT_SWIFT_ERROR:
#endif
            case GT_GCPOLL:
#if TARGET_WASM
            case GT_WASM_THROW_REF:
            case GT_WASM_JEXCEPT:
#endif
            {
                break;
            }

            case GT_RET_EXPR:
            {
                var retExpr = tree.AsRetExpr();
                var inlineCand = retExpr.InlineCandidate;

                jitprintf("(for ");
                printTreeId(inlineCand);
                jitprintf(")");

                if (retExpr.SubstExpr is not null)
                {
                    jitprintf(" -> ");
                    printTreeId(retExpr.SubstExpr);
                }
            }
            break;

            case GT_PHYSREG:
            {
                jitprintf($" {tree.AsPhysReg().SrcReg.Name}");
                break;
            }

            case GT_IL_OFFSET:
            {
                jitprintf(" ");
                tree.AsILOffset().StmtDebugInfo.Dump(recurse: true);
                break;
            }

            case GT_RECORD_ASYNC_RESUME:
            case GT_ASYNC_RESUME_INFO:
            {
                jitprintf($" state={tree.AsVal().Val1}");
                break;
            }

            case GT_JCC:
            case GT_SETCC:
            {
                jitprintf($" cond={tree.AsCC().Condition.Name}");
                break;
            }

            case GT_JCMP:
            case GT_JTEST:
            {
                jitprintf($" cond={tree.AsOpCC().Condition.Name}");
                break;
            }

            default:
            {
                NO_WAY("don't know how to display tree leaf node");
                break;
            }
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
        gtDispTree(node, ref indentStack, "", topOnly, isLIR);

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

    /// <summary>Print description of a local node to jitstdout.</summary>
    /// <param name="tree">the local tree</param>
    /// <param name="indentStack">the specification for the current level of indentation &amp; arcs</param>
    /// <remarks>Prints the information common to all local nodes. Does not print children.</remarks>
    public void gtDispLocal(GenTreeLclVarCommon tree, ref IndentStack indentStack)
    {
        jitprintf(" ");

        var varNum = tree.LclNum;
        ref var varDsc = ref lvaGetDesc(varNum);
        var isDef = (tree.Flags & GTF_VAR_DEF) is not 0;
        var isLclFld = tree.Oper.IsLocalField;

        gtDispLclVar(varNum);
        gtDispSsaName(varNum, tree.SsaNum, isDef);

        if (isLclFld)
        {
            jitprintf($"[+{tree.AsLclFld().LclOffs}]");
        }

        if (varDsc.lvRegister)
        {
            jitprintf(" ");
            varDsc.PrintVarReg();
        }
        else if (tree.InReg)
        {
            jitprintf($" {compRegVarName(tree.RegNum)}");
        }

        if (varDsc.lvPromoted)
        {
            if (!varTypeIsPromotable(varDsc.Type) && !varDsc.lvUnusedStruct)
            {
                // Promoted implicit byrefs can get in this state while they are being rewritten
                // in global morph.
            }
            else
            {
                for (var index = 0; index < varDsc.lvFieldCnt; index++)
                {
                    var fieldLclNum = varDsc.lvFieldLclStart + index;
                    ref var fieldVarDsc = ref lvaGetDesc(fieldLclNum);

                    jitprintf("\n");
                    jitprintf("                                                            ");
                    indentStack.Print();
                    jitprintf($"    {fieldVarDsc.Type.Name,-6} {fieldVarDsc.lvReason} -> ");
                    gtDispLclVar(fieldLclNum);
                    gtDispSsaName(fieldLclNum, tree.GetSsaNum(this, index), isDef);

                    if (fieldVarDsc.lvRegister)
                    {
                        jitprintf(" ");
                        fieldVarDsc.PrintVarReg();
                    }

                    if (fieldVarDsc.lvTracked && fgLocalVarLivenessDone && tree.IsLastUse(index))
                    {
                        jitprintf(" (last use)");
                    }
                }
            }
        }
        else
        {
            // a normal not-promoted lclvar

            if ((varDsc.lvTracked || varDsc.lvTrackedWithoutIndex) && fgLocalVarLivenessDone && ((tree.Flags & GTF_VAR_DEATH) is not 0))
            {
                jitprintf(" (last use)");
            }
        }
    }

    /// <summary>determine how many registers to print for a multi-reg node</summary>
    /// <param name="tree">GenTree node whose registers we want to print</param>
    /// <returns>The number of registers to print</returns>
    /// <remarks> This is not the same in all cases as GenTree.GetMultiRegCount(). In particular, for COPY or RELOAD it only returns the number of *valid* registers, and for CALL, it will return 0 if the ReturnTypeDesc hasn't yet been initialized. But we want to print all register positions.</remarks>
    public byte gtDispMultiRegCount(GenTree tree)
    {
        if (tree.Oper.IsCopyOrReload)
        {
            // GetRegCount() will return only the number of valid regs for COPY or RELOAD,
            // but we want to print all positions, so we get the reg count for op1.
            return gtDispMultiRegCount(tree.AsCopyOrReload().Op1);
        }
        else if (!tree.IsMultiRegNode)
        {
            // We can wind up here because IsMultiRegNode() always returns true for COPY or RELOAD,
            // even if its op1 is not multireg.
            // Note that this method won't be called for non-register-producing nodes.
            return 1;
        }
        else if (tree.Oper is GT_CALL)
        {
            var regCount = tree.AsCall().ReturnTypeDesc.ReturnRegCount;

            // If it hasn't yet been initialized, we'd still like to see the registers printed.
            if (regCount is 0)
            {
                regCount = MAX_RET_REG_COUNT;
            }
            return regCount;
        }
        else
        {
            return tree.GetMultiRegCount(this);
        }
    }

    /// <summary>Print a tree to jitstdout.</summary>
    /// <param name="tree">the tree to be printed</param>
    /// <param name="indentStack">the specification for the current level of indentation &amp; arcs</param>
    /// <param name="msg">a contextual method (i.e. from the parent) to print</param>
    /// <param name="isLIR">'indentStack' may be null, in which case no indentation or arcs are printed 'msg' may be null</param>
    public unsafe void gtDispNode(GenTree tree, ref IndentStack indentStack, string msg, bool isLIR)
    {
        var printFlags = true; // always true..
        var msgLength = 35;
        var prev = null as GenTree;

        if (tree._seqNum is not 0)
        {
            jitprintf($"N{tree._seqNum:D3} ");

            if (tree._costsInitialized)
            {
                jitprintf($"({tree.CostEx,3},{tree.CostSz,3}) ");
            }
            else
            {
                // This probably indicates a bug: the node has a sequence number, but not costs.
                jitprintf("(???,???) ");
            }
        }
        else
        {
            prev = tree;

            var hasSeqNum = true;
            var dotNum = 0;

            do
            {
                dotNum++;
                prev = prev.Prev;

                if ((prev is null) || (prev == tree))
                {
                    hasSeqNum = false;
                    break;
                }
            }
            while (prev._seqNum is 0);

            // If we have an indent stack, don't add additional characters,
            // as it will mess up the alignment.
            var displayDotNum = hasSeqNum && (indentStack.Depth == 0);

            if (displayDotNum)
            {
                assert(prev is not null);
                jitprintf($"N{prev._seqNum:D3}.{dotNum:D2} ");
            }
            else
            {
                jitprintf("     ");
            }

            if (tree._costsInitialized)
            {
                jitprintf($"({tree.CostEx,3},{tree.CostSz,3}) ");
            }
            else
            {
                if (displayDotNum)
                {
                    // Do better alignment in this case
                    jitprintf("       ");
                }
                else
                {
                    jitprintf("          ");
                }
            }
        }

        if (optValnumCSE_phase)
        {
            if (IS_CSE_INDEX(tree._cseNum))
            {
                jitprintf($"{FMT_CSE(GET_CSE_INDEX(tree._cseNum))} ({(IS_CSE_USE(tree._cseNum) ? "use" : "def")})");
            }
            else
            {
                jitprintf("             ");
            }
        }

        // Print the node ID
        printTreeId((JitConfig[ConfigInteger.JitDumpTreeIDs] is not 0) ? tree : null);
        jitprintf(" ");

        if (tree.Oper >= GT_COUNT)
        {
            jitprintf(" **** ILLEGAL NODE ****");
            return;
        }

        if (printFlags)
        {
            // First print the flags associated with the node

            switch (tree.Oper)
            {
                case GT_BLK:
                case GT_IND:
                case GT_STOREIND:
                case GT_STORE_BLK:
                {
                    // We prefer printing V or U
                    if ((tree.Flags & (GTF_IND_VOLATILE | GTF_IND_UNALIGNED)) is 0)
                    {
                        if ((tree.Flags & GTF_IND_TGT_NOT_HEAP) is not 0)
                        {
                            jitprintf("s");
                            --msgLength;
                            break;
                        }
                        else if ((tree.Flags & GTF_IND_TGT_HEAP) is not 0)
                        {
                            jitprintf("h");
                            --msgLength;
                            break;
                        }
                        else if ((tree.Flags & GTF_IND_INITCLASS) is not 0)
                        {
                            jitprintf("I");
                            --msgLength;
                            break;
                        }
                        else if ((tree.Flags & GTF_IND_INVARIANT) is not 0)
                        {
                            jitprintf("#");
                            --msgLength;
                            break;
                        }
                        else if ((tree.Flags & GTF_IND_NONFAULTING) is not 0)
                        {
                            jitprintf("n"); // print a n for non-faulting
                            --msgLength;
                            break;
                        }
                        else if ((tree.Flags & GTF_IND_NONNULL) is not 0)
                        {
                            jitprintf("@");
                            --msgLength;
                            break;
                        }
                    }

                    if ((tree.Flags & GTF_IND_VOLATILE) is not 0)
                    {
                        jitprintf("V");
                        --msgLength;
                        break;
                    }
                    else if ((tree.Flags & GTF_IND_UNALIGNED) is not 0)
                    {
                        jitprintf("U");
                        --msgLength;
                        break;
                    }
                    goto default;
                }

                case GT_CALL:
                {
                    var call = tree.AsCall();

                    if (call.IsInlineCandidate)
                    {
                        if (call.IsGuardedDevirtualizationCandidate)
                        {
                            jitprintf("&");
                        }
                        else
                        {
                            jitprintf("I");
                        }
                        --msgLength;
                        break;
                    }
                    else if (call.IsGuardedDevirtualizationCandidate)
                    {
                        jitprintf("G");
                        --msgLength;
                        break;
                    }
                    else if ((call._callMoreFlags & GTF_CALL_M_RETBUFFARG) is not 0)
                    {
                        jitprintf("S");
                        --msgLength;
                        break;
                    }
                    else if ((call.Flags & GTF_CALL_HOISTABLE) is not 0)
                    {
                        jitprintf("H");
                        --msgLength;
                        break;
                    }
                    goto default;
                }

                case GT_MUL:
#if !TARGET_64BIT
                case GT_MUL_LONG:
#endif
                {
                    if ((tree.Flags & GTF_MUL_64RSLT) is not 0)
                    {
                        jitprintf("L");
                        --msgLength;
                        break;
                    }
                    goto default;
                }

                case GT_LCL_FLD:
                case GT_LCL_VAR:
                case GT_LCL_ADDR:
                case GT_STORE_LCL_FLD:
                case GT_STORE_LCL_VAR:
                {
                    if ((tree.Flags & GTF_VAR_USEASG) is not 0)
                    {
                        jitprintf("U");
                        --msgLength;
                        break;
                    }
                    else if ((tree.Flags & GTF_VAR_MULTIREG) is not 0)
                    {
                        jitprintf((tree.Flags & GTF_VAR_DEF) is not 0 ? "M" : "m");
                        --msgLength;
                        break;
                    }
                    else if ((tree.Flags & GTF_VAR_DEF) is not 0)
                    {
                        jitprintf("D");
                        --msgLength;
                        break;
                    }
                    else if ((tree.Flags & GTF_VAR_CONTEXT) is not 0)
                    {
                        jitprintf("!");
                        --msgLength;
                        break;
                    }
                    goto default;
                }

                case GT_EQ:
                case GT_NE:
                case GT_LT:
                case GT_LE:
                case GT_GE:
                case GT_GT:
                case GT_TEST_EQ:
                case GT_TEST_NE:
                case GT_SELECT:
                {
                    if ((tree.Flags & GTF_RELOP_NAN_UN) is not 0)
                    {
                        jitprintf("N");
                        --msgLength;
                        break;
                    }
                    else if ((tree.Flags & GTF_RELOP_JMP_USED) is not 0)
                    {
                        jitprintf("J");
                        --msgLength;
                        break;
                    }
                    goto default;
                }

                case GT_CNS_INT:
                {
                    if (tree.AsIntCon().IsIconHandle())
                    {
                        jitprintf("H");
                        --msgLength;
                        break;
                    }
                    goto default;
                }

                default:
                {
                    jitprintf("-");
                    --msgLength;
                    break;
                }
            }

            // Then print the general purpose flags
            var flags = tree.Flags;

            if (tree.IsPartOfAddressMode)
            {
                flags |= GTF_DONT_CSE; // Force the GTF_ADDRMODE_NO_CSE flag to print out like GTF_DONT_CSE
            }
            if (!(tree.Oper.IsBinary || tree.Oper.IsMultiOp))
            {
                // the GTF_REVERSE flag only applies to binary operations (which some MultiOp nodes are).
                flags &= ~GTF_REVERSE_OPS;
            }

            msgLength -= GenTree.gtDispFlags(flags, tree._debugFlags);
            /*
                jitprintf("%c", (flags & GTF_ASG           ) ? 'A' : '-');
                jitprintf("%c", (flags & GTF_CALL          ) ? 'C' : '-');
                jitprintf("%c", (flags & GTF_EXCEPT        ) ? 'X' : '-');
                jitprintf("%c", (flags & GTF_GLOB_REF      ) ? 'G' : '-');
                jitprintf("%c", (flags & GTF_ORDER_SIDEEFF ) ? 'O' : '-');
                jitprintf("%c", (flags & GTF_COLON_COND    ) ? '?' : '-');
                jitprintf("%c", (flags & GTF_DONT_CSE      ) ? 'N' :        // N is for No cse
                             (flags & GTF_MAKE_CSE      ) ? 'H' : '-');  // H is for Hoist this expr
                jitprintf("%c", (flags & GTF_REVERSE_OPS   ) ? 'R' : '-');
                jitprintf("%c", (flags & GTF_uint      ) ? 'U' :
                             (flags & GTF_BOOLEAN       ) ? 'B' : '-');
                jitprintf("%c", (flags & GTF_SET_FLAGS     ) ? 'S' : '-');
                jitprintf("%c", ((flags & (GTF_SPILL | GTF_SPILLED)) == (GTF_SPILL | GTF_SPILLED)) ? '#' : ((flags &
               GTF_SPILLED) ? 'z' : ((flags & GTF_SPILL) ? 'Z' : '-')));
            */
        }

        // If we're printing a node for LIR, we use the space normally associated with the message
        // to display the node's temp name (if any)
        var hasOperands = tree.Operands.Any();

        if (isLIR)
        {
            assert(msg.Length == 0);

            // If the tree does not have any operands, we do not display the indent stack. This gives us
            // two additional characters for alignment.
            if (!hasOperands)
            {
                msgLength += 1;
            }

            if (tree.IsValue)
            {
                msg = $"{(tree.IsUnusedValue ? 'u' : 't')}{tree.TreeId} = {(hasOperands ? "" : " ")}";
            }
        }

        // print the msg associated with the node

        jitprintf(isLIR ? $" {new string(' ', msgLength)}{msg}" : $" {msg}{new string(' ', msgLength)}");

        /* Indent the node accordingly */
        if (!isLIR || hasOperands)
        {
            indentStack.Print();
        }

        gtDispNodeName(tree);

        assert((tree is null) || (tree.Oper < GT_COUNT));

        if (tree is not null)
        {
            // print the type of the node
            if (tree.Oper is not GT_CAST)
            {
                jitprintf($" {tree.Type.Name,-6}");

                if (varTypeIsStruct(tree.Type))
                {
                    var layout = null as ClassLayout;

                    if (tree.Oper is GT_BLK or GT_STORE_BLK)
                    {
                        layout = tree.AsBlk().Layout;
                    }
                    else if (tree.Oper is GT_LCL_VAR or GT_STORE_LCL_VAR)
                    {
                        ref var varDsc = ref lvaGetDesc(tree.AsLclVar().LclNum);

                        if (varTypeIsStruct(varDsc.Type))
                        {
                            layout = varDsc.Layout;
                        }
                    }
                    else if (tree.Oper is GT_LCL_FLD or GT_STORE_LCL_FLD)
                    {
                        layout = tree.AsLclFld().Layout;
                    }

                    if (layout is not null)
                    {
                        gtDispClassLayout(layout, tree.Type);
                    }
                }

                if (tree.Oper is GT_INDEX_ADDR or GT_ARR_ADDR)
                {
                    var elemType = (tree.Oper is GT_INDEX_ADDR) ? tree.AsIndexAddr().ElemType : tree.AsArrAddr().ElemType;
                    var elemClsHnd = (tree.Oper is GT_INDEX_ADDR) ? tree.AsIndexAddr().StructElemClass : tree.AsArrAddr().ElemClassHandle;

                    if (varTypeIsStruct(elemType) && (elemClsHnd != NO_CLASS_HANDLE))
                    {
                        jitprintf($" {eeGetShortClassName(elemClsHnd)}[]");
                    }
                    else
                    {
                        jitprintf($"{elemType.Name}[]");
                    }
                }

                if (tree.Oper.IsLocal)
                {
                    ref var varDsc = ref lvaGetDesc(tree.AsLclVarCommon().LclNum);

                    if (varDsc.IsAddressExposed)
                    {
                        jitprintf("(AX)"); // Variable has address exposed.
                    }

                    if (varDsc.IsDefinedViaAddress)
                    {
                        jitprintf("(DA)"); // Variable is defined via address
                    }

                    if (varDsc.lvUnusedStruct)
                    {
                        assert(varDsc.lvPromoted);
                        jitprintf("(U)"); // Unused struct
                    }
                    else if (varDsc.lvPromoted)
                    {
                        if (varTypeIsPromotable(varDsc.Type))
                        {
                            jitprintf("(P)"); // Promoted struct
                        }
                        else
                        {
                            // Promoted implicit by-refs can have this state during
                            // global morph while they are being rewritten
                            jitprintf("(P?!)"); // Promoted struct
                        }
                    }
                }

                if (tree.Oper is GT_RUNTIMELOOKUP)
                {
                    var runtimeLookup = tree.AsRuntimeLookup();
                    jitprintf($" 0x{dspPtr(runtimeLookup.Handle):x}");

                    switch (runtimeLookup.HandleType)
                    {
                        case CORINFO_HANDLETYPE_CLASS:
                        {
                            jitprintf(" class");
                            break;
                        }

                        case CORINFO_HANDLETYPE_METHOD:
                        {
                            jitprintf(" method");
                            break;
                        }

                        case CORINFO_HANDLETYPE_FIELD:
                        {
                            jitprintf(" field");
                            break;
                        }

                        default:
                        {
                            jitprintf(" unknown");
                            break;
                        }
                    }
                }

                if (tree.Oper is GT_MDARR_LENGTH or GT_MDARR_LOWER_BOUND)
                {
                    jitprintf($" ({tree.AsMDArr().Dim})");
                }
            }

#if HAS_FIXED_REGISTER_SET
            // for tracking down problems in reguse prediction or liveness tracking
            if (verbose && false)
            {
                ref var internalRegisters = ref JitTls.Compiler.codeGen.InternalRegisters;

                jitprintf(" RR=");
                dspRegMask(internalRegisters.GetAll(tree));
                jitprintf("\n");
            }
#endif
        }
    }

    public void gtDispNodeName(GenTree tree)
    {
        // print the node name

        var name = "<ERROR>";

        if (tree.Oper < GT_COUNT)
        {
            name = tree.Oper.Name;
        }

        var buf = "";

        if (tree.IsIconHandle())
        {
            buf = $" {name}(h)";
        }
        else if (tree.Oper is GT_PUTARG_STK)
        {
            buf = $" {name} [+0x{tree.AsPutArgStk().ArgOffset:x2}]";
        }
        else if (tree.Oper is GT_CALL)
        {
            var call = tree.AsCall();

            var callType = "CALL";
            var gtfType = "";
            var ctType = "";

            if (call._callType is CT_USER_FUNC)
            {
                if (call.IsVirtual)
                {
                    callType = "CALLV";
                }
            }
            else if (call.IsHelperCall())
            {
                ctType = " help";
            }
            else if (call._callType is CT_INDIRECT)
            {
                if (call.IsVirtual)
                {
                    callType = "CALLV";
                }
                ctType = " ind";
            }
            else
            {
                NO_WAY("Unknown gtCallType");
            }

            if ((tree.Flags & GTF_CALL_NULLCHECK) is not 0)
            {
                gtfType = " nullcheck";
            }

            if (call.IsVirtualVtable)
            {
                gtfType = " vt-ind";
            }
            else if (call.IsVirtualStub)
            {
                gtfType = " stub";
            }
#if FEATURE_READYTORUN
            else if (call.IsR2RRelativeIndir)
            {
                gtfType = " r2r_ind";
            }
            else if ((tree.Flags & GTF_TLS_GET_ADDR) is not 0)
            {
                gtfType = " _tls_get_addr";
            }
#endif
            else if ((tree.Flags & GTF_CALL_UNMANAGED) is not 0)
            {
                gtfType = $" unman{(((tree.Flags & GTF_CALL_POP_ARGS) is not 0) ? " popargs" : "")}";
#if TARGET_X86
                gtfType += $" {GetCallConvName(call.UnmanagedCallConv)}";
#endif
            }

            buf = $"{callType}{ctType}{gtfType}";
        }
        else if (tree.Oper is GT_ARR_ELEM)
        {
            buf = $" {name}[{new string(',', tree.AsArrElem().ArrRank)}]";
        }
        else if (tree.Oper is GT_LEA)
        {
            var addrMode = tree.AsAddrMode();
            buf = $" {name}({(addrMode.HasBaseAddress ? "b+" : "")}{(addrMode.HasIndex ? $"(i*{addrMode.Scale})+" : "")}{addrMode.Offset})";
        }
        else if (tree.Oper is GT_BOUNDS_CHECK)
        {
            switch (tree.AsBoundsChk().ThrowKind)
            {
                case SCK_RNGCHK_FAIL:
                {
                    buf = $" {name}_Rng";
                    break;
                }

                case SCK_ARG_EXCPN:
                {
                    buf = $" {name}_Arg";
                    break;
                }

                case SCK_ARG_RNG_EXCPN:
                {
                    buf = $" {name}_ArgRng";
                    break;
                }

                default:
                {
                    unreached();
                    break;
                }
            }
        }
        else if (tree.HasOverflowCheckEx)
        {
            buf = $" {name}_ovfl";
        }
        else
        {
            buf = $" {name}";
        }

        if (buf.Length < 10)
        {
            jitprintf($" {buf,-10}");
        }
        else
        {
            jitprintf($" {buf}");
        }
    }

    public void gtDispRange(LIR.ReadOnlyRange range)
    {
        foreach (var node in range)
        {
            gtDispLIRNode(node);
        }
    }

    /// <summary>Print the register(s) defined by the given node</summary>
    /// <param name="tree">GenTree node whose registers we want to print</param>
    public void gtDispRegVal(GenTree tree)
    {
        switch (tree.RegTag)
        {
            // Don't display anything for the GT_REGTAG_NONE case;
            // the absence of printed register values will imply this state.

            case GenTree.GT_REGTAG_REG:
            {
                jitprintf($" REG {compRegVarName(tree.RegNum)}");
                break;
            }

            default:
            {
                return;
            }
        }

        if (tree.IsMultiRegNode)
        {
            // 0th reg is GetRegNum(), which is already printed above.
            // Print the remaining regs of a multi-reg node.
            var regCount = gtDispMultiRegCount(tree);

            // For some nodes, e.g. COPY, RELOAD or CALL, we may not have valid regs for all positions.
            for (byte i = 1; i < regCount; i++)
            {
                var reg = tree.GetRegByIndex(i);
                jitprintf($",{(genIsValidReg(reg) ? compRegVarName(reg) : "NA")}");
            }
        }
    }

    /// <summary>Display the SSA use/def for a given local.</summary>
    /// <param name="lclNum">The local's number.</param>
    /// <param name="ssaNum">The SSA number.</param>
    /// <param name="isDef">Whether this is a def.</param>
    public void gtDispSsaName(int lclNum, int ssaNum, bool isDef)
    {
        if (ssaNum is SsaConfig.RESERVED_SSA_NUM)
        {
            return;
        }

        ref var lclDsc = ref lvaGetDesc(lclNum);
        var isValid = lclDsc.IsValidSsaNum(ssaNum);

        if (isDef)
        {
            if (!isValid)
            {
                jitprintf($"?d:{ssaNum}");
                return;
            }

            var oldDefSsaNum = lclDsc.GetPerSsaData(ssaNum).UseDefSsaNum;

            if (oldDefSsaNum != SsaConfig.RESERVED_SSA_NUM)
            {
                jitprintf($"ud:{oldDefSsaNum}->{ssaNum}");
                return;
            }
        }
        jitprintf($"{(isValid ? "" : "?")}{(isDef ? "d" : "u")}:{ssaNum}");
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

    public void gtDispTree(GenTree tree, string msg = "", bool topOnly = false, bool isLIR = false)
    {
        var indentStack = new IndentStack(this);
        gtDispTree(tree, ref indentStack, msg, topOnly, isLIR);
    }

    public unsafe void gtDispTree(GenTree tree, ref IndentStack indentStack, string msg = "", bool topOnly = false, bool isLIR = false)
    {
        if (tree is null)
        {
            jitprintf($" [{0:X8}] <NULL>\n");
            jitprintf(""); // null string means flush
            return;
        }

        if (tree.Oper >= GT_COUNT)
        {
            gtDispNode(tree, ref indentStack, msg, isLIR);
            jitprintf("Bogus operator!\n");
            return;
        }

        // Determine what kind of arc to propagate.
        var myArc = IINone;
        var lowerArc = IINone;

        if (indentStack.Depth > 0)
        {
            myArc = indentStack.Pop();

            switch (myArc)
            {
                case IIArcBottom:
                {
                    indentStack.Push(IIArc);
                    lowerArc = IINone;
                    break;
                }

                case IIArc:
                {
                    indentStack.Push(IIArc);
                    lowerArc = IIArc;
                    break;
                }

                case IIArcTop:
                {
                    indentStack.Push(IINone);
                    lowerArc = IIArc;
                    break;
                }

                case IINone:
                {
                    indentStack.Push(IINone);
                    lowerArc = IINone;
                    break;
                }

                default:
                {
                    unreached();
                    break;
                }
            }
        }

        // Is it a 'simple' unary/binary operator?

        var childMsg = "";

        if (tree.Oper.IsSimple)
        {
            // Now, get the right type of arc for this node
            if (myArc is not IINone)
            {
                _ = indentStack.Pop();
                indentStack.Push(myArc);
            }

            gtDispNode(tree, ref indentStack, msg, isLIR);

            // Propagate lowerArc to the lower children.
            if (indentStack.Depth > 0)
            {
                _ = indentStack.Pop();
                indentStack.Push(lowerArc);
            }

            if (tree.Oper is GT_CAST)
            {
                // Format a message that explains the effect of this GT_CAST

                var cast = tree.AsCast();

                var fromType = cast.CastOp.Type.ActualType;
                var toType = cast.CastType;
                var finalType = cast.Type;

                // if GTF_uint is set then force fromType to an unsigned type
                if (cast.IsUnsigned)
                {
                    fromType = varTypeToUnsigned(fromType);
                }

                if (finalType != toType)
                {
                    jitprintf($" {finalType.Name} <-");
                }

                jitprintf($" {toType.Name} <- {fromType.Name}");
            }
            else if (tree.Oper.IsLocalStore)
            {
                // Local stores used to be leaf nodes.
                gtDispLocal(tree.AsLclVarCommon(), ref indentStack);
            }
            else if (tree.IsBlkOp)
            {
                if (tree.IsCopyBlkOp)
                {
                    jitprintf(" (copy)");
                }
                else if (tree.IsInitBlkOp)
                {
                    jitprintf(" (init)");
                }

                if (tree.Oper.IsStoreBlk && (tree.AsBlk()._kind is not GenTreeBlk.BlkOpKindInvalid))
                {
                    switch (tree.AsBlk()._kind)
                    {
                        case GenTreeBlk.BlkOpKindUnroll:
                        {
                            jitprintf(" (Unroll)");
                            break;
                        }

                        case GenTreeBlk.BlkOpKindUnrollMemmove:
                        {
                            jitprintf(" (Memmove)");
                            break;
                        }

                        case GenTreeBlk.BlkOpKindLoop:
                        {
                            jitprintf(" (Loop)");
                            break;
                        }

#if TARGET_WASM
                        case GenTreeBlk.BlkOpKindNativeOpcode:
                        {
                            jitprintf($" (memory.{(tree.Oper.IsCopyBlkOp ? "copy" : "fill")})");
                            break;
                        }
#endif

                        default:
                        {
                            unreached();
                            break;
                        }
                    }
                }
            }
            else if (tree.Oper is GT_PUTARG_STK)
            {
                var putArg = tree.AsPutArgStk();
                jitprintf($" ({putArg.StackByteSize} stackByteSize), ({putArg.ArgOffset} byteOffset)");

                if (putArg._kind is not GenTreePutArgStk.Kind.Invalid)
                {
                    switch (putArg._kind)
                    {
                        case GenTreePutArgStk.Kind.RepInstr:
                        {
                            jitprintf(" (RepInstr)");
                            break;
                        }

                        case GenTreePutArgStk.Kind.PartialRepInstr:
                        {
                            jitprintf(" (PartialRepInstr)");
                            break;
                        }

                        case GenTreePutArgStk.Kind.Unroll:
                        {
                            jitprintf(" (Unroll)");
                            break;
                        }

                        case GenTreePutArgStk.Kind.Push:
                        {
                            jitprintf(" (Push)");
                            break;
                        }

                        default:
                        {
                            unreached();
                            break;
                        }
                    }
                }
            }
            else if (tree.Oper is GT_FIELD_ADDR)
            {
                jitprintf($" {eeGetFieldName(tree.AsFieldAddr().FldHnd, includeType: true)}");
            }
            else if (tree.Oper is GT_INTRINSIC)
            {
                var intrinsic = tree.AsIntrinsic();

                var name = intrinsic.IntrinsicName switch {
                    NI_System_Math_Abs => " abs",
                    NI_System_Math_Acos => " acos",
                    NI_System_Math_Acosh => " acosh",
                    NI_System_Math_Asin => " asin",
                    NI_System_Math_Asinh => " asinh",
                    NI_System_Math_Atan => " atan",
                    NI_System_Math_Atanh => " atanh",
                    NI_System_Math_Atan2 => " atan2",
                    NI_System_Math_Cbrt => " cbrt",
                    NI_System_Math_Ceiling => " ceiling",
                    NI_System_Math_Cos => " cos",
                    NI_System_Math_Cosh => " cosh",
                    NI_System_Math_Exp => " exp",
                    NI_System_Math_Floor => " floor",
                    NI_System_Math_FusedMultiplyAdd => " fma",
                    NI_System_Math_ILogB => " ilogb",
                    NI_System_Math_Log => " log",
                    NI_System_Math_Log2 => " log2",
                    NI_System_Math_Log10 => " log10",
#if TARGET_RISCV64
                    NI_System_Math_Max => " max",
                    NI_System_Math_MaxNative => " maxNative",
                    NI_System_Math_Maxuint => " maxuint",
                    NI_System_Math_Min => " min",
                    NI_System_Math_MinNative => " minNative",
                    NI_System_Math_Minuint => " minuint",
                    NI_PRIMITIVE_LeadingZeroCount => " leadingZeroCount",
                    NI_PRIMITIVE_TrailingZeroCount => " trailingZeroCount",
                    NI_PRIMITIVE_PopCount => " popCount",
#endif
                    NI_System_Math_Pow => " pow",
                    NI_System_Math_Round => " round",
                    NI_System_Math_Sin => " sin",
                    NI_System_Math_Sinh => " sinh",
                    NI_System_Math_Sqrt => " sqrt",
                    NI_System_Math_Tan => " tan",
                    NI_System_Math_Tanh => " tanh",
                    NI_System_Math_Truncate => " truncate",
                    NI_System_Object_GetType => " objGetType",
                    NI_System_Runtime_CompilerServices_RuntimeHelpers_IsKnownConstant => " isKnownConst",
                    NI_System_Runtime_CompilerServices_RuntimeHelpers_WriteBarrier => " WriteBarrier",
#if FEATURE_SIMD
                    NI_SIMD_UpperRestore => " simdUpperRestore",
                    NI_SIMD_UpperSave => " simdUpperSave",
#endif
                    _ => "",
                };

                if (name.Length != 0)
                {
                    jitprintf(name);
                }
                else
                {
                    jitprintf("Unknown intrinsic: ");
                    printTreeId(tree);
                }
            }
            else if (tree.Oper is GT_SELECTCC)
            {
                jitprintf($" cond={tree.AsOpCC().Condition.Name}");
            }
#if TARGET_ARM64
            else if (tree.Oper is GT_SELECT_INCCC or GT_SELECT_INVCC or GT_SELECT_NEGCC)
            {
                jitprintf($" cond={tree.AsOpCC().Condition.Name}");
            }
            else if (tree.Oper is GT_CCMP)
            {
                var ccmp = tree.AsCCMP();
                jitprintf(" cond={ccmp.Condition.Name} flags={InsCflagsToString(ccmp.FlagsVal)}");
            }
#endif
            gtDispCommonEndLine(tree);

            if (!topOnly && (tree is GenTreeUnOp unOp))
            {
                var op1 = unOp.Op1;
                var op2 = null as GenTree;

                if (unOp is GenTreeOp op)
                {
                    op2 = op.Op2;
                }

                if (op1 is not null)
                {
                    // Label the child of the GT_COLON operator
                    // op1 is the else part
                    if (tree.Oper is GT_COLON)
                    {
                        childMsg = "else";
                    }
                    else if (tree.Oper is GT_QMARK)
                    {
                        childMsg = "   if";
                    }
                    gtDispChild(op1, ref indentStack, (op2 is null) ? IIArcBottom : IIArc, childMsg, topOnly);
                }

                if (op2 is not null)
                {
                    // Label the childMsgs of the GT_COLON operator
                    // op2 is the then part

                    if (tree.Oper is GT_COLON)
                    {
                        childMsg = "then";
                    }
                    gtDispChild(op2, ref indentStack, IIArcBottom, childMsg, topOnly);
                }
            }

            return;
        }

        // Now, get the right type of arc for this node
        if (myArc != IINone)
        {
            _ = indentStack.Pop();
            indentStack.Push(myArc);
        }
        gtDispNode(tree, ref indentStack, msg, isLIR);

        // Propagate lowerArc to the lower children.
        if (indentStack.Depth > 0)
        {
            _ = indentStack.Pop();
            indentStack.Push(lowerArc);
        }

        // See what kind of a special operator we have here, and handle its special children.

        switch (tree.Oper)
        {
            case GT_FIELD_LIST:
            {
                gtDispCommonEndLine(tree);

                if (!topOnly)
                {
                    var fieldList = tree.AsFieldList();

                    foreach (var use in fieldList.Uses)
                    {
                        var offset = $"ofs {use.Offset}";
                        gtDispChild(use.Node, ref indentStack, (use.Next is null) ? IIArcBottom : IIArc, offset);
                    }
                }
                break;
            }

            case GT_PHI:
            {
                gtDispCommonEndLine(tree);

                if (!topOnly)
                {
                    var phi = tree.AsPhi();

                    foreach (var use in phi.Uses)
                    {
                        var block = $"pred {FMT_BB(use.Node.AsPhiArg().PredBB.bbNum)}";
                        gtDispChild(use.Node, ref indentStack, (use.Next is null) ? IIArcBottom : IIArc, block);
                    }
                }
                break;
            }

            case GT_CALL:
            {
                var call = tree.AsCall();
                var lastChild = null as GenTree;

                call.VisitOperands((GenTree operand) => {
                    lastChild = operand;
                    return GenTree.VisitResult.Continue;
                });

                if (call._callType is not CT_INDIRECT)
                {
                    jitprintf($" {eeGetMethodFullName(call._callMethHnd, includeReturnType: true, includeThisSpecifier: true)}");
                }

                if (call.IsAsync)
                {
                    jitprintf(" (async)");
                }

                if (((call.Flags & GTF_CALL_UNMANAGED) is not 0) && ((call._callMoreFlags & GTF_CALL_M_FRAME_VAR_DEATH) is not 0))
                {
                    jitprintf(" (FramesRoot last use)");
                }

                if ((call.Flags & GTF_CALL_INLINE_CANDIDATE) is not 0)
                {
                    InlineCandidateInfo inlineInfo;

                    if (call.IsGuardedDevirtualizationCandidate)
                    {
                        inlineInfo = call.GetGdvCandidateInfo(0);
                    }
                    else
                    {
                        inlineInfo = call.SingleInlineCandidateInfo;
                    }

                    if ((inlineInfo is not null) && (inlineInfo.exactContextHandle is not null))
                    {
                        jitprintf($" (exactContextHandle=0x{FMT_DSP_PTR(inlineInfo.exactContextHandle)})");
                    }
                }

                // Dump profile if any
                if (call.IsHelperCall() && impIsCastHelperMayHaveProfileData(eeGetHelperNum(call._callMethHnd)))
                {
                    Unsafe.SkipInit(out InlineArrayMaxGdvTypeChecks<nint> likelyClasses);
                    Unsafe.SkipInit(out InlineArrayMaxGdvTypeChecks<int> likelyLikelihoods);

                    pickGDV(call, call._castHelperILOffset, isInterface: false, likelyClasses, methodGuesses: [], out var likelyClassCount, likelyLikelihoods, verboseLogging: false);

                    if (likelyClassCount > 0)
                    {
                        jitprintf($" ({likelyLikelihoods[0]}% likely '{eeGetClassName(unchecked((CORINFO_CLASS_HANDLE)(likelyClasses[0])))}')");
                    }
                }

                gtDispCommonEndLine(tree);

                if (!topOnly)
                {
                    assert(lastChild is not null);
                    gtDispArgList(call, lastChild, ref indentStack);

                    foreach (var arg in call.Args.LateArgs)
                    {
                        assert(arg is not null);
                        assert(arg.LateNode is not null);

                        var arcType = (arg.LateNode == lastChild) ? IIArcBottom : IIArc;
                        var buf = gtGetLateArgMsg(call, arg);

                        gtDispChild(arg.LateNode, ref indentStack, arcType, buf, topOnly);
                    }

                    if (call._controlExpr is not null)
                    {
                        gtDispChild(call._controlExpr, ref indentStack, (call._controlExpr == lastChild) ? IIArcBottom : IIArc, "control expr", topOnly);
                    }
                }
                break;
            }

#if FEATURE_HW_INTRINSICS
            case GT_HWINTRINSIC:
            {
                var hwintrinsic = tree.AsHWIntrinsic();
                jitprintf($" {hwintrinsic.SimdSize}");

                if (hwintrinsic.SimdBaseType is not TYP_UNKNOWN)
                {
                    jitprintf($" {hwintrinsic.SimdBaseType.Name}");
                }

                if (hwintrinsic.AuxiliaryType is not TYP_UNKNOWN)
                {
                    jitprintf($" (aux {hwintrinsic.AuxiliaryType.Name})");
                }
                jitprintf($" {HWIntrinsicInfo.lookupName(hwintrinsic.HWIntrinsicId)}");

                gtDispCommonEndLine(tree);

                if (!topOnly)
                {
                    var operands = hwintrinsic.Operands;
                    var index = 0;
                    var count = operands.Length;

                    foreach (var operand in operands)
                    {
                        gtDispChild(operand, ref indentStack, (++index < count) ? IIArc : IIArcBottom, "", topOnly);
                    }
                }
                break;
            }
#endif

            case GT_ARR_ELEM:
            {
                gtDispCommonEndLine(tree);

                if (!topOnly)
                {
                    var arrElem = tree.AsArrElem();

                    gtDispChild(arrElem.ArrObj, ref indentStack, IIArc, "", topOnly);

                    for (var dim = 0; dim < arrElem.ArrRank; dim++)
                    {
                        var arcType = ((dim + 1) == arrElem.ArrRank) ? IIArcBottom : IIArc;
                        gtDispChild(arrElem.ArrInds[dim], ref indentStack, arcType, "", topOnly);
                    }
                }
                break;
            }

            case GT_CMPXCHG:
            {
                gtDispCommonEndLine(tree);

                if (!topOnly)
                {
                    var cmpXchg = tree.AsCmpXchg();

                    gtDispChild(cmpXchg.Addr, ref indentStack, IIArc, "", topOnly);
                    gtDispChild(cmpXchg.Data, ref indentStack, IIArc, "", topOnly);
                    gtDispChild(cmpXchg.Comparand, ref indentStack, IIArcBottom, "", topOnly);
                }
                break;
            }

            case GT_SELECT:
            {
                gtDispCommonEndLine(tree);

                if (!topOnly)
                {
                    var conditional = tree.AsConditional();

                    gtDispChild(conditional.Cond, ref indentStack, IIArc, childMsg, topOnly);
                    gtDispChild(conditional.Op1, ref indentStack, IIArc, childMsg, topOnly);
                    gtDispChild(conditional.Op2, ref indentStack, IIArcBottom, childMsg, topOnly);
                }
                break;
            }

            default:
            {
                if (tree.Oper.IsLeaf)
                {
                    gtDispLeaf(tree, ref indentStack);
                    gtDispCommonEndLine(tree);
                }
                else
                {
                    jitprintf("<DON'T KNOW HOW TO DISPLAY THIS NODE> :");
                    jitprintf(""); // null string means flush
                }
                break;
            }
        }
    }

    public void gtDispTreeRange(LIR.Range containingRange, GenTree tree)
    {
        gtDispRange(containingRange.GetTreeRangeWithFlags(tree, out _, out _));
    }

    /// <summary>Utility function that prints a tree's ValueNumber: gtVNPair</summary>
    /// <param name="tree"></param>
    public void gtDispVN(GenTree tree)
    {
        if (tree._vnPair.Liberal != ValueNumStore.NoVN)
        {
            assert(tree._vnPair.Conservative != ValueNumStore.NoVN);
            jitprintf(" ");
            vnpPrint(tree._vnPair, 0);
        }
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
                _ = stringBuilder.Append(CultureInfo.InvariantCulture, $" out+{segment.StackOffset:x2}");
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

    /// <summary>see if a type comparison can be further simplified</summary>
    /// <param name="tree">tree possibly comparing types</param>
    /// <returns>An alternative tree if folding happens. Original tree otherwise.</returns>
    public unsafe GenTree gtFoldTypeCompare(GenTreeOp tree)
    {
        // Notes:
        //    Checks for
        //        typeof(...) == obj.GetType()
        //        typeof(...) == typeof(...)
        //        typeof(...) is null
        //        obj1.GetType() == obj2.GetType()
        //
        //    And potentially optimizes away the need to obtain actual
        //    RuntimeType objects to do the comparison.

        // Only handle EQ and NE, (maybe relop vs null someday)
        var oper = tree.Oper;

        if (oper is not GT_EQ and not GT_NE)
        {
            return tree;
        }

        // Screen for the right kinds of operands

        var op1 = tree.Op1;
        var op2 = tree.Op2;

        var op1Kind = gtGetTypeProducerKind(op1);
        var op2Kind = gtGetTypeProducerKind(op2);

        // Fold "typeof(handle) cmp null"
        if (((op2Kind is TPK_Null) && (op1Kind is TPK_Handle)) || ((op1Kind is TPK_Null) && (op2Kind is TPK_Handle)))
        {
            var call = (op1Kind is TPK_Handle) ? op1 : op2;

            var callArg = call.AsCall().Args.GetArgByIndex(0);
            assert(callArg is not null);

            var handle = callArg.Node;

            if (gtGetHelperArgClassHandle(handle) != NO_CLASS_HANDLE)
            {
                return (oper is GT_EQ) ? gtNewFalse() : gtNewTrue();
            }
        }

        // If both types are created via handles, we can simply compare
        // handles instead of the types that they'd create.
        if ((op1Kind is TPK_Handle) && (op2Kind is TPK_Handle))
        {
            JITDUMP("Optimizing compare of types-from-handles to instead compare handles\n");
            assert((op1.AsCall().Args.CountArgs() is 1) && (op2.AsCall().Args.CountArgs() is 1));

            var op1CallArg = op1.AsCall().Args.GetArgByIndex(0);
            var op2CallArg = op2.AsCall().Args.GetArgByIndex(0);

            assert((op1CallArg is not null) && (op2CallArg is not null));

            var op1ClassFromHandle = op1CallArg.Node;
            var op2ClassFromHandle = op2CallArg.Node;

            var cls1Hnd = NO_CLASS_HANDLE;
            var cls2Hnd = NO_CLASS_HANDLE;

            // Try and find class handles from op1 and op2
            cls1Hnd = gtGetHelperArgClassHandle(op1ClassFromHandle);
            cls2Hnd = gtGetHelperArgClassHandle(op2ClassFromHandle);

            // If we have both class handles, try and resolve the type equality test completely.
            var resolveFailed = false;

            if ((cls1Hnd != NO_CLASS_HANDLE) && (cls2Hnd != NO_CLASS_HANDLE))
            {
                JITDUMP($"Asking runtime to compare {FMT_PTR(cls1Hnd)} ({eeGetClassName(cls1Hnd)}) and {FMT_PTR(cls2Hnd)} ({eeGetClassName(cls2Hnd)}) for equality\n");
                var s = info.compCompHnd->compareTypesForEquality(cls1Hnd, cls2Hnd);

                if (s is not TypeCompareState.May)
                {
                    // Type comparison result is known.
                    var typesAreEqual = s is TypeCompareState.Must;
                    var operatorIsEQ = oper is GT_EQ;
                    var compareResult = (operatorIsEQ ^ typesAreEqual) ? 0 : 1;

                    JITDUMP($"Runtime reports comparison is known at jit time: {compareResult}\n");
                    return gtNewIconNode(TYP_INT, compareResult);
                }
                else
                {
                    resolveFailed = true;
                }
            }

            if (resolveFailed)
            {
                JITDUMP("Runtime reports comparison is NOT known at jit time\n");
            }
            else
            {
                JITDUMP($"Could not find handle for {(cls1Hnd == NO_CLASS_HANDLE ? "cls1" : "")}{(cls2Hnd == NO_CLASS_HANDLE ? " cls2" : "")}\n");
            }

            var compare = gtNewBinaryNode(oper, TYP_INT, op1ClassFromHandle, op2ClassFromHandle);

            // Drop any now-irrelevant flags
            compare.Flags |= (tree.Flags & (GTF_RELOP_JMP_USED | GTF_DONT_CSE));

            return compare;
        }
        else if ((op1Kind is TPK_GetType) && (op2Kind is TPK_GetType))
        {
            GenTree arg1;

            if (op1.Oper is GT_INTRINSIC)
            {
                arg1 = op1.AsUnOp().Op1;
            }
            else
            {
                var thisArg = op1.AsCall().Args.ThisArg;
                assert(thisArg is not null);
                arg1 = thisArg.Node;
            }

            arg1 = gtNewMethodTableLookup(arg1);

            GenTree arg2;

            if (op2.Oper is GT_INTRINSIC)
            {
                arg2 = op2.AsUnOp().Op1;
            }
            else
            {
                var thisArg = op2.AsCall().Args.ThisArg;
                assert(thisArg is not null);
                arg2 = thisArg.Node;
            }

            arg2 = gtNewMethodTableLookup(arg2);

            var compare = gtNewBinaryNode(oper, TYP_INT, arg1, arg2);

            // Drop any now-irrelevant flags
            compare.Flags |= (tree.Flags & (GTF_RELOP_JMP_USED | GTF_DONT_CSE));

            return compare;
        }
        else if ((op1Kind is not TPK_GetType || op2Kind is not TPK_Handle) && (op1Kind is not TPK_Handle || op2Kind is not TPK_GetType))
        {
            // If one operand creates a type from a handle and the other operand is fetching the type from an object,
            // we can sometimes optimize the type compare into a simpler
            // method table comparison.
            //
            // TODO: if other operand is null...
            return tree;
        }
        else
        {
            var opHandle = (op1Kind is TPK_Handle) ? op1 : op2;
            var opOther = (op1Kind is TPK_Handle) ? op2 : op1;

            // Tunnel through the handle operand to get at the class handle involved.
            var callArg = opHandle.AsCall().Args.GetArgByIndex(0);
            assert(callArg is not null);

            var opHandleArgument = callArg.Node;
            var clsHnd = gtGetHelperArgClassHandle(opHandleArgument);

            // If we couldn't find the class handle, give up.
            if (clsHnd == NO_CLASS_HANDLE)
            {
                return tree;
            }

            // We're good to go.
            JITDUMP("Optimizing compare of obj.GetType() and type-from-handle to compare method table pointer\n");

            // opHandleArgument is the method table we're looking for.
            var knownMT = opHandleArgument;

            // Fetch object method table from the object itself.
            var objOp = null as GenTree;

            // Note we may see intrinsified or regular calls to GetType
            if (opOther.Oper is GT_INTRINSIC)
            {
                objOp = opOther.AsUnOp().Op1;
            }
            else
            {
                var thisArg = opOther.AsCall().Args.ThisArg;
                assert(thisArg is not null);
                objOp = thisArg.Node;
            }

            // Check if an object of this type can even exist
            if (info.compCompHnd->getExactClasses(clsHnd, 0, null) is 0)
            {
                JITDUMP($"Runtime reported {FMT_PTR(clsHnd)} ({eeGetClassName(clsHnd)}) is never allocated\n");

                var operatorIsEQ = oper is GT_EQ;
                var compareResult = operatorIsEQ ? 0 : 1;
                JITDUMP($"Runtime reports comparison is known at jit time: {compareResult}\n");

                var result = gtNewIconNode(TYP_INT, compareResult);
                var sideEffects = fgAddrCouldBeNull(objOp) ? gtNewNullCheck(objOp) : objOp;
                return gtWrapWithSideEffects(result, sideEffects, GTF_ALL_EFFECT);
            }

            var objCls = gtGetClassHandle(objOp, out var isExact, out var isNonNull);

            // if both classes are "final" (e.g. System.String[]) we can replace the comparison
            // with `true/false` + null check.
            if ((objCls != NO_CLASS_HANDLE) && (isExact || info.compCompHnd->isExactType(objCls)))
            {
                var tcs = info.compCompHnd->compareTypesForEquality(objCls, clsHnd);

                if (tcs is not TypeCompareState.May)
                {
                    var operatorIsEQ = oper is GT_EQ;
                    var typesAreEqual = tcs is TypeCompareState.Must;
                    var compareResult = gtNewIconNode(TYP_INT, (operatorIsEQ ^ typesAreEqual) ? 0 : 1);

                    if (!isNonNull)
                    {
                        // we still have to emit a null-check
                        // obj.GetType == typeof() -> (nullcheck) true/false

                        var nullcheck = gtNewNullCheck(objOp);
                        return gtNewCommaNode(tree.Type, nullcheck, compareResult);
                    }
                    else if ((objOp.Flags & GTF_ALL_EFFECT) is not 0)
                    {
                        return gtNewCommaNode(tree.Type, objOp, compareResult);
                    }
                    else
                    {
                        return compareResult;
                    }
                }
            }

            // Fetch the method table from the object
            var objMT = gtNewMethodTableLookup(objOp);

            // Compare the two method tables
            var compare = gtNewBinaryNode(oper, TYP_INT, objMT, knownMT);

            // Drop any now irrelevant flags
            compare.Flags |= (tree.Flags & (GTF_RELOP_JMP_USED | GTF_DONT_CSE));

            // And we're done
            return compare;
        }
    }

    /// <summary>see if a (potential) type equality call is foldable</summary>
    /// <param name="isEq">is it == or != operator</param>
    /// <param name="op1">first argument to call</param>
    /// <param name="op2">second argument to call</param>
    /// <returns>nulltpr if no folding happened. An alternative tree if folding happens.</returns>
    /// <remarks>If either operand is known to be a RuntimeType, then the type equality methods will simply check object identity and so we can fold the call into a simple compare of the call's operands.</remarks>
    public GenTreeOp? gtFoldTypeEqualityCall(bool isEq, GenTree op1, GenTree op2)
    {
        if ((gtGetTypeProducerKind(op1) is TPK_Unknown) && (gtGetTypeProducerKind(op2) is TPK_Unknown))
        {
            return null;
        }

        var simpleOp = isEq ? GT_EQ : GT_NE;
        JITDUMP($"\nFolding call to Type:op_{(isEq ? "Equality" : "Inequality")} to a simple compare via {simpleOp.Name}\n");
        return gtNewBinaryNode(simpleOp, TYP_INT, op1, op2);
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

    /// <summary>Calculate the cost for the address of an indirection node.</summary>
    /// <param name="addr">The address node in question</param>
    /// <param name="type">The type of the indirection</param>
    /// <param name="isVolatile">true if the indirection is volatile</param>
    /// <param name="costEx">parameter for the execution cost</param>
    /// <param name="costSz">parameter for the size cost</param>
    /// <returns>Whether the cost calculated includes that of address.</returns>
    /// <remarks>Used for both loads and stores.</remarks>
    public bool gtGetAddrNodeCost(GenTree addr, var_types type, bool isVolatile, out byte costEx, out byte costSz)
    {
        costEx = 0;
        costSz = 0;

        var includesAddrCost = false;

        if (addr.EffectiveVal.Oper is GT_ADD)
        {
            // See if we can form a complex addressing mode.
            var doAddrMode = true;

#if TARGET_ARM64
            if (isVolatile)
            {
                // For volatile store/loads when address is contained we always emit `dmb`
                // if it's not - we emit one-way barriers i.e. ldar/stlr
                doAddrMode = false;
            }
#endif

            if (doAddrMode && gtMarkAddrMode(addr, ref costEx, ref costSz, type))
            {
                includesAddrCost = true;
            }
        }
        else if (gtIsLikelyRegVar(addr))
        {
            // Indirection of an enregister LCL_VAR, don't increase costEx/costSz.
            includesAddrCost = true;
        }
#if TARGET_XARCH
        else if (addr.Oper.IsCnsIntOrI)
        {
            // Indirection of a CNS_INT, subtract 1 from costEx makes costEx 3 for x86 and 4 for amd64.

            costEx += (byte)(addr.CostEx - 1);
            costSz += addr.CostSz;

            includesAddrCost = true;
        }
#endif

        return includesAddrCost;
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

#if FEATURE_HW_INTRINSICS
    /// <summary>Returns intrinsic ID based on the oper, base type, and simd size</summary>
    /// <param name="oper">The oper for which to get the intrinsic ID</param>
    /// <param name="op1">The first operand on which oper is executed</param>
    /// <param name="op2">The second operand on which oper is executed</param>
    /// <param name="simdBaseType">The base type on which oper is executed</param>
    /// <param name="simdSize">The simd size on which oper is executed</param>
    /// <param name="isScalar">True if the oper is over scalar data; otherwise false</param>
    /// <returns>The intrinsic ID based on the oper, base type, and simd size</returns>
    public NamedIntrinsic GetHWIntrinsicIdForBinOp(genTreeOps oper, GenTree op1, GenTree op2, var_types simdBaseType, byte simdSize, bool isScalar)
    {
        var simdType = GetSimdTypeForSize(simdSize);

        assert(varTypeIsArithmetic(simdBaseType));
        assert(varTypeIsSimd(simdType));

        assert(op1 is not null);
        assert(op1.Type == simdType);
        assert(op2 is not null);

#if TARGET_XARCH
        if (simdSize is 32 or 64)
        {
            assert(!isScalar);
        }
        else
#endif
        {
#if TARGET_ARM64
        assert(!isScalar || (simdSize is 8));
        // TODO-SVE: Add scalable length support
        assert(simdSize is 8 or 16);
#endif

            assert(!isScalar || varTypeIsFloating(simdBaseType));
        }

        var id = NI_Illegal;

        switch (oper)
        {
            case GT_ADD:
            {
                assert(op2.Type == simdType);

#if TARGET_XARCH
                if (simdSize is 64)
                {
                    id = NI_AVX512_Add;
                }
                else if (simdSize is 32)
                {
                    if (varTypeIsIntegral(simdBaseType))
                    {
                        assert(compIsaSupportedDebugOnly(InstructionSet_AVX2));
                        id = NI_AVX2_Add;
                    }
                    else
                    {
                        id = NI_AVX_Add;
                    }
                }
                else
                {
                    id = isScalar ? NI_X86Base_AddScalar : NI_X86Base_Add;
                }
#elif TARGET_ARM64
                if ((simdSize is 8) && (isScalar || (simdBaseType.Size is 8)))
                {
                    id = NI_AdvSimd_AddScalar;
                }
                else if (simdBaseType == TYP_DOUBLE)
                {
                    id = NI_AdvSimd_Arm64_Add;
                }
                else
                {
                    id = NI_AdvSimd_Add;
                }
#endif
                break;
            }

            case GT_AND:
            {
                assert(!isScalar);
                assert(op2.Type == simdType);

#if TARGET_XARCH
                if (simdSize is 64)
                {
                    id = NI_AVX512_And;
                }
                else if (simdSize is 32)
                {
                    if (varTypeIsIntegral(simdBaseType))
                    {
                        assert(compIsaSupportedDebugOnly(InstructionSet_AVX2));
                        id = NI_AVX2_And;
                    }
                    else
                    {
                        id = NI_AVX_And;
                    }
                }
                else
                {
                    id = NI_X86Base_And;
                }
#elif TARGET_ARM64
                id = NI_AdvSimd_And;
#endif
                break;
            }

            case GT_AND_NOT:
            {
                assert(!isScalar);
                assert(op2.Type == simdType);

                if (fgNodeThreading is not NodeThreading.LIR)
                {
                    // We don't want to support creating AND_NOT nodes prior to LIR
                    // as it can break important optimizations. We'll produces this
                    // in lowering instead.
                    break;
                }

#if TARGET_XARCH
                if (simdSize is 64)
                {
                    id = NI_AVX512_AndNot;
                }
                else if (simdSize is 32)
                {
                    if (varTypeIsIntegral(simdBaseType))
                    {
                        assert(compIsaSupportedDebugOnly(InstructionSet_AVX2));
                        id = NI_AVX2_AndNotVector;
                    }
                    else
                    {
                        id = NI_AVX_AndNot;
                    }
                }
                else
                {
                    id = NI_X86Base_AndNot;
                }
#elif TARGET_ARM64
                // TODO-SVE: Add scalable length support
                assert(simdSize is 8 or 16);

                id = NI_AdvSimd_BitwiseClear;
#endif
                break;
            }

            case GT_DIV:
            {
#if TARGET_XARCH
                assert(varTypeIsFloating(simdBaseType) || !varTypeIsLong(simdBaseType));
#else
                assert(varTypeIsFloating(simdBaseType));
#endif
                assert(op2.Type == simdType);

#if TARGET_XARCH
                if (varTypeIsFloating(simdBaseType))
                {
                    if (simdSize is 64)
                    {
                        id = NI_AVX512_Divide;
                    }
                    else if (simdSize is 32)
                    {
                        id = NI_AVX_Divide;
                    }
                    else
                    {
                        id = isScalar ? NI_X86Base_DivideScalar : NI_X86Base_Divide;
                    }
                }
#elif TARGET_ARM64
                if ((simdSize is 8) && (isScalar || (simdBaseType is TYP_DOUBLE)))
                {
                    id = NI_AdvSimd_DivideScalar;
                }
                else
                {
                    id = NI_AdvSimd_Arm64_Divide;
                }
#endif
                break;
            }

            case GT_LSH:
            {
                assert(!isScalar);
                assert((op2.Type == simdType) || varTypeIsInt(op2.Type));
                assert(varTypeIsIntegral(simdBaseType));

#if TARGET_XARCH
                if (varTypeIsByte(simdBaseType))
                {
                    break;
                }

                if (varTypeIsInt(op2.Type))
                {
                    if (simdSize is 64)
                    {
                        id = NI_AVX512_ShiftLeftLogical;
                    }
                    else if (simdSize is 32)
                    {
                        assert(compIsaSupportedDebugOnly(InstructionSet_AVX2));
                        id = NI_AVX2_ShiftLeftLogical;
                    }
                    else
                    {
                        id = NI_X86Base_ShiftLeftLogical;
                    }
                }
                else if ((simdSize is 64) || varTypeIsShort(simdBaseType))
                {
                    if (compOpportunisticallyDependsOn(InstructionSet_AVX512))
                    {
                        id = NI_AVX512_ShiftLeftLogicalVariable;
                    }
                }
                else if (compOpportunisticallyDependsOn(InstructionSet_AVX2))
                {
                    id = NI_AVX2_ShiftLeftLogicalVariable;
                }
#elif TARGET_ARM64
                if ((simdSize is 8) && (simdBaseType.Size is 8))
                {
                    id = op2.Oper.IsCnsIntOrI ? NI_AdvSimd_ShiftLeftLogicalScalar : NI_AdvSimd_ShiftLogicalScalar;
                }
                else
                {
                    id = op2.Oper.IsCnsIntOrI ? NI_AdvSimd_ShiftLeftLogical : NI_AdvSimd_ShiftLogical;
                }
#endif
                break;
            }

            case GT_MUL:
            {
#if TARGET_XARCH
                assert(op2.Type == simdType);

                if (simdSize is 64)
                {
                    if (varTypeIsFloating(simdBaseType))
                    {
                        id = NI_AVX512_Multiply;
                    }
                    else if (!varTypeIsByte(simdBaseType))
                    {
                        id = NI_AVX512_MultiplyLow;
                    }
                }
                else if (varTypeIsLong(simdBaseType))
                {
                    if (compOpportunisticallyDependsOn(InstructionSet_AVX512))
                    {
                        id = NI_AVX512_MultiplyLow;
                    }
                }
                else if (simdSize is 32)
                {
                    if (varTypeIsFloating(simdBaseType))
                    {
                        id = NI_AVX_Multiply;
                    }
                    else if (!varTypeIsByte(simdBaseType))
                    {
                        assert(compIsaSupportedDebugOnly(InstructionSet_AVX2));
                        id = NI_AVX2_MultiplyLow;
                    }
                }
                else if (varTypeIsFloating(simdBaseType))
                {
                    id = isScalar ? NI_X86Base_MultiplyScalar : NI_X86Base_Multiply;
                }
                else if (!varTypeIsByte(simdBaseType))
                {
                    id = NI_X86Base_MultiplyLow;
                }
#elif TARGET_ARM64
                if ((simdSize is 8) && (isScalar || (simdBaseType is TYP_DOUBLE)))
                {
                    id = NI_AdvSimd_MultiplyScalar;
                }
                else if (simdBaseType is TYP_DOUBLE)
                {
                    id = (op2.Type == simdType) ? NI_AdvSimd_Arm64_Multiply : NI_AdvSimd_Arm64_MultiplyByScalar;
                }
                else if (!varTypeIsLong(simdBaseType))
                {
                    id = (op2.Type == simdType) ? NI_AdvSimd_Multiply : NI_AdvSimd_MultiplyByScalar;
                }
#endif
                break;
            }

            case GT_OR:
            {
                assert(!isScalar);
                assert(op2.Type == simdType);

#if TARGET_XARCH
                if (simdSize is 64)
                {
                    id = NI_AVX512_Or;
                }
                else if (simdSize is 32)
                {
                    if (varTypeIsIntegral(simdBaseType))
                    {
                        assert(compIsaSupportedDebugOnly(InstructionSet_AVX2));
                        id = NI_AVX2_Or;
                    }
                    else
                    {
                        id = NI_AVX_Or;
                    }
                }
                else
                {
                    id = NI_X86Base_Or;
                }
#elif TARGET_ARM64
                id = NI_AdvSimd_Or;
#endif
                break;
            }

            case GT_ROL:
            {
                assert(!isScalar);
                assert((op2.Type == simdType) || varTypeIsInt(op2.Type));
                assert(varTypeIsIntegral(simdBaseType));

#if TARGET_XARCH
                if (!varTypeIsSmall(simdBaseType) && compOpportunisticallyDependsOn(InstructionSet_AVX512))
                {
                    id = varTypeIsInt(op2.Type) ? NI_AVX512_RotateLeft : NI_AVX512_RotateLeftVariable;
                }
#endif // TARGET_XARCH
                break;
            }

            case GT_ROR:
            {
                assert(!isScalar);
                assert((op2.Type == simdType) || varTypeIsInt(op2.Type));
                assert(varTypeIsIntegral(simdBaseType));

#if TARGET_XARCH
                if (!varTypeIsSmall(simdBaseType) && compOpportunisticallyDependsOn(InstructionSet_AVX512))
                {
                    id = varTypeIsInt(op2.Type) ? NI_AVX512_RotateRight : NI_AVX512_RotateRightVariable;
                }
#endif // TARGET_XARCH
                break;
            }

            case GT_RSH:
            {
                assert(!isScalar);
                assert((op2.Type == simdType) || varTypeIsInt(op2.Type));
                assert(varTypeIsIntegral(simdBaseType));

#if TARGET_XARCH
                if (varTypeIsByte(simdBaseType))
                {
                    break;
                }

                if (varTypeIsInt(op2.Type))
                {
                    if ((simdSize is 64) || (simdBaseType.Size is 8))
                    {
                        if (compOpportunisticallyDependsOn(InstructionSet_AVX512))
                        {
                            id = NI_AVX512_ShiftRightArithmetic;
                        }
                    }
                    else if (simdSize is 32)
                    {
                        assert(compIsaSupportedDebugOnly(InstructionSet_AVX2));
                        id = NI_AVX2_ShiftRightArithmetic;
                    }
                    else
                    {
                        id = NI_X86Base_ShiftRightArithmetic;
                    }
                }
                else if ((simdSize is 64) || varTypeIsShort(simdBaseType) || (simdBaseType.Size is 8))
                {
                    if (compOpportunisticallyDependsOn(InstructionSet_AVX512))
                    {
                        id = NI_AVX512_ShiftRightArithmeticVariable;
                    }
                }
                else if (compOpportunisticallyDependsOn(InstructionSet_AVX2))
                {
                    id = NI_AVX2_ShiftRightArithmeticVariable;
                }
#elif TARGET_ARM64
                if ((simdSize is 8) && (simdBaseType.Size is 8))
                {
                    id = op2.Oper.IsCnsIntOrI ? NI_AdvSimd_ShiftRightArithmeticScalar : NI_AdvSimd_ShiftArithmeticScalar;
                }
                else
                {
                    id = op2.Oper.IsCnsIntOrI ? NI_AdvSimd_ShiftRightArithmetic : NI_AdvSimd_ShiftArithmetic;
                }
#endif
                break;
            }

            case GT_RSZ:
            {
                assert(!isScalar);
                assert((op2.Type == simdType) || varTypeIsInt(op2.Type));
                assert(varTypeIsIntegral(simdBaseType));

#if TARGET_XARCH
                if (varTypeIsByte(simdBaseType))
                {
                    break;
                }

                if (varTypeIsInt(op2.Type))
                {
                    if (simdSize is 64)
                    {
                        id = NI_AVX512_ShiftRightLogical;
                    }
                    else if (simdSize is 32)
                    {
                        assert(compIsaSupportedDebugOnly(InstructionSet_AVX2));
                        id = NI_AVX2_ShiftRightLogical;
                    }
                    else
                    {
                        id = NI_X86Base_ShiftRightLogical;
                    }
                }
                else if ((simdSize is 64) || varTypeIsShort(simdBaseType))
                {
                    if (compOpportunisticallyDependsOn(InstructionSet_AVX512))
                    {
                        id = NI_AVX512_ShiftRightLogicalVariable;
                    }
                }
                else if (compOpportunisticallyDependsOn(InstructionSet_AVX2))
                {
                    id = NI_AVX2_ShiftRightLogicalVariable;
                }
#elif TARGET_ARM64
                if ((simdSize is 8) && (simdBaseType.Size is 8))
                {
                    id = varTypeIsInt(op2.Type) ? NI_AdvSimd_ShiftRightLogicalScalar : NI_AdvSimd_ShiftLogicalScalar;
                }
                else
                {
                    id = varTypeIsInt(op2.Type) ? NI_AdvSimd_ShiftRightLogical : NI_AdvSimd_ShiftLogical;
                }
#endif
                break;
            }

            case GT_SUB:
            {
                assert(op2.Type == simdType);

#if TARGET_XARCH
                if (simdSize is 64)
                {
                    id = NI_AVX512_Subtract;
                }
                else if (simdSize is 32)
                {
                    if (varTypeIsIntegral(simdBaseType))
                    {
                        assert(compIsaSupportedDebugOnly(InstructionSet_AVX2));
                        id = NI_AVX2_Subtract;
                    }
                    else
                    {
                        id = NI_AVX_Subtract;
                    }
                }
                else
                {
                    id = isScalar ? NI_X86Base_SubtractScalar : NI_X86Base_Subtract;
                }
#elif TARGET_ARM64
                if ((simdSize is 8) && (isScalar || (simdBaseType.Size is 8)))
                {
                    id = NI_AdvSimd_SubtractScalar;
                }
                else if (simdBaseType == TYP_DOUBLE)
                {
                    id = NI_AdvSimd_Arm64_Subtract;
                }
                else
                {
                    id = NI_AdvSimd_Subtract;
                }
#endif
                break;
            }

            case GT_XOR:
            {
                assert(!isScalar);
                assert(op2.Type == simdType);

#if TARGET_XARCH
                if (simdSize is 64)
                {
                    id = NI_AVX512_Xor;
                }
                else if (simdSize is 32)
                {
                    if (varTypeIsIntegral(simdBaseType))
                    {
                        assert(compIsaSupportedDebugOnly(InstructionSet_AVX2));
                        id = NI_AVX2_Xor;
                    }
                    else
                    {
                        id = NI_AVX_Xor;
                    }
                }
                else
                {
                    id = NI_X86Base_Xor;
                }
#elif TARGET_ARM64
                id = NI_AdvSimd_Xor;
#endif
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }

        return id;
    }

    /// <summary>Returns intrinsic ID based on the oper, base type, and simd size</summary>
    /// <param name="oper">The oper for which to get the intrinsic ID</param>
    /// <param name="type">The return type of the comparison</param>
    /// <param name="op1">The first operand on which oper is executed</param>
    /// <param name="op2">The second operand on which oper is executed</param>
    /// <param name="simdBaseType">The base type on which oper is executed</param>
    /// <param name="simdSize">The simd size on which oper is executed</param>
    /// <param name="isScalar">True if the oper is over scalar data; otherwise false</param>
    /// <param name="reverseCond">True if the oper should be reversed; otherwise false</param>
    /// <returns></returns>
    public NamedIntrinsic GetHWIntrinsicIdForCmpOp(genTreeOps oper, var_types type, GenTree op1, GenTree op2, var_types simdBaseType, byte simdSize, bool isScalar, bool reverseCond = false)
    {
        var simdType = GetSimdTypeForSize(simdSize);
        assert(varTypeIsMask(type) || (type == simdType));

        assert(varTypeIsArithmetic(simdBaseType));
        assert(varTypeIsSimd(simdType));

        assert(op1 is not null);
        assert(op1.Type == simdType);
        assert(op2 is not null);

#if TARGET_XARCH
        if (varTypeIsMask(type))
        {
#if DEBUG
            assert(!isScalar);
            assert(canUseEvexEncodingDebugOnly());
#endif
        }
        else if (simdSize is 32)
        {
            assert(!isScalar);
        }
        else
#endif
            {
                assert((simdSize is 8) || (simdSize is 16));

#if TARGET_ARM64
            assert(!isScalar || (simdSize is 8));
#endif

            assert(!isScalar || varTypeIsFloating(simdBaseType));
        }

        var id = NI_Illegal;

        if (reverseCond)
        {
            oper = oper.ReverseRelop;

            if (varTypeIsIntegral(simdBaseType))
            {
                reverseCond = false;
            }
#if TARGET_ARM64
            else if (oper is not GT_EQ)
            {
                // Unlike xarch, there is no reverse comparison
                // for floating-point and so we cannot actually
                // optimize these. The exception is GT_NE which
                // becomes GT_EQ

                return NI_Illegal;
            }
#endif
        }

        switch (oper)
        {
            case GT_EQ:
            {
                assert(op2.Type == simdType);

#if TARGET_XARCH
                if (varTypeIsMask(type))
                {
                    id = NI_AVX512_CompareEqualMask;
                }
                else if (simdSize is 32)
                {
                    if (varTypeIsIntegral(simdBaseType))
                    {
                        assert(compIsaSupportedDebugOnly(InstructionSet_AVX2));
                        id = NI_AVX2_CompareEqual;
                    }
                    else
                    {
                        id = NI_AVX_CompareEqual;
                    }
                }
                else
                {
                    id = isScalar ? NI_X86Base_CompareScalarEqual : NI_X86Base_CompareEqual;
                }
#elif TARGET_ARM64
                if (simdBaseType.Size is 8)
                {
                    id = (simdSize is 8) ? NI_AdvSimd_Arm64_CompareEqualScalar : NI_AdvSimd_Arm64_CompareEqual;
                }
                else
                {
                    id = NI_AdvSimd_CompareEqual;
                }
#endif
                break;
            }

            case GT_GE:
            {
                assert(op2.Type == simdType);

#if TARGET_XARCH
                if (varTypeIsMask(type))
                {
                    id = reverseCond ? NI_AVX512_CompareNotLessThanMask : NI_AVX512_CompareGreaterThanOrEqualMask;
                }
                else if (varTypeIsIntegral(simdBaseType))
                {
#if DEBUG
                    // This should have been handled by the caller setting type = TYP_MASK
                    assert(!canUseEvexEncodingDebugOnly());
#endif
                }
                else if (simdSize is 32)
                {
                    id = reverseCond ? NI_AVX_CompareNotLessThan : NI_AVX_CompareGreaterThanOrEqual;
                }
                else if (isScalar)
                {
                    id = reverseCond ? NI_X86Base_CompareScalarNotLessThan : NI_X86Base_CompareScalarGreaterThanOrEqual;
                }
                else
                {
                    id = reverseCond ? NI_X86Base_CompareNotLessThan : NI_X86Base_CompareGreaterThanOrEqual;
                }
#elif TARGET_ARM64
                if (simdBaseType.Size is 8)
                {
                    id = (simdSize is 8) ? NI_AdvSimd_Arm64_CompareGreaterThanOrEqualScalar
                                         : NI_AdvSimd_Arm64_CompareGreaterThanOrEqual;
                }
                else
                {
                    id = NI_AdvSimd_CompareGreaterThanOrEqual;
                }
#endif
                    break;
            }

            case GT_GT:
            {
                assert(op2.Type == simdType);

#if TARGET_XARCH
                if (varTypeIsMask(type))
                {
                    id = reverseCond ? NI_AVX512_CompareNotLessThanOrEqualMask : NI_AVX512_CompareGreaterThanMask;
                }
                else if (varTypeIsIntegral(simdBaseType))
                {
                    if (varTypeIsSigned(simdBaseType))
                    {
                        if (simdSize is 32)
                        {
                            assert(compIsaSupportedDebugOnly(InstructionSet_AVX2));
                            id = NI_AVX2_CompareGreaterThan;
                        }
                        else
                        {
                            id = NI_X86Base_CompareGreaterThan;
                        }
                    }
                    else
                    {
#if DEBUG
                        // This should have been handled by the caller setting type = TYP_MASK
                        assert(!canUseEvexEncodingDebugOnly());
#endif
                    }
                }
                else if (simdSize is 32)
                {
                    id = reverseCond ? NI_AVX_CompareNotLessThanOrEqual : NI_AVX_CompareGreaterThan;
                }
                else if (isScalar)
                {
                    id = reverseCond ? NI_X86Base_CompareScalarNotLessThanOrEqual : NI_X86Base_CompareScalarGreaterThan;
                }
                else
                {
                    id = reverseCond ? NI_X86Base_CompareNotLessThanOrEqual : NI_X86Base_CompareGreaterThan;
                }
#elif TARGET_ARM64
            if (simdBaseType.Size is 8)
            {
                id = (simdSize is 8) ? NI_AdvSimd_Arm64_CompareGreaterThanScalar : NI_AdvSimd_Arm64_CompareGreaterThan;
            }
            else
            {
                id = NI_AdvSimd_CompareGreaterThan;
            }
#endif
                        break;
            }

            case GT_LE:
            {
                assert(op2.Type == simdType);

#if TARGET_XARCH
                if (varTypeIsMask(type))
                {
                    id = reverseCond ? NI_AVX512_CompareNotGreaterThanMask : NI_AVX512_CompareLessThanOrEqualMask;
                }
                else if (varTypeIsIntegral(simdBaseType))
                {
#if DEBUG
                    // This should have been handled by the caller setting type = TYP_MASK
                    assert(!canUseEvexEncodingDebugOnly());
#endif
                }
                else if (simdSize is 32)
                {
                    id = reverseCond ? NI_AVX_CompareNotGreaterThan : NI_AVX_CompareLessThanOrEqual;
                }
                else if (isScalar)
                {
                    id = reverseCond ? NI_X86Base_CompareScalarNotGreaterThan : NI_X86Base_CompareScalarLessThanOrEqual;
                }
                else
                {
                    id = reverseCond ? NI_X86Base_CompareNotGreaterThan : NI_X86Base_CompareLessThanOrEqual;
                }
#elif TARGET_ARM64
                if (simdBaseType.Size is 8)
                {
                    id = (simdSize is 8) ? NI_AdvSimd_Arm64_CompareLessThanOrEqualScalar
                                         : NI_AdvSimd_Arm64_CompareLessThanOrEqual;
                }
                else
                {
                    id = NI_AdvSimd_CompareLessThanOrEqual;
                }
#endif
                    break;
            }

            case GT_LT:
            {
                assert(op2.Type == simdType);

                // !GE

#if TARGET_XARCH
                if (varTypeIsMask(type))
                {
                    id = reverseCond ? NI_AVX512_CompareNotGreaterThanOrEqualMask : NI_AVX512_CompareLessThanMask;
                }
                else if (varTypeIsIntegral(simdBaseType))
                {
                    if (varTypeIsSigned(simdBaseType))
                    {
                        if (simdSize is 32)
                        {
                            assert(compIsaSupportedDebugOnly(InstructionSet_AVX2));
                            id = NI_AVX2_CompareLessThan;
                        }
                        else
                        {
                            id = NI_X86Base_CompareLessThan;
                        }
                    }
                    else
                    {
#if DEBUG
                        // This should have been handled by the caller setting type = TYP_MASK
                        assert(!canUseEvexEncodingDebugOnly());
#endif
                    }
                }
                else if (simdSize is 32)
                {
                    id = reverseCond ? NI_AVX_CompareNotGreaterThanOrEqual : NI_AVX_CompareLessThan;
                }
                else if (isScalar)
                {
                    id = reverseCond ? NI_X86Base_CompareScalarNotGreaterThanOrEqual : NI_X86Base_CompareScalarLessThan;
                }
                else
                {
                    id = reverseCond ? NI_X86Base_CompareNotGreaterThanOrEqual : NI_X86Base_CompareLessThan;
                }
#elif TARGET_ARM64
                if (simdBaseType.Size is 8)
                {
                    id = (simdSize is 8) ? NI_AdvSimd_Arm64_CompareLessThanScalar : NI_AdvSimd_Arm64_CompareLessThan;
                }
                else
                {
                    id = NI_AdvSimd_CompareLessThan;
                }
#endif
                        break;
            }

            case GT_NE:
            {
                assert(op2.Type == simdType);

#if TARGET_XARCH
                if (varTypeIsMask(type))
                {
                    id = NI_AVX512_CompareNotEqualMask;
                }
                else if (varTypeIsIntegral(simdBaseType))
                {
#if DEBUG
                    // This should have been handled by the caller setting type = TYP_MASK
                    assert(!canUseEvexEncodingDebugOnly());
#endif
                }
                else if (simdSize is 32)
                {
                    id = NI_AVX_CompareNotEqual;
                }
                else
                {
                    id = isScalar ? NI_X86Base_CompareScalarNotEqual : NI_X86Base_CompareNotEqual;
                }
#endif
                    break;
            }

            default:
            {
                unreached();
                break;
            }
        }

        return id;
    }

    /// <summary>Returns intrinsic ID based on the oper, base type, and simd size</summary>
    /// <param name="oper">The oper for which to get the intrinsic ID</param>
    /// <param name="op1">The first operand on which oper is executed</param>
    /// <param name="simdBaseType">The base type on which oper is executed</param>
    /// <param name="simdSize">The simd size on which oper is executed</param>
    /// <param name="isScalar">True if the oper is over scalar data; otherwise false</param>
    /// <returns>The intrinsic ID based on the oper, base type, and simd size</returns>
    public NamedIntrinsic GetHWIntrinsicIdForUnOp(genTreeOps oper, GenTree op1, var_types simdBaseType, byte simdSize, bool isScalar)
    {
        var simdType = GetSimdTypeForSize(simdSize);

        assert(varTypeIsArithmetic(simdBaseType));
        assert(varTypeIsSimd(simdType));

#if TARGET_XARCH
        if (simdSize is 32 or 64)
        {
            assert(!isScalar);
        }
        else
#endif
        {
#if TARGET_ARM64
            assert(!isScalar || (simdSize is 8));
            // TODO-SVE: Add scalable length support
            assert(simdSize is 8 or 16);
#endif

            assert(!isScalar || varTypeIsFloating(simdBaseType));
        }

        assert(op1 is not null);
        assert(op1.Type == simdType);

        var id = NI_Illegal;

        switch (oper)
        {
            case GT_NEG:
            {
#if TARGET_ARM64
                assert(varTypeIsSigned(simdBaseType) || varTypeIsFloating(simdBaseType));

                if (varTypeIsLong(simdBaseType))
                {
                    id = (simdSize is 8) ? NI_AdvSimd_Arm64_NegateScalar : NI_AdvSimd_Arm64_Negate;
                }
                else if ((simdSize is 8) && (isScalar || (simdBaseType.Size is 8)))
                {
                    id = NI_AdvSimd_NegateScalar;
                }
                else if (simdBaseType == TYP_DOUBLE)
                {
                    id = NI_AdvSimd_Arm64_Negate;
                }
                else
                {
                    id = NI_AdvSimd_Negate;
                }
#endif
                break;
            }

            case GT_NOT:
            {
                assert(!isScalar);

#if TARGET_ARM64
                id = NI_AdvSimd_Not;
#endif
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }

        return id;
    }

    /// <summary>Returns the lookup type for a SIMD comparison operation</summary>
    /// <param name="oper">The comparison operation</param>
    /// <param name="type">The expected IR type of the comparison</param>
    /// <param name="simdBaseType">The base type on which oper is executed</param>
    /// <param name="simdSize">The simd size on which oper is executed</param>
    /// <param name="reverseCond">True if the oper should be reversed; otherwise false</param>
    /// <returns>The lookup type for the given operation given the expected IR type, base type, and simd size</returns>
    /// <remarks>This API is namely meant to assist in handling cases where the underlying instruction return type doesn't match with the type IR wants us to be producing. For example, the consuming node may expect a TYP_SIMD16 but the underlying instruction may produce a TYP_MASK.</remarks>
    public var_types GetLookupTypeForCmpOp(genTreeOps oper, var_types type, var_types simdBaseType, byte simdSize, bool reverseCond = false)
    {
        var simdType = GetSimdTypeForSize(simdSize);
        assert(varTypeIsMask(type) || (type == simdType));

        assert(varTypeIsArithmetic(simdBaseType));
        assert(varTypeIsSimd(simdType));

        var lookupType = type;

#if TARGET_XARCH
        if ((simdSize is 64) || canUseEvexEncoding())
        {
            lookupType = TYP_MASK;
        }
#endif

        return lookupType;
    }
#endif

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
            else if ((optCSEstart >= 0) && (optCSEstart <= lclNum))
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

    /// <summary>determine if a tree produces a runtime type, and if so, how.</summary>
    /// <param name="tree">The tree to examine</param>
    /// <returns>TypeProducerKind for the tree.</returns>
    public unsafe TypeProducerKind gtGetTypeProducerKind(GenTree tree)
    {
        // Notes:
        //    Checks to see if this tree returns a RuntimeType value, and if so,
        //    how that value is determined.
        //
        //    Currently handles these cases
        //    1) The result of Object.GetType
        //    2) The result of typeof(...)
        //    3) A null reference
        //    4) Tree is otherwise known to have type RuntimeType
        //
        //    The null reference case is surprisingly common because operator
        //    overloading turns the otherwise innocuous
        //
        //        Type t = ....;
        //        if (t is null)
        //
        //    into a method call.

        if (tree.Oper is GT_CALL)
        {
            var call = tree.AsCall();

            if (call.IsHelperCall())
            {
                if (gtIsTypeHandleToRuntimeTypeHelper(call))
                {
                    return TPK_Handle;
                }
            }
            else if (call.IsSpecialIntrinsic())
            {
                if (lookupNamedIntrinsic(call._callMethHnd) is NI_System_Object_GetType)
                {
                    return TPK_GetType;
                }
            }
        }
        else if ((tree.Oper is GT_INTRINSIC) && (tree.AsIntrinsic().IntrinsicName is NI_System_Object_GetType))
        {
            return TPK_GetType;
        }
        else if ((tree.Oper is GT_CNS_INT) && (tree.AsIntCon().IconVal is 0))
        {
            return TPK_Null;
        }
        else
        {
            var clsHnd = gtGetClassHandle(tree, out _, out _);

            if ((clsHnd != NO_CLASS_HANDLE) && (clsHnd == info.compCompHnd->getBuiltinClass(CLASSID_RUNTIME_TYPE)))
            {
                return TPK_Other;
            }
        }
        return TPK_Unknown;
    }

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

    private bool gtIsLikelyRegVar(GenTree tree)
    {
        if (!tree.Oper.IsScalarLocal)
        {
            return false;
        }

        ref var varDsc = ref lvaGetDesc(tree.AsLclVar().LclNum);

        if (varDsc.lvDoNotEnregister)
        {
            return false;
        }

        // If this is an EH-live var, return false if it is a def,
        // as it will have to go to memory.
        if (varDsc.lvTracked && varDsc.IsLiveInOutOfHandler && ((tree.Flags & GTF_VAR_DEF) is not 0))
        {
            return false;
        }

        // Be pessimistic if ref counts are not yet set up.
        //
        // Perhaps we should be optimistic though.
        // See notes in GitHub issue 18969.
        if (!lvaLocalVarRefCounted)
        {
            return false;
        }

        if (varDsc.lvRefCntWtd() < (BB_UNITY_WEIGHT * 3))
        {
            return false;
        }

#if TARGET_X86
        if (!varTypeUsesIntReg(tree.Type))
        {
            return false;
        }

        if (varTypeIsLong(tree.Type))
        {
            return false;
        }
#endif

        return true;
    }

    /// <summary>Given an address expression, compute its costs and addressing mode opportunities, and mark addressing mode candidates as GTF_DONT_CSE.</summary>
    /// <param name="addr">The address expression</param>
    /// <param name="costEx">The execution cost of this address expression (in/out arg to be updated)</param>
    /// <param name="costSz">The size cost of this address expression (in/out arg to be updated)</param>
    /// <param name="type">The type of the value being referenced by the parent of this address expression.</param>
    /// <returns>Returns true if it finds an addressing mode.</returns>
    public bool gtMarkAddrMode(GenTree addr, ref byte costEx, ref byte costSz, var_types type)
    {
        // TODO-Throughput - Consider actually instantiating these early, to avoid having to re-run the algorithm that looks for them (might also improve CQ).

        var addrComma = addr;
        addr = addr.EffectiveVal;

        var naturalMul = 0;

#if TARGET_ARM64
        // Multiplier should be a "natural-scale" power of two number which is equal to target's width.
        //
        //   *(ulong*)(data + index * 8); - can be optimized
        //   *(ulong*)(data + index * 7); - can not be optimized
        //     *(int*)(data + index * 2); - can not be optimized
        //
        naturalMul = type.Size;
#endif

        assert(codeGen is not null);

        if (codeGen.genCreateAddrMode(addr.AsOp(), fold: false, naturalMul, out var rev, out var baseAddr, out var idx, out var mul, out var cns))
        {
#if TARGET_ARM64
            assert((mul is 0 or 1) || (mul == naturalMul));
#endif

            // We can form a complex addressing mode, so mark each of the interior
            // nodes with GTF_ADDRMODE_NO_CSE and calculate a more accurate cost.
            addr.Flags |= GTF_ADDRMODE_NO_CSE;

            var originalAddrCostEx = addr.CostEx;
            var originalAddrCostSz = addr.CostSz;
            var addrModeCostEx = (byte)(0);
            var addrModeCostSz = (byte)(0);

#if TARGET_WASM
            NYI_WASM("gtMarkAddrMode");
#else
#if TARGET_XARCH
            // addrmodeCount is the count of items that we used to form
            // an addressing mode.  The maximum value is 4 when we have
            // all of these:   { base, idx, cns, mul }
            //
            var addrmodeCount = 0;
#endif

            if (baseAddr is not null)
            {
                addrModeCostEx += baseAddr.CostEx;
                addrModeCostSz += baseAddr.CostSz;

#if TARGET_XARCH
                addrmodeCount++;
#elif TARGET_ARM
                if ((baseAddr.Oper is GT_LCL_VAR) && ((idx is null) || (cns is 0)))
                {
                    addrModeCostSz -= 1;
                }
#endif
            }

            if (idx is not null)
            {
                addrModeCostEx += idx.CostEx;
                addrModeCostSz += idx.CostSz;

#if TARGET_XARCH
                addrmodeCount++;
#elif TARGET_ARM
                if (mul > 0)
                {
                    addrModeCostSz += 2;
                }
#endif
            }

            if (cns is not 0)
            {
#if TARGET_XARCH
                if ((sbyte)(cns) == cns)
                {
                    addrModeCostSz += 1;
                }
                else
                {
                    addrModeCostSz += 4;
                }

                addrmodeCount++;
#elif TARGET_ARM
                if (cns >= 128) // small offsets fits into a 16-bit instruction
                {
                    if (cns < 4096) // medium offsets require a 32-bit instruction
                    {
                        if (!varTypeIsFloating(type))
                        {
                            addrModeCostSz += 2;
                        }
                    }
                    else
                    {
                        addrModeCostEx += 2; // Very large offsets require movw/movt instructions
                        addrModeCostSz += 8;
                    }
                }
#elif TARGET_ARM64
                if (cns >= (4096 * type.Size))
                {
                    addrModeCostEx += 1;
                    addrModeCostSz += 4;
                }
#elif TARGET_LOONGARCH64 || TARGET_RISCV64
                if (!emitter.isValidSimm12(cns))
                {
                    // TODO-LoongArch64-CQ: tune for LoongArch64.
                    // TODO-RISCV64-CQ: tune for RISCV64.
                    addrModeCostEx += 1;
                    addrModeCostSz += 4;
                }
#else
#error "Unknown TARGET"
#endif
            }

#if TARGET_XARCH
            if (mul is not 0)
            {
                addrmodeCount++;
            }

            // When we form a complex addressing mode we can reduced the costs
            // associated with the interior GT_ADD and GT_LSH nodes:
            //
            //                      GT_ADD      -- reduce this interior GT_ADD by (-3,-3)
            //                      /   \       --
            //                  GT_ADD  'cns'   -- reduce this interior GT_ADD by (-2,-2)
            //                  /   \           --
            //               'base'  GT_LSL     -- reduce this interior GT_LSL by (-1,-1)
            //                      /   \       --
            //                   'idx'  'mul'
            //
            if (addrmodeCount > 1)
            {
                // The number of interior GT_ADD and GT_LSL will always be one less than addrmodeCount
                //
                addrmodeCount--;

                var tmp = addr;
                while (addrmodeCount > 0)
                {
                    // decrement the gtCosts for the interior GT_ADD or GT_LSH node by the remaining addrmodeCount

                    tmp.SetCosts((byte)(tmp.CostEx - addrmodeCount), (byte)(tmp.CostSz - addrmodeCount));
                    addrmodeCount--;

                    if (addrmodeCount > 0)
                    {
                        var tmpOp = tmp.AsOp();

                        var tmpOp1 = tmpOp.Op1;
                        var tmpOp2 = tmpOp.Op2;

                        if ((tmpOp1 != baseAddr) && (tmpOp1.Oper is GT_ADD))
                        {
                            tmp = tmpOp1;
                        }
                        else if (tmpOp2.Oper is GT_LSH)
                        {
                            tmp = tmpOp2;
                        }
                        else if (tmpOp1.Oper is GT_LSH)
                        {
                            tmp = tmpOp1;
                        }
                        else if (tmpOp2.Oper is GT_ADD)
                        {
                            tmp = tmpOp2;
                        }
                        else
                        {
                            // We can very rarely encounter a tree that has a GT_COMMA node
                            // that is difficult to walk, so we just early out without decrementing.
                            addrmodeCount = 0;
                        }
                    }
                }
            }
#endif
#endif

            assert(addr.Oper is GT_ADD);
            assert(!addr.HasOverflowCheck);
            assert(mul is not 1);

            // If we have an addressing mode, we have one of:
            //   [base             + cns]
            //   [       idx * mul      ]  // mul >= 2, else we would use base instead of idx
            //   [       idx * mul + cns]  // mul >= 2, else we would use base instead of idx
            //   [base + idx * mul      ]  // mul can be 0, 2, 4, or 8
            //   [base + idx * mul + cns]  // mul can be 0, 2, 4, or 8
            // Note that mul is 0 is semantically equivalent to mul is 1.
            // Note that cns can be zero.

            assert((baseAddr is not null) || ((idx is not null) && (mul >= 2)));

            // Walk 'addr' identifying non-overflow ADDs that will be part of the address mode.
            // Note that we will be modifying 'op1' and 'op2' so that eventually they should
            // map to the base and index.
            var op1 = addr;
            var op2 = null as GenTree;

            gtWalkOp(ref op1, ref op2, baseAddr, false);

            // op1 and op2 are now descendents of the root GT_ADD of the addressing mode.
#if TARGET_XARCH
            // Walk the operands again (the third operand is unused in this case).
            // This time we will only consider adds with constant op2's, since
            // we have already found either a non-ADD op1 or a non-constant op2.
            // NOTE: we don't support ADD(op1, cns) addressing for ARM/ARM64 yet so
            // this walk makes no sense there.
            gtWalkOp(ref op1, ref op2, null, true);
            assert(op2 is not null);

            // For XARCH we will fold GT_ADDs in the op2 position into the addressing mode, so we call
            // gtWalkOp on both operands of the original GT_ADD.
            // This is not done for ARMARCH. Though the stated reason is that we don't try to create a
            // scaled index, in fact we actually do create them (even base + index*scale + offset).

            // At this point, 'op2' may itself be an ADD of a constant that should be folded
            // into the addressing mode.
            // Walk op2 looking for non-overflow GT_ADDs of constants.
            gtWalkOp(ref op2, ref op1, null, true);
#endif

            var noCSE = (op2 is not null) && (op2.Oper is GT_LSH or GT_MUL);

#if TARGET_RISCV64
            noCSE &= compOpportunisticallyDependsOn(InstructionSet_Zba);
#else
            noCSE &= (mul > 1);
#endif

            if (noCSE)
            {
                assert(op2 is not null);
                op2.Flags |= GTF_ADDRMODE_NO_CSE;

#if TARGET_RISCV64
                // RISC-V addressing mode follows the form: (base + index*scale) + offset.
                // To emit sh1/2/3add.uw, GT_ADD + GT_LSH/MUL + GT_CAST(zero-extend) nodes are required (Zba extension).
                // Disabling CSE for GT_CAST prevents breaking the pattern and ensures emitting sh1/2/3add.uw.
                // Note that emitting sh1/2/3add instructions (without .uw) don't require a GT_CAST node.
                //
                // Example:
                //      ADD
                //      |- ADD
                //      |  |- LCL_VAR       (base)
                //      |  |- LSH (or MUL)  (index * scale)
                //      |     |- GT_CAST    (index, CSE must be disabled here to emit sh1/2/3add.uw)
                //      |        |- OP1     (CSE/ConstCSE allowed here)
                //      |     |- CNS_INT    (scale)
                //      |- CNS_INT          (offset)

                var index = op2.Op1;

                if ((index is not null) && (index.Oper is GT_CAST))
                {
                    assert(index.Type is TYP_I_IMPL);
                    index->gtFlags |= GTF_ADDRMODE_NO_CSE;
                }
#endif
            }

            // Finally, adjust the costs on the parenting COMMAs.
            while (addrComma != addr)
            {
                var addrCostExDelta = originalAddrCostEx - addrModeCostEx;
                var addrCostSzDelta = originalAddrCostSz - addrModeCostSz;

                addrComma.SetCosts((byte)(addrComma.CostEx - addrCostExDelta), (byte)(addrComma.CostSz - addrCostSzDelta));

                var addrCommaOp = addrComma.AsOp();

                var addrCommaOp1 = addrCommaOp.Op1;
                var addrCommaOp2 = addrCommaOp.Op2;

                costEx += addrCommaOp1.CostEx;
                costSz += addrCommaOp1.CostSz;

                addrComma = addrCommaOp2;
            }

            costEx += addrModeCostEx;
            costSz += addrModeCostSz;

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

    public unsafe bool gtIsTypeof(GenTree tree) => gtIsTypeof(tree, out _);

    /// <summary>Checks if the tree is a typeof()</summary>
    /// <param name="tree">the tree that is checked</param>
    /// <param name="handle">set to the type</param>
    /// <returns>Is the tree typeof()</returns>
    public unsafe bool gtIsTypeof(GenTree tree, out CORINFO_CLASS_HANDLE handle)
    {
        if (tree.Oper.IsCall)
        {
            var call = tree.AsCall();

            if (gtIsTypeHandleToRuntimeTypeHelper(call))
            {
                assert(call.Args.CountArgs() is 1);

                var callArg = call.Args.GetArgByIndex(0);
                assert(callArg is not null);

                var hClass = gtGetHelperArgClassHandle(callArg.EarlyNode);

                if (hClass != NO_CLASS_HANDLE)
                {
                    handle = hClass;
                    return true;
                }
            }
        }

        handle = NO_CLASS_HANDLE;
        return false;
    }

    /// <summary>Check if two trees may interfere because of a store in one of the trees.</summary>
    /// <param name="treeWithStores">Tree that may have stores in it</param>
    /// <param name="tree">Tree that may be reading from a local stored to in "treeWithStores"</param>
    /// <returns>False if there is no interference. Returns true if there is any GT_LCL_VAR or GT_LCL_FLD in "tree" whose value depends on a local stored in "treeWithStores". May also return true in cases without interference if the trees are too large and the function runs out of budget.</returns>
    public bool gtMayHaveStoreInterference(GenTree treeWithStores, GenTree tree)
    {
        if (((treeWithStores.Flags & GTF_ASG) is 0) || tree.Oper.IsInvariant)
        {
            return false;
        }

        var visitor = new MayHaveStoreInterferenceVisitor(this, tree);
        return visitor.WalkTree(ref treeWithStores, user: null) == WALK_ABORT;
    }

    public GenTree gtNewAllBitsSetConNode(var_types type)
    {
#if FEATURE_SIMD
        if (varTypeIsSimd(type))
        {
            var allBitsSet = gtNewVconNode(type);
            allBitsSet.SimdVal = simd_t.AllBitsSet;
            return allBitsSet;
        }
#endif

        switch (type)
        {
            case TYP_UBYTE:
            {
                return gtNewIconNode(TYP_INT, 0xFF);
            }

            case TYP_USHORT:
            {
                return gtNewIconNode(TYP_INT, 0xFFFF);
            }

            case TYP_UINT:
            {
                return gtNewIconNode(TYP_INT, unchecked((nint)(0xFFFF_FFFF)));
            }

            case TYP_BYTE:
            case TYP_SHORT:
            case TYP_INT:
            {
                return gtNewIconNode(TYP_INT, -1);
            }

            case TYP_LONG:
            case TYP_ULONG:
            {
                return gtNewLconNode(-1);
            }

            default:
            {
                unreached();
                return null;
            }
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

    /// <summary>Create a new atomic operation node.</summary>
    /// <param name="oper">The atomic oper</param>
    /// <param name="type">Type to store/load</param>
    /// <param name="addr">Destination ("location") address</param>
    /// <param name="value">Value</param>
    /// <param name="comparand">Comparand value for a CMPXCHG</param>
    /// <returns>The created node.</returns>
    public GenTree gtNewAtomicNode(genTreeOps oper, var_types type, GenTree addr, GenTree value, GenTree? comparand = null)
    {
        assert(oper.IsAtomic && ((oper is GT_CMPXCHG) == (comparand is not null)));
        GenTreeIndir node;

        if (comparand is not null)
        {
            node = new GenTreeCmpXchg(type, addr, value, comparand);
            addr.Flags |= GTF_DONT_CSE;
        }
        else
        {
            node = new GenTreeIndir(oper, type, addr, value);
        }

        // All atomics are opaque global stores.
        node.AddAllEffectsFlags(GTF_ASG);

        gtInitializeIndirNode(node, GTF_EMPTY);
        return node;
    }

    public GenTreeOp gtNewBinaryNode(genTreeOps oper, var_types type, GenTree op1, GenTree op2)
    {
        return new GenTreeOp(oper, type, op1, op2);
    }

    /// <summary>Creates a new BitCast node.</summary>
    /// <param name="type">The actual type of the argument</param>
    /// <param name="arg">The argument node</param>
    /// <returns>Returns the newly created BitCast node.</returns>
    public GenTreeUnOp gtNewBitCastNode(var_types type, GenTree arg)
    {
        assert(arg is not null);
        assert(type is not TYP_STRUCT);
        return gtNewUnaryNode(GT_BITCAST, type, arg);
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

    public GenTree gtNewKeepAliveNode(GenTree op)
    {
        var keepalive = gtNewUnaryNode(GT_KEEPALIVE, TYP_VOID, op);

        // Prevent both reordering and removal. Invalid optimizations of GC.KeepAlive are
        // very subtle and hard to observe. Thus we are conservatively marking it with both
        // GTF_CALL and GTF_GLOB_REF side-effects even though it may be more than strictly
        // necessary. The conservative side-effects are unlikely to have negative impact
        // on code quality in this case.
        keepalive.Flags |= (GTF_CALL | GTF_GLOB_REF);

        return keepalive;
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

    /// <summary> create a throw node (calling into JIT helper) that must be thrown.</summary>
    /// <param name="helper">JIT helper ID</param>
    /// <param name="type">return type of the node</param>
    /// <param name="clsHnd"></param>
    /// <returns>pointer to the throw node</returns>
    /// <remarks>The result would be a comma node: COMMA(jithelperthrow(void), x) where x's type should be specified.</remarks>
    public unsafe GenTree gtNewMustThrowException(CorInfoHelpFunc helper, var_types type, CORINFO_CLASS_HANDLE clsHnd)
    {
        var node = gtNewHelperCallNode(TYP_VOID, helper);
        assert(node.IsNoReturn);

        if (type is not TYP_VOID)
        {
            var dummyTemp = lvaGrabTemp(shortLifetime: true, "dummy temp of must thrown exception");

            if (type is TYP_STRUCT)
            {
                // struct type is normalized
                lvaSetStruct(dummyTemp, clsHnd, false);
                type = lvaTable[dummyTemp].Type;
            }
            else
            {
                lvaTable[dummyTemp].Type = type;
            }

            var dummyNode = gtNewLclvNode(type, dummyTemp);
            return gtNewCommaNode(type, node, dummyNode);
        }
        return node;
    }

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
        Unsafe.SkipInit(out InlineArray4<GenTree> inlineArgs);
        var args = (Span<GenTree>)(inlineArgs);

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
            JITDUMP($"Registering {(nuint)(runtimeLookup.signature):x} in SignatureToLookupInfoMap\n");
            signatureToLookupInfoMap[runtimeLookup.signature] = runtimeLookup;
        }
        return helperCall;
    }

    public unsafe GenTreeStrCon gtNewSconNode(int cpx, CORINFO_MODULE_HANDLE scpHandle)
    {
        return new GenTreeStrCon(cpx, scpHandle);
    }

#if FEATURE_HW_INTRINSICS
    public GenTreeHWIntrinsic gtNewSimdHWIntrinsicNode(var_types type, NamedIntrinsic hwIntrinsicId, var_types simdBaseType, byte simdSize, params GenTree[] operands)
    {
        foreach (var operand in operands)
        {
            SetOpLclRelatedToSimdIntrinsic(operand);
        }
        return new GenTreeHWIntrinsic(type, hwIntrinsicId, simdBaseType, simdSize, operands);
    }

    public GenTreeHWIntrinsic gtNewScalarHWIntrinsicNode(var_types type, NamedIntrinsic hwIntrinsicId, params GenTree[] operands)
    {
        foreach (var operand in operands)
        {
            SetOpLclRelatedToSimdIntrinsic(operand);
        }
        return new GenTreeHWIntrinsic(type, hwIntrinsicId, TYP_UNKNOWN, simdSize: 0, operands);
    }

    public GenTree gtNewSimdAbsNode(var_types type, GenTree op1, var_types simdBaseType, byte simdSize)
    {
        assert(varTypeIsSimd(type));
        assert(GetSimdTypeForSize(simdSize) == type);

        assert(op1 is not null);
        assert(op1.Type == type);

        assert(varTypeIsArithmetic(simdBaseType));

        if (varTypeIsUnsigned(simdBaseType))
        {
            return op1;
        }

#if TARGET_XARCH
        if (varTypeIsFloating(simdBaseType))
        {
            // Abs(v) = v & ~new vector<T>(-0.0);
            GenTree bitMask;

            if (simdBaseType == TYP_FLOAT)
            {
                bitMask = gtNewIconNode(TYP_INT, 0x7FFFFFFF);
                bitMask = gtNewSimdCreateBroadcastNode(type, bitMask, TYP_INT, simdSize);
            }
            else
            {
                bitMask = gtNewLconNode(0x7FFFFFFFFFFFFFFF);
                bitMask = gtNewSimdCreateBroadcastNode(type, bitMask, TYP_LONG, simdSize);
            }
            return gtNewSimdBinOpNode(GT_AND, type, op1, bitMask, simdBaseType, simdSize);
        }

        var intrinsic = NI_Illegal;

        if ((simdSize is 64) || (simdBaseType == TYP_LONG))
        {
            if (compOpportunisticallyDependsOn(InstructionSet_AVX512))
            {
                intrinsic = NI_AVX512_Abs;
            }
        }
        else if (simdSize is 32)
        {
            assert(compIsaSupportedDebugOnly(InstructionSet_AVX2));
            intrinsic = NI_AVX2_Abs;
        }
        else
        {
            intrinsic = NI_X86Base_Abs;
        }

        if (intrinsic != NI_Illegal)
        {
            return gtNewSimdHWIntrinsicNode(type, intrinsic, simdBaseType, simdSize, op1);
        }
        else
        {
            var op1Dup1 = fgMakeMultiUse(ref op1);
            var op1Dup2 = gtCloneExpr(op1Dup1);

            // op1 = IsNegative(op1)
            op1 = gtNewSimdIsNegativeNode(type, op1, simdBaseType, simdSize);

            // tmp = -op1Dup1
            var tmp = gtNewSimdUnOpNode(GT_NEG, type, op1Dup1, simdBaseType, simdSize);

            // result = ConditionalSelect(op1, tmp, op1Dup2)
            return gtNewSimdCndSelNode(type, op1, tmp, op1Dup2, simdBaseType, simdSize);
        }
#elif TARGET_ARM64
        var intrinsic = NI_AdvSimd_Abs;

        if (simdBaseType == TYP_DOUBLE)
        {
            intrinsic = (simdSize is 8) ? NI_AdvSimd_AbsScalar : NI_AdvSimd_Arm64_Abs;
        }
        else if (varTypeIsLong(simdBaseType))
        {
            intrinsic = (simdSize is 8) ? NI_AdvSimd_Arm64_AbsScalar : NI_AdvSimd_Arm64_Abs;
        }

        assert(intrinsic != NI_Illegal);
        return gtNewSimdHWIntrinsicNode(type, intrinsic, simdBaseType, simdSize, op1);
#else
#error Unsupported platform
#endif
    }

    public GenTree gtNewSimdBinOpNode(genTreeOps op, var_types type, GenTree op1, GenTree op2, var_types simdBaseType, byte simdSize)
    {
        assert(varTypeIsSimd(type));
        assert(GetSimdTypeForSize(simdSize) == type);

        assert(varTypeIsArithmetic(simdBaseType));

        assert(op1 is not null);
        assert(op2 is not null);

        assert((op1.Type == type) || (op1.Type == simdBaseType) || (op1.Type == simdBaseType.ActualType) || ((op1.Type is TYP_SIMD12) && (type is TYP_SIMD16)));

        if (op is GT_LSH or GT_RSH or GT_RSZ)
        {
            assert(op2.Type.ActualType is TYP_INT);
        }
        else
        {
            assert((op2.Type.ActualType == type.ActualType) || (op2.Type.ActualType == simdBaseType.ActualType) || ((op2.Type is TYP_SIMD12) && (type is TYP_SIMD16)));
        }

        var needsReverseOps = false;
        var op2ForLookup = null as GenTree;

        switch (op)
        {
            case GT_DIV:
            {
                if (varTypeIsArithmetic(op2.Type))
                {
                    op2 = gtNewSimdCreateBroadcastNode(type, op2, simdBaseType, simdSize);
                }
                break;
            }

            case GT_LSH:
            case GT_RSH:
            case GT_RSZ:
            {
                // float and double don't have actual instructions for shifting
                // so we'll just use the equivalent integer instruction instead.

                if (simdBaseType is TYP_FLOAT)
                {
                    simdBaseType = TYP_INT;
                }
                else if (simdBaseType is TYP_DOUBLE)
                {
                    simdBaseType = TYP_LONG;
                }

                // "over shifting" is platform specific behavior. We will match the C# behavior
                // this requires we mask with (sizeof(T) * 8) - 1 which ensures the shift cannot
                // exceed the number of bits available in `T`. This is roughly equivalent to
                // x % (sizeof(T) * 8), but that is "more expensive" and only the same for uint
                // inputs, where-as we have a signed-input and so negative values would differ.

                var shiftCountMask = (simdBaseType.Size * 8) - 1;

                if (op2.Oper.IsCnsIntOrI)
                {
                    op2.AsIntCon().IconVal &= shiftCountMask;

#if TARGET_ARM64
                    // On ARM64, ShiftRight* intrinsics cannot encode a shift value of zero, so use the generic Shift* fallback intrinsic.
                    // GenTreeHWIntrinsic.GetHWIntrinsicIdForBinOp will see that the immediate node is not const, and return the correct fallback intrinsic.

                    if ((op is not GT_LSH) && (op2.AsIntCon().IconVal is 0))
                    {
                        op2 = gtNewZeroConNode(type);
                    }
#endif
                }
                else
                {
                    op2 = gtNewBinaryNode(GT_AND, TYP_INT, op2, gtNewIconNode(TYP_INT, shiftCountMask));

#if TARGET_XARCH
                    op2ForLookup = op2;
                    op2 = gtNewSimdCreateScalarNode(TYP_SIMD16, op2, TYP_INT, 16);
#elif TARGET_ARM64
                    if (op is not GT_LSH)
                    {
                        op2 = gtNewOperNode(GT_NEG, TYP_INT, op2);
                    }

                    op2 = gtNewSimdCreateBroadcastNode(type, op2, simdBaseType, simdSize);
#endif
                }
                break;
            }

            case GT_MUL:
            {
                scoped ref GenTree broadcastOp = ref Unsafe.NullRef<GenTree>();

                if (varTypeIsArithmetic(op1.Type))
                {
                    broadcastOp = ref op1;

#if TARGET_ARM64
                    if (!varTypeIsByte(simdBaseType))
                    {
                        // MultiplyByScalar requires the scalar op to be op2 for GetHWIntrinsicIdForBinOp
                        needsReverseOps = true;
                    }
#endif
                }
                else if (varTypeIsArithmetic(op2.Type))
                {
                    broadcastOp = ref op2;
                }

                if (!Unsafe.IsNullRef(in broadcastOp))
                {
#if TARGET_ARM64
                    if (varTypeIsLong(simdBaseType))
                    {
                        // This is handled via emulation and the scalar is consumed directly
                        break;
                    }
                    else if (!varTypeIsByte(simdBaseType))
                    {
                        op2ForLookup = broadcastOp;
                        broadcastOp = gtNewSimdCreateScalarUnsafeNode(TYP_SIMD8, broadcastOp, simdBaseType, 8);
                        break;
                    }
#endif

                    broadcastOp = gtNewSimdCreateBroadcastNode(type, broadcastOp, simdBaseType, simdSize);
                }
                break;
            }

#if TARGET_XARCH
            case GT_AND:
            case GT_AND_NOT:
            case GT_OR:
            case GT_XOR:
            {
                if (simdSize is 32)
                {
                    if (varTypeIsIntegral(simdBaseType) && !compOpportunisticallyDependsOn(InstructionSet_AVX2))
                    {
                        if (varTypeIsLong(simdBaseType))
                        {
                            simdBaseType = TYP_DOUBLE;
                        }
                        else
                        {
                            simdBaseType = TYP_FLOAT;
                        }
                    }
                }
                break;
            }
#endif

            default:
            {
                break;
            }
        }

        if (needsReverseOps)
        {
            // We expect op1 to have already been spilled if needed
            (op2, op1) = (op1, op2);
        }

        if (op2ForLookup is null)
        {
            op2ForLookup = op2;
        }
        else
        {
            assert(op2ForLookup != op1);
        }

        var intrinsic = GetHWIntrinsicIdForBinOp(op, op1, op2ForLookup, simdBaseType, simdSize, isScalar: false);

        if (intrinsic != NI_Illegal)
        {
            if (op == GT_AND_NOT)
            {
                assert(fgNodeThreading == NodeThreading.LIR);

#if TARGET_XARCH
                // GT_AND_NOT expects `op1 & ~op2`, but xarch does `~op1 & op2`
                // We specially handle this here since we're only producing a
                // native intrinsic node in LIR

                (op1, op2) = (op2, op1);
#endif
            }
            return gtNewSimdHWIntrinsicNode(type, intrinsic, simdBaseType, simdSize, op1, op2);
        }

        switch (op)
        {
            case GT_AND_NOT:
            {
                // Prior to LIR, we want to explicitly decompose this operation so that downstream phases can
                // appropriately optimize around the individual operations being performed, particularly ~op2,
                // and produce overall better codegen.
                assert(fgNodeThreading != NodeThreading.LIR);

                op2 = gtNewSimdUnOpNode(GT_NOT, type, op2, simdBaseType, simdSize);
                return gtNewSimdBinOpNode(GT_AND, type, op1, op2, simdBaseType, simdSize);
            }

#if TARGET_XARCH
            case GT_LSH:
            case GT_RSH:
            case GT_RSZ:
            {
                // This emulates byte shift instructions, which don't exist in x86 SIMD,
                // plus arithmetic shift of qwords, which did not exist before AVX-512.

                assert(varTypeIsByte(simdBaseType) || (varTypeIsLong(simdBaseType) && (op == GT_RSH)));

                // We will emulate arithmetic shift by using logical shift and then masking in the sign bits.
                var instrOp = op == GT_RSH ? GT_RSZ : op;
                intrinsic = GetHWIntrinsicIdForBinOp(instrOp, op1, op2ForLookup, simdBaseType.ActualType, simdSize, isScalar: false);
                assert(intrinsic != NI_Illegal);

                GenTree maskAmountOp;

                if (op2.Oper.IsCnsIntOrI)
                {
                    var shiftCount = (int)(op2.AsIntCon().IconValue);

                    if (varTypeIsByte(simdBaseType))
                    {
                        var mask = (op is GT_LSH) ? ((0xFF << shiftCount) & 0xFF) : (0xFF >> shiftCount);
                        maskAmountOp = gtNewIconNode(type, mask);
                    }
                    else
                    {
                        var mask = -1L >> shiftCount;
                        maskAmountOp = gtNewLconNode(mask);
                    }
                }
                else
                {
                    assert(op2.IsHWIntrinsic(NI_Vector128_CreateScalar));

                    ref var op2Op1Ref = ref op2.AsHWIntrinsic().GetOpRef(1);
                    var shiftCountDup = fgMakeMultiUse(ref op2Op1Ref);

                    if (op is GT_RSH)
                    {
                        // For arithmetic shift, we will be using ConditionalSelect to mask in the sign bits, which means
                        // the mask will be evaluated before the shift. We swap the copied operand with the shift amount
                        // operand here in order to preserve correct evaluation order for the masked shift count.
                        var tmp = shiftCountDup;
                        shiftCountDup = op2Op1Ref;
                        op2Op1Ref = tmp;
                    }

                    maskAmountOp = gtNewBinaryNode(instrOp, simdBaseType.ActualType, gtNewAllBitsSetConNode(simdBaseType), shiftCountDup);
                }

                if (op is GT_RSH)
                {
                    var op1Dup = fgMakeMultiUse(ref op1);
                    var signOp = gtNewSimdCmpOpNode(GT_GT, type, gtNewZeroConNode(type), op1Dup, simdBaseType, simdSize);

                    var shiftType = varTypeIsSmall(simdBaseType) ? TYP_INT : simdBaseType;

                    var shiftOp = gtNewSimdHWIntrinsicNode(type, intrinsic, shiftType, simdSize, op1, op2);
                    var maskOp = gtNewSimdCreateBroadcastNode(type, maskAmountOp, simdBaseType, simdSize);

                    return gtNewSimdCndSelNode(type, maskOp, shiftOp, signOp, simdBaseType, simdSize);
                }
                else
                {
                    var shiftOp = gtNewSimdHWIntrinsicNode(type, intrinsic, TYP_INT, simdSize, op1, op2);
                    var maskOp = gtNewSimdCreateBroadcastNode(type, maskAmountOp, simdBaseType, simdSize);

                    return gtNewSimdBinOpNode(GT_AND, type, shiftOp, maskOp, simdBaseType, simdSize);
                }
            }
#endif

#if TARGET_XARCH && FEATURE_HW_INTRINSICS
            case GT_DIV:
            {
                if (varTypeIsIntegral(simdBaseType))
                {
                    assert(!varTypeIsLong(simdBaseType));
                    if ((varTypeIsSmall(simdBaseType) && simdSize > 16) ||
                        (varTypeIsInt(simdBaseType) && simdSize is 32 &&
                         !compOpportunisticallyDependsOn(InstructionSet_AVX512)) ||
                        simdSize is 64)
                    {
                        var divType = simdSize is 64 ? TYP_SIMD32 : TYP_SIMD16;

                        var op1Dup = fgMakeMultiUse(ref op1);
                        var op2Dup = fgMakeMultiUse(ref op2);

                        var op1Lower = gtNewSimdGetLowerNode(divType, op1, simdBaseType, simdSize);
                        var op2Lower = gtNewSimdGetLowerNode(divType, op2, simdBaseType, simdSize);
                        var divLower = gtNewSimdBinOpNode(GT_DIV, divType, op1Lower, op2Lower, simdBaseType, (byte)(simdSize / 2));

                        var op1Upper = gtNewSimdGetUpperNode(divType, op1Dup, simdBaseType, simdSize);
                        var op2Upper = gtNewSimdGetUpperNode(divType, op2Dup, simdBaseType, simdSize);
                        var divUpper = gtNewSimdBinOpNode(GT_DIV, divType, op1Upper, op2Upper, simdBaseType, (byte)(simdSize / 2));

                        return gtNewSimdWithUpperNode(type, divLower, divUpper, simdBaseType, simdSize);
                    }

                    if (varTypeIsSmall(simdBaseType))
                    {
                        assert(simdSize is 16);

                        if (compOpportunisticallyDependsOn(InstructionSet_AVX512))
                        {
                            var cvtBaseType = varTypeIsUnsigned(simdBaseType) ? TYP_UINT : TYP_INT;

                            var widenCvtIntrinsic = NI_Illegal;
                            var narrowCvtIntrinsic = NI_Illegal;

                            if (varTypeIsByte(simdBaseType))
                            {
                                if (varTypeIsSigned(simdBaseType))
                                {
                                    widenCvtIntrinsic = NI_AVX512_ConvertToVector512Int32;
                                    narrowCvtIntrinsic = NI_AVX512_ConvertToVector128SByte;
                                }
                                else
                                {
                                    widenCvtIntrinsic = NI_AVX512_ConvertToVector512UInt32;
                                    narrowCvtIntrinsic = NI_AVX512_ConvertToVector128Byte;
                                }
                            }
                            else
                            {
                                if (varTypeIsSigned(simdBaseType))
                                {
                                    narrowCvtIntrinsic = NI_AVX512_ConvertToVector128Int16;
                                }
                                else
                                {
                                    narrowCvtIntrinsic = NI_AVX512_ConvertToVector128UInt16;
                                }
                                widenCvtIntrinsic = NI_AVX2_ConvertToVector256Int32;
                            }

                            var cvtType = varTypeIsByte(simdBaseType) ? TYP_SIMD64 : TYP_SIMD32;
                            var cvtSize = (byte)(varTypeIsByte(simdBaseType) ? 64 : 32);

                            op1 = gtNewSimdHWIntrinsicNode(cvtType, widenCvtIntrinsic, simdBaseType, cvtSize, op1);
                            op2 = gtNewSimdHWIntrinsicNode(cvtType, widenCvtIntrinsic, simdBaseType, cvtSize, op2);

                            var div = gtNewSimdBinOpNode(GT_DIV, cvtType, op1, op2, cvtBaseType, cvtSize);

                            return gtNewSimdHWIntrinsicNode(type, narrowCvtIntrinsic, cvtBaseType, cvtSize, div);
                        }
                        else
                        {
                            var signedType = TYP_SHORT;
                            var unsignedType =  TYP_USHORT;

                            if (varTypeIsShort(simdBaseType))
                            {
                                signedType = TYP_INT;
                                unsignedType = TYP_UINT;
                            }

                            var cvtType = varTypeIsSigned(simdBaseType) ? signedType : unsignedType;

                            var op1Dup = fgMakeMultiUse(ref op1);
                            var op2Dup = fgMakeMultiUse(ref op2);

                            var op1LowerWiden = gtNewSimdWidenLowerNode(type, op1, simdBaseType, simdSize);
                            var op2LowerWiden = gtNewSimdWidenLowerNode(type, op2, simdBaseType, simdSize);
                            var divLower = gtNewSimdBinOpNode(GT_DIV, type, op1LowerWiden, op2LowerWiden, cvtType, simdSize);

                            var op1UpperWiden = gtNewSimdWidenUpperNode(type, op1Dup, simdBaseType, simdSize);
                            var op2UpperWiden = gtNewSimdWidenUpperNode(type, op2Dup, simdBaseType, simdSize);
                            var divUpper = gtNewSimdBinOpNode(GT_DIV, type, op1UpperWiden, op2UpperWiden, cvtType, simdSize);

                            return gtNewSimdNarrowNode(type, divLower, divUpper, simdBaseType, simdSize);
                        }
                    }
                    else
                    {

                        assert(varTypeIsInt(simdBaseType));

                        if (compOpportunisticallyDependsOn(InstructionSet_AVX512) && simdSize is 32)
                        {
                            return gtNewSimdHWIntrinsicNode(type, NI_Vector256_op_Division, simdBaseType, simdSize, op1, op2);
                        }

                        assert(simdSize is 16);

                        if (compOpportunisticallyDependsOn(InstructionSet_AVX))
                        {
                            return gtNewSimdHWIntrinsicNode(type, NI_Vector128_op_Division, simdBaseType, simdSize, op1, op2);
                        }

                        var op1Dup = fgMakeMultiUse(ref op1);
                        var op2Dup = fgMakeMultiUse(ref op2);

                        var op1Dup2 = gtCloneExpr(op1Dup);
                        var op2Dup2 = gtCloneExpr(op2Dup);

                        var op1Hi = gtNewSimdHWIntrinsicNode(type, NI_X86Base_MoveHighToLow, TYP_FLOAT, simdSize, op1, op1Dup);
                        var op2Hi = gtNewSimdHWIntrinsicNode(type, NI_X86Base_MoveHighToLow, TYP_FLOAT, simdSize, op2, op2Dup);

                        var divLo = gtNewSimdHWIntrinsicNode(type, NI_Vector128_op_Division, simdBaseType, simdSize, op1Dup2, op2Dup2);
                        var divHi = gtNewSimdHWIntrinsicNode(type, NI_Vector128_op_Division, simdBaseType, simdSize, op1Hi, op2Hi);

                        var div = gtNewSimdHWIntrinsicNode(type, NI_X86Base_MoveLowToHigh, TYP_FLOAT, simdSize, divHi, divLo);
                        return gtNewSimdHWIntrinsicNode(type, NI_X86Base_Shuffle, simdBaseType, simdSize, div, gtNewIconNode(TYP_INT, 0x4E));
                    }
                }

                unreached();
                return null;
            }
#endif

            case GT_MUL:
            {
#if TARGET_XARCH
                if (varTypeIsByte(simdBaseType))
                {
                    if (simdSize is 32 && compOpportunisticallyDependsOn(InstructionSet_AVX512))
                    {
                        // Input is SIMD32 [U]Byte and AVX512 is supported:
                        // - Widen inputs as SIMD64 [U]Short
                        // - Multiply widened inputs (SIMD64 [U]Short) as widened product (SIMD64 [U]Short)
                        // - Narrow widened product (SIMD64 [U]Short) as SIMD32 [U]Byte

                        var widenedSimdBaseType = TYP_USHORT;
                        var widenIntrinsic = NI_AVX512_ConvertToVector512UInt16;
                        var narrowIntrinsic = NI_AVX512_ConvertToVector256Byte;

                        if (simdBaseType is TYP_BYTE)
                        {
                            widenedSimdBaseType = TYP_SHORT;
                            widenIntrinsic = NI_AVX512_ConvertToVector512Int16;
                            narrowIntrinsic = NI_AVX512_ConvertToVector256SByte;
                        }

                        var widenedType = TYP_SIMD64;
                        var widenedSimdSize = (byte)(64);

                        // Vector512<ushort> widenedOp1 = Avx512BW.ConvertToVector512UInt16(op1)
                        var widenedOp1 = gtNewSimdHWIntrinsicNode(widenedType, widenIntrinsic, simdBaseType, widenedSimdSize, op1);

                        // Vector512<ushort> widenedOp2 = Avx512BW.ConvertToVector512UInt16(op2)
                        var widenedOp2 = gtNewSimdHWIntrinsicNode(widenedType, widenIntrinsic, simdBaseType, widenedSimdSize, op2);

                        // Vector512<ushort> widenedProduct = widenedOp1 * widenedOp2;
                        var widenedProduct = gtNewSimdBinOpNode(GT_MUL, widenedType, widenedOp1, widenedOp2, widenedSimdBaseType, widenedSimdSize);

                        // Vector256<byte> product = Avx512BW.ConvertToVector256Byte(widenedProduct)
                        return gtNewSimdHWIntrinsicNode(type, narrowIntrinsic, widenedSimdBaseType, widenedSimdSize, widenedProduct);
                    }
                    else if (simdSize is 16 && compOpportunisticallyDependsOn(InstructionSet_AVX2))
                    {
                        if (compOpportunisticallyDependsOn(InstructionSet_AVX512))
                        {
                            // Input is SIMD16 [U]Byte and AVX512 is supported:
                            // - Widen inputs as SIMD32 [U]Short
                            // - Multiply widened inputs (SIMD32 [U]Short) as widened product (SIMD32 [U]Short)
                            // - Narrow widened product (SIMD32 [U]Short) as SIMD16 [U]Byte

                            var widenIntrinsic = NI_AVX2_ConvertToVector256Int16;
                            var widenedSimdBaseType = TYP_USHORT;
                            var narrowIntrinsic = NI_AVX512_ConvertToVector128Byte;

                            if (simdBaseType is TYP_BYTE)
                            {
                                widenedSimdBaseType = TYP_SHORT;
                                narrowIntrinsic = NI_AVX512_ConvertToVector128SByte;
                            }

                            var widenedType = TYP_SIMD32;
                            var widenedSimdSize = (byte)(32);

                            // Vector256<ushort> widenedOp1 = Avx2.ConvertToVector256Int16(op1).AsUInt16()
                            var widenedOp1 = gtNewSimdHWIntrinsicNode(widenedType, widenIntrinsic, simdBaseType, widenedSimdSize, op1);

                            // Vector256<ushort> widenedOp2 = Avx2.ConvertToVector256Int16(op2).AsUInt16()
                            var widenedOp2 = gtNewSimdHWIntrinsicNode(widenedType, widenIntrinsic, simdBaseType, widenedSimdSize, op2);

                            // Vector256<ushort> widenedProduct = widenedOp1 * widenedOp2
                            var widenedProduct = gtNewSimdBinOpNode(GT_MUL, widenedType, widenedOp1, widenedOp2, widenedSimdBaseType, widenedSimdSize);

                            // Vector128<byte> product = Avx512BW.VL.ConvertToVector128Byte(widenedProduct)
                            return gtNewSimdHWIntrinsicNode(type, narrowIntrinsic, widenedSimdBaseType, widenedSimdSize, widenedProduct);
                        }
                        else
                        {
                            // Input is SIMD16 [U]Byte and AVX512 is NOT supported (only AVX2 will be used):
                            // - Widen inputs as SIMD32 [U]Short
                            // - Multiply widened inputs (SIMD32 [U]Short) as widened product (SIMD32 [U]Short)
                            // - Mask widened product (SIMD32 [U]Short) to select relevant bits
                            // - Pack masked product so that relevant bits are packed together in upper and lower halves
                            // - Shuffle packed product so that relevant bits are placed together in the lower half
                            // - Select lower (SIMD16 [U]Byte) from shuffled product (SIMD32 [U]Short)
                            var widenedSimdBaseType = (simdBaseType is TYP_BYTE) ? TYP_SHORT : TYP_USHORT;
                            var widenIntrinsic = NI_AVX2_ConvertToVector256Int16;
                            var widenedType = TYP_SIMD32;
                            var widenedSimdSize = (byte)(32);

                            // Vector256<ushort> widenedOp1 = Avx2.ConvertToVector256Int16(op1).AsUInt16()
                            var widenedOp1 = gtNewSimdHWIntrinsicNode(widenedType, widenIntrinsic, simdBaseType, widenedSimdSize, op1);

                            // Vector256<ushort> widenedOp2 = Avx2.ConvertToVector256Int16(op2).AsUInt16()
                            var widenedOp2 = gtNewSimdHWIntrinsicNode(widenedType, widenIntrinsic, simdBaseType, widenedSimdSize, op2);

                            // Vector256<ushort> widenedProduct = widenedOp1 * widenedOp2
                            var widenedProduct = gtNewSimdBinOpNode(GT_MUL, widenedType, widenedOp1, widenedOp2, widenedSimdBaseType, widenedSimdSize);

                            // Vector256<ushort> vecCon1 = Vector256.Create(0x00FF00FF00FF00FF).AsUInt16()
                            var vecCon1 = gtNewVconNode(widenedType);
                            vecCon1.EvaluateBroadcastInPlace(TYP_USHORT, 0x00FF);

                            // Vector256<short> maskedProduct = Avx2.And(widenedProduct, vecCon1).AsInt16()
                            var maskedProduct = gtNewSimdBinOpNode(GT_AND, widenedType, widenedProduct, vecCon1, widenedSimdBaseType, widenedSimdSize);
                            var maskedProductDup = fgMakeMultiUse(ref maskedProduct);

                            // Vector256<ulong> packedProduct = Avx2.PackuintSaturate(maskedProduct, maskedProduct).AsUInt64()
                            var packedProduct = gtNewSimdHWIntrinsicNode(widenedType, NI_AVX2_PackUnsignedSaturate, TYP_UBYTE, widenedSimdSize, maskedProduct, maskedProductDup);

                            var permuteBaseType = (simdBaseType == TYP_BYTE) ? TYP_LONG : TYP_ULONG;

                            // Vector256<byte> shuffledProduct = Avx2.Permute4x64(w1, 0xD8).AsByte()
                            var shuffledProduct = gtNewSimdHWIntrinsicNode(widenedType, NI_AVX2_Permute4x64, permuteBaseType, widenedSimdSize, packedProduct, gtNewIconNode(TYP_INT, SHUFFLE_WYZX));

                            // Vector128<byte> product = shuffledProduct.getLower()
                            return gtNewSimdGetLowerNode(type, shuffledProduct, simdBaseType, widenedSimdSize);
                        }
                    }
                    else
                    {
                        // No special handling could be performed, apply fallback logic:
                        // - Widen both inputs lower and upper halves as [U]Short (using helper method)
                        // - Multiply corrsponding widened input halves together as widened product halves
                        // - Narrow widened product halves as [U]Byte (using helper method)
                        var widenedSimdBaseType = simdBaseType == TYP_BYTE ? TYP_SHORT : TYP_USHORT;

                        // op1Dup = op1
                        var op1Dup = fgMakeMultiUse(ref op1);

                        // op2Dup = op2
                        var op2Dup = fgMakeMultiUse(ref op2);

                        // Vector256<ushort> lowerOp1 = Avx2.ConvertToVector256Int16(op1.GetLower()).AsUInt16()
                        var lowerOp1 = gtNewSimdWidenLowerNode(type, op1, simdBaseType, simdSize);

                        // Vector256<ushort> lowerOp2 = Avx2.ConvertToVector256Int16(op2.GetLower()).AsUInt16()
                        var lowerOp2 = gtNewSimdWidenLowerNode(type, op2, simdBaseType, simdSize);

                        // Vector256<ushort> lowerProduct = lowerOp1 * lowerOp2
                        var lowerProduct = gtNewSimdBinOpNode(GT_MUL, type, lowerOp1, lowerOp2, widenedSimdBaseType, simdSize);

                        // Vector256<ushort> upperOp1 = Avx2.ConvertToVector256Int16(op1.GetUpper()).AsUInt16()
                        var upperOp1 = gtNewSimdWidenUpperNode(type, op1Dup, simdBaseType, simdSize);

                        // Vector256<ushort> upperOp2 = Avx2.ConvertToVector256Int16(op2.GetUpper()).AsUInt16()
                        var upperOp2 = gtNewSimdWidenUpperNode(type, op2Dup, simdBaseType, simdSize);

                        // Vector256<ushort> upperProduct = upperOp1 * upperOp2
                        var upperProduct = gtNewSimdBinOpNode(GT_MUL, type, upperOp1, upperOp2, widenedSimdBaseType, simdSize);

                        // Narrow and merge halves using helper method
                        return gtNewSimdNarrowNode(type, lowerProduct, upperProduct, simdBaseType, simdSize);
                    }
                }
                else if (varTypeIsLong(simdBaseType))
                {
                    // This fallback path will be used only if the vpmullq instruction is not available.
                    // The implementation is a simple decomposition using pmuludq, which multiplies
                    // two uint32s and returns a uint64 result.
                    //
                    // aLo * bLo + ((aLo * bHi + aHi * bLo) << 32)

#if DEBUG
                    assert(!canUseEvexEncodingDebugOnly());
                    assert((simdSize is 16) || compIsaSupportedDebugOnly(InstructionSet_AVX2));
#endif

                    var muludq = (simdSize is 16) ? NI_X86Base_Multiply : NI_AVX2_Multiply;

                    var op1Dup1 = fgMakeMultiUse(ref op1);
                    var op1Dup2 = gtCloneExpr(op1Dup1);
                    var op2Dup1 = fgMakeMultiUse(ref op2);
                    var op2Dup2 = gtCloneExpr(op2Dup1);

                    // Vector128<ulong> low = Sse2.Multiply(a.AsUInt32(), b.AsUInt32());
                    var low = gtNewSimdHWIntrinsicNode(type, muludq, TYP_ULONG, simdSize, op1, op2);

                    // Vector128<ulong> mid = (b >>> 32).AsUInt64();
                    var mid = gtNewSimdBinOpNode(GT_RSZ, type, op2Dup1, gtNewIconNode(TYP_INT, 32), simdBaseType, simdSize);

                    // mid = Sse2.Multiply(mid.AsUInt32(), a.AsUInt32());
                    mid = gtNewSimdHWIntrinsicNode(type, muludq, TYP_ULONG, simdSize, mid, op1Dup1);

                    // Vector128<ulong> tmp = (a >>> 32).AsUInt64();
                    var tmp = gtNewSimdBinOpNode(GT_RSZ, type, op1Dup2, gtNewIconNode(TYP_INT, 32), simdBaseType, simdSize);

                    // tmp = Sse2.Multiply(tmp.AsUInt32(), b.AsUInt32());
                    tmp = gtNewSimdHWIntrinsicNode(type, muludq, TYP_ULONG, simdSize, tmp, op2Dup2);

                    // mid += tmp;
                    mid = gtNewSimdBinOpNode(GT_ADD, type, mid, tmp, simdBaseType, simdSize);

                    // mid <<= 32;
                    mid = gtNewSimdBinOpNode(GT_LSH, type, mid, gtNewIconNode(TYP_INT, 32), simdBaseType, simdSize);

                    // return low + mid;
                    return gtNewSimdBinOpNode(GT_ADD, type, low, mid, simdBaseType, simdSize);
                }
#elif TARGET_ARM64
                if (varTypeIsLong(simdBaseType))
                {
                    GenTree** op2ToDup = null;

                    assert(varTypeIsSimd(op1));
                    op1                = gtNewSimdToScalarNode(TYP_LONG, op1, simdBaseType, simdSize);
                    GenTree** op1ToDup = &op1->AsHWIntrinsic()->Op(1);

                    if (varTypeIsSimd(op2))
                    {
                        op2      = gtNewSimdToScalarNode(TYP_LONG, op2, simdBaseType, simdSize);
                        op2ToDup = &op2->AsHWIntrinsic()->Op(1);
                    }

                    // lower = op1.GetElement(0) * op2.GetElement(0)
                    GenTree* lower = gtNewOperNode(GT_MUL, TYP_LONG, op1, op2);

                    if (op2ToDup is null)
                    {
                        op2ToDup = &lower->AsOp()->gtOp2;
                    }
                    lower = gtNewSimdCreateScalarUnsafeNode(type, lower, simdBaseType, simdSize);

                    if (simdSize is 8)
                    {
                        // return Vector64.CreateScalarUnsafe(lower)
                        return lower;
                    }

                    // Make the original op1 and op2 multi-use:
                    GenTree* op1Dup = fgMakeMultiUse(op1ToDup);
                    GenTree* op2Dup = fgMakeMultiUse(op2ToDup);

                    assert(!varTypeIsArithmetic(op1Dup));
                    op1Dup = gtNewSimdGetElementNode(TYP_LONG, op1Dup, gtNewIconNode(1), simdBaseType, simdSize);

                    if (!varTypeIsArithmetic(op2Dup))
                    {
                        op2Dup = gtNewSimdGetElementNode(TYP_LONG, op2Dup, gtNewIconNode(1), simdBaseType, simdSize);
                    }

                    // upper = op1.GetElement(1) * op2.GetElement(1)
                    GenTree* upper = gtNewOperNode(GT_MUL, TYP_LONG, op1Dup, op2Dup);

                    // return Vector128.Create(lower, upper)
                    return gtNewSimdWithElementNode(type, lower, gtNewIconNode(1), upper, simdBaseType, simdSize);
                }
#endif
                    unreached();
                return null;
            }

            default:
            {
                unreached();
                return null;
            }
        }
    }

    /// <summary>Creates a new simd CreateBroadcast node</summary>
    /// <param name="type">The return type of SIMD node being created</param>
    /// <param name="op1">The value of broadcast to every element of the simd value</param>
    /// <param name="simdBaseType">The base type of SIMD type of the intrinsic</param>
    /// <param name="simdSize">The size of the SIMD type of the intrinsic</param>
    /// <returns>The created CreateBroadcast node</returns>
    public GenTree gtNewSimdCreateBroadcastNode(var_types type, GenTree op1, var_types simdBaseType, byte simdSize)
    {
        if (op1.Oper.IsConst)
        {
            var vecCon = gtNewVconNode(type);

            if (op1.Oper.IsIntegralConst)
            {
                vecCon.EvaluateBroadcastInPlace(simdBaseType, op1.AsIntConCommon().IntegralValue);
            }
            else
            {
                assert(op1.Oper.IsCnsFltOrDbl);
                vecCon.EvaluateBroadcastInPlace(simdBaseType, op1.AsDblCon().DconVal);
            }
            return vecCon;
        }

        var hwIntrinsicId = NI_Vector128_Create;

#if TARGET_XARCH
        if (simdSize is 64)
        {
            hwIntrinsicId = NI_Vector512_Create;
        }
        else if (simdSize is 32)
        {
            hwIntrinsicId = NI_Vector256_Create;
        }
#elif TARGET_ARM64
        if (simdSize is 8)
        {
            hwIntrinsicId = NI_Vector64_Create;
        }
#else
#error Unsupported platform
#endif

        return gtNewSimdHWIntrinsicNode(type, hwIntrinsicId, simdBaseType, simdSize, op1);
    }

    public GenTree gtNewSimdCmpOpNode(genTreeOps op, var_types type, GenTree op1, GenTree op2, var_types simdBaseType, byte simdSize)
    {
        assert(varTypeIsSimd(type));
        assert(GetSimdTypeForSize(simdSize) == type);

        assert(op1 is not null);
        assert(op1.Type == type);

        assert(op2 is not null);
        assert(op2.Type == type);

        assert(varTypeIsArithmetic(simdBaseType));

        var lookupType = GetLookupTypeForCmpOp(op, type, simdBaseType, simdSize);
        var intrinsic = GetHWIntrinsicIdForCmpOp(op, lookupType, op1, op2, simdBaseType, simdSize, false);

        if (intrinsic != NI_Illegal)
        {
#if FEATURE_MASKED_HW_INTRINSICS
            if (lookupType != type)
            {
                assert(varTypeIsMask(lookupType));
                var retNode = gtNewSimdHWIntrinsicNode(lookupType, intrinsic, simdBaseType, simdSize, op1, op2);
                return gtNewSimdCvtMaskToVectorNode(type, retNode, simdBaseType, simdSize);
            }
#else
            assert(lookupType == type);
#endif
            return gtNewSimdHWIntrinsicNode(type, intrinsic, simdBaseType, simdSize, op1, op2);
        }

        assert(lookupType == type);

#if DEBUG && TARGET_XARCH
        assert(varTypeIsIntegral(simdBaseType));
        assert(!canUseEvexEncodingDebugOnly());
        assert((simdSize is 16) || ((simdSize is 32) && compOpportunisticallyDependsOn(InstructionSet_AVX2)));
#endif

        switch (op)
        {
#if TARGET_XARCH
            case GT_GE:
            case GT_LE:
            {
                // If we don't have an intrinsic set for this, try "Max(op1, op2) == op1" for GE
                // and "Min(op1, op2) == op1" for LE

                if (!varTypeIsLong(simdBaseType))
                {
                    var op1Dup = fgMakeMultiUse(ref op1);
                    var isMax = (op is GT_GE);

                    // EQ(MinMax(op1, op2), op1)
                    op1 = gtNewSimdMinMaxNativeNode(type, op1, op2, simdBaseType, simdSize, isMax);
                    return gtNewSimdCmpOpNode(GT_EQ, type, op1, op1Dup, simdBaseType, simdSize);
                }
                else
                {
                    // There is no direct support for doing a combined comparison and equality for integral types.
                    // These have to be implemented by performing both halves and combining their results.
                    //
                    // op1Dup = op1
                    // op2Dup = op2
                    //
                    // For greater than:
                    //   op1 = GreaterThan(op1, op2)
                    //   op2 = Equals(op1Dup, op2Dup)
                    //
                    // For less than:
                    //   op1 = LessThan(op1, op2)
                    //   op2 = Equals(op1Dup, op2Dup)
                    //
                    // result = BitwiseOr(op1, op2)

                    var op1Dup = fgMakeMultiUse(ref op1);
                    var op2Dup = fgMakeMultiUse(ref op2);

                    if (op is GT_GE)
                    {
                        op = GT_GT;
                    }
                    else
                    {
                        op = GT_LT;
                    }

                    op1 = gtNewSimdCmpOpNode(op, type, op1, op2, simdBaseType, simdSize);
                    op2 = gtNewSimdCmpOpNode(GT_EQ, type, op1Dup, op2Dup, simdBaseType, simdSize);

                    return gtNewSimdBinOpNode(GT_OR, type, op1, op2, simdBaseType, simdSize);
                }
            }

            case GT_GT:
            case GT_LT:
            {
                assert(varTypeIsUnsigned(simdBaseType));

                // Vector of byte, ushort, uint and ulong:
                // Hardware supports > and < for signed comparison. Therefore, to use it for
                // comparing uint numbers, we subtract a constant from both the
                // operands such that the result fits within the corresponding signed
                // type. The resulting signed numbers are compared using signed comparison.
                //
                // Vector of byte: constant to be subtracted is 2^7
                // Vector of ushort: constant to be subtracted is 2^15
                // Vector of uint: constant to be subtracted is 2^31
                // Vector of ulong: constant to be subtracted is 2^63
                //
                // We need to treat op1 and op2 as signed for comparison purpose after
                // the transformation.

                var opType = simdBaseType;
                var vecCon1 = gtNewVconNode(type);

                switch (simdBaseType)
                {
                    case TYP_UBYTE:
                    {
                        simdBaseType = TYP_BYTE;
                        vecCon1.EvaluateBroadcastInPlace(simdBaseType, sbyte.MinValue);
                        break;
                    }

                    case TYP_USHORT:
                    {
                        simdBaseType = TYP_SHORT;
                        vecCon1.EvaluateBroadcastInPlace(simdBaseType, short.MinValue);
                        break;
                    }

                    case TYP_UINT:
                    {
                        simdBaseType = TYP_INT;
                        vecCon1.EvaluateBroadcastInPlace(simdBaseType, int.MinValue);
                        break;
                    }

                    case TYP_ULONG:
                    {
                        simdBaseType = TYP_LONG;
                        vecCon1.EvaluateBroadcastInPlace(simdBaseType, long.MinValue);
                        break;
                    }

                    default:
                    {
                        unreached();
                        break;
                    }
                }

                var vecCon2 = gtCloneCnsVec(vecCon1);

                // op1 = op1 - constVector
                op1 = gtNewSimdBinOpNode(GT_SUB, type, op1, vecCon1, opType, simdSize);

                // op2 = op2 - constVector
                op2 = gtNewSimdBinOpNode(GT_SUB, type, op2, vecCon2, opType, simdSize);

                return gtNewSimdCmpOpNode(op, type, op1, op2, simdBaseType, simdSize);
            }
#endif

            case GT_NE:
            {
                var result = gtNewSimdCmpOpNode(GT_EQ, type, op1, op2, simdBaseType, simdSize);
                return gtNewSimdUnOpNode(GT_NOT, type, result, simdBaseType, simdSize);
            }

            default:
            {
                unreached();
                return null;
            }
        }
    }

    public GenTree gtNewSimdCmpOpAllNode(genTreeOps op, var_types type, GenTree op1, GenTree op2, var_types simdBaseType, byte simdSize)
    {
        assert(type == TYP_INT);

        var simdType = GetSimdTypeForSize(simdSize);
        assert(varTypeIsSimd(simdType));

        assert(op1 is not null);
        assert(op1.Type == simdType);

        assert(op2 is not null);
        assert(op2.Type == simdType);

        assert(varTypeIsArithmetic(simdBaseType));

        var intrinsic = NI_Illegal;

        switch (op)
        {
#if TARGET_XARCH
            case GT_EQ:
            {
                if (simdSize is 32)
                {
                    assert(varTypeIsFloating(simdBaseType) || compIsaSupportedDebugOnly(InstructionSet_AVX2));
                    intrinsic = NI_Vector256_op_Equality;
                }
                else if (simdSize is 64)
                {
                    intrinsic = NI_Vector512_op_Equality;
                }
                else
                {
                    intrinsic = NI_Vector128_op_Equality;
                }
                break;
            }

            case GT_GE:
            case GT_GT:
            case GT_LE:
            case GT_LT:
            {
                // We want to generate a comparison along the lines of
                // GT_XX(op1, op2).As<T, TInteger>() == Vector128<TInteger>.AllBitsSet

                if (simdSize is 32)
                {
                    // TODO-XArch-CQ: It's a non-trivial amount of work to support these
                    // for floating-point while only utilizing AVX. It would require, among
                    // other things, inverting the comparison and potentially support for a
                    // new Avx.TestNotZ intrinsic to ensure the codegen remains efficient.
                    assert(compIsaSupportedDebugOnly(InstructionSet_AVX2));
                    intrinsic = NI_Vector256_op_Equality;
                }
                else if (simdSize is 64)
                {
                    intrinsic = NI_Vector512_op_Equality;
                }
                else
                {
                    intrinsic = NI_Vector128_op_Equality;
                }

                op1 = gtNewSimdCmpOpNode(op, simdType, op1, op2, simdBaseType, simdSize);
                op2 = gtNewAllBitsSetConNode(simdType);

                if (simdBaseType == TYP_FLOAT)
                {
                    simdBaseType = TYP_INT;
                }
                else if (simdBaseType == TYP_DOUBLE)
                {
                    simdBaseType = TYP_LONG;
                }
                break;
            }
#elif TARGET_ARM64
            case GT_EQ:
            {
                intrinsic = (simdSize is 8) ? NI_Vector64_op_Equality : NI_Vector128_op_Equality;
                break;
            }

            case GT_GE:
            case GT_GT:
            case GT_LE:
            case GT_LT:
            {
                // We want to generate a comparison along the lines of
                // GT_XX(op1, op2).As<T, TInteger>() == Vector128<TInteger>.AllBitsSet

                if (simdSize is 8)
                {
                    intrinsic = NI_Vector64_op_Equality;
                }
                else
                {
                    intrinsic = NI_Vector128_op_Equality;
                }

                op1 = gtNewSimdCmpOpNode(op, simdType, op1, op2, simdBaseType, simdSize);
                op2 = gtNewAllBitsSetConNode(simdType);

                if (simdBaseType == TYP_FLOAT)
                {
                    simdBaseType = TYP_INT;
                }
                else if (simdBaseType == TYP_DOUBLE)
                {
                    simdBaseType = TYP_LONG;
                }
                break;
            }
#else
#error Unsupported platform
#endif

            default:
            {
                unreached();
                break;
            }
        }

        assert(intrinsic != NI_Illegal);
        return gtNewSimdHWIntrinsicNode(type, intrinsic, simdBaseType, simdSize, op1, op2);
    }

    public GenTree gtNewSimdCndSelNode(var_types type, GenTree op1, GenTree op2, GenTree op3, var_types simdBaseType, byte simdSize)
    {
        assert(varTypeIsSimd(type));
        assert(GetSimdTypeForSize(simdSize) == type);

        assert(op1 is not null);
        assert(op1.Type == type);

        assert(op2 is not null);
        assert(op2.Type == type);

        assert(op3 is not null);
        assert(op3.Type == type);

        assert(varTypeIsArithmetic(simdBaseType));

        var intrinsic = NI_Illegal;

#if TARGET_XARCH
        if (simdSize is 64)
        {
#if DEBUG
            assert(canUseEvexEncodingDebugOnly());
#endif

            intrinsic = NI_Vector512_ConditionalSelect;
        }
        else if (simdSize is 32)
        {
            intrinsic = NI_Vector256_ConditionalSelect;
        }
        else
        {
            intrinsic = NI_Vector128_ConditionalSelect;
        }
        return gtNewSimdHWIntrinsicNode(type, intrinsic, simdBaseType, simdSize, op1, op2, op3);
#elif TARGET_ARM64
        return gtNewSimdHWIntrinsicNode(type, NI_AdvSimd_BitwiseSelect, simdBaseType, simdSize, op1, op2, op3);
#else
#error Unsupported platform
#endif
        }

    /// <summary>Creates a new simd CreateScalar node</summary>
    /// <param name="type">The return type of SIMD node being created</param>
    /// <param name="op1">The value of element 0 of the simd value</param>
    /// <param name="simdBaseType">The base type of SIMD type of the intrinsic</param>
    /// <param name="simdSize">The size of the SIMD type of the intrinsic</param>
    /// <returns>The created CreateScalar node</returns>
    public GenTree gtNewSimdCreateScalarNode( var_types type, GenTree op1, var_types simdBaseType, byte simdSize)
    {
        if (op1.Oper.IsConst)
        {
            var vecCon = gtNewVconNode(type);

            if (op1.Oper.IsIntegralConst)
            {
                vecCon.SetElementIntegral(simdBaseType, 0, op1.AsIntConCommon().IntegralValue);
            }
            else
            {
                assert(op1.Oper.IsCnsFltOrDbl);
                vecCon.SetElementFloating(simdBaseType, 0, op1.AsDblCon().DconVal);
            }
            return vecCon;
        }

        var hwIntrinsicId = NI_Vector128_CreateScalar;

#if TARGET_XARCH
        if (simdSize is 32)
        {
            hwIntrinsicId = NI_Vector256_CreateScalar;
        }
        else if (simdSize is 64)
        {
            hwIntrinsicId = NI_Vector512_CreateScalar;
        }
#elif TARGET_ARM64
        if (simdSize is 8)
        {
            hwIntrinsicId = (simdBaseType.Size is 8) ? NI_Vector64_Create : NI_Vector64_CreateScalar;
        }
#else
#error Unsupported platform
#endif

        return gtNewSimdHWIntrinsicNode(type, hwIntrinsicId, simdBaseType, simdSize, op1);
    }

    /// <summary>Creates a new simd CreateScalarUnsafe node</summary>
    /// <param name="type">The return type of SIMD node being created</param>
    /// <param name="op1">The value of element 0 of the simd value</param>
    /// <param name="simdBaseType">The base type of SIMD type of the intrinsic</param>
    /// <param name="simdSize">The size of the SIMD type of the intrinsic</param>
    /// <returns>The created CreateScalarUnsafe node</returns>
    /// <remarks>This API is unsafe as it leaves the upper-bits of the vector undefined</remarks>
    public GenTree gtNewSimdCreateScalarUnsafeNode(var_types type, GenTree op1, var_types simdBaseType, byte simdSize)
    {
        if (op1.Oper.IsConst)
        {
            // Since the upper bits are considered non-deterministic and we can therefore
            // set them to anything, we broadcast the value.
            //
            // We do this as it simplifies the logic and allows certain code paths to
            // have better codegen, such as for 0, AllBitsSet, or certain small constants

            var vecCon = gtNewVconNode(type);

            if (op1.Oper.IsIntegralConst)
            {
                vecCon.EvaluateBroadcastInPlace(simdBaseType, op1.AsIntConCommon().IntegralValue);
            }
            else
            {
                assert(op1.Oper.IsCnsFltOrDbl);
                vecCon.EvaluateBroadcastInPlace(simdBaseType, op1.AsDblCon().DconVal);
            }
            return vecCon;
        }

        var hwIntrinsicId = NI_Vector128_CreateScalarUnsafe;

#if TARGET_XARCH
        if (simdSize is 32)
        {
            hwIntrinsicId = NI_Vector256_CreateScalarUnsafe;
        }
        else if (simdSize is 64)
        {
            hwIntrinsicId = NI_Vector512_CreateScalarUnsafe;
        }
#elif TARGET_ARM64
        if (simdSize is 8)
        {
            hwIntrinsicId = (simdBaseType.Size is 8) ? NI_Vector64_Create : NI_Vector64_CreateScalarUnsafe;
        }
#else
#error Unsupported platform
#endif

        return gtNewSimdHWIntrinsicNode(type, hwIntrinsicId, simdBaseType, simdSize, op1);
    }

#if FEATURE_MASKED_HW_INTRINSICS
    /// <summary>Convert a HW intrinsic mask node to a vector</summary>
    /// <param name="type">The type of the node to convert to</param>
    /// <param name="op1">The node to convert</param>
    /// <param name="simdBaseType">The base type of the converted node</param>
    /// <param name="simdSize">the simd size of the converted node</param>
    /// <returns>The node converted to the given type</returns>
    public GenTree gtNewSimdCvtMaskToVectorNode(var_types type, GenTree op1, var_types simdBaseType, byte simdSize)
    {
        assert(varTypeIsMask(op1.Type));
        assert(varTypeIsSimd(type));
        compMaskConvertUsed = true;

#if TARGET_XARCH
        return gtNewSimdHWIntrinsicNode(type, NI_AVX512_ConvertMaskToVector, simdBaseType, simdSize, op1);
#elif TARGET_ARM64
        return gtNewSimdHWIntrinsicNode(type, NI_Sve_ConvertMaskToVector, simdBaseType, simdSize, op1);
#else
#error Unsupported platform
#endif
    }
#endif

    public GenTree gtNewSimdGetLowerNode(var_types type, GenTree op1, var_types simdBaseType, byte simdSize)
    {
        assert(varTypeIsArithmetic(simdBaseType));

        var intrinsicId = NI_Illegal;

#if TARGET_XARCH
        if (simdSize is 32)
        {
            assert(type == TYP_SIMD16);
            intrinsicId = NI_Vector256_GetLower;
        }
        else
        {
            assert((type == TYP_SIMD32) && (simdSize is 64));
            intrinsicId = NI_Vector512_GetLower;
        }
#elif TARGET_ARM64
        assert((type == TYP_SIMD8) && (simdSize is 16));
        intrinsicId = NI_Vector128_GetLower;
#else
#error Unsupported platform
#endif

        return gtNewSimdHWIntrinsicNode(type, intrinsicId, simdBaseType, simdSize, op1);
    }

    public GenTree gtNewSimdGetUpperNode(var_types type, GenTree op1, var_types simdBaseType, byte simdSize)
    {
        assert(varTypeIsArithmetic(simdBaseType));

        var intrinsicId = NI_Illegal;

#if TARGET_XARCH
        if (simdSize is 32)
        {
            assert(type == TYP_SIMD16);
            intrinsicId = NI_Vector256_GetUpper;
        }
        else
        {
            assert((type == TYP_SIMD32) && (simdSize is 64));
            intrinsicId = NI_Vector512_GetUpper;
        }
#elif TARGET_ARM64
        assert((type == TYP_SIMD8) && (simdSize is 16));
        intrinsicId = NI_Vector128_GetUpper;
#else
#error Unsupported platform
#endif

        return gtNewSimdHWIntrinsicNode(type, intrinsicId, simdBaseType, simdSize, op1);
    }

    /// <summary>Creates a new simd IsEvenInteger node</summary>
    /// <param name="type">The return type of SIMD node being created</param>
    /// <param name="op1">The vector to check for even integers</param>
    /// <param name="simdBaseType">The base type of SIMD type of the intrinsic</param>
    /// <param name="simdSize">The size of the SIMD type of the intrinsic</param>
    /// <returns>The created IsEvenInteger node</returns>
    public GenTree gtNewSimdIsEvenIntegerNode(var_types type, GenTree op1, var_types simdBaseType, byte simdSize)
    {
        assert(varTypeIsSimd(type));
        assert(GetSimdTypeForSize(simdSize) == type);

        assert(op1 is not null);
        assert(op1.Type == type);

        assert(varTypeIsIntegral(simdBaseType));

        op1 = gtNewSimdBinOpNode(GT_AND, type, op1, gtNewOneConNode(type, simdBaseType), simdBaseType, simdSize);
        return gtNewSimdIsZeroNode(type, op1, simdBaseType, simdSize);
    }

    /// <summary>Creates a new simd IsFinite node</summary>
    /// <param name="type">The return type of SIMD node being created</param>
    /// <param name="op1">The vector to check for finite values</param>
    /// <param name="simdBaseType">The base type of SIMD type of the intrinsic</param>
    /// <param name="simdSize">The size of the SIMD type of the intrinsic</param>
    /// <returns>The created IsFinite node</returns>
    public GenTree gtNewSimdIsFiniteNode(var_types type, GenTree op1, var_types simdBaseType, byte simdSize)
    {
        assert(varTypeIsSimd(type));
        assert(GetSimdTypeForSize(simdSize) == type);

        assert(op1 is not null);
        assert(op1.Type == type);

        assert(varTypeIsArithmetic(simdBaseType));

        if (varTypeIsFloating(simdBaseType))
        {
            GenTree cnsNode;

            if (simdBaseType == TYP_FLOAT)
            {
                simdBaseType = TYP_INT;
                cnsNode = gtNewIconNode(TYP_INT, 0x7F800000);
            }
            else
            {
                assert(simdBaseType == TYP_DOUBLE);

                simdBaseType = TYP_LONG;
                cnsNode = gtNewLconNode(0x7FF0000000000000);
            }
            cnsNode = gtNewSimdCreateBroadcastNode(type, cnsNode, simdBaseType, simdSize);

            op1 = gtNewSimdBinOpNode(GT_AND_NOT, type, cnsNode, op1, simdBaseType, simdSize);
            return gtNewSimdCmpOpNode(GT_NE, type, op1, gtNewZeroConNode(type), simdBaseType, simdSize);
        }

        assert(varTypeIsIntegral(simdBaseType));
        return gtNewAllBitsSetConNode(type);
    }

    /// <summary>Creates a new simd IsInfinity node</summary>
    /// <param name="type">The return type of SIMD node being created</param>
    /// <param name="op1">The vector to check for infinities</param>
    /// <param name="simdBaseType">The base type of SIMD type of the intrinsic</param>
    /// <param name="simdSize">The size of the SIMD type of the intrinsic</param>
    /// <returns>The created IsInfinity node</returns>
    public GenTree gtNewSimdIsInfinityNode(var_types type, GenTree op1, var_types simdBaseType, byte simdSize)
    {
        assert(varTypeIsSimd(type));
        assert(GetSimdTypeForSize(simdSize) == type);

        assert(op1 is not null);
        assert(op1.Type == type);

        assert(varTypeIsArithmetic(simdBaseType));

        if (varTypeIsFloating(simdBaseType))
        {
            op1 = gtNewSimdAbsNode(type, op1, simdBaseType, simdSize);
            return gtNewSimdIsPositiveInfinityNode(type, op1, simdBaseType, simdSize);
        }
        return gtNewZeroConNode(type);
    }

    /// <summary>Creates a new simd IsInteger node</summary>
    /// <param name="type">The return type of SIMD node being created</param>
    /// <param name="op1">The vector to check for integers</param>
    /// <param name="simdBaseType">The base type of SIMD type of the intrinsic</param>
    /// <param name="simdSize">The size of the SIMD type of the intrinsic</param>
    /// <returns>The created IsInteger node</returns>
    public GenTree gtNewSimdIsIntegerNode(var_types type, GenTree op1, var_types simdBaseType, byte simdSize)
    {
        assert(varTypeIsSimd(type));
        assert(GetSimdTypeForSize(simdSize) == type);

        assert(op1 is not null);
        assert(op1.Type == type);

        assert(varTypeIsArithmetic(simdBaseType));

        if (varTypeIsFloating(simdBaseType))
        {
            var op1Dup1 = fgMakeMultiUse(ref op1);
            var op1Dup2 = gtCloneExpr(op1Dup1);

            op1 = gtNewSimdIsFiniteNode(type, op1, simdBaseType, simdSize);

            op1Dup1 = gtNewSimdTruncNode(type, op1Dup1, simdBaseType, simdSize);
            var op2 = gtNewSimdCmpOpNode(GT_EQ, type, op1Dup1, op1Dup2, simdBaseType, simdSize);

            return gtNewSimdBinOpNode(GT_AND, type, op1, op2, simdBaseType, simdSize);
        }

        assert(varTypeIsIntegral(simdBaseType));
        return gtNewAllBitsSetConNode(type);
    }

    /// <summary>Creates a new simd IsNaN node</summary>
    /// <param name="type">The return type of SIMD node being created</param>
    /// <param name="op1">The vector to check for NaNs</param>
    /// <param name="simdBaseType">The base type of SIMD type of the intrinsic</param>
    /// <param name="simdSize">The size of the SIMD type of the intrinsic</param>
    /// <returns>The created IsNaN node</returns>
    public GenTree gtNewSimdIsNaNNode(var_types type, GenTree op1, var_types simdBaseType, byte simdSize)
    {
        assert(varTypeIsSimd(type));
        assert(GetSimdTypeForSize(simdSize) == type);

        assert(op1 is not null);
        assert(op1.Type == type);

        assert(varTypeIsArithmetic(simdBaseType));

        if (varTypeIsFloating(simdBaseType))
        {
            var op1Dup = fgMakeMultiUse(ref op1);
            return gtNewSimdCmpOpNode(GT_NE, type, op1, op1Dup, simdBaseType, simdSize);
        }
        return gtNewZeroConNode(type);
    }

    /// <summary>Creates a new simd IsNegative node</summary>
    /// <param name="type">The return type of SIMD node being created</param>
    /// <param name="op1">The vector to check for negatives</param>
    /// <param name="simdBaseType">The base type of SIMD type of the intrinsic</param>
    /// <param name="simdSize">The size of the SIMD type of the intrinsic</param>
    /// <returns>The created IsNegative node</returns>
    public GenTree gtNewSimdIsNegativeNode(var_types type, GenTree op1, var_types simdBaseType, byte simdSize)
    {
        assert(varTypeIsSimd(type));
        assert(GetSimdTypeForSize(simdSize) == type);

        assert(op1 is not null);
        assert(op1.Type == type);

        if (simdBaseType == TYP_FLOAT)
        {
            simdBaseType = TYP_INT;
        }
        else if (simdBaseType == TYP_DOUBLE)
        {
            simdBaseType = TYP_LONG;
        }

        assert(varTypeIsIntegral(simdBaseType));

        if (varTypeIsUnsigned(simdBaseType))
        {
            return gtNewZeroConNode(type);
        }
        return gtNewSimdCmpOpNode(GT_LT, type, op1, gtNewZeroConNode(type), simdBaseType, simdSize);
    }

    /// <summary>Creates a new simd IsNegativeInfinity node</summary>
    /// <param name="type">The return type of SIMD node being created</param>
    /// <param name="op1">The vector to check for negative infinities</param>
    /// <param name="simdBaseType">The base type of SIMD type of the intrinsic</param>
    /// <param name="simdSize">The size of the SIMD type of the intrinsic</param>
    /// <returns>The created IsNegativeInfinity node</returns>
    public GenTree gtNewSimdIsNegativeInfinityNode(var_types type, GenTree op1, var_types simdBaseType, byte simdSize)
    {
        assert(varTypeIsSimd(type));
        assert(GetSimdTypeForSize(simdSize) == type);

        assert(op1 is not null);
        assert(op1.Type == type);

        assert(varTypeIsArithmetic(simdBaseType));

        if (varTypeIsFloating(simdBaseType))
        {
            GenTree cnsNode;

            if (simdBaseType == TYP_FLOAT)
            {
                simdBaseType = TYP_UINT;
                cnsNode = gtNewIconNode(TYP_INT, unchecked((int)(0xFF800000)));
            }
            else
            {
                assert(simdBaseType == TYP_DOUBLE);

                simdBaseType = TYP_ULONG;
                cnsNode = gtNewLconNode(unchecked((long)(0xFFF0000000000000)));
            }

            cnsNode = gtNewSimdCreateBroadcastNode(type, cnsNode, simdBaseType, simdSize);
            return gtNewSimdCmpOpNode(GT_EQ, type, op1, cnsNode, simdBaseType, simdSize);
        }
        return gtNewZeroConNode(type);
    }

    /// <summary>Creates a new simd IsNormal node</summary>
    /// <param name="type">The return type of SIMD node being created</param>
    /// <param name="op1">The vector to check for normal values</param>
    /// <param name="simdBaseType">The base type of SIMD type of the intrinsic</param>
    /// <param name="simdSize">The size of the SIMD type of the intrinsic</param>
    /// <returns>The created IsNormal node</returns>
    public GenTree gtNewSimdIsNormalNode(var_types type, GenTree op1, var_types simdBaseType, byte simdSize)
    {
        assert(varTypeIsSimd(type));
        assert(GetSimdTypeForSize(simdSize) == type);

        assert(op1 is not null);
        assert(op1.Type == type);

        assert(varTypeIsArithmetic(simdBaseType));

        if (varTypeIsFloating(simdBaseType))
        {
            op1 = gtNewSimdAbsNode(type, op1, simdBaseType, simdSize);

            GenTree cnsNode1;
            GenTree cnsNode2;

            if (simdBaseType == TYP_FLOAT)
            {
                simdBaseType = TYP_UINT;

                cnsNode1 = gtNewIconNode(TYP_INT, 0x00800000);
                cnsNode2 = gtNewIconNode(TYP_INT, 0x7F800000 - 0x00800000);
            }
            else
            {
                assert(simdBaseType == TYP_DOUBLE);

                simdBaseType = TYP_ULONG;

                cnsNode1 = gtNewLconNode(0x0010000000000000);
                cnsNode2 = gtNewLconNode(0x7FF0000000000000 - 0x0010000000000000);
            }

            cnsNode1 = gtNewSimdCreateBroadcastNode(type, cnsNode1, simdBaseType, simdSize);
            cnsNode2 = gtNewSimdCreateBroadcastNode(type, cnsNode2, simdBaseType, simdSize);

            op1 = gtNewSimdBinOpNode(GT_SUB, type, op1, cnsNode1, simdBaseType, simdSize);
            return gtNewSimdCmpOpNode(GT_LT, type, op1, cnsNode2, simdBaseType, simdSize);
        }

        assert(varTypeIsIntegral(simdBaseType));
        return gtNewSimdCmpOpNode(GT_NE, type, op1, gtNewZeroConNode(type), simdBaseType, simdSize);
    }

    /// <summary>Creates a new simd IsOddInteger node</summary>
    /// <param name="type">The return type of SIMD node being created</param>
    /// <param name="op1">The vector to check for odd integers</param>
    /// <param name="simdBaseType">The base type of SIMD type of the intrinsic</param>
    /// <param name="simdSize">The size of the SIMD type of the intrinsic</param>
    /// <returns>The created IsOddInteger node</returns>
    public GenTree gtNewSimdIsOddIntegerNode(var_types type, GenTree op1, var_types simdBaseType, byte simdSize)
    {
        assert(varTypeIsSimd(type));
        assert(GetSimdTypeForSize(simdSize) == type);

        assert(op1 is not null);
        assert(op1.Type == type);

        assert(varTypeIsIntegral(simdBaseType));

        op1 = gtNewSimdBinOpNode(GT_AND, type, op1, gtNewOneConNode(type, simdBaseType), simdBaseType, simdSize);
        return gtNewSimdCmpOpNode(GT_NE, type, op1, gtNewZeroConNode(type), simdBaseType, simdSize);
    }

    /// <summary>Creates a new simd IsPositive node</summary>
    /// <param name="type">The return type of SIMD node being created</param>
    /// <param name="op1">The vector to check for positives</param>
    /// <param name="simdBaseType">The base type of SIMD type of the intrinsic</param>
    /// <param name="simdSize">The size of the SIMD type of the intrinsic</param>
    /// <returns>The created IsPositive node</returns>
    public GenTree gtNewSimdIsPositiveNode(var_types type, GenTree op1, var_types simdBaseType, byte simdSize)
    {
        assert(varTypeIsSimd(type));
        assert(GetSimdTypeForSize(simdSize) == type);

        assert(op1 is not null);
        assert(op1.Type == type);

        if (simdBaseType == TYP_FLOAT)
        {
            simdBaseType = TYP_INT;
        }
        else if (simdBaseType == TYP_DOUBLE)
        {
            simdBaseType = TYP_LONG;
        }

        assert(varTypeIsIntegral(simdBaseType));

        if (varTypeIsUnsigned(simdBaseType))
        {
            return gtNewAllBitsSetConNode(type);
        }
        return gtNewSimdCmpOpNode(GT_GE, type, op1, gtNewZeroConNode(type), simdBaseType, simdSize);
    }

    /// <summary>Creates a new simd IsPositiveInfinity node </summary>
    /// <param name="type">The return type of SIMD node being created</param>
    /// <param name="op1">The vector to check for positive infinities</param>
    /// <param name="simdBaseType">The base type of SIMD type of the intrinsic</param>
    /// <param name="simdSize">The size of the SIMD type of the intrinsic</param>
    /// <returns>The created IsPositiveInfinity node</returns>
    public GenTree gtNewSimdIsPositiveInfinityNode(var_types type, GenTree op1, var_types simdBaseType, byte simdSize)
    {
        assert(varTypeIsSimd(type));
        assert(GetSimdTypeForSize(simdSize) == type);

        assert(op1 is not null);
        assert(op1.Type == type);

        assert(varTypeIsArithmetic(simdBaseType));

        if (varTypeIsFloating(simdBaseType))
        {
            GenTree cnsNode;

            if (simdBaseType == TYP_FLOAT)
            {
                simdBaseType = TYP_UINT;
                cnsNode = gtNewIconNode(TYP_INT, 0x7F800000);
            }
            else
            {
                assert(simdBaseType == TYP_DOUBLE);

                simdBaseType = TYP_ULONG;
                cnsNode = gtNewLconNode(0x7FF0000000000000);
            }
            cnsNode = gtNewSimdCreateBroadcastNode(type, cnsNode, simdBaseType, simdSize);

            return gtNewSimdCmpOpNode(GT_EQ, type, op1, cnsNode, simdBaseType, simdSize);
        }
        return gtNewZeroConNode(type);
    }

    /// <summary>Creates a new simd IsSubnormal node</summary>
    /// <param name="type">The return type of SIMD node being created</param>
    /// <param name="op1">The vector to check for subnormal values</param>
    /// <param name="simdBaseType">The base type of SIMD type of the intrinsic</param>
    /// <param name="simdSize">The size of the SIMD type of the intrinsic</param>
    /// <returns>The created IsSubnormal node</returns>
    public GenTree gtNewSimdIsSubnormalNode(var_types type, GenTree op1, var_types simdBaseType, byte simdSize)
    {
        assert(varTypeIsSimd(type));
        assert(GetSimdTypeForSize(simdSize) == type);

        assert(op1 is not null);
        assert(op1.Type == type);

        assert(varTypeIsArithmetic(simdBaseType));

        if (varTypeIsFloating(simdBaseType))
        {
            op1 = gtNewSimdAbsNode(type, op1, simdBaseType, simdSize);

            GenTree cnsNode1;
            GenTree cnsNode2;

            if (simdBaseType == TYP_FLOAT)
            {
                simdBaseType = TYP_UINT;
                cnsNode2 = gtNewIconNode(TYP_INT, 0x007FFFFF);
            }
            else
            {
                assert(simdBaseType == TYP_DOUBLE);
                simdBaseType = TYP_ULONG;
                cnsNode2 = gtNewLconNode(0x000FFFFFFFFFFFFF);
            }

            cnsNode1 = gtNewOneConNode(type, simdBaseType);
            cnsNode2 = gtNewSimdCreateBroadcastNode(type, cnsNode2, simdBaseType, simdSize);

            op1 = gtNewSimdBinOpNode(GT_SUB, type, op1, cnsNode1, simdBaseType, simdSize);

            return gtNewSimdCmpOpNode(GT_LT, type, op1, cnsNode2, simdBaseType, simdSize);
        }
        return gtNewZeroConNode(type);
    }

    /// <summary>Creates a new simd IsZero node</summary>
    /// <param name="type">The return type of SIMD node being created</param>
    /// <param name="op1">The vector to check for Zeroes</param>
    /// <param name="simdBaseType">The base type of SIMD type of the intrinsic</param>
    /// <param name="simdSize">The size of the SIMD type of the intrinsic</param>
    /// <returns>The created IsZero node</returns>
    public GenTree gtNewSimdIsZeroNode(var_types type, GenTree op1, var_types simdBaseType, byte simdSize)
    {
        assert(varTypeIsSimd(type));
        assert(GetSimdTypeForSize(simdSize) == type);

        assert(op1 is not null);
        assert(op1.Type == type);

        assert(varTypeIsArithmetic(simdBaseType));

        return gtNewSimdCmpOpNode(GT_EQ, type, op1, gtNewZeroConNode(type), simdBaseType, simdSize);
    }

    /// <summary>Creates a new HWIntrinsic node that performs a min or max computation that follows IEEE 754 semantics</summary>
    /// <param name="type">The node representing the minimum or maximum operation</param>
    /// <param name="op1">The node representing the minimum or maximum operation</param>
    /// <param name="op2">The node representing the minimum or maximum operation</param>
    /// <param name="simdBaseType">The node representing the minimum or maximum operation</param>
    /// <param name="simdSize">The node representing the minimum or maximum operation</param>
    /// <param name="isMax">The node representing the minimum or maximum operation</param>
    /// <param name="isMagnitude">The node representing the minimum or maximum operation</param>
    /// <param name="isNumber">The node representing the minimum or maximum operation</param>
    /// <returns>The node representing the minimum or maximum operation</returns>
    public GenTree gtNewSimdMinMaxNode(var_types type, GenTree op1, GenTree op2, var_types simdBaseType, byte simdSize, bool isMax, bool isMagnitude, bool isNumber)
    {
        assert(op1 is not null);
        assert(op1.Type == type);

        assert(op2 is not null);
        assert(op2.Type == type);

        assert(varTypeIsArithmetic(simdBaseType));

        var isScalar = false;

        if (simdSize is 0)
        {
            isScalar = true;
            assert(varTypeIsFloating(type));
            assert(simdBaseType == type);
        }
        else if (!varTypeIsLong(simdBaseType))
        {
            assert(varTypeIsSimd(type));
            assert(GetSimdTypeForSize(simdSize) == type);
        }

        var intrinsic = NI_Illegal;

        if (varTypeIsFloating(simdBaseType))
        {
            var retNode = null as GenTree;

#if TARGET_XARCH
            var cnsNode = null as GenTree;
            var otherNode = op2;

            if (isScalar)
            {
                if (op1.Oper.IsCnsFltOrDbl)
                {
                    cnsNode = op1;
                    otherNode = op2;
                }
                else if (op2.Oper.IsCnsFltOrDbl)
                {
                    cnsNode = op2;
                    otherNode = op1;
                }

                simdSize = 16;
                type = TYP_SIMD16;
            }
            else if (op1.Oper.IsCnsVec)
            {
                cnsNode = op1;
                otherNode = op2;
            }
            else if (op2.Oper.IsCnsVec)
            {
                cnsNode = op2;
                otherNode = op1;
            }

            // ctrlByte: A control byte (imm8) that specifies the type of min/max operation and sign behavior for AVX512+
            //  - Bits [1:0] (Op-select): Determines the operation performed:
            //      - 0b00: minimum - Returns x if x ≤ y, otherwise y; NaN handling applies.
            //      - 0b01: maximum - Returns x if x ≥ y, otherwise y; NaN handling applies.
            //      - 0b10: minimumMagnitude - Compares absolute values, returns the smaller magnitude.
            //      - 0b11: maximumMagnitude - Compares absolute values, returns the larger magnitude.
            //  - Bits [3:2] (Sign control): Defines how the result’s sign is determined:
            //      - 0b00: Select sign from the first operand (src1).
            //      - 0b01: Select sign from the comparison result.
            //      - 0b10: Force result sign to 0 (positive).
            //      - 0b11: Force result sign to 1 (negative).
            //
            // AVX10.2 additionally adds:
            //  - Bit [4] (min/max mode): Determines whether the instruction follows IEEE-compliant NaN handling:
            //      - 0b0: Standard min/max (propagates NaNs).
            //      - 0b1: Number-preferential min/max (ignores signaling NaNs).
            //
            var ctrlByte = 0x04; // Select sign from comparison result
            ctrlByte |= isMax ? 0x01 : 0x00;
            ctrlByte |= isMagnitude ? 0x02 : 0x00;

            if (compOpportunisticallyDependsOn(InstructionSet_AVX10v2))
            {
                if (isScalar)
                {
                    op1 = gtNewSimdCreateScalarUnsafeNode(type, op1, simdBaseType, simdSize);
                    op2 = gtNewSimdCreateScalarUnsafeNode(type, op2, simdBaseType, simdSize);
                }

                ctrlByte |= isNumber ? 0x10 : 0x00;

                var op3 = gtNewIconNode(TYP_INT, ctrlByte);
                intrinsic = isScalar ? NI_AVX10v2_MinMaxScalar : NI_AVX10v2_MinMax;

                retNode = gtNewSimdHWIntrinsicNode(type, intrinsic, simdBaseType, simdSize, op1, op2, op3);
            }
            else if ((cnsNode is not null) && !otherNode.Oper.IsConst)
            {
                var isNaN = false;

                if (isScalar)
                {
                    isNaN = cnsNode.IsFloatNaN;
                }
                else
                {
                    isNaN = cnsNode.IsVectorNaN(simdBaseType);
                }

                if (isNaN)
                {
                    if (isNumber)
                    {
                        return otherNode;
                    }
                    else
                    {
                        return cnsNode;
                    }
                }

                if (!isMagnitude)
                {
                    var needsFixup = false;
                    var canHandle = false;

                    if (isMax)
                    {
                        // xarch max return op2 if both inputs are 0 of either sign
                        // we require +0 to be greater than -0 we also require NaN to
                        // not be propagated for isNumber and to be propagated otherwise.
                        //
                        // This means for isNumber we want to do `max other, cns` and
                        // can only handle cns being -0 if Avx512F is supported. This is
                        // because if other was NaN, we want to return the non-NaN cns.
                        // But if cns was -0 and other was +0 we'd want to return +0 and
                        // so need to be able to fixup the result.
                        //
                        // For !isNumber we have the inverse and want `max cns, other` and
                        // can only handle cns being +0 if Avx512F is supported. This is
                        // because if other was NaN, we want to return other and if cns
                        // was +0 and other was -0 we'd want to return +0 and so need
                        // so need to be able to fixup the result.

                        if (isNumber)
                        {
                            if (isScalar)
                            {
                                needsFixup = cnsNode.IsFloatNegativeZero;
                            }
                            else
                            {
                                needsFixup = cnsNode.IsVectorNegativeZero(simdBaseType);
                            }
                        }
                        else if (isScalar)
                        {
                            needsFixup = cnsNode.IsFloatPositiveZero;
                        }
                        else
                        {
                            needsFixup = cnsNode.IsVectorZero;
                        }

                        if (!needsFixup || compOpportunisticallyDependsOn(InstructionSet_AVX512))
                        {
                            // Given the checks, op1 can safely be the cns and op2 the other node

                            intrinsic = isScalar ? NI_X86Base_MaxScalar : NI_X86Base_Max;

                            op1 = cnsNode;
                            op2 = otherNode;

                            canHandle = true;
                        }
                    }
                    else
                    {
                        // xarch min return op2 if both inputs are 0 of either sign
                        // we require -0 to be lesser than +0, we also require NaN to
                        // not be propagated for isNumber and to be propagated otherwise.
                        //
                        // This means for isNumber we want to do `min other, cns` and
                        // can only handle cns being +0 if Avx512F is supported. This is
                        // because if other was NaN, we want to return the non-NaN cns.
                        // But if cns was +0 and other was -0 we'd want to return -0 and
                        // so need to be able to fixup the result.
                        //
                        // For !isNumber we have the inverse and want `min cns, other` and
                        // can only handle cns being -0 if Avx512F is supported. This is
                        // because if other was NaN, we want to return other and if cns
                        // was -0 and other was +0 we'd want to return -0 and so need
                        // so need to be able to fixup the result.

                        if (isNumber)
                        {
                            if (isScalar)
                            {
                                needsFixup = cnsNode.IsFloatPositiveZero;
                            }
                            else
                            {
                                needsFixup = cnsNode.IsVectorZero;
                            }
                        }
                        else if (isScalar)
                        {
                            needsFixup = cnsNode.IsFloatNegativeZero;
                        }
                        else
                        {
                            needsFixup = cnsNode.IsVectorZero;
                        }
                        {
                            needsFixup = cnsNode.IsVectorNegativeZero(simdBaseType);
                        }

                        if (!needsFixup || compOpportunisticallyDependsOn(InstructionSet_AVX512))
                        {
                            // Given the checks, op1 can safely be the cns and op2 the other node

                            intrinsic = isScalar ? NI_X86Base_MinScalar : NI_X86Base_Min;

                            op1 = cnsNode;
                            op2 = otherNode;

                            canHandle = true;
                        }
                    }

                    if (canHandle)
                    {
                        assert(op1.Oper.IsConst && !op2.Oper.IsConst);

                        if (isScalar)
                        {
                            var vecCon = gtNewVconNode(type);
                            vecCon.EvaluateBroadcastInPlace(simdBaseType, cnsNode.AsDblCon().DconVal);

                            op1 = vecCon;
                            op2 = gtNewSimdCreateScalarUnsafeNode(type, op2, simdBaseType, simdSize);
                        }

                        retNode = gtNewSimdHWIntrinsicNode(type, intrinsic, simdBaseType, simdSize, op1, op2);

                        if (needsFixup)
                        {
                            var op2Clone = fgMakeMultiUse(ref op2);
                            retNode.AsHWIntrinsic().GetOpRef(2) = op2;

                            var tblVecCon = gtNewVconNode(type);

                            // FixupScalar(left, right, table, control) computes the input type of right
                            // adjusts it based on the table and then returns
                            //
                            // In our case, left is going to be the result of the RangeScalar operation
                            // and right is going to be op1 or op2. In the case op1/op2 is QNaN or SNaN
                            // we want to preserve it instead. Otherwise we want to preserve the original
                            // result computed by RangeScalar.
                            //
                            // If both inputs are NaN, then we'll end up taking op1 by virtue of it being
                            // the latter fixup.

                            if (isMax)
                            {
                                // QNAN: 0b0000:  Preserve left
                                // SNAN: 0b0000
                                // ZERO: 0b1000:  +0
                                // +ONE: 0b0000
                                // -INF: 0b0000
                                // +INF: 0b0000
                                // -VAL: 0b0000
                                // +VAL: 0b0000

                                var tblValue = 0x00000800;
                                tblVecCon.EvaluateBroadcastInPlace((simdBaseType == TYP_FLOAT) ? TYP_INT : TYP_LONG, tblValue);
                            }
                            else
                            {
                                // QNAN: 0b0000:  Preserve left
                                // SNAN: 0b0000
                                // ZERO: 0b0111:  -0
                                // +ONE: 0b0000
                                // -INF: 0b0000
                                // +INF: 0b0000
                                // -VAL: 0b0000
                                // +VAL: 0b0000

                                var tblValue = 0x00000700;
                                tblVecCon.EvaluateBroadcastInPlace((simdBaseType == TYP_FLOAT) ? TYP_INT : TYP_LONG, tblValue);
                            }

                            intrinsic = isScalar ? NI_AVX512_FixupScalar : NI_AVX512_Fixup;

                            retNode = gtNewSimdHWIntrinsicNode(type, intrinsic, simdBaseType, simdSize, retNode, op2Clone, tblVecCon, gtNewIconNode(TYP_INT, 0));
                        }

                        if (isNumber)
                        {
                            // Swap the operands so that the cnsNode is op1, this prevents
                            // the unknown value (which could be NaN) from being selected.

                            retNode.AsHWIntrinsic().GetOpRef(1) = op2;
                            retNode.AsHWIntrinsic().GetOpRef(2) = op1;
                        }
                    }
                }
            }

            if (retNode is null)
            {
                if (isScalar)
                {
                    op1 = gtNewSimdCreateScalarUnsafeNode(type, op1, simdBaseType, simdSize);
                    op2 = gtNewSimdCreateScalarUnsafeNode(type, op2, simdBaseType, simdSize);
                }

                if (compOpportunisticallyDependsOn(InstructionSet_AVX512))
                {
                    // We are constructing a chain of intrinsics similar to:
                    //    var tmp = Avx512DQ.Range(op1, op2, imm8);
                    //    var tbl = Vector128.Create(...);
                    //
                    //    tmp = Avx512F.FixupScalar(tmp, op2, tbl, 0x00);
                    //    tmp = Avx512F.FixupScalar(tmp, op1, tbl, 0x00);
                    //
                    //    return tmp;

                    // Range operates by default almost as MaxNumber or MinNumber
                    // but, it propagates sNaN and does not propagate qNaN. So we need
                    // an additional fixup to ensure we propagate qNaN as well.

                    var op1Clone = fgMakeMultiUse(ref op1);
                    var op2Clone = fgMakeMultiUse(ref op2);

                    var op3 = gtNewIconNode(TYP_INT, ctrlByte);
                    intrinsic = isScalar ? NI_AVX512_RangeScalar : NI_AVX512_Range;

                    if (isNumber)
                    {
                        retNode = gtNewSimdHWIntrinsicNode(type, intrinsic, simdBaseType, simdSize, op1Clone, op2Clone, op3);
                    }
                    else
                    {
                        retNode = gtNewSimdHWIntrinsicNode(type, intrinsic, simdBaseType, simdSize, op1, op2, op3);
                    }

                    // FixupScalar(left, right, table, control) computes the input type of right
                    // adjusts it based on the table and then returns
                    //
                    // In our case, left is going to be the result of the RangeScalar operation,
                    // which is either sNaN or a normal value, and right is going to be op1 or op2.

                    var tblVecCon1 = gtNewVconNode(type);
                    var tblVecCon2 = gtNewVconNode(type);

                    // We currently have (commutative)
                    // * snan, snan = snan
                    // * snan, qnan = snan
                    // * snan, norm = snan
                    // * qnan, qnan = qnan
                    // * qnan, norm = norm
                    // * norm, norm = norm

                    intrinsic = isScalar ? NI_AVX512_FixupScalar : NI_AVX512_Fixup;

                    if (isNumber)
                    {
                        // We need to fixup the case of:
                        // * snan, norm = snan
                        //
                        // Instead, it should be:
                        // * snan, norm = norm

                        // First look at op1 and op2 using op2 as the classification
                        //
                        // If op2 is norm, we take op2 (norm)
                        // If op2 is  nan, we take op1 ( nan or norm)
                        //
                        // Thus, if one input was norm the fixup is now norm

                        // QNAN: 0b0000:  Preserve left
                        // SNAN: 0b0000
                        // ZERO: 0b0001:  Preserve right
                        // +ONE: 0b0001
                        // -INF: 0b0001
                        // +INF: 0b0001
                        // -VAL: 0b0001
                        // +VAL: 0b0001

                        var tblValue = 0x11111100;
                        tblVecCon1.EvaluateBroadcastInPlace((simdBaseType == TYP_FLOAT) ? TYP_INT : TYP_LONG, tblValue);
                        tblVecCon2.EvaluateBroadcastInPlace((simdBaseType == TYP_FLOAT) ? TYP_INT : TYP_LONG, tblValue);

                        // Next look at result and fixup using result as the classification
                        //
                        // If result is norm, we take the result (norm)
                        // If result is  nan, we take the fixup  ( nan or norm)
                        //
                        // Thus if either input was snan, we now have norm as expected
                        // Otherwise, the result was already correct

                        op1 = gtNewSimdHWIntrinsicNode(type, intrinsic, simdBaseType, simdSize, op1, op2, tblVecCon1, gtNewIconNode(TYP_INT, 0));
                        retNode = gtNewSimdHWIntrinsicNode(type, intrinsic, simdBaseType, simdSize, op1, retNode, tblVecCon2, gtNewIconNode(TYP_INT, 0));
                    }
                    else
                    {
                        // We need to fixup the case of:
                        // * qnan, norm = norm
                        //
                        // Instead, it should be:
                        // * qnan, norm = qnan

                        // First look at op1 and op2 using op2 as the classification
                        //
                        // If op2 is norm, we take op1 ( nan or norm)
                        // If op2 is snan, we take op1 ( nan or norm)
                        // If op2 is qnan, we take op2 (qnan)
                        //
                        // Thus, if either input was qnan the fixup is now qnan

                        // QNAN: 0b0001:  Preserve right
                        // SNAN: 0b0000:  Preserve left
                        // ZERO: 0b0000
                        // +ONE: 0b0000
                        // -INF: 0b0000
                        // +INF: 0b0000
                        // -VAL: 0b0000
                        // +VAL: 0b0000

                        var tblValue = 0x00000001;
                        tblVecCon1.EvaluateBroadcastInPlace((simdBaseType == TYP_FLOAT) ? TYP_INT : TYP_LONG, tblValue);
                        tblVecCon2.EvaluateBroadcastInPlace((simdBaseType == TYP_FLOAT) ? TYP_INT : TYP_LONG, tblValue);

                        // Next look at result and fixup using fixup as the classification
                        //
                        // If fixup is norm, we take the result (norm)
                        // If fixup is sNaN, we take the result (sNaN)
                        // If fixup is qNaN, we take the fixup  (qNaN)
                        //
                        // Thus if the fixup was qnan, we now have qnan as expected
                        // Otherwise, the result was already correct

                        op1Clone = gtNewSimdHWIntrinsicNode(type, intrinsic, simdBaseType, simdSize, op1Clone, op2Clone, tblVecCon1, gtNewIconNode(TYP_INT, 0));
                        retNode = gtNewSimdHWIntrinsicNode(type, intrinsic, simdBaseType, simdSize, retNode, op1Clone, tblVecCon2, gtNewIconNode(TYP_INT, 0));
                    }
                }
            }
#elif TARGET_ARM64
            if (!isMagnitude && !isNumber)
            {
                return gtNewSimdMinMaxNativeNode(type, op1, op2, simdBaseType, simdSize, isMax);
            }

            if (isScalar)
            {
                simdSize = 8;
                type     = TYP_SIMD8;

                op1 = gtNewSimdCreateScalarUnsafeNode(type, op1, simdBaseType, simdSize);
                op2 = gtNewSimdCreateScalarUnsafeNode(type, op2, simdBaseType, simdSize);
            }
#else
            assert(!isScalar);
#endif

            if (retNode is null)
            {
                var op1Dup = fgMakeMultiUse(ref op1);
                var op2Dup = fgMakeMultiUse(ref op2);

                var absOp1Dup = null as GenTree;
                var absOp2Dup = null as GenTree;

                var equalsMask = null as GenTree;
                var signMask = null as GenTree;
                var nanMask = null as GenTree;
                var cmpMask = null as GenTree;

                // | name               | cmpMask                 | nanMask     | equalsMask         | signMask      |
                // | ------------------ | ----------------------- | ----------- | ------------------ | ------------- |
                // | Max                | LessThan(y, x)          | IsNaN(x)    | Equals(x, y)       | IsNegative(y) |
                // | Min                | LessThan(x, y)          | IsNaN(x)    | Equals(x, y)       | IsNegative(x) |
                // | MaxMagnitude       | GreaterThan(xMag, yMag) | IsNaN(xMag) | Equals(xMag, yMag) | IsPositive(x) |
                // | MinMagnitude       | LessThan(xMag, yMag)    | IsNaN(xMag) | Equals(xMag, yMag) | IsNegative(x) |
                // | MaxMagnitudeNumber | GreaterThan(xMag, yMag) | IsNaN(yMag) | Equals(xMag, yMag) | IsPositive(x) |
                // | MinMagnitudeNumber | LessThan(xMag, yMag)    | IsNaN(yMag) | Equals(xMag, yMag) | IsNegative(x) |
                // | MaxNumber          | LessThan(y, x)          | IsNaN(y)    | Equals(x, y)       | IsNegative(y) |
                // | MinNumber          | LessThan(x, y)          | IsNaN(y)    | Equals(x, y)       | IsNegative(x) |

                if (isMagnitude)
                {
                    var absOp1 = gtNewSimdAbsNode(type, op1, simdBaseType, simdSize);
                    var absOp2 = gtNewSimdAbsNode(type, op2, simdBaseType, simdSize);

                    absOp1Dup = fgMakeMultiUse(ref absOp1);
                    absOp2Dup = fgMakeMultiUse(ref absOp2);

                    equalsMask = gtNewSimdCmpOpNode(GT_EQ, type, absOp1, absOp2, simdBaseType, simdSize);

                    if (isMax)
                    {
                        signMask = gtNewSimdIsPositiveNode(type, op1Dup, simdBaseType, simdSize);
                        cmpMask = gtNewSimdCmpOpNode(GT_GT, type, absOp1Dup, absOp2Dup, simdBaseType, simdSize);
                    }
                    else
                    {
                        signMask = gtNewSimdIsNegativeNode(type, op1Dup, simdBaseType, simdSize);
                        cmpMask = gtNewSimdCmpOpNode(GT_LT, type, absOp1Dup, absOp2Dup, simdBaseType, simdSize);
                    }

                    if (isNumber)
                    {
                        nanMask = gtNewSimdIsNaNNode(type, gtCloneExpr(absOp2Dup), simdBaseType, simdSize);
                    }
                    else
                    {
                        nanMask = gtNewSimdIsNaNNode(type, gtCloneExpr(absOp1Dup), simdBaseType, simdSize);
                    }
                }
                else
                {
                    equalsMask = gtNewSimdCmpOpNode(GT_EQ, type, op1, op2, simdBaseType, simdSize);

                    if (isMax)
                    {
                        signMask = gtNewSimdIsNegativeNode(type, op2Dup, simdBaseType, simdSize);
                        cmpMask = gtNewSimdCmpOpNode(GT_LT, type, gtCloneExpr(op2Dup), op1Dup, simdBaseType, simdSize);
                    }
                    else
                    {
                        signMask = gtNewSimdIsNegativeNode(type, op1Dup, simdBaseType, simdSize);
                        cmpMask = gtNewSimdCmpOpNode(GT_LT, type, gtCloneExpr(op1Dup), op2Dup, simdBaseType, simdSize);
                    }

                    if (isNumber)
                    {
                        nanMask = gtNewSimdIsNaNNode(type, gtCloneExpr(op2Dup), simdBaseType, simdSize);
                    }
                    else
                    {
                        nanMask = gtNewSimdIsNaNNode(type, gtCloneExpr(op1Dup), simdBaseType, simdSize);
                    }

                    op2Dup = gtCloneExpr(op2Dup);
                }

                var mask = gtNewSimdBinOpNode(GT_AND, type, equalsMask, signMask, simdBaseType, simdSize);

                mask = gtNewSimdBinOpNode(GT_OR, type, mask, nanMask, simdBaseType, simdSize);
                mask = gtNewSimdBinOpNode(GT_OR, type, mask, cmpMask, simdBaseType, simdSize);

                retNode = gtNewSimdCndSelNode(type, mask, gtCloneExpr(op1Dup), op2Dup, simdBaseType, simdSize);
            }
            assert(retNode is not null);

            if (isScalar)
            {
                retNode = gtNewSimdToScalarNode(simdBaseType, retNode, simdBaseType, simdSize);
            }
            return retNode;
        }

        assert(!isScalar);

        if (isMagnitude)
        {
            var op1Dup = fgMakeMultiUse(ref op1);
            var op2Dup = fgMakeMultiUse(ref op2);

            var absOp1 = gtNewSimdAbsNode(type, op1, simdBaseType, simdSize);
            var absOp2 = gtNewSimdAbsNode(type, op2, simdBaseType, simdSize);

            var absOp1Dup = fgMakeMultiUse(ref absOp1);
            var absOp2Dup = fgMakeMultiUse(ref absOp2);

            var equalsMask = gtNewSimdCmpOpNode(GT_EQ, type, absOp1, absOp2, simdBaseType, simdSize);

            var signMask1 = null as GenTree;
            var signMask2 = null as GenTree;
            var signMask3 = null as GenTree;
            var cmpMask = null as GenTree;

            if (isMax)
            {
                signMask1 = gtNewSimdIsNegativeNode(type, op2Dup, simdBaseType, simdSize);
                signMask2 = gtNewSimdIsPositiveNode(type, absOp2Dup, simdBaseType, simdSize);
                signMask3 = gtNewSimdIsNegativeNode(type, absOp1Dup, simdBaseType, simdSize);
                cmpMask = gtNewSimdCmpOpNode(GT_GT, type, gtCloneExpr(absOp1Dup), gtCloneExpr(absOp2Dup), simdBaseType, simdSize);
            }
            else
            {
                signMask1 = gtNewSimdIsNegativeNode(type, op1Dup, simdBaseType, simdSize);
                signMask2 = gtNewSimdIsPositiveNode(type, absOp1Dup, simdBaseType, simdSize);
                signMask3 = gtNewSimdIsNegativeNode(type, absOp2Dup, simdBaseType, simdSize);
                cmpMask = gtNewSimdCmpOpNode(GT_LT, type, gtCloneExpr(absOp1Dup), gtCloneExpr(absOp2Dup), simdBaseType, simdSize);
            }

            var mask1 = gtNewSimdBinOpNode(GT_AND, type, equalsMask, signMask1, simdBaseType, simdSize);
            var mask2 = gtNewSimdBinOpNode(GT_AND, type, cmpMask, signMask2, simdBaseType, simdSize);
            var mask3 = gtNewSimdBinOpNode(GT_OR, type, mask1, mask2, simdBaseType, simdSize);

            mask3 = gtNewSimdBinOpNode(GT_OR, type, mask3, signMask3, simdBaseType, simdSize);
            return gtNewSimdCndSelNode(type, mask3, gtCloneExpr(op1Dup), gtCloneExpr(op2Dup), simdBaseType, simdSize);
        }

        return gtNewSimdMinMaxNativeNode(type, op1, op2, simdBaseType, simdSize, isMax);
    }

    /// <summary>Creates a new HWIntrinsic node that performs a min or max computation without consideration for IEEE 754 semantics</summary>
    /// <param name="type">The type of the node to generate</param>
    /// <param name="op1">The first operand</param>
    /// <param name="op2">The second operand</param>
    /// <param name="simdBaseType">the base type of the node</param>
    /// <param name="simdSize">the simd size of the node</param>
    /// <param name="isMax">true to compute the maximum; otherwise, false for the minimum</param>
    /// <returns>The node representing the minimum or maximum operation</returns>
    /// <remarks>This follows the platform specific behavior for comparisons and will do whatever is most efficient. This means that the exact result returned if either input is NaN or -0 can differ based on the underlying hardware.</remarks>
    public GenTree gtNewSimdMinMaxNativeNode(var_types type, GenTree op1, GenTree op2, var_types simdBaseType, byte simdSize, bool isMax)
    {
        assert(op1 is not null);
        assert(op1.Type == type);

        assert(op2 is not null);
        assert(op2.Type == type);

        assert(varTypeIsArithmetic(simdBaseType));

        var isScalar = false;

        if (simdSize is 0)
        {
            isScalar = true;
            assert(varTypeIsFloating(type));
            assert(simdBaseType == type);
        }
        else
        {
            assert(varTypeIsSimd(type));
            assert(GetSimdTypeForSize(simdSize) == type);
        }

        var intrinsic = NI_Illegal;

#if TARGET_XARCH
        if (simdSize is 32)
        {
            if (varTypeIsFloating(simdBaseType))
            {
                intrinsic = isMax ? NI_AVX_Max : NI_AVX_Min;
            }
            else
            {
                assert(compIsaSupportedDebugOnly(InstructionSet_AVX2));

                if (!varTypeIsLong(simdBaseType))
                {
                    intrinsic = isMax ? NI_AVX2_Max : NI_AVX2_Min;
                }
                else if (compOpportunisticallyDependsOn(InstructionSet_AVX512))
                {
                    intrinsic = isMax ? NI_AVX512_Max : NI_AVX512_Min;
                }
            }
        }
        else if (simdSize is 64)
        {
            intrinsic = isMax ? NI_AVX512_Max : NI_AVX512_Min;
        }
        else if (!varTypeIsLong(simdBaseType))
        {
            if (isScalar)
            {
                simdSize = 16;
                type = TYP_SIMD16;

                op1 = gtNewSimdCreateScalarUnsafeNode(type, op1, simdBaseType, simdSize);
                op2 = gtNewSimdCreateScalarUnsafeNode(type, op2, simdBaseType, simdSize);

                intrinsic = isMax ? NI_X86Base_MaxScalar : NI_X86Base_MinScalar;
            }
            else
            {
                intrinsic = isMax ? NI_X86Base_Max : NI_X86Base_Min;
            }
        }
        else if (compOpportunisticallyDependsOn(InstructionSet_AVX512))
        {
            intrinsic = isMax ? NI_AVX512_Max : NI_AVX512_Min;
        }
#elif TARGET_ARM64
        if (!varTypeIsLong(simdBaseType))
        {
            if (isScalar)
            {
                simdSize = 8;
                type     = TYP_SIMD8;

                op1 = gtNewSimdCreateScalarUnsafeNode(type, op1, simdBaseType, simdSize);
                op2 = gtNewSimdCreateScalarUnsafeNode(type, op2, simdBaseType, simdSize);

                intrinsic = isMax ? NI_AdvSimd_Arm64_MaxScalar : NI_AdvSimd_Arm64_MinScalar;
            }
            else if (simdBaseType == TYP_DOUBLE)
            {
                if (simdSize is 8)
                {
                    intrinsic = isMax ? NI_AdvSimd_Arm64_MaxScalar : NI_AdvSimd_Arm64_MinScalar;
                }
                else
                {
                    intrinsic = isMax ? NI_AdvSimd_Arm64_Max : NI_AdvSimd_Arm64_Min;
                }
            }
            else
            {
                intrinsic = isMax ? NI_AdvSimd_Max : NI_AdvSimd_Min;
            }
        }
#else
#error Unsupported platform
#endif

        if (intrinsic != NI_Illegal)
        {
            var retNode = gtNewSimdHWIntrinsicNode(type, intrinsic, simdBaseType, simdSize, op1, op2) as GenTree;

            if (isScalar)
            {
                retNode = gtNewSimdToScalarNode(simdBaseType, retNode, simdBaseType, simdSize);
            }
            return retNode;
        }

        assert(!isScalar);

        var op1Dup = fgMakeMultiUse(ref op1);
        var op2Dup = fgMakeMultiUse(ref op2);

        // op1 = op1 < op2
        // -or-
        // op1 = op1 > op2
        op1 = gtNewSimdCmpOpNode(isMax ? GT_GT : GT_LT, type, op1, op2, simdBaseType, simdSize);

        // result = ConditionalSelect(op1, op1Dup, op2Dup)
        return gtNewSimdCndSelNode(type, op1, op1Dup, op2Dup, simdBaseType, simdSize);
    }

    public GenTree gtNewSimdNarrowNode(var_types type, GenTree op1, GenTree op2, var_types simdBaseType, byte simdSize)
    {
        assert(varTypeIsSimd(type));
        assert(GetSimdTypeForSize(simdSize) == type);

        assert(op1 is not null);
        assert(op1.Type == type);

        assert(op2 is not null);
        assert(op2.Type == type);

        assert(varTypeIsArithmetic(simdBaseType) && !varTypeIsLong(simdBaseType));

        GenTree tmp1;
        GenTree tmp2;

#if TARGET_XARCH
        GenTree tmp3;

        if (compOpportunisticallyDependsOn(InstructionSet_AVX512))
        {
            // This is the same in principle to the other comments below, however due to
            // code formatting, its too long to reasonably display here.

            assert((simdSize is 16) || (simdSize is 32) || (simdSize is 64));
            var tmpSimdType = (simdSize is 64) ? TYP_SIMD32 : TYP_SIMD16;

            var intrinsicId = NI_Illegal;
            var opBaseType = TYP_UNDEF;

            switch (simdBaseType)
            {
                case TYP_BYTE:
                {
                    if (simdSize is 64)
                    {
                        intrinsicId = NI_AVX512_ConvertToVector256SByte;
                    }
                    else
                    {
                        intrinsicId = NI_AVX512_ConvertToVector128SByte;
                    }

                    opBaseType = TYP_SHORT;
                    break;
                }

                case TYP_UBYTE:
                {
                    if (simdSize is 64)
                    {
                        intrinsicId = NI_AVX512_ConvertToVector256Byte;
                    }
                    else
                    {
                        intrinsicId = NI_AVX512_ConvertToVector128Byte;
                    }

                    opBaseType = TYP_USHORT;
                    break;
                }

                case TYP_SHORT:
                {
                    if (simdSize is 64)
                    {
                        intrinsicId = NI_AVX512_ConvertToVector256Int16;
                    }
                    else
                    {
                        intrinsicId = NI_AVX512_ConvertToVector128Int16;
                    }

                    opBaseType = TYP_INT;
                    break;
                }

                case TYP_USHORT:
                {
                    if (simdSize is 64)
                    {
                        intrinsicId = NI_AVX512_ConvertToVector256UInt16;
                    }
                    else
                    {
                        intrinsicId = NI_AVX512_ConvertToVector128UInt16;
                    }

                    opBaseType = TYP_UINT;
                    break;
                }

                case TYP_INT:
                {
                    if (simdSize is 64)
                    {
                        intrinsicId = NI_AVX512_ConvertToVector256Int32;
                    }
                    else
                    {
                        intrinsicId = NI_AVX512_ConvertToVector128Int32;
                    }

                    opBaseType = TYP_LONG;
                    break;
                }

                case TYP_UINT:
                {
                    if (simdSize is 64)
                    {
                        intrinsicId = NI_AVX512_ConvertToVector256UInt32;
                    }
                    else
                    {
                        intrinsicId = NI_AVX512_ConvertToVector128UInt32;
                    }

                    opBaseType = TYP_ULONG;
                    break;
                }

                case TYP_FLOAT:
                {
                    if (simdSize is 64)
                    {
                        intrinsicId = NI_AVX512_ConvertToVector256Single;
                    }
                    else if (simdSize is 32)
                    {
                        intrinsicId = NI_AVX_ConvertToVector128Single;
                    }
                    else
                    {
                        intrinsicId = NI_X86Base_ConvertToVector128Single;
                    }

                    opBaseType = TYP_DOUBLE;
                    break;
                }

                default:
                {
                    unreached();
                    break;
                }
            }

            tmp1 = gtNewSimdHWIntrinsicNode(tmpSimdType, intrinsicId, opBaseType, simdSize, op1);
            tmp2 = gtNewSimdHWIntrinsicNode(tmpSimdType, intrinsicId, opBaseType, simdSize, op2);

            if (simdSize is 16)
            {
                return gtNewSimdHWIntrinsicNode(type, NI_X86Base_MoveLowToHigh, TYP_FLOAT, simdSize, tmp1, tmp2);
            }

            intrinsicId = (simdSize is 64) ? NI_Vector256_ToVector512Unsafe : NI_Vector128_ToVector256Unsafe;

            tmp1 = gtNewSimdHWIntrinsicNode(type, intrinsicId, simdBaseType, (byte)(simdSize / 2), tmp1);
            return gtNewSimdWithUpperNode(type, tmp1, tmp2, simdBaseType, simdSize);
        }
        else if (simdSize is 32)
        {
            switch (simdBaseType)
            {
                case TYP_BYTE:
                case TYP_UBYTE:
                {
                    assert(compIsaSupportedDebugOnly(InstructionSet_AVX2));

                    // This is the same in principle to the other comments below, however due to
                    // code formatting, its too long to reasonably display here.
                    var vecCon1 = gtNewVconNode(type);
                    vecCon1.EvaluateBroadcastInPlace(TYP_USHORT, 0x00FF);

                    var vecCon2 = gtCloneCnsVec(vecCon1);

                    tmp1 = gtNewSimdBinOpNode(GT_AND, type, op1, vecCon1, simdBaseType, simdSize);
                    tmp2 = gtNewSimdBinOpNode(GT_AND, type, op2, vecCon2, simdBaseType, simdSize);
                    tmp3 = gtNewSimdHWIntrinsicNode(type, NI_AVX2_PackUnsignedSaturate, TYP_UBYTE, simdSize, tmp1, tmp2);

                    var permuteBaseType = (simdBaseType == TYP_BYTE) ? TYP_LONG : TYP_ULONG;
                    return gtNewSimdHWIntrinsicNode(type, NI_AVX2_Permute4x64, permuteBaseType, simdSize, tmp3, gtNewIconNode(TYP_INT, SHUFFLE_WYZX));
                }

                case TYP_SHORT:
                case TYP_USHORT:
                {
                    assert(compIsaSupportedDebugOnly(InstructionSet_AVX2));

                    // op1 = Elements 0L, 0U, 1L, 1U, 2L, 2U, 3L, 3U | 4L, 4U, 5L, 5U, 6L, 6U, 7L, 7U
                    // op2 = Elements 8L, 8U, 9L, 9U, AL, AU, BL, BU | CL, CU, DL, DU, EL, EU, FL, FU
                    //
                    // tmp2 = Elements 0L, --, 1L, --, 2L, --, 3L, -- | 4L, --, 5L, --, 6L, --, 7L, --
                    // tmp3 = Elements 8L, --, 9L, --, AL, --, BL, -- | CL, --, DL, --, EL, --, FL, --
                    // tmp4 = Elements 0L, 1L, 2L, 3L, 8L, 9L, AL, BL | 4L, 5L, 6L, 7L, CL, DL, EL, FL
                    // return Elements 0L, 1L, 2L, 3L, 4L, 5L, 6L, 7L | 8L, 9L, AL, BL, CL, DL, EL, FL
                    //
                    // var vcns = Vector256.Create(0x0000FFFF).AsInt16();
                    // var tmp1 = Avx2.And(op1.AsInt16(), vcns);
                    // var tmp2 = Avx2.And(op2.AsInt16(), vcns);
                    // var tmp3 = Avx2.PackuintSaturate(tmp1, tmp2);
                    // return Avx2.Permute4x64(tmp3.AsUInt64(), SHUFFLE_WYZX).As<T>();

                    var vecCon1 = gtNewVconNode(type);
                    vecCon1.EvaluateBroadcastInPlace(TYP_UINT, 0x0000FFFF);

                    var vecCon2 = gtCloneCnsVec(vecCon1);

                    tmp1 = gtNewSimdBinOpNode(GT_AND, type, op1, vecCon1, simdBaseType, simdSize);
                    tmp2 = gtNewSimdBinOpNode(GT_AND, type, op2, vecCon2, simdBaseType, simdSize);
                    tmp3 = gtNewSimdHWIntrinsicNode(type, NI_AVX2_PackUnsignedSaturate, TYP_USHORT, simdSize, tmp1, tmp2);

                    var permuteBaseType = (simdBaseType == TYP_SHORT) ? TYP_LONG : TYP_ULONG;
                    return gtNewSimdHWIntrinsicNode(type, NI_AVX2_Permute4x64, permuteBaseType, simdSize, tmp3, gtNewIconNode(TYP_INT, SHUFFLE_WYZX));
                }

                case TYP_INT:
                case TYP_UINT:

                case TYP_FLOAT:
                {
                    // op1 = Elements 0, 1 | 2, 3
                    // op2 = Elements 4, 5 | 6, 7
                    //
                    // tmp1 = Elements 0, 1, 2, 3 | -, -, -, -
                    // tmp1 = Elements 4, 5, 6, 7
                    // return Elements 0, 1, 2, 3 | 4, 5, 6, 7
                    //
                    // var tmp1 = Avx.ConvertToVector128Single(op1).ToVector256Unsafe();
                    // var tmp2 = Avx.ConvertToVector128Single(op2);
                    // return tmp1.WithUpper(tmp2);

                    var opBaseType = TYP_DOUBLE;

                    tmp1 = gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_AVX_ConvertToVector128Single, opBaseType, simdSize, op1);
                    tmp2 = gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_AVX_ConvertToVector128Single, opBaseType, simdSize, op2);

                    tmp1 = gtNewSimdHWIntrinsicNode(type, NI_Vector128_ToVector256Unsafe, simdBaseType, 16, tmp1);
                    return gtNewSimdWithUpperNode(type, tmp1, tmp2, simdBaseType, simdSize);
                }

                default:
                {
                    unreached();
                    return null;
                }
            }
        }
        else
        {
            assert(simdSize is 16);

            switch (simdBaseType)
            {
                case TYP_BYTE:
                case TYP_UBYTE:
                {
                    // op1 = Elements 0, 1, 2, 3, 4, 5, 6, 7; 0L, 0U, 1L, 1U, 2L, 2U, 3L, 3U, 4L, 4U, 5L, 5U, 6L, 6U, 7L, 7U
                    // op2 = Elements 8, 9, A, B, C, D, E, F; 8L, 8U, 9L, 9U, AL, AU, BL, BU, CL, CU, DL, DU, EL, EU, FL, FU
                    //
                    // tmp2 = Elements 0L, --, 1L, --, 2L, --, 3L, --, 4L, --, 5L, --, 6L, --, 7L, --
                    // tmp3 = Elements 8L, --, 9L, --, AL, --, BL, --, CL, --, DL, --, EL, --, FL, --
                    // return Elements 0L, 1L, 2L, 3L, 4L, 5L, 6L, 7L, 8L, 9L, AL, BL, CL, DL, EL, FL
                    //
                    // var vcns = Vector128.Create((ushort)(0x00FF)).AsSByte();
                    // var tmp1 = Sse2.And(op1.AsSByte(), vcns);
                    // var tmp2 = Sse2.And(op2.AsSByte(), vcns);
                    // return Sse2.PackuintSaturate(tmp1, tmp2).As<T>();

                    var vecCon1 = gtNewVconNode(type);
                    vecCon1.EvaluateBroadcastInPlace(TYP_USHORT, 0x00FF);

                    var vecCon2 = gtCloneCnsVec(vecCon1);

                    tmp1 = gtNewSimdBinOpNode(GT_AND, type, op1, vecCon1, simdBaseType, simdSize);
                    tmp2 = gtNewSimdBinOpNode(GT_AND, type, op2, vecCon2, simdBaseType, simdSize);

                    return gtNewSimdHWIntrinsicNode(type, NI_X86Base_PackUnsignedSaturate, TYP_UBYTE, simdSize, tmp1, tmp2);
                }

                case TYP_SHORT:
                case TYP_USHORT:
                {
                    // op1 = Elements 0, 1, 2, 3;      0L, 0U, 1L, 1U, 2L, 2U, 3L, 3U
                    // op2 = Elements 4, 5, 6, 7;      4L, 4U, 5L, 5U, 6L, 6U, 7L, 7U
                    //
                    // tmp2 = Elements 0L, --, 1L, --, 2L, --, 3L, --
                    // tmp3 = Elements 4L, --, 5L, --, 6L, --, 7L, --
                    // return Elements 0L, 1L, 2L, 3L, 4L, 5L, 6L, 7L
                    //
                    // var vcns = Vector128.Create(0x0000FFFF).AsInt16();
                    // var tmp1 = Sse2.And(op1.AsInt16(), vcns);
                    // var tmp2 = Sse2.And(op2.AsInt16(), vcns);
                    // return Sse2.PackuintSaturate(tmp1, tmp2).As<T>();

                    var vecCon1 = gtNewVconNode(type);
                    vecCon1.EvaluateBroadcastInPlace(TYP_UINT, 0x0000FFFF);

                    var vecCon2 = gtCloneCnsVec(vecCon1);

                    tmp1 = gtNewSimdBinOpNode(GT_AND, type, op1, vecCon1, simdBaseType, simdSize);
                    tmp2 = gtNewSimdBinOpNode(GT_AND, type, op2, vecCon2, simdBaseType, simdSize);

                    return gtNewSimdHWIntrinsicNode(type, NI_X86Base_PackUnsignedSaturate, TYP_USHORT, simdSize, tmp1, tmp2);
                }

                case TYP_INT:
                case TYP_UINT:
                {
                    // op1 = Elements 0, 1;      0L, 0U, 1L, 1U
                    // op2 = Elements 2, 3;      2L, 2U, 3L, 3U
                    //
                    // tmp1 = Elements 0L, 2L, 0U, 2U
                    // tmp2 = Elements 1L, 3L, 1U, 3U
                    // return Elements 0L, 1L, 2L, 3L
                    //
                    // var tmp1 = Sse2.UnpackLow(op1.AsUInt32(), op2.AsUInt32());
                    // var tmp2 = Sse2.UnpackHigh(op1.AsUInt32(), op2.AsUInt32());
                    // return Sse2.UnpackLow(tmp1, tmp2).As<T>();

                    var op1Dup = fgMakeMultiUse(ref op1);
                    var op2Dup = fgMakeMultiUse(ref op2);

                    tmp1 = gtNewSimdHWIntrinsicNode(type, NI_X86Base_UnpackLow, simdBaseType, simdSize, op1, op2);
                    tmp2 = gtNewSimdHWIntrinsicNode(type, NI_X86Base_UnpackHigh, simdBaseType, simdSize, op1Dup, op2Dup);

                    return gtNewSimdHWIntrinsicNode(type, NI_X86Base_UnpackLow, simdBaseType, simdSize, tmp1, tmp2);
                }

                case TYP_FLOAT:
                {
                    // op1 = Elements 0, 1
                    // op2 = Elements 2, 3
                    //
                    // tmp1 = Elements 0, 1, -, -
                    // tmp1 = Elements 2, 3, -, -
                    // return Elements 0, 1, 2, 3
                    //
                    // var tmp1 = Sse2.ConvertToVector128Single(op1);
                    // var tmp2 = Sse2.ConvertToVector128Single(op2);
                    // return Sse.MoveLowToHigh(tmp1, tmp2);

                    var opBaseType = TYP_DOUBLE;

                    tmp1 = gtNewSimdHWIntrinsicNode(type, NI_X86Base_ConvertToVector128Single, opBaseType, simdSize, op1);
                    tmp2 = gtNewSimdHWIntrinsicNode(type, NI_X86Base_ConvertToVector128Single, opBaseType, simdSize, op2);

                    return gtNewSimdHWIntrinsicNode(type, NI_X86Base_MoveLowToHigh, simdBaseType, simdSize, tmp1, tmp2);
                }

                default:
                {
                    unreached();
                    return null;
                }
            }
        }
#elif TARGET_ARM64
        if (simdSize is 16)
        {
            if (varTypeIsFloating(simdBaseType))
            {
                // var tmp1 = AdvSimd.Arm64.ConvertToSingleLower(op1);
                // return AdvSimd.Arm64.ConvertToSingleUpper(tmp1, op2);

                tmp1 = gtNewSimdHWIntrinsicNode(TYP_SIMD8, NI_AdvSimd_Arm64_ConvertToSingleLower, simdBaseType, 8, op1);
                return gtNewSimdHWIntrinsicNode(type, NI_AdvSimd_Arm64_ConvertToSingleUpper, simdBaseType, simdSize, tmp1, op2);
            }
            else
            {
                // var tmp1 = AdvSimd.ExtractNarrowingLower(op1);
                // return AdvSimd.ExtractNarrowingUpper(tmp1, op2);

                tmp1 = gtNewSimdHWIntrinsicNode(TYP_SIMD8, NI_AdvSimd_ExtractNarrowingLower, simdBaseType, 8, op1);
                return gtNewSimdHWIntrinsicNode(type, NI_AdvSimd_ExtractNarrowingUpper, simdBaseType, simdSize, tmp1, op2);
            }
        }
        else if (varTypeIsFloating(simdBaseType))
        {
            // var tmp1 = op1.ToVector128Unsafe();
            // var tmp2 = AdvSimd.InsertScalar(tmp1, op2);
            // return AdvSimd.Arm64.ConvertToSingleLower(tmp2);

            var tmp2BaseType = TYP_DOUBLE;

            tmp1 = gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_Vector64_ToVector128Unsafe, simdBaseType, simdSize, op1);
            tmp2 = gtNewSimdWithUpperNode(TYP_SIMD16, tmp1, op2, tmp2BaseType, 16);

            return gtNewSimdHWIntrinsicNode(type, NI_AdvSimd_Arm64_ConvertToSingleLower, simdBaseType, simdSize, tmp2);
        }
        else
        {
            // var tmp1 = op1.ToVector128Unsafe();
            // var tmp2 = tmp1.WithUpper(op2);
            // return AdvSimd.ExtractNarrowingLower(tmp2);

            tmp1 = gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_Vector64_ToVector128Unsafe, simdBaseType, simdSize, op1);
            tmp2 = gtNewSimdWithUpperNode(TYP_SIMD16, tmp1, op2, simdBaseType, 16);

            return gtNewSimdHWIntrinsicNode(type, NI_AdvSimd_ExtractNarrowingLower, simdBaseType, simdSize, tmp2);
        }
#else
#error Unsupported platform
#endif
    }

    /// <summary>Creates a new simd ToScalar node.</summary>
    /// <param name="type">The return type of SIMD node being created.</param>
    /// <param name="op1">The SIMD operand.</param>
    /// <param name="simdBaseType">The base type of SIMD type of the intrinsic.</param>
    /// <param name="simdSize">The size of the SIMD type of the intrinsic.</param>
    /// <returns>The created node that has the ToScalar implementation.</returns>
    public GenTree gtNewSimdToScalarNode(var_types type, GenTree op1, var_types simdBaseType, byte simdSize)
    {
        assert(varTypeIsArithmetic(type));

        assert(op1 is not null);
        assert(varTypeIsSimd(op1.Type));

        assert(varTypeIsArithmetic(simdBaseType));

        var intrinsic = NI_Illegal;

#if TARGET_XARCH
        if (simdSize is 64)
        {
            intrinsic = NI_Vector512_ToScalar;
        }
        else if (simdSize is 32)
        {
            intrinsic = NI_Vector256_ToScalar;
        }
        else
        {
            intrinsic = NI_Vector128_ToScalar;
        }
#elif TARGET_ARM64
        if (simdSize is 8)
        {
            intrinsic = NI_Vector64_ToScalar;
        }
        else
        {
            intrinsic = NI_Vector128_ToScalar;
        }
#else
#error Unsupported platform
#endif

        return gtNewSimdHWIntrinsicNode(type, intrinsic, simdBaseType, simdSize, op1);
    }

    /// <summary>Creates a new simd Truncate node</summary>
    /// <param name="type">The type of the node</param>
    /// <param name="op1">The node to truncate</param>
    /// <param name="simdBaseType"></param>
    /// <param name="simdSize">the simd size of the node</param>
    /// <returns>The truncate node</returns>
    public GenTree gtNewSimdTruncNode(var_types type, GenTree op1, var_types simdBaseType, byte simdSize)
    {
        assert(varTypeIsSimd(type));
        assert(GetSimdTypeForSize(simdSize) == type);

        assert(op1 is not null);
        assert(op1.Type == type);

        assert(varTypeIsFloating(simdBaseType));

        var intrinsic = NI_Illegal;

#if TARGET_XARCH
        if (simdSize is 32)
        {
            intrinsic = NI_AVX_RoundToZero;
        }
        else if (simdSize is 64)
        {
            var op2 = gtNewIconNode(TYP_INT, (int)(FloatRoundingMode.ToZero));
            return gtNewSimdHWIntrinsicNode(type, NI_AVX512_RoundScale, simdBaseType, simdSize, op1, op2);
        }
        else
        {
            intrinsic = NI_X86Base_RoundToZero;
        }
#elif TARGET_ARM64
        if (simdBaseType == TYP_DOUBLE)
        {
            intrinsic = (simdSize is 8) ? NI_AdvSimd_RoundToZeroScalar : NI_AdvSimd_Arm64_RoundToZero;
        }
        else
        {
            intrinsic = NI_AdvSimd_RoundToZero;
        }
#else
#error Unsupported platform
#endif

        return gtNewSimdHWIntrinsicNode(type, intrinsic, simdBaseType, simdSize, op1);
    }

    public GenTree gtNewSimdUnOpNode(genTreeOps op, var_types type, GenTree op1, var_types simdBaseType, byte simdSize)
    {
        assert(varTypeIsSimd(type));
        assert(GetSimdTypeForSize(simdSize) == type);

        assert(op1 is not null);
        assert(op1.Type == type);

        assert(varTypeIsArithmetic(simdBaseType));

#if TARGET_ARM64
        if (op is GT_NEG)
        {
            simdBaseType = varTypeToSigned(simdBaseType);
        }
#endif

        var intrinsic = GetHWIntrinsicIdForUnOp(op, op1, simdBaseType, simdSize, isScalar: false);

        if (intrinsic != NI_Illegal)
        {
            return gtNewSimdHWIntrinsicNode(type, intrinsic, simdBaseType, simdSize, op1);
        }

        switch (op)
        {
#if TARGET_XARCH
            case GT_NEG:
            {
                if (varTypeIsFloating(simdBaseType))
                {
                    // op1 ^ -0.0
                    var negZero = gtNewVconNode(type);
                    negZero.EvaluateBroadcastInPlace(simdBaseType, -0.0);
                    return gtNewSimdBinOpNode(GT_XOR, type, op1, negZero, simdBaseType, simdSize);
                }
                else
                {
                    // Zero - op1
                    var zero = gtNewZeroConNode(type);
                    return gtNewSimdBinOpNode(GT_SUB, type, zero, op1, simdBaseType, simdSize);
                }
            }

            case GT_NOT:
            {
                // op1 ^ AllBitsSet
                var allBitsSet = gtNewAllBitsSetConNode(type);
                return gtNewSimdBinOpNode(GT_XOR, type, op1, allBitsSet, simdBaseType, simdSize);
            }
#endif

            default:
            {
                unreached();
                return null;
            }
        }
    }

    public GenTree gtNewSimdWidenLowerNode(var_types type, GenTree op1, var_types simdBaseType, byte simdSize)
    {
        assert(varTypeIsSimd(type));
        assert(GetSimdTypeForSize(simdSize) == type);

        assert(op1 is not null);
        assert(op1.Type == type);

        assert(varTypeIsArithmetic(simdBaseType) && !varTypeIsLong(simdBaseType));

        var intrinsic = NI_Illegal;

#if TARGET_XARCH
        if (simdSize is 64)
        {
            var tmp1 = gtNewSimdGetLowerNode(TYP_SIMD32, op1, simdBaseType, simdSize);

            switch (simdBaseType)
            {
                case TYP_BYTE:
                {
                    intrinsic = NI_AVX512_ConvertToVector512Int16;
                    break;
                }

                case TYP_UBYTE:
                {
                    intrinsic = NI_AVX512_ConvertToVector512UInt16;
                    break;
                }

                case TYP_SHORT:
                {
                    intrinsic = NI_AVX512_ConvertToVector512Int32;
                    break;
                }

                case TYP_USHORT:
                {
                    intrinsic = NI_AVX512_ConvertToVector512UInt32;
                    break;
                }

                case TYP_INT:
                {
                    intrinsic = NI_AVX512_ConvertToVector512Int64;
                    break;
                }

                case TYP_UINT:
                {
                    intrinsic = NI_AVX512_ConvertToVector512UInt64;
                    break;
                }

                case TYP_FLOAT:
                {
                    intrinsic = NI_AVX512_ConvertToVector512Double;
                    break;
                }

                default:
                {
                    unreached();
                    break;
                }
            }

            assert(intrinsic != NI_Illegal);
            return gtNewSimdHWIntrinsicNode(type, intrinsic, simdBaseType, simdSize, tmp1);
        }
        else if (simdSize is 32)
        {
            assert(!varTypeIsIntegral(simdBaseType) || compIsaSupportedDebugOnly(InstructionSet_AVX2));

            var tmp1 = gtNewSimdGetLowerNode(TYP_SIMD16, op1, simdBaseType, simdSize);

            switch (simdBaseType)
            {
                case TYP_BYTE:
                case TYP_UBYTE:
                {
                    intrinsic = NI_AVX2_ConvertToVector256Int16;
                    break;
                }

                case TYP_SHORT:
                case TYP_USHORT:
                {
                    intrinsic = NI_AVX2_ConvertToVector256Int32;
                    break;
                }

                case TYP_INT:
                case TYP_UINT:
                {
                    intrinsic = NI_AVX2_ConvertToVector256Int64;
                    break;
                }

                case TYP_FLOAT:
                {
                    intrinsic = NI_AVX_ConvertToVector256Double;
                    break;
                }

                default:
                {
                    unreached();
                    break;
                }
            }

            assert(intrinsic != NI_Illegal);
            return gtNewSimdHWIntrinsicNode(type, intrinsic, simdBaseType, simdSize, tmp1);
        }
        else
        {
            switch (simdBaseType)
            {
                case TYP_BYTE:
                case TYP_UBYTE:
                {
                    intrinsic = NI_X86Base_ConvertToVector128Int16;
                    break;
                }

                case TYP_SHORT:
                case TYP_USHORT:
                {
                    intrinsic = NI_X86Base_ConvertToVector128Int32;
                    break;
                }

                case TYP_INT:
                case TYP_UINT:
                {
                    intrinsic = NI_X86Base_ConvertToVector128Int64;
                    break;
                }

                case TYP_FLOAT:
                {
                    intrinsic = NI_X86Base_ConvertToVector128Double;
                    break;
                }

                default:
                {
                    unreached();
                    break;
                }
            }

            assert(intrinsic != NI_Illegal);
            return gtNewSimdHWIntrinsicNode(type, intrinsic, simdBaseType, simdSize, op1);
        }
#elif TARGET_ARM64
        var tmp1 = op1;

        if (simdSize is 16)
        {
            tmp1 = gtNewSimdGetLowerNode(TYP_SIMD8, op1, simdBaseType, simdSize);
        }
        else
        {
            assert(simdSize is 8);
        }

        if (varTypeIsFloating(simdBaseType))
        {
            assert(simdBaseType == TYP_FLOAT);
            intrinsic = NI_AdvSimd_Arm64_ConvertToDouble;
        }
        else if (varTypeIsSigned(simdBaseType))
        {
            intrinsic = NI_AdvSimd_SignExtendWideningLower;
        }
        else
        {
            intrinsic = NI_AdvSimd_ZeroExtendWideningLower;
        }

        assert(intrinsic != NI_Illegal);
        tmp1 = gtNewSimdHWIntrinsicNode(TYP_SIMD16, intrinsic, simdBaseType, 8, tmp1);

        if (simdSize is 8)
        {
            tmp1 = gtNewSimdGetLowerNode(TYP_SIMD8, tmp1, simdBaseType, 16);
        }
        return tmp1;
#else
#error Unsupported platform
#endif
    }

    public GenTree gtNewSimdWidenUpperNode(var_types type, GenTree op1, var_types simdBaseType, byte simdSize)
    {
        assert(varTypeIsSimd(type));
        assert(GetSimdTypeForSize(simdSize) == type);

        assert(op1 is not null);
        assert(op1.Type == type);

        assert(varTypeIsArithmetic(simdBaseType) && !varTypeIsLong(simdBaseType));

        var intrinsic = NI_Illegal;

#if TARGET_XARCH
        if (simdSize is 64)
        {
            var tmp1 = gtNewSimdGetUpperNode(TYP_SIMD32, op1, simdBaseType, simdSize);

            switch (simdBaseType)
            {
                case TYP_BYTE:
                {
                    intrinsic = NI_AVX512_ConvertToVector512Int16;
                    break;
                }

                case TYP_UBYTE:
                {
                    intrinsic = NI_AVX512_ConvertToVector512UInt16;
                    break;
                }

                case TYP_SHORT:
                {
                    intrinsic = NI_AVX512_ConvertToVector512Int32;
                    break;
                }

                case TYP_USHORT:
                {
                    intrinsic = NI_AVX512_ConvertToVector512UInt32;
                    break;
                }

                case TYP_INT:
                {
                    intrinsic = NI_AVX512_ConvertToVector512Int64;
                    break;
                }

                case TYP_UINT:
                {
                    intrinsic = NI_AVX512_ConvertToVector512UInt64;
                    break;
                }

                case TYP_FLOAT:
                {
                    intrinsic = NI_AVX512_ConvertToVector512Double;
                    break;
                }

                default:
                {
                    unreached();
                    break;
                }
            }

            assert(intrinsic != NI_Illegal);
            return gtNewSimdHWIntrinsicNode(type, intrinsic, simdBaseType, simdSize, tmp1);
        }
        else if (simdSize is 32)
        {
            assert(!varTypeIsIntegral(simdBaseType) || compIsaSupportedDebugOnly(InstructionSet_AVX2));

            var tmp1 = gtNewSimdGetUpperNode(TYP_SIMD16, op1, simdBaseType, simdSize);

            switch (simdBaseType)
            {
                case TYP_BYTE:
                case TYP_UBYTE:
                {
                    intrinsic = NI_AVX2_ConvertToVector256Int16;
                    break;
                }

                case TYP_SHORT:
                case TYP_USHORT:
                {
                    intrinsic = NI_AVX2_ConvertToVector256Int32;
                    break;
                }

                case TYP_INT:
                case TYP_UINT:
                {
                    intrinsic = NI_AVX2_ConvertToVector256Int64;
                    break;
                }

                case TYP_FLOAT:
                {
                    intrinsic = NI_AVX_ConvertToVector256Double;
                    break;
                }

                default:
                {
                    unreached();
                    break;
                }
            }

            assert(intrinsic != NI_Illegal);
            return gtNewSimdHWIntrinsicNode(type, intrinsic, simdBaseType, simdSize, tmp1);
        }
        else if (varTypeIsFloating(simdBaseType))
        {
            assert(simdBaseType == TYP_FLOAT);
            var op1Dup = fgMakeMultiUse(ref op1);

            var tmp1 = gtNewSimdHWIntrinsicNode(type, NI_X86Base_MoveHighToLow, simdBaseType, simdSize, op1, op1Dup);
            return gtNewSimdHWIntrinsicNode(type, NI_X86Base_ConvertToVector128Double, simdBaseType, simdSize, tmp1);
        }
        else
        {
            var tmp1 = gtNewSimdHWIntrinsicNode(type, NI_X86Base_ShiftRightLogical128BitLane, simdBaseType, simdSize, op1, gtNewIconNode(TYP_INT, 8));

            switch (simdBaseType)
            {
                case TYP_BYTE:
                case TYP_UBYTE:
                {
                    intrinsic = NI_X86Base_ConvertToVector128Int16;
                    break;
                }

                case TYP_SHORT:
                case TYP_USHORT:
                {
                    intrinsic = NI_X86Base_ConvertToVector128Int32;
                    break;
                }

                case TYP_INT:
                case TYP_UINT:
                {
                    intrinsic = NI_X86Base_ConvertToVector128Int64;
                    break;
                }

                default:
                {
                    unreached();
                    break;
                }
            }

            assert(intrinsic != NI_Illegal);
            return gtNewSimdHWIntrinsicNode(type, intrinsic, simdBaseType, simdSize, tmp1);
        }
#elif TARGET_ARM64
        if (simdSize is 16)
        {
            if (varTypeIsFloating(simdBaseType))
            {
                assert(simdBaseType == TYP_FLOAT);
                intrinsic = NI_AdvSimd_Arm64_ConvertToDoubleUpper;
            }
            else if (varTypeIsSigned(simdBaseType))
            {
                intrinsic = NI_AdvSimd_SignExtendWideningUpper;
            }
            else
            {
                intrinsic = NI_AdvSimd_ZeroExtendWideningUpper;
            }

            assert(intrinsic != NI_Illegal);
            return gtNewSimdHWIntrinsicNode(type, op1, intrinsic, simdBaseType, simdSize);
        }
        else
        {
            assert(simdSize is 8);
            var index = 8 / simdBaseType.Size;

            if (varTypeIsFloating(simdBaseType))
            {
                assert(simdBaseType is TYP_FLOAT);
                intrinsic = NI_AdvSimd_Arm64_ConvertToDouble;
            }
            else if (varTypeIsSigned(simdBaseType))
            {
                intrinsic = NI_AdvSimd_SignExtendWideningLower;
            }
            else
            {
                intrinsic = NI_AdvSimd_ZeroExtendWideningLower;
            }

            assert(intrinsic != NI_Illegal);

            tmp1 = gtNewSimdHWIntrinsicNode(TYP_SIMD16, intrinsic, simdBaseType, simdSize, op1);
            return gtNewSimdGetUpperNode(TYP_SIMD8, tmp1, simdBaseType, 16);
        }
#else
#error Unsupported platform
#endif
    }

    public GenTree gtNewSimdWithLowerNode(var_types type, GenTree op1, GenTree op2, var_types simdBaseType, byte simdSize)
    {
        assert(varTypeIsArithmetic(simdBaseType));

        var intrinsicId = NI_Illegal;

#if TARGET_XARCH
        if (simdSize is 32)
        {
            assert(type == TYP_SIMD32);
            intrinsicId = NI_Vector256_WithLower;
        }
        else
        {
            assert((type == TYP_SIMD64) && (simdSize is 64));
            intrinsicId = NI_Vector512_WithLower;
        }
#elif TARGET_ARM64
        assert((type == TYP_SIMD16) && (simdSize is 16));
        intrinsicId = NI_Vector128_WithLower;
#else
#error Unsupported platform
#endif

        return gtNewSimdHWIntrinsicNode(type, intrinsicId, simdBaseType, simdSize, op1, op2);
    }

    public GenTree gtNewSimdWithUpperNode(var_types type, GenTree op1, GenTree op2, var_types simdBaseType, byte simdSize)
    {
        assert(varTypeIsArithmetic(simdBaseType));

        var intrinsicId = NI_Illegal;

#if TARGET_XARCH
        if (simdSize is 32)
        {
            assert(type == TYP_SIMD32);
            intrinsicId = NI_Vector256_WithUpper;
        }
        else
        {
            assert((type == TYP_SIMD64) && (simdSize is 64));
            intrinsicId = NI_Vector512_WithUpper;
        }
#elif TARGET_ARM64
        assert((type == TYP_SIMD16) && (simdSize is 16));
        intrinsicId = NI_Vector128_WithUpper;
#else
#error Unsupported platform
#endif // !TARGET_XARCH && !TARGET_ARM64

        return gtNewSimdHWIntrinsicNode(type, intrinsicId, simdBaseType, simdSize, op1, op2);
    }
#endif

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

    /// <summary>create GenTreeIntCon node for the given string literal to store its length.</summary>
    /// <param name="node">string literal node.</param>
    /// <returns>GenTreeIntCon node with string's length as a value or null.</returns>
    public unsafe GenTreeIntCon? gtNewStringLiteralLength(GenTreeStrCon node)
    {
        if (node.IsStringEmptyField)
        {
            JITDUMP("Folded String.Empty.Length to 0\n");
            return gtNewIconNode(TYP_INT, 0);
        }

        var length = info.compCompHnd->getStringLiteral(node.ScpHnd, node.SconCpx, buffer: null, bufferSize: 0);

        if (length >= 0)
        {
            var iconNode = gtNewIconNode(TYP_INT, length);
            JITDUMP($"Folded 'CNS_STR.Length' to '{length}'\n");
            return iconNode;
        }
        return null;
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

    /// <summary>given the operands for a call to Enum.HasFlag, try and optimize the call to a simple and/compare tree.</summary>
    /// <param name="thisOp">first argument to the call</param>
    /// <param name="flagOp">second argument to the call</param>
    /// <returns>A new cmp/amd tree if successful. null on failure.</returns>
    /// <remarks>If successful, may allocate new temps and modify connected statements.</remarks>
    public unsafe GenTree? gtOptimizeEnumHasFlag(GenTree thisOp, GenTree flagOp)
    {
        JITDUMP("Considering optimizing call to Enum.HasFlag....\n");

        if ((thisOp.Oper is not GT_BOX) || (flagOp.Oper is not GT_BOX))
        {
            JITDUMP("bailing, need both inputs to be BOXes\n");
            return null;
        }

        var thisBox = thisOp.AsBox();
        var flagBox = flagOp.AsBox();

        // Operands must be boxes
        if (!thisBox.IsBoxedValue || !flagBox.IsBoxedValue)
        {
            JITDUMP("bailing, need both inputs to be BOXes\n");
            return null;
        }

        // Operands must have same type
        var thisHnd = gtGetClassHandle(thisBox, out var isExactThis, out var isNonNullThis);

        if (thisHnd is null)
        {
            JITDUMP("bailing, can't find type for 'this' operand\n");
            return null;
        }

        // A boxed thisOp should have exact type and non-null instance
        assert(isExactThis);
        assert(isNonNullThis);

        var flagHnd = gtGetClassHandle(flagBox, out var isExactFlag, out var isNonNullFlag);

        if (flagHnd is null)
        {
            JITDUMP("bailing, can't find type for 'flag' operand\n");
            return null;
        }

        // A boxed flagOp should have exact type and non-null instance
        assert(isExactFlag);
        assert(isNonNullFlag);

        if (flagHnd != thisHnd)
        {
            JITDUMP("bailing, operand types differ\n");
            return null;
        }

        // If we have a shared type instance we can't safely check type equality, so bail.
        if (eeIsSharedInst(thisHnd))
        {
            JITDUMP("bailing, have shared instance type\n");
            return null;
        }

        // Simulate removing the box for thisOP. We need to know that it can be safely removed before we can optimize.
        var thisVal = gtTryRemoveBoxUpstreamEffects(thisBox, BR_DONT_REMOVE);

        if (thisVal is null)
        {
            // Note we may fail here if the this operand comes from
            // a call. We should be able to retry this post-inlining.
            JITDUMP("bailing, can't undo box of 'this' operand\n");
            return null;
        }

        // Do likewise with flagOp.
        var flagVal = gtTryRemoveBoxUpstreamEffects(flagBox, BR_DONT_REMOVE);

        if (flagVal is null)
        {
            // Note we may fail here if the flag operand comes from
            // a call. We should be able to retry this post-inlining.
            JITDUMP("bailing, can't undo box of 'flag' operand\n");
            return null;
        }

        // Only proceed when both box sources have the same actual type.
        // (this rules out long/int mismatches)
        if (thisVal.Type.ActualType != flagVal.Type.ActualType)
        {
            JITDUMP("bailing, pre-boxed values have different types\n");
            return null;
        }

        // Yes, both boxes can be cleaned up. Optimize.
        JITDUMP("Optimizing call to Enum.HasFlag\n");

        // Undo the boxing of the Ops and prepare to operate directly
        // on the pre-boxed values.
        thisVal = gtTryRemoveBoxUpstreamEffects(thisBox, BR_REMOVE_BUT_NOT_NARROW);
        flagVal = gtTryRemoveBoxUpstreamEffects(flagBox, BR_REMOVE_BUT_NOT_NARROW);

        // Our trial removals above should guarantee successful removals here.
        assert(thisVal is not null);
        assert(flagVal is not null);
        assert(thisVal.Type.ActualType == flagVal.Type.ActualType);

        // Type to use for optimized check
        var type = thisVal.Type.ActualType;

        // The thisVal and flagVal trees come from earlier statements.
        //
        // Unless they are invariant values, we need to evaluate them both
        // to temps at those points to safely transmit the values here.
        //
        // Also we need to use the flag twice, so we need two trees for it.
        GenTree thisValOpt;
        GenTree flagValOpt;
        GenTree flagValOptCopy;

        if (thisVal.Oper.IsIntegralConst)
        {
            thisValOpt = gtCloneCnsInt(thisVal.AsIntCon());
            assert(thisValOpt is not null);
        }
        else
        {
            var thisTmp = lvaGrabTemp(shortLifetime: true, "Enum:HasFlag this temp");
            var thisStore = gtNewTempStore(thisTmp, thisVal);
            var thisStoreStmt = thisBox.CopyStmtWhenInlinedBoxValue;
            thisStoreStmt.RootNode = thisStore;
            thisValOpt = gtNewLclvNode(type, thisTmp);

            // If this is invoked during global morph we are adding code to a remote tree
            // Despite this being a store, we can't meaningfully add assertions
            thisStore.SetMorphed(this);
        }

        if (flagVal.Oper.IsIntegralConst)
        {
            flagValOpt = gtCloneCnsInt(flagVal.AsIntCon());
            assert(flagValOpt is not null);

            flagValOptCopy = gtCloneCnsInt(flagVal.AsIntCon());
            assert(flagValOptCopy is not null);
        }
        else
        {
            var flagTmp = lvaGrabTemp(shortLifetime: true, "Enum:HasFlag flag temp");
            var flagStore = gtNewTempStore(flagTmp, flagVal);
            var flagStoreStmt = flagBox.CopyStmtWhenInlinedBoxValue;
            flagStoreStmt.RootNode = flagStore;
            flagValOpt = gtNewLclvNode(type, flagTmp);
            flagValOptCopy = gtNewLclvNode(type, flagTmp);

            // If this is invoked during global morph we are adding code to a remote tree
            // Despite this being a store, we can't meaningfully add assertions
            flagStore.SetMorphed(this);
        }

        // Turn the call into (thisValTmp & flagTmp) == flagTmp.
        var andTree = gtNewBinaryNode(GT_AND, type, thisValOpt, flagValOpt);
        var cmpTree = gtNewBinaryNode(GT_EQ, TYP_INT, andTree, flagValOptCopy);

        JITDUMP("Optimized call to Enum.HasFlag\n");
        return cmpTree;
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

    public static void gtSetEvalOrderIndirectStore(Compiler comp, GenTreeIndir store, out bool allowReversal)
    {
        assert(store.Oper is GT_STORE_BLK or GT_STOREIND);

#if TARGET_WASM
        allowReversal = false;
#else
        var addr = store.Addr;
        var data = store.Data;

        if (addr.Oper.IsInvariant)
        {
            allowReversal = false;
            store.IsReverseOp = true;
            return;
        }

        if ((addr.Flags & GTF_ALL_EFFECT) is not 0)
        {
            allowReversal = true;
            return;
        }

        // In case op2 assigns to a local var that is used in op1, we have to evaluate op1 first.
        if (comp.gtMayHaveStoreInterference(data, addr))
        {
            // TODO-ASG-Cleanup: move this guard to "gtCanSwapOrder".
            allowReversal = false;
            return;
        }

        // If op2 is simple then evaluate op1 first
        if (data.Oper.IsLeaf)
        {
            allowReversal = true;
            return;
        }

        allowReversal = false;
        store.IsReverseOp = true;
#endif
    }

    /// <summary>A MinOpts specific version of gtSetEvalOrder. We don't need to set costs, but we're looking for opportunities to swap operands.</summary>
    /// <param name="tree">The tree for which we are setting the evaluation order.</param>
    /// <returns>the Sethi 'complexity' estimate for this tree (the higher the number, the higher is the tree's resources requirement)</returns>
    public int gtSetEvalOrderMinOpts(GenTree tree)
    {
        if (fgOrder is FGOrderLinear)
        {
            // We don't re-order operands in LIR anyway.
            return 0;
        }

        var oper = tree.Oper;

        if (oper.IsLeaf)
        {
            // Nothing to do for leaves, report as having Sethi 'complexity' of 0
            return 0;
        }

        var level = 1;

        if (oper.IsSimple)
        {
            var op1 = null as GenTree;
            var op2 = null as GenTree;

            if (oper.IsUnary)
            {
                op1 = tree.AsUnOp().Op1;
            }
            else
            {
                assert(oper.IsBinary);
                var op = tree.AsOp();

                op1 = op.Op1;
                op2 = op.Op2;
            }

            // Only GT_LEA may have a null op1 and a non-null op2
            if ((oper is GT_LEA) && (op1 is null))
            {
                (op1, op2) = (op2, op1);
            }

            // Check for a nilary operator
            if (op1 is null)
            {
                // E.g. void GT_RETURN, GT_RETFIT
                assert(op2 is null);
                return 0;
            }

            if (op2 is null)
            {
                gtSetEvalOrderMinOpts(op1);
                return 1;
            }

            level = gtSetEvalOrderMinOpts(op1);
            var levelOp2 = gtSetEvalOrderMinOpts(op2);

            var allowSwap = true;

            // TODO: Introduce a function to check whether we can swap the order of its operands or not.
            switch (oper)
            {
                case GT_COMMA:
                case GT_BOUNDS_CHECK:
                case GT_INTRINSIC:
                case GT_QMARK:
                case GT_COLON:
                {
                    // We're not going to swap operands in these
                    allowSwap = false;
                    break;
                }

                case GT_STORE_BLK:
                case GT_STOREIND:
                {
                    gtSetEvalOrderIndirectStore(this, tree.AsIndir(), out allowSwap);
                    break;
                }

                default:
                {
                    break;
                }
            }

            var shouldSwap = tree.IsReverseOp ? (level > levelOp2) : (level < levelOp2);

            if (shouldSwap && allowSwap)
            {
                // Can we swap the order by commuting the operands?
                var canSwap = tree.IsReverseOp ? gtCanSwapOrder(op2, op1) : gtCanSwapOrder(op1, op2);

                if (canSwap)
                {
                    var performSwap = oper.IsCommutative;

                    if (oper.IsCmpCompare)
                    {
                        var swapRelop = oper.SwapRelop;

                        if (swapRelop != oper)
                        {
                            tree.SetOper(swapRelop);
                        }
                        performSwap = true;
                    }

                    if (performSwap)
                    {
                        var op = tree.AsOp();

                        op.Op1 = op2;
                        op.Op2 = op1;

                        (op1, op2) = (op2, op1);
                    }
                    else
                    {

#if TARGET_WASM
                        // For WASM if we can't swap the operands or swap the operator, don't swap.
#else
                        // Mark the operand's evaluation order to be swapped.
                        tree.Flags ^= GTF_REVERSE_OPS;
#endif
                    }
                }
            }

            // Swap the level counts
            if (tree.IsReverseOp)
            {
                (level, levelOp2) = (levelOp2, level);
            }

            // Compute the sethi number for this binary operator
            if (level < 1)
            {
                level = levelOp2;
            }
            else if (level == levelOp2)
            {
                level++;
            }
        }
        else if (oper.IsCall)
        {
            var call = tree.AsCall();

            // We ignore late args - they don't bring any noticeable benefits according to asmdiffs/tpdiff
            foreach (var arg in call.Args.EarlyArgs)
            {
                gtSetEvalOrderMinOpts(arg.EarlyNode);
            }

            level = 3;
        }
#if FEATURE_HW_INTRINSICS
        else if (oper.IsHWIntrinsic)
        {
            return gtSetMultiOpOrder(tree.AsMultiOp());
        }
#endif

        // NOTE: we skip many operators here in order to maintain a good trade-off between CQ and TP.
        return level;
    }

#if FEATURE_SIMD || FEATURE_HW_INTRINSICS
    /// <summary>Calculate the costs for a MultiOp.</summary>
    /// <param name="multiOp">The MultiOp tree in question</param>
    /// <returns>The Sethi "complexity" for this tree (the idealized number of registers needed to evaluate it).</returns>
    public int gtSetMultiOpOrder(GenTreeMultiOp multiOp)
    {
        // Most HWI nodes are simple arithmetic operations.
        var costEx = (byte)(1);
        var costSz = (byte)(1);
        var level = 0;

        var optsEnabled = opts.OptimizationEnabled;
        var opCount = multiOp.Operands.Length;
        var addrOp = null as GenTree;

#if FEATURE_HW_INTRINSICS
        if ((multiOp.Oper is GT_HWINTRINSIC) && optsEnabled)
        {
            var hwTree = multiOp.AsHWIntrinsic();
            var intrinsicId = hwTree.HWIntrinsicId;
            var retType = hwTree.Type;
            var simdBaseType = hwTree.SimdBaseType;
            var simdSize = hwTree.SimdSize;

#if TARGET_XARCH
            if ((retType is TYP_SIMD64) || (simdSize is 64))
            {
                costSz = 6;
            }
            else
            {
                costSz = 4;
            }

            var isLoad = hwTree.IsMemoryLoad(out addrOp);

            if (isLoad || hwTree.IsMemoryStore(out addrOp))
            {
                assert(addrOp is not null);
                costEx = FLT_IND_COST_EX;

                if (simdSize is not 16)
                {
                    if (simdSize is 32)
                    {
                        costEx += 1;
                    }
                    else
                    {
                        costEx += 2;
                    }
                }

                if (!isLoad)
                {
                    costEx += 2;
                }

                switch (intrinsicId)
                {
                    case NI_X86Base_StoreAlignedNonTemporal:
                    case NI_X86Base_StoreNonTemporal:
                    case NI_X86Base_X64_StoreNonTemporal:
                    case NI_AVX_StoreAlignedNonTemporal:
                    case NI_AVX512_StoreAlignedNonTemporal:
                    {
                        costEx += 38;
                        break;
                    }

                    case NI_AVX_MaskStore:
                    case NI_AVX2_MaskStore:
                    {
                        costEx += 5;
                        break;
                    }

                    case NI_AVX2_GatherVector128:
                    case NI_AVX2_GatherVector256:
                    case NI_AVX2_GatherMaskVector128:
                    case NI_AVX2_GatherMaskVector256:
                    {
                        if (varTypeIsLong(simdBaseType))
                        {
                            costEx += (byte)((simdSize is 16) ? 13 : 14);
                        }
                        else
                        {
                            costEx += (byte)((simdSize is 16) ? 15 : 16);
                        }
                        break;
                    }

                    case NI_AVX2_MultiplyNoFlags:
                    case NI_AVX2_X64_MultiplyNoFlags:
                    {
                        costEx = 4 + IND_COST_EX;
                        break;
                    }

                    case NI_AVX512_CompressStoreMask:
                    case NI_AVX512_ExpandLoadMask:
                    case NI_AVX512_MaskLoadMask:
                    case NI_AVX512_MaskLoadAlignedMask:
                    case NI_AVX512_MaskStoreMask:
                    case NI_AVX512_MaskStoreAlignedMask:
                    {
                        costEx += 3;
                        break;
                    }

                    default:
                    {
                        // The default costing is correct
                        break;
                    }
                }

                // Can we form an addressing mode with this indirection?
                level = gtSetEvalOrder(addrOp);

                if (gtGetAddrNodeCost(addrOp, retType, false, out var addrCostEx, out var addrCostSz))
                {
                    costEx += addrCostEx;
                    costSz += addrCostSz;
                }
                else
                {
                    addrOp = null;
                }
            }
            else
            {
                if (varTypeUsesIntReg(simdBaseType))
                {
                    costEx = HWIntrinsicInfo.lookupIntCost(intrinsicId);
                }
                else
                {
                    costEx = HWIntrinsicInfo.lookupFltCost(intrinsicId);
                }

                if (costEx == byte.MaxValue)
                {
                    switch (intrinsicId)
                    {
                        case NI_Vector128_ConditionalSelect:
                        case NI_Vector256_ConditionalSelect:
                        case NI_Vector512_ConditionalSelect:
                        {
                            // We either become `(o2 & op1) | (op3 & ~op1)`
                            // or we get optimized into some kind of single
                            // instruction variant, so average the cost at 2

                            costEx = 2;
                            costSz *= 2;
                            break;
                        }

                        case NI_Vector128_Create:
                        case NI_Vector256_Create:
                        case NI_Vector512_Create:
                        {
                            // We shouldn't have "all constants" as they get transformed to CNS_VEC

                            if (opCount is 1)
                            {
                                // We will end up as a broadcast
                                costEx = (byte)((simdSize is 16) ? 1 : 3);
                            }
                            else
                            {
                                // We will end up as a sequence of opCount inserts

                                costEx = (byte)(opCount);
                                costSz *= (byte)(opCount);

                                if (varTypeIsIntegral(simdBaseType))
                                {
                                    costEx *= 4;
                                }
                            }
                            break;
                        }

                        case NI_Vector128_CreateScalar:
                        case NI_Vector128_CreateScalarUnsafe:
                        case NI_Vector256_CreateScalar:
                        case NI_Vector256_CreateScalarUnsafe:
                        case NI_Vector512_CreateScalar:
                        case NI_Vector512_CreateScalarUnsafe:
                        {
                            // We shouldn't have "all constants" as they get transformed to CNS_VEC

                            if (varTypeIsIntegral(simdBaseType))
                            {
                                costEx = 3;

#if TARGET_X86
                                if (varTypeIsLong(simdBaseType))
                                {
                                    costEx += 4;
                                    costSz *= 2;
                                }
#endif
                            }
                            else
                            {
                                costEx = 1;
                            }
                            break;
                        }

                        case NI_Vector128_Dot:
                        case NI_Vector256_Dot:
                        case NI_Vector512_Dot:
                        {
                            var elementCount = 16 / simdBaseType.Size;

                            if (varTypeIsIntegral(simdBaseType))
                            {
                                // We have a multiply, 0-2 additions to reduce down to
                                // V128 and then log2(V128<T>.Count) add operations

                                costEx = (byte)(5 + (3 * int.Log2(elementCount)));
                                costSz += (byte)(costSz * int.Log2(elementCount));
                            }
                            else
                            {
                                costEx = (byte)((simdBaseType == TYP_DOUBLE) ? 9 : 13);
                            }

                            if (simdSize is not 16)
                            {
                                if (simdSize is 32)
                                {
                                    costEx += 1;
                                }
                                else
                                {
                                    costEx += 2;
                                }
                            }
                            break;
                        }

                        case NI_Vector128_ExtractMostSignificantBits:
                        case NI_Vector256_ExtractMostSignificantBits:
                        case NI_Vector512_ExtractMostSignificantBits:
                        {
                            costEx = 3;

                            if (simdSize is not 16)
                            {
                                if (simdSize is 32)
                                {
                                    costEx += 2;
                                }
                                else
                                {
                                    // Convert vector to mask, then extract
                                    costEx += 3;
                                    costSz += 6;
                                }
                            }
                            break;
                        }

                        case NI_Vector128_GetElement:
                        case NI_Vector256_GetElement:
                        case NI_Vector512_GetElement:
                        {
                            var op2 = hwTree.GetOp(2);

                            if (op2.Oper.IsConst)
                            {
                                // We can extract the value, possibly
                                // after extracting a particular V128

                                if (varTypeIsIntegral(simdBaseType))
                                {
                                    costEx = 4;
                                }
                                else
                                {
                                    costEx = 1;
                                }

                                if (op2.AsIntCon().IconValue >= (16 / simdBaseType.Size))
                                {
                                    costEx += 3;
                                    costSz *= 2;
                                }
                            }
                            else
                            {
                                // We need a spill + load
                                costEx = FLT_IND_COST_EX;

                                if (simdSize is not 16)
                                {
                                    if (simdSize is 32)
                                    {
                                        costEx += 1;
                                    }
                                    else
                                    {
                                        costEx += 2;
                                    }
                                }

                                if (varTypeIsIntegral(simdBaseType))
                                {
                                    costEx += IND_COST_EX + 1;
                                    costSz += 4;

                                    if (varTypeIsSmall(simdBaseType))
                                    {
                                        costEx += 1;
                                        costSz += 1;
                                    }
                                }
                                else
                                {
                                    costEx = FLT_IND_COST_EX + 2;
                                    costSz += 6;
                                }
                            }
                            break;
                        }

                        case NI_Vector256_GetLower:
                        case NI_Vector512_GetLower:
                        case NI_Vector512_GetLower128:
                        {
                            costEx = 1;
                            break;
                        }

                        case NI_Vector256_GetUpper:
                        case NI_Vector512_GetUpper:
                        {
                            costEx = 3;
                            break;
                        }

                        case NI_Vector128_Shuffle:
                        case NI_Vector128_ShuffleNative:
                        case NI_Vector128_ShuffleNativeFallback:
                        case NI_Vector256_Shuffle:
                        case NI_Vector256_ShuffleNative:
                        case NI_Vector256_ShuffleNativeFallback:
                        case NI_Vector512_Shuffle:
                        case NI_Vector512_ShuffleNative:
                        case NI_Vector512_ShuffleNativeFallback:
                        {
                            // These are likely becoming calls
                            costEx = 5 + (3 * IND_COST_EX);
                            costSz = 5;
                            break;
                        }

                        case NI_Vector128_ToScalar:
                        case NI_Vector256_ToScalar:
                        case NI_Vector512_ToScalar:
                        {
                            costEx = (byte)(varTypeIsIntegral(simdBaseType) ? 3 : 1);
                            break;
                        }

                        case NI_Vector128_ToVector512:
                        case NI_Vector256_ToVector512:
                        case NI_Vector128_ToVector256:
                        case NI_Vector128_ToVector256Unsafe:
                        case NI_Vector256_ToVector512Unsafe:
                        {
                            costEx = 1;
                            break;
                        }

                        case NI_Vector128_WithElement:
                        case NI_Vector256_WithElement:
                        case NI_Vector512_WithElement:
                        {
                            var op2 = hwTree.GetOp(2);

                            if (op2.Oper.IsConst)
                            {
                                // We can insert the value, possibly
                                // after extracting a particular V128,
                                // and then reinserting the V128 as well

                                if (varTypeIsIntegral(simdBaseType))
                                {
                                    costEx = 4;
                                }
                                else
                                {
                                    costEx = 1;
                                }

                                if (op2.AsIntCon().IconValue >= (16 / simdBaseType.Size))
                                {
                                    costEx += 6;
                                    costSz *= 3;
                                }
                            }
                            else
                            {
                                // We need a spill + write + load
                                costEx = FLT_IND_COST_EX;

                                if (simdSize is not 16)
                                {
                                    if (simdSize is 32)
                                    {
                                        costEx += 1;
                                    }
                                    else
                                    {
                                        costEx += 2;
                                    }
                                }

                                if (varTypeIsIntegral(simdBaseType))
                                {
                                    costEx += IND_COST_EX + IND_COST_EX + 1;
                                    costSz += 8;

                                    if (varTypeIsSmall(simdBaseType))
                                    {
                                        costEx += 2;
                                        costSz += 2;
                                    }
                                }
                                else
                                {
                                    costEx = FLT_IND_COST_EX + FLT_IND_COST_EX + 2;
                                    costSz += 12;
                                }
                            }
                            break;
                        }

                        case NI_Vector256_WithLower:
                        case NI_Vector256_WithUpper:
                        case NI_Vector512_WithLower:
                        case NI_Vector512_WithUpper:
                        {
                            costEx = 3;
                            break;
                        }

                        case NI_Vector128_op_Division:
                        case NI_Vector256_op_Division:
                        case NI_Vector512_op_Division:
                        {
                            // We generate a fairly complex sequence involving
                            // comparisons, two branches, conversions, and a fp
                            // division

                            costEx = 46;
                            costSz = (byte)((costSz * 11) + 4);
                            break;
                        }

                        case NI_Vector128_op_Equality:
                        case NI_Vector128_op_Inequality:
                        case NI_Vector256_op_Equality:
                        case NI_Vector256_op_Inequality:
                        case NI_Vector512_op_Equality:
                        case NI_Vector512_op_Inequality:
                        {
                            // We emit a simd compare, get mask, integer compare,
                            // and a branch or setcc

                            if (varTypeIsIntegral(simdBaseType))
                            {
                                costEx = 6;
                                costSz = (byte)((costSz * 2) + 3);
                            }
                            else
                            {
                                costEx = 9;
                                costSz = (byte)((costSz * 2) + 3);
                            }
                            break;
                        }

                        case NI_X86Base_Divide:
                        case NI_X86Base_DivideScalar:
                        case NI_AVX_Divide:
                        case NI_AVX512_Divide:
                        case NI_AVX512_DivideScalar:
                        {
                            costEx = (byte)((simdBaseType == TYP_DOUBLE) ? 14 : 11);
                            break;
                        }

                        case NI_X86Base_DotProduct:
                        {
                            costEx = (byte)((simdBaseType == TYP_DOUBLE) ? 9 : 13);
                            break;
                        }

                        case NI_X86Base_LoadFence:
                        {
                            costEx = 4;
                            break;
                        }

                        case NI_X86Base_MemoryFence:
                        {
                            costEx = 33;
                            break;
                        }

                        case NI_X86Base_MultiplyLow:
                        case NI_AVX2_MultiplyLow:
                        {
                            costEx = (byte)(varTypeIsInt(simdBaseType) ? 10 : 5);
                            break;
                        }

                        case NI_X86Base_Pause:
                        {
                            costEx = 140;
                            break;
                        }

                        case NI_X86Base_Prefetch0:
                        case NI_X86Base_Prefetch1:
                        case NI_X86Base_Prefetch2:
                        case NI_X86Base_PrefetchNonTemporal:
                        {
                            costEx = 1;
                            break;
                        }

                        case NI_X86Base_Sqrt:
                        case NI_X86Base_SqrtScalar:
                        case NI_AVX_Sqrt:
                        case NI_AVX512_Sqrt:
                        case NI_AVX512_SqrtScalar:
                        {
                            costEx = (byte)((simdBaseType == TYP_DOUBLE) ? 16 : 12);
                            break;
                        }

                        case NI_X86Base_StoreFence:
                        {
                            costEx = 6;
                            break;
                        }

                        case NI_AVX_TestC:
                        case NI_AVX_TestNotZAndNotC:
                        case NI_AVX_TestZ:
                        {
                            costEx = (byte)((simdSize is 16) ? 3 : 5);
                            break;
                        }

                        case NI_AVX512_AlignRight32:
                        case NI_AVX512_AlignRight64:
                        {
                            costEx = (byte)((simdSize is 16) ? 1 : 3);
                            break;
                        }

                        case NI_AVX512_ConvertToVector128Byte:
                        case NI_AVX512_ConvertToVector128ByteWithSaturation:
                        case NI_AVX512_ConvertToVector128Int16:
                        case NI_AVX512_ConvertToVector128Int16WithSaturation:
                        case NI_AVX512_ConvertToVector128Int32WithSaturation:
                        case NI_AVX512_ConvertToVector128SByte:
                        case NI_AVX512_ConvertToVector128SByteWithSaturation:
                        case NI_AVX512_ConvertToVector128UInt16:
                        case NI_AVX512_ConvertToVector128UInt16WithSaturation:
                        case NI_AVX512_ConvertToVector128UInt32WithSaturation:
                        case NI_AVX512_ConvertToVector256Byte:
                        case NI_AVX512_ConvertToVector256ByteWithSaturation:
                        case NI_AVX512_ConvertToVector256Int16:
                        case NI_AVX512_ConvertToVector256Int16WithSaturation:
                        case NI_AVX512_ConvertToVector256Int32WithSaturation:
                        case NI_AVX512_ConvertToVector256SByte:
                        case NI_AVX512_ConvertToVector256SByteWithSaturation:
                        case NI_AVX512_ConvertToVector256UInt16:
                        case NI_AVX512_ConvertToVector256UInt16WithSaturation:
                        case NI_AVX512_ConvertToVector256UInt32WithSaturation:
                        {
                            costEx = (byte)((simdSize is 16) ? 2 : 4);
                            break;
                        }

                        case NI_AVX512_ConvertToVector128Int32:
                        {
                            costEx = (byte)((simdSize is 16) ? 1 : 3);
                            break;
                        }

                        case NI_AVX512_ConvertToVector128Double:
                        case NI_AVX512_ConvertToVector256Double:
                        case NI_AVX512_ConvertToVector512Double:
                        {
                            if (varTypeIsLong(simdBaseType))
                            {
                                costEx = 4;
                            }
                            else
                            {
                                costEx = (byte)((retType == TYP_SIMD16) ? 5 : 7);
                            }
                            break;
                        }

                        case NI_AVX512_ConvertToVector128Int64:
                        case NI_AVX512_ConvertToVector128Int64WithTruncation:
                        {
                            if (simdBaseType == TYP_DOUBLE)
                            {
                                costEx = 4;
                            }
                            else
                            {
                                costEx = (byte)((retType == TYP_SIMD16) ? 5 : 7);
                            }
                            break;
                        }

                        case NI_AVX512_ConvertToVector128Single:
                        case NI_AVX512_ConvertToVector256Single:
                        case NI_AVX512_ConvertToVector512Single:
                        {
                            if (varTypeIsLong(simdBaseType))
                            {
                                costEx = (byte)((simdSize is 16) ? 5 : 7);
                            }
                            else
                            {
                                costEx = 4;
                            }
                            break;
                        }

                        case NI_AVX512_ConvertToVector128UInt32:
                        case NI_AVX512_ConvertToVector128UInt32WithTruncation:
                        case NI_AVX512_ConvertToVector256Int32:
                        case NI_AVX512_ConvertToVector256Int32WithTruncation:
                        case NI_AVX512_ConvertToVector256UInt32:
                        case NI_AVX512_ConvertToVector256UInt32WithTruncation:
                        case NI_AVX10v2_ConvertToVectorInt32WithTruncatedSaturation:
                        case NI_AVX10v2_ConvertToVectorUInt32WithTruncatedSaturation:
                        {
                            if (varTypeIsIntegral(simdBaseType))
                            {
                                costEx = (byte)((simdSize is 16) ? 1 : 3);
                            }
                            else if (simdBaseType == TYP_DOUBLE)
                            {
                                costEx = (byte)((simdSize is 16) ? 5 : 7);
                            }
                            else
                            {
                                costEx = 4;
                            }
                            break;
                        }

                        case NI_AVX512_ConvertToVector128UInt64:
                        case NI_AVX512_ConvertToVector128UInt64WithTruncation:
                        case NI_AVX512_ConvertToVector256Int64:
                        case NI_AVX512_ConvertToVector256Int64WithTruncation:
                        case NI_AVX512_ConvertToVector256UInt64:
                        case NI_AVX512_ConvertToVector256UInt64WithTruncation:
                        case NI_AVX512_ConvertToVector512Int64:
                        case NI_AVX512_ConvertToVector512Int64WithTruncation:
                        case NI_AVX512_ConvertToVector512UInt64:
                        case NI_AVX512_ConvertToVector512UInt64WithTruncation:
                        case NI_AVX10v2_ConvertToVectorInt64WithTruncatedSaturation:
                        case NI_AVX10v2_ConvertToVectorUInt64WithTruncatedSaturation:
                        {
                            if (simdBaseType == TYP_FLOAT)
                            {
                                costEx = (byte)((retType == TYP_SIMD16) ? 5 : 7);
                            }
                            else
                            {
                                costEx = 4;
                            }
                            break;
                        }

                        case NI_AVX512_DetectConflicts:
                        {
                            if (simdSize is 16)
                            {
                                costEx = (byte)(varTypeIsLong(simdBaseType) ? 4 : 11);
                            }
                            else if (simdSize is 32)
                            {
                                costEx = (byte)(varTypeIsLong(simdBaseType) ? 13 : 16);
                            }
                            else
                            {
                                costEx = (byte)(varTypeIsLong(simdBaseType) ? 17 : 26);
                            }
                            break;
                        }

                        case NI_AVX512_Reciprocal14:
                        case NI_AVX512_Reciprocal14Scalar:
                        case NI_AVX512_ReciprocalSqrt14:
                        case NI_AVX512_ReciprocalSqrt14Scalar:
                        {
                            if (simdBaseType == TYP_FLOAT)
                            {
                                costEx = (byte)((simdSize is 64) ? 7 : 4);
                            }
                            else
                            {
                                costEx = 4;
                            }
                            break;
                        }

                        case NI_X86Serialize_Serialize:
                        {
                            costEx = 105;
                            break;
                        }

                        case NI_AVX_PTEST:
                        {
                            if (varTypeIsIntegral(simdBaseType))
                            {
                                costEx = (byte)((simdSize is 16) ? 4 : 6);
                            }
                            else
                            {
                                costEx = (byte)((simdSize is 16) ? 3 : 5);
                            }
                            break;
                        }

                        case NI_AVX512_ConvertMaskToVector:
                        {
                            costEx = (byte)(varTypeIsSmall(simdBaseType) ? 3 : 1);
                            break;
                        }

                        default:
                        {
                            NO_WAY("Unhandled costing for HWIntrinsic");
                            costEx = (byte)(varTypeIsIntegral(simdBaseType) ? 1 : 4);
                            break;
                        }
                    }
                }
            }
#endif
        }
#endif

        // The binary case is special because of GTF_REVERSE_OPS.
        if (opCount is 2)
        {
            var lvl2 = 0;

            var op1 = multiOp.GetOp(1);
            var op2 = multiOp.GetOp(2);

            var addrLevel = level;

            if (op1 != addrOp)
            {
                level = gtSetEvalOrder(op1);

                if (optsEnabled)
                {
                    costEx += op1.CostEx;
                    costSz += op1.CostSz;
                }
            }

            if (op2 != addrOp)
            {
                lvl2 = gtSetEvalOrder(op2);

                if (optsEnabled)
                {
                    costEx += op2.CostEx;
                    costSz += op2.CostSz;
                }
            }
            else
            {
                lvl2 = addrLevel;
            }

            // This way we have "level" be the complexity of the
            // first tree to be evaluated, and "lvl2" - the second.

            if (multiOp.IsReverseOp)
            {
                assert(!multiOp.AsHWIntrinsic().IsUserCall);

                (op1, op2) = (op2, op1);
                (level, lvl2) = (lvl2, level);
            }

            // We want the more complex tree to be evaluated first.
            if ((level < lvl2) && !multiOp.AsHWIntrinsic().IsUserCall && gtCanSwapOrder(op1, op2))
            {
                multiOp.IsReverseOp ^= true;
                (level, lvl2) = (lvl2, level);
            }

            if (level < 1)
            {
                level = lvl2;
            }
            else if (level == lvl2)
            {
                level += 1;
            }
        }
        else if (opCount is 1)
        {
            var op1 = multiOp.GetOp(1);

            if (op1 != addrOp)
            {
                level = gtSetEvalOrder(op1);

                if (optsEnabled)
                {
                    costEx += op1.CostEx;
                    costSz += op1.CostSz;
                }
            }
        }
        else
        {
            var operands = multiOp.Operands;

            for (var i = operands.Length; i >= 1; i--)
            {
                var op = operands[i - 1];

                if (op == addrOp)
                {
                    continue;
                }

                level = int.Max(gtSetEvalOrder(op), level + 1);

                if (optsEnabled)
                {
                    // We don't need/have costs in MinOpts
                    costEx += op.CostEx;
                    costSz += op.CostSz;
                }
            }
        }

        if (optsEnabled)
        {
            multiOp.SetCosts(costEx, costSz);
        }
        return level;
    }
#endif

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

    /// <summary>Check if a tree has a read of the specified local, taking promotion into account.</summary>
    /// <param name="tree">The tree to check.</param>
    /// <param name="lclNum">The local to look for.</param>
    /// <returns>True if there is any GT_LCL_VAR or GT_LCL_FLD node whose value depends on "lclNum".</returns>
    public bool gtTreeHasLocalRead(GenTree tree, int lclNum)
    {
        var visitor = new TreeHasLocalReadVisitor(this, lclNum);
        return visitor.WalkTree(ref tree, user: null) == WALK_ABORT;
    }

    /// <summary>Check if a tree has a store that affects the specified local, taking promotion into account.</summary>
    /// <param name="tree">The tree to check.</param>
    /// <param name="lclNum">The local to look for.</param>
    /// <returns>True if there is any definition that affects "lclNum".</returns>
    public bool gtTreeHasLocalStore(GenTree tree, int lclNum)
    {
        var visitor = new TreeHasLocalStoreVisitor(this, lclNum);
        return visitor.WalkTree(ref tree, user: null) == WALK_ABORT;
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

#if DEBUG
        JITDUMP($"gtTryRemoveBoxUpstreamEffects: {((options == BR_DONT_REMOVE) ? "checking if it is possible" : "attempting")} of BOX (valuetype) [{box.TreeId:D6}] (assign/newobj {FMT_STMT(allocStmt.Id)} copy {FMT_STMT(copyStmt.Id)}\n");
#endif

        // If we don't recognize the form of the store, bail.
        var boxLclDef = allocStmt.RootNode;

        if (boxLclDef.Oper is not GT_STORE_LCL_VAR)
        {
            JITDUMP($" bailing; unexpected alloc def op {boxLclDef.Oper.Name}\n");
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
                JITDUMP($" bailing; must wait for replacement of copy {copy.Oper.Name}\n");
            }
            else
            {
                // Anything else is a missed case we should figure out how to handle.
                // One known case is GT_COMMAs enclosing the store we are looking for.
                JITDUMP($" bailing; unexpected copy op {copy.Oper.Name}\n");
            }
            return null;
        }

        // If the copy is a struct copy, make sure we know how to isolate any source side effects.
        var copySrc = copy.Data;

        // If the copy source is from a pending inline, wait for it to resolve.
        if (copySrc.Oper is GT_RET_EXPR)
        {
            JITDUMP($" bailing; must wait for replacement of copy source {copySrc.Oper.Name}\n");
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
                    JITDUMP($" bailing; unexpected copy source struct op with side effect {copySrc.Oper.Name}\n");
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
#if DEBUG
        JITDUMP($"\nBashing NEWOBJ [{boxLclDef.TreeId:D6}] to NOP\n");
#endif

        allocStmt.RootNode = gtNewNothingNode();
        DEBUG_DESTROY_NODE(boxLclDef);

        // Change the copy expression so it preserves key source side effects.

#if DEBUG
        JITDUMP($"\nBashing COPY [{copy.TreeId:D6}]");
#endif

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

#if DEBUG
            JITDUMP($" to scalar read via [{copySrc.TreeId:D6}]\n");
#endif
        }
        else
        {
            // For struct types read the first byte of the source struct; there's
            // no need to read the entire thing, and no place to put it.
            assert(copySrc.Oper.IsLoad);
            copyStmt.RootNode = copySrc;

            if (options is BR_REMOVE_AND_NARROW or BR_REMOVE_AND_NARROW_WANT_TYPE_HANDLE)
            {
#if DEBUG
                JITDUMP($" to read first byte of struct via modified [{copySrc.TreeId:D6}]\n");
#endif

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
#if DEBUG
                JITDUMP($" to read entire struct via modified [{copySrc.TreeId:D6}]\n");
#endif
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

    /// <summary>Traverse and mark an address expression</summary>
    /// <param name="op1">An out parameter which is either the address expression, or one of its operands.</param>
    /// <param name="op2">An out parameter which starts as either null or one of the operands of the address expression.</param>
    /// <param name="baseAddr">The base address of the addressing mode, or null if 'constOnly' is false</param>
    /// <param name="constOnly">True if we will only traverse into ADDs with constant op2.</param>
    public void gtWalkOp(ref GenTree op1, ref GenTree? op2, GenTree? baseAddr, bool constOnly)
    {
        // This routine is a helper routine for gtSetEvalOrder() and is used to identify the
        // base and index nodes, which will be validated against those identified by
        // genCreateAddrMode().
        // It also marks the ADD nodes involved in the address expression with the
        // GTF_ADDRMODE_NO_CSE flag which prevents them from being considered for CSE's.
        //
        // Its two output parameters are modified under the following conditions:
        //
        // It is called once with the original address expression as 'op1WB', and
        // with 'constOnly' set to false. On this first invocation, *op1WB is always
        // an ADD node, and it will consider the operands of the ADD even if its op2 is
        // not a constant. However, when it encounters a non-constant or the base in the
        // op2 position, it stops iterating. That operand is returned in the 'op2WB' out
        // parameter, and will be considered on the third invocation of this method if
        // it is an ADD.
        //
        // It is called the second time with the two operands of the original expression, in
        // the original order, and the third time in reverse order. For these invocations
        // 'constOnly' is true, so it will only traverse cascaded ADD nodes if they have a
        // constant op2.
        //
        // The result, after three invocations, is that the values of the two out parameters
        // correspond to the base and index in some fashion. This method doesn't attempt
        // to determine or validate the scale or offset, if any.
        //
        // Assumptions (presumed to be ensured by genCreateAddrMode()):
        //    If an ADD has a constant operand, it is in the op2 position.
        //
        // Notes:
        //    This method, and its invocation sequence, are quite confusing, and since they
        //    were not originally well-documented, this specification is a possibly-imperfect
        //    reconstruction.
        //    The motivation for the handling of the NOP case is unclear.
        //    Note that 'op2WB' is only modified in the initial (!constOnly) case,
        //    or if a NOP is encountered in the op1 position.

        op1 = op1.EffectiveVal;

        // Now we look for op1's with non-overflow GT_ADDs [of constants]
        while ((op1.Oper is GT_ADD) && !op1.HasOverflowCheck)
        {
            var add = op1.AsOp();

            var addOp1 = add.Op1;
            var addOp2 = add.Op2;

            if (constOnly)
            {
                if (!addOp2.Oper.IsCnsIntOrI)
                {
                    break;
                }

                var intCon = addOp2.AsIntCon();

                if (!intCon.AsIntCon().ImmedValCanBeFolded(this, GT_ADD))
                {
                    break;
                }

                if (intCon.IsIconHandle(GTF_ICON_OBJ_HDL) && !intCon.IsIntegralConst(0))
                {
                    // Ignore ADD(CNS, CNS-gc-handle)
                    break;
                }
            }

            // mark it with GTF_ADDRMODE_NO_CSE
            add.Flags |= GTF_ADDRMODE_NO_CSE;

            if (!constOnly)
            {
                op2 = addOp2;
            }
            op1 = addOp1;

            if (!constOnly)
            {
                if (op2 == baseAddr)
                {
                    break;
                }

                assert(op2 is not null);

                if (!op2.Oper.IsCnsIntOrI || !op2.AsIntCon().ImmedValCanBeFolded(this, GT_ADD))
                {
                    break;
                }
            }

            op1 = op1.EffectiveVal;
        }
    }

    /// <summary>Extracts side effects from sideEffectSource (if any) and wraps the input tree with a COMMA node with them.</summary>
    /// <param name="tree">the expression tree to wrap with side effects (if any) it has to be either a side effect free subnode of sideEffectsSource or any tree outside sideEffectsSource's hierarchy</param>
    /// <param name="sideEffectsSource">the expression tree to extract side effects from</param>
    /// <param name="sideEffectsFlags">side effect flags to be considered</param>
    /// <param name="ignoreRoot">ignore side effects on the expression root node</param>
    /// <returns>The original tree wrapped with a COMMA node that contains the side effects or just the tree itself if sideEffectSource has no side effects.</returns>
    public GenTree gtWrapWithSideEffects(GenTree tree, GenTree sideEffectsSource, GenTreeFlags sideEffectsFlags = GTF_SIDE_EFFECT, bool ignoreRoot = false)
    {
        var sideEffects = null as GenTree;
        gtExtractSideEffList(sideEffectsSource, ref sideEffects, sideEffectsFlags, ignoreRoot);

        if (sideEffects is not null)
        {
            // TODO: assert if tree is a subnode of sideEffectsSource and the tree has its own side effects
            // otherwise the resulting COMMA might have some side effects to be duplicated
            // It should be possible to be smarter here and allow such cases by extracting the side effects
            // properly for this particular case. For now, caller is responsible for avoiding such cases.

            var comma = gtNewCommaNode(tree.Type, sideEffects, tree);

            if ((vnStore is not null) && tree._vnPair.BothDefined() && sideEffectsSource._vnPair.BothDefined())
            {
                NYI("TODO: Port once vnStore is ported");
                // comma._vnPair = vnStore.VNPWithExc(tree._vnPair, vnStore.VNPExceptionSet(sideEffectsSource._vnPair));
            }
            comma.SetMorphed(this);
            return comma;
        }
        return tree;
    }
}
