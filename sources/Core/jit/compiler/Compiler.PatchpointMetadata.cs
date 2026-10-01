// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public unsafe void generatePatchpointInfo()
    {
        if (!MethodHasPatchpoint)
        {
            return;
        }

        assert(codeGen is not null);
        assert(codeGen.IsFramePointerUsed);

        var patchpointInfoSize = unchecked((uint)PatchpointInfo.ComputeSize(info.compLocalsCount));
        var patchpointInfo = (PatchpointInfo*)info.compCompHnd->allocateArray(unchecked((nint)patchpointInfoSize));

#if TARGET_AMD64 || TARGET_ARM64 || TARGET_LOONGARCH64 || TARGET_RISCV64
#if TARGET_AMD64
        // The OSR prolog pushes a pseudo return address below the Tier0 frame.
        // Tier0 always uses FP, so its FP-relative offsets are already virtual frame offsets.
        var totalFrameSize = unchecked(codeGen.genTotalFrameSize + TARGET_POINTER_SIZE);
        var offsetAdjust = 0;
#else
        // Calls do not manipulate SP; FP-relative offsets must refer to the top of the frame.
        var totalFrameSize = codeGen.genTotalFrameSize;
        var offsetAdjust = unchecked(codeGen.genSPtoFPdelta - totalFrameSize);
#endif
        patchpointInfo->Initialize(info.compLocalsCount, totalFrameSize);
        JITDUMP($"--OSR--- Total Frame Size {patchpointInfo->TotalFrameSize}, local offset adjust is {offsetAdjust}\n");

        for (var lclNum = 0; lclNum < info.compLocalsCount; lclNum++)
        {
            var varNum = lclNum;
            if (lvaIsUnknownSizeLocal(varNum))
            {
                continue;
            }

            if (gsShadowVarInfo is not null)
            {
                var shadowNum = gsShadowVarInfo[lclNum].ShadowCopy;
                if (shadowNum != BAD_VAR_NUM)
                {
                    assert(shadowNum < lvaCount);
                    assert(shadowNum >= info.compLocalsCount);
                    varNum = shadowNum;
                }
            }

            ref var varDsc = ref lvaGetDesc(varNum);
            assert(varDsc.lvOnFrame);
            assert(varDsc.lvFramePointerBased);

            // OSR partial importation may skip the original address-of operation.
            patchpointInfo->SetOffsetAndExposure(lclNum, unchecked(varDsc.StackOffset + offsetAdjust),
                varDsc.lvHasLdAddrOp);
            JITDUMP($"--OSR-- V{lclNum:D2} is at virtual offset {patchpointInfo->Offset(lclNum)}" +
                $"{(patchpointInfo->IsExposed(lclNum) ? " (exposed)" : "")}{(varNum != lclNum ? " (shadowed)" : "")}\n");
        }

        if (lvaReportParamTypeArg())
        {
            patchpointInfo->GenericContextArgOffset = unchecked(lvaCachedGenericContextArgOffset() + offsetAdjust);
            JITDUMP($"--OSR-- cached generic context virtual offset is {patchpointInfo->GenericContextArgOffset}\n");
        }

        if (lvaKeepAliveAndReportThis())
        {
            patchpointInfo->KeptAliveThisOffset = unchecked(lvaCachedGenericContextArgOffset() + offsetAdjust);
            JITDUMP($"--OSR-- kept-alive this virtual offset is {patchpointInfo->KeptAliveThisOffset}\n");
        }

        if (compGSReorderStackLayout)
        {
            assert(lvaGSSecurityCookie != BAD_VAR_NUM);
            patchpointInfo->SecurityCookieOffset = unchecked(lvaGetDesc(lvaGSSecurityCookie).StackOffset + offsetAdjust);
            JITDUMP($"--OSR-- security cookie V{lvaGSSecurityCookie:D2} virtual offset is {patchpointInfo->SecurityCookieOffset}\n");
        }

        if (lvaMonAcquired != BAD_VAR_NUM)
        {
            patchpointInfo->MonitorAcquiredOffset = unchecked(lvaGetDesc(lvaMonAcquired).StackOffset + offsetAdjust);
            JITDUMP($"--OSR-- monitor acquired V{lvaMonAcquired:D2} virtual offset is {patchpointInfo->MonitorAcquiredOffset}\n");
        }

        if (lvaResumedIndicator != BAD_VAR_NUM)
        {
            patchpointInfo->ResumedIndicatorOffset = unchecked(lvaGetDesc(lvaResumedIndicator).StackOffset + offsetAdjust);
            JITDUMP($"--OSR-- resumed indicator V{lvaResumedIndicator:D2} virtual offset is {patchpointInfo->ResumedIndicatorOffset}\n");
        }

        if (lvaAsyncThreadObjectVar != BAD_VAR_NUM)
        {
            patchpointInfo->AsyncThreadOffset = unchecked(lvaGetDesc(lvaAsyncThreadObjectVar).StackOffset + offsetAdjust);
            JITDUMP($"--OSR-- async thread object V{lvaAsyncThreadObjectVar:D2} virtual offset is {patchpointInfo->AsyncThreadOffset}\n");
        }

        if (lvaAsyncExecutionContextVar != BAD_VAR_NUM)
        {
            patchpointInfo->AsyncExecutionContextOffset =
                unchecked(lvaGetDesc(lvaAsyncExecutionContextVar).StackOffset + offsetAdjust);
            JITDUMP($"--OSR-- async execution context V{lvaAsyncExecutionContextVar:D2} virtual offset is {patchpointInfo->AsyncExecutionContextOffset}\n");
        }

        if (lvaAsyncSynchronizationContextVar != BAD_VAR_NUM)
        {
            patchpointInfo->AsyncSynchronizationContextOffset =
                unchecked(lvaGetDesc(lvaAsyncSynchronizationContextVar).StackOffset + offsetAdjust);
            JITDUMP($"--OSR-- async synchronization context V{lvaAsyncSynchronizationContextVar:D2} virtual offset is {patchpointInfo->AsyncSynchronizationContextOffset}\n");
        }

        var pushRegs = codeGen.RegSet.rsGetModifiedCalleeSavedRegsMask() | new regMaskTP(SRBM_FPBASE);
#if TARGET_ARM64
        pushRegs |= RBM_LR;

        // The mask records whether FP/LR are stored with the other callee saves.
        if (!codeGen.IsSaveFpLrWithAllCalleeSavedRegisters)
        {
            pushRegs &= ~(RBM_FP | RBM_LR);
        }
#elif TARGET_LOONGARCH64 || TARGET_RISCV64
        pushRegs |= RBM_RA;
#endif
        patchpointInfo->CalleeSaveRegisters = unchecked((long)pushRegs.Lower);
        JITDUMP("--OSR-- Tier0 callee saves: ");
#if DEBUG
        if (verbose)
        {
            dspRegMask(new regMaskTP(unchecked((regMask)patchpointInfo->CalleeSaveRegisters)));
        }
#endif
        JITDUMP("\n");

        info.compCompHnd->setPatchpointInfo(patchpointInfo);
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Patchpoint metadata generation is not implemented for this target.");
#endif
    }
}
