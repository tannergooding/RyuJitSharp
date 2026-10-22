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
        if (Configuration.TARGET_ARM)
        {
            // ARM always places temps at lower stack addresses than variables.
            return false;
        }

        assert(codeGen is not null);
        return !compGSReorderStackLayout || codeGen.IsFramePointerUsed;
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

        if (Configuration.TARGET_XARCH)
        {
            stkOffs -= Configuration.TARGET_POINTER_SIZE;
            if (lvaRetAddrVar != BAD_VAR_NUM)
            {
                lvaTable[lvaRetAddrVar].StackOffset = stkOffs;
            }
        }

        if (opts.IsOSR)
        {
            assert(info.compPatchpointInfo is not null);
            if (Configuration.TARGET_LOONGARCH64 || Configuration.TARGET_RISCV64)
            {
                originalFrameStkOffs = info.compPatchpointInfo->TotalFrameSize;
            }
            else
            {
                originalFrameSize = info.compPatchpointInfo->TotalFrameSize;
                originalFrameStkOffs = stkOffs;
                stkOffs -= originalFrameSize;
            }
        }

        if (Configuration.TARGET_XARCH && lvaDoubleAlignOrFramePointerUsed())
        {
            stkOffs -= Configuration.REGSIZE_BYTES;
        }

        var preSpillSize = 0;
        var mustDoubleAlign = false;
        if (Configuration.TARGET_ARM)
        {
            mustDoubleAlign = true;
            preSpillSize = BitOperations.PopCount(
                unchecked((ulong)codeGen.RegSet.rsMaskPreSpillRegs(true).IntRegSet)) * Configuration.REGSIZE_BYTES;
        }
        else if (Configuration.DOUBLE_ALIGN)
        {
            mustDoubleAlign = genDoubleAlign;
        }

        var initialStkOffs = 0;
        if (Configuration.TARGET_ARM64)
        {
            // Initially place FP/LR with the other callee saves; fixup may move them below the locals.
            if (info.compIsVarArgs)
            {
                initialStkOffs = Target.ARM64_MAX_REG_ARG * Configuration.REGSIZE_BYTES;
                stkOffs -= initialStkOffs;
            }

            stkOffs -= compCalleeRegsPushed * Configuration.REGSIZE_BYTES;
        }
        else if (Configuration.TARGET_LOONGARCH64 || Configuration.TARGET_RISCV64)
        {
            assert(compCalleeRegsPushed >= 2);
            stkOffs -= compCalleeRegsPushed * Configuration.REGSIZE_BYTES;
        }
        else if (Configuration.HAS_FIXED_REGISTER_SET)
        {
            if (Configuration.TARGET_ARM && (lvaRetAddrVar != BAD_VAR_NUM))
            {
                lvaTable[lvaRetAddrVar].StackOffset = stkOffs - Configuration.REGSIZE_BYTES;
            }

            stkOffs -= compCalleeRegsPushed * Configuration.REGSIZE_BYTES;
        }

        compLclFrameSize = 0;
        if (Configuration.TARGET_AMD64)
        {
            const int xmmRegSize = 16;

            if (MethodHasPatchpoint)
            {
                // Tier0 reserves the callee-save slots an OSR method may subsequently need.
                var regsPushed = compCalleeRegsPushed + (codeGen.IsFramePointerUsed ? 1 : 0);
                var osrIntCalleeSaved =
                    SRBM_X64_RBX | SRBM_X64_RBP | SRBM_X64_R12 | SRBM_X64_R13 | SRBM_X64_R14 | SRBM_X64_R15;
                if (Configuration.TARGET_WINDOWS)
                {
                    osrIntCalleeSaved |= SRBM_X64_RSI | SRBM_X64_RDI;
                }

                var extraSlots = BitOperations.PopCount((ulong)osrIntCalleeSaved) - regsPushed;
                noway_assert(extraSlots >= 0);
                var extraSlotSize = extraSlots * Configuration.REGSIZE_BYTES;
                JITDUMP($"\nMethod has patchpoints and has {regsPushed} callee saves.\n" +
                    $"Reserving {extraSlots} extra slots ({extraSlotSize} bytes) for potential OSR method callee saves\n");
                stkOffs -= extraSlotSize;
                lvaIncrementFrameSize(extraSlotSize);
            }

            var calleeFPRegsSavedSize = BitOperations.PopCount((ulong)compCalleeFPRegsSavedMask) * xmmRegSize;
            var offsetForAlign = -(stkOffs + originalFrameSize);
            if ((calleeFPRegsSavedSize > 0) && ((offsetForAlign % xmmRegSize) != 0))
            {
                var alignPad = FrameAlignmentPad(offsetForAlign, xmmRegSize);
                assert(alignPad != 0);
                stkOffs -= alignPad;
                lvaIncrementFrameSize(alignPad);
            }

            stkOffs -= calleeFPRegsSavedSize;
            lvaIncrementFrameSize(calleeFPRegsSavedSize);
        }

        if (!opts.IsOSR)
        {
            if (lvaMonAcquired != BAD_VAR_NUM)
            {
                stkOffs = lvaAllocLocalAndSetVirtualOffset(
                    lvaMonAcquired, lvaLclStackHomeSize(lvaMonAcquired), stkOffs);
            }

            if (!Configuration.JIT32_GCENCODER)
            {
                stkOffs = lvaAllocAsyncContexts(stkOffs);
            }
        }

        if (mustDoubleAlign)
        {
            if (lvaDoneFrameLayout != FINAL_FRAME_LAYOUT)
            {
                lvaIncrementFrameSize(Configuration.TARGET_POINTER_SIZE);
                stkOffs -= Configuration.TARGET_POINTER_SIZE;
                lvaIncrementFrameSize(Configuration.TARGET_POINTER_SIZE);
                stkOffs -= Configuration.TARGET_POINTER_SIZE;
            }
            else
            {
                if (((stkOffs + preSpillSize) % (2 * Configuration.TARGET_POINTER_SIZE)) != 0)
                {
                    lvaIncrementFrameSize(Configuration.TARGET_POINTER_SIZE);
                    stkOffs -= Configuration.TARGET_POINTER_SIZE;
                }
                noway_assert(((stkOffs + preSpillSize) % (2 * Configuration.TARGET_POINTER_SIZE)) == 0);
            }
        }

        if (Configuration.JIT32_GCENCODER && (lvaLocAllocSPvar != BAD_VAR_NUM))
        {
            noway_assert(codeGen.IsFramePointerUsed);
            stkOffs = lvaAllocLocalAndSetVirtualOffset(lvaLocAllocSPvar, Configuration.TARGET_POINTER_SIZE, stkOffs);
        }

        if (lvaReportParamTypeArg())
        {
            if (Configuration.JIT32_GCENCODER)
            {
                noway_assert(codeGen.IsFramePointerUsed);
            }

            if (opts.IsOSR)
            {
                assert(info.compPatchpointInfo is not null);
                assert(info.compPatchpointInfo->HasGenericContextArgOffset);
                lvaCachedGenericContextArgOffs = originalFrameStkOffs
                    + info.compPatchpointInfo->GenericContextArgOffset;
            }
            else
            {
                lvaIncrementFrameSize(Configuration.TARGET_POINTER_SIZE);
                stkOffs -= Configuration.TARGET_POINTER_SIZE;
                lvaCachedGenericContextArgOffs = stkOffs;
            }
        }
        else if (!Configuration.JIT32_GCENCODER && lvaKeepAliveAndReportThis())
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
                lvaIncrementFrameSize(Configuration.TARGET_POINTER_SIZE);
                stkOffs -= Configuration.TARGET_POINTER_SIZE;
                lvaCachedGenericContextArgOffs = stkOffs;
            }
        }

        if (Configuration.JIT32_GCENCODER)
        {
            assert(!opts.IsOSR);
            stkOffs = lvaAllocAsyncContexts(stkOffs);
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
                if (Configuration.FEATURE_FIXED_OUT_ARGS && (lclNum == lvaOutgoingArgSpaceVar))
                {
                    continue;
                }
                if (Configuration.TARGET_WASM &&
                    ((lclNum == lvaWasmVirtualIP) || (lclNum == lvaWasmResumeIP) ||
                     (lclNum == lvaWasmFunctionIndex)))
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
                    if (Configuration.FEATURE_SIMD && Configuration.TARGET_ARM64)
                    {
                        lvaAllocUnknownSizeLocal(lclNum);
                        continue;
                    }

                    unreached();
                    continue;
                }

                if ((Configuration.JIT32_GCENCODER && (lclNum == lvaLocAllocSPvar)) ||
                    (lclNum == lvaRetAddrVar))
                {
                    if (Configuration.DEBUG)
                    {
                        assert(dsc.StackOffset != BAD_STK_OFFS);
                    }

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
                    if (Configuration.TARGET_ARM64 && info.compIsVarArgs && dsc.lvIsRegArg &&
                        (lclNum != info.compRetBuffArg) &&
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
                            dsc.StackOffset = -initialStkOffs + (regArgNum * Configuration.REGSIZE_BYTES);
                            break;
                        }

                        assert(found);
                        continue;
                    }
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

                if (mustDoubleAlign &&
                    ((dsc.Type is TYP_DOUBLE) ||
                     (Configuration.TARGET_ARM && (dsc.Type is TYP_LONG)) ||
                     (!Configuration.TARGET_64BIT && dsc.lvStructDoubleAlign)))
                {
                    noway_assert((compLclFrameSize % Configuration.TARGET_POINTER_SIZE) == 0);
                    if ((lvaDoneFrameLayout != FINAL_FRAME_LAYOUT) && !haveLocalDoubleAlign)
                    {
                        lvaIncrementFrameSize(Configuration.TARGET_POINTER_SIZE);
                        stkOffs -= Configuration.TARGET_POINTER_SIZE;
                    }
                    else
                    {
                        if (((stkOffs + preSpillSize) % (2 * Configuration.TARGET_POINTER_SIZE)) != 0)
                        {
                            lvaIncrementFrameSize(Configuration.TARGET_POINTER_SIZE);
                            stkOffs -= Configuration.TARGET_POINTER_SIZE;
                        }
                        noway_assert(((stkOffs + preSpillSize) % (2 * Configuration.TARGET_POINTER_SIZE)) == 0);
                    }
                    haveLocalDoubleAlign = true;
                }

                stkOffs = lvaAllocLocalAndSetVirtualOffset(lclNum, lvaLclStackHomeSize(lclNum), stkOffs);
                if ((Configuration.TARGET_ARMARCH || Configuration.TARGET_LOONGARCH64 ||
                     Configuration.TARGET_RISCV64) &&
                    dsc.lvIsRegArg && dsc.lvPromoted)
                {
                    for (var field = 0; field < dsc.lvFieldCnt; field++)
                    {
                        ref var fieldDsc = ref lvaGetDesc(dsc.lvFieldLclStart + field);
                        fieldDsc.StackOffset = dsc.StackOffset + fieldDsc.lvFldOffset;
                    }
                }
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

        if (Configuration.JIT32_GCENCODER &&
            (lvaGSSecurityCookie != BAD_VAR_NUM) &&
            (lvaGetDesc(lvaGSSecurityCookie).StackOffset == stkOffs))
        {
            lvaIncrementFrameSize(Configuration.TARGET_POINTER_SIZE);
            stkOffs -= Configuration.TARGET_POINTER_SIZE;
        }

        if (mustDoubleAlign)
        {
            if (lvaDoneFrameLayout != FINAL_FRAME_LAYOUT)
            {
                lvaIncrementFrameSize(Configuration.TARGET_POINTER_SIZE);
                stkOffs -= Configuration.TARGET_POINTER_SIZE;
                if (haveLocalDoubleAlign)
                {
                    lvaIncrementFrameSize(Configuration.TARGET_POINTER_SIZE);
                    stkOffs -= Configuration.TARGET_POINTER_SIZE;
                }
            }
            else
            {
                if (((stkOffs + preSpillSize) % (2 * Configuration.TARGET_POINTER_SIZE)) != 0)
                {
                    lvaIncrementFrameSize(Configuration.TARGET_POINTER_SIZE);
                    stkOffs -= Configuration.TARGET_POINTER_SIZE;
                }
                noway_assert(((stkOffs + preSpillSize) % (2 * Configuration.TARGET_POINTER_SIZE)) == 0);
            }
        }

        if (Configuration.FEATURE_FIXED_OUT_ARGS && (lvaOutgoingArgSpaceSize.Value > 0))
        {
            if (Configuration.WINDOWS_AMD64_ABI)
            {
                noway_assert(lvaOutgoingArgSpaceSize.Value >= 4 * Configuration.TARGET_POINTER_SIZE);
            }

            noway_assert((lvaOutgoingArgSpaceSize.Value % Configuration.TARGET_POINTER_SIZE) == 0);
            stkOffs = lvaAllocLocalAndSetVirtualOffset(
                lvaOutgoingArgSpaceVar, lvaLclStackHomeSize(lvaOutgoingArgSpaceVar), stkOffs);
        }

        if (Configuration.TARGET_WASM)
        {
            if (lvaWasmResumeIP != BAD_VAR_NUM)
            {
                stkOffs = lvaAllocLocalAndSetVirtualOffset(
                    lvaWasmResumeIP, Configuration.TARGET_POINTER_SIZE, stkOffs);
            }
            if (lvaWasmVirtualIP != BAD_VAR_NUM)
            {
                stkOffs = lvaAllocLocalAndSetVirtualOffset(
                    lvaWasmVirtualIP, Configuration.TARGET_POINTER_SIZE, stkOffs);
            }
            if (lvaWasmFunctionIndex != BAD_VAR_NUM)
            {
                stkOffs = lvaAllocLocalAndSetVirtualOffset(
                    lvaWasmFunctionIndex, Configuration.TARGET_POINTER_SIZE, stkOffs);
            }
        }

        var pushedCount = Configuration.HAS_FIXED_REGISTER_SET ? compCalleeRegsPushed : 0;
        if (Configuration.TARGET_ARM64 && info.compIsVarArgs)
        {
            pushedCount += Target.ARM64_MAX_REG_ARG;
        }

        if (Configuration.TARGET_XARCH)
        {
            if (lvaDoubleAlignOrFramePointerUsed())
            {
                pushedCount++;
            }

            pushedCount++;
        }
        noway_assert(compLclFrameSize + originalFrameSize == -(stkOffs + (pushedCount * Configuration.TARGET_POINTER_SIZE)));

        if (Configuration.TARGET_ARM64)
        {
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
        }
    }

    public int lvaAllocLocalAndSetVirtualOffset(int lclNum, int size, int stkOffs)
    {
        noway_assert(lclNum != BAD_VAR_NUM);
        ref var local = ref lvaGetDesc(lclNum);
        var isAlignedSimd = Configuration.FEATURE_SIMD && Configuration.ALIGN_SIMD_TYPES &&
            varTypeIsSimd(local.Type);
        if ((Configuration.TARGET_64BIT || Configuration.TARGET_WASM) &&
            (size >= 8) &&
            ((lvaDoneFrameLayout != FINAL_FRAME_LAYOUT) || ((stkOffs % 8) != 0) || isAlignedSimd))
        {
            assert(stkOffs <= 0);
            var pad = 0;
            if (isAlignedSimd)
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
            {
                // Tentative offsets must never underestimate the final stack displacement.
                pad = lvaDoneFrameLayout != FINAL_FRAME_LAYOUT ? 7 : 8 + (stkOffs % 8);
            }

            lvaIncrementFrameSize(pad);
            stkOffs -= pad;
            if (Configuration.DEBUG && verbose)
            {
                jitprintf("Pad ");
                gtDispLclVar(lclNum, false);
                jitprintf($", size={size}, stkOffs={(stkOffs < 0 ? '-' : '+')}0x{Math.Abs(stkOffs):x}, pad={pad}\n");
            }
        }

        lvaIncrementFrameSize(size);
        stkOffs -= size;
        local.StackOffset = stkOffs;
        if (Configuration.DEBUG && verbose)
        {
            jitprintf("Assign ");
            gtDispLclVar(lclNum, false);
            jitprintf($", size={size}, stkOffs={(stkOffs < 0 ? '-' : '+')}0x{Math.Abs(stkOffs):x}\n");
        }
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
        if (Configuration.TARGET_AMD64 || Configuration.TARGET_ARM64 ||
            Configuration.TARGET_LOONGARCH64 || Configuration.TARGET_RISCV64)
        {
            if ((compLclFrameSize % Configuration.REGSIZE_BYTES) != 0)
            {
                lvaIncrementFrameSize(Configuration.REGSIZE_BYTES -
                    (compLclFrameSize % Configuration.REGSIZE_BYTES));
            }
            else if (lvaDoneFrameLayout != FINAL_FRAME_LAYOUT)
            {
                lvaIncrementFrameSize(Configuration.REGSIZE_BYTES);
            }

            assert((compLclFrameSize % Configuration.REGSIZE_BYTES) == 0);
            if (Configuration.TARGET_ARM64 || Configuration.TARGET_LOONGARCH64 ||
                Configuration.TARGET_RISCV64)
            {
                var regPushedCountAligned =
                    (compCalleeRegsPushed % (Configuration.STACK_ALIGN / Configuration.REGSIZE_BYTES)) == 0;
                var lclFrameSizeAligned = (compLclFrameSize % Configuration.STACK_ALIGN) == 0;
                if ((lvaDoneFrameLayout != FINAL_FRAME_LAYOUT) ||
                    (regPushedCountAligned != lclFrameSizeAligned))
                {
                    lvaIncrementFrameSize(Configuration.REGSIZE_BYTES);
                }
            }
            else
            {
                var regPushedCountAligned = lvaIsCalleeSavedIntRegCountEven();
                var lclFrameSizeAligned = (compLclFrameSize % Configuration.STACK_ALIGN) == 0;
                var stackNeedsAlignment = (compLclFrameSize != 0) ||
                    (Configuration.UNIX_AMD64_ABI && opts.compNeedToAlignFrame);
                if ((!codeGen.IsFramePointerUsed && (lvaDoneFrameLayout != FINAL_FRAME_LAYOUT)) ||
                    (stackNeedsAlignment && (regPushedCountAligned == lclFrameSizeAligned)))
                {
                    lvaIncrementFrameSize(Configuration.REGSIZE_BYTES);
                }
            }
        }
        else if (Configuration.TARGET_ARM)
        {
            var lclFrameSizeAligned = (compLclFrameSize % sizeof(double)) == 0;
            var preSpillCount = BitOperations.PopCount(
                unchecked((ulong)codeGen.RegSet.rsMaskPreSpillRegs(true).IntRegSet));
            var regPushedCountAligned = ((compCalleeRegsPushed + preSpillCount)
                % (sizeof(double) / Configuration.TARGET_POINTER_SIZE)) == 0;
            if (regPushedCountAligned != lclFrameSizeAligned)
            {
                lvaIncrementFrameSize(Configuration.TARGET_POINTER_SIZE);
            }
        }
        else if (Configuration.TARGET_X86)
        {
            if (Configuration.DOUBLE_ALIGN && genDoubleAlign && (compLclFrameSize == 0))
            {
                lvaIncrementFrameSize(Configuration.TARGET_POINTER_SIZE);
            }
            if (Configuration.STACK_ALIGN > Configuration.REGSIZE_BYTES)
            {
                if (lvaDoneFrameLayout != FINAL_FRAME_LAYOUT)
                {
                    lvaIncrementFrameSize(Configuration.STACK_ALIGN - Configuration.REGSIZE_BYTES);
                }

                var adjustFrameSize = compLclFrameSize;
                if (Configuration.UNIX_X86_ABI)
                {
                    var isEbpPushed = lvaDoubleAlignOrFramePointerUsed();
                    var adjustCount = compCalleeRegsPushed + 1 + (isEbpPushed ? 1 : 0);
                    adjustFrameSize += (adjustCount * Configuration.REGSIZE_BYTES) % Configuration.STACK_ALIGN;
                }

                if ((adjustFrameSize % Configuration.STACK_ALIGN) != 0)
                {
                    lvaIncrementFrameSize(Configuration.STACK_ALIGN -
                        (adjustFrameSize % Configuration.STACK_ALIGN));
                }
            }
        }
        else if (Configuration.TARGET_WASM)
        {
            var pad = 0;
            if ((compLclFrameSize % Configuration.STACK_ALIGN) != 0)
            {
                pad = Configuration.STACK_ALIGN - (compLclFrameSize % Configuration.STACK_ALIGN);
            }
            else if (lvaDoneFrameLayout != FINAL_FRAME_LAYOUT)
            {
                pad = Configuration.STACK_ALIGN;
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

            assert((compLclFrameSize % Configuration.STACK_ALIGN) == 0);
        }
        else
        {
            NYI("TARGET specific lvaAlignFrame");
            fatal(CORJIT_IMPLLIMITATION);
            FatalJitException.ThrowFatalJitException(
        CORJIT_IMPLLIMITATION,
        FatalJitErrorCode.LvaAlignFrameIsNotSupportedForThisTarget
    );
            return;
        }
    }

    public int lvaAllocateTemps(int stkOffs, bool mustDoubleAlign)
    {
        var spillTempSize = 0;
        if (lvaDoneFrameLayout == FINAL_FRAME_LAYOUT)
        {
            assert(codeGen is not null);
            var preSpillSize = 0;
            if (Configuration.TARGET_ARM)
            {
                preSpillSize = BitOperations.PopCount(
                    unchecked((ulong)codeGen.RegSet.rsMaskPreSpillRegs(true).IntRegSet)) *
                    Configuration.TARGET_POINTER_SIZE;
            }

            assert(codeGen.RegSet.tmpGetAllFree());
            for (var temp = codeGen.RegSet.tmpListBeg(); temp is not null; temp = codeGen.RegSet.tmpListNxt(temp))
            {
                if (varTypeHasUnknownSize(temp.tdTempType))
                {
                    if (Configuration.FEATURE_SIMD && Configuration.TARGET_ARM64)
                    {
                        lvaAllocateUnknownSizeTemp(temp);
                        continue;
                    }

                    unreached();
                    continue;
                }

                if (Configuration.TARGET_64BIT &&
                    varTypeIsGC(temp.tdTempType) && ((stkOffs % Configuration.TARGET_POINTER_SIZE) != 0))
                {
                    var pad = FrameAlignmentPad(-stkOffs, Configuration.TARGET_POINTER_SIZE);
                    lvaIncrementFrameSize(pad);
                    stkOffs -= pad;
                    noway_assert((stkOffs % Configuration.TARGET_POINTER_SIZE) == 0);
                }

                if (mustDoubleAlign && (temp.tdTempType == TYP_DOUBLE))
                {
                    noway_assert((compLclFrameSize % Configuration.TARGET_POINTER_SIZE) == 0);
                    if (((stkOffs + preSpillSize) % (2 * Configuration.TARGET_POINTER_SIZE)) != 0)
                    {
                        if (Configuration.TARGET_ARM)
                        {
                            spillTempSize += Configuration.TARGET_POINTER_SIZE;
                        }

                        lvaIncrementFrameSize(Configuration.TARGET_POINTER_SIZE);
                        stkOffs -= Configuration.TARGET_POINTER_SIZE;
                    }
                    noway_assert(((stkOffs + preSpillSize) % (2 * Configuration.TARGET_POINTER_SIZE)) == 0);
                }

                if (Configuration.TARGET_ARM)
                {
                    spillTempSize += temp.tdTempSize;
                }

                lvaIncrementFrameSize(temp.tdTempSize);
                stkOffs -= temp.tdTempSize;
                temp.tdTempOffs = stkOffs;
            }
            if (Configuration.TARGET_ARM)
            {
                noway_assert(spillTempSize <= lvaMaxSpillTempSize);
            }
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
