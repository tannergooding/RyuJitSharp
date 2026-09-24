// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private int buildIndirUses(GenTreeIndir indirTree, SingleTypeRegSet candidates)
    {
        return buildAddrUses(indirTree.Op1, candidates);
    }

    private int buildAddrUses(GenTree addr, SingleTypeRegSet candidates = SRBM_NONE)
    {
        if (!addr.IsContained)
        {
            _ = buildUse(addr, candidates);
            return 1;
        }

        if (addr.Oper is not GT_LEA)
        {
            return 0;
        }

        var addrMode = addr.AsAddrMode();
        var srcCount = 0;
        if (addrMode.HasBaseAddress && !addrMode.BaseAddress.IsContained)
        {
            _ = buildUse(addrMode.BaseAddress, candidates);
            srcCount++;
        }

        if (addrMode.HasIndex)
        {
            var index = addrMode.Index;
            if (!index.IsContained)
            {
                _ = buildUse(index, candidates);
                srcCount++;
            }
#if TARGET_ARM64
            else if (index.Oper is GT_BFIZ)
            {
                var cast = index.AsOp().Op1.AsCast();
                assert(cast.IsContained);
                _ = buildUse(cast.CastOp, candidates);
                srcCount++;
            }
            else if (index.Oper is GT_CAST)
            {
                var cast = index.AsCast();
                assert(cast.IsContained);
                _ = buildUse(cast.CastOp, candidates);
                srcCount++;
            }
#endif
        }

        return srcCount;
    }

    private int buildOperandUses(GenTree node, SingleTypeRegSet candidates = SRBM_NONE)
    {
        if (!node.IsContained)
        {
            _ = buildUse(node, candidates);
            return 1;
        }

#if TARGET_ARM64
        if (node.IsVectorZero)
        {
            return 0;
        }
#endif

#if !TARGET_64BIT
        if (node.Oper is GT_LONG)
        {
            return buildBinaryUses(node.AsOp(), candidates);
        }
#endif

        if (node.Oper.IsIndir)
        {
            return buildIndirUses(node.AsIndir(), candidates);
        }

        if (node.Oper is GT_LEA)
        {
            return buildAddrUses(node, candidates);
        }

        if (node.Oper is GT_BSWAP or GT_BSWAP16)
        {
            return buildOperandUses(node.AsUnOp().Op1, candidates);
        }

#if FEATURE_HW_INTRINSICS
        if (node.Oper.IsHWIntrinsic)
        {
            var intrinsic = node.AsHWIntrinsic();
            var numArgs = intrinsic.Operands.Length;
            if (intrinsic.IsMemoryLoad())
            {
#if TARGET_ARM64
                if (numArgs == 2)
                {
                    return buildAddrUses(intrinsic.GetOp(1)) + buildOperandUses(intrinsic.GetOp(2), candidates);
                }
#endif
                return buildAddrUses(intrinsic.GetOp(1));
            }

            if (numArgs != 1)
            {
#if TARGET_ARM64
                if ((HWIntrinsicInfo.lookupFlags(intrinsic.HWIntrinsicId) & HW_Flag_Scalable) != 0)
                {
                    var srcCount = 0;
                    for (var argNum = 1; argNum <= numArgs; argNum++)
                    {
                        srcCount += buildOperandUses(intrinsic.GetOp(argNum), candidates);
                    }

                    return srcCount;
                }
#endif
                assert(numArgs == 2);
                assert(intrinsic.GetOp(2).IsContained && intrinsic.GetOp(2).Oper.IsCnsIntOrI);
            }

            return buildOperandUses(intrinsic.GetOp(1), candidates);
        }
#endif

#if TARGET_XARCH || TARGET_ARM64
        if (node.Oper.IsCompare)
        {
            return buildBinaryUses(node.AsOp(), candidates);
        }
#endif

#if TARGET_ARM64
        if (node.Oper is GT_MUL or GT_AND)
        {
            return buildBinaryUses(node.AsOp(), candidates);
        }

        if (node.Oper is GT_NEG or GT_CAST or GT_LSH or GT_RSH or GT_RSZ or GT_ROR)
        {
            return buildOperandUses(node.AsUnOp().Op1, candidates);
        }
#endif

        return 0;
    }

    private void addDelayFreeUses(RefPosition useRefPosition, GenTree? rmwNode)
    {
        Interval? rmwInterval = null;
        var rmwIsLastUse = false;
        if ((rmwNode is not null) && isCandidateLocalRef(rmwNode))
        {
            rmwInterval = getIntervalForLocalVarNode(rmwNode.AsLclVarCommon());
            assert(!rmwNode.AsLclVar().IsMultiReg);
            rmwIsLastUse = rmwNode.AsLclVar().IsLastUse(0);
        }

        if (!ReferenceEquals(useRefPosition.getInterval(), rmwInterval) ||
            (!rmwIsLastUse && !useRefPosition.lastUse))
        {
            setDelayFree(useRefPosition);
        }
    }

    private int buildDelayFreeUses(
        GenTree node,
        GenTree? rmwNode,
        SingleTypeRegSet candidates,
        ref RefPosition? useRefPositionRef)
    {
        useRefPositionRef = null;
        GenTree? addr = null;
        RefPosition? use = null;

        if (!node.IsContained)
        {
            use = buildUse(node, candidates);
        }
#if TARGET_ARM64
        else if (node.IsVectorZero)
        {
            return 0;
        }
#endif
#if FEATURE_HW_INTRINSICS
        else if (node.Oper.IsHWIntrinsic)
        {
            assert(node.AsHWIntrinsic().Operands.Length == 1);
            return buildDelayFreeUses(node.AsHWIntrinsic().GetOp(1), rmwNode, candidates, ref useRefPositionRef);
        }
#endif
        else if (!node.Oper.IsIndir)
        {
            return 0;
        }
        else
        {
            addr = node.AsIndir().Op1;
            if (!addr.IsContained)
            {
                use = buildUse(addr, candidates);
            }
            else if (addr.Oper is not GT_LEA)
            {
                return 0;
            }
        }

#if TARGET_ARM64
        assert(!node.IsMultiRegNode);
        assert((rmwNode is null) || varTypeUsesSameRegType(rmwNode.Type, node.Type) ||
            (rmwNode.IsMultiRegNode && varTypeUsesFloatReg(node.Type)));
#endif

        if (use is not null)
        {
            addDelayFreeUses(use, rmwNode);
            useRefPositionRef = use;
            return 1;
        }

        var addrMode = (addr ?? throw new FatalJitException("A contained address use requires an address mode.")).AsAddrMode();
        var srcCount = 0;
        if (addrMode.HasBaseAddress && !addrMode.BaseAddress.IsContained)
        {
            use = buildUse(addrMode.BaseAddress, candidates);
            addDelayFreeUses(use, rmwNode);
            srcCount++;
        }

        if (addrMode.HasIndex && !addrMode.Index.IsContained)
        {
            use = buildUse(addrMode.Index, candidates);
            addDelayFreeUses(use, rmwNode);
            srcCount++;
        }

        useRefPositionRef = use;
        return srcCount;
    }

    private int buildCallArgUses(GenTreeCall call)
    {
        var srcCount = 0;
        foreach (var arg in call.Args.LateArgs)
        {
            var argNode = arg.LateNode
                ?? throw new FatalJitException("A late call argument must have a lowered node.");

#if FEATURE_MULTIREG_ARGS
            if (argNode.Oper.IsFieldList)
            {
                foreach (var fieldUse in argNode.AsFieldList().Uses)
                {
                    assert(fieldUse.Node.Oper.IsPutArgReg);
                    srcCount++;
                    _ = buildUse(fieldUse.Node, genSingleTypeRegMask(fieldUse.Node.RegNum));
                }

                continue;
            }
#endif

            if (argNode.Oper.IsPutArgReg)
            {
                srcCount++;
                _ = buildUse(argNode, genSingleTypeRegMask(argNode.RegNum));
                continue;
            }

            assert(!arg.AbiInfo.HasAnyRegisterSegment);
            assert(argNode.Oper.IsPutArgStk);
        }

#if DEBUG
        foreach (var arg in call.Args.EarlyArgs)
        {
            assert(arg.EarlyNode?.Oper.IsPutArgStk is true);
            assert(arg.LateNode is null);
        }
#endif

        return srcCount;
    }
}
