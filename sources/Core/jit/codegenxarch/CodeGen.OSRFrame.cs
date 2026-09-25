// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_AMD64 && WINDOWS_AMD64_ABI
using System.Numerics;
#endif

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public unsafe void genOSRHandleTier0CalleeSavedRegistersAndFrame()
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Tier0 OSR frame reconstruction requires Windows AMD64.");
#else
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
#endif
    }

    public unsafe void genOSRSaveRemainingCalleeSavedRegisters()
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "OSR callee-save recording requires Windows AMD64.");
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
