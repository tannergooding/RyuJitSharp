// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if HAS_FIXED_REGISTER_SET
using System.Numerics;
#endif

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genFinalizeFrame()
    {
        JITDUMP("Finalizing stack frame\n");
        assert(_compiler.RegisterAllocator is not null);
        assert(_compiler.fgFirstBB is not null);
        _compiler.RegisterAllocator.recordVarLocationsAtStartOfBB(_compiler.fgFirstBB);
        genCheckUseBlockInit();

#if HAS_FIXED_REGISTER_SET
#if TARGET_X86
        if (_compiler.compTailCallUsed)
        {
            _regSet.rsSetRegsModified(new regMaskTP(SRBM_INT_CALLEE_SAVED));
        }
#endif
#if TARGET_ARM
        if (_compiler.compLclFrameSize >= _compiler.eeGetPageSize())
        {
            var probeMask = new regMaskTP(SRBM_STACK_PROBE_HELPER_ARG
                | SRBM_STACK_PROBE_HELPER_CALL_TARGET | SRBM_STACK_PROBE_HELPER_TRASH);
            _regSet.rsSetRegsModified(probeMask);
        }

        if (_regSet.rsMaskResvd.IsNonEmpty)
        {
            _regSet.rsSetRegsModified(_regSet.rsMaskResvd);
        }
#endif
#if TARGET_ARM64
        if (_compiler.IsTargetAbi(CORINFO_RUNTIME_ABI.CORINFO_NATIVEAOT_ABI) && TargetOS.IsApplePlatform)
        {
            JITDUMP("Setting genReverseAndPairCalleeSavedRegisters = true");
            genReverseAndPairCalleeSavedRegisters = true;

            // Pair adjacent saves to use STP while retaining 16-byte stack alignment.
            var modified = _regSet.rsGetModifiedRegsMask();
            var evenFloat = new regMaskTP(SRBM_V8 | SRBM_V10 | SRBM_V12 | SRBM_V14);
            var oddInt = new regMaskTP(SRBM_R19 | SRBM_R21 | SRBM_R23 | SRBM_R25 | SRBM_R27);
            var pairedBits = (unchecked((ulong)(modified & evenFloat).Lower) << 1)
                | (unchecked((ulong)(modified & oddInt).Lower) << 1);
            var paired = new regMaskTP(unchecked((regMask)pairedBits));
            if (paired.IsNonEmpty)
            {
                _regSet.rsSetRegsModified(paired);
            }
        }

        if (_compiler.compUsesUnknownSizeFrame)
        {
            _regSet.rsSetRegsModified(new regMaskTP(SRBM_UNKBASE));
        }
#endif
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
#if TARGET_AMD64 || TARGET_ARM64
#if HAS_MORE_THAN_64_REGISTERS
            var calleeTrash = new regMaskTP(SRBM_INT_CALLEE_TRASH | SRBM_FLT_CALLEE_TRASH, SRBM_MSK_CALLEE_TRASH);
#else
            var calleeTrash = new regMaskTP(SRBM_INT_CALLEE_TRASH | SRBM_FLT_CALLEE_TRASH);
#endif
            var encCalleeSaved = new regMaskTP(SRBM_ENC_CALLEE_SAVED);
            var okRegs = calleeTrash | framePointer | encCalleeSaved;
            if (encCalleeSaved.IsNonEmpty)
            {
                _regSet.rsSetRegsModified(encCalleeSaved);
            }
            noway_assert((_regSet.rsGetModifiedRegsMask() & ~okRegs).IsEmpty);
#else
            _regSet.rsSetRegsModified(new regMaskTP(SRBM_INT_CALLEE_SAVED) & ~framePointer);
#endif
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
            var extraReg = (regNumber)BitOperations.TrailingZeroCount(unchecked((ulong)extraRegMask.Lower));
            JITDUMP($"No temporary registers are available for integer parameter homing. Adding {extraReg.Name}\n");
            _regSet.rsSetRegsModified(regMaskTP.CreateFromRegNum(extraReg, extraReg.SingleTypeMask));
        }
        if (((homingCandidates & ~_calleeRegArgMaskLiveIn) & allFloat).IsEmpty)
        {
            var extraRegMask = allFloat & ~homingCandidates & ~_regSet.rsMaskResvd;
            assert(extraRegMask.IsNonEmpty);
            var extraReg = (regNumber)BitOperations.TrailingZeroCount(unchecked((ulong)extraRegMask.Lower));
            JITDUMP($"No temporary registers are available for float parameter homing. Adding {extraReg.Name}\n");
            _regSet.rsSetRegsModified(regMaskTP.CreateFromRegNum(extraReg, extraReg.SingleTypeMask));
        }

#if UNIX_AMD64_ABI
        if (_compiler.compIsProfilerHookNeeded)
        {
            _regSet.rsSetRegsModified(new regMaskTP(SRBM_PROFILER_ENTER_ARG_0 | SRBM_PROFILER_ENTER_ARG_1));
        }
#endif
        noway_assert(!doubleAlignOrFramePointerUsed() || !_regSet.rsRegsModified(framePointer));
#if ETW_EBP_FRAMED
        noway_assert(!_regSet.rsRegsModified(framePointer));
#endif
        var maskCalleeRegsPushed = _regSet.rsGetModifiedCalleeSavedRegsMask();
#if TARGET_ARMARCH
        if (IsFramePointerUsed)
        {
            maskCalleeRegsPushed |= framePointer;
            assert(!_regSet.rsRegsModified(framePointer));
        }

        maskCalleeRegsPushed |= new regMaskTP(SRBM_LR);
#if TARGET_ARM
        var floatPush = maskCalleeRegsPushed & allFloat;
        var intPush = maskCalleeRegsPushed & ~floatPush;
        if (floatPush.IsNonEmpty
            || (_compiler.opts.MinOpts && (_regSet.rsMaskResvd & maskCalleeRegsPushed
                & new regMaskTP(SRBM_OPT_RSVD)).IsNonEmpty))
        {
            var pushBits = unchecked((ulong)(_regSet.rsMaskPreSpillRegs(true) | intPush).Lower);
            if ((BitOperations.PopCount(pushBits) % 2) != 0)
            {
                var extraReg = REG_R4;
                while ((intPush & regMaskTP.CreateFromRegNum(extraReg, extraReg.SingleTypeMask)).IsNonEmpty)
                {
                    extraReg = (regNumber)((int)extraReg + 1);
                }
                if (extraReg < REG_R11)
                {
                    var extraMask = regMaskTP.CreateFromRegNum(extraReg, extraReg.SingleTypeMask);
                    intPush |= extraMask;
                    _regSet.rsSetRegsModified(extraMask);
                }
            }
            maskCalleeRegsPushed = intPush | floatPush;
        }

        if (floatPush.IsNonEmpty)
        {
            // ARM pushes a consecutive run of double-sized FP registers beginning at F16/F17.
            var firstPairBits = unchecked((ulong)(SRBM_F16 | SRBM_F17));
            var contiguousBits = firstPairBits;
            var floatBits = unchecked((ulong)floatPush.Lower);
            while (floatBits > contiguousBits)
            {
                contiguousBits = (contiguousBits << 2) | firstPairBits;
            }
            if (floatBits != contiguousBits)
            {
                var extraMask = new regMaskTP(unchecked((regMask)(contiguousBits - floatBits)));
                _regSet.rsSetRegsModified(extraMask);
                maskCalleeRegsPushed |= extraMask;
            }
        }
#endif
#endif
#if TARGET_XARCH
        _compiler.compCalleeFPRegsSavedMask = maskCalleeRegsPushed.Lower & SRBM_FLT_CALLEE_SAVED;
        maskCalleeRegsPushed &= ~new regMaskTP(SRBM_FLT_CALLEE_SAVED);
#endif
#if TARGET_LOONGARCH64 || TARGET_RISCV64
        assert(!_regSet.rsRegsModified(framePointer));
        assert(IsFramePointerUsed);
        maskCalleeRegsPushed |= framePointer | new regMaskTP(SRBM_RA);
#endif
        _compiler.compCalleeRegsPushed = BitOperations.PopCount(unchecked((ulong)maskCalleeRegsPushed.Lower));
#if DEBUG
        if (_verbose)
        {
            jitprintf($"Callee-saved registers pushed: {_compiler.compCalleeRegsPushed} ");
            dspRegMask(maskCalleeRegsPushed);
            jitprintf("\n");
        }
#endif
#endif
        _compiler.lvaAssignFrameOffsets(Compiler.FINAL_FRAME_LAYOUT);
#if DEBUG
        if (_compiler.opts.dspCode || _compiler.opts.disAsm || _compiler.opts.disAsm2 || _verbose)
        {
            _compiler.lvaTableDump();
        }
#endif
    }
}
