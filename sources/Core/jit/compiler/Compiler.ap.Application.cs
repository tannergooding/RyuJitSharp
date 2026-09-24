// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;
using static RyuJitSharp.Compiler.optOp1Kind;
using static RyuJitSharp.Compiler.optOp2Kind;
using static RyuJitSharp.GCInfo.WriteBarrierForm;

namespace RyuJitSharp;

public partial class Compiler
{
    public GenTree optAssertionProp_Update(GenTree newTree, GenTree tree, Statement? statement)
    {
        if (statement is null)
        {
            noway_assert(optLocalAssertionProp);
        }
        else
        {
            noway_assert(!optLocalAssertionProp);
            if (newTree != tree)
            {
                var link = gtFindLink(statement, tree);
                noway_assert(!Unsafe.IsNullRef(in link.result));
                var parent = link.parent;
                if (parent is not null)
                {
                    parent.ReplaceOperand(ref link.result, newTree);
                    if ((parent.Oper is GT_IND) && newTree.IsIconHandle())
                    {
                        var flags = newTree.AsIntCon().IconHandleFlag;
                        if (GenTree.HandleKindDataIsInvariant(flags))
                        {
                            parent.Flags |= GTF_IND_INVARIANT;
                            if (flags is GTF_ICON_STR_HDL)
                            {
                                parent.Flags |= GTF_IND_NONNULL;
                            }
                        }
                    }
                }
                else
                {
                    assert((statement.RootNode == tree) && Unsafe.AreSame(ref statement.RootNodeRef, ref link.result));
                    statement.RootNode = newTree;
                }

                // The current traversal only needs Next. Remorphing the statement
                // will rebuild both directions of its node threading.
                newTree.Next = tree.Next;
            }
        }

        optAssertionPropagated = true;
        optAssertionPropagatedCurrentStmt = true;
        return newTree;
    }

    public bool optZeroObjAssertionProp(ref GenTree tree, ASSERT_TP? assertions)
    {
        if (!optLocalAssertionProp || !tree.Oper.IsLocal || varTypeIsSimd(tree.Type))
        {
            return false;
        }

        var local = tree.AsLclVarCommon().LclNum;
        if (lvaGetDesc(local).IsAddressExposed)
        {
            return false;
        }

        assert(assertions is not null);
        var index = optLocalAssertionIsEqualOrNotEqual(O1K_LCLVAR, local, O2K_ZEROOBJ, 0, assertions);
        if (index == NO_ASSERTION_INDEX)
        {
            return false;
        }

        var assertion = optGetAssertion(index);
#if DEBUG
        assert(compCurBB is not null);
        JITDUMP($"\nZEROOBJ Assertion prop in {FMT_BB(compCurBB.bbNum)}:\n");
        if (verbose)
        {
            optPrintAssertion(assertion, index);
        }
#endif
        DISPNODE(tree);
        tree = tree.BashToZeroConst(TYP_INT);
        JITDUMP(" =>\n");
        DISPNODE(tree);
        return true;
    }

    public GenTree? optAssertionProp_Return(ASSERT_TP? assertions, GenTree tree, Statement? statement)
    {
        ref var value = ref (tree.Oper is GT_RETURN ? ref tree.AsUnOp().Op1Ref : ref tree.AsOp().Op2Ref);
        if ((tree.Type is not TYP_VOID) && varTypeIsStruct(value.Type) && !varTypeIsStruct(info.compRetNativeType) &&
            optZeroObjAssertionProp(ref value, assertions))
        {
            return optAssertionProp_Update(tree, tree, statement);
        }

        return null;
    }

    public GenTree? optAssertionProp_Ind(ASSERT_TP? assertions, GenTree tree, Statement? statement)
    {
        assert(tree.Oper.IsIndirOrArrMetaData);
        var updated = optNonNullAssertionProp_Ind(assertions, tree);
        if (tree.Oper is GT_STOREIND)
        {
            updated |= optWriteBarrierAssertionProp_StoreInd(assertions, tree.AsStoreInd());
        }

        return updated ? optAssertionProp_Update(tree, tree, statement) : null;
    }

    public GenTree? optNonNullAssertionProp_Call(ASSERT_TP? assertions, GenTreeCall call)
    {
        if (!call.NeedsNullCheck)
        {
            return null;
        }

        var thisArg = call.Args.ThisArg;
        noway_assert(thisArg is not null);
        var operand = thisArg.Node;
        noway_assert(operand is not null);
        if (optAssertionIsNonNull(operand, assertions))
        {
#if DEBUG
            assert(compCurBB is not null);
            JITDUMP($"Non-null assertion prop for tree [{operand.TreeId:D6}] in {FMT_BB(compCurBB.bbNum)}:\n");
#endif
            call.Flags &= ~(GTF_CALL_NULLCHECK | GTF_EXCEPT);
            noway_assert((call.Flags & GTF_SIDE_EFFECT) != 0);
            return call;
        }

        return null;
    }

    public bool optNonNullAssertionProp_Ind(ASSERT_TP? assertions, GenTree indir)
    {
        assert(indir.Oper.IsIndirOrArrMetaData);
        if ((indir.Flags & GTF_EXCEPT) == 0)
        {
            return false;
        }

        if (optAssertionIsNonNull(indir.IndirOrArrMetaDataAddr, assertions))
        {
#if DEBUG
            assert(compCurBB is not null);
            JITDUMP($"Non-null assertion prop for indirection [{indir.TreeId:D6}] in {FMT_BB(compCurBB.bbNum)}:\n");
#endif
            indir.Flags &= ~GTF_EXCEPT;
            indir.Flags |= GTF_IND_NONFAULTING;
            indir.HasOrderingSideEffect = true;
            return true;
        }

        return false;
    }

    private GCInfo.WriteBarrierForm GetWriteBarrierForm(ValueNum vn)
    {
        assert(vnStore is not null);
        var type = vnStore.TypeOfVN(vn);
        if (type is TYP_REF)
        {
            return WBF_BarrierUnchecked;
        }

        if (type is not TYP_BYREF)
        {
            return WBF_BarrierUnknown;
        }

        var app = new VNFuncApp();
        if (vnStore.GetVNFunc(vnStore.VNNormalValue(vn), ref app))
        {
            if (app.Func is VNF_PtrToArrElem)
            {
                return GetWriteBarrierForm(app.GetArg(1));
            }

            if (app.Func is VNF_PtrToLoc)
            {
                return WBF_NoBarrier;
            }

            if ((app.Func is VNF_PtrToStatic) && vnStore.IsVNHandle(app.GetArg(0), GTF_ICON_STATIC_BOX_PTR))
            {
                return WBF_BarrierUnchecked;
            }

            if (app.Func is VNF_ADD)
            {
                // A variable offset can bridge the stack and heap, so only
                // constant nonhandle offsets preserve the base's barrier form.
                if (vnStore.IsVNConstantNonHandle(app.GetArg(0)))
                {
                    return GetWriteBarrierForm(app.GetArg(1));
                }

                if (vnStore.IsVNConstantNonHandle(app.GetArg(1)))
                {
                    return GetWriteBarrierForm(app.GetArg(0));
                }
            }
        }

        return WBF_BarrierUnknown;
    }

    public bool optWriteBarrierAssertionProp_StoreInd(ASSERT_TP? assertions, GenTreeStoreInd indir)
    {
        var value = indir.Data;
        var address = indir.Addr;
        if (optLocalAssertionProp || (indir.Type is not TYP_REF) || (value.Type is not TYP_REF) ||
            ((indir.Flags & GTF_IND_TGT_NOT_HEAP) != 0))
        {
            return false;
        }

        assert(vnStore is not null);
        var form = WBF_BarrierUnknown;
        var result = vnStore.VNVisitReachingVNs(optConservativeNormalVN(value), vn =>
            ((vn == ValueNumStore.VNForNull()) || vnStore.IsVNObjHandle(vn))
                ? ValueNumStore.VNVisit.Continue : ValueNumStore.VNVisit.Abort);
        if (result is ValueNumStore.VNVisit.Continue)
        {
            form = WBF_NoBarrier;
        }
        else if ((indir.Flags & GTF_IND_TGT_HEAP) == 0)
        {
            // Reclassifying known heap stores would cost throughput upstream.
            form = GetWriteBarrierForm(optConservativeNormalVN(address));
        }

#if DEBUG
        JITDUMP($"Trying to determine the exact type of write barrier for STOREIND [{indir.TreeId}06]: ");
#endif
        if (form is WBF_NoBarrier)
        {
            JITDUMP("is not needed at all.\n");
            indir.Flags |= GTF_IND_TGT_NOT_HEAP;
            return true;
        }

        if (form is WBF_BarrierUnchecked)
        {
            JITDUMP("unchecked is fine.\n");
            indir.Flags |= GTF_IND_TGT_HEAP;
            return true;
        }

        JITDUMP("unknown (checked).\n");
        return false;
    }

    public bool optWriteBarrierAssertionProp_StoreBlk(ASSERT_TP? assertions, GenTreeBlk store)
    {
        if (optLocalAssertionProp || !store.Layout.HasGCPtr ||
            ((store.Flags & (GTF_IND_TGT_NOT_HEAP | GTF_IND_TGT_HEAP)) != 0))
        {
            return false;
        }

        var form = GetWriteBarrierForm(optConservativeNormalVN(store.Addr));
        if (form is WBF_NoBarrier)
        {
#if DEBUG
            JITDUMP($"Add GTF_IND_TGT_NOT_HEAP to STORE_BLK [{store.TreeId:D6}]: ");
#endif
            store.Flags |= GTF_IND_TGT_NOT_HEAP;
            return true;
        }

        if (form is WBF_BarrierUnchecked)
        {
#if DEBUG
            JITDUMP($"Add GTF_IND_TGT_HEAP to STORE_BLK [{store.TreeId:D6}]: ");
#endif
            store.Flags |= GTF_IND_TGT_HEAP;
            return true;
        }

        return false;
    }

#if FEATURE_HW_INTRINSICS
    public void optAssertionProp_HWIntrinsic(GenTreeHWIntrinsic tree)
    {
        assert(vnStore is not null);
        if (tree.HWIntrinsicId is not NI_Vector_ExtractMostSignificantBits)
        {
            return;
        }

        assert(tree.Operands.Length == 1);
        var operand = tree.GetOp(1);
        if (operand.Oper is not GT_LCL_VAR)
        {
            return;
        }

        var local = operand.AsLclVar().LclNum;
        if (!lvaGetDesc(local).lvSingleDef)
        {
            return;
        }

        var vn = vnStore.VNNormalValue(operand._vnPair.Conservative);
        var result = vnStore.VNVisitReachingVNs(vn, reachingVN => {
            if (reachingVN == ValueNumStore.NoVN)
            {
                return ValueNumStore.VNVisit.Abort;
            }

            reachingVN = vnStore.VNNormalValue(reachingVN);
            var type = vnStore.TypeOfVN(reachingVN);
            var size = tree.SimdSize;
            if (!varTypeIsSimd(type) || (type.Size != size))
            {
                return ValueNumStore.VNVisit.Abort;
            }

            return vnStore.IsVectorPerElementMask(reachingVN, tree.SimdBaseType, size)
                ? ValueNumStore.VNVisit.Continue : ValueNumStore.VNVisit.Abort;
        });
        if (result is ValueNumStore.VNVisit.Continue)
        {
            lvaGetDesc(local).SetIsVectorPerElementMask(tree.SimdBaseType);
        }
    }
#endif
}
