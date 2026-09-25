// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public int lvaCachedGenericContextArgOffset()
    {
        assert(lvaDoneFrameLayout == FINAL_FRAME_LAYOUT);
        return lvaCachedGenericContextArgOffs;
    }

    public void lvaAssignFrameOffsets(FrameLayoutState curState)
    {
#if !TARGET_AMD64 || UNIX_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Frame layout requires Windows AMD64.");
#else
        noway_assert((lvaDoneFrameLayout < curState) || (curState == REGALLOC_FRAME_LAYOUT));
        lvaDoneFrameLayout = curState;

#if DEBUG
        if (verbose)
        {
            jitprintf($"*************** In lvaAssignFrameOffsets({curState})\n");
        }
#endif
        assert(lvaOutgoingArgSpaceVar != BAD_VAR_NUM);
        lvaAssignVirtualFrameOffsetsToArgs();
        lvaAssignVirtualFrameOffsetsToLocals();
        lvaAlignFrame();
        lvaFixVirtualFrameOffsets();
        lvaAssignFrameOffsetsToPromotedStructs();

        if (curState < FINAL_FRAME_LAYOUT)
        {
            assert(codeGen is not null);
            ((CodeGen)codeGen).resetFramePointerUsedWritePhase();
        }
#endif
    }

    public void lvaAssignVirtualFrameOffsetsToArgs()
    {
#if !TARGET_AMD64 || UNIX_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Argument frame layout requires Windows AMD64.");
#else
        for (var lclNum = 0; lclNum < info.compArgsCount; lclNum++)
        {
            if (!lvaGetRelativeOffsetToCallerAllocatedSpaceForParameter(lclNum, out var startOffset))
            {
                continue;
            }

            assert(!lvaIsUnknownSizeLocal(lclNum));
            ref var dsc = ref lvaGetDesc(lclNum);
            dsc.StackOffset = startOffset;
            JITDUMP($"Set V{lclNum:D2} to offset {startOffset}\n");

            if (dsc.lvPromoted)
            {
                for (var field = 0; field < dsc.lvFieldCnt; field++)
                {
                    var fieldLclNum = dsc.lvFieldLclStart + field;
                    ref var fieldDsc = ref lvaGetDesc(fieldLclNum);
                    fieldDsc.StackOffset = dsc.StackOffset + fieldDsc.lvFldOffset;
                    JITDUMP($"  Set field V{fieldLclNum:D2} to offset {fieldDsc.StackOffset}\n");
                }
            }
        }
#endif
    }

    public bool lvaGetRelativeOffsetToCallerAllocatedSpaceForParameter(int lclNum, out int offset)
    {
#if !TARGET_AMD64 || UNIX_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Caller-allocated argument homes require Windows AMD64.");
#else
        ref readonly var abiInfo = ref lvaGetParameterAbiInfo(lclNum);
        foreach (var segment in abiInfo.Segments)
        {
            if (!segment.IsPassedOnStack)
            {
                if (AbiPassingInformation.GetShadowSpaceCallerOffsetForReg(segment.Register, out offset))
                {
                    return true;
                }

                continue;
            }

            if (info.compArgOrder == Target.ArgOrder.ARG_ORDER_L2R)
            {
                assert(segment.Offset == 0);
                offset = lvaParameterStackSize - segment.StackOffset;
            }
            else
            {
                offset = segment.StackOffset - segment.Offset;
            }

            return true;
        }

        offset = 0;
        return false;
#endif
    }

    public bool lvaParamHasLocalStackSpace(int lclNum)
    {
#if !TARGET_AMD64 || UNIX_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Parameter stack-home selection requires Windows AMD64.");
#else
        ref var dsc = ref lvaGetDesc(lclNum);
#if SWIFT_SUPPORT
        if ((info.compCallConv == CorInfoCallConvExtension.Swift) && !lvaIsImplicitByRefLocal(lclNum) &&
            !lvaGetParameterAbiInfo(lclNum).HasExactlyOneStackSegment)
        {
            return true;
        }
#endif
        var paramLclNum = dsc.lvIsStructField ? dsc.lvParentLcl : lclNum;
        return !lvaGetRelativeOffsetToCallerAllocatedSpaceForParameter(paramLclNum, out _);
#endif
    }

    public unsafe void lvaFixVirtualFrameOffsets()
    {
#if !TARGET_AMD64 || UNIX_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Final frame offsets require Windows AMD64.");
#else
        assert(codeGen is not null);
        var delta = REGSIZE_BYTES;
        JITDUMP($"--- delta bump {REGSIZE_BYTES} for RA\n");

        if (codeGen.IsFramePointerUsed)
        {
            JITDUMP($"--- delta bump {REGSIZE_BYTES} for FP\n");
            delta += REGSIZE_BYTES;
        }

        if (!codeGen.IsFramePointerUsed)
        {
            JITDUMP($"--- delta bump {codeGen.genTotalFrameSize} for RSP frame\n");
            delta += codeGen.genTotalFrameSize;
        }
        else
        {
            JITDUMP($"--- delta bump {codeGen.genTotalFrameSize - codeGen.genSPtoFPdelta} for FP frame\n");
            delta += codeGen.genTotalFrameSize - codeGen.genSPtoFPdelta;
        }

        if (opts.IsOSR)
        {
            assert(info.compPatchpointInfo is not null);
            JITDUMP($"--- delta bump {info.compPatchpointInfo->TotalFrameSize} for OSR + Tier0 frame\n");
            delta += info.compPatchpointInfo->TotalFrameSize;
        }

        JITDUMP($"--- virtual stack offset to actual stack offset delta is {delta}\n");
        for (var lclNum = 0; lclNum < lvaCount; lclNum++)
        {
            ref var dsc = ref lvaGetDesc(lclNum);
            noway_assert(!dsc.lvFramePointerBased || codeGen.IsFramePointerUsed);
            if (lvaIsUnknownSizeLocal(lclNum))
            {
                continue;
            }

            var doAssignStkOffs = true;
            if (dsc.lvIsStructField)
            {
                ref var parent = ref lvaGetDesc(dsc.lvParentLcl);
                if (!dsc.lvIsParam && (lvaGetPromotionType(in parent) == PROMOTION_TYPE_DEPENDENT))
                {
                    doAssignStkOffs = false;
                }
            }

            if (!dsc.lvOnFrame && (!dsc.lvIsParam || lvaParamHasLocalStackSpace(lclNum)))
            {
                doAssignStkOffs = false;
            }

            if (doAssignStkOffs)
            {
                JITDUMP($"-- V{lclNum:D2} was {dsc.StackOffset}, now {dsc.StackOffset + delta}\n");
                dsc.StackOffset += delta;
                assert(codeGen.IsFramePointerUsed || (dsc.StackOffset >= 0));
            }
        }

#if DEBUG
        assert(codeGen.RegSet.tmpGetAllFree());
#endif
        for (var temp = codeGen.RegSet.tmpListBeg(); temp is not null; temp = codeGen.RegSet.tmpListNxt(temp))
        {
            if (!varTypeHasUnknownSize(temp.tdTempType))
            {
                temp.tdAdjustTempOffs(delta);
            }
        }

        lvaCachedGenericContextArgOffs += delta;
        if (lvaOutgoingArgSpaceVar != BAD_VAR_NUM)
        {
            ref var outgoing = ref lvaGetDesc(lvaOutgoingArgSpaceVar);
            outgoing.StackOffset = 0;
            outgoing.lvFramePointerBased = false;
            outgoing.lvMustInit = false;
        }
#endif
    }

    public void lvaAssignFrameOffsetsToPromotedStructs()
    {
#if !TARGET_AMD64 || UNIX_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Promoted struct frame offsets require Windows AMD64.");
#else
        var mustProcessParams = opts.IsOSR || (info.compCallConv == CorInfoCallConvExtension.Swift);
        for (var lclNum = 0; lclNum < lvaCount; lclNum++)
        {
            ref var dsc = ref lvaGetDesc(lclNum);
            if (!dsc.lvIsStructField || (dsc.lvIsParam && !mustProcessParams))
            {
                continue;
            }

            ref var parent = ref lvaGetDesc(dsc.lvParentLcl);
            var promotionType = lvaGetPromotionType(in parent);
            if (promotionType == PROMOTION_TYPE_INDEPENDENT)
            {
                continue;
            }

            noway_assert(promotionType == PROMOTION_TYPE_DEPENDENT);
            noway_assert(dsc.lvOnFrame);
            if (parent.lvOnFrame)
            {
                JITDUMP($"Adjusting offset of dependent V{lclNum:D2} of V{dsc.lvParentLcl:D2}: " +
                    $"parent {parent.StackOffset} field {dsc.lvFldOffset} net {parent.StackOffset + dsc.lvFldOffset}\n");
                dsc.StackOffset = parent.StackOffset + dsc.lvFldOffset;
            }
            else
            {
                dsc.lvOnFrame = false;
                noway_assert(dsc.lvRefCnt() == 0);
            }
        }
#endif
    }
}
