// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics.CodeAnalysis;

namespace RyuJitSharp;

public partial class Compiler
{
    public bool lvaIsCalleeSavedIntRegCountEven()
    {
        assert(codeGen is not null);
        var regsPushed = compCalleeRegsPushed + (codeGen.IsFramePointerUsed ? 1 : 0);
        return (regsPushed % (16 / REGSIZE_BYTES)) == 0;
    }

    public int lvaCachedGenericContextArgOffset()
    {
        assert(lvaDoneFrameLayout == FINAL_FRAME_LAYOUT);
        return lvaCachedGenericContextArgOffs;
    }

    public uint lvaFrameSize(FrameLayoutState curState)
    {
#if (TARGET_AMD64 && !UNIX_AMD64_ABI) || TARGET_ARM64
        assert(curState < FINAL_FRAME_LAYOUT);
        assert(codeGen is not null);
        compCalleeRegsPushed = CNT_CALLEE_SAVED;
#if TARGET_ARM64
        if (compFloatingPointUsed)
        {
            compCalleeRegsPushed += CNT_CALLEE_SAVED_FLOAT;
        }

        compCalleeRegsPushed++;
#else
        compCalleeFPRegsSavedMask = compFloatingPointUsed ? SRBM_FLT_CALLEE_SAVED : SRBM_NONE;
        if (codeGen.IsFramePointerUsed)
        {
            compCalleeRegsPushed--;
        }
#endif

        lvaAssignFrameOffsets(curState);
        uint calleeSavedRegMaxSz = CALLEE_SAVED_REG_MAXSZ;
#if TARGET_ARM64
        if (compFloatingPointUsed)
        {
            calleeSavedRegMaxSz += CALLEE_SAVED_FLOAT_MAXSZ;
        }

        calleeSavedRegMaxSz += REGSIZE_BYTES;
#endif

        return (uint)compLclFrameSize + calleeSavedRegMaxSz;
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Frame size estimation requires Windows AMD64 or ARM64.");
#endif
    }

#if TARGET_ARM64
    public bool compRsvdRegCheck(FrameLayoutState curState)
    {
        var frameSize = lvaFrameSize(curState);
        JITDUMP($"\ncompRsvdRegCheck\n  frame size  = {frameSize,6}\n" +
            $"  lvaParameterStackSize = {lvaParameterStackSize,6}\n");

        if (opts.MinOpts)
        {
            JITDUMP(" Returning true (MinOpts)\n\n");
            return true;
        }

        uint calleeSavedRegMaxSz = CALLEE_SAVED_REG_MAXSZ;
        if (compFloatingPointUsed)
        {
            calleeSavedRegMaxSz += CALLEE_SAVED_FLOAT_MAXSZ;
        }

        calleeSavedRegMaxSz += REGSIZE_BYTES;
        noway_assert(frameSize >= calleeSavedRegMaxSz);
        JITDUMP(" Returning true (ARM64)\n\n");
        return true;
    }
#endif

    public void lvaAssignFrameOffsets(FrameLayoutState curState)
    {
#if (!TARGET_AMD64 || UNIX_AMD64_ABI) && !TARGET_ARM64
        throw new FatalJitException(CORJIT_SKIPPED, "Frame layout requires Windows AMD64 or ARM64.");
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
#if FEATURE_SIMD && TARGET_ARM64
        lvaInitUnknownSizeFrame();
#endif
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
#if FEATURE_SIMD && TARGET_ARM64
        else
        {
            assert(curState == FINAL_FRAME_LAYOUT);
            unkSizeFrame.FinalizeLayout();
        }
#endif
#endif
    }

    public void lvaAssignVirtualFrameOffsetsToArgs()
    {
#if (!TARGET_AMD64 || UNIX_AMD64_ABI) && !TARGET_ARM64
        throw new FatalJitException(CORJIT_SKIPPED, "Argument frame layout requires Windows AMD64 or ARM64.");
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
#if (!TARGET_AMD64 || UNIX_AMD64_ABI) && !TARGET_ARM64
        throw new FatalJitException(CORJIT_SKIPPED, "Caller-allocated argument homes require Windows AMD64 or ARM64.");
#else
        ref readonly var abiInfo = ref lvaGetParameterAbiInfo(lclNum);
        foreach (var segment in abiInfo.Segments)
        {
            if (!segment.IsPassedOnStack)
            {
#if TARGET_AMD64
                if (AbiPassingInformation.GetShadowSpaceCallerOffsetForReg(segment.Register, out offset))
                {
                    return true;
                }
#endif

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
#if (!TARGET_AMD64 || UNIX_AMD64_ABI) && !TARGET_ARM64
        throw new FatalJitException(CORJIT_SKIPPED, "Parameter stack-home selection requires Windows AMD64 or ARM64.");
#else
        ref var dsc = ref lvaGetDesc(lclNum);
#if SWIFT_SUPPORT
        if ((info.compCallConv == CorInfoCallConvExtension.Swift) && !lvaIsImplicitByRefLocal(lclNum) &&
            !lvaGetParameterAbiInfo(lclNum).HasExactlyOneStackSegment)
        {
            return true;
        }
#endif
#if TARGET_AMD64
        var paramLclNum = dsc.lvIsStructField ? dsc.lvParentLcl : lclNum;
        return !lvaGetRelativeOffsetToCallerAllocatedSpaceForParameter(paramLclNum, out _);
#else
        return dsc.lvIsRegArg;
#endif
#endif
    }

    [SuppressMessage("Maintainability", "CA1508:Avoid dead conditional code",
        Justification = "Native ARM64 relocation deltas remain zero on AMD64.")]
    public unsafe void lvaFixVirtualFrameOffsets()
    {
#if (!TARGET_AMD64 || UNIX_AMD64_ABI) && !TARGET_ARM64
        throw new FatalJitException(CORJIT_SKIPPED, "Final frame offsets require Windows AMD64 or ARM64.");
#else
        assert(codeGen is not null);
        var frameLocalsDelta = 0;
        var frameBoundary = 0;
#if TARGET_AMD64
        var delta = REGSIZE_BYTES;
        JITDUMP($"--- delta bump {REGSIZE_BYTES} for RA\n");

        if (codeGen.IsFramePointerUsed)
        {
            JITDUMP($"--- delta bump {REGSIZE_BYTES} for FP\n");
            delta += REGSIZE_BYTES;
        }
#else
        var delta = 0;
#endif

        if (!codeGen.IsFramePointerUsed)
        {
            JITDUMP($"--- delta bump {codeGen.genTotalFrameSize} for RSP frame\n");
            delta += codeGen.genTotalFrameSize;
        }
        else
        {
#if TARGET_ARM64
            delta += codeGen.genTotalFrameSize - codeGen.genSPtoFPdelta;
            if (!codeGen.IsSaveFpLrWithAllCalleeSavedRegisters)
            {
                frameLocalsDelta = 2 * REGSIZE_BYTES;
                frameBoundary = opts.IsOSR ? -info.compPatchpointInfo->TotalFrameSize : 0;
                if (info.compIsVarArgs)
                {
                    frameBoundary -= MAX_REG_ARG * REGSIZE_BYTES;
                }
            }

            JITDUMP($"--- delta bump {delta} for FP frame, {frameLocalsDelta} inside frame for FP/LR relocation\n");
#else
            JITDUMP($"--- delta bump {codeGen.genTotalFrameSize - codeGen.genSPtoFPdelta} for FP frame\n");
            delta += codeGen.genTotalFrameSize - codeGen.genSPtoFPdelta;
#endif
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
                var localDelta = delta;
                if ((frameLocalsDelta != 0) && (dsc.StackOffset < frameBoundary))
                {
                    localDelta += frameLocalsDelta;
                }

                JITDUMP($"-- V{lclNum:D2} was {dsc.StackOffset}, now {dsc.StackOffset + localDelta}\n");
                dsc.StackOffset += localDelta;
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
                temp.tdAdjustTempOffs(delta + frameLocalsDelta);
            }
        }

        if (lvaCachedGenericContextArgOffs < frameBoundary)
        {
            lvaCachedGenericContextArgOffs += frameLocalsDelta;
        }

        lvaCachedGenericContextArgOffs += delta;
        if (lvaOutgoingArgSpaceVar != BAD_VAR_NUM)
        {
            ref var outgoing = ref lvaGetDesc(lvaOutgoingArgSpaceVar);
            outgoing.StackOffset = 0;
            outgoing.lvFramePointerBased = false;
            outgoing.lvMustInit = false;
        }
#if TARGET_ARM64
        assert(codeGen.IsFramePointerUsed);
        if (lvaRetAddrVar != BAD_VAR_NUM)
        {
            lvaTable[lvaRetAddrVar].StackOffset = REGSIZE_BYTES;
        }
#endif
#endif
    }

    public void lvaAssignFrameOffsetsToPromotedStructs()
    {
#if (!TARGET_AMD64 || UNIX_AMD64_ABI) && !TARGET_ARM64
        throw new FatalJitException(CORJIT_SKIPPED, "Promoted struct frame offsets require Windows AMD64 or ARM64.");
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
