// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using System.Numerics;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private void genEstablishFramePointerArm64(int delta, bool reportUnwindData)
    {
        assert(Emitter.emitGeneratingPrologOrFuncletProlog());
        if (delta == 0)
        {
            Emitter.emitIns_Mov(INS_mov, EA_PTRSIZE, REG_FPBASE, REG_SPBASE, canSkip: false);
        }
        else
        {
            Emitter.emitIns_R_R_I(INS_add, EA_PTRSIZE, REG_FPBASE, REG_SPBASE, delta);
        }

        if (reportUnwindData)
        {
            _compiler.unwindSetFrameReg(REG_FPBASE, unchecked((uint)delta));
        }
    }

    private void genAllocLclFrameArm64(uint frameSize, regNumber initReg, ref bool initRegZeroed,
        regMaskTP maskArgRegsLiveIn)
    {
        unchecked
        {
            assert(Emitter.emitGeneratingPrologOrFuncletProlog());
            if (frameSize == 0)
            {
                return;
            }

            var pageSize = _compiler.eeGetPageSize();
            nuint lastTouchDelta;
            assert(!_compiler.compHasSecretStubArgument() || (REG_SECRET_STUB_PARAM != initReg));

            if (frameSize < pageSize)
            {
                lastTouchDelta = frameSize;
            }
            else if (frameSize < 3 * pageSize)
            {
                lastTouchDelta = frameSize;
                for (var probeOffset = pageSize; probeOffset <= frameSize; probeOffset += pageSize)
                {
                    instGen_Set_Reg_To_Imm(EA_PTRSIZE, initReg, -(nint)probeOffset);
                    Emitter.emitIns_R_R_R(INS_ldr, EA_4BYTE, REG_ZR, REG_SPBASE, initReg);
                    _regSet.verifyRegUsed(initReg);
                    initRegZeroed = false;
                    lastTouchDelta -= pageSize;
                }

                assert(lastTouchDelta == frameSize % pageSize);
                _compiler.unwindPadding();
            }
            else
            {
                // Probe without changing SP so a fault can still unwind the frame.
                var available = new regMaskTP(SRBM_ALLINT) &
                    (_regSet.rsGetModifiedRegsMask() | ~new regMaskTP(SRBM_INT_CALLEE_SAVED));
                available &= ~maskArgRegsLiveIn;
                available &= ~regMaskTP.CreateFromRegNum(initReg, initReg.SingleTypeMask);
                var offsetReg = initReg;

                noway_assert(available != RBM_NONE, "availMask != RBM_NONE");
                var lowestBit = genFindLowestBitArm64(available);
                var limitReg = genRegNumFromMaskArm64(lowestBit);
                noway_assert((nint)(int)frameSize == (nint)frameSize,
                    "(ssize_t)(int)frameSize == (ssize_t)frameSize");

                instGen_Set_Reg_To_Imm(EA_PTRSIZE, offsetReg, -(nint)pageSize);
                instGen_Set_Reg_To_Imm(EA_PTRSIZE, limitReg, -(nint)frameSize);
                var loop = genCreateTempLabel();
                genDefineInlineTempLabel(loop);
                Emitter.emitIns_R_R_R(INS_ldr, EA_4BYTE, REG_ZR, REG_SPBASE, offsetReg);
                Emitter.emitIns_R_R_I(INS_sub, EA_PTRSIZE, offsetReg, offsetReg, (nint)pageSize);
                Emitter.emitIns_R_R(INS_cmp, EA_PTRSIZE, limitReg, offsetReg);
                Emitter.emitIns_ShortJ(INS_bls, loop);
                initRegZeroed = false;
                _compiler.unwindPadding();
                lastTouchDelta = frameSize % pageSize;
            }

            if (lastTouchDelta + Arm64StackProbeBoundaryThresholdBytes > pageSize)
            {
                assert(lastTouchDelta + Arm64StackProbeBoundaryThresholdBytes < 2 * pageSize);
                instGen_Set_Reg_To_Imm(EA_PTRSIZE, initReg, -(nint)frameSize);
                Emitter.emitIns_R_R_R(INS_ldr, EA_4BYTE, REG_ZR, REG_SPBASE, initReg);
                _compiler.unwindPadding();
                _regSet.verifyRegUsed(initReg);
                initRegZeroed = false;
            }
        }
    }

    private static regMaskTP genFindLowestBitArm64(regMaskTP value)
    {
        unchecked
        {
#if HAS_MORE_THAN_64_REGISTERS
            if (value.Lower != SRBM_NONE)
            {
                var lower = (ulong)value.Lower;

                return new regMaskTP((regMask)(lower & (0UL - lower)));
            }

            var upper = (ulong)value.Upper;

            return new regMaskTP(SRBM_NONE, (regMask)(upper & (0UL - upper)));
#else
            var lower = (ulong)value.Lower;

            return new regMaskTP((regMask)(lower & (0UL - lower)));
#endif
        }
    }

    private static regNumber genRegNumFromMaskArm64(regMaskTP mask)
    {
#if HAS_MORE_THAN_64_REGISTERS
        assert(mask.Upper == SRBM_NONE, "mask.getHigh() == RBM_NONE");
#endif
        assert(mask.IsNonEmpty, "mask.IsNonEmpty()");
        var value = unchecked((ulong)mask.Lower);
        assert(BitOperations.IsPow2(value), "genExactlyOneBit(value)");
        assert(value != 0, "value != 0");
        var reg = (regNumber)BitOperations.TrailingZeroCount(value);
        assert(regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask).Lower == mask.Lower,
            "genRegMask(regNum).getLow() == mask.getLow()");

        return reg;
    }

    private bool genInstrWithConstant(instruction ins, emitAttr attr, regNumber reg1,
        regNumber reg2, nint imm, regNumber tmpReg, bool inUnwindRegion = false)
    {
        unchecked
        {
            var immediateFits = false;
            var size = EA_SIZE(attr);
            assert(tmpReg != reg2);

            switch (ins)
            {
                case INS_add:
                case INS_sub:
                {
                    if (imm < 0)
                    {
                        imm = -imm;
                        ins = ins == INS_add ? INS_sub : INS_add;
                    }

                    immediateFits = Emitter.emitIns_valid_imm_for_add(imm, size);
                    break;
                }

                case INS_strb:
                {
                    assert(size == EA_1BYTE);
                    immediateFits = Emitter.emitIns_valid_imm_for_ldst_offset(imm, EA_1BYTE);
                    break;
                }

                case INS_strh:
                {
                    assert(size == EA_2BYTE);
                    immediateFits = Emitter.emitIns_valid_imm_for_ldst_offset(imm, EA_2BYTE);
                    break;
                }

                case INS_str:
                {
                    assert(tmpReg != reg1);
                    immediateFits = Emitter.emitIns_valid_imm_for_ldst_offset(imm, size);
                    break;
                }

                case INS_ldrb:
                case INS_ldrsb:
                {
                    immediateFits = Emitter.emitIns_valid_imm_for_ldst_offset(imm, EA_1BYTE);
                    break;
                }

                case INS_ldrh:
                case INS_ldrsh:
                {
                    immediateFits = Emitter.emitIns_valid_imm_for_ldst_offset(imm, EA_2BYTE);
                    break;
                }

                case INS_ldrsw:
                {
                    assert(size == EA_4BYTE);
                    immediateFits = Emitter.emitIns_valid_imm_for_ldst_offset(imm, EA_4BYTE);
                    break;
                }

                case INS_ldr:
                {
                    assert(size is EA_4BYTE or EA_8BYTE or EA_16BYTE,
                        "(size == EA_4BYTE) || (size == EA_8BYTE) || (size == EA_16BYTE)");
                    immediateFits = Emitter.emitIns_valid_imm_for_ldst_offset(imm, size);
                    break;
                }

                default:
                {
                    assert(false, "!\"Unexpected instruction in genInstrWithConstant\"");
                    break;
                }
            }

            if (immediateFits)
            {
                Emitter.emitIns_R_R_I(ins, attr, reg1, reg2, imm);
            }
            else
            {
                assert(tmpReg != REG_NA);
                instGen_Set_Reg_To_Imm(EA_PTRSIZE, tmpReg, imm);
                _regSet.verifyRegUsed(tmpReg);
                if (inUnwindRegion)
                {
                    _compiler.unwindPadding();
                }

                Emitter.emitIns_R_R_R(ins, attr, reg1, reg2, tmpReg);
            }

            return immediateFits;
        }
    }
}
#endif
