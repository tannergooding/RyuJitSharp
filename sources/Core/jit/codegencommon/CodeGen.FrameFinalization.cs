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
        JITDUMP("Finalizing stack frame\n");
        assert(_compiler.RegisterAllocator is not null);
        assert(_compiler.fgFirstBB is not null);
        _compiler.RegisterAllocator.recordVarLocationsAtStartOfBB(_compiler.fgFirstBB);
        genCheckUseBlockInit();

        if (Configuration.HAS_FIXED_REGISTER_SET)
        {
            if (Configuration.TARGET_X86 && _compiler.compTailCallUsed)
            {
                _regSet.rsSetRegsModified(new regMaskTP(TargetRuntimeMetadata.IntegerCalleeSavedMask));
            }

            if (Configuration.TARGET_ARM)
            {
                if ((nuint)_compiler.compLclFrameSize >= _compiler.eeGetPageSize())
                {
                    var probeMask = new regMaskTP(SRBM_STACK_PROBE_HELPER_ARG_ARM
                        | SRBM_STACK_PROBE_HELPER_CALL_TARGET | SRBM_STACK_PROBE_HELPER_TRASH_ARM);
                    _regSet.rsSetRegsModified(probeMask);
                }

                if (_regSet.rsMaskResvd.IsNonEmpty)
                {
                    _regSet.rsSetRegsModified(_regSet.rsMaskResvd);
                }
            }

            if (Configuration.TARGET_ARM64 &&
                _compiler.IsTargetAbi(CORINFO_RUNTIME_ABI.CORINFO_NATIVEAOT_ABI) && TargetOS.IsApplePlatform)
            {
                JITDUMP("Setting genReverseAndPairCalleeSavedRegisters = true");
                genReverseAndPairCalleeSavedRegisters = true;

                // Pair adjacent saves to use STP while retaining 16-byte stack alignment.
                var modified = _regSet.rsGetModifiedRegsMask();
                var evenFloat = new regMaskTP(SRBM_ARM64_V8 | SRBM_ARM64_V10 | SRBM_ARM64_V12 | SRBM_ARM64_V14);
                var oddInt = new regMaskTP(SRBM_ARM64_R19 | SRBM_ARM64_R21 | SRBM_ARM64_R23
                    | SRBM_ARM64_R25 | SRBM_ARM64_R27);
                var pairedBits = (unchecked((ulong)(modified & evenFloat).Lower) << 1)
                    | (unchecked((ulong)(modified & oddInt).Lower) << 1);
                var paired = new regMaskTP(unchecked((regMask)pairedBits));
                if (paired.IsNonEmpty)
                {
                    _regSet.rsSetRegsModified(paired);
                }
            }

            if (Configuration.TARGET_ARM64 && _compiler.compUsesUnknownSizeFrame)
            {
                _regSet.rsSetRegsModified(new regMaskTP(SRBM_UNKBASE));
            }
#if DEBUG
            if (_verbose)
            {
                jitprintf("Modified regs: ");
                dspRegMask(_regSet.rsGetModifiedRegsMask());
                jitprintf("\n");
            }
#endif
            var framePointerReg = genFramePointerReg();
            var framePointer = genRegMask(framePointerReg);
            if (_compiler.opts.compDbgEnC)
            {
                noway_assert(IsFramePointerUsed);
                if (Configuration.TARGET_AMD64 || Configuration.TARGET_ARM64)
                {
                    var calleeTrash = Configuration.HAS_MORE_THAN_64_REGISTERS
                        ? new regMaskTP(SRBM_INT_CALLEE_TRASH | SRBM_FLT_CALLEE_TRASH, SRBM_MSK_CALLEE_TRASH)
                        : new regMaskTP(SRBM_INT_CALLEE_TRASH | SRBM_FLT_CALLEE_TRASH);
                    var encCalleeSaved = new regMaskTP(TargetRuntimeMetadata.EncodedCalleeSavedMask);
                    var okRegs = calleeTrash | framePointer | encCalleeSaved;
                    if (encCalleeSaved.IsNonEmpty)
                    {
                        _regSet.rsSetRegsModified(encCalleeSaved);
                    }
                    noway_assert((_regSet.rsGetModifiedRegsMask() & ~okRegs).IsEmpty);
                }
                else
                {
                    _regSet.rsSetRegsModified(new regMaskTP(TargetRuntimeMetadata.IntegerCalleeSavedMask) & ~framePointer);
                }
            }
            if (_compiler.compMethodRequiresPInvokeFrame)
            {
                noway_assert(IsFramePointerUsed);
                _regSet.rsSetRegsModified(new regMaskTP(TargetRuntimeMetadata.IntegerCalleeSavedMask) & ~framePointer);
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

            if (Configuration.UNIX_AMD64_ABI && _compiler.compIsProfilerHookNeeded)
            {
                _regSet.rsSetRegsModified(new regMaskTP(SRBM_X64_R14 | SRBM_X64_R15));
            }
            noway_assert(!doubleAlignOrFramePointerUsed() || !_regSet.rsRegsModified(framePointer));
            if (Configuration.ETW_EBP_FRAMED)
            {
                noway_assert(!_regSet.rsRegsModified(framePointer));
            }
            var maskCalleeRegsPushed = _regSet.rsGetModifiedCalleeSavedRegsMask();
            if (Configuration.TARGET_ARMARCH)
            {
                if (IsFramePointerUsed)
                {
                    maskCalleeRegsPushed |= framePointer;
                    assert(!_regSet.rsRegsModified(framePointer));
                }

                var linkRegisterMask = Configuration.TARGET_ARM
                    ? SRBM_ARM32_LR
                    : SRBM_ARM64_LR;
                maskCalleeRegsPushed |= new regMaskTP(linkRegisterMask);
                if (Configuration.TARGET_ARM)
                {
                    var floatPush = maskCalleeRegsPushed & allFloat;
                    var intPush = maskCalleeRegsPushed & ~floatPush;
                    if (floatPush.IsNonEmpty
                        || (_compiler.opts.MinOpts && (_regSet.rsMaskResvd & maskCalleeRegsPushed
                            & new regMaskTP(SRBM_OPT_RSVD_ARM)).IsNonEmpty))
                    {
                        var pushBits = unchecked((ulong)(_regSet.rsMaskPreSpillRegs(true) | intPush).Lower);
                        if ((BitOperations.PopCount(pushBits) % 2) != 0)
                        {
                            var extraReg = REG_ARM32_R4;
                            while ((intPush & regMaskTP.CreateFromRegNum(extraReg, extraReg.SingleTypeMask)).IsNonEmpty)
                            {
                                extraReg = (regNumber)((int)extraReg + 1);
                            }
                            if (extraReg < REG_ARM32_R11)
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
                        var firstPairBits = unchecked((ulong)(SRBM_ARM32_F16 | SRBM_ARM32_F17));
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
                }
            }
            if (Configuration.TARGET_XARCH)
            {
                _compiler.compCalleeFPRegsSavedMask = maskCalleeRegsPushed.Lower & TargetRuntimeMetadata.FloatingCalleeSavedMask;
                maskCalleeRegsPushed &= ~new regMaskTP(TargetRuntimeMetadata.FloatingCalleeSavedMask);
            }
            if (Configuration.TARGET_LOONGARCH64 || Configuration.TARGET_RISCV64)
            {
                assert(!_regSet.rsRegsModified(framePointer));
                assert(IsFramePointerUsed);
                var returnAddressMask = Configuration.TARGET_LOONGARCH64
                    ? SRBM_LOONGARCH64_RA
                    : SRBM_RISCV64_RA;
                maskCalleeRegsPushed |= framePointer | new regMaskTP(returnAddressMask);
            }
            _compiler.compCalleeRegsPushed = BitOperations.PopCount(unchecked((ulong)maskCalleeRegsPushed.Lower));
#if DEBUG
            if (_verbose)
            {
                jitprintf($"Callee-saved registers pushed: {_compiler.compCalleeRegsPushed} ");
                dspRegMask(maskCalleeRegsPushed);
                jitprintf("\n");
            }
#endif
        }
        _compiler.lvaAssignFrameOffsets(Compiler.FINAL_FRAME_LAYOUT);
#if DEBUG
        if (_compiler.opts.dspCode || _compiler.opts.disAsm || _compiler.opts.disAsm2 || _verbose)
        {
            _compiler.lvaTableDump();
        }
#endif
    }
}
