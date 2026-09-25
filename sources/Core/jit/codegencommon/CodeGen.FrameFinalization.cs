// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Numerics;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genFinalizeFrame()
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Frame finalization requires Windows AMD64.");
#else
        JITDUMP("Finalizing stack frame\n");
        assert(_compiler.RegisterAllocator is not null);
        assert(_compiler.fgFirstBB is not null);
        _compiler.RegisterAllocator.recordVarLocationsAtStartOfBB(_compiler.fgFirstBB);
        genCheckUseBlockInit();

#if DEBUG
        if (_verbose)
        {
            jitprintf("Modified regs: ");
            dspRegMask(_regSet.rsGetModifiedRegsMask());
            jitprintf("\n");
        }
#endif
        var framePointer = new regMaskTP(SRBM_FPBASE);
        if (_compiler.opts.compDbgEnC)
        {
            noway_assert(IsFramePointerUsed);
            var calleeTrash = new regMaskTP(SRBM_INT_CALLEE_TRASH | SRBM_FLT_CALLEE_TRASH, SRBM_MSK_CALLEE_TRASH);
            var encCalleeSaved = new regMaskTP(SRBM_ENC_CALLEE_SAVED);
            var okRegs = calleeTrash | framePointer | encCalleeSaved;
            if (encCalleeSaved.IsNonEmpty)
            {
                _regSet.rsSetRegsModified(encCalleeSaved);
            }
            noway_assert((_regSet.rsGetModifiedRegsMask() & ~okRegs).IsEmpty);
        }
        if (_compiler.compMethodRequiresPInvokeFrame)
        {
            noway_assert(IsFramePointerUsed);
            _regSet.rsSetRegsModified(new regMaskTP(SRBM_INT_CALLEE_SAVED) & ~framePointer);
        }

        // Finalization must account for a homing scratch register before fixing
        // the saved-register area. Integer and floating-point banks are independent.
        var homingCandidates = genGetParameterHomingTempRegisterCandidates();
        var allInt = new regMaskTP(SRBM_ALLINT);
        var allFloat = new regMaskTP(SRBM_ALLFLOAT);
        if (((homingCandidates & ~_calleeRegArgMaskLiveIn) & allInt).IsEmpty)
        {
            var extraRegMask = allInt & ~homingCandidates & ~_regSet.rsMaskResvd;
            assert(extraRegMask.IsNonEmpty);
            var extraReg = (regNumber)BitOperations.TrailingZeroCount((ulong)extraRegMask.Lower);
            JITDUMP($"No temporary registers are available for integer parameter homing. Adding {extraReg.Name}\n");
            _regSet.rsSetRegsModified(regMaskTP.CreateFromRegNum(extraReg, extraReg.SingleTypeMask));
        }
        if (((homingCandidates & ~_calleeRegArgMaskLiveIn) & allFloat).IsEmpty)
        {
            var extraRegMask = allFloat & ~homingCandidates & ~_regSet.rsMaskResvd;
            assert(extraRegMask.IsNonEmpty);
            var extraReg = (regNumber)BitOperations.TrailingZeroCount((ulong)extraRegMask.Lower);
            JITDUMP($"No temporary registers are available for float parameter homing. Adding {extraReg.Name}\n");
            _regSet.rsSetRegsModified(regMaskTP.CreateFromRegNum(extraReg, extraReg.SingleTypeMask));
        }

        noway_assert(!IsFramePointerUsed || !_regSet.rsRegsModified(framePointer));
#if ETW_EBP_FRAMED
        noway_assert(!_regSet.rsRegsModified(framePointer));
#endif
        var maskCalleeRegsPushed = _regSet.rsGetModifiedCalleeSavedRegsMask();
        _compiler.compCalleeFPRegsSavedMask = maskCalleeRegsPushed.Lower & SRBM_FLT_CALLEE_SAVED;
        maskCalleeRegsPushed &= ~new regMaskTP(SRBM_FLT_CALLEE_SAVED);
        _compiler.compCalleeRegsPushed = BitOperations.PopCount((ulong)maskCalleeRegsPushed.Lower);
#if DEBUG
        if (_verbose)
        {
            jitprintf($"Callee-saved registers pushed: {_compiler.compCalleeRegsPushed} ");
            dspRegMask(maskCalleeRegsPushed);
            jitprintf("\n");
        }
#endif
        _compiler.lvaAssignFrameOffsets(Compiler.FINAL_FRAME_LAYOUT);
#if DEBUG
        if (_compiler.opts.dspCode || _compiler.opts.disAsm || _compiler.opts.disAsm2 || _verbose)
        {
            _compiler.lvaTableDump();
        }
#endif
#endif
    }
}
