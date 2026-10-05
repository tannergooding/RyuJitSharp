// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using System.Numerics;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    internal bool genUsedPopToReturn;

    private unsafe void genPushCalleeSavedRegistersArmCore(regNumber initReg, ref bool initRegZeroed)
    {
        assert(Emitter.emitGeneratingPrologOrFuncletProlog());

        var pushRegs = _regSet.rsGetModifiedCalleeSavedRegsMask();
#if ETW_EBP_FRAMED
        if (!IsFramePointerUsed && _regSet.rsRegsModified(new regMaskTP(SRBM_FPBASE)))
        {
            noway_assert(false, "!\"Used register RBM_FPBASE as a scratch register!\"");
        }
#endif
        if (IsFramePointerUsed)
        {
            pushRegs |= new regMaskTP(SRBM_FPBASE);
        }

        // Always save LR: partially interruptible leaf methods still require return-address hijacking.
        pushRegs |= new regMaskTP(SRBM_LR);
        _regSet.rsSetCalleeSavedRegsMask(pushRegs);

#if DEBUG
        var count = BitOperations.PopCount(unchecked((ulong)pushRegs.Lower));
        if (_compiler.compCalleeRegsPushed != count)
        {
            jitprintf($"Error: unexpected number of callee-saved registers to push. Expected: {_compiler.compCalleeRegsPushed}. " +
                $"Got: {count} ");
            dspRegMask(pushRegs);
            jitprintf("\n");
            assert(_compiler.compCalleeRegsPushed == count);
        }
#endif
        var pushFloat = pushRegs & new regMaskTP(SRBM_ALLFLOAT);
        var pushInt = pushRegs & ~pushFloat;
        pushInt |= genStackAllocRegisterMaskArmCore(unchecked((uint)_compiler.compLclFrameSize), pushFloat);

        assert(unchecked((ulong)pushInt.Lower) <= int.MaxValue);
        inst_IV(INS_push, unchecked((int)pushInt.Lower));
        _compiler.unwindPushMaskInt(pushInt);

        if (pushFloat.IsNonEmpty)
        {
            genPushFltRegsArmCore(pushFloat);
            unwindPushMaskFloatArmCore(pushFloat);
        }
    }

    private unsafe void genPopCalleeSavedRegistersArmCore(bool jmpEpilog = false)
    {
        assert(Emitter.emitGeneratingEpilogOrFuncletEpilog());

        var popRegs = _regSet.rsGetModifiedCalleeSavedRegsMask();
        var popFloat = popRegs & new regMaskTP(SRBM_ALLFLOAT);
        var popInt = popRegs & ~popFloat;

        if (popFloat.IsNonEmpty)
        {
            genPopFltRegsArmCore(popFloat);
            unwindPopMaskFloatArmCore(popFloat);
        }

        if (!jmpEpilog)
        {
            var stackAlloc = genStackAllocRegisterMaskArmCore(unchecked((uint)_compiler.compLclFrameSize), popFloat);
            popInt |= stackAlloc;
        }

        if (IsFramePointerUsed)
        {
            assert(!_regSet.rsRegsModified(new regMaskTP(SRBM_FPBASE)));
            popInt |= new regMaskTP(SRBM_FPBASE);
        }

        if (genCanUsePopToReturnArmCore(popInt, jmpEpilog))
        {
            popInt |= new regMaskTP(SRBM_PC);
            genUsedPopToReturn = true;
        }
        else
        {
            popInt |= new regMaskTP(SRBM_LR);
            genUsedPopToReturn = false;
        }

        assert(unchecked((ulong)popInt.Lower) <= int.MaxValue);
        inst_IV(INS_pop, unchecked((int)popInt.Lower));
        unwindPopMaskIntArmCore(popInt);
    }

    private void genPushFltRegsArmCore(regMaskTP regMask)
    {
        assert(regMask.IsNonEmpty, conditionExpression: "regMask != 0");
        assert((regMask & new regMaskTP(SRBM_ALLFLOAT)) == regMask,
            conditionExpression: "(regMask & RBM_ALLFLOAT) == regMask");

        var bits = unchecked((ulong)regMask.Lower);
        var lowReg = (regNumber)BitOperations.TrailingZeroCount(bits);
        var slots = BitOperations.PopCount(bits);
        var tmpMask = unchecked((bits >> (int)lowReg) + 1);
        assert(LsraGlobals.genMaxOneBit(unchecked((SingleTypeRegSet)tmpMask)),
            conditionExpression: "genMaxOneBit(tmpMask)");
        assert(lowReg == REG_F16, conditionExpression: "lowReg == REG_F16");

        noway_assert(floatRegCanHoldType(lowReg, TYP_DOUBLE));
        noway_assert((slots % 2) == 0);
        Emitter.emitIns_R_I(INS_vpush, EA_8BYTE, lowReg, slots / 2);
    }

    private void genPopFltRegsArmCore(regMaskTP regMask)
    {
        assert(regMask.IsNonEmpty, conditionExpression: "regMask != 0");
        assert((regMask & new regMaskTP(SRBM_ALLFLOAT)) == regMask,
            conditionExpression: "(regMask & RBM_ALLFLOAT) == regMask");

        var bits = unchecked((ulong)regMask.Lower);
        var lowReg = (regNumber)BitOperations.TrailingZeroCount(bits);
        var slots = BitOperations.PopCount(bits);
        var tmpMask = unchecked((bits >> (int)lowReg) + 1);
        assert(LsraGlobals.genMaxOneBit(unchecked((SingleTypeRegSet)tmpMask)),
            conditionExpression: "genMaxOneBit(tmpMask)");

        noway_assert(floatRegCanHoldType(lowReg, TYP_DOUBLE));
        noway_assert((slots % 2) == 0);
        Emitter.emitIns_R_I(INS_vpop, EA_8BYTE, lowReg, slots / 2);
    }

    private unsafe regMaskTP genStackAllocRegisterMaskArmCore(uint frameSize, regMaskTP maskCalleeSavedFloat)
    {
        assert(Emitter.emitGeneratingPrologOrFuncletProlog() || Emitter.emitGeneratingEpilogOrFuncletEpilog());

        // Extra integer pushes would place the local frame on the wrong side of saved VFP registers.
        if (maskCalleeSavedFloat.IsNonEmpty)
        {
            return RBM_NONE;
        }

        // A pop must not overwrite the async continuation return register.
        if (_compiler.compIsAsync)
        {
            return RBM_NONE;
        }

        // R0/R1 can contain return values; only one- and two-word frames use scratch-register pushes.
        return frameSize switch
        {
            REGSIZE_BYTES => new regMaskTP(SRBM_R3),
            2 * REGSIZE_BYTES => new regMaskTP(SRBM_R2 | SRBM_R3),
            _ => RBM_NONE,
        };
    }

    private bool genCanUsePopToReturnArmCore(regMaskTP maskPopRegsInt, bool jmpEpilog)
    {
        assert(Emitter.emitGeneratingEpilogOrFuncletEpilog());

        if (!jmpEpilog && _regSet.rsMaskPreSpillRegs(true).IsEmpty)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    private void unwindPushMaskFloatArmCore(regMaskTP mask)
    {
        _compiler.unwindPushMaskFloat(mask);
    }

    private void unwindPopMaskIntArmCore(regMaskTP mask)
    {
        _compiler.unwindPopMaskInt(mask);
    }

    private void unwindPopMaskFloatArmCore(regMaskTP mask)
    {
        _compiler.unwindPopMaskFloat(mask);
    }
}
#endif
