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
            else if (Configuration.TARGET_ARM64 && index.Oper is GT_BFIZ)
            {
                var cast = index.AsOp().Op1.AsCast();
                assert(cast.IsContained);
                _ = buildUse(cast.CastOp, candidates);
                srcCount++;
            }
            else if (Configuration.TARGET_ARM64 && index.Oper is GT_CAST)
            {
                var cast = index.AsCast();
                assert(cast.IsContained);
                _ = buildUse(cast.CastOp, candidates);
                srcCount++;
            }
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

        if (Configuration.TARGET_ARM64 && node.IsVectorZero)
        {
            return 0;
        }

        if (!Configuration.TARGET_64BIT && node.Oper is GT_LONG)
        {
            return buildBinaryUses(node.AsOp(), candidates);
        }

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

        if (Configuration.FEATURE_HW_INTRINSICS && node.Oper.IsHWIntrinsic)
        {
            var intrinsic = node.AsHWIntrinsic();
            var numArgs = intrinsic.Operands.Length;
            if (intrinsic.IsMemoryLoad())
            {
                if (Configuration.TARGET_ARM64 && numArgs == 2)
                {
                    return buildAddrUses(intrinsic.GetOp(1)) + buildOperandUses(intrinsic.GetOp(2), candidates);
                }
                return buildAddrUses(intrinsic.GetOp(1));
            }

            if (numArgs != 1)
            {
                if (Configuration.TARGET_ARM64 &&
                    (HWIntrinsicInfo.lookupFlags(intrinsic.HWIntrinsicId) & HW_Flag_Scalable) != 0)
                {
                    var srcCount = 0;
                    for (var argNum = 1; argNum <= numArgs; argNum++)
                    {
                        srcCount += buildOperandUses(intrinsic.GetOp(argNum), candidates);
                    }

                    return srcCount;
                }
                assert(numArgs == 2);
                assert(intrinsic.GetOp(2).IsContained && intrinsic.GetOp(2).Oper.IsCnsIntOrI);
            }

            return buildOperandUses(intrinsic.GetOp(1), candidates);
        }

        if ((Configuration.TARGET_XARCH || Configuration.TARGET_ARM64) && node.Oper.IsCompare)
        {
            return buildBinaryUses(node.AsOp(), candidates);
        }

        if (Configuration.TARGET_ARM64 && (node.Oper is GT_MUL or GT_AND))
        {
            return buildBinaryUses(node.AsOp(), candidates);
        }

        if (Configuration.TARGET_ARM64 &&
            (node.Oper is GT_NEG or GT_CAST or GT_LSH or GT_RSH or GT_RSZ or GT_ROR))
        {
            return buildOperandUses(node.AsUnOp().Op1, candidates);
        }

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
        else if (Configuration.TARGET_ARM64 && node.IsVectorZero)
        {
            return 0;
        }
        else if (Configuration.FEATURE_HW_INTRINSICS && node.Oper.IsHWIntrinsic)
        {
            assert(node.AsHWIntrinsic().Operands.Length == 1);
            return buildDelayFreeUses(node.AsHWIntrinsic().GetOp(1), rmwNode, candidates, ref useRefPositionRef);
        }
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

        if (Configuration.TARGET_ARM64)
        {
            assert(!node.IsMultiRegNode);
            assert((rmwNode is null) || varTypeUsesSameRegType(rmwNode.Type, node.Type) ||
                (rmwNode.IsMultiRegNode && varTypeUsesFloatReg(node.Type)));
        }

        if (use is not null)
        {
            addDelayFreeUses(use, rmwNode);
            useRefPositionRef = use;
            return 1;
        }

        if (addr is null)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_INTERNALERROR,
        FatalJitErrorCode.ContainedAddressUseRequiresAnAddressMode
    );
            return 0;
        }

        var addrMode = addr.AsAddrMode();
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
            var argNode = arg.LateNode;
            if (argNode is null)
            {
                FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.LateCallArgumentMustHaveALoweredNode);
                return default!;
            }

            if (Configuration.FEATURE_MULTIREG_ARGS && argNode.Oper.IsFieldList)
            {
                foreach (var fieldUse in argNode.AsFieldList().Uses)
                {
                    assert(fieldUse.Node.Oper.IsPutArgReg);
                    srcCount++;
                    _ = buildUse(fieldUse.Node, genSingleTypeRegMask(fieldUse.Node.RegNum));
                }

                continue;
            }

            if (argNode.Oper.IsPutArgReg)
            {
                srcCount++;
                _ = buildUse(argNode, genSingleTypeRegMask(argNode.RegNum));
                continue;
            }

            assert(!arg.AbiInfo.HasAnyRegisterSegment);
            assert(argNode.Oper.IsPutArgStk);
        }

        if (Configuration.DEBUG)
        {
            foreach (var arg in call.Args.EarlyArgs)
            {
                assert(arg.EarlyNode?.Oper.IsPutArgStk is true);
                assert(arg.LateNode is null);
            }
        }

        return srcCount;
    }
}
