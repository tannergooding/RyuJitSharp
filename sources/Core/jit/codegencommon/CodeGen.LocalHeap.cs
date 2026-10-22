// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Numerics;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void inst_IV(instruction ins, nint value)
    {
        Emitter.emitIns_I(ins, EA_PTRSIZE, value);
    }

    public void genStackPointerConstantAdjustment(nint spDelta, bool trackSpAdjustments)
    {
        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.StackPointerAdjustmentWithProbingRequiresXarch
    );
            return;
        }

        Emitter.RequireSupportedInstructionRecording();
        assert(spDelta < 0);
        assert(unchecked((nuint)(-spDelta)) <= _compiler.eeGetPageSize());
        if (Configuration.TARGET_AMD64)
        {
            trackSpAdjustments = true;
        }
        if (trackSpAdjustments)
        {
            inst_RV_IV(INS_sub, TargetRuntimeMetadata.StackPointerBaseRegister, unchecked(-spDelta), EA_PTRSIZE);
        }
        else
        {
            inst_RV_IV(INS_sub_hide, TargetRuntimeMetadata.StackPointerBaseRegister, unchecked(-spDelta), EA_PTRSIZE);
        }
    }

    public void genStackPointerConstantAdjustmentWithProbe(nint spDelta, bool trackSpAdjustments)
    {
        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.StackPointerAdjustmentWithProbingRequiresXarch
    );
            return;
        }

        Emitter.RequireSupportedInstructionRecording();
        Emitter.emitIns_AR_R(INS_test, EA_4BYTE, TargetRuntimeMetadata.StackPointerBaseRegister, TargetRuntimeMetadata.StackPointerBaseRegister, 0);
        genStackPointerConstantAdjustment(spDelta, trackSpAdjustments);
    }

    public nint genStackPointerConstantAdjustmentLoopWithProbe(nint spDelta, bool trackSpAdjustments)
    {
        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.StackPointerAdjustmentWithProbingRequiresXarch
    );
            return default!;
        }

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
            (unchecked(lastTouchDelta + (nuint)STACK_PROBE_BOUNDARY_THRESHOLD_BYTES) > pageSize))
        {
            Emitter.emitIns_AR_R(INS_test, EA_PTRSIZE, TargetRuntimeMetadata.IntegerReturnRegister,
                TargetRuntimeMetadata.StackPointerBaseRegister, 0);
            lastTouchDelta = 0;
        }

        return unchecked((nint)lastTouchDelta);
    }

    public void genStackPointerDynamicAdjustmentWithProbe(regNumber regSpDelta)
    {
        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.StackPointerAdjustmentWithProbingRequiresXarch
    );
            return;
        }

        Emitter.RequireSupportedInstructionRecording();
        assert(regSpDelta != REG_NA);
        var loop = genCreateTempLabel();
        inst_RV_RV(INS_add, regSpDelta, TargetRuntimeMetadata.StackPointerBaseRegister, TYP_I_IMPL);
        inst_JMP(EJ_jb, loop);
        instGen_Set_Reg_To_Zero(EA_PTRSIZE, regSpDelta);
        genDefineTempLabel(loop);

        // Touch the current SP before moving it: it may already be on the
        // guard page. The carry test above clamps a wrapped target to zero.
        Emitter.emitIns_AR_R(INS_test, EA_4BYTE, TargetRuntimeMetadata.StackPointerBaseRegister, TargetRuntimeMetadata.StackPointerBaseRegister, 0);
        inst_RV_IV(INS_sub_hide, TargetRuntimeMetadata.StackPointerBaseRegister, unchecked((nint)_compiler.eeGetPageSize()), EA_PTRSIZE);
        inst_RV_RV(INS_cmp, TargetRuntimeMetadata.StackPointerBaseRegister, regSpDelta, TYP_I_IMPL);
        inst_JMP(EJ_jae, loop);
        inst_Mov(TYP_I_IMPL, TargetRuntimeMetadata.StackPointerBaseRegister, regSpDelta, canSkip: false);
    }

    public void genLclHeap(GenTree tree)
    {
        if (Configuration.TARGET_WASM)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.LocalHeapGenerationUsesASeparateWasmPath);
            return;
        }

        if (Configuration.TARGET_ARM)
        {
            genLclHeapArm(tree);
            return;
        }

        if (Configuration.TARGET_ARM64)
        {
            genLclHeapArm64(tree);
            return;
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
            genLclHeapLoongArch64(tree);
            return;
        }

        if (Configuration.TARGET_RISCV64)
        {
            genLclHeapRiscV64(tree);
            return;
        }

        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.LocalHeapGenerationIsNotPortedForThisTarget);
            return;
        }

        Emitter.RequireSupportedInstructionRecording();
        assert(tree.Oper == GT_LCLHEAP);
        assert(_compiler.compLocallocUsed);
        var size = tree.AsUnOp().Op1;
        noway_assert((size.Type.ActualType is var __targetAliasValue0 && (__targetAliasValue0 is TYP_INT || __targetAliasValue0 == TYP_I_IMPL)));
        var targetReg = tree.RegNum;
        var regCnt = REG_NA;
        var type = size.Type.ActualType;
        BasicBlock? endLabel = null;
        nint lastTouchDelta = -1;

        if (Configuration.DEBUG)
        {
            genStackPointerCheck(_compiler.compStackCheckOnRet, _compiler.lvaReturnSpCheck);
        }
        noway_assert(IsFramePointerUsed);
        noway_assert(genStackLevel == 0);
        nuint stackAdjustment = 0;
        nuint locAllocStackOffset = 0;
        nuint amount = 0;

        if (size.Oper.IsCnsIntOrI && size.IsContained)
        {
            amount = unchecked((nuint)size.AsIntCon().IconValue);
            assert((amount > 0) && (amount <= uint.MaxValue));
            amount = unchecked((amount + (nuint)Configuration.STACK_ALIGN - 1) & ~(nuint)(Configuration.STACK_ALIGN - 1));
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

            inst_RV_IV(INS_add, regCnt, Configuration.STACK_ALIGN - 1, type.EmitActualSize);
            if (_compiler.info.compInitMem)
            {
                // Shift away alignment bits to count 16-byte zeroing iterations.
                inst_RV_SH(INS_shr, EA_PTRSIZE, regCnt, unchecked((uint)BitOperations.Log2(unchecked((uint)Configuration.STACK_ALIGN))));
            }
            else
            {
                inst_RV_IV(INS_and, regCnt, ~(Configuration.STACK_ALIGN - 1), type.EmitActualSize);
            }
        }

        var initMemOrLargeAlloc = _compiler.info.compInitMem || (amount >= _compiler.eeGetPageSize());
        if (Configuration.FEATURE_FIXED_OUT_ARGS && (_compiler.lvaOutgoingArgSpaceSize.Value > 0))
        {
            assert((_compiler.lvaOutgoingArgSpaceSize.Value % Configuration.STACK_ALIGN) == 0);
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
                inst_RV_IV(INS_add, TargetRuntimeMetadata.StackPointerBaseRegister, _compiler.lvaOutgoingArgSpaceSize.Value, EA_PTRSIZE);
                stackAdjustment = unchecked(stackAdjustment + (nuint)_compiler.lvaOutgoingArgSpaceSize.Value);
                locAllocStackOffset = stackAdjustment;
            }
        }

        if (size.Oper.IsCnsIntOrI && size.IsContained)
        {
            assert((amount > 0) && ((amount % (nuint)Configuration.STACK_ALIGN) == 0));
            // Constant allocations are initialized by a separate lowered BLK.
            if ((amount >= _compiler.eeGetPageSize()) || !Configuration.TARGET_64BIT)
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
            assert((Configuration.STACK_ALIGN % Configuration.REGSIZE_BYTES) == 0);
            for (var index = 0; index < (Configuration.STACK_ALIGN / Configuration.REGSIZE_BYTES); index++)
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
            assert((stackAdjustment % (nuint)Configuration.STACK_ALIGN) == 0);
            assert(lastTouchDelta >= -1);
            if ((lastTouchDelta == -1) ||
                (unchecked(stackAdjustment + (nuint)lastTouchDelta + (nuint)STACK_PROBE_BOUNDARY_THRESHOLD_BYTES) >
                    _compiler.eeGetPageSize()))
            {
                _ = genStackPointerConstantAdjustmentLoopWithProbe(unchecked(-(nint)stackAdjustment), true);
            }
            else
            {
                genStackPointerConstantAdjustment(unchecked(-(nint)stackAdjustment), true);
            }
        }

        Emitter.emitIns_R_AR(INS_lea, EA_PTRSIZE, targetReg, TargetRuntimeMetadata.StackPointerBaseRegister, unchecked((int)locAllocStackOffset));
        if (endLabel is not null)
        {
            genDefineTempLabel(endLabel);
        }

        if (Configuration.JIT32_GCENCODER && (_compiler.lvaLocAllocSPvar != BAD_VAR_NUM))
        {
            Emitter.emitIns_S_R(ins_Store(TYP_I_IMPL), EA_PTRSIZE, TargetRuntimeMetadata.StackPointerBaseRegister, _compiler.lvaLocAllocSPvar, 0);
        }

        if (Configuration.DEBUG && _compiler.compStackCheckOnRet)
        {
            assert(_compiler.lvaReturnSpCheck != BAD_VAR_NUM);
            assert(_compiler.lvaGetDesc(_compiler.lvaReturnSpCheck).lvDoNotEnregister);
            assert(_compiler.lvaGetDesc(_compiler.lvaReturnSpCheck).lvOnFrame);
            Emitter.emitIns_S_R(ins_Store(TYP_I_IMPL), EA_PTRSIZE, TargetRuntimeMetadata.StackPointerBaseRegister, _compiler.lvaReturnSpCheck, 0);
        }
        genProduceReg(tree);
    }
}
