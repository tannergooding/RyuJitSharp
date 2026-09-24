// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.

namespace RyuJitSharp;

public partial class Compiler
{
    public unsafe GenTree fgMorphTree(GenTree tree)
    {
        // Constant-operand reordering below is not valid during CSE.
        assert(!optValnumCSE_phase);
        tree.ClearMorphed();
        var thisMorphNum = 0;

#if DEBUG
        if (verbose && (unchecked((uint)JitConfig.JitBreakMorphTree) == (uint)tree.TreeId))
        {
            NO_WAY("JitBreakMorphTree hit");
        }

        if (verbose && treesBeforeAfterMorph)
        {
            thisMorphNum = morphNum++;
            jitprintf($"\nfgMorphTree (before {thisMorphNum}):\n");
            gtDispTree(tree);
        }

        if (compStressCompile(STRESS_GENERIC_CHECK, 0))
        {
            tree = tree.CloneForMorphStress(this);
        }
#endif

        var assertionPropDone = false;

        if (fgGlobalMorph && optLocalAssertionProp && (optAssertionCount > 0))
        {
            assert(apLocal is not null);
            var newTree = tree;
            while (newTree is not null)
            {
                tree = newTree;
                newTree = optLocalAssertionPropTree(apLocal, tree);
            }
        }

        if (tree.Oper.IsConst)
        {
            tree = fgMorphConst(tree);
        }
        else if (tree.Oper.IsLeaf)
        {
            tree = fgMorphLeaf(tree);
        }
        else if (tree.Oper.IsSimple)
        {
            tree = fgMorphSmpOp(tree, out assertionPropDone);
        }
        else
        {
            switch (tree.Oper)
            {
                case GT_CALL:
                {
                    if (tree.MayThrow(this))
                    {
                        tree.Flags |= GTF_EXCEPT;
                    }
                    else
                    {
                        tree.Flags &= ~GTF_EXCEPT;
                    }

                    tree = fgMorphCall(tree.AsCall());
                    break;
                }

#if FEATURE_HW_INTRINSICS
                case GT_HWINTRINSIC:
                {
                    tree = fgMorphHWIntrinsic(tree.AsHWIntrinsic());
                    break;
                }
#endif

                case GT_ARR_ELEM:
                {
                    var array = tree.AsArrElem();
                    array.ArrObj = fgMorphTree(array.ArrObj);
                    for (var i = 0; i < array.ArrRank; i++)
                    {
                        array.ArrInds[i] = fgMorphTree(array.ArrInds[i]);
                    }

                    tree.Flags &= ~GTF_CALL;
                    tree.Flags |= array.ArrObj.Flags & GTF_ALL_EFFECT;
                    foreach (var index in array.ArrInds)
                    {
                        tree.Flags |= index.Flags & GTF_ALL_EFFECT;
                    }

                    break;
                }

                case GT_PHI:
                {
                    tree.Flags &= ~GTF_ALL_EFFECT;
                    foreach (var use in tree.AsPhi().Uses)
                    {
                        use.Node = fgMorphTree(use.Node);
                        tree.Flags |= use.Node.Flags & GTF_ALL_EFFECT;
                    }

                    break;
                }

                case GT_FIELD_LIST:
                {
                    tree.Flags &= ~GTF_ALL_EFFECT;
                    foreach (var use in tree.AsFieldList().Uses)
                    {
                        use.Node = fgMorphTree(use.Node);
                        tree.Flags |= use.Node.Flags & GTF_ALL_EFFECT;

                        // A whole promoted SIMD operand requires dependent promotion.
                        // Other promoted structs remain legal field-list operands.
                        var operand = use.Node;
                        if ((operand.Oper is GT_LCL_VAR) && varTypeIsSimd(operand.Type) &&
                            lvaGetDesc(operand.AsLclVar().LclNum).lvPromoted)
                        {
                            lvaSetVarDoNotEnregister(operand.AsLclVar().LclNum, DoNotEnregisterReason.simdUserForcesDep);
                        }
                    }

                    break;
                }

                case GT_CMPXCHG:
                {
                    var cmpXchg = tree.AsCmpXchg();
                    cmpXchg.Addr = fgMorphTree(cmpXchg.Addr);
                    cmpXchg.DataRef = fgMorphTree(cmpXchg.Data);
                    cmpXchg.Comparand = fgMorphTree(cmpXchg.Comparand);
                    gtUpdateNodeSideEffects(tree);
                    break;
                }

                case GT_SELECT:
                {
                    var select = tree.AsConditional();
                    select.CondRef = fgMorphTree(select.Cond);
                    select.Op1 = fgMorphTree(select.Op1);
                    select.Op2 = fgMorphTree(select.Op2);
                    tree.Flags &= ~(GTF_EXCEPT | GTF_CALL);
                    tree.Flags |= (select.Cond.Flags | select.Op1.Flags | select.Op2.Flags) & GTF_ALL_EFFECT;
                    tree = gtFoldExpr(tree);
                    break;
                }

                default:
                {
#if DEBUG
                    gtDispTree(tree);
#endif
                    NO_WAY("unexpected operator");
                    break;
                }
            }
        }

        fgMorphTreeDone(tree, assertionPropDone, thisMorphNum);
        return tree;
    }

    private unsafe GenTree fgMorphConst(GenTree tree)
    {
        assert(tree.Oper.IsConst);
        tree.Flags &= ~(GTF_ALL_EFFECT | GTF_REVERSE_OPS);

        if (tree.Oper is not GT_CNS_STR)
        {
            return tree;
        }

        var literal = tree.AsStrCon();
        void* value;
        InfoAccessType accessType;

        if (literal.IsStringEmptyField)
        {
            accessType = info.compCompHnd->emptyStringLiteral(&value);
        }
        else
        {
            accessType = info.compCompHnd->constructStringLiteral(literal.ScpHnd, literal.SconCpx, &value);
        }

        return fgMorphTree(gtNewStringLiteralNode(accessType, value));
    }

    private unsafe GenTree fgMorphLeaf(GenTree tree)
    {
        assert(tree.Oper.IsLeaf);

        if (tree.Oper is GT_LCL_VAR or GT_LCL_FLD or GT_LCL_ADDR)
        {
            tree = fgMorphLeafLocal(tree.AsLclVarCommon());
        }
        else if (tree.Oper is GT_FTN_ADDR)
        {
            var function = tree.AsFptrVal();
            var method = function.FptrMethod;
            CORINFO_CONST_LOOKUP address;

#if FEATURE_READYTORUN
            if (function.EntryPoint.addr is not null)
            {
                address = function.EntryPoint;
            }
            else
#endif
            {
                info.compCompHnd->getFunctionFixedEntryPoint(method, !function.FptrDelegateTarget, &address);
            }

            GenTree? indirection = null;
            switch (address.accessType)
            {
                case IAT_PPVALUE:
                {
                    indirection = gtNewIndOfIconHandleNode(TYP_I_IMPL, (nint)address.handle, GTF_ICON_CONST_PTR);
                    indirection = gtNewIndir(TYP_I_IMPL, indirection, GTF_IND_NONFAULTING | GTF_IND_INVARIANT);
                    break;
                }

                case IAT_PVALUE:
                {
                    indirection = gtNewIndOfIconHandleNode(TYP_I_IMPL, (nint)address.handle, GTF_ICON_FTN_ADDR);
#if DEBUG
                    indirection.AsUnOp().Op1.AsIntCon().TargetHandle = (nint)method;
#endif
                    break;
                }

                case IAT_VALUE:
                {
                    var constant = new GenTreeIntCon(tree.Type, (nint)address.handle, fields: null, tree) {
                        Flags = tree.Flags | GTF_ICON_FTN_ADDR,
                    };
                    constant._vnPair.SetBoth(ValueNumStore.NoVN);
                    fgUpdateConstTreeValueNumber(constant);
#if DEBUG
                    constant.TargetHandle = (nint)method;
#endif
                    tree = constant;
                    break;
                }

                default:
                {
                    NO_WAY("Unknown addrInfo.accessType");
                    break;
                }
            }

            if (indirection is not null)
            {
                tree = fgMorphTree(indirection);
            }
        }

        return tree;
    }

    private unsafe GenTree fgMorphLeafLocal(GenTreeLclVarCommon local)
    {
        assert(local.Oper is GT_LCL_VAR or GT_LCL_FLD or GT_LCL_ADDR);
        var expanded = fgMorphExpandLocal(local);

        if (expanded is not null)
        {
            return fgMorphTree(expanded);
        }

        if (local.Oper is GT_LCL_ADDR)
        {
            return local;
        }

        ref var descriptor = ref lvaGetDesc(local.LclNum);
        // Copy-omission candidates can still use assertions, but must not reorder
        // across the call that will expose their address.
        if (descriptor.IsAddressExposed
#if FEATURE_IMPLICIT_BYREFS
            || descriptor.lvIsLastUseCopyOmissionCandidate
#endif
        )
        {
            local.Flags |= GTF_GLOB_REF;
        }

        if (fgGlobalMorph && (local.Oper is GT_LCL_VAR) && descriptor.lvNormalizeOnLoad && local.CanCse)
        {
            var localType = descriptor.Type;
            if (optLocalAssertionProp)
            {
                assert(apLocal is not null);
                if (optAssertionIsSubrange(local, IntegralRange.ForType(localType), apLocal) != NO_ASSERTION_INDEX)
                {
                    // Memory uses still need the small type even when assertions
                    // guarantee that a register value is already normalized.
                    assert(local.Type == localType);
                    return local;
                }
            }

            local.Type = TYP_INT;
            fgMorphTreeDone(local);
            var cast = gtNewCastNode(TYP_INT, local, fromUnsigned: false, localType);
            fgMorphTreeDone(cast);
            return cast;
        }

        return local;
    }
}
