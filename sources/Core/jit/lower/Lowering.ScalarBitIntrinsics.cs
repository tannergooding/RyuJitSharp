// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
#if FEATURE_HW_INTRINSICS && TARGET_XARCH
    private GenTreeHWIntrinsic? TryLowerAndOpToResetLowestSetBit(GenTreeOp andNode)
    {
        assert((andNode.Oper is GT_AND) && varTypeIsIntegral(andNode.Type));
        var op1 = andNode.Op1;
        if ((op1.Oper is not GT_LCL_VAR) || CompilerInstance.lvaGetDesc(op1.AsLclVar().LclNum).IsAddressExposed)
        {
            return null;
        }

        var op2 = andNode.Op2;
        nint expectedConst;
        if (op2.Oper is GT_ADD)
        {
            expectedConst = -1;
        }
        else if (op2.Oper is GT_SUB)
        {
            expectedConst = 1;
        }
        else
        {
            return null;
        }

        if (op2.HasOverflowCheck)
        {
            return null;
        }

        var addOp2 = op2.AsOp().Op2;
        if (!addOp2.IsIntegralConst(expectedConst))
        {
            return null;
        }
        var addOp1 = op2.AsOp().Op1;
        if ((addOp1.Oper is not GT_LCL_VAR) || (addOp1.AsLclVar().LclNum != op1.AsLclVar().LclNum))
        {
            return null;
        }

        if (((addOp2.Flags | op2.Flags | andNode.Flags) & GTF_SET_FLAGS) != 0)
        {
            return null;
        }
        if (!CompilerInstance.compOpportunisticallyDependsOn(InstructionSet_AVX2))
        {
            return null;
        }

        NamedIntrinsic intrinsic;
#if TARGET_AMD64
        if (andNode.Type is TYP_LONG)
        {
            intrinsic = NI_AVX2_X64_ResetLowestSetBit;
        }
        else
#endif
        {
            assert(andNode.Type is TYP_INT);
            intrinsic = NI_AVX2_ResetLowestSetBit;
        }

        if (!BlockRange().TryGetUse(andNode, out var use))
        {
            return null;
        }
        var blsrNode = CompilerInstance.gtNewScalarHWIntrinsicNode(andNode.Type, intrinsic, op1);
        JITDUMP("Lower: optimize AND(X, ADD(X, -1))\n");
        DISPNODE(andNode);
        JITDUMP("to:\n");
        DISPNODE(blsrNode);

        BlockRange().InsertBefore(andNode, blsrNode);
        use.ReplaceWith(blsrNode);
        BlockRange().Remove(andNode);
        BlockRange().Remove(op2);
        BlockRange().Remove(addOp1);
        BlockRange().Remove(addOp2);
        ContainCheckHWIntrinsic(blsrNode);
        return blsrNode;
    }

    private GenTreeHWIntrinsic? TryLowerAndOpToExtractLowestSetBit(GenTreeOp andNode)
    {
        GenTree opNode;
        GenTree negNode;
        if (andNode.Op1.Oper is GT_NEG)
        {
            negNode = andNode.Op1;
            opNode = andNode.Op2;
        }
        else if (andNode.Op2.Oper is GT_NEG)
        {
            negNode = andNode.Op2;
            opNode = andNode.Op1;
        }
        else
        {
            return null;
        }

        var negOp = negNode.AsUnOp().Op1;
        if ((negOp.Oper is not GT_LCL_VAR) || (opNode.Oper is not GT_LCL_VAR) ||
            (negOp.AsLclVar().LclNum != opNode.AsLclVar().LclNum))
        {
            return null;
        }
        if (((andNode.Flags | negNode.Flags) & GTF_SET_FLAGS) != 0)
        {
            return null;
        }
        if (!CompilerInstance.compOpportunisticallyDependsOn(InstructionSet_AVX2))
        {
            return null;
        }

        NamedIntrinsic intrinsic;
#if TARGET_AMD64
        if (andNode.Type is TYP_LONG)
        {
            intrinsic = NI_AVX2_X64_ExtractLowestSetBit;
        }
        else
#endif
        {
            assert(andNode.Type is TYP_INT);
            intrinsic = NI_AVX2_ExtractLowestSetBit;
        }

        if (!BlockRange().TryGetUse(andNode, out var use))
        {
            return null;
        }
        var blsiNode = CompilerInstance.gtNewScalarHWIntrinsicNode(andNode.Type, intrinsic, opNode);
        JITDUMP("Lower: optimize AND(X, NEG(X)))\n");
        DISPNODE(andNode);
        JITDUMP("to:\n");
        DISPNODE(blsiNode);

        BlockRange().InsertBefore(andNode, blsiNode);
        use.ReplaceWith(blsiNode);
        BlockRange().Remove(andNode);
        BlockRange().Remove(negNode);
        BlockRange().Remove(negOp);
        ContainCheckHWIntrinsic(blsiNode);
        return blsiNode;
    }

    private GenTreeHWIntrinsic? TryLowerAndOpToZeroHighBits(GenTreeOp andNode)
    {
        assert((andNode.Oper is GT_AND) && varTypeIsIntegral(andNode.Type));
        if (andNode.Type is not TYP_INT and not TYP_LONG)
        {
            return null;
        }

        GenTree srcNode;
        GenTree maskNode;
        if (andNode.Op2.Oper is GT_ADD or GT_SUB)
        {
            maskNode = andNode.Op2;
            srcNode = andNode.Op1;
        }
        else if (andNode.Op1.Oper is GT_ADD or GT_SUB)
        {
            maskNode = andNode.Op1;
            srcNode = andNode.Op2;
        }
        else
        {
            return null;
        }

        GenTree lshNode;
        GenTree constNode;
        var maskOp = maskNode.AsOp();
        if ((maskNode.Oper is GT_ADD) && maskOp.Op2.IsIntegralConst(-1) && (maskOp.Op1.Oper is GT_LSH))
        {
            lshNode = maskOp.Op1;
            constNode = maskOp.Op2;
        }
        else if ((maskNode.Oper is GT_ADD) && maskOp.Op1.IsIntegralConst(-1) && (maskOp.Op2.Oper is GT_LSH))
        {
            lshNode = maskOp.Op2;
            constNode = maskOp.Op1;
        }
        else if ((maskNode.Oper is GT_SUB) && maskOp.Op2.IsIntegralConst(1) && (maskOp.Op1.Oper is GT_LSH))
        {
            lshNode = maskOp.Op1;
            constNode = maskOp.Op2;
        }
        else
        {
            return null;
        }

        if (maskNode.HasOverflowCheck || !lshNode.AsOp().Op1.IsIntegralConst(1))
        {
            return null;
        }
        var indexNode = lshNode.AsOp().Op2;
        if (indexNode.Oper.IsIntegralConst)
        {
            // Keep constant masks as AND-immediate rather than materializing a BZHI index register.
            return null;
        }
        if (((andNode.Flags | maskNode.Flags | lshNode.Flags) & GTF_SET_FLAGS) != 0)
        {
            return null;
        }
        if (!CompilerInstance.compOpportunisticallyDependsOn(InstructionSet_AVX2))
        {
            return null;
        }

        NamedIntrinsic intrinsic;
#if TARGET_AMD64
        if (andNode.Type is TYP_LONG)
        {
            intrinsic = NI_AVX2_X64_ZeroHighBits;
        }
        else
#endif
        {
            assert(andNode.Type is TYP_INT);
            intrinsic = NI_AVX2_ZeroHighBits;
        }

        if (!BlockRange().TryGetUse(andNode, out var use))
        {
            return null;
        }

        // BZHI leaves the value unchanged for indices >= width, whereas C# shifts
        // mask the count. Keep the mask and the backend's index-first operand order.
        var maskCns = CompilerInstance.gtNewIconNode(indexNode.Type.ActualType, andNode.Type is TYP_LONG ? 63 : 31);
        var indexMask = CompilerInstance.gtNewBinaryNode(GT_AND, indexNode.Type.ActualType, indexNode, maskCns);
        var bzhiNode = CompilerInstance.gtNewScalarHWIntrinsicNode(andNode.Type, intrinsic, indexMask, srcNode);
        JITDUMP("Lower: optimize AND(X, ADD(LSH(1, Y), -1))\n");
        DISPNODE(andNode);
        JITDUMP("to:\n");
        DISPNODE(bzhiNode);

        BlockRange().InsertBefore(andNode, maskCns);
        BlockRange().InsertBefore(andNode, indexMask);
        BlockRange().InsertBefore(andNode, bzhiNode);
        use.ReplaceWith(bzhiNode);
        BlockRange().Remove(andNode);
        BlockRange().Remove(maskNode);
        BlockRange().Remove(lshNode);
        BlockRange().Remove(lshNode.AsOp().Op1);
        BlockRange().Remove(constNode);
        ContainCheckBinary(indexMask);
        ContainCheckHWIntrinsic(bzhiNode);
        return bzhiNode;
    }

    private GenTreeHWIntrinsic? TryLowerAndOpToAndNot(GenTreeOp andNode)
    {
        assert((andNode.Oper is GT_AND) && varTypeIsIntegral(andNode.Type));
        GenTree opNode;
        GenTree notNode;
        if (andNode.Op1.Oper is GT_NOT)
        {
            notNode = andNode.Op1;
            opNode = andNode.Op2;
        }
        else if (andNode.Op2.Oper is GT_NOT)
        {
            notNode = andNode.Op2;
            opNode = andNode.Op1;
        }
        else
        {
            return null;
        }

        // Preserve the smaller memory read-modify-write AND when it is available.
        if (IsBinOpInRMWStoreInd(andNode))
        {
            return null;
        }
        if (((andNode.Flags | notNode.Flags) & GTF_SET_FLAGS) != 0)
        {
            return null;
        }

        NamedIntrinsic intrinsic;
        if ((andNode.Type is TYP_LONG) && CompilerInstance.compOpportunisticallyDependsOn(InstructionSet_AVX2_X64))
        {
            intrinsic = NI_AVX2_X64_AndNot;
        }
        else if (CompilerInstance.compOpportunisticallyDependsOn(InstructionSet_AVX2))
        {
            intrinsic = NI_AVX2_AndNotScalar;
        }
        else
        {
            return null;
        }

        if (!BlockRange().TryGetUse(andNode, out var use))
        {
            return null;
        }
        var andnNode = CompilerInstance.gtNewScalarHWIntrinsicNode(andNode.Type, intrinsic, notNode.AsUnOp().Op1, opNode);
        JITDUMP("Lower: optimize AND(X, NOT(Y)))\n");
        DISPNODE(andNode);
        JITDUMP("to:\n");
        DISPNODE(andnNode);

        BlockRange().InsertBefore(andNode, andnNode);
        use.ReplaceWith(andnNode);
        BlockRange().Remove(andNode);
        BlockRange().Remove(notNode);
        ContainCheckHWIntrinsic(andnNode);
        return andnNode;
    }

    private GenTreeHWIntrinsic? TryLowerXorOpToGetMaskUpToLowestSetBit(GenTreeOp xorNode)
    {
        assert((xorNode.Oper is GT_XOR) && varTypeIsIntegral(xorNode.Type));
        var op1 = xorNode.Op1;
        if ((op1.Oper is not GT_LCL_VAR) || CompilerInstance.lvaGetDesc(op1.AsLclVar().LclNum).IsAddressExposed)
        {
            return null;
        }
        var op2 = xorNode.Op2;
        nint expectedConst;
        if (op2.Oper is GT_ADD)
        {
            expectedConst = -1;
        }
        else if (op2.Oper is GT_SUB)
        {
            expectedConst = 1;
        }
        else
        {
            return null;
        }
        if (op2.HasOverflowCheck)
        {
            return null;
        }
        var addOp2 = op2.AsOp().Op2;
        if (!addOp2.IsIntegralConst(expectedConst))
        {
            return null;
        }
        var addOp1 = op2.AsOp().Op1;
        if ((addOp1.Oper is not GT_LCL_VAR) || (addOp1.AsLclVar().LclNum != op1.AsLclVar().LclNum))
        {
            return null;
        }
        if (((addOp2.Flags | op2.Flags | xorNode.Flags) & GTF_SET_FLAGS) != 0)
        {
            return null;
        }
        if (!CompilerInstance.compOpportunisticallyDependsOn(InstructionSet_AVX2))
        {
            return null;
        }

        NamedIntrinsic intrinsic;
#if TARGET_AMD64
        if (xorNode.Type is TYP_LONG)
        {
            intrinsic = NI_AVX2_X64_GetMaskUpToLowestSetBit;
        }
        else
#endif
        {
            assert(xorNode.Type is TYP_INT);
            intrinsic = NI_AVX2_GetMaskUpToLowestSetBit;
        }

        if (!BlockRange().TryGetUse(xorNode, out var use))
        {
            return null;
        }
        var blsmskNode = CompilerInstance.gtNewScalarHWIntrinsicNode(xorNode.Type, intrinsic, op1);
        JITDUMP("Lower: optimize XOR(X, ADD(X, -1)))\n");
        DISPNODE(xorNode);
        JITDUMP("to:\n");
        DISPNODE(blsmskNode);

        BlockRange().InsertBefore(xorNode, blsmskNode);
        use.ReplaceWith(blsmskNode);
        BlockRange().Remove(xorNode);
        BlockRange().Remove(op2);
        BlockRange().Remove(addOp1);
        BlockRange().Remove(addOp2);
        ContainCheckHWIntrinsic(blsmskNode);
        return blsmskNode;
    }
#endif
}
