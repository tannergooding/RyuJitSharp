// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private void ContainCheckBinary(GenTreeOp node)
    {
#if TARGET_XARCH
        assert(node.Oper.IsBinary);
        if (varTypeIsFloating(node.Type))
        {
            assert(node.Oper is GT_ADD or GT_SUB);
            ContainCheckFloatBinary(node);
            return;
        }

        var op1 = node.Op1;
        var op2 = node.Op2;
        var binOpInRmw = false;
        GenTree? operand = null;
        if (IsContainableImmed(node, op2))
        {
            operand = op2;
        }
        else
        {
            binOpInRmw = IsBinOpInRMWStoreInd(node);
            if (!binOpInRmw)
            {
                // Different memory widths would lose the normalization performed by a register load.
                if (IsContainableMemoryOpSize(node, op2) && IsContainableMemoryOp(op2) &&
                    IsSafeToContainMem(node, op2))
                {
                    operand = op2;
                }
                if ((operand is null) && node.Oper.IsCommutative)
                {
                    if (IsContainableImmed(node, op1) ||
                        (IsContainableMemoryOpSize(node, op1) && IsContainableMemoryOp(op1) &&
                            IsSafeToContainMem(node, op1)))
                    {
                        operand = op1;
                    }
                }
            }
        }

        if (operand is not null)
        {
            MakeSrcContained(node, operand);
        }
        else if (!binOpInRmw)
        {
            SetRegOptionalForBinOp(node, IsSafeToMarkRegOptional(node, op1), IsSafeToMarkRegOptional(node, op2));
        }
#else
        throw new System.NotImplementedException("Non-xarch binary containment is not ported.");
#endif
    }

#if TARGET_XARCH
    private void ContainCheckFloatBinary(GenTreeOp node)
    {
        assert((node.Oper is GT_ADD or GT_SUB or GT_MUL or GT_DIV) && varTypeIsFloating(node.Type));
        assert(!node.HasOverflowCheckEx);
        var op1 = node.Op1;
        var op2 = node.Op2;
        assert(op1.Type == op2.Type);

        if (op2.IsCnsNonZeroFltOrDbl || (IsContainableMemoryOp(op2) && IsSafeToContainMem(node, op2)))
        {
            MakeSrcContained(node, op2);
        }
        if (!op2.IsContained && node.Oper.IsCommutative)
        {
            // A safe memory source can occupy the second instruction operand after swapping.
            if (op1.IsCnsNonZeroFltOrDbl || (IsContainableMemoryOp(op1) && IsSafeToContainMem(node, op1)))
            {
                MakeSrcContained(node, op1);
            }
        }
        if (!op1.IsContained && !op2.IsContained)
        {
            SetRegOptionalForBinOp(node, IsSafeToMarkRegOptional(node, op1), IsSafeToMarkRegOptional(node, op2));
        }
    }

    private void SetRegOptionalForBinOp(GenTreeOp tree, bool isSafeToMarkOp1, bool isSafeToMarkOp2)
    {
        assert(tree.Oper.IsBinary);
        var op1 = tree.Op1;
        var op2 = tree.Op2;
        var op1Legal = isSafeToMarkOp1 && tree.Oper.IsCommutative && IsContainableMemoryOpSize(tree, op1);
        var op2Legal = isSafeToMarkOp2 && IsContainableMemoryOpSize(tree, op2);
        GenTree? operand = null;
        if (op1Legal)
        {
            operand = op2Legal ? PreferredRegOptionalOperand(op1, op2) : op1;
        }
        else if (op2Legal)
        {
            operand = op2;
        }
        if (operand is not null)
        {
            MakeSrcRegOptional(tree, operand);
        }
    }

    private GenTree PreferredRegOptionalOperand(GenTree? op1, GenTree op2)
    {
        if (op1 is null)
        {
            return op2;
        }
        assert(!op1.IsRegOptional && !op2.IsRegOptional);

        // Prefer a local over a tree temp. Otherwise op1's longer lifetime makes it more
        // likely to spill, except when tracked-local weights predict that op2 will spill.
        var preferred = op1;
        if (op1.Oper is GT_LCL_VAR)
        {
            if (op2.Oper is GT_LCL_VAR)
            {
                ref var first = ref CompilerInstance.lvaGetDesc(op1.AsLclVarCommon().LclNum);
                ref var second = ref CompilerInstance.lvaGetDesc(op2.AsLclVarCommon().LclNum);
                if (!first.lvDoNotEnregister && !second.lvDoNotEnregister && first.lvTracked && second.lvTracked &&
                    (first.lvRefCntWtd() >= second.lvRefCntWtd()))
                {
                    preferred = op2;
                }
            }
        }
        else if (op2.Oper is GT_LCL_VAR)
        {
            preferred = op2;
        }

        return preferred;
    }
#endif
}
