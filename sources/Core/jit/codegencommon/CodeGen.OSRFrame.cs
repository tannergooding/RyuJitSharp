// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_AMD64
using System.Numerics;
#endif

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public unsafe void genOSRHandleTier0CalleeSavedRegistersAndFrame()
    {
#if TARGET_AMD64
        assert(Emitter.emitGeneratingPrologOrFuncletProlog());
        assert(_compiler.opts.IsOSR);
        assert(_compiler.funCurrentFunc().funKind == FuncKind.FUNC_ROOT);
#if ETW_EBP_FRAMED
        noway_assert(IsFramePointerUsed || !_regSet.rsRegsModified(RBM_RBP));
#endif
        var patchpoint = _compiler.info.compPatchpointInfo;
        var tier0IntSaves = new regMaskTP((regMask)patchpoint->CalleeSaveRegisters)
            & new regMaskTP(SRBM_OSR_INT_CALLEE_SAVED);
        var tier0SavedSize = BitOperations.PopCount((ulong)tier0IntSaves.IntRegSet) * REGSIZE_BYTES;

#if DEBUG
        if (_verbose)
        {
            jitprintf("--OSR--- tier0 has already saved ");
            dspRegMask(tier0IntSaves);
            jitprintf("\n");
        }
#endif
        assert((tier0IntSaves & RBM_RBP) == RBM_RBP);
        _compiler.unwindPush(REG_RBP);
        tier0IntSaves &= ~RBM_RBP;

        for (var reg = REG_INT_LAST; tier0IntSaves.IsNonEmpty; reg--)
        {
            var bit = regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);
            if ((bit & tier0IntSaves).IsNonEmpty)
            {
                _compiler.unwindPush(reg);
            }
            tier0IntSaves &= ~bit;
        }

        // The synthetic call slot is recorded after its instruction; the saved RBP
        // and other Tier0 pushes are already represented by unwindPush above.
        var tier0FrameSize = patchpoint->TotalFrameSize - REGSIZE_BYTES + REGSIZE_BYTES;
        var tier0NetSize = tier0FrameSize - tier0SavedSize;
        _compiler.unwindAllocStack(unchecked((uint)tier0NetSize));
#elif TARGET_ARM64
        assert(Emitter.emitGeneratingPrologOrFuncletProlog());
        assert(_compiler.opts.IsOSR);
        assert(_compiler.funCurrentFunc().funKind == FuncKind.FUNC_ROOT);

        var patchpoint = _compiler.info.compPatchpointInfo;
        var tier0CalleeSaves = new regMaskTP((regMask)patchpoint->CalleeSaveRegisters);

#if DEBUG
        if (_verbose)
        {
            jitprintf("--OSR--- tier0 has already saved ");
            dspRegMask(tier0CalleeSaves);
            jitprintf("\nEmitting restores\n");
        }
#endif

        var restoreRegsFrame = tier0CalleeSaves & new regMaskTP(SRBM_FP | SRBM_LR);
        var restoreRegsFloat = tier0CalleeSaves & new regMaskTP(SRBM_ALLFLOAT);
        var restoreRegsInt = tier0CalleeSaves & ~restoreRegsFrame & ~restoreRegsFloat;

        regNumber baseReg;
        int topOfCalleeSaves;
        if (restoreRegsFrame.IsNonEmpty)
        {
            // FP/LR are always at the top of the callee saves, so restore the rest relative to FP.
            baseReg = REG_FP;
            topOfCalleeSaves = 0;
        }
        else
        {
            // The offset from FP is unknown, but the SP-relative offset is known.
            baseReg = REG_SPBASE;
            topOfCalleeSaves = unchecked((int)patchpoint->TotalFrameSize);
            if (_compiler.info.compIsVarArgs)
            {
                topOfCalleeSaves -= MAX_REG_ARG * REGSIZE_BYTES;
            }

            if (topOfCalleeSaves > 504 && (restoreRegsInt.IsNonEmpty || restoreRegsFloat.IsNonEmpty))
            {
                // LDP cannot encode this offset from SP. Unwind padding below accounts for the extra instruction.
                genInstrWithConstant(INS_add, EA_PTRSIZE, REG_IP0, REG_SPBASE, topOfCalleeSaves, REG_IP0,
                    inUnwindRegion: false);
                baseReg = REG_IP0;
                topOfCalleeSaves = 0;
            }
        }

        if (restoreRegsInt.IsNonEmpty)
        {
            genRestoreCalleeSavedRegisterGroupArm64(restoreRegsInt, baseReg, 0, topOfCalleeSaves,
                reportUnwindData: false);
            topOfCalleeSaves -= genCalleeSaveCountArm64(restoreRegsInt) * REGSIZE_BYTES;
        }

        if (restoreRegsFloat.IsNonEmpty)
        {
            genRestoreCalleeSavedRegisterGroupArm64(restoreRegsFloat, baseReg, 0, topOfCalleeSaves,
                reportUnwindData: false);
            topOfCalleeSaves -= genCalleeSaveCountArm64(restoreRegsFloat) * REGSIZE_BYTES;
        }

        // FP always points to the saved FP/LR pair for frame-pointer chaining.
        // Restoring LR relies on Tier0 having been unhijacked when the OSR prolog runs.
        // The transition helper does this; direct OSR entry without it does not support
        // Tier0 hijacking, matching the tailcall behavior recorded by SetHasTailCalls.
        genRestoreRegPairArm64(REG_FP, REG_LR, REG_FP, 0, 0, false, REG_IP1, null,
            reportUnwindData: false);

        if (JitConfig.JitPacEnabled != 0)
        {
            // Tier0 signed LR with the caller SP before allocating its frame.
            // Recreate that SP to authenticate LR before the OSR prolog re-signs it.
            // TODO-PAC: Avoid authenticating and re-signing so the signing SP points to
            // the frame start; this may require a phantom pac_sign_lr unwind code.
            genInstrWithConstant(INS_add, EA_PTRSIZE, REG_IP0, REG_SPBASE,
                unchecked((nint)patchpoint->TotalFrameSize), REG_IP0, inUnwindRegion: false);
            Emitter.emitIns_Mov(INS_mov, EA_PTRSIZE, REG_IP1, REG_LR, canSkip: false);
            Emitter.emitIns(TargetOS.IsWindows ? INS_autib1716 : INS_autia1716);
            Emitter.emitIns_Mov(INS_mov, EA_PTRSIZE, REG_LR, REG_IP1, canSkip: false);
        }

        // Record the phantom Tier0 frame allocation and pad the prolog for ARM64 unwind codes.
        _compiler.unwindAllocStack(unchecked((uint)patchpoint->TotalFrameSize));
        _compiler.unwindPadding();
#else
        unreached();
#endif
    }

    public unsafe void genOSRSaveRemainingCalleeSavedRegisters()
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "OSR callee-save recording requires AMD64.");
#else
        assert(Emitter.emitGeneratingPrologOrFuncletProlog());
        assert(_compiler.opts.IsOSR);
        assert(_compiler.funCurrentFunc().funKind == FuncKind.FUNC_ROOT);
        var pushRegs = _regSet.rsGetModifiedOsrIntCalleeSavedRegsMask();
#if ETW_EBP_FRAMED
        noway_assert(IsFramePointerUsed || !_regSet.rsRegsModified(RBM_RBP));
#endif
        var patchpoint = _compiler.info.compPatchpointInfo;
        var tier0Saves = new regMaskTP((regMask)patchpoint->CalleeSaveRegisters);
        var tier0IntSaves = tier0Saves & new regMaskTP(SRBM_OSR_INT_CALLEE_SAVED);
        var tier0SavedSize = BitOperations.PopCount((ulong)tier0IntSaves.IntRegSet) * REGSIZE_BYTES;
        var osrIntSaves = pushRegs & new regMaskTP(SRBM_OSR_INT_CALLEE_SAVED);
        var additionalSaves = osrIntSaves & ~tier0IntSaves;

#if DEBUG
        if (_verbose)
        {
            jitprintf("---OSR--- int callee saves are ");
            dspRegMask(osrIntSaves);
            jitprintf("; tier0 already saved ");
            dspRegMask(tier0IntSaves);
            jitprintf("; so only saving ");
            dspRegMask(additionalSaves);
            jitprintf("\n");
        }
#endif
        var osrFrameSize = _compiler.compLclFrameSize;
        var tier0FrameSize = patchpoint->TotalFrameSize;
        var osrCalleeSaveSize = _compiler.compCalleeRegsPushed * REGSIZE_BYTES;
        var osrFramePointerSize = IsFramePointerUsed ? REGSIZE_BYTES : 0;
        var offset = osrFrameSize + osrCalleeSaveSize + osrFramePointerSize
            + tier0FrameSize - tier0SavedSize;

        assert((tier0Saves & RBM_RBP) == RBM_RBP);
        assert((additionalSaves & RBM_RBP).IsEmpty);

        // Extra saves occupy the reserved Tier0 save area, not the OSR frame.
        for (var reg = REG_INT_LAST; additionalSaves.IsNonEmpty; reg--)
        {
            var bit = regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);
            if ((bit & additionalSaves).IsNonEmpty)
            {
                Emitter.emitIns_AR_R(INS_mov, EA_8BYTE, reg, REG_SPBASE, offset);
                _compiler.unwindSaveReg(reg, unchecked((uint)offset));
                offset -= REGSIZE_BYTES;
            }
            additionalSaves &= ~bit;
        }
#endif
    }
}
