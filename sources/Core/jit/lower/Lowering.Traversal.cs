// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed partial class Lowering
{
#if FEATURE_HW_INTRINSICS && TARGET_WASM
    private void ContainCheckHWIntrinsic(GenTreeHWIntrinsic node)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "WebAssembly hardware-intrinsic containment is not ported.");
    }
#endif

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
                ContainCheckReturnTrap(node.AsUnOp());
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

#if TARGET_XARCH
            case GT_INTRINSIC:
            {
                ContainCheckIntrinsic(node.AsIntrinsic());
                break;
            }
#endif

            case GT_NONLOCAL_JMP:
            {
                ContainCheckNonLocalJmp(node.AsUnOp());
                break;
            }

            case GT_PATCHPOINT:
            case GT_PATCHPOINT_FORCED:
            {
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
    }

    private void ContainCheckReturnTrap(GenTreeUnOp node)
    {
#if TARGET_XARCH
        assert(node.Oper is GT_RETURNTRAP);
        if (node.Op1.Oper is GT_IND or GT_STOREIND)
        {
            MakeSrcContained(node, node.Op1);
        }
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
        assert(node is not null);
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
#if TARGET_XARCH || TARGET_ARM64 || TARGET_LOONGARCH64 || TARGET_RISCV64 || TARGET_WASM
                LowerShift(node.AsOp());
#else
                ContainCheckShiftRotate(node.AsOp());
#endif
                break;
            }

#if !TARGET_64BIT
            case GT_LSH_HI:
            case GT_RSH_LO:
            {
                ContainCheckShiftRotate(node.AsOp());
                break;
            }
#endif

            case GT_ROL:
            case GT_ROR:
            {
                var next = node.Next;
                if (CompilerInstance.opts.OptimizationEnabled && TryFoldBinop(node.AsOp()))
                {
                    return next;
                }
#if TARGET_XARCH || TARGET_ARM64 || TARGET_LOONGARCH64 || TARGET_RISCV64 || TARGET_WASM
                TryRemoveShiftRotateMask(node.AsOp());
#endif
                LowerRotate(node);
                break;
            }

            case GT_NEG:
            {
#if TARGET_ARM64
                if (TryLowerNegToMulLongOp(node.AsUnOp(), out var next))
                {
                    return next;
                }

                ContainCheckNeg(node.AsUnOp());
#endif
#if TARGET_WASM
                return LowerNeg(node.AsUnOp());
#endif
                break;
            }

            case GT_NOT:
            {
#if TARGET_ARM64
                ContainCheckNot(node.AsUnOp());
#endif
                break;
            }

            case GT_BITCAST:
            {
                return LowerBitCast(node.AsUnOp());
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
#if !TARGET_64BIT
            case GT_ADD_LO:
            case GT_ADD_HI:
            case GT_SUB_LO:
            case GT_SUB_HI:
#endif
            case GT_SUB:
            {
                if (CompilerInstance.opts.OptimizationEnabled)
                {
                    if (node.Oper is GT_AND)
                    {
                        if (TryLowerAndNegativeOne(node.AsOp(), out var nextNode))
                        {
                            return nextNode;
                        }
                        assert(nextNode is null);
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
#if TARGET_X86 || TARGET_ARM64
            case GT_MUL_LONG:
#endif
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
#if TARGET_ARM64
                _ffrTrashed = true;
#endif
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

#if TARGET_XARCH || TARGET_ARM64 || TARGET_LOONGARCH64 || TARGET_RISCV64
            case GT_BOUNDS_CHECK:
            {
                ContainCheckBoundsChk(node.AsBoundsChk());
                break;
            }
#endif

#if TARGET_XARCH || TARGET_ARM64 || TARGET_LOONGARCH64 || TARGET_RISCV64
            case GT_XADD:
            {
#if TARGET_ARM64 || TARGET_LOONGARCH64 || TARGET_RISCV64
                _ = CheckImmedAndMakeContained(node, node.AsOp().Op2);
#else
                if (node.IsUnusedValue)
                {
                    node.IsUnusedValue = false;
                    // Codegen uses the data operand's type once the result becomes void.
                    assert(node.AsOp().Op2.Type.ActualType == node.Type);
                    node.SetOper(GT_LOCKADD);
                    node.Type = TYP_VOID;
                    _ = CheckImmedAndMakeContained(node, node.AsOp().Op2);
                }
#endif
                break;
            }

#if TARGET_ARM64 || TARGET_LOONGARCH64 || TARGET_RISCV64
            case GT_CMPXCHG:
            {
#if TARGET_RISCV64
                _ = CheckImmedAndMakeContained(node, node.AsCmpXchg().Data);
#endif
                _ = CheckImmedAndMakeContained(node, node.AsCmpXchg().Comparand);
                break;
            }

            case GT_XORR:
            case GT_XAND:
            case GT_XCHG:
            {
                _ = CheckImmedAndMakeContained(node, node.AsOp().Op2);
                break;
            }
#endif
#endif

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

#if TARGET_XARCH
            case GT_INTRINSIC:
            {
                ContainCheckIntrinsic(node.AsIntrinsic());
                break;
            }
#endif

#if FEATURE_HW_INTRINSICS && TARGET_XARCH
            case GT_BSWAP:
            case GT_BSWAP16:
            {
                LowerBswapOp(node.AsUnOp());
                break;
            }
#endif

#if FEATURE_HW_INTRINSICS
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
                ContainCheckReturnTrap(node.AsUnOp());
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

            case GT_LCL_VAR:
            {
                LowerLclVar(node.AsLclVar());
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
                return LowerStoreBlock(node.AsBlk());
            }

            case GT_LCLHEAP:
            {
                return LowerLclHeap(node.AsUnOp());
            }

            case GT_STOREIND:
            {
                return LowerStoreIndirCommon(node.AsStoreInd());
            }

#if FEATURE_HW_INTRINSICS && TARGET_ARM64
            case GT_CNS_MSK:
            {
                return LowerCnsMask(node.AsMskCon());
            }
#endif

            case GT_IND:
            case GT_NULLCHECK:
            {
                return LowerIndir(node.AsIndir());
            }

#if TARGET_WASM
            case GT_INDEX_ADDR:
            {
                LowerIndexAddr(node.AsIndexAddr());
                break;
            }

            case GT_CKFINITE:
            {
                LowerCkfinite(node.AsOp());
                break;
            }
#endif

            default:
            {
                break;
            }
        }

        return node.Next;
    }

    private void TryRemoveShiftRotateMask(GenTreeOp operation)
    {
        assert(operation.Oper is GT_LSH or GT_RSH or GT_RSZ or GT_ROL or GT_ROR);
#if LOWER_DECOMPOSE_LONGS
        assert(!varTypeIsLong(operation.Type));
        var mask = 0x1fL;
#else
        var mask = varTypeIsLong(operation.Type) ? 0x3fL : 0x1fL;
#endif
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
#if TARGET_ARM64
        if (CompilerInstance.opts.OptimizationEnabled && (shift.Oper is GT_LSH) &&
            (shift.Op1 is GenTreeCast cast) && shift.Op2.Oper.IsCnsIntOrI && !shift.IsContained)
        {
            var constant = shift.Op2.AsIntCon();
            if (!cast.IsContained && !cast.IsRegOptional && !cast.HasOverflowCheck &&
                (cast.CastOp.Type is TYP_LONG or TYP_INT))
            {
                var dstBits = cast.Type.Size * BITS_PER_BYTE;
                var srcBits = (varTypeIsSmall(cast.CastType) ? cast.CastType.Size : cast.CastOp.Type.Size) *
                    BITS_PER_BYTE;
                if ((srcBits < dstBits) && (constant.IconValue > 0) && (constant.IconValue < srcBits))
                {
                    JITDUMP("Recognized ubfix/sbfix pattern in LSH(CAST, CNS). Changing op to GT_BFIZ");
                    shift.SetOper(GT_BFIZ);
                    shift.Flags &= GTF_COMMON_MASK;
                    cast.CastOp.IsContained = false;
                    MakeSrcContained(shift, cast);
                }
            }
        }
#endif
#if TARGET_RISCV64
        if (CompilerInstance.compOpportunisticallyDependsOn(CORINFO_InstructionSet.InstructionSet_Zba))
        {
            TryLowerZextLeftShiftToSlliUw(shift, out _);
        }
#endif
    }

#if TARGET_RISCV64
    private void TryLowerZextLeftShiftToSlliUw(GenTreeOp shift, out GenTree? next)
    {
        throw new NotImplementedException("RISC-V Zba left-shift lowering is not ported.");
    }
#endif

#if TARGET_WASM
    private GenTree? LowerNeg(GenTreeUnOp node)
    {
        throw new NotImplementedException("Wasm negate lowering is not ported.");
    }

    private void LowerIndexAddr(GenTreeIndexAddr node)
    {
        throw new NotImplementedException("Wasm index-address lowering is not ported.");
    }

    private void LowerCkfinite(GenTreeOp node)
    {
        throw new NotImplementedException("Wasm finite-check lowering is not ported.");
    }
#endif
}
