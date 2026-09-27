// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Numerics;

namespace RyuJitSharp;

public partial class Compiler
{
    private const int ALLOC_NON_PTRS = 0x1;
    private const int ALLOC_PTRS = 0x2;
    private const int ALLOC_UNSAFE_BUFFERS = 0x4;
    private const int ALLOC_UNSAFE_BUFFERS_WITH_PTRS = 0x8;

    public void lvaIncrementFrameSize(int size)
    {
        if ((size < 0) || (size > MAX_FrameSize) || (compLclFrameSize > MAX_FrameSize - size))
        {
            BADCODE("Frame size overflow");
        }

        compLclFrameSize += size;
    }

    public bool lvaTempsHaveLargerOffsetThanVars()
    {
        assert(codeGen is not null);
        return !compGSReorderStackLayout || codeGen.IsFramePointerUsed;
    }

    public unsafe void lvaAssignVirtualFrameOffsetsToLocals()
    {
#if (!TARGET_AMD64 || UNIX_AMD64_ABI) && !TARGET_ARM64
        throw new FatalJitException(CORJIT_SKIPPED, "Local stack layout requires Windows AMD64 or ARM64.");
#else
        assert(codeGen is not null);
        var stkOffs = 0;
        var originalFrameStkOffs = 0;
        var originalFrameSize = 0;

        if (lvaDoneFrameLayout <= PRE_REGALLOC_FRAME_LAYOUT)
        {
            codeGen.IsFramePointerUsed = codeGen.IsFramePointerRequired;
        }

#if TARGET_AMD64
        stkOffs -= TARGET_POINTER_SIZE;
        if (lvaRetAddrVar != BAD_VAR_NUM)
        {
            lvaTable[lvaRetAddrVar].StackOffset = stkOffs;
        }
#endif

        if (opts.IsOSR)
        {
            assert(info.compPatchpointInfo is not null);
            originalFrameSize = info.compPatchpointInfo->TotalFrameSize;
            originalFrameStkOffs = stkOffs;
            stkOffs -= originalFrameSize;
        }

#if TARGET_AMD64
        if (codeGen.IsFramePointerUsed)
        {
            stkOffs -= REGSIZE_BYTES;
        }
#endif

#if TARGET_ARM64
        // Initially place FP/LR with the other callee saves; fixup may move them below the locals.
        var initialStkOffs = 0;
        if (info.compIsVarArgs)
        {
            initialStkOffs = MAX_REG_ARG * REGSIZE_BYTES;
            stkOffs -= initialStkOffs;
        }
#endif

        stkOffs -= compCalleeRegsPushed * REGSIZE_BYTES;
        compLclFrameSize = 0;
#if TARGET_AMD64
        if (MethodHasPatchpoint)
        {
            // Tier0 reserves the callee-save slots an OSR method may subsequently need.
            var regsPushed = compCalleeRegsPushed + (codeGen.IsFramePointerUsed ? 1 : 0);
            var extraSlots = BitOperations.PopCount((ulong)SRBM_OSR_INT_CALLEE_SAVED) - regsPushed;
            noway_assert(extraSlots >= 0);
            var extraSlotSize = extraSlots * REGSIZE_BYTES;
            JITDUMP($"\nMethod has patchpoints and has {regsPushed} callee saves.\n" +
                $"Reserving {extraSlots} extra slots ({extraSlotSize} bytes) for potential OSR method callee saves\n");
            stkOffs -= extraSlotSize;
            lvaIncrementFrameSize(extraSlotSize);
        }

        var calleeFPRegsSavedSize = BitOperations.PopCount((ulong)compCalleeFPRegsSavedMask) * XMM_REGSIZE_BYTES;
        var offsetForAlign = -(stkOffs + originalFrameSize);
        if ((calleeFPRegsSavedSize > 0) && ((offsetForAlign % XMM_REGSIZE_BYTES) != 0))
        {
            var alignPad = FrameAlignmentPad(offsetForAlign, XMM_REGSIZE_BYTES);
            assert(alignPad != 0);
            stkOffs -= alignPad;
            lvaIncrementFrameSize(alignPad);
        }

        stkOffs -= calleeFPRegsSavedSize;
        lvaIncrementFrameSize(calleeFPRegsSavedSize);
#endif

        if (!opts.IsOSR)
        {
            if (lvaMonAcquired != BAD_VAR_NUM)
            {
                stkOffs = lvaAllocLocalAndSetVirtualOffset(
                    lvaMonAcquired, lvaLclStackHomeSize(lvaMonAcquired), stkOffs);
            }

            stkOffs = lvaAllocAsyncContexts(stkOffs);
        }

        if (lvaReportParamTypeArg())
        {
            if (opts.IsOSR)
            {
                assert(info.compPatchpointInfo is not null);
                assert(info.compPatchpointInfo->HasGenericContextArgOffset);
                lvaCachedGenericContextArgOffs = originalFrameStkOffs
                    + info.compPatchpointInfo->GenericContextArgOffset;
            }
            else
            {
                lvaIncrementFrameSize(TARGET_POINTER_SIZE);
                stkOffs -= TARGET_POINTER_SIZE;
                lvaCachedGenericContextArgOffs = stkOffs;
            }
        }
        else if (lvaKeepAliveAndReportThis())
        {
            var canUseExistingSlot = false;
            if (opts.IsOSR)
            {
                assert(info.compPatchpointInfo is not null);
                if (info.compPatchpointInfo->HasKeptAliveThis)
                {
                    lvaCachedGenericContextArgOffs = originalFrameStkOffs
                        + info.compPatchpointInfo->KeptAliveThisOffset;
                    canUseExistingSlot = true;
                }
            }

            if (!canUseExistingSlot)
            {
                lvaIncrementFrameSize(TARGET_POINTER_SIZE);
                stkOffs -= TARGET_POINTER_SIZE;
                lvaCachedGenericContextArgOffs = stkOffs;
            }
        }

        if (compGSReorderStackLayout)
        {
            assert(NeedsGSSecurityCookie);
            if (!opts.IsOSR || !info.compPatchpointInfo->HasSecurityCookie)
            {
                stkOffs = lvaAllocLocalAndSetVirtualOffset(
                    lvaGSSecurityCookie, lvaLclStackHomeSize(lvaGSSecurityCookie), stkOffs);
            }
        }

        Span<int> allocOrder = stackalloc int[5];
        var count = 0;
        if (compGSReorderStackLayout && codeGen.IsFramePointerUsed)
        {
            allocOrder[count++] = ALLOC_UNSAFE_BUFFERS;
            allocOrder[count++] = ALLOC_UNSAFE_BUFFERS_WITH_PTRS;
        }

        var tempsAllocated = false;
        if (lvaTempsHaveLargerOffsetThanVars() && !codeGen.IsFramePointerUsed)
        {
            stkOffs = lvaAllocateTemps(stkOffs, mustDoubleAlign: false);
            tempsAllocated = true;
        }

        allocOrder[count++] = ALLOC_NON_PTRS;
        if (opts.compDbgEnC)
        {
            allocOrder[count - 1] |= ALLOC_PTRS;
            noway_assert(!compGSReorderStackLayout);
        }
        else
        {
            allocOrder[count++] = ALLOC_PTRS;
        }

        if (!codeGen.IsFramePointerUsed && compGSReorderStackLayout)
        {
            allocOrder[count++] = ALLOC_UNSAFE_BUFFERS_WITH_PTRS;
            allocOrder[count++] = ALLOC_UNSAFE_BUFFERS;
        }

        noway_assert(count < allocOrder.Length);
        var assignMore = -1;
        for (var pass = 0; pass < count; pass++)
        {
            if ((assignMore & allocOrder[pass]) == 0)
            {
                continue;
            }

            assignMore = 0;
            for (var lclNum = 0; lclNum < lvaCount; lclNum++)
            {
                ref var dsc = ref lvaGetDesc(lclNum);
                if (lvaIsFieldOfDependentlyPromotedStruct(in dsc) || (lclNum == lvaOutgoingArgSpaceVar))
                {
                    continue;
                }

                var allocateOnFrame = dsc.lvOnFrame;
                if (dsc.lvRegister && (lvaDoneFrameLayout == REGALLOC_FRAME_LAYOUT) &&
                    ((dsc.Type is not TYP_LONG) || (dsc.OtherReg != REG_STK)))
                {
                    allocateOnFrame = false;
                }

                if (lvaIsOSRLocal(lclNum))
                {
                    var originalOffset = dsc.lvIsStructField
                        ? lvaOSRLocalTier0FrameOffset(dsc.lvParentLcl) + dsc.lvFldOffset
                        : lvaOSRLocalTier0FrameOffset(lclNum);
                    dsc.StackOffset = originalFrameStkOffs + originalOffset;
                    JITDUMP($"---OSR--- V{lclNum:D2} (on tier0 frame) tier0 FP-rel offset {originalOffset} " +
                        $"tier0 frame offset {originalFrameStkOffs} new virt offset {dsc.StackOffset}\n");
                    continue;
                }

                if (!allocateOnFrame)
                {
                    if (!opts.compDbgEnC || (lclNum >= info.compLocalsCount))
                    {
                        continue;
                    }
                }
                else if ((lvaGSSecurityCookie == lclNum) && NeedsGSSecurityCookie)
                {
                    if (opts.IsOSR && info.compPatchpointInfo->HasSecurityCookie)
                    {
                        var originalOffset = info.compPatchpointInfo->SecurityCookieOffset;
                        dsc.StackOffset = originalFrameStkOffs + originalOffset;
                        JITDUMP($"---OSR--- V{lclNum:D2} (on tier0 frame, security cookie) tier0 FP-rel offset " +
                            $"{originalOffset} tier0 frame offset {originalFrameStkOffs} new virt offset {dsc.StackOffset}\n");
                    }
                    continue;
                }
                else if (lvaIsUnknownSizeLocal(lclNum))
                {
#if FEATURE_SIMD && TARGET_ARM64
                    lvaAllocUnknownSizeLocal(lclNum);
                    continue;
#else
                    // On AMD64 all locals have a compile-time-known stack size.
                    throw new FatalJitException(CORJIT_SKIPPED, "AMD64 unknown-size stack local is unsupported.");
#endif
                }

                if (lclNum == lvaRetAddrVar ||
                    lclNum == lvaMonAcquired ||
                    lclNum == lvaResumedIndicator ||
                    lclNum == lvaAsyncThreadObjectVar ||
                    lclNum == lvaAsyncExecutionContextVar ||
                    lclNum == lvaAsyncSynchronizationContextVar)
                {
                    continue;
                }

                if (dsc.lvIsParam)
                {
#if TARGET_ARM64
                    if (info.compIsVarArgs && dsc.lvIsRegArg && (lclNum != info.compRetBuffArg) &&
                        (lclNum != lvaSecretStubArg))
                    {
                        ref readonly var abiInfo = ref lvaGetParameterAbiInfo(
                            dsc.lvIsStructField ? dsc.lvParentLcl : lclNum);
                        var found = false;
                        foreach (var segment in abiInfo.Segments)
                        {
                            if (!segment.IsPassedInRegister ||
                                (dsc.lvIsStructField && (segment.Offset != dsc.lvFldOffset)))
                            {
                                continue;
                            }

                            found = true;
                            var regArgNum = genMapIntRegNumToRegArgNum(segment.Register, info.compCallConv);
                            dsc.StackOffset = -initialStkOffs + (regArgNum * REGSIZE_BYTES);
                            break;
                        }

                        assert(found);
                        continue;
                    }
#endif
                    if (!lvaParamHasLocalStackSpace(lclNum))
                    {
                        continue;
                    }
                }

                if (dsc.lvIsUnsafeBuffer && compGSReorderStackLayout)
                {
                    var allocation = dsc.lvIsPtr ? ALLOC_UNSAFE_BUFFERS_WITH_PTRS : ALLOC_UNSAFE_BUFFERS;
                    if ((allocOrder[pass] & allocation) == 0)
                    {
                        assignMore |= allocation;
                        continue;
                    }
                }
                else if (varTypeIsGC(dsc.Type) && dsc.lvTracked)
                {
                    if ((allocOrder[pass] & ALLOC_PTRS) == 0)
                    {
                        assignMore |= ALLOC_PTRS;
                        continue;
                    }
                }
                else if ((allocOrder[pass] & ALLOC_NON_PTRS) == 0)
                {
                    assignMore |= ALLOC_NON_PTRS;
                    continue;
                }

                stkOffs = lvaAllocLocalAndSetVirtualOffset(lclNum, lvaLclStackHomeSize(lclNum), stkOffs);
#if TARGET_ARM64
                if (dsc.lvIsRegArg && dsc.lvPromoted)
                {
                    for (var field = 0; field < dsc.lvFieldCnt; field++)
                    {
                        ref var fieldDsc = ref lvaGetDesc(dsc.lvFieldLclStart + field);
                        fieldDsc.StackOffset = dsc.StackOffset + fieldDsc.lvFldOffset;
                    }
                }
#endif
            }
        }

        if (NeedsGSSecurityCookie && !compGSReorderStackLayout)
        {
            if (!opts.IsOSR || !info.compPatchpointInfo->HasSecurityCookie)
            {
                stkOffs = lvaAllocLocalAndSetVirtualOffset(
                    lvaGSSecurityCookie, lvaLclStackHomeSize(lvaGSSecurityCookie), stkOffs);
            }
        }

        if (!tempsAllocated)
        {
            stkOffs = lvaAllocateTemps(stkOffs, mustDoubleAlign: false);
        }

        if (lvaOutgoingArgSpaceSize.Value > 0)
        {
#if TARGET_AMD64
            noway_assert(lvaOutgoingArgSpaceSize.Value >= 4 * TARGET_POINTER_SIZE);
#endif
            noway_assert((lvaOutgoingArgSpaceSize.Value % TARGET_POINTER_SIZE) == 0);
            stkOffs = lvaAllocLocalAndSetVirtualOffset(
                lvaOutgoingArgSpaceVar, lvaLclStackHomeSize(lvaOutgoingArgSpaceVar), stkOffs);
        }

        var pushedCount = compCalleeRegsPushed;
#if TARGET_ARM64
        if (info.compIsVarArgs)
        {
            pushedCount += MAX_REG_ARG;
        }
#else
        if (codeGen.IsFramePointerUsed)
        {
            pushedCount++;
        }
        pushedCount++;
#endif
        noway_assert(compLclFrameSize + originalFrameSize == -(stkOffs + (pushedCount * TARGET_POINTER_SIZE)));

#if TARGET_ARM64
        if (opts.compJitSaveFpLrWithCalleeSavedRegisters == 0)
        {
            if (IsTargetAbi(CORINFO_NATIVEAOT_ABI) && TargetOS.IsApplePlatform &&
                (!codeGen.IsFramePointerRequired || (codeGen.genTotalFrameSize < 0x100)))
            {
                codeGen.SetSaveFpLrWithAllCalleeSavedRegisters(true);
            }
            else
            {
                codeGen.SetSaveFpLrWithAllCalleeSavedRegisters(
                    (NeedsGSSecurityCookie && compLocallocUsed) || opts.compDbgEnC ||
                    compStressCompile(STRESS_GENERIC_VARN, 20));
            }
        }
        else if (opts.compJitSaveFpLrWithCalleeSavedRegisters == 1)
        {
            codeGen.SetSaveFpLrWithAllCalleeSavedRegisters(false);
        }
        else if (opts.compJitSaveFpLrWithCalleeSavedRegisters is 2 or 3)
        {
            codeGen.SetSaveFpLrWithAllCalleeSavedRegisters(true);
        }
#endif
#endif
    }

    public int lvaAllocLocalAndSetVirtualOffset(int lclNum, int size, int stkOffs)
    {
#if (!TARGET_AMD64 || UNIX_AMD64_ABI) && !TARGET_ARM64
        throw new FatalJitException(CORJIT_SKIPPED, "Local stack slot assignment requires Windows AMD64 or ARM64.");
#else
        noway_assert(lclNum != BAD_VAR_NUM);
        ref var local = ref lvaGetDesc(lclNum);
        if ((size >= 8) && ((lvaDoneFrameLayout != FINAL_FRAME_LAYOUT) ||
                            ((stkOffs % 8) != 0)
#if FEATURE_SIMD && ALIGN_SIMD_TYPES
                            || varTypeIsSimd(local.Type)
#endif
                            ))
        {
            assert(stkOffs <= 0);
            var pad = 0;
#if FEATURE_SIMD && ALIGN_SIMD_TYPES
            if (varTypeIsSimd(local.Type))
            {
                var alignment = getSIMDTypeAlignment(local.Type);
                if ((stkOffs % alignment) != 0)
                {
                    pad = lvaDoneFrameLayout != FINAL_FRAME_LAYOUT
                        ? alignment - 1
                        : alignment + (stkOffs % alignment);
                }
            }
            else
#endif
            {
                // Tentative offsets must never underestimate the final stack displacement.
                pad = lvaDoneFrameLayout != FINAL_FRAME_LAYOUT ? 7 : 8 + (stkOffs % 8);
            }

            lvaIncrementFrameSize(pad);
            stkOffs -= pad;
#if DEBUG
            if (verbose)
            {
                jitprintf("Pad ");
                gtDispLclVar(lclNum, false);
                jitprintf($", size={size}, stkOffs={(stkOffs < 0 ? '-' : '+')}0x{Math.Abs(stkOffs):x}, pad={pad}\n");
            }
#endif
        }

        lvaIncrementFrameSize(size);
        stkOffs -= size;
        local.StackOffset = stkOffs;
#if DEBUG
        if (verbose)
        {
            jitprintf("Assign ");
            gtDispLclVar(lclNum, false);
            jitprintf($", size={size}, stkOffs={(stkOffs < 0 ? '-' : '+')}0x{Math.Abs(stkOffs):x}\n");
        }
#endif
        return stkOffs;
#endif
    }

    private static int FrameAlignmentPad(int value, int alignment)
    {
        return (alignment - (value % alignment)) % alignment;
    }

    public unsafe int lvaAllocAsyncContexts(int stkOffs)
    {
#if (!TARGET_AMD64 || UNIX_AMD64_ABI) && !TARGET_ARM64
        throw new FatalJitException(CORJIT_SKIPPED, "Async frame contexts require Windows AMD64 or ARM64.");
#else
        if (lvaResumedIndicator != BAD_VAR_NUM)
        {
            stkOffs = lvaAllocLocalAndSetVirtualOffset(
                lvaResumedIndicator, lvaLclStackHomeSize(lvaResumedIndicator), stkOffs);
        }
        else
        {
            assert((info.compMethodInfo->options & CORINFO_ASYNC_SAVE_CONTEXTS) == 0);
        }

        if (lvaAsyncThreadObjectVar != BAD_VAR_NUM)
        {
            stkOffs = lvaAllocLocalAndSetVirtualOffset(
                lvaAsyncThreadObjectVar, lvaLclStackHomeSize(lvaAsyncThreadObjectVar), stkOffs);
        }
        else
        {
            assert((info.compMethodInfo->options & CORINFO_ASYNC_SAVE_CONTEXTS) == 0);
        }

        if (lvaAsyncExecutionContextVar != BAD_VAR_NUM)
        {
            stkOffs = lvaAllocLocalAndSetVirtualOffset(
                lvaAsyncExecutionContextVar, lvaLclStackHomeSize(lvaAsyncExecutionContextVar), stkOffs);
        }
        else
        {
            assert((info.compMethodInfo->options & CORINFO_ASYNC_SAVE_CONTEXTS) == 0);
        }

        if (lvaAsyncSynchronizationContextVar != BAD_VAR_NUM)
        {
            stkOffs = lvaAllocLocalAndSetVirtualOffset(
                lvaAsyncSynchronizationContextVar, lvaLclStackHomeSize(lvaAsyncSynchronizationContextVar), stkOffs);
        }
        else
        {
            assert((info.compMethodInfo->options & CORINFO_ASYNC_SAVE_CONTEXTS) == 0);
        }

        return stkOffs;
#endif
    }

    public void lvaAlignFrame()
    {
#if (!TARGET_AMD64 || UNIX_AMD64_ABI) && !TARGET_ARM64
        throw new FatalJitException(CORJIT_SKIPPED, "Frame alignment requires Windows AMD64 or ARM64.");
#else
        assert(codeGen is not null);
        if ((compLclFrameSize % REGSIZE_BYTES) != 0)
        {
            lvaIncrementFrameSize(REGSIZE_BYTES - (compLclFrameSize % REGSIZE_BYTES));
        }
        else if (lvaDoneFrameLayout != FINAL_FRAME_LAYOUT)
        {
            lvaIncrementFrameSize(REGSIZE_BYTES);
        }

        assert((compLclFrameSize % REGSIZE_BYTES) == 0);
#if TARGET_ARM64
        var regPushedCountAligned = (compCalleeRegsPushed % (STACK_ALIGN / REGSIZE_BYTES)) == 0;
        var lclFrameSizeAligned = (compLclFrameSize % STACK_ALIGN) == 0;
        if ((lvaDoneFrameLayout != FINAL_FRAME_LAYOUT) || (regPushedCountAligned != lclFrameSizeAligned))
        {
            lvaIncrementFrameSize(REGSIZE_BYTES);
        }
#else
        var regPushedCountAligned = lvaIsCalleeSavedIntRegCountEven();
        var lclFrameSizeAligned = (compLclFrameSize % STACK_ALIGN) == 0;
        if ((!codeGen.IsFramePointerUsed && (lvaDoneFrameLayout != FINAL_FRAME_LAYOUT)) ||
            ((compLclFrameSize != 0) && (regPushedCountAligned == lclFrameSizeAligned)))
        {
            lvaIncrementFrameSize(REGSIZE_BYTES);
        }
#endif
#endif
    }

    public int lvaAllocateTemps(int stkOffs, bool mustDoubleAlign)
    {
#if (!TARGET_AMD64 || UNIX_AMD64_ABI) && !TARGET_ARM64
        throw new FatalJitException(CORJIT_SKIPPED, "Spill temp frame layout requires Windows AMD64 or ARM64.");
#else
        if (lvaDoneFrameLayout == FINAL_FRAME_LAYOUT)
        {
            assert(codeGen is not null);
#if DEBUG
            assert(codeGen.RegSet.tmpGetAllFree());
#endif
            for (var temp = codeGen.RegSet.tmpListBeg(); temp is not null; temp = codeGen.RegSet.tmpListNxt(temp))
            {
                if (varTypeHasUnknownSize(temp.tdTempType))
                {
#if FEATURE_SIMD && TARGET_ARM64
                    lvaAllocateUnknownSizeTemp(temp);
                    continue;
#else
                    throw new FatalJitException(CORJIT_SKIPPED, "AMD64 unknown-size spill temp is unsupported.");
#endif
                }

                if (varTypeIsGC(temp.tdTempType) && ((stkOffs % TARGET_POINTER_SIZE) != 0))
                {
                    var pad = FrameAlignmentPad(-stkOffs, TARGET_POINTER_SIZE);
                    lvaIncrementFrameSize(pad);
                    stkOffs -= pad;
                    noway_assert((stkOffs % TARGET_POINTER_SIZE) == 0);
                }

                if (mustDoubleAlign && (temp.tdTempType == TYP_DOUBLE) &&
                    ((stkOffs % (2 * TARGET_POINTER_SIZE)) != 0))
                {
                    lvaIncrementFrameSize(TARGET_POINTER_SIZE);
                    stkOffs -= TARGET_POINTER_SIZE;
                }

                lvaIncrementFrameSize(temp.tdTempSize);
                stkOffs -= temp.tdTempSize;
                temp.tdTempOffs = stkOffs;
            }
        }
        else
        {
            var size = lvaMaxSpillTempSize;
            lvaIncrementFrameSize(size);
            stkOffs -= size;
        }

        return stkOffs;
#endif
    }

    public unsafe int lvaOSRLocalTier0FrameOffset(int varNum)
    {
#if (!TARGET_AMD64 || UNIX_AMD64_ABI) && !TARGET_ARM64
        throw new FatalJitException(CORJIT_SKIPPED, "OSR local frame offsets require Windows AMD64 or ARM64.");
#else
        assert(lvaIsOSRLocal(varNum));
        assert(info.compPatchpointInfo is not null);
        if (varNum == lvaMonAcquired)
        {
            return info.compPatchpointInfo->MonitorAcquiredOffset;
        }

        if (varNum == lvaResumedIndicator)
        {
            return info.compPatchpointInfo->ResumedIndicatorOffset;
        }

        if (varNum == lvaAsyncThreadObjectVar)
        {
            return info.compPatchpointInfo->AsyncThreadOffset;
        }

        if (varNum == lvaAsyncExecutionContextVar)
        {
            return info.compPatchpointInfo->AsyncExecutionContextOffset;
        }

        if (varNum == lvaAsyncSynchronizationContextVar)
        {
            return info.compPatchpointInfo->AsyncSynchronizationContextOffset;
        }

        assert(varNum < info.compPatchpointInfo->NumberOfLocals);
        return info.compPatchpointInfo->Offset(varNum);
#endif
    }
}
