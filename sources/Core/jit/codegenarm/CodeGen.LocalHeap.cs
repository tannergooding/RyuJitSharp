// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private void genLclHeapArm32(GenTree tree)
    {
        assert(tree.Oper == GT_LCLHEAP);
        assert(_compiler.compLocallocUsed);

        var size = tree.AsUnOp().Op1;
        noway_assert(size.Type.ActualType is TYP_INT or TYP_I_IMPL);

        var regCnt = tree.RegNum;
        var type = size.Type.ActualType;
        var attr = type.EmitSize;
        BasicBlock? endLabel = null;
        uint stackAdjustment = 0;
        var regTmp = REG_NA;
        const target_ssize_t IllegalLastTouchDelta = -1;
        target_ssize_t lastTouchDelta = IllegalLastTouchDelta;

        noway_assert(IsFramePointerUsed);
        noway_assert(genStackLevel == 0);

        if (size.Oper.IsCnsIntOrI)
        {
            assert(size.IsContained);
            var amount = unchecked((uint)size.AsIntCon().IconValue);
            if (amount == 0)
            {
                instGen_Set_Reg_To_Zero(EA_PTRSIZE, regCnt);
                goto BAILOUT;
            }
        }
        else
        {
            genConsumeRegAndCopy(size, regCnt);
            endLabel = genCreateTempLabel();
            Emitter.emitIns_R_R(INS_tst, attr, regCnt, regCnt);
            inst_JMP(EJ_eq, endLabel);
        }

        if (InternalRegisters.Count(tree) > 0)
        {
            regTmp = InternalRegisters.Extract(tree);
        }

        if (_compiler.lvaOutgoingArgSpaceSize.Value > 0)
        {
            assert((_compiler.lvaOutgoingArgSpaceSize.Value % STACK_ALIGN) == 0);
            // Restore outgoing argument space after the dynamic allocation and probe it if needed.
            _ = genStackPointerAdjustment(_compiler.lvaOutgoingArgSpaceSize.Value, regTmp);
            stackAdjustment = unchecked(stackAdjustment + (uint)_compiler.lvaOutgoingArgSpaceSize.Value);
        }

        if (size.Oper.IsCnsIntOrI)
        {
            var amount = unchecked((uint)size.AsIntCon().IconValue);
            amount = unchecked((amount + (uint)STACK_ALIGN - 1) & ~((uint)STACK_ALIGN - 1));
            assert(STACK_ALIGN == (REGSIZE_BYTES * 2));
            assert((amount % (uint)REGSIZE_BYTES) == 0);
            var pushCount = amount / (uint)REGSIZE_BYTES;

            if (pushCount <= 4)
            {
                // Constant allocations are initialized by the separately lowered block.
                instGen_Set_Reg_To_Zero(EA_PTRSIZE, regCnt);
                while (pushCount != 0)
                {
                    inst_IV(INS_push, unchecked((nint)genRegMask(regCnt).Lower));
                    pushCount -= 1;
                }

                lastTouchDelta = 0;
                goto ALLOC_DONE;
            }
            else if (!_compiler.info.compInitMem && (amount < _compiler.eeGetPageSize()))
            {
                Emitter.emitIns_R_R_I(INS_ldr, EA_4BYTE, regCnt, REG_SP, 0, INS_FLAGS_DONT_CARE);
                inst_RV_IV(INS_sub, REG_SP, unchecked((nint)amount), EA_PTRSIZE);
                lastTouchDelta = unchecked((target_ssize_t)amount);
                goto ALLOC_DONE;
            }

            instGen_Set_Reg_To_Imm(EA_4BYTE, regCnt, unchecked((nint)amount));
        }
        else
        {
            inst_RV_IV(INS_add, regCnt, STACK_ALIGN - 1, type.EmitActualSize);
            inst_RV_IV(INS_and, regCnt, ~(STACK_ALIGN - 1), type.EmitActualSize);
        }

        if (_compiler.info.compInitMem)
        {
            instGen_Set_Reg_To_Zero(EA_PTRSIZE, regTmp);
            var loop = genCreateTempLabel();
            genDefineTempLabel(loop);
            noway_assert(STACK_ALIGN == 8);
            inst_IV(INS_push, unchecked((nint)genRegMask(regTmp).Lower));
            inst_IV(INS_push, unchecked((nint)genRegMask(regTmp).Lower));
            assert(genIsValidIntReg(regCnt));
            Emitter.emitIns_R_I(INS_sub, EA_PTRSIZE, regCnt, STACK_ALIGN, INS_FLAGS_SET);
            inst_JMP(EJ_ne, loop);
            lastTouchDelta = 0;
        }
        else
        {
            // Touch SP before moving it because it may already be in the guard page; clamp overflow and step only
            // after each page is touched.
            var loop = genCreateTempLabel();
            var done = genCreateTempLabel();
            Emitter.emitIns_R_R_R(INS_sub, EA_PTRSIZE, regCnt, REG_SPBASE, regCnt, INS_FLAGS_SET);
            inst_JMP(EJ_vc, loop);
            instGen_Set_Reg_To_Zero(EA_PTRSIZE, regCnt);
            genDefineTempLabel(loop);
            Emitter.emitIns_R_R_I(INS_ldr, EA_4BYTE, regTmp, REG_SPBASE, 0, INS_FLAGS_DONT_CARE);
            Emitter.emitIns_R_R_I(INS_sub, EA_PTRSIZE, regTmp, REG_SPBASE,
                unchecked((int)_compiler.eeGetPageSize()), INS_FLAGS_DONT_CARE);
            Emitter.emitIns_R_R(INS_cmp, EA_PTRSIZE, regTmp, regCnt);
            inst_JMP(EJ_lo, done);
            _ = Emitter.emitIns_Mov(INS_mov, EA_PTRSIZE, REG_SPBASE, regTmp, canSkip: false);
            inst_JMP(EJ_jmp, loop);
            genDefineTempLabel(done);
            _ = Emitter.emitIns_Mov(INS_mov, EA_PTRSIZE, REG_SPBASE, regCnt, canSkip: false);
        }

    ALLOC_DONE:
        if (stackAdjustment != 0)
        {
            assert((stackAdjustment % STACK_ALIGN) == 0);
            assert((lastTouchDelta == IllegalLastTouchDelta) || (lastTouchDelta >= 0));

            var needsProbe = lastTouchDelta == IllegalLastTouchDelta;
            if (!needsProbe)
            {
                // The native unsigned additions wrap at target width before the host-sized page comparison.
                var probeDistance = unchecked(stackAdjustment + unchecked((uint)lastTouchDelta) +
                    (uint)STACK_PROBE_BOUNDARY_THRESHOLD_BYTES);
                needsProbe = (nuint)probeDistance > _compiler.eeGetPageSize();
            }

            if (needsProbe)
            {
                _ = genStackPointerConstantAdjustmentLoopWithProbe(unchecked(-(nint)stackAdjustment), regTmp);
            }
            else
            {
                genStackPointerConstantAdjustment(unchecked(-(nint)stackAdjustment), regTmp);
            }

            _ = genInstrWithConstant(INS_add, EA_PTRSIZE, regCnt, REG_SPBASE,
                unchecked((nint)stackAdjustment), regTmp);
        }
        else
        {
            _ = Emitter.emitIns_Mov(INS_mov, EA_PTRSIZE, regCnt, REG_SPBASE, canSkip: false);
        }

    BAILOUT:
        if (endLabel is not null)
        {
            genDefineTempLabel(endLabel);
        }

        genProduceReg(tree);
    }
}
#endif
