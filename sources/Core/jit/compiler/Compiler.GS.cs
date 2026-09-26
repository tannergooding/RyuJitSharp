// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Compiler
{
    public PhaseStatus gsPhase()
    {
        if (NeedsGSSecurityCookie)
        {
            gsGSChecksInitCookie();

            if (compGSReorderStackLayout)
            {
                gsCopyShadowParams();
            }

            return PhaseStatus.MODIFIED_EVERYTHING;
        }

#if DEBUG
        if (compGSSecurityCheckBlocker is not null)
        {
            JITDUMP($"GS security check requested, but not provided: {compGSSecurityCheckBlocker}\n");
        }
        else
        {
            JITDUMP("No GS security needed\n");
        }
#endif
        return PhaseStatus.MODIFIED_NOTHING;
    }

    public unsafe void gsGSChecksInitCookie()
    {
        lvaGSSecurityCookie = lvaGrabTempWithImplicitUse(false, "GSSecurityCookie");
        lvaSetVarAddrExposed(lvaGSSecurityCookie, AddressExposedReason.TOO_CONSERVATIVE);
        lvaGetDesc(lvaGSSecurityCookie).Type = TYP_I_IMPL;

        GSCookie cookieValue = default;
        GSCookie* cookieAddress = null;
        info.compCompHnd->getGSCookie(&cookieValue, &cookieAddress);
        gsGlobalSecurityCookieVal = cookieValue;
        gsGlobalSecurityCookieAddr = cookieAddress;
    }

    public void gsCopyShadowParams()
    {
        if (info.compIsVarArgs)
        {
            return;
        }

        gsShadowVarInfoCount = lvaCount;
        gsShadowVarInfo = new ShadowParamVarInfo[gsShadowVarInfoCount];

        for (var lclNum = 0; lclNum < gsShadowVarInfoCount; lclNum++)
        {
            gsShadowVarInfo[lclNum] = new ShadowParamVarInfo();
        }

        if (gsFindVulnerableParams())
        {
            gsParamsToShadows();
        }
        else
        {
            gsShadowVarInfo = null;
            gsShadowVarInfoCount = 0;
        }
    }

    public void gsUnionAssignGroups(int lclNum1, int lclNum2, GenTree reason)
    {
        var shadowInfo = gsShadowVarInfo ?? throw new InvalidOperationException("GS shadow analysis requires a local table.");
        var info1 = shadowInfo[lclNum1];
        var info2 = shadowInfo[lclNum2];
        var traits = new BitVecTraits(this, gsShadowVarInfoCount);

#if DEBUG
        if (info1.AssignGroup != info2.AssignGroup)
        {
            JITDUMP($"Unifying assign groups of V{lclNum1:D2} and V{lclNum2:D2} because of [{reason.TreeId:D6}]\n");
        }
#endif
        if (info1.AssignGroup is not null)
        {
            if (info2.AssignGroup is not null)
            {
                BitVecOps.UnionD(traits, info1.AssignGroup, info2.AssignGroup);
            }
            else
            {
                BitVecOps.AddElemD(traits, info1.AssignGroup, lclNum2);
            }

            info2.AssignGroup = info1.AssignGroup;
        }
        else if (info2.AssignGroup is not null)
        {
            BitVecOps.AddElemD(traits, info2.AssignGroup, lclNum1);
            info1.AssignGroup = info2.AssignGroup;
        }
        else
        {
            var group = BitVecOps.MakeEmpty(traits);
            info1.AssignGroup = group;
            info2.AssignGroup = group;
            BitVecOps.AddElemD(traits, group, lclNum1);
            BitVecOps.AddElemD(traits, group, lclNum2);
        }
    }

    public void gsVisitDependentLocals(GenTree node, Action<int> visit)
    {
        Visit(node, null);

        void Visit(GenTree current, GenTree? user)
        {
            if (current.Oper is GT_IND or GT_BLK or GT_MDARR_LENGTH or GT_MDARR_LOWER_BOUND or GT_CALL)
            {
                return;
            }

            if ((user is not null) && (user.Oper is GT_SELECT) && (current == user.AsConditional().Cond))
            {
                return;
            }

            if (current.Oper is GT_LCL_VAR or GT_LCL_FLD)
            {
                visit(current.AsLclVarCommon().LclNum);
            }

            foreach (var operand in current.Operands)
            {
                Visit(operand, current);
            }
        }
    }

    public void gsMarkPointers(GenTree tree)
    {
        gsVisitDependentLocals(tree, lclNum => {
            ref var descriptor = ref lvaGetDesc(lclNum);
#if DEBUG
            if (!descriptor.lvIsPtr)
            {
                JITDUMP($"Marking V{lclNum:D2} as a pointer because of [{tree.TreeId:D6}]\n");
            }
#endif
            descriptor.lvIsPtr = true;
        });
    }

    public bool gsFindVulnerableParams()
    {
        foreach (var block in Blocks)
        {
            foreach (var node in block)
            {
                switch (node.Oper)
                {
                    case GT_IND:
                    case GT_BLK:
                    case GT_MDARR_LENGTH:
                    case GT_MDARR_LOWER_BOUND:
                    case GT_STOREIND:
                    case GT_STORE_BLK:
                    {
                        gsMarkPointers(node.IndirOrArrMetaDataAddr);
                        break;
                    }

                    case GT_STORE_LCL_VAR:
                    case GT_STORE_LCL_FLD:
                    {
                        var local = node.AsLclVarCommon();
                        gsVisitDependentLocals(local.Data, lclNum => gsUnionAssignGroups(local.LclNum, lclNum, local));
                        break;
                    }

                    case GT_CALL:
                    {
                        var call = node.AsCall();
                        if (call.Args.HasThisPointer)
                        {
                            gsMarkPointers(call.Args.ThisArg.Node);
                        }

                        if (call._callType is CT_INDIRECT)
                        {
                            assert(call.ControlExpr is not null);
                            gsMarkPointers(call.ControlExpr);
                        }

                        break;
                    }
                }
            }
        }

        var hasOneVulnerable = false;
        var traits = new BitVecTraits(this, gsShadowVarInfoCount);
        var propagated = BitVecOps.MakeEmpty(traits);
        var shadowInfo = gsShadowVarInfo ?? throw new InvalidOperationException("GS shadow analysis requires a local table.");

        for (var lclNum = 0; lclNum < gsShadowVarInfoCount; lclNum++)
        {
            ref var descriptor = ref lvaGetDesc(lclNum);
            if (descriptor.lvIsPtr || descriptor.lvIsUnsafeBuffer)
            {
                hasOneVulnerable = true;
            }

            var group = shadowInfo[lclNum].AssignGroup;
            if ((group is null) || BitVecOps.IsMember(traits, propagated, lclNum))
            {
                continue;
            }

            var isUnderIndir = descriptor.lvIsPtr;
            _ = BitVecOps.VisitBits(traits, group, member => {
                isUnderIndir |= lvaGetDesc(member).lvIsPtr;
                return !isUnderIndir;
            });

            if (!isUnderIndir)
            {
                continue;
            }

            hasOneVulnerable = true;
            _ = BitVecOps.VisitBits(traits, group, member => {
                lvaGetDesc(member).lvIsPtr = true;
                BitVecOps.AddElemD(traits, propagated, member);
                return true;
            });
        }

        return hasOneVulnerable;
    }

    public void gsParamsToShadows()
    {
        var shadowInfo = gsShadowVarInfo ?? throw new InvalidOperationException("GS shadow copies require a local table.");
        if (!gsCreateShadowingLocals())
        {
            return;
        }

        foreach (var block in Blocks)
        {
            foreach (var node in block)
            {
                gsRewriteTreeForShadowParam(node);
            }
        }

        for (var lclNum = 0; lclNum < gsShadowVarInfoCount; lclNum++)
        {
            var shadowLclNum = shadowInfo[lclNum].ShadowCopy;
            if (shadowLclNum != BAD_VAR_NUM)
            {
                gsCopyIntoShadow(lclNum, shadowLclNum);
            }
        }

        if (!compJmpOpUsed)
        {
            return;
        }

        foreach (var block in Blocks)
        {
            if (block.Kind is not BBJ_RETURN)
            {
                continue;
            }

            var range = block;
            var lastNode = range.LastNode;
            if ((lastNode is null) || (lastNode.Oper is not GT_JMP))
            {
                continue;
            }

            for (var lclNum = 0; lclNum < info.compArgsCount; lclNum++)
            {
                var shadowLclNum = shadowInfo[lclNum].ShadowCopy;
                if (shadowLclNum == BAD_VAR_NUM)
                {
                    continue;
                }

                var source = gtNewLclVarNode(TYP_UNDEF, shadowLclNum);
                var store = gtNewStoreLclVarNode(lclNum, source);
                range.InsertBefore(lastNode, LIR.SeqTree(this, store));
            }
        }
    }

    public bool gsCreateShadowingLocals()
    {
        var createdAny = false;
        var shadowInfo = gsShadowVarInfo ?? throw new InvalidOperationException("GS shadow copies require a local table.");

        for (var lclNum = 0; lclNum < gsShadowVarInfoCount; lclNum++)
        {
            shadowInfo[lclNum].ShadowCopy = BAD_VAR_NUM;
            ref var descriptor = ref lvaGetDesc(lclNum);
            if (!ShadowParamVarInfo.MayNeedShadowCopy(in descriptor) ||
                (!descriptor.lvIsPtr && !descriptor.lvIsUnsafeBuffer))
            {
                continue;
            }

            var shadowLclNum = lvaGrabTemp(false, $"V{lclNum:D2} shadow");
            descriptor = ref lvaGetDesc(lclNum);
            ref var shadow = ref lvaGetDesc(shadowLclNum);

            var type = varTypeIsSmall(descriptor.Type) ? TYP_INT : descriptor.Type;
            shadow.Type = type;
            shadow.lvRegStruct = descriptor.lvRegStruct;
            shadow.SetAddressExposed(descriptor.IsAddressExposed, descriptor.AddrExposedReason);
            shadow.lvDoNotEnregister = descriptor.lvDoNotEnregister;
            shadow.lvSingleDefRegCandidate = descriptor.lvSingleDefRegCandidate;
#if DEBUG
            shadow.DoNotEnregisterReason = descriptor.DoNotEnregisterReason;
            shadow.IsDefinedViaAddress = descriptor.IsDefinedViaAddress;
#endif
            if (varTypeIsStruct(type))
            {
                var layout = descriptor.Layout;
                assert(layout is not null);
                lvaSetStruct(shadowLclNum, layout, unsafeValueClsCheck: false);
                shadow.lvIsMultiRegArg = descriptor.lvIsMultiRegArg;
                shadow.lvIsMultiRegRet = descriptor.lvIsMultiRegRet;
                shadow.lvIsMultiRegDest = descriptor.lvIsMultiRegDest;
            }

            shadow.lvIsUnsafeBuffer = descriptor.lvIsUnsafeBuffer;
            shadow.lvIsPtr = descriptor.lvIsPtr;
            if (descriptor.IsNeverNegative)
            {
                shadow.IsNeverNegative = true;
            }

#if DEBUG
            JITDUMP($"Var V{lclNum:D2} is shadow param candidate. Shadow copy is V{shadowLclNum:D2}.\n");
#endif
            shadowInfo[lclNum].ShadowCopy = shadowLclNum;
            createdAny = true;
        }

        return createdAny;
    }

    public void gsRewriteTreeForShadowParam(GenTree tree)
    {
        if (!tree.Oper.IsAnyLocal)
        {
            return;
        }

        var local = tree.AsLclVarCommon();
        var lclNum = local.LclNum;
        if (lclNum >= gsShadowVarInfoCount)
        {
            return;
        }

        var shadowInfo = gsShadowVarInfo ?? throw new InvalidOperationException("GS shadow rewriting requires a local table.");
        var shadowLclNum = shadowInfo[lclNum].ShadowCopy;
        if (shadowLclNum == BAD_VAR_NUM)
        {
            return;
        }

        ref var descriptor = ref lvaGetDesc(lclNum);
        assert(ShadowParamVarInfo.MayNeedShadowCopy(in descriptor));
        local.LclNum = shadowLclNum;
        if (varTypeIsSmall(descriptor.Type))
        {
            if (tree.Oper.IsScalarLocal)
            {
                tree.Type = TYP_INT;
            }
            else if ((tree.Oper is GT_STORE_LCL_FLD) && tree.AsLclFld().IsPartial(this))
            {
                tree.Flags |= GTF_VAR_USEASG;
            }
        }
    }

    public void gsCopyIntoShadow(int lclNum, int shadowLclNum)
    {
        ref var descriptor = ref lvaGetDesc(lclNum);
        if (descriptor.lvPromoted && !descriptor.lvDoNotEnregister)
        {
            lvaSetVarDoNotEnregister(lclNum, DoNotEnregisterReason.BlockOp);
        }

#if TARGET_X86 && FEATURE_IJW
        if ((lclNum < info.compArgsCount) && (descriptor.Type is TYP_STRUCT))
        {
            throw new FatalJitException(CORJIT_SKIPPED, "IJW struct-argument shadow copies require the x86 special-copy helper.");
        }
#endif

        var source = gtNewLclvNode(descriptor.Type, lclNum);
        var store = gtNewStoreLclVarNode(shadowLclNum, source);
        var firstBlock = fgFirstBB ?? throw new InvalidOperationException("GS shadow copies require an entry block.");
        firstBlock.InsertAtBeginning(LIR.SeqTree(this, store));
        JITDUMP($"Created shadow param copy for V{lclNum:D2} to V{shadowLclNum:D2}\n");
    }
}
