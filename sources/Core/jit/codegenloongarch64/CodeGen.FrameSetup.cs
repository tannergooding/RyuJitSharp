// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime. Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private void genEstablishFramePointerLoongArch64(int delta, bool reportUnwindData)
    {
        assert(Emitter.emitGeneratingPrologOrFuncletProlog());

        if (delta == 0)
        {
            Emitter.emitIns_R_R(INS_mov, EA_PTRSIZE, REG_FPBASE_LOONGARCH64, REG_SPBASE_LOONGARCH64);
        }
        else
        {
            assert(Emitter.isValidSimm12(delta));
            Emitter.emitIns_R_R_I(INS_addi_d, EA_PTRSIZE, REG_FPBASE_LOONGARCH64, REG_SPBASE_LOONGARCH64, delta);
        }

        if (reportUnwindData)
        {
            _compiler.unwindSetFrameReg(REG_FPBASE_LOONGARCH64, unchecked((uint)delta));
        }
    }

    private unsafe void genAllocLclFrameLoongArch64(
        uint frameSize,
        regNumber initReg,
        ref bool initRegZeroed,
        regMaskTP maskArgRegsLiveIn)
    {
        assert(Emitter.emitGeneratingPrologOrFuncletProlog());
        if (frameSize == 0)
        {
            return;
        }

        var pageSize = unchecked((nuint)_compiler.eeGetPageSize());
        assert(!_compiler.compHasSecretStubArgument() || (REG_SECRET_STUB_PARAM_LOONGARCH64 != initReg));

        var lastTouchDelta = (nuint)frameSize;
        if (frameSize >= pageSize)
        {
            if (frameSize < unchecked(3 * pageSize))
            {
                for (var probeOffset = pageSize; probeOffset <= frameSize; probeOffset = unchecked(probeOffset + pageSize))
                {
                    Emitter.emitIns_I_la(EA_PTRSIZE, initReg, unchecked(-(nint)probeOffset));
                    Emitter.emitIns_R_R_R(INS_ldx_w, EA_4BYTE, REG_LOONGARCH64_R0, REG_SPBASE_LOONGARCH64, initReg);
                    _regSet.verifyRegUsed(initReg);
                    initRegZeroed = false;

                    lastTouchDelta = unchecked(lastTouchDelta - pageSize);
                }

                assert(lastTouchDelta == (frameSize % pageSize));
                _compiler.unwindPadding();
            }
            else
            {
                assert(frameSize >= unchecked(3 * pageSize));

                var availableMask = new regMaskTP(SRBM_ALLINT_LOONGARCH64) &
                    (_regSet.rsGetModifiedRegsMask() | ~new regMaskTP(SRBM_INT_CALLEE_SAVED_LOONGARCH64));
                availableMask &= ~maskArgRegsLiveIn;
                availableMask &= ~genRegMask(initReg);

                var offsetReg = initReg;
                assert(availableMask.IsNonEmpty);
                var limitMask = genFindLowestBit(availableMask);
                var limitReg = genRegNumFromMask((regMask)limitMask, TYP_INT);

                assert(unchecked((nint)(int)frameSize) == unchecked((nint)frameSize));

                Emitter.emitIns_R_I(INS_lu12i_w, EA_PTRSIZE, offsetReg, unchecked(-(nint)pageSize) >> 12);
                _regSet.verifyRegUsed(offsetReg);
                Emitter.emitIns_I_la(EA_PTRSIZE, limitReg, unchecked(-(nint)frameSize));
                _regSet.verifyRegUsed(limitReg);

                assert((pageSize & 0xfff) == 0);
                Emitter.emitIns_R_I(INS_lu12i_w, EA_PTRSIZE, REG_LOONGARCH64_R21, unchecked((nint)pageSize) >> 12);

                Emitter.emitIns_R_R_R(INS_ldx_w, EA_4BYTE, REG_LOONGARCH64_R0, REG_SPBASE_LOONGARCH64, offsetReg);
                Emitter.emitIns_R_R_R(INS_sub_d, EA_PTRSIZE, offsetReg, offsetReg, REG_LOONGARCH64_R21);

                assert(REG_LOONGARCH64_R21 != limitReg);
                assert(REG_LOONGARCH64_R21 != offsetReg);
                Emitter.emitIns_R_R_I(INS_bge, EA_PTRSIZE, offsetReg, limitReg, -2 << 2);

                initRegZeroed = false;
                _compiler.unwindPadding();
                lastTouchDelta = frameSize % pageSize;
            }
        }

        if (lastTouchDelta + STACK_PROBE_BOUNDARY_THRESHOLD_BYTES_LOONGARCH64 > pageSize)
        {
            assert(lastTouchDelta + STACK_PROBE_BOUNDARY_THRESHOLD_BYTES_LOONGARCH64 < unchecked(2 * pageSize));

            Emitter.emitIns_I_la(EA_PTRSIZE, initReg, unchecked(-(nint)frameSize));
            Emitter.emitIns_R_R_R(INS_ldx_w, EA_4BYTE, REG_LOONGARCH64_R0, REG_SPBASE_LOONGARCH64, initReg);
            _compiler.unwindPadding();

            _regSet.verifyRegUsed(initReg);
            initRegZeroed = false;
        }
    }

    private void genZeroInitFrameUsingBlockInitLoongArch64(
        int untrackedLocalHighOffset,
        int untrackedLocalLowOffset,
        regNumber initReg,
        ref bool initRegZeroed)
    {
        var availableMask = _regSet.rsGetModifiedRegsMask() | new regMaskTP(SRBM_INT_CALLEE_TRASH_LOONGARCH64);
        availableMask &= ~_calleeRegArgMaskLiveIn;
        availableMask &= ~genRegMask(initReg);

        var addressReg = initReg;
        initRegZeroed = false;

        assert((genRegMask(addressReg) & _calleeRegArgMaskLiveIn).IsEmpty);
        assert((untrackedLocalLowOffset % 4) == 0);

        if (Emitter.isValidSimm12(untrackedLocalLowOffset))
        {
            Emitter.emitIns_R_R_I(
                INS_addi_d,
                EA_PTRSIZE,
                addressReg,
                genFramePointerReg(),
                untrackedLocalLowOffset);
        }
        else
        {
            instGen_Set_Reg_To_Imm(EA_PTRSIZE, initReg, untrackedLocalLowOffset);
            Emitter.emitIns_R_R_R(INS_add_d, EA_PTRSIZE, addressReg, genFramePointerReg(), initReg);
            initRegZeroed = false;
        }

        var byteCount = unchecked((uint)untrackedLocalHighOffset - (uint)untrackedLocalLowOffset);
        assert((byteCount % sizeof(int)) == 0);
        var padding = unchecked((uint)untrackedLocalLowOffset & 0x7);

        if (padding != 0)
        {
            assert(padding == 4);
            Emitter.emitIns_R_R_I(INS_st_w, EA_4BYTE, REG_LOONGARCH64_R0, addressReg, 0);
            byteCount = unchecked(byteCount - 4);
        }

        var regSize = unchecked((uint)Configuration.REGSIZE_BYTES);
        var regPairSize = unchecked(regSize * 2);
        var slotCount = byteCount / regSize;
        var useLoop = slotCount >= 10;
        if (useLoop)
        {
            assert(availableMask.IsNonEmpty);
            var counterMask = genFindLowestBit(availableMask);
            var counterReg = genRegNumFromMask((regMask)counterMask, TYP_INT);

            assert(slotCount >= 2);
            assert((genRegMask(counterReg) & _calleeRegArgMaskLiveIn).IsEmpty);
            instGen_Set_Reg_To_Imm(EA_PTRSIZE, counterReg, unchecked((nint)(slotCount / 2)));

            Emitter.emitIns_R_R_I(INS_st_d, EA_PTRSIZE, REG_LOONGARCH64_R0, addressReg, unchecked((int)(8 + padding)));
            Emitter.emitIns_R_R_I(INS_st_d, EA_PTRSIZE, REG_LOONGARCH64_R0, addressReg, unchecked((int)padding));
            Emitter.emitIns_R_R_I(INS_addi_d, EA_PTRSIZE, counterReg, counterReg, -1);
            Emitter.emitIns_R_R_I(INS_addi_d, EA_PTRSIZE, addressReg, addressReg, unchecked((int)regPairSize));
            Emitter.emitIns_R_R_I(INS_bne, EA_PTRSIZE, counterReg, REG_LOONGARCH64_R0, -16);

            byteCount %= regPairSize;
        }
        else
        {
            while (byteCount >= regPairSize)
            {
                Emitter.emitIns_R_R_I(INS_st_d, EA_PTRSIZE, REG_LOONGARCH64_R0, addressReg, unchecked((int)(8 + padding)));
                Emitter.emitIns_R_R_I(INS_st_d, EA_PTRSIZE, REG_LOONGARCH64_R0, addressReg, unchecked((int)padding));
                Emitter.emitIns_R_R_I(
                    INS_addi_d,
                    EA_PTRSIZE,
                    addressReg,
                    addressReg,
                    unchecked((int)(regPairSize + padding)));
                byteCount -= regPairSize;
                padding = 0;
            }
        }

        if (byteCount >= regSize)
        {
            Emitter.emitIns_R_R_I(INS_st_d, EA_PTRSIZE, REG_LOONGARCH64_R0, addressReg, unchecked((int)padding));
            if ((byteCount - regSize) != 0)
            {
                Emitter.emitIns_R_R_I(INS_addi_d, EA_PTRSIZE, addressReg, addressReg, unchecked((int)regSize));
            }

            byteCount -= regSize;
        }

        if (byteCount > 0)
        {
            assert(byteCount == sizeof(int));
            Emitter.emitIns_R_R_I(INS_st_w, EA_4BYTE, REG_LOONGARCH64_R0, addressReg, unchecked((int)padding));
            byteCount -= sizeof(int);
        }

        noway_assert(byteCount == 0);
    }
}
