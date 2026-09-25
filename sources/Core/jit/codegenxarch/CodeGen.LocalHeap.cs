// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void inst_IV(instruction ins, nint value)
    {
        Emitter.emitIns_I(ins, EA_PTRSIZE, value);
    }

    public void genStackPointerConstantAdjustment(nint spDelta, bool trackSpAdjustments)
    {
#if !TARGET_AMD64 || UNIX_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Stack-pointer adjustment requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(spDelta < 0);
        assert(unchecked((nuint)(-spDelta)) <= _compiler.eeGetPageSize());
        // AMD64 always exposes this constant adjustment; the flag is x86-only.
        inst_RV_IV(INS_sub, REG_SPBASE, unchecked(-spDelta), EA_PTRSIZE);
#endif
    }

    public void genStackPointerConstantAdjustmentWithProbe(nint spDelta, bool trackSpAdjustments)
    {
#if !TARGET_AMD64 || UNIX_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Stack probing requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        Emitter.emitIns_AR_R(INS_test, EA_4BYTE, REG_SPBASE, REG_SPBASE, 0);
        genStackPointerConstantAdjustment(spDelta, trackSpAdjustments);
#endif
    }

    public nint genStackPointerConstantAdjustmentLoopWithProbe(nint spDelta, bool trackSpAdjustments)
    {
#if !TARGET_AMD64 || UNIX_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Stack probing requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(spDelta < 0);
        var pageSize = _compiler.eeGetPageSize();
        var remaining = spDelta;

        do
        {
            var oneDelta = unchecked(-(nint)nuint.Min((nuint)(-remaining), pageSize));
            genStackPointerConstantAdjustmentWithProbe(oneDelta, trackSpAdjustments);
            remaining = unchecked(remaining - oneDelta);
        }
        while (remaining < 0);

        // Probes precede subtraction. Exact-page allocations need a final touch
        // so a following predecrement cannot step over an untouched guard page.
        var lastTouchDelta = unchecked((nuint)(-spDelta)) % pageSize;
        if ((lastTouchDelta == 0) ||
            (unchecked(lastTouchDelta + STACK_PROBE_BOUNDARY_THRESHOLD_BYTES) > pageSize))
        {
            Emitter.emitIns_AR_R(INS_test, EA_PTRSIZE, REG_EAX, REG_SPBASE, 0);
            lastTouchDelta = 0;
        }

        return unchecked((nint)lastTouchDelta);
#endif
    }

    public void genStackPointerDynamicAdjustmentWithProbe(regNumber regSpDelta)
    {
#if !TARGET_AMD64 || UNIX_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Dynamic stack probing requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(regSpDelta != REG_NA);
        var loop = genCreateTempLabel();
        inst_RV_RV(INS_add, regSpDelta, REG_SPBASE, TYP_I_IMPL);
        inst_JMP(EJ_jb, loop);
        instGen_Set_Reg_To_Zero(EA_PTRSIZE, regSpDelta);
        genDefineTempLabel(loop);

        // Touch the current SP before moving it: it may already be on the
        // guard page. The carry test above clamps a wrapped target to zero.
        Emitter.emitIns_AR_R(INS_test, EA_4BYTE, REG_SPBASE, REG_SPBASE, 0);
        inst_RV_IV(INS_sub_hide, REG_SPBASE, unchecked((nint)_compiler.eeGetPageSize()), EA_PTRSIZE);
        inst_RV_RV(INS_cmp, REG_SPBASE, regSpDelta, TYP_I_IMPL);
        inst_JMP(EJ_jae, loop);
        inst_Mov(TYP_I_IMPL, REG_SPBASE, regSpDelta, canSkip: false);
#endif
    }

    public void genLclHeap(GenTree tree)
    {
#if !TARGET_AMD64 || UNIX_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Local heap generation requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(tree.Oper == GT_LCLHEAP);
        assert(_compiler.compLocallocUsed);
        var size = tree.AsUnOp().Op1;
        noway_assert(size.Type.ActualType is TYP_INT or TYP_I_IMPL);
        var targetReg = tree.RegNum;
        var regCnt = REG_NA;
        var type = size.Type.ActualType;
        BasicBlock? endLabel = null;
        nint lastTouchDelta = -1;

#if DEBUG
        genStackPointerCheck(_compiler.opts.compStackCheckOnRet, _compiler.lvaReturnSpCheck);
#endif
        noway_assert(IsFramePointerUsed);
        noway_assert(genStackLevel == 0);
        nuint stackAdjustment = 0;
        nuint locAllocStackOffset = 0;
        nuint amount = 0;

        if (size.Oper.IsCnsIntOrI && size.IsContained)
        {
            amount = unchecked((nuint)size.AsIntCon().IconValue);
            assert((amount > 0) && (amount <= uint.MaxValue));
            amount = unchecked((amount + STACK_ALIGN - 1) & ~(nuint)(STACK_ALIGN - 1));
        }
        else
        {
            genConsumeRegAndCopy(size, targetReg);
            endLabel = genCreateTempLabel();
            Emitter.emitIns_R_R(INS_test, type.EmitSize, targetReg, targetReg);
            inst_JMP(EJ_je, endLabel);

            if (_compiler.info.compInitMem)
            {
                assert(InternalRegisters.Count(tree) == 0);
                regCnt = targetReg;
            }
            else
            {
                regCnt = InternalRegisters.GetSingle(tree);
                inst_Mov(size.Type, regCnt, targetReg, canSkip: true);
            }

            inst_RV_IV(INS_add, regCnt, STACK_ALIGN - 1, type.EmitActualSize);
            if (_compiler.info.compInitMem)
            {
                // Shift away alignment bits to count 16-byte zeroing iterations.
                inst_RV_SH(INS_shr, EA_PTRSIZE, regCnt, STACK_ALIGN_SHIFT);
            }
            else
            {
                inst_RV_IV(INS_and, regCnt, ~(STACK_ALIGN - 1), type.EmitActualSize);
            }
        }

        var initMemOrLargeAlloc = _compiler.info.compInitMem || (amount >= _compiler.eeGetPageSize());
        if (_compiler.lvaOutgoingArgSpaceSize.Value > 0)
        {
            assert((_compiler.lvaOutgoingArgSpaceSize.Value % STACK_ALIGN) == 0);
            if ((amount > 0) && !initMemOrLargeAlloc)
            {
                lastTouchDelta = genStackPointerConstantAdjustmentLoopWithProbe(unchecked(-(nint)amount), true);
                stackAdjustment = 0;
                locAllocStackOffset = unchecked((nuint)_compiler.lvaOutgoingArgSpaceSize.Value);
                goto ALLOC_DONE;
            }

            if (size.Oper.IsCnsIntOrI && size.IsContained)
            {
                stackAdjustment = 0;
                locAllocStackOffset = unchecked((nuint)_compiler.lvaOutgoingArgSpaceSize.Value);
            }
            else
            {
                inst_RV_IV(INS_add, REG_SPBASE, _compiler.lvaOutgoingArgSpaceSize.Value, EA_PTRSIZE);
                stackAdjustment = unchecked(stackAdjustment + (nuint)_compiler.lvaOutgoingArgSpaceSize.Value);
                locAllocStackOffset = stackAdjustment;
            }
        }

        if (size.Oper.IsCnsIntOrI && size.IsContained)
        {
            assert((amount > 0) && ((amount % STACK_ALIGN) == 0));
            // Constant allocations are initialized by a separate lowered BLK.
            if (amount >= _compiler.eeGetPageSize())
            {
                regCnt = InternalRegisters.GetSingle(tree);
                instGen_Set_Reg_To_Imm(EA_PTRSIZE, regCnt, unchecked(-(nint)amount));
                genStackPointerDynamicAdjustmentWithProbe(regCnt);
            }
            else
            {
                lastTouchDelta = genStackPointerConstantAdjustmentLoopWithProbe(unchecked(-(nint)amount), true);
            }
            goto ALLOC_DONE;
        }

        assert(InternalRegisters.Count(tree) == 0);
        if (_compiler.info.compInitMem)
        {
            assert(genIsValidIntReg(regCnt));
            var loop = genCreateTempLabel();
            genDefineTempLabel(loop);
            assert((STACK_ALIGN % REGSIZE_BYTES) == 0);
            for (var index = 0; index < (STACK_ALIGN / REGSIZE_BYTES); index++)
            {
                inst_IV(INS_push_hide, 0);
            }
            inst_RV(INS_dec, regCnt, TYP_I_IMPL);
            inst_JMP(EJ_jne, loop);
            lastTouchDelta = 0;
        }
        else
        {
            inst_RV(INS_neg, regCnt, TYP_I_IMPL);
            genStackPointerDynamicAdjustmentWithProbe(regCnt);
        }

    ALLOC_DONE:
        if (stackAdjustment > 0)
        {
            assert((stackAdjustment % STACK_ALIGN) == 0);
            assert(lastTouchDelta >= -1);
            if ((lastTouchDelta == -1) ||
                (unchecked(stackAdjustment + (nuint)lastTouchDelta + STACK_PROBE_BOUNDARY_THRESHOLD_BYTES) >
                    _compiler.eeGetPageSize()))
            {
                _ = genStackPointerConstantAdjustmentLoopWithProbe(unchecked(-(nint)stackAdjustment), true);
            }
            else
            {
                genStackPointerConstantAdjustment(unchecked(-(nint)stackAdjustment), true);
            }
        }

        Emitter.emitIns_R_AR(INS_lea, EA_PTRSIZE, targetReg, REG_SPBASE, unchecked((int)locAllocStackOffset));
        if (endLabel is not null)
        {
            genDefineTempLabel(endLabel);
        }

#if DEBUG
        if (_compiler.opts.compStackCheckOnRet)
        {
            assert(_compiler.lvaReturnSpCheck != BAD_VAR_NUM);
            assert(_compiler.lvaGetDesc(_compiler.lvaReturnSpCheck).lvDoNotEnregister);
            assert(_compiler.lvaGetDesc(_compiler.lvaReturnSpCheck).lvOnFrame);
            Emitter.emitIns_S_R(ins_Store(TYP_I_IMPL), EA_PTRSIZE, REG_SPBASE, _compiler.lvaReturnSpCheck, 0);
        }
#endif
        genProduceReg(tree);
#endif
    }
}
