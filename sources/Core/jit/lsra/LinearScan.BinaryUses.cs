// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private int buildBinaryUses(GenTreeUnOp node, SingleTypeRegSet candidates = SRBM_NONE)
    {
        var op1 = node.Op1;
        var op2 = node is GenTreeOp binary ? binary.Op2 : null;
        if (Configuration.TARGET_XARCH && node.Oper.IsBinary && isRMWRegOper(node))
        {
            var right = node.AsOp().Op2;
            if ((candidates == SRBM_NONE) && varTypeUsesFloatReg(node.Type) &&
                (op1.IsContainedIndir || right.IsContainedIndir))
            {
                if (op1.IsContainedIndir && !_evexIsSupported)
                {
                    return buildRmwUses(node, op1, right, _lowGprRegs, candidates);
                }
                else if (right.IsContainedIndir && !_evexIsSupported)
                {
                    return buildRmwUses(node, op1, right, candidates, _lowGprRegs);
                }
            }

            return buildRmwUses(node, op1, right, candidates, candidates);
        }

        var srcCount = 0;
        if (op1 is not null)
        {
            if (Configuration.TARGET_XARCH && op1.IsContainedIndir && !_evexIsSupported)
            {
                if (candidates == SRBM_NONE)
                {
                    srcCount += buildOperandUses(op1, _lowGprRegs);
                }
                else
                {
                    assert((candidates & _lowGprRegs) != SRBM_NONE);
                    srcCount += buildOperandUses(op1, candidates & _lowGprRegs);
                }
            }
            else
            {
                srcCount += buildOperandUses(op1, candidates);
            }
        }

        if (op2 is not null)
        {
            if (Configuration.TARGET_XARCH && op2.IsContainedIndir && !_evexIsSupported)
            {
                if (candidates == SRBM_NONE)
                {
                    candidates = _lowGprRegs;
                }
                else
                {
                    assert((candidates & _lowGprRegs) != SRBM_NONE);
                    srcCount += buildOperandUses(node.Op1, candidates & _lowGprRegs);
                }
            }
            srcCount += buildOperandUses(op2, candidates);
        }

        return srcCount;
    }

    private int buildRmwUses(
        GenTree node,
        GenTree op1,
        GenTree? op2,
        SingleTypeRegSet op1Candidates,
        SingleTypeRegSet op2Candidates)
    {
        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED);
            return default!;
        }

        if (Configuration.TARGET_X86 && varTypeIsByte(node.Type))
        {
            var allByteRegisters = _availableIntRegs & ~RBM_NON_BYTE_REGS.IntRegSet;
            var byteCandidates = (op1Candidates == SRBM_NONE)
                ? allByteRegisters
                : (op1Candidates & allByteRegisters);
            if (!op1.IsContained)
            {
                assert(byteCandidates != SRBM_NONE);
                op1Candidates = byteCandidates;
            }

            if (node.Oper.IsCommutative && (op2 is not null) && !op2.IsContained)
            {
                assert(byteCandidates != SRBM_NONE);
                op2Candidates = byteCandidates;
            }
        }

        var (prefOp1, prefOp2) = getTgtPrefOperands(node, op1, op2);
        assert(!prefOp2 || node.Oper.IsCommutative);

        var delayUseOperand = op2;
        if (node.Oper.IsCommutative)
        {
            if (op1.IsContained && (op2 is not null))
            {
                delayUseOperand = op1;
            }
            else if ((op2 is not null) && (!op2.IsContained || op2.Oper.IsCnsIntOrI))
            {
                delayUseOperand = null;
            }
        }
        else if (op1.IsContained)
        {
            delayUseOperand = null;
        }

        if (delayUseOperand is not null)
        {
            assert(!prefOp1 || !ReferenceEquals(delayUseOperand, op1));
            assert(!prefOp2 || !ReferenceEquals(delayUseOperand, op2));
        }

        var srcCount = 0;
        if (prefOp1)
        {
            assert(!op1.IsContained);
            _targetPreferredUse = buildUse(op1, op1Candidates);
            srcCount++;
        }
        else if (ReferenceEquals(delayUseOperand, op1))
        {
            RefPosition? use = null;
            srcCount += buildDelayFreeUses(op1, op2, op1Candidates, ref use);
        }
        else
        {
            srcCount += buildOperandUses(op1, op1Candidates);
        }

        if (op2 is not null)
        {
            if (prefOp2)
            {
                assert(!op2.IsContained);
                _targetPreferredUse2 = buildUse(op2, op2Candidates);
                srcCount++;
            }
            else if (ReferenceEquals(delayUseOperand, op2))
            {
                RefPosition? use = null;
                srcCount += buildDelayFreeUses(op2, op1, op2Candidates, ref use);
            }
            else
            {
                srcCount += buildOperandUses(op2, op2Candidates);
            }
        }

        return srcCount;
    }

    private (bool PrefOp1, bool PrefOp2) getTgtPrefOperands(GenTree tree, GenTree op1, GenTree? op2)
    {
        var prefOp1 = false;
        var prefOp2 = false;
        if (isRMWRegOper(tree))
        {
            if (!op1.IsContained)
            {
                prefOp1 = true;
            }

            if (tree.Oper.IsCommutative && (op2 is not null) && !op2.IsContained)
            {
                prefOp2 = true;
            }
        }

        return (prefOp1, prefOp2);
    }

    private bool isRMWRegOper(GenTree tree)
    {
        if (Configuration.FEATURE_HW_INTRINSICS)
        {
            assert(tree.Oper.IsBinary || (tree.Oper.IsMultiOp && (tree.AsMultiOp().Operands.Length <= 2)));
        }
        else
        {
            assert(tree.Oper.IsBinary);
        }

        if (tree.Oper.IsCompare || tree.Oper is GT_CMP or GT_TEST or GT_BT)
        {
            return false;
        }

        switch (tree.Oper)
        {
            case GT_LEA:
            case GT_STOREIND:
            case GT_STORE_BLK:
            case GT_SWITCH_TABLE:
            case GT_LOCKADD:
            case GT_LONG:
            case GT_SWIFT_ERROR_RET:
            {
                return (tree.Oper is GT_LONG) && !Configuration.TARGET_X86;
            }

            case GT_ADD:
            case GT_SUB:
            case GT_DIV:
            {
                return !varTypeIsFloating(tree.Type) || !_compiler.canUseVexEncoding();
            }

            case GT_MUL:
            case GT_SUB_HI:
            case GT_LSH_HI:
            {
                if ((tree.Oper is not GT_MUL) && !Configuration.TARGET_X86)
                {
                    return true;
                }

                if (varTypeIsFloating(tree.Type))
                {
                    return !_compiler.canUseVexEncoding();
                }

                return !tree.AsOp().Op2.IsContainedIntOrIImmed && !tree.AsOp().Op1.IsContainedIntOrIImmed;
            }

            case GT_MUL_LONG:
            case GT_MULHI:
            {
                if ((tree.Oper is GT_MUL_LONG) && !Configuration.TARGET_X86)
                {
                    return true;
                }

                return ((tree.Flags & GTF_UNSIGNED) == 0) ||
                    !_compiler.compOpportunisticallyDependsOn(InstructionSet_AVX2);
            }

            case GT_HWINTRINSIC:
            {
                return !Configuration.FEATURE_HW_INTRINSICS || tree.AsHWIntrinsic().IsRmwHWIntrinsic(_compiler);
            }

            default:
            {
                return true;
            }
        }
    }
}
