// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    // A following frame setup can predecrement SP by up to 504 bytes before touching it.
    // Match targetarm64.h's conservative, aligned probe boundary.
    private const uint Arm64StackProbeBoundaryThresholdBytes = 512;

    private void genStackPointerConstantAdjustmentArm64(nint spDelta, regNumber regTmp)
    {
        assert(spDelta < 0);
        assert(unchecked((nuint)(-spDelta)) <= _compiler.eeGetPageSize());

        _ = genInstrWithConstantArm64(INS_sub, EA_PTRSIZE, REG_SPBASE_ARM64, REG_SPBASE_ARM64,
            unchecked(-spDelta), regTmp);
    }

    private void genStackPointerConstantAdjustmentWithProbeArm64(nint spDelta, regNumber regTmp)
    {
        Emitter.emitIns_R_R_IArm64(INS_ldr, EA_4BYTE, regTmp, REG_ARM64_SP, 0);
        genStackPointerConstantAdjustmentArm64(spDelta, regTmp);
    }

    private nint genStackPointerConstantAdjustmentLoopWithProbeArm64(nint spDelta, regNumber regTmp)
    {
        unchecked
        {
            assert(spDelta < 0);
            var pageSize = _compiler.eeGetPageSize();
            var remaining = spDelta;

            do
            {
                var oneDelta = -(nint)nuint.Min((nuint)(-remaining), pageSize);
                genStackPointerConstantAdjustmentWithProbeArm64(oneDelta, regTmp);
                remaining -= oneDelta;
            }
            while (remaining < 0);

            // Probes precede subtraction, so an exact page or near-boundary remainder
            // needs a final touch before a subsequent predecrement.
            var lastTouchDelta = (nuint)(-spDelta) % pageSize;
            if ((lastTouchDelta == 0) ||
                (lastTouchDelta + Arm64StackProbeBoundaryThresholdBytes > pageSize))
            {
                Emitter.emitIns_R_R_IArm64(INS_ldr, EA_4BYTE, regTmp, REG_ARM64_SP, 0);
                lastTouchDelta = 0;
            }

            return (nint)lastTouchDelta;
        }
    }

    private void genLclHeapArm64(GenTree tree)
    {
        unchecked
        {
            assert(tree.Oper == GT_LCLHEAP);
            assert(_compiler.compLocallocUsed);

            var size = tree.AsUnOp().Op1;
            noway_assert((size.Type.ActualType is var __targetAliasValue0 && (__targetAliasValue0 is TYP_INT || __targetAliasValue0 == TYP_I_IMPL)));
            var targetReg = tree.RegNum;
            var regCnt = REG_ARM64_NA;
            var type = size.Type.ActualType;
            var attr = type.EmitSize;
            BasicBlock? endLabel = null;
            uint stackAdjustment = 0;
            const nint IllegalLastTouchDelta = -1;
            var lastTouchDelta = IllegalLastTouchDelta;

            noway_assert(IsFramePointerUsed);
            noway_assert(genStackLevel == 0);

            var needsZeroing = _compiler.info.compInitMem;
            nuint amount = 0;
            if (size.Oper.IsCnsIntOrI && size.IsContained)
            {
                needsZeroing = false;
                amount = (nuint)size.AsIntCon().IconValue;
                if (amount == 0)
                {
                    instGen_Set_Reg_To_Zero(EA_PTRSIZE, targetReg);
                    goto BAILOUT;
                }

                var stackAlign = unchecked((nuint)Configuration.STACK_ALIGN);
                amount = (amount + stackAlign - 1) & ~(stackAlign - 1);
            }
            else
            {
                genConsumeRegAndCopy(size, targetReg);
                endLabel = genCreateTempLabel();
                Emitter.emitIns_R_R(INS_tst, attr, targetReg, targetReg);
                inst_JMP(EJ_eq, endLabel);

                if (needsZeroing)
                {
                    assert(InternalRegisters.Count(tree) == 0);
                    regCnt = targetReg;
                }
                else
                {
                    regCnt = InternalRegisters.Extract(tree);
                    inst_Mov(size.Type, regCnt, targetReg, canSkip: true);
                }

                inst_RV_IV(INS_add, regCnt, Configuration.STACK_ALIGN - 1, type.EmitActualSize);
                inst_RV_IV(INS_and, regCnt, ~(Configuration.STACK_ALIGN - 1), type.EmitActualSize);
            }

            if (_compiler.lvaOutgoingArgSpaceSize.Value > 0)
            {
                assert((_compiler.lvaOutgoingArgSpaceSize.Value % Configuration.STACK_ALIGN) == 0);
                _ = genInstrWithConstantArm64(INS_add, EA_PTRSIZE, REG_SPBASE_ARM64, REG_SPBASE_ARM64,
                    (nint)_compiler.lvaOutgoingArgSpaceSize.Value, rsGetRsvdReg());
                stackAdjustment += (uint)_compiler.lvaOutgoingArgSpaceSize.Value;
            }

            if (size.Oper.IsCnsIntOrI && size.IsContained)
            {
                assert(amount > 0);
                var storePairBytes = unchecked((nuint)(2 * Configuration.REGSIZE_BYTES));
                assert(amount % storePairBytes == 0);

                if (amount < _compiler.eeGetPageSize())
                {
                    if (amount <= 256)
                    {
                        Emitter.emitIns_R_R_IArm64(INS_ldr, EA_4BYTE, REG_ARM64_ZR, REG_SPBASE_ARM64,
                            -(nint)amount, INS_OPTS_POST_INDEX);
                    }
                    else if (canEncodeLoadOrStorePairOffsetArm64(-(long)amount, EA_8BYTE))
                    {
                        // LDP with two identical destination registers is unpredictable.
                        Emitter.emitIns_R_R_R_IArm64(INS_ldp, EA_8BYTE, targetReg, REG_ARM64_ZR, REG_SPBASE_ARM64,
                            -(nint)amount, INS_OPTS_POST_INDEX);
                    }
                    else
                    {
                        Emitter.emitIns_R_R_IArm64(INS_ldr, EA_4BYTE, REG_ARM64_ZR, REG_SPBASE_ARM64, 0);
                        _ = genInstrWithConstantArm64(INS_sub, EA_PTRSIZE, REG_SPBASE_ARM64, REG_SPBASE_ARM64,
                            (nint)amount, rsGetRsvdReg());
                    }

                    lastTouchDelta = (nint)amount;
                    goto ALLOC_DONE;
                }

                assert(regCnt == REG_ARM64_NA);
                if (needsZeroing)
                {
                    assert(InternalRegisters.Count(tree) == 0);
                    regCnt = targetReg;
                }
                else
                {
                    regCnt = InternalRegisters.Extract(tree);
                }

                instGen_Set_Reg_To_ImmArm64((uint)amount == amount ? EA_4BYTE : EA_8BYTE, regCnt, (nint)amount);
            }

            if (needsZeroing)
            {
                var loop = genCreateTempLabel();
                genDefineTempLabel(loop);
                Emitter.emitIns_R_R_R_IArm64(INS_stp, EA_PTRSIZE, REG_ARM64_ZR, REG_ARM64_ZR, REG_SPBASE_ARM64,
                    -16, INS_OPTS_PRE_INDEX);
                assert(genIsValidIntReg(regCnt));
                inst_RV_IV(INS_subs, regCnt, 16, type.EmitActualSize);
                inst_JMP(EJ_ne, loop);
                lastTouchDelta = 0;
            }
            else
            {
                var regTmp = InternalRegisters.GetSingle(tree);
                var loop = genCreateTempLabel();
                var done = genCreateTempLabel();

                Emitter.emitIns_R_R_R(INS_subs, EA_PTRSIZE, regCnt, REG_SPBASE_ARM64, regCnt);
                inst_JMP(EJ_hs, loop);
                instGen_Set_Reg_To_Zero(EA_PTRSIZE, regCnt);
                genDefineTempLabel(loop);

                // SP remains at a probed address until the next full page is reachable.
                Emitter.emitIns_R_R_IArm64(INS_ldr, EA_4BYTE, REG_ARM64_ZR, REG_SPBASE_ARM64, 0);
                Emitter.emitIns_R_R_IArm64(INS_sub, EA_PTRSIZE, regTmp, REG_SPBASE_ARM64,
                    (nint)_compiler.eeGetPageSize());
                Emitter.emitIns_R_R(INS_cmp, EA_PTRSIZE, regTmp, regCnt);
                inst_JMP(EJ_lo, done);
                _ = Emitter.emitIns_Mov(INS_mov, EA_PTRSIZE, REG_SPBASE_ARM64, regTmp, canSkip: false);
                inst_JMP(EJ_jmp, loop);

                genDefineTempLabel(done);
                _ = Emitter.emitIns_Mov(INS_mov, EA_PTRSIZE, REG_SPBASE_ARM64, regCnt, canSkip: false);
            }

        ALLOC_DONE:
            if (stackAdjustment != 0)
            {
                assert((stackAdjustment % Configuration.STACK_ALIGN) == 0);
                assert((lastTouchDelta == IllegalLastTouchDelta) || (lastTouchDelta >= 0));
                var tmpReg = rsGetRsvdReg();

                if ((lastTouchDelta == IllegalLastTouchDelta) ||
                    (stackAdjustment + (uint)lastTouchDelta + Arm64StackProbeBoundaryThresholdBytes >
                        _compiler.eeGetPageSize()))
                {
                    _ = genStackPointerConstantAdjustmentLoopWithProbeArm64(-(nint)stackAdjustment, tmpReg);
                }
                else
                {
                    genStackPointerConstantAdjustmentArm64(-(nint)stackAdjustment, tmpReg);
                }

                _ = genInstrWithConstantArm64(INS_add, EA_PTRSIZE, targetReg, REG_SPBASE_ARM64,
                    (nint)stackAdjustment, tmpReg);
            }
            else
            {
                inst_Mov(TYP_I_IMPL, targetReg, REG_SPBASE_ARM64, canSkip: false);
            }

        BAILOUT:
            if (endLabel is not null)
            {
                genDefineTempLabel(endLabel);
            }

            genProduceReg(tree);
        }
    }

    private static bool canEncodeLoadOrStorePairOffsetArm64(long imm, emitAttr attr)
    {
        assert(attr is EA_4BYTE or EA_8BYTE or EA_16BYTE,
            "(attr == EA_4BYTE) || (attr == EA_8BYTE) || (attr == EA_16BYTE)");
        var size = (int)EA_SIZE_IN_BYTES(attr);

        return (imm % size == 0) && (imm >= -64 * size) && (imm < 64 * size);
    }
}
