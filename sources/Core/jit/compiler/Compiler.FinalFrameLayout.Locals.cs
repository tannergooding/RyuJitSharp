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
#if TARGET_ARM
        // ARM always places temps at lower stack addresses than variables.
        return false;
#else
        assert(codeGen is not null);
        return !compGSReorderStackLayout || codeGen.IsFramePointerUsed;
#endif
    }

    public unsafe void lvaAssignVirtualFrameOffsetsToLocals()
    {
        assert(codeGen is not null);
        var stkOffs = 0;
        var originalFrameStkOffs = 0;
        var originalFrameSize = 0;

        if (lvaDoneFrameLayout <= PRE_REGALLOC_FRAME_LAYOUT)
        {
            codeGen.IsFramePointerUsed = codeGen.IsFramePointerRequired;
        }

#if TARGET_XARCH
        stkOffs -= TARGET_POINTER_SIZE;
        if (lvaRetAddrVar != BAD_VAR_NUM)
        {
            lvaTable[lvaRetAddrVar].StackOffset = stkOffs;
        }
#endif

        if (opts.IsOSR)
        {
            assert(info.compPatchpointInfo is not null);
#if TARGET_LOONGARCH64 || TARGET_RISCV64
            originalFrameStkOffs = info.compPatchpointInfo->TotalFrameSize;
#else
            originalFrameSize = info.compPatchpointInfo->TotalFrameSize;
            originalFrameStkOffs = stkOffs;
            stkOffs -= originalFrameSize;
#endif
        }

#if TARGET_XARCH
        if (lvaDoubleAlignOrFramePointerUsed())
        {
            stkOffs -= REGSIZE_BYTES;
        }
#endif

        var preSpillSize = 0;
        var mustDoubleAlign = false;
#if TARGET_ARM
        mustDoubleAlign = true;
        preSpillSize = BitOperations.PopCount(
            unchecked((ulong)codeGen.RegSet.rsMaskPreSpillRegs(true).IntRegSet)) * REGSIZE_BYTES;
#elif DOUBLE_ALIGN
        mustDoubleAlign = genDoubleAlign;
#endif

#if TARGET_ARM64
        // Initially place FP/LR with the other callee saves; fixup may move them below the locals.
        var initialStkOffs = 0;
        if (info.compIsVarArgs)
        {
            initialStkOffs = MAX_REG_ARG * REGSIZE_BYTES;
            stkOffs -= initialStkOffs;
        }
        stkOffs -= compCalleeRegsPushed * REGSIZE_BYTES;
#elif TARGET_LOONGARCH64 || TARGET_RISCV64
        assert(compCalleeRegsPushed >= 2);
        stkOffs -= compCalleeRegsPushed << 3;
#elif HAS_FIXED_REGISTER_SET
#if TARGET_ARM
        if (lvaRetAddrVar != BAD_VAR_NUM)
        {
            lvaTable[lvaRetAddrVar].StackOffset = stkOffs - REGSIZE_BYTES;
        }
#endif
        stkOffs -= compCalleeRegsPushed * REGSIZE_BYTES;
#endif

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

#if !JIT32_GCENCODER
            stkOffs = lvaAllocAsyncContexts(stkOffs);
#endif
        }

        if (mustDoubleAlign)
        {
            if (lvaDoneFrameLayout != FINAL_FRAME_LAYOUT)
            {
                lvaIncrementFrameSize(TARGET_POINTER_SIZE);
                stkOffs -= TARGET_POINTER_SIZE;
                lvaIncrementFrameSize(TARGET_POINTER_SIZE);
                stkOffs -= TARGET_POINTER_SIZE;
            }
            else
            {
                if (((stkOffs + preSpillSize) % (2 * TARGET_POINTER_SIZE)) != 0)
                {
                    lvaIncrementFrameSize(TARGET_POINTER_SIZE);
                    stkOffs -= TARGET_POINTER_SIZE;
                }
                noway_assert(((stkOffs + preSpillSize) % (2 * TARGET_POINTER_SIZE)) == 0);
            }
        }

#if JIT32_GCENCODER
        if (lvaLocAllocSPvar != BAD_VAR_NUM)
        {
            noway_assert(codeGen.IsFramePointerUsed);
            stkOffs = lvaAllocLocalAndSetVirtualOffset(lvaLocAllocSPvar, TARGET_POINTER_SIZE, stkOffs);
        }
#endif

        if (lvaReportParamTypeArg())
        {
#if JIT32_GCENCODER
            noway_assert(codeGen.IsFramePointerUsed);
#endif
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
#if !JIT32_GCENCODER
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
#endif

#if JIT32_GCENCODER
        assert(!opts.IsOSR);
        stkOffs = lvaAllocAsyncContexts(stkOffs);
#endif

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
            stkOffs = lvaAllocateTemps(stkOffs, mustDoubleAlign);
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
        var haveLocalDoubleAlign = false;
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
                if (lvaIsFieldOfDependentlyPromotedStruct(in dsc))
                {
                    continue;
                }
#if FEATURE_FIXED_OUT_ARGS
                if (lclNum == lvaOutgoingArgSpaceVar)
                {
                    continue;
                }
#endif
#if TARGET_WASM
                if ((lclNum == lvaWasmVirtualIP) || (lclNum == lvaWasmResumeIP) ||
                    (lclNum == lvaWasmFunctionIndex))
                {
                    continue;
                }
#endif

                var allocateOnFrame = dsc.lvOnFrame;
                if (dsc.lvRegister && (lvaDoneFrameLayout == REGALLOC_FRAME_LAYOUT) &&
                    ((dsc.Type is not TYP_LONG) || (dsc.OtherReg != REG_STK)))
                {
                    allocateOnFrame = false;
                }

                if (lvaIsOSRLocal(lclNum))
                {
                    if (dsc.lvIsStructField)
                    {
                        var parentOriginalOffset = lvaOSRLocalTier0FrameOffset(dsc.lvParentLcl);
                        dsc.StackOffset = originalFrameStkOffs + parentOriginalOffset + dsc.lvFldOffset;
                        JITDUMP($"---OSR--- V{lclNum:D2} (promoted field of V{dsc.lvParentLcl:D2}; on tier0 frame) " +
                            $"tier0 FP-rel offset {parentOriginalOffset} frame offset {originalFrameStkOffs} " +
                            $"field offset {dsc.lvFldOffset} new virt offset {dsc.StackOffset}\n");
                    }
                    else
                    {
                        var originalOffset = lvaOSRLocalTier0FrameOffset(lclNum);
                        dsc.StackOffset = originalFrameStkOffs + originalOffset;
                        JITDUMP($"---OSR--- V{lclNum:D2} (on tier0 frame) tier0 FP-rel offset {originalOffset} " +
                            $"frame offset {originalFrameStkOffs} new virt offset {dsc.StackOffset}\n");
                    }
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
                else if (lvaLocalIsOnUnknownSizeFrame(lclNum))
                {
#if FEATURE_SIMD && TARGET_ARM64
                    lvaAllocUnknownSizeLocal(lclNum);
                    continue;
#else
                    unreached();
                    continue;
#endif
                }

                if (
#if JIT32_GCENCODER
                    (lclNum == lvaLocAllocSPvar) ||
#endif
                    (lclNum == lvaRetAddrVar))
                {
#if DEBUG
                    assert(dsc.StackOffset != BAD_STK_OFFS);
#endif
                    continue;
                }

                if (lclNum == lvaMonAcquired ||
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

                if (mustDoubleAlign && ((dsc.Type is TYP_DOUBLE)
#if TARGET_ARM
                    || (dsc.Type is TYP_LONG)
#endif
#if !TARGET_64BIT
                    || dsc.lvStructDoubleAlign
#endif
                    ))
                {
                    noway_assert((compLclFrameSize % TARGET_POINTER_SIZE) == 0);
                    if ((lvaDoneFrameLayout != FINAL_FRAME_LAYOUT) && !haveLocalDoubleAlign)
                    {
                        lvaIncrementFrameSize(TARGET_POINTER_SIZE);
                        stkOffs -= TARGET_POINTER_SIZE;
                    }
                    else
                    {
                        if (((stkOffs + preSpillSize) % (2 * TARGET_POINTER_SIZE)) != 0)
                        {
                            lvaIncrementFrameSize(TARGET_POINTER_SIZE);
                            stkOffs -= TARGET_POINTER_SIZE;
                        }
                        noway_assert(((stkOffs + preSpillSize) % (2 * TARGET_POINTER_SIZE)) == 0);
                    }
                    haveLocalDoubleAlign = true;
                }

                stkOffs = lvaAllocLocalAndSetVirtualOffset(lclNum, lvaLclStackHomeSize(lclNum), stkOffs);
#if TARGET_ARMARCH || TARGET_LOONGARCH64 || TARGET_RISCV64
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
            stkOffs = lvaAllocateTemps(stkOffs, mustDoubleAlign);
        }

#if JIT32_GCENCODER
        if ((lvaGSSecurityCookie != BAD_VAR_NUM) && (lvaGetDesc(lvaGSSecurityCookie).StackOffset == stkOffs))
        {
            lvaIncrementFrameSize(TARGET_POINTER_SIZE);
            stkOffs -= TARGET_POINTER_SIZE;
        }
#endif

        if (mustDoubleAlign)
        {
            if (lvaDoneFrameLayout != FINAL_FRAME_LAYOUT)
            {
                lvaIncrementFrameSize(TARGET_POINTER_SIZE);
                stkOffs -= TARGET_POINTER_SIZE;
                if (haveLocalDoubleAlign)
                {
                    lvaIncrementFrameSize(TARGET_POINTER_SIZE);
                    stkOffs -= TARGET_POINTER_SIZE;
                }
            }
            else
            {
                if (((stkOffs + preSpillSize) % (2 * TARGET_POINTER_SIZE)) != 0)
                {
                    lvaIncrementFrameSize(TARGET_POINTER_SIZE);
                    stkOffs -= TARGET_POINTER_SIZE;
                }
                noway_assert(((stkOffs + preSpillSize) % (2 * TARGET_POINTER_SIZE)) == 0);
            }
        }

#if FEATURE_FIXED_OUT_ARGS
        if (lvaOutgoingArgSpaceSize.Value > 0)
        {
#if WINDOWS_AMD64_ABI
            noway_assert(lvaOutgoingArgSpaceSize.Value >= 4 * TARGET_POINTER_SIZE);
#endif
            noway_assert((lvaOutgoingArgSpaceSize.Value % TARGET_POINTER_SIZE) == 0);
            stkOffs = lvaAllocLocalAndSetVirtualOffset(
                lvaOutgoingArgSpaceVar, lvaLclStackHomeSize(lvaOutgoingArgSpaceVar), stkOffs);
        }
#endif

#if TARGET_WASM
        if (lvaWasmResumeIP != BAD_VAR_NUM)
        {
            stkOffs = lvaAllocLocalAndSetVirtualOffset(lvaWasmResumeIP, TARGET_POINTER_SIZE, stkOffs);
        }
        if (lvaWasmVirtualIP != BAD_VAR_NUM)
        {
            stkOffs = lvaAllocLocalAndSetVirtualOffset(lvaWasmVirtualIP, TARGET_POINTER_SIZE, stkOffs);
        }
        if (lvaWasmFunctionIndex != BAD_VAR_NUM)
        {
            stkOffs = lvaAllocLocalAndSetVirtualOffset(lvaWasmFunctionIndex, TARGET_POINTER_SIZE, stkOffs);
        }
#endif

#if HAS_FIXED_REGISTER_SET
        var pushedCount = compCalleeRegsPushed;
#else
        var pushedCount = 0;
#endif
#if TARGET_ARM64
        if (info.compIsVarArgs)
        {
            pushedCount += MAX_REG_ARG;
        }
#endif
#if TARGET_XARCH
        if (lvaDoubleAlignOrFramePointerUsed())
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
    }

    public int lvaAllocLocalAndSetVirtualOffset(int lclNum, int size, int stkOffs)
    {
        noway_assert(lclNum != BAD_VAR_NUM);
        ref var local = ref lvaGetDesc(lclNum);
#if TARGET_64BIT || TARGET_WASM
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
#endif

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
    }

    private static int FrameAlignmentPad(int value, int alignment)
    {
        return (alignment - (value % alignment)) % alignment;
    }

    public unsafe int lvaAllocAsyncContexts(int stkOffs)
    {
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
    }

    public void lvaAlignFrame()
    {
        assert(codeGen is not null);
#if TARGET_AMD64 || TARGET_ARM64 || TARGET_LOONGARCH64 || TARGET_RISCV64
        if ((compLclFrameSize % REGSIZE_BYTES) != 0)
        {
            lvaIncrementFrameSize(REGSIZE_BYTES - (compLclFrameSize % REGSIZE_BYTES));
        }
        else if (lvaDoneFrameLayout != FINAL_FRAME_LAYOUT)
        {
            lvaIncrementFrameSize(REGSIZE_BYTES);
        }

        assert((compLclFrameSize % REGSIZE_BYTES) == 0);
#if TARGET_ARM64 || TARGET_LOONGARCH64 || TARGET_RISCV64
        var regPushedCountAligned = (compCalleeRegsPushed % (STACK_ALIGN / REGSIZE_BYTES)) == 0;
        var lclFrameSizeAligned = (compLclFrameSize % STACK_ALIGN) == 0;
        if ((lvaDoneFrameLayout != FINAL_FRAME_LAYOUT) || (regPushedCountAligned != lclFrameSizeAligned))
        {
            lvaIncrementFrameSize(REGSIZE_BYTES);
        }
#else
        var regPushedCountAligned = lvaIsCalleeSavedIntRegCountEven();
        var lclFrameSizeAligned = (compLclFrameSize % STACK_ALIGN) == 0;
#if UNIX_AMD64_ABI
        var stackNeedsAlignment = (compLclFrameSize != 0) || opts.compNeedToAlignFrame;
#else
        var stackNeedsAlignment = compLclFrameSize != 0;
#endif
        if ((!codeGen.IsFramePointerUsed && (lvaDoneFrameLayout != FINAL_FRAME_LAYOUT)) ||
            (stackNeedsAlignment && (regPushedCountAligned == lclFrameSizeAligned)))
        {
            lvaIncrementFrameSize(REGSIZE_BYTES);
        }
#endif
#elif TARGET_ARM
        var lclFrameSizeAligned = (compLclFrameSize % sizeof(double)) == 0;
        var preSpillCount = BitOperations.PopCount(
            unchecked((ulong)codeGen.RegSet.rsMaskPreSpillRegs(true).IntRegSet));
        var regPushedCountAligned = ((compCalleeRegsPushed + preSpillCount)
            % (sizeof(double) / TARGET_POINTER_SIZE)) == 0;
        if (regPushedCountAligned != lclFrameSizeAligned)
        {
            lvaIncrementFrameSize(TARGET_POINTER_SIZE);
        }
#elif TARGET_X86
#if DOUBLE_ALIGN
        if (genDoubleAlign && (compLclFrameSize == 0))
        {
            lvaIncrementFrameSize(TARGET_POINTER_SIZE);
        }
#endif
        if (STACK_ALIGN > REGSIZE_BYTES)
        {
            if (lvaDoneFrameLayout != FINAL_FRAME_LAYOUT)
            {
                lvaIncrementFrameSize(STACK_ALIGN - REGSIZE_BYTES);
            }

            var adjustFrameSize = compLclFrameSize;
#if UNIX_X86_ABI
            var isEbpPushed = lvaDoubleAlignOrFramePointerUsed();
            var adjustCount = compCalleeRegsPushed + 1 + (isEbpPushed ? 1 : 0);
            adjustFrameSize += (adjustCount * REGSIZE_BYTES) % STACK_ALIGN;
#endif
            if ((adjustFrameSize % STACK_ALIGN) != 0)
            {
                lvaIncrementFrameSize(STACK_ALIGN - (adjustFrameSize % STACK_ALIGN));
            }
        }
#elif TARGET_WASM
        var pad = 0;
        if ((compLclFrameSize % STACK_ALIGN) != 0)
        {
            pad = STACK_ALIGN - (compLclFrameSize % STACK_ALIGN);
        }
        else if (lvaDoneFrameLayout != FINAL_FRAME_LAYOUT)
        {
            pad = STACK_ALIGN;
        }

        if (pad != 0)
        {
            lvaIncrementFrameSize(pad);
            ReadOnlySpan<int> ehSlots = [lvaWasmFunctionIndex, lvaWasmVirtualIP, lvaWasmResumeIP];
            foreach (var ehSlot in ehSlots)
            {
                if (ehSlot != BAD_VAR_NUM)
                {
                    lvaGetDesc(ehSlot).StackOffset -= pad;
                }
            }
        }
        assert((compLclFrameSize % STACK_ALIGN) == 0);
#else
        NYI("TARGET specific lvaAlignFrame");
        fatal(CORJIT_IMPLLIMITATION);
#endif
    }

    public int lvaAllocateTemps(int stkOffs, bool mustDoubleAlign)
    {
#if TARGET_ARM
        var spillTempSize = 0;
#endif
        if (lvaDoneFrameLayout == FINAL_FRAME_LAYOUT)
        {
            assert(codeGen is not null);
            var preSpillSize = 0;
#if TARGET_ARM
            preSpillSize = BitOperations.PopCount(
                unchecked((ulong)codeGen.RegSet.rsMaskPreSpillRegs(true).IntRegSet)) * TARGET_POINTER_SIZE;
#endif
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
                    unreached();
                    continue;
#endif
                }

#if TARGET_64BIT
                if (varTypeIsGC(temp.tdTempType) && ((stkOffs % TARGET_POINTER_SIZE) != 0))
                {
                    var pad = FrameAlignmentPad(-stkOffs, TARGET_POINTER_SIZE);
                    lvaIncrementFrameSize(pad);
                    stkOffs -= pad;
                    noway_assert((stkOffs % TARGET_POINTER_SIZE) == 0);
                }
#endif

                if (mustDoubleAlign && (temp.tdTempType == TYP_DOUBLE))
                {
                    noway_assert((compLclFrameSize % TARGET_POINTER_SIZE) == 0);
                    if (((stkOffs + preSpillSize) % (2 * TARGET_POINTER_SIZE)) != 0)
                    {
#if TARGET_ARM
                        spillTempSize += TARGET_POINTER_SIZE;
#endif
                        lvaIncrementFrameSize(TARGET_POINTER_SIZE);
                        stkOffs -= TARGET_POINTER_SIZE;
                    }
                    noway_assert(((stkOffs + preSpillSize) % (2 * TARGET_POINTER_SIZE)) == 0);
                }

#if TARGET_ARM
                spillTempSize += temp.tdTempSize;
#endif
                lvaIncrementFrameSize(temp.tdTempSize);
                stkOffs -= temp.tdTempSize;
                temp.tdTempOffs = stkOffs;
            }
#if TARGET_ARM
            noway_assert(spillTempSize <= lvaMaxSpillTempSize);
#endif
        }
        else
        {
            var size = lvaMaxSpillTempSize;
            lvaIncrementFrameSize(size);
            stkOffs -= size;
        }

        return stkOffs;
    }

    public unsafe int lvaOSRLocalTier0FrameOffset(int varNum)
    {
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
    }
}
