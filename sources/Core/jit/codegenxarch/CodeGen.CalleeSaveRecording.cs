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
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Callee-save pushes require Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(Emitter.emitGeneratingPrologOrFuncletProlog());
#if DEBUG
        if (_compiler.opts.IsOSR)
        {
            assert(_compiler.funCurrentFunc().funKind != FuncKind.FUNC_ROOT);
        }
#endif
        var pushRegs = _regSet.rsGetModifiedIntCalleeSavedRegsMask();
#if ETW_EBP_FRAMED
        noway_assert(IsFramePointerUsed || !_regSet.rsRegsModified(RBM_RBP));
#endif
        if (IsFramePointerUsed)
        {
            pushRegs &= ~RBM_RBP;
        }

#if DEBUG
        var count = BitOperations.PopCount((ulong)pushRegs.IntRegSet);
        if (_compiler.compCalleeRegsPushed != count)
        {
            jitprintf($"Error: unexpected number of callee-saved registers to push. Expected: {_compiler.compCalleeRegsPushed}. Got: {count} ");
            dspRegMask(pushRegs);
            jitprintf("\n");
            assert(_compiler.compCalleeRegsPushed == count);
        }
#endif
        if (_compiler.canUseApxEvexEncoding() && (JitConfig.EnableApxPP2 != 0))
        {
            genPushCalleeSavedRegistersFromMaskAPX(pushRegs);
            return;
        }

        for (var reg = REG_INT_LAST; pushRegs.IsNonEmpty; reg--)
        {
            var bit = regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);
            if ((bit & pushRegs).IsNonEmpty)
            {
                var options = _compiler.canUseApxEvexEncoding() && (JitConfig.EnableApxPPHint != 0)
                    ? INS_OPTS_APX_ppx : INS_OPTS_NONE;
                Emitter.emitIns_R(INS_push, EA_PTRSIZE, reg, options);
                _compiler.unwindPush(reg);
                pushRegs &= ~bit;
            }
        }
#endif
    }

    public void genPushCalleeSavedRegistersFromMaskAPX(regMaskTP pushRegs)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "APX callee-save pushes require Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert((_compiler.funCurrentFunc().funKind == FuncKind.FUNC_ROOT) && !_compiler.opts.IsOSR);
        assert((pushRegs & RBM_RSP).IsEmpty);
        if (!IsFramePointerUsed && pushRegs.IsNonEmpty)
        {
            // CALL misaligns SP; a single push restores the alignment required by PUSH2.
            var alignReg = (pushRegs & RBM_RBP).IsNonEmpty
                ? REG_FPBASE : (regNumber)BitOperations.TrailingZeroCount((ulong)pushRegs.IntRegSet);
            Emitter.emitIns_R(INS_push, EA_PTRSIZE, alignReg, INS_OPTS_APX_ppx);
            _compiler.unwindPush(alignReg);
            pushRegs &= ~regMaskTP.CreateFromRegNum(alignReg, alignReg.SingleTypeMask);
        }

        Span<regNumber> registers = stackalloc regNumber[REG_INT_LAST - REG_INT_FIRST + 1];
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
#endif
    }

    public void genPreserveCalleeSavedFltRegs()
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Floating callee-save recording requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        var mask = new regMaskTP(_compiler.compCalleeFPRegsSavedMask);
        assert((mask & new regMaskTP(SRBM_FLT_CALLEE_SAVED)) == mask);
        if (mask.IsEmpty)
        {
            return;
        }

        var padding = _compiler.lvaIsCalleeSavedIntRegCountEven() ? REGSIZE_BYTES : 0;
        var offset = unchecked((uint)(_compiler.compLclFrameSize - padding - XMM_REGSIZE_BYTES));
        assert((offset % 16) == 0);
        var ins = ins_Copy(TYP_FLOAT);
        for (var reg = REG_FLT_CALLEE_SAVED_FIRST; mask.IsNonEmpty; reg++)
        {
            var bit = regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);
            if ((bit & mask).IsNonEmpty)
            {
                // The ABI preserves only the low 128 bits of each vector register.
                Emitter.emitIns_AR_R(ins, EA_16BYTE, reg, REG_SPBASE, (nint)offset);
                _compiler.unwindSaveReg(reg, offset);
                mask &= ~bit;
                offset = unchecked(offset - XMM_REGSIZE_BYTES);
            }
        }
#endif
    }

    public void genRestoreCalleeSavedFltRegs()
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Floating callee-save restoration requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        var mask = new regMaskTP(_compiler.compCalleeFPRegsSavedMask);
        assert((mask & new regMaskTP(SRBM_FLT_CALLEE_SAVED)) == mask);
        if (mask.IsEmpty)
        {
            return;
        }

        var padding = _compiler.lvaIsCalleeSavedIntRegCountEven() ? REGSIZE_BYTES : 0;
        var offset = unchecked((uint)(_compiler.compLclFrameSize - padding - XMM_REGSIZE_BYTES));
        var baseReg = REG_SPBASE;
        if (_compiler.compLocallocUsed)
        {
            assert(IsFramePointerUsed);
            baseReg = REG_FPBASE;
            offset = unchecked(offset - (uint)genSPtoFPdelta);
        }

        assert((offset % 16) == 0);
        var ins = ins_Copy(TYP_FLOAT);
        for (var reg = REG_FLT_CALLEE_SAVED_FIRST; mask.IsNonEmpty; reg++)
        {
            var bit = regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);
            if ((bit & mask).IsNonEmpty)
            {
                Emitter.emitIns_R_AR(ins, EA_16BYTE, reg, baseReg, unchecked((int)offset));
                mask &= ~bit;
                offset = unchecked(offset - XMM_REGSIZE_BYTES);
            }
        }
#endif
    }
}
