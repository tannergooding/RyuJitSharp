// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private void ContainCheckRange(LIR.ReadOnlyRange range)
    {
        foreach (var node in range)
        {
            ContainCheckNode(node);
        }
    }

    private void ContainCheckRange(GenTree firstNode, GenTree lastNode)
    {
        ContainCheckRange(new LIR.ReadOnlyRange(firstNode, lastNode));
    }

    private void ContainCheckNode(GenTree node)
    {
#if TARGET_XARCH
        switch (node.Oper)
        {
            case GT_STORE_LCL_VAR:
            case GT_STORE_LCL_FLD:
            {
                ContainCheckStoreLoc(node.AsLclVarCommon());
                break;
            }

            case GT_ADD:
            case GT_SUB:
            case GT_AND:
            case GT_OR:
            case GT_XOR:
#if !TARGET_64BIT
            case GT_ADD_LO:
            case GT_ADD_HI:
            case GT_SUB_LO:
            case GT_SUB_HI:
#endif
            {
                ContainCheckBinary(node.AsOp());
                break;
            }

            case GT_MUL:
            case GT_MULHI:
#if TARGET_X86
            case GT_MUL_LONG:
#endif
            {
                ContainCheckMul(node.AsOp());
                break;
            }

            case GT_LSH:
            case GT_RSH:
            case GT_RSZ:
            case GT_ROL:
            case GT_ROR:
#if !TARGET_64BIT
            case GT_LSH_HI:
            case GT_RSH_LO:
#endif
            {
                ContainCheckShiftRotate(node.AsOp());
                break;
            }

            case GT_CAST:
            {
                ContainCheckCast(node.AsCast());
                break;
            }

            case GT_BITCAST:
            {
                ContainCheckBitCast(node.AsUnOp());
                break;
            }

            case GT_LCLHEAP:
            {
                ContainCheckLclHeap(node.AsUnOp());
                break;
            }

            case GT_RETURN:
            {
                ContainCheckRet(node.AsUnOp());
                break;
            }

            case GT_RETURNTRAP:
            {
                if (node.AsUnOp().Op1.Oper is GT_IND or GT_STOREIND)
                {
                    MakeSrcContained(node, node.AsUnOp().Op1);
                }
                break;
            }

            case GT_STOREIND:
            {
                ContainCheckStoreIndir(node.AsStoreInd());
                break;
            }

            case GT_IND:
            {
                ContainCheckIndir(node.AsIndir());
                break;
            }

            case GT_PUTARG_REG:
            case GT_PUTARG_STK:
            {
                assert(node.RegNum is not REG_NA);
                break;
            }

            case GT_EQ:
            case GT_NE:
            case GT_LT:
            case GT_LE:
            case GT_GE:
            case GT_GT:
            case GT_TEST_EQ:
            case GT_TEST_NE:
            case GT_CMP:
            case GT_TEST:
            case GT_JCMP:
            {
                ContainCheckCompare(node.AsOp());
                break;
            }

            case GT_SELECT:
            {
                ContainCheckSelect(node.AsConditional());
                break;
            }

            case GT_DIV:
            case GT_MOD:
            case GT_UDIV:
            case GT_UMOD:
            {
                ContainCheckDivOrMod(node.AsOp());
                break;
            }

            case GT_INTRINSIC:
            {
                ContainCheckIntrinsic(node.AsIntrinsic());
                break;
            }

            case GT_NONLOCAL_JMP:
            {
                ContainCheckNonLocalJmp(node.AsUnOp());
                break;
            }

#if FEATURE_HW_INTRINSICS
            case GT_HWINTRINSIC:
            {
                ContainCheckHWIntrinsic(node.AsHWIntrinsic());
                break;
            }
#endif
        }
#else
        throw new NotImplementedException("Non-xarch containment traversal is not ported.");
#endif
    }

    public void LowerRange(BasicBlock block, LIR.ReadOnlyRange range)
    {
        // Out-of-phase lowering uses separate scratch state, just as native lower.h does.
        var lowerer = new Lowering(CompilerInstance, _regAlloc) { _block = block };
        lowerer.LowerRange(range.FirstNode, range.LastNode);
    }

    private void LowerRange(GenTree? firstNode, GenTree? lastNode)
    {
        if (lastNode is null)
        {
            throw new ArgumentException("The range to lower must not be empty.");
        }

        // LowerNode may remove or replace the last node, so capture its successor first.
        var stopNode = lastNode.Next;
        for (var current = firstNode; current != stopNode;)
        {
            assert(current is not null);
            current = LowerNode(current);
        }
    }

    private GenTree? LowerNode(GenTree node)
    {
#if TARGET_XARCH
        switch (node.Oper)
        {
            case GT_LCL_FLD:
            {
                VerifyLclFldDoNotEnregister(node.AsLclVarCommon().LclNum);
                break;
            }

            case GT_LCL_ADDR:
            {
                var localNumber = node.AsLclVarCommon().LclNum;
                if (!CompilerInstance.lvaGetDesc(localNumber).lvDoNotEnregister)
                {
                    CompilerInstance.lvaSetVarDoNotEnregister(localNumber, DoNotEnregisterReason.LclAddrNode);
                }
                break;
            }

            case GT_KEEPALIVE:
            {
                node.AsUnOp().Op1.IsRegOptional = true;
                break;
            }

            case GT_PATCHPOINT:
            case GT_PATCHPOINT_FORCED:
            {
                RequireOutgoingArgSpace(node, MIN_ARG_AREA_FOR_CALL);
                break;
            }

            case GT_LSH:
            case GT_RSH:
            case GT_RSZ:
            {
                var next = node.Next;
                if (CompilerInstance.opts.OptimizationEnabled && TryFoldBinop(node.AsOp()))
                {
                    return next;
                }
                LowerShift(node.AsOp());
                break;
            }

            case GT_ROL:
            case GT_ROR:
            {
                var next = node.Next;
                if (CompilerInstance.opts.OptimizationEnabled && TryFoldBinop(node.AsOp()))
                {
                    return next;
                }
                TryRemoveShiftRotateMask(node.AsOp());
                LowerRotate(node);
                break;
            }

            case GT_NEG:
            case GT_NOT:
            {
                break;
            }

            case GT_BITCAST:
            {
                var next = node.Next;
                if (!TryRemoveBitCast(node.AsUnOp()))
                {
                    ContainCheckBitCast(node.AsUnOp());
                }
                return next;
            }

            case GT_CAST:
            {
                var next = node.Next;
                if (!TryRemoveCast(node.AsCast()))
                {
                    LowerCast(node.AsCast());
                }
                return next;
            }

            case GT_AND:
            case GT_OR:
            case GT_XOR:
            case GT_SUB:
            {
                if (CompilerInstance.opts.OptimizationEnabled)
                {
                    if ((node.Oper is GT_AND) && TryLowerAndNegativeOne(node.AsOp(), out var nextNode))
                    {
                        return nextNode;
                    }

                    var next = node.Next;
                    if ((node.Oper is GT_AND or GT_OR or GT_XOR) && TryFoldBinop(node.AsOp()))
                    {
                        return next;
                    }
                }

                return LowerBinaryArithmetic(node.AsOp());
            }

            case GT_ADD:
            {
                var next = LowerAdd(node.AsOp());
                if (next is not null)
                {
                    return next;
                }
                break;
            }

            case GT_MUL:
            case GT_MULHI:
            {
                return LowerMul(node.AsOp());
            }

            case GT_DIV:
            case GT_MOD:
            {
                return LowerSignedDivOrMod(node.AsOp());
            }

            case GT_UDIV:
            case GT_UMOD:
            {
                return LowerUnsignedDivOrMod(node.AsOp());
            }

            case GT_SWITCH:
            {
                return LowerSwitch(node);
            }

            case GT_CALL:
            {
                var next = LowerCall(node);
                if (next is not null)
                {
                    return next;
                }
                break;
            }

            case GT_LT:
            case GT_LE:
            case GT_GT:
            case GT_GE:
            case GT_EQ:
            case GT_NE:
            case GT_TEST_EQ:
            case GT_TEST_NE:
            case GT_CMP:
            {
                return LowerCompare(node);
            }

            case GT_BOUNDS_CHECK:
            {
                ContainCheckBoundsChk(node.AsBoundsChk());
                break;
            }

            case GT_XADD:
            {
                if (node.IsUnusedValue)
                {
                    node.IsUnusedValue = false;
                    // Codegen uses the data operand's type once the result becomes void.
                    assert(node.AsOp().Op2.Type.ActualType == node.Type);
                    node.SetOper(GT_LOCKADD);
                    node.Type = TYP_VOID;
                    _ = CheckImmedAndMakeContained(node, node.AsOp().Op2);
                }
                break;
            }

            case GT_JTRUE:
            {
                return LowerJTrue(node.AsUnOp());
            }

            case GT_JMP:
            {
                LowerJmpMethod(node);
                break;
            }

            case GT_NONLOCAL_JMP:
            {
                ContainCheckNonLocalJmp(node.AsUnOp());
                break;
            }

            case GT_SELECT:
            {
                return LowerSelect(node.AsConditional());
            }

            case GT_SELECTCC:
            {
                ContainCheckSelect(node.AsOpCC());
                break;
            }

            case GT_INTRINSIC:
            {
                ContainCheckIntrinsic(node.AsIntrinsic());
                break;
            }

#if FEATURE_HW_INTRINSICS
            case GT_BSWAP:
            case GT_BSWAP16:
            {
                LowerBswapOp(node.AsUnOp());
                break;
            }

            case GT_HWINTRINSIC:
            {
                return LowerHWIntrinsic(node.AsHWIntrinsic());
            }
#endif

            case GT_ARR_LENGTH:
            case GT_MDARR_LENGTH:
            case GT_MDARR_LOWER_BOUND:
            {
                return LowerArrLength(node.AsArrCommon());
            }

            case GT_RETURN:
            case GT_SWIFT_ERROR_RET:
            {
                LowerRet(node.AsUnOp());
                break;
            }

            case GT_RETURNTRAP:
            {
                ContainCheckNode(node);
                break;
            }

            case GT_ASYNC_CONTINUATION:
            {
                return LowerAsyncContinuation(node);
            }

            case GT_RETURN_SUSPEND:
            {
                LowerReturnSuspend(node);
                break;
            }

            case GT_PHI:
            case GT_PHI_ARG:
            {
                throw new InvalidOperationException("Phi nodes must be removed during rationalization.");
            }

            case GT_LCL_VAR:
            {
                var local = node.AsLclVar();
                WidenSIMD12IfNecessary(local);
                ref var descriptor = ref CompilerInstance.lvaGetDesc(local.LclNum);
                if (local.IsMultiRegLclVar &&
                    (!descriptor.lvPromoted ||
                        (CompilerInstance.lvaGetPromotionType(in descriptor) is not Compiler.lvaPromotionType.PROMOTION_TYPE_INDEPENDENT) ||
                        (descriptor.lvFieldCnt > MAX_MULTIREG_COUNT)))
                {
                    local.ClearMultiReg();
                    if (local.Type is TYP_STRUCT)
                    {
                        CompilerInstance.lvaSetVarDoNotEnregister(local.LclNum, DoNotEnregisterReason.BlockOp);
                    }
                }
                break;
            }

            case GT_STORE_LCL_VAR:
            {
                WidenSIMD12IfNecessary(node.AsLclVarCommon());
                return LowerStoreLocCommon(node.AsLclVarCommon());
            }

            case GT_STORE_LCL_FLD:
            {
                return LowerStoreLocCommon(node.AsLclVarCommon());
            }

            case GT_STORE_BLK:
            {
                var next = node.Next;
                if (node.AsBlk().Data.Oper is GT_CALL)
                {
                    LowerStoreSingleRegCallStruct(node.AsBlk());
                }
                else
                {
                    LowerBlockStoreCommon(node.AsBlk());
                }
                return next;
            }

            case GT_LCLHEAP:
            {
                return LowerLclHeap(node.AsUnOp());
            }

            case GT_STOREIND:
            {
                return LowerStoreIndirCommon(node.AsStoreInd());
            }

            case GT_IND:
            case GT_NULLCHECK:
            {
                return LowerIndir(node.AsIndir());
            }

            default:
            {
                break;
            }
        }

        return node.Next;
#else
        throw new NotImplementedException("Non-xarch node lowering is not ported.");
#endif
    }

#if TARGET_XARCH
    private void TryRemoveShiftRotateMask(GenTreeOp operation)
    {
        assert(operation.Oper is GT_LSH or GT_RSH or GT_RSZ or GT_ROL or GT_ROR);
        var mask = operation.Type is TYP_LONG ? 0x3fL : 0x1fL;
        var block = _block;
        assert(block is not null);

        for (var and = operation.Op2; and.Oper is GT_AND; and = and.AsOp().Op1)
        {
            var maskNode = and.AsOp().Op2;
            if (!maskNode.Oper.IsCnsIntOrI ||
                ((((long)maskNode.AsIntConCommon().IconValue) & mask) != mask))
            {
                break;
            }

            operation.Op2 = and.AsOp().Op1;
            block.Remove(and);
            block.Remove(maskNode);
            operation.Op2.IsContained = false;
        }
    }

    private void LowerShift(GenTreeOp shift)
    {
        assert(shift.Oper is GT_LSH or GT_RSH or GT_RSZ);
        TryRemoveShiftRotateMask(shift);
        ContainCheckShiftRotate(shift);
    }
#endif
}
