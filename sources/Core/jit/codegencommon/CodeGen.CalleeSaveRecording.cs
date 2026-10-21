// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Numerics;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genPushCalleeSavedRegisters(regNumber initReg, ref bool initRegZeroed)
    {
        if (Configuration.TARGET_WASM)
        {
            genPushCalleeSavedRegistersWasm(initReg, ref initRegZeroed);
            return;
        }

        if (Configuration.TARGET_ARM64)
        {
            genPushCalleeSavedRegistersArm64Core(initReg, ref initRegZeroed);
            return;
        }

        if (Configuration.TARGET_ARM)
        {
            genPushCalleeSavedRegistersArmCore(initReg, ref initRegZeroed);
            return;
        }

        if (Configuration.TARGET_RISCV64)
        {
            genPushCalleeSavedRegistersRiscV(initReg, ref initRegZeroed);
            return;
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
            genPushCalleeSavedRegistersLoongArch64(initReg, ref initRegZeroed);
            return;
        }

        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Callee-save pushes require xarch.");
            return;
        }

        if (Configuration.TARGET_AMD64)
        {
            Emitter.RequireSupportedInstructionRecording();
        }
        assert(Emitter.emitGeneratingPrologOrFuncletProlog());
        if (Configuration.DEBUG && _compiler.opts.IsOSR)
        {
            assert(_compiler.funCurrentFunc().funKind != FuncKind.FUNC_ROOT);
        }
        var pushRegs = _regSet.rsGetModifiedIntCalleeSavedRegsMask();
        if (Configuration.ETW_EBP_FRAMED)
        {
            noway_assert(IsFramePointerUsed || !_regSet.rsRegsModified(
                new regMaskTP(TargetRuntimeMetadata.SelectXarch(SRBM_X86_EBP, SRBM_X64_RBP))));
        }
        if (IsFramePointerUsed)
        {
            pushRegs &= ~new regMaskTP(TargetRuntimeMetadata.SelectXarch(SRBM_X86_EBP, SRBM_X64_RBP));
        }

        if (Configuration.DEBUG)
        {
            var count = BitOperations.PopCount((ulong)pushRegs.IntRegSet);
            if (_compiler.compCalleeRegsPushed != count)
            {
                jitprintf($"Error: unexpected number of callee-saved registers to push. Expected: {_compiler.compCalleeRegsPushed}. Got: {count} ");
                dspRegMask(pushRegs);
                jitprintf("\n");
                assert(_compiler.compCalleeRegsPushed == count);
            }
        }
        if (Configuration.TARGET_AMD64 && _compiler.canUseApxEvexEncoding() && (JitConfig.EnableApxPP2 != 0))
        {
            genPushCalleeSavedRegistersFromMaskAPX(pushRegs);
            return;
        }

        for (var reg = TargetRuntimeMetadata.IntegerRegisterLast; pushRegs.IsNonEmpty; reg--)
        {
            var bit = regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);
            if ((bit & pushRegs).IsNonEmpty)
            {
                if (Configuration.TARGET_AMD64)
                {
                    var options = _compiler.canUseApxEvexEncoding() && (JitConfig.EnableApxPPHint != 0)
                        ? INS_OPTS_APX_ppx : INS_OPTS_NONE;
                    Emitter.emitIns_R(INS_push, EA_PTRSIZE, reg, options);
                }
                else
                {
                    inst_RV(INS_push, reg, TYP_REF);
                }
                _compiler.unwindPush(reg);
                pushRegs &= ~bit;
            }
        }
    }

    public void genPushCalleeSavedRegistersFromMaskAPX(regMaskTP pushRegs)
    {
        if (!Configuration.TARGET_AMD64)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "APX callee-save pushes require AMD64.");
            return;
        }

        Emitter.RequireSupportedInstructionRecording();
        assert((_compiler.funCurrentFunc().funKind == FuncKind.FUNC_ROOT) && !_compiler.opts.IsOSR);
        assert((pushRegs & RBM_RSP).IsEmpty);
        if (!IsFramePointerUsed && pushRegs.IsNonEmpty)
        {
            // CALL misaligns SP; a single push restores the alignment required by PUSH2.
            var alignReg = (pushRegs & RBM_RBP).IsNonEmpty
                ? TargetRuntimeMetadata.FramePointerBaseRegister : (regNumber)BitOperations.TrailingZeroCount((ulong)pushRegs.IntRegSet);
            Emitter.emitIns_R(INS_push, EA_PTRSIZE, alignReg, INS_OPTS_APX_ppx);
            _compiler.unwindPush(alignReg);
            pushRegs &= ~regMaskTP.CreateFromRegNum(alignReg, alignReg.SingleTypeMask);
        }

        Span<regNumber> registers = stackalloc regNumber[unchecked((int)(TargetRuntimeMetadata.IntegerRegisterLast - TargetRuntimeMetadata.IntegerRegisterFirst + 1))];
        var count = 0;
        while (pushRegs.IsNonEmpty)
        {
            var reg = (regNumber)BitOperations.TrailingZeroCount((ulong)pushRegs.IntRegSet);
            registers[count++] = reg;
            pushRegs &= ~regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);
        }

        while (count > 1)
        {
            var reg1 = registers[--count];
            var reg2 = registers[--count];
            Emitter.emitIns_R_R(INS_push2, EA_PTRSIZE, reg1, reg2, INS_OPTS_EVEX_nd | INS_OPTS_APX_ppx);
            _compiler.unwindPush2(reg1, reg2);
        }
        if (count == 1)
        {
            var reg = registers[--count];
            Emitter.emitIns_R(INS_push, EA_PTRSIZE, reg, INS_OPTS_APX_ppx);
            _compiler.unwindPush(reg);
        }
        assert(count == 0);
    }

    public void genPreserveCalleeSavedFltRegs()
    {
        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Floating callee-save recording requires xarch.");
            return;
        }

        if (Configuration.TARGET_AMD64)
        {
            Emitter.RequireSupportedInstructionRecording();
        }

        var mask = new regMaskTP(_compiler.compCalleeFPRegsSavedMask);
        assert((mask & new regMaskTP(TargetRuntimeMetadata.FloatingCalleeSavedMask)) == mask);
        if (mask.IsEmpty)
        {
            return;
        }

        var padding = Configuration.TARGET_AMD64 && _compiler.lvaIsCalleeSavedIntRegCountEven()
            ? Configuration.REGSIZE_BYTES : 0;
        var ins = Configuration.TARGET_AMD64 ? ins_Copy(TYP_FLOAT) : INS_movupd;
        var offset = unchecked((uint)(_compiler.compLclFrameSize - padding - Configuration.XMM_REGSIZE_BYTES));
        if (Configuration.TARGET_AMD64)
        {
            assert((offset % 16) == 0);
        }
        for (var reg = TargetRuntimeMetadata.FloatingRegisterFirst; mask.IsNonEmpty; reg++)
        {
            var bit = regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);
            if ((bit & mask).IsNonEmpty)
            {
                // The ABI preserves only the low 128 bits of each vector register.
                Emitter.emitIns_AR_R(ins, EA_16BYTE, reg, TargetRuntimeMetadata.StackPointerBaseRegister, (nint)offset);
                _compiler.unwindSaveReg(reg, offset);
                mask &= ~bit;
                offset = unchecked(offset - (uint)Configuration.XMM_REGSIZE_BYTES);
            }
        }
    }

    public void genRestoreCalleeSavedFltRegs()
    {
        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Floating callee-save restoration requires xarch.");
            return;
        }

        if (Configuration.TARGET_AMD64)
        {
            Emitter.RequireSupportedInstructionRecording();
        }

        var mask = new regMaskTP(_compiler.compCalleeFPRegsSavedMask);
        assert((mask & new regMaskTP(TargetRuntimeMetadata.FloatingCalleeSavedMask)) == mask);
        if (mask.IsEmpty)
        {
            return;
        }

        var padding = Configuration.TARGET_AMD64 && _compiler.lvaIsCalleeSavedIntRegCountEven()
            ? Configuration.REGSIZE_BYTES : 0;
        var ins = Configuration.TARGET_AMD64 ? ins_Copy(TYP_FLOAT) : INS_movupd;
        var offset = unchecked((uint)(_compiler.compLclFrameSize - padding - Configuration.XMM_REGSIZE_BYTES));
        var baseReg = TargetRuntimeMetadata.StackPointerBaseRegister;
        if (_compiler.compLocallocUsed)
        {
            assert(IsFramePointerUsed);
            baseReg = TargetRuntimeMetadata.FramePointerBaseRegister;
            offset = unchecked((uint)(_compiler.compLclFrameSize - genSPtoFPdelta - padding - Configuration.XMM_REGSIZE_BYTES));
        }

        if (Configuration.TARGET_AMD64)
        {
            assert((offset % 16) == 0);
        }
        for (var reg = TargetRuntimeMetadata.FloatingRegisterFirst; mask.IsNonEmpty; reg++)
        {
            var bit = regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);
            if ((bit & mask).IsNonEmpty)
            {
                Emitter.emitIns_R_AR(ins, EA_16BYTE, reg, baseReg, unchecked((int)offset));
                mask &= ~bit;
                offset = unchecked(offset - (uint)Configuration.XMM_REGSIZE_BYTES);
            }
        }
    }
}
