// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
#if TARGET_XARCH
    private bool IsBinOpInRMWStoreInd(GenTreeOp tree)
    {
        assert(!varTypeIsFloating(tree.Type));
        assert(tree.Oper.IsBinary);
        if ((tree.Op1.Oper is not GT_IND) && (tree.Op2.Oper is not GT_IND))
        {
            return false;
        }
        if (!BlockRange().TryGetUse(tree, out var use) || (use.User().Oper is not GT_STOREIND) ||
            (use.User().AsStoreInd().Data != tree))
        {
            return false;
        }

        return IsRMWMemOpRootedAtStoreInd(use.User().AsStoreInd(), out _, out _);
    }

    private bool IsRMWIndirCandidate(GenTree operand, GenTreeStoreInd storeInd)
    {
        if (operand.Oper is not GT_IND)
        {
            return false;
        }
        if ((operand.AsIndir().Addr.Oper != storeInd.Addr.Oper) || !IndirsAreEquivalent(operand, storeInd))
        {
            return false;
        }

        // Every node in the load's address tree must be movable to the store, not just the load.
        _scratchSideEffects.Clear();
        assert((operand._lirFlags & LIR.Flags.Mark) == 0);
        operand._lirFlags |= LIR.Flags.Mark;
        var markCount = 1;
        for (var node = storeInd.Prev; markCount > 0; node = node.Prev)
        {
            assert(node is not null);
            if ((node._lirFlags & LIR.Flags.Mark) == 0)
            {
                _scratchSideEffects.AddNode(CompilerInstance, node);
            }
            else
            {
                node._lirFlags &= ~LIR.Flags.Mark;
                markCount--;
                if (_scratchSideEffects.InterferesWith(CompilerInstance, node, false))
                {
                    for (; markCount > 0; node = node.Prev)
                    {
                        assert(node is not null);
                        if ((node._lirFlags & LIR.Flags.Mark) != 0)
                        {
                            node._lirFlags &= ~LIR.Flags.Mark;
                            markCount--;
                        }
                    }
                    return false;
                }

                foreach (var nodeOperand in node.Operands)
                {
                    assert((nodeOperand._lirFlags & LIR.Flags.Mark) == 0);
                    nodeOperand._lirFlags |= LIR.Flags.Mark;
                    markCount++;
                }
            }
        }

        return true;
    }

    private bool IsRMWMemOpRootedAtStoreInd(
        GenTreeStoreInd storeInd, out GenTree? indirCandidate, out GenTree? indirOpSource)
    {
        assert(!varTypeIsFloating(storeInd.Type));
        indirCandidate = null;
        indirOpSource = null;
        if (storeInd.RmwStatus is STOREIND_RMW_UNSUPPORTED_ADDR or STOREIND_RMW_UNSUPPORTED_OPER or
            STOREIND_RMW_UNSUPPORTED_TYPE or STOREIND_RMW_INDIR_UNEQUAL)
        {
            return false;
        }

        var indirDst = storeInd.Addr;
        var indirSrc = storeInd.Data;
        var oper = indirSrc.Oper;
        if (storeInd.RmwStatus is STOREIND_RMW_DST_IS_OP1 or STOREIND_RMW_DST_IS_OP2)
        {
            if (oper.IsBinary)
            {
                var binOp = indirSrc.AsOp();
                var first = storeInd.RmwStatus is STOREIND_RMW_DST_IS_OP1;
                indirCandidate = first ? binOp.Op1 : binOp.Op2;
                indirOpSource = first ? binOp.Op2 : binOp.Op1;
            }
            else
            {
                assert(oper.IsUnary);
                indirCandidate = indirSrc.AsUnOp().Op1;
                indirOpSource = indirCandidate;
            }
            assert(IndirsAreEquivalent(indirCandidate, storeInd));
            return true;
        }
        assert(storeInd.RmwStatus is STOREIND_RMW_STATUS_UNKNOWN);

        if ((indirDst.Oper is not (GT_LEA or GT_LCL_VAR or GT_CNS_INT)) && !indirDst.IsLclVarAddr)
        {
            storeInd.RmwStatus = STOREIND_RMW_UNSUPPORTED_ADDR;
            return false;
        }
        // An overflow check must succeed before the target is modified.
        if (indirSrc.HasOverflowCheckEx)
        {
            storeInd.RmwStatus = STOREIND_RMW_UNSUPPORTED_OPER;
            return false;
        }

        GenTree candidate;
        GenTree source;
        RmwStatus status;
        if (oper.IsBinary)
        {
            if (!oper.IsRmwMemOp)
            {
                storeInd.RmwStatus = STOREIND_RMW_UNSUPPORTED_OPER;
                return false;
            }
            if (oper.IsShiftOrRotate && varTypeIsSmall(storeInd.Type))
            {
                // Shifting the narrow memory value would lose the load's extension bits.
                storeInd.RmwStatus = STOREIND_RMW_UNSUPPORTED_TYPE;
                return false;
            }

            var binOp = indirSrc.AsOp();
            if (oper.IsCommutative && IsRMWIndirCandidate(binOp.Op2, storeInd))
            {
                candidate = binOp.Op2;
                source = binOp.Op1;
                status = STOREIND_RMW_DST_IS_OP2;
            }
            else if (IsRMWIndirCandidate(binOp.Op1, storeInd))
            {
                candidate = binOp.Op1;
                source = binOp.Op2;
                status = STOREIND_RMW_DST_IS_OP1;
            }
            else
            {
                storeInd.RmwStatus = STOREIND_RMW_UNSUPPORTED_ADDR;
                return false;
            }
        }
        else if (oper.IsUnary)
        {
            if (oper is not (GT_NOT or GT_NEG))
            {
                storeInd.RmwStatus = STOREIND_RMW_UNSUPPORTED_OPER;
                return false;
            }
            var operand = indirSrc.AsUnOp().Op1;
            if ((operand.Oper is not GT_IND) || !IsRMWIndirCandidate(operand, storeInd))
            {
                storeInd.RmwStatus = STOREIND_RMW_UNSUPPORTED_ADDR;
                return false;
            }
            candidate = source = operand;
            status = STOREIND_RMW_DST_IS_OP1;
        }
        else
        {
            storeInd.RmwStatus = STOREIND_RMW_UNSUPPORTED_OPER;
            return false;
        }

        if (!IsSafeToContainMem(storeInd, indirDst))
        {
            storeInd.RmwStatus = STOREIND_RMW_UNSUPPORTED_ADDR;
            return false;
        }

        indirCandidate = candidate;
        indirOpSource = source;
        storeInd.RmwStatus = status;
        return true;
    }

#endif

    public static bool IndirsAreEquivalent(GenTree candidate, GenTree storeInd)
    {
        assert(candidate.Oper is GT_IND);
        assert(storeInd.Oper is GT_STOREIND);
        // Signedness may differ, but a width difference can represent a cast that cannot be dropped.
        if (candidate.Type.Size != storeInd.Type.Size)
        {
            return false;
        }
        var first = candidate.AsIndir().Addr.SkipCopyOrReload;
        var second = storeInd.AsIndir().Addr.SkipCopyOrReload;
        if (first.Oper != second.Oper)
        {
            return false;
        }

        switch (first.Oper)
        {
            case GT_LCL_ADDR:
            {
                if (first.AsLclFld().LclOffs != 0)
                {
                    return false;
                }
                return NodesAreEquivalentLeaves(first, second);
            }

            case GT_LCL_VAR:
            case GT_CNS_INT:
            {
                return NodesAreEquivalentLeaves(first, second);
            }

            case GT_LEA:
            {
                var firstAddr = first.AsAddrMode();
                var secondAddr = second.AsAddrMode();
                return NodesAreEquivalentLeaves(firstAddr.BaseAddress, secondAddr.BaseAddress) &&
                    NodesAreEquivalentLeaves(firstAddr.Index, secondAddr.Index) &&
                    (firstAddr.Scale == secondAddr.Scale) && (firstAddr.Offset == secondAddr.Offset);
            }

            default:
            {
                return false;
            }
        }
    }

    private static bool NodesAreEquivalentLeaves(GenTree? first, GenTree? second)
    {
        if (first == second)
        {
            return true;
        }
        if ((first is null) || (second is null))
        {
            return false;
        }

        first = first.SkipCopyOrReload;
        second = second.SkipCopyOrReload;
        if ((first.Type != second.Type) || (first.Oper != second.Oper) ||
            !first.Oper.IsLeaf || !second.Oper.IsLeaf)
        {
            return false;
        }

        return first.Oper switch {
            GT_CNS_INT => (first.AsIntCon().IconValue == second.AsIntCon().IconValue) &&
                (first.IsIconHandle() == second.IsIconHandle()),
            GT_LCL_ADDR => (first.AsLclFld().LclOffs == second.AsLclFld().LclOffs) &&
                (first.AsLclVarCommon().LclNum == second.AsLclVarCommon().LclNum),
            GT_LCL_VAR => first.AsLclVarCommon().LclNum == second.AsLclVarCommon().LclNum,
            _ => false,
        };
    }
}
