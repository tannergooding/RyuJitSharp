// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, valuenum.cpp.

namespace RyuJitSharp;

public partial class Compiler
{
    private static VNFunc GetVNFuncForNode(GenTree node)
    {
        var oper = node.Oper;
        switch (oper)
        {
            case GT_EQ:
            case GT_NE:
            {
                if (varTypeIsFloating(node.AsOp().Op1.Type))
                {
                    assert(varTypeIsFloating(node.AsOp().Op2.Type));
                    assert(((node.Flags & GTF_RELOP_NAN_UN) != 0) == (oper is GT_NE));
                }
                break;
            }

            case GT_LT:
            case GT_LE:
            case GT_GT:
            case GT_GE:
            {
                var op1 = node.AsOp().Op1;
                var op2 = node.AsOp().Op2;
                if (varTypeIsFloating(op1.Type))
                {
                    assert(varTypeIsFloating(op2.Type));
                    if ((node.Flags & GTF_RELOP_NAN_UN) != 0)
                    {
                        return oper switch
                        {
                            GT_LT => VNF_LT_UN,
                            GT_LE => VNF_LE_UN,
                            GT_GE => VNF_GE_UN,
                            GT_GT => VNF_GT_UN,
                            _ => throw new System.Diagnostics.UnreachableException(),
                        };
                    }
                }
                else
                {
                    assert(varTypeIsIntegralOrI(op1.Type) && varTypeIsIntegralOrI(op2.Type));
                    if (node.AsOp().IsUnsigned)
                    {
                        return oper switch
                        {
                            GT_LT => VNF_LT_UN,
                            GT_LE => VNF_LE_UN,
                            GT_GE => VNF_GE_UN,
                            GT_GT => VNF_GT_UN,
                            _ => throw new System.Diagnostics.UnreachableException(),
                        };
                    }
                }
                break;
            }

            case GT_ADD:
            case GT_SUB:
            case GT_MUL:
            {
                if (varTypeIsIntegralOrI(node.AsOp().Op1.Type) && node.HasOverflowCheck)
                {
                    assert(varTypeIsIntegralOrI(node.AsOp().Op2.Type));
                    return (oper, node.AsOp().IsUnsigned) switch
                    {
                        (GT_ADD, false) => VNF_ADD_OVF,
                        (GT_SUB, false) => VNF_SUB_OVF,
                        (GT_MUL, false) => VNF_MUL_OVF,
                        (GT_ADD, true) => VNF_ADD_UN_OVF,
                        (GT_SUB, true) => VNF_SUB_UN_OVF,
                        (GT_MUL, true) => VNF_MUL_UN_OVF,
                        _ => throw new System.Diagnostics.UnreachableException(),
                    };
                }
                break;
            }

#if FEATURE_HW_INTRINSICS
            case GT_HWINTRINSIC:
            {
                return (VNFunc)((int)VNF_HWI_FIRST +
                    (int)node.AsHWIntrinsic().HWIntrinsicId - (int)NI_HW_INTRINSIC_START - 1);
            }
#endif
            case GT_CAST:
            {
                throw new System.Diagnostics.UnreachableException(
                    "Cast nodes use fgValueNumberCastTree instead of GetVNFuncForNode.");
            }

            default:
            {
                assert(!oper.MayOverflow);
                break;
            }
        }

        return (VNFunc)oper;
    }

    private static bool VNTreeFuncIsLegal(VNFunc func)
        => (func > VNF_Boundary) ||
            ((VNFuncExtensions.GetAttributes(func) &
              ValueNumStore.VNFOpAttrib.VNFOA_IllegalGenTreeOp) == 0);

    public unsafe void fgValueNumberTree(GenTree tree)
    {
        assert(vnStore is not null);
        var oper = tree.Oper;
        var type = tree.Type;

        if (oper.IsConst)
        {
            fgValueNumberTreeConst(tree);
        }
        else if (oper.IsLeaf)
        {
            switch (oper)
            {
                case GT_LCL_ADDR:
                {
                    var local = tree.AsLclFld();
                    var lclNum = local.LclNum;
                    tree._vnPair.SetBoth(vnStore.VNForFunc(TYP_BYREF, VNF_PtrToLoc,
                        vnStore.VNForIntCon(lclNum), vnStore.VNForIntPtrCon(local.LclOffs)));
#if DEBUG
                    ref var descriptor = ref lvaGetDesc(lclNum);
                    assert(descriptor.IsAddressExposed || descriptor.IsDefinedViaAddress);
#endif
                    break;
                }

                case GT_LCL_VAR:
                {
                    var local = tree.AsLclVarCommon();
                    var lclNum = local.LclNum;
                    ref var descriptor = ref lvaGetDesc(lclNum);
                    if (local.HasSsaName)
                    {
                        fgValueNumberSsaVarDef(local);
                    }
                    else if (descriptor.IsAddressExposed)
                    {
                        var address = vnStore.VNForFunc(TYP_BYREF, VNF_PtrToLoc,
                            vnStore.VNForIntCon(lclNum), vnStore.VNForIntPtrCon(local.LclOffs));
                        tree._vnPair.Liberal = fgValueNumberByrefExposedLoad(type, address);
                        tree._vnPair.Conservative = vnStore.VNForExpr(compCurBB, type);
                    }
                    else
                    {
                        tree._vnPair.SetBoth(vnStore.VNForExpr(compCurBB, type));
                    }
                    break;
                }

                case GT_LCL_FLD:
                {
                    var local = tree.AsLclFld();
                    var lclNum = local.LclNum;
                    ref var descriptor = ref lvaGetDesc(lclNum);
                    if (local.HasSsaName)
                    {
                        var value = descriptor.GetPerSsaData(local.SsaNum)._vnPair;
                        tree._vnPair = vnStore.VNPairForLoad(value, lvaLclValueSize(lclNum),
                            type, local.LclOffs, local.ValueSize);
                    }
                    else if (descriptor.IsAddressExposed)
                    {
                        var address = vnStore.VNForFunc(TYP_BYREF, VNF_PtrToLoc,
                            vnStore.VNForIntCon(lclNum), vnStore.VNForIntPtrCon(local.LclOffs));
                        tree._vnPair.Liberal = fgValueNumberByrefExposedLoad(type, address);
                        tree._vnPair.Conservative = vnStore.VNForExpr(compCurBB, type);
                    }
                    else
                    {
                        tree._vnPair.SetBoth(vnStore.VNForExpr(compCurBB, type));
                    }
                    break;
                }

                case GT_CATCH_ARG:
                case GT_ASYNC_CONTINUATION:
                case GT_CONTINUATION_MEMBER_OFFSET:
                case GT_SWIFT_ERROR:
                {
                    tree._vnPair.SetBoth(vnStore.VNForExpr(compCurBB, type));
                    break;
                }

                case GT_MEMORYBARRIER:
                {
                    fgMutateGcHeap(tree, "MEMORYBARRIER");
                    tree._vnPair = ValueNumStore.VNPForVoid();
                    break;
                }

                case GT_NO_OP:
                case GT_NOP:
                case GT_GCPOLL:
                case GT_JMP:
                case GT_LABEL:
                {
                    tree._vnPair = ValueNumStore.VNPForVoid();
                    break;
                }

                case GT_PHI_ARG:
                {
                    assert(false);
                    break;
                }

                default:
                {
                    throw new System.Diagnostics.UnreachableException(
                        $"Unhandled leaf {oper} in fgValueNumberTree.");
                }
            }
        }
        else if (oper.IsSimple)
        {
            if (oper is GT_IND or GT_BLK)
            {
                var addr = tree.AsIndir().Addr;
                var isVolatile = (tree.Flags & GTF_IND_VOLATILE) != 0;
                vnStore.VNPUnpackExc(addr._vnPair, out var addrNormal, out var addrExceptions);

                if ((tree.Flags & GTF_IND_INVARIANT) != 0)
                {
                    assert(!isVolatile);
                    var returnsTypeHandle = false;
                    if ((oper is GT_IND) && (addr.Type is TYP_REF) && (type is TYP_I_IMPL))
                    {
                        var handle = gtGetClassHandle(addr, out var isExact, out _);
                        if (isExact && (handle != NO_CLASS_HANDLE))
                        {
#if DEBUG
                            JITDUMP($"IND(obj) is actually a class handle for {eeGetClassName(handle)}\n");
#endif
                            if (!eeIsSharedInst(handle))
                            {
                                void* indirect;
                                var embedded = info.compCompHnd->embedClassHandle(handle, &indirect);
                                if (indirect is null)
                                {
                                    assert(embedded is not null);
                                    var handleVN = vnStore.VNForHandle((nint)embedded, GTF_ICON_CLASS_HDL);
                                    tree._vnPair = vnStore.VNPWithExc(new(handleVN, handleVN), addrExceptions);
                                    returnsTypeHandle = true;
                                }
                            }
                        }
                        else
                        {
                            var app = new VNFuncApp();
                            if (vnStore.GetVNFunc(addrNormal.Liberal, ref app) &&
                                app.FuncIs(VNF_JitNew) && addrNormal.BothEqual())
                            {
                                var classVN = app.GetArg(0);
                                tree._vnPair = vnStore.VNPWithExc(new(classVN, classVN), addrExceptions);
                                returnsTypeHandle = true;
                            }
                        }
                    }

                    if (!returnsTypeHandle)
                    {
                        if ((addr.Type is TYP_REF) && fgValueNumberConstLoad(tree.AsIndir()))
                        {
                            // The constant-load helper assigns the value numbers.
                        }
                        else if (addr.Oper.IsCnsIntOrI &&
                            addr.AsIntCon().IsIconHandle(GTF_ICON_STATIC_BOX_PTR))
                        {
                            assert(addrNormal.BothEqual() &&
                                (addrExceptions == ValueNumStore.VNPForEmptyExcSet()));
                            var fieldSeq = vnStore.VNForFieldSeq(null);
                            var offset = vnStore.VNForIntPtrCon(-TARGET_POINTER_SIZE);
                            var staticAddr = vnStore.VNForFunc(type, VNF_PtrToStatic,
                                addrNormal.Liberal, fieldSeq, offset);
                            tree._vnPair.SetBoth(staticAddr);
                        }
                        else
                        {
                            var loadFunc = (tree.Flags & GTF_IND_NONNULL) != 0
                                ? VNF_InvariantNonNullLoad : VNF_InvariantLoad;
                            if ((loadFunc is VNF_InvariantNonNullLoad) &&
                                addr.Oper.IsCnsIntOrI &&
                                addr.AsIntCon().IsIconHandle(GTF_ICON_CONST_PTR) &&
                                (addr.AsIntCon().FieldSeq is FieldSeq sequence) &&
                                (sequence.Offset == addr.AsIntCon().IconValue))
                            {
                                addrNormal.SetBoth(vnStore.VNForFieldSeq(sequence));
                            }
                            tree._vnPair = vnStore.VNPairForFunc(type, loadFunc, addrNormal);
                            tree._vnPair = vnStore.VNPWithExc(tree._vnPair, addrExceptions);
                        }
                    }
                }
                else if (isVolatile)
                {
                    fgMutateGcHeap(tree, "GTF_IND_VOLATILE - read");
                    var unique = vnStore.VNForExpr(compCurBB, type);
                    tree._vnPair = vnStore.VNPWithExc(new(unique, unique), addrExceptions);
                }
                else
                {
                    var app = new VNFuncApp();
                    if (fgValueNumberConstLoad(tree.AsIndir()))
                    {
                        // The constant-load helper assigns the value numbers.
                    }
                    else if (vnStore.GetVNFunc(addrNormal.Liberal, ref app) &&
                        app.FuncIs(VNF_PtrToStatic))
                    {
                        var fieldSeq = vnStore.FieldSeqVNToFieldSeq(app.GetArg(1));
                        var offset = vnStore.ConstantValue<nint>(app.GetArg(2));
                        System.ArgumentNullException.ThrowIfNull(fieldSeq);
                        fgValueNumberFieldLoad(tree, null, fieldSeq, offset);
                    }
                    else if (vnStore.GetVNFunc(addrNormal.Liberal, ref app) &&
                        app.FuncIs(VNF_PtrToArrElem))
                    {
                        fgValueNumberArrayElemLoad(tree, app);
                    }
                    else if (addr.IsFieldAddr(this, out var baseAddr, out var fieldSeq, out var offset))
                    {
                        assert(fieldSeq is not null);
                        fgValueNumberFieldLoad(tree, baseAddr, fieldSeq, offset);
                    }
                    else
                    {
                        tree._vnPair.Liberal = fgValueNumberByrefExposedLoad(type, addr._vnPair.Liberal);
                        tree._vnPair.Conservative = vnStore.VNForExpr(compCurBB, type);
                    }
                    tree._vnPair = vnStore.VNPWithExc(tree._vnPair, addrExceptions);
                }
            }
            else if (oper is GT_CAST)
            {
                fgValueNumberCastTree(tree);
            }
            else if (oper is GT_INTRINSIC)
            {
                fgValueNumberIntrinsic(tree);
            }
            else
            {
                var func = GetVNFuncForNode(tree);
                if (VNTreeFuncIsLegal(func))
                {
                    if (oper.IsUnary)
                    {
                        assert(tree.AsUnOp().Op1 is not null);
                        vnStore.VNPUnpackExc(tree.AsUnOp().Op1._vnPair,
                            out var operand, out var operandExceptions);
                        if (oper.IsArrLength && ((tree.AsUnOp().Op1.Flags & GTF_GLOB_REF) != 0))
                        {
                            operand.SetBoth(operand.Conservative);
                        }
                        tree._vnPair = vnStore.VNPWithExc(
                            vnStore.VNPairForFunc(type, func, operand), operandExceptions);
                    }
                    else
                    {
                        assert(oper.IsBinary);
                        vnStore.VNPUnpackExc(tree.AsOp().Op1._vnPair,
                            out var left, out var leftExceptions);
                        vnStore.VNPUnpackExc(tree.AsOp().Op2._vnPair,
                            out var right, out var rightExceptions);
                        var exceptions = vnStore.VNPExcSetUnion(leftExceptions, rightExceptions);

                        var extended = ValueNumStore.NoVN;
                        if ((oper is GT_ADD) && !tree.HasOverflowCheckEx)
                        {
                            extended = vnStore.ExtendPtrVN(tree.AsOp().Op1, tree.AsOp().Op2);
                        }
                        if (extended != ValueNumStore.NoVN)
                        {
                            tree._vnPair = vnStore.VNPWithExc(new(extended, extended), exceptions);
                        }
                        else
                        {
                            var normal = vnStore.VNPairForFunc(type, func, left, right);
                            tree._vnPair = vnStore.VNPWithExc(normal, exceptions);
                        }
                    }
                }
                else
                {
                    switch (oper)
                    {
                        case GT_STORE_LCL_VAR:
                        case GT_STORE_LCL_FLD:
                        case GT_STOREIND:
                        case GT_STORE_BLK:
                        {
                            fgValueNumberStore(tree);
                            break;
                        }

                        case GT_COMMA:
                        {
                            var exceptions = vnStore.VNPExceptionSet(tree.AsOp().Op1._vnPair);
                            tree._vnPair = vnStore.VNPWithExc(tree.AsOp().Op2._vnPair, exceptions);
                            break;
                        }

                        case GT_ARR_ADDR:
                        {
                            fgValueNumberArrIndexAddr(tree.AsArrAddr());
                            break;
                        }

                        case GT_MDARR_LENGTH:
                        case GT_MDARR_LOWER_BOUND:
                        {
                            var mdarr = tree.AsMDArr();
                            var mdarrFunc = oper is GT_MDARR_LENGTH ? VNF_MDArrLength : VNF_MDArrLowerBound;
                            var array = mdarr.ArrRef;
                            vnStore.VNPUnpackExc(array._vnPair, out var arrNormal, out var arrExceptions);
                            if ((array.Flags & GTF_GLOB_REF) != 0)
                            {
                                arrNormal.SetBoth(arrNormal.Conservative);
                            }
                            var dim = vnStore.VNForIntCon(mdarr.Dim);
                            var normal = vnStore.VNPairForFunc(type, mdarrFunc, arrNormal, new(dim, dim));
                            tree._vnPair = vnStore.VNPWithExc(normal, arrExceptions);
                            break;
                        }

                        case GT_BOUNDS_CHECK:
                        {
                            var check = tree.AsBoundsChk();
                            var index = check.Index._vnPair;
                            var length = check.ArrayLength._vnPair;
                            var exceptions = ValueNumStore.VNPForEmptyExcSet();
                            exceptions = vnStore.VNPUnionExcSet(index, exceptions);
                            exceptions = vnStore.VNPUnionExcSet(length, exceptions);
                            tree._vnPair = vnStore.VNPWithExc(ValueNumStore.VNPForVoid(), exceptions);
                            fgValueNumberAddExceptionSet(tree);

                            var indexVN = vnStore.VNNormalValue(index.Conservative);
                            if ((indexVN != ValueNumStore.NoVN) && !vnStore.IsVNConstant(indexVN))
                            {
                                vnStore.SetVNIsCheckedBound(indexVN, true);
                            }
                            var lengthVN = vnStore.VNNormalValue(length.Conservative);
                            if ((lengthVN != ValueNumStore.NoVN) && !vnStore.IsVNConstant(lengthVN))
                            {
                                vnStore.SetVNIsCheckedBound(lengthVN);
                            }
                            break;
                        }

                        case GT_XORR:
                        case GT_XAND:
                        case GT_XADD:
                        case GT_XCHG:
                        {
                            fgMutateGcHeap(tree, "Interlocked intrinsic");
                            var exceptions = ValueNumStore.VNPForEmptyExcSet();
                            exceptions = vnStore.VNPUnionExcSet(tree.AsOp().Op1._vnPair, exceptions);
                            exceptions = vnStore.VNPUnionExcSet(tree.AsOp().Op2._vnPair, exceptions);
                            tree._vnPair = vnStore.VNPUniqueWithExc(type, exceptions);
                            break;
                        }

                        case GT_JTRUE:
                        case GT_SWITCH:
                        case GT_RETURN:
                        case GT_RETFILT:
                        case GT_RETURN_SUSPEND:
                        case GT_NULLCHECK:
                        {
                            var operand = tree.AsUnOp().Op1;
                            tree._vnPair = operand is not null
                                ? vnStore.VNPWithExc(ValueNumStore.VNPForVoid(),
                                    vnStore.VNPExceptionSet(operand._vnPair))
                                : ValueNumStore.VNPForVoid();
                            break;
                        }

                        case GT_SWIFT_ERROR_RET:
                        {
                            var value = tree.AsOp().Op2;
                            if (value is not null)
                            {
                                vnStore.VNPUnpackExc(tree.AsOp().Op1._vnPair, out _, out var first);
                                vnStore.VNPUnpackExc(value._vnPair, out _, out var second);
                                tree._vnPair = vnStore.VNPWithExc(ValueNumStore.VNPForVoid(),
                                    vnStore.VNPExcSetUnion(first, second));
                            }
                            else
                            {
                                tree._vnPair = vnStore.VNPWithExc(ValueNumStore.VNPForVoid(),
                                    vnStore.VNPExceptionSet(tree.AsOp().Op1._vnPair));
                            }
                            break;
                        }

                        case GT_BOX:
                        case GT_CKFINITE:
                        {
                            tree._vnPair = tree.AsUnOp().Op1._vnPair;
                            break;
                        }

                        case GT_LCLHEAP:
                        case GT_INIT_VAL:
                        {
                            tree._vnPair = vnStore.VNPUniqueWithExc(type,
                                vnStore.VNPExceptionSet(tree.AsUnOp().Op1._vnPair));
                            break;
                        }

                        case GT_BITCAST:
                        {
                            fgValueNumberBitCast(tree);
                            break;
                        }

                        default:
                        {
                            assert(false);
                            tree._vnPair.SetBoth(vnStore.VNForExpr(compCurBB, type));
                            break;
                        }
                    }
                }
            }
            fgValueNumberAddExceptionSet(tree);
        }
        else
        {
            assert(oper.IsSpecial);
            switch (oper)
            {
                case GT_CALL:
                {
                    fgValueNumberCall(tree.AsCall());
                    break;
                }

#if FEATURE_HW_INTRINSICS
                case GT_HWINTRINSIC:
                {
                    fgValueNumberHWIntrinsic(tree.AsHWIntrinsic());
                    break;
                }
#endif
                case GT_CMPXCHG:
                {
                    fgMutateGcHeap(tree, "Interlocked intrinsic");
                    var cmpXchg = tree.AsCmpXchg();
                    assert(tree.IsImplicitIndir);
                    var location = cmpXchg.Addr;
                    var value = cmpXchg.Data;
                    var comparand = cmpXchg.Comparand;
                    var exceptions = ValueNumStore.VNPForEmptyExcSet();
                    exceptions = vnStore.VNPUnionExcSet(location._vnPair, exceptions);
                    exceptions = vnStore.VNPUnionExcSet(value._vnPair, exceptions);
                    exceptions = vnStore.VNPUnionExcSet(comparand._vnPair, exceptions);
                    tree._vnPair = vnStore.VNPUniqueWithExc(type, exceptions);
                    fgValueNumberAddExceptionSetForIndirection(tree, location);
                    break;
                }

                case GT_ARR_ELEM:
                {
                    throw new System.Diagnostics.UnreachableException(
                        "ARR_ELEM must be morphed before value numbering.");
                }

                case GT_FIELD_LIST:
                {
                    tree._vnPair.SetBoth(vnStore.VNForExpr(compCurBB, type));
                    foreach (var use in tree.AsFieldList().Uses)
                    {
                        tree._vnPair = vnStore.VNPWithExc(tree._vnPair,
                            vnStore.VNPExceptionSet(use.Node._vnPair));
                    }
                    break;
                }

                case GT_SELECT:
                {
                    var conditional = tree.AsConditional();
                    vnStore.VNPUnpackExc(conditional.Cond._vnPair, out var condition, out var conditionExc);
                    vnStore.VNPUnpackExc(conditional.Op1._vnPair, out var first, out var firstExc);
                    vnStore.VNPUnpackExc(conditional.Op2._vnPair, out var second, out var secondExc);
                    var exceptions = vnStore.VNPExcSetUnion(conditionExc, firstExc);
                    exceptions = vnStore.VNPExcSetUnion(exceptions, secondExc);
                    var func = GetVNFuncForNode(tree);
                    assert(VNTreeFuncIsLegal(func));
                    var normal = vnStore.VNPairForFunc(type, func, condition, first, second);
                    tree._vnPair = vnStore.VNPWithExc(normal, exceptions);
                    break;
                }

                default:
                {
                    assert(false);
                    tree._vnPair.SetBoth(vnStore.VNForExpr(compCurBB, type));
                    break;
                }
            }
        }

#if DEBUG
        if (verbose && (tree._vnPair.Liberal != ValueNumStore.NoVN))
        {
            var indent = default(IndentStack);
            jitprintf($"N{tree._seqNum:D3} ");
            printTreeId(tree);
            jitprintf(" ");
            gtDispNodeName(tree);
            if (oper.IsLocalStore)
            {
                gtDispLocal(tree.AsLclVarCommon(), ref indent);
            }
            else if (oper.IsLeaf)
            {
                gtDispLeaf(tree, ref indent);
            }
            jitprintf(" => ");
            vnpPrint(tree._vnPair, 1);
            jitprintf("\n");
        }
#endif
    }
}
