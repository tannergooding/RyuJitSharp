// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace RyuJitSharp;

public partial class Compiler
{
    public bool lvaIsCalleeSavedIntRegCountEven()
    {
        assert(codeGen is not null);
        var regsPushed = compCalleeRegsPushed + (codeGen.IsFramePointerUsed ? 1 : 0);
        return (regsPushed % (16 / Configuration.REGSIZE_BYTES)) == 0;
    }

    public int lvaCachedGenericContextArgOffset()
    {
        assert(lvaDoneFrameLayout == FINAL_FRAME_LAYOUT);
        return lvaCachedGenericContextArgOffs;
    }

    public uint lvaFrameSize(FrameLayoutState curState)
    {
        assert(curState < FINAL_FRAME_LAYOUT);
        if (Configuration.HAS_FIXED_REGISTER_SET)
        {
            assert(codeGen is not null);
            compCalleeRegsPushed = CNT_CALLEE_SAVED;

            if (Configuration.TARGET_ARMARCH || Configuration.TARGET_LOONGARCH64 || Configuration.TARGET_RISCV64)
            {
                if (compFloatingPointUsed)
                {
                    compCalleeRegsPushed += CNT_CALLEE_SAVED_FLOAT;
                }

                // LR/RA is always saved in addition to the ordinary callee-save set.
                compCalleeRegsPushed++;
            }
            else if (Configuration.TARGET_AMD64)
            {
                compCalleeFPRegsSavedMask = compFloatingPointUsed ? TargetRuntimeMetadata.FloatingCalleeSavedMask : SRBM_NONE;
            }

            if (Configuration.DOUBLE_ALIGN && genDoubleAlign)
            {
                // The x86 "and esp, -8" prolog can introduce one additional four-byte slot.
                compCalleeRegsPushed++;
            }

            if (Configuration.TARGET_XARCH && codeGen.IsFramePointerUsed)
            {
                compCalleeRegsPushed--;
            }
        }

        lvaAssignFrameOffsets(curState);
        var calleeSavedRegMaxSz = CALLEE_SAVED_REG_MAXSZ;
        if (Configuration.TARGET_ARMARCH || Configuration.TARGET_LOONGARCH64 || Configuration.TARGET_RISCV64)
        {
            if (compFloatingPointUsed)
            {
                calleeSavedRegMaxSz += CALLEE_SAVED_FLOAT_MAXSZ;
            }

            calleeSavedRegMaxSz += (uint)Configuration.REGSIZE_BYTES;
        }

        return (uint)compLclFrameSize + calleeSavedRegMaxSz;
    }

    public bool compRsvdRegCheck(FrameLayoutState curState)
    {
        if (!Configuration.TARGET_ARMARCH && !Configuration.TARGET_RISCV64)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.ReservedRegisterFrameChecksAreUnavailableForThisTarget
    );
            return default!;
        }

        var frameSize = lvaFrameSize(curState);
        JITDUMP($"\ncompRsvdRegCheck\n  frame size  = {frameSize,6}\n" +
            $"  lvaParameterStackSize = {lvaParameterStackSize,6}\n");

        if (opts.MinOpts)
        {
            JITDUMP(" Returning true (MinOpts)\n\n");
            return true;
        }

        var calleeSavedRegMaxSz = CALLEE_SAVED_REG_MAXSZ;
        if (compFloatingPointUsed)
        {
            calleeSavedRegMaxSz += CALLEE_SAVED_FLOAT_MAXSZ;
        }

        calleeSavedRegMaxSz += (uint)Configuration.REGSIZE_BYTES;
        noway_assert(frameSize >= calleeSavedRegMaxSz);

        if (!Configuration.TARGET_ARM)
        {
            var targetName = Configuration.TARGET_ARM64 ? "ARM64" : "RISCV64";
            JITDUMP($" Returning true ({targetName})\n\n");
            return true;
        }

        assert(codeGen is not null);

        // R11 is the frame pointer: positive offsets reach incoming arguments and negative offsets reach locals.
        // Floating-point loads/stores can reach -0x3FC, but integer locals limit negative R11 offsets to -0xFF.
        var maxR11PositiveEncodingOffset = compFloatingPointUsed ? 0x03FCu : 0x0FFFu;
        JITDUMP($"  maxR11PositiveEncodingOffset     = {maxR11PositiveEncodingOffset,6}\n");

        var maxR11NegativeEncodingOffset = 0x00FFu;
        JITDUMP($"  maxR11NegativeEncodingOffset     = {maxR11NegativeEncodingOffset,6}\n");

        // Exclude the address just beyond the last incoming argument; R11 and LR occupy the first two frame slots.
        var parameterStackSize = unchecked((uint)lvaParameterStackSize);
        var framePointerAndLinkSize = 2 * (uint)Configuration.REGSIZE_BYTES;
        var maxR11PositiveOffset = unchecked(parameterStackSize + framePointerAndLinkSize - 1);
        JITDUMP($"  maxR11PositiveOffset             = {maxR11PositiveOffset,6}\n");

        // The frame size includes R11 and LR, which are at non-negative offsets from R11.
        assert(frameSize >= framePointerAndLinkSize);
        var maxR11NegativeOffset = unchecked(frameSize - framePointerAndLinkSize);
        JITDUMP($"  maxR11NegativeOffset             = {maxR11NegativeOffset,6}\n");

        if (codeGen.IsFramePointerRequired)
        {
            if (maxR11NegativeOffset > maxR11NegativeEncodingOffset)
            {
                JITDUMP(" Returning true (frame required and maxR11NegativeOffset)\n\n");
                return true;
            }

            if (maxR11PositiveOffset > maxR11PositiveEncodingOffset)
            {
                JITDUMP(" Returning true (frame required and maxR11PositiveOffset)\n\n");
                return true;
            }
        }

        var maxSPPositiveEncodingOffset = compFloatingPointUsed ? 0x03FCu : 0x0FFFu;
        JITDUMP($"  maxSPPositiveEncodingOffset      = {maxSPPositiveEncodingOffset,6}\n");

        assert(unchecked(parameterStackSize + frameSize) > 0);
        var maxSPPositiveOffset = unchecked(parameterStackSize + frameSize - 1);

        if (codeGen.IsFramePointerUsed)
        {
            // Split local reachability between SP's positive range and R11's negative range.
            var maxSPLocalsCombinedOffset = unchecked(frameSize - framePointerAndLinkSize - 1);
            JITDUMP($"  maxSPLocalsCombinedOffset        = {maxSPLocalsCombinedOffset,6}\n");

            if (maxSPLocalsCombinedOffset > maxSPPositiveEncodingOffset)
            {
                var maxRemainingLocalsCombinedOffset =
                    unchecked(maxSPLocalsCombinedOffset - maxSPPositiveEncodingOffset);
                JITDUMP($"  maxRemainingLocalsCombinedOffset = {maxRemainingLocalsCombinedOffset,6}\n");

                if (maxRemainingLocalsCombinedOffset > maxR11NegativeEncodingOffset)
                {
                    JITDUMP(" Returning true (frame pointer exists; R11 and SP can't reach entire stack between them)\n\n");
                    return true;
                }
            }

            // Incoming arguments are reachable when either base register can encode their offsets.
            if ((maxR11PositiveOffset > maxR11PositiveEncodingOffset) &&
                (maxSPPositiveOffset > maxSPPositiveEncodingOffset))
            {
                JITDUMP(" Returning true (frame pointer exists; R11 and SP can't reach all arguments)\n\n");
                return true;
            }
        }
        else if (maxSPPositiveOffset > maxSPPositiveEncodingOffset)
        {
            JITDUMP(" Returning true (no frame pointer exists; SP can't reach all of frame)\n\n");
            return true;
        }

        JITDUMP(" Returning false\n\n");
        return false;
    }

    public void lvaAssignFrameOffsets(FrameLayoutState curState)
    {
        noway_assert((lvaDoneFrameLayout < curState) || (curState == REGALLOC_FRAME_LAYOUT));
        lvaDoneFrameLayout = curState;

        if (Configuration.DEBUG && verbose)
        {
            var stateName = curState switch {
                INITIAL_FRAME_LAYOUT => nameof(INITIAL_FRAME_LAYOUT),
                PRE_REGALLOC_FRAME_LAYOUT => nameof(PRE_REGALLOC_FRAME_LAYOUT),
                REGALLOC_FRAME_LAYOUT => nameof(REGALLOC_FRAME_LAYOUT),
                TENTATIVE_FRAME_LAYOUT => nameof(TENTATIVE_FRAME_LAYOUT),
                FINAL_FRAME_LAYOUT => nameof(FINAL_FRAME_LAYOUT),
                _ => null,
            };
            jitprintf($"*************** In lvaAssignFrameOffsets({stateName ?? "UNKNOWN"})");
            if (stateName is null)
            {
                unreached();
            }
            jitprintf("\n");
        }
        if (Configuration.FEATURE_FIXED_OUT_ARGS)
        {
            assert(lvaOutgoingArgSpaceVar != BAD_VAR_NUM);
        }

        if (Configuration.FEATURE_SIMD && Configuration.TARGET_ARM64)
        {
            lvaInitUnknownSizeFrame();
        }

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
        else if (Configuration.FEATURE_SIMD && Configuration.TARGET_ARM64)
        {
            assert(curState == FINAL_FRAME_LAYOUT);
            unkSizeFrame.FinalizeLayout();

            if (compUsesUnknownSizeFrame)
            {
                assert(codeGen is not null);
                JITDUMP("*** Final UnknownSizeFrame ***\n");
                JITDUMP($"Total Size in VL: {unkSizeFrame.VectorBlockSize()}\n");
                JITDUMP($"Vector Count    : {unkSizeFrame.nVector}\n");
                JITDUMP($"Mask Count      : {unkSizeFrame.nMask}\n");
                JITDUMP($"Start offset    : {-codeGen.genTotalFrameSize}\n");
            }
        }
    }

    public void lvaAssignVirtualFrameOffsetsToArgs()
    {
        var relativeZero = 0;
        if (Configuration.TARGET_ARM)
        {
            assert(codeGen is not null);
            var prespilled = codeGen.RegSet.rsMaskPreSpillRegs(true);
            JITDUMP("Prespill regs is ");
            if (Configuration.DEBUG && verbose)
            {
                dspRegMask(prespilled);
            }
            JITDUMP("\n");
            relativeZero = BitOperations.PopCount(unchecked((ulong)prespilled.IntRegSet)) *
                Configuration.TARGET_POINTER_SIZE;
        }

        for (var lclNum = 0; lclNum < info.compArgsCount; lclNum++)
        {
            if (!lvaGetRelativeOffsetToCallerAllocatedSpaceForParameter(lclNum, out var startOffset))
            {
                continue;
            }

            assert(!lvaLocalIsOnUnknownSizeFrame(lclNum));
            ref var dsc = ref lvaGetDesc(lclNum);
            dsc.StackOffset = startOffset + relativeZero;
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
    }

    public bool lvaGetRelativeOffsetToCallerAllocatedSpaceForParameter(int lclNum, out int offset)
    {
        ref readonly var abiInfo = ref lvaGetParameterAbiInfo(lclNum);
        foreach (var segment in abiInfo.Segments)
        {
            if (!segment.IsPassedOnStack)
            {
                if (Configuration.WINDOWS_AMD64_ABI)
                {
                    if (AbiPassingInformation.GetShadowSpaceCallerOffsetForReg(segment.Register, out offset))
                    {
                        return true;
                    }
                }
                else if (Configuration.TARGET_ARM)
                {
                    assert(codeGen is not null);
                    var prespills = codeGen.RegSet.rsMaskPreSpillRegs(true);
                    if ((prespills & genRegMask(segment.Register)) != RBM_NONE)
                    {
                        var higherMask = new regMaskTP(unchecked((regMask)~((1UL << (int)segment.Register) - 1)));
                        var higherPrespills = prespills & higherMask;
                        offset = -BitOperations.PopCount(unchecked((ulong)higherPrespills.IntRegSet))
                            * Configuration.TARGET_POINTER_SIZE;
                        offset -= segment.Offset;

                        return true;
                    }
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
    }

    public bool lvaIsPreSpilled(int lclNum, regMaskTP preSpillMask)
    {
        if (!Configuration.TARGET_ARM)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.PreSpilledRegisterArgumentsAreUnavailableForThisTarget
    );
            return default!;
        }

        ref var dsc = ref lvaGetDesc(lclNum);
        if (dsc.lvIsStructField)
        {
            lclNum = dsc.lvParentLcl;
        }

        ref readonly var abiInfo = ref lvaGetParameterAbiInfo(lclNum);
        foreach (var segment in abiInfo.Segments)
        {
            if (segment.IsPassedInRegister &&
                (preSpillMask & new regMaskTP(
                    (regMask)((ulong)segment.RegisterMask << segment.RegisterMaskBase))) != RBM_NONE)
            {
                return true;
            }
        }
        return false;
    }

    public bool lvaParamHasLocalStackSpace(int lclNum)
    {
        ref var dsc = ref lvaGetDesc(lclNum);
        if (Configuration.SWIFT_SUPPORT
            && (info.compCallConv == CorInfoCallConvExtension.Swift)
            && !lvaIsImplicitByRefLocal(lclNum)
            && !lvaGetParameterAbiInfo(lclNum).HasExactlyOneStackSegment)
        {
            return true;
        }

        if (Configuration.WINDOWS_AMD64_ABI)
        {
            var paramLclNum = dsc.lvIsStructField ? dsc.lvParentLcl : lclNum;
            return !lvaGetRelativeOffsetToCallerAllocatedSpaceForParameter(paramLclNum, out _);
        }

        if (!dsc.lvIsRegArg)
        {
            return false;
        }
        if (Configuration.TARGET_ARM)
        {
            assert(codeGen is not null);
            if (!lvaIsPreSpilled(lclNum, codeGen.RegSet.rsMaskPreSpillRegs(false)))
            {
                return true;
            }

            if (Configuration.DEBUG)
            {
                assert(dsc.StackOffset != BAD_STK_OFFS);
            }

            return false;
        }
        return true;
    }

    [SuppressMessage("Maintainability", "CA1508:Avoid dead conditional code",
        Justification = "Native ARM64 relocation deltas remain zero on AMD64.")]
    public unsafe void lvaFixVirtualFrameOffsets()
    {
        assert(codeGen is not null);
        var frameLocalsDelta = 0;
        var frameBoundary = 0;
        var delta = 0;
        if (Configuration.TARGET_XARCH)
        {
            delta = Configuration.REGSIZE_BYTES;
            JITDUMP($"--- delta bump {Configuration.REGSIZE_BYTES} for RA\n");

            if (lvaDoubleAlignOrFramePointerUsed())
            {
                JITDUMP($"--- delta bump {Configuration.REGSIZE_BYTES} for FP\n");
                delta += Configuration.REGSIZE_BYTES;
            }
        }

        if (!codeGen.IsFramePointerUsed)
        {
            JITDUMP($"--- delta bump {codeGen.genTotalFrameSize} for RSP frame\n");
            delta += codeGen.genTotalFrameSize;
        }
        else if (Configuration.TARGET_ARM)
        {
            delta += 2 * Configuration.REGSIZE_BYTES;
        }
        else if (Configuration.TARGET_ARM64)
        {
            delta += codeGen.genTotalFrameSize - codeGen.genSPtoFPdelta;
            if (!codeGen.IsSaveFpLrWithAllCalleeSavedRegisters)
            {
                frameLocalsDelta = 2 * Configuration.REGSIZE_BYTES;
                frameBoundary = opts.IsOSR ? -info.compPatchpointInfo->TotalFrameSize : 0;
                if (info.compIsVarArgs)
                {
                    frameBoundary -= Target.ARM64_MAX_REG_ARG * Configuration.REGSIZE_BYTES;
                }
            }

            JITDUMP($"--- delta bump {delta} for FP frame, {frameLocalsDelta} inside frame for FP/LR relocation\n");
        }
        else if (Configuration.TARGET_AMD64)
        {
            JITDUMP($"--- delta bump {codeGen.genTotalFrameSize - codeGen.genSPtoFPdelta} for FP frame\n");
            delta += codeGen.genTotalFrameSize - codeGen.genSPtoFPdelta;
        }
        else if (Configuration.TARGET_LOONGARCH64 || Configuration.TARGET_RISCV64)
        {
            delta += compCalleeRegsPushed << 3;
            if ((lvaMonAcquired != BAD_VAR_NUM) && !opts.IsOSR)
            {
                lvaTable[lvaMonAcquired].StackOffset += compCalleeRegsPushed << 3;
                delta += lvaLclStackHomeSize(lvaMonAcquired);
            }
            JITDUMP($"--- delta bump {delta} for FP frame\n");
        }
        else if (Configuration.TARGET_WASM)
        {
            JITDUMP($"--- delta bump {codeGen.genTotalFrameSize} for FP frame\n");
            delta += codeGen.genTotalFrameSize;
        }

        if ((Configuration.TARGET_AMD64 || Configuration.TARGET_ARM64) && opts.IsOSR)
        {
            assert(info.compPatchpointInfo is not null);
            JITDUMP($"--- delta bump {info.compPatchpointInfo->TotalFrameSize} for OSR + Tier0 frame\n");
            delta += info.compPatchpointInfo->TotalFrameSize;
        }

        JITDUMP($"--- virtual stack offset to actual stack offset delta is {delta}\n");
        for (var lclNum = 0; lclNum < lvaCount; lclNum++)
        {
            ref var dsc = ref lvaGetDesc(lclNum);
            noway_assert(!dsc.lvFramePointerBased || lvaDoubleAlignOrFramePointerUsed());
            if (lvaLocalIsOnUnknownSizeFrame(lclNum))
            {
                continue;
            }

            var doAssignStkOffs = true;
            if (dsc.lvIsStructField)
            {
                ref var parent = ref lvaGetDesc(dsc.lvParentLcl);
                var promotionType = lvaGetPromotionType(in parent);
                if ((!dsc.lvIsParam || (Configuration.TARGET_X86 && parent.lvIsParam)) &&
                    (promotionType == PROMOTION_TYPE_DEPENDENT))
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
                if (Configuration.DOUBLE_ALIGN && genDoubleAlign && !codeGen.IsFramePointerUsed &&
                    dsc.lvFramePointerBased)
                {
                    dsc.StackOffset -= localDelta;
                    dsc.StackOffset += 2 * Configuration.TARGET_POINTER_SIZE;
                    noway_assert(dsc.StackOffset >= (2 * Configuration.TARGET_POINTER_SIZE));
                }
                assert(codeGen.IsFramePointerUsed || (dsc.StackOffset >= 0));
            }
        }

        assert(codeGen.RegSet.tmpGetAllFree());
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
        if (Configuration.FEATURE_FIXED_OUT_ARGS && (lvaOutgoingArgSpaceVar != BAD_VAR_NUM))
        {
            ref var outgoing = ref lvaGetDesc(lvaOutgoingArgSpaceVar);
            outgoing.StackOffset = 0;
            outgoing.lvFramePointerBased = false;
            outgoing.lvMustInit = false;
        }
        if (Configuration.TARGET_ARM64 || Configuration.TARGET_LOONGARCH64 || Configuration.TARGET_RISCV64)
        {
            assert(codeGen.IsFramePointerUsed);
            if (lvaRetAddrVar != BAD_VAR_NUM)
            {
                lvaTable[lvaRetAddrVar].StackOffset = Configuration.REGSIZE_BYTES;
            }
        }
    }

    public void lvaAssignFrameOffsetsToPromotedStructs()
    {
        var mustProcessParams = Configuration.UNIX_AMD64_ABI
            || Configuration.TARGET_ARM
            || Configuration.TARGET_X86
            || Configuration.TARGET_WASM
            || opts.IsOSR
            || (info.compCallConv == CorInfoCallConvExtension.Swift);

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
                    $"parent {unchecked((uint)parent.StackOffset)} field {dsc.lvFldOffset} " +
                    $"net {unchecked((uint)(parent.StackOffset + dsc.lvFldOffset))}\n");
                dsc.StackOffset = parent.StackOffset + dsc.lvFldOffset;
            }
            else
            {
                dsc.lvOnFrame = false;
                noway_assert(dsc.lvRefCnt() == 0);
            }
        }
    }

    private bool lvaDoubleAlignOrFramePointerUsed()
    {
        assert(codeGen is not null);
        return codeGen.IsFramePointerUsed || (Configuration.DOUBLE_ALIGN && genDoubleAlign);
    }
}
