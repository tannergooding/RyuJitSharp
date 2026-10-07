// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForCpBlkUnroll(GenTreeBlk node)
    {
        assert(node.Oper is GT_STORE_BLK);

        var dstLclNum = BAD_VAR_NUM;
        var dstAddrBaseReg = REG_NA;
        var dstOffset = 0;
        var dstAddr = node.Addr;

        if (!dstAddr.IsContained)
        {
            dstAddrBaseReg = genConsumeReg(dstAddr);
        }
        else if (dstAddr.Oper.IsAddrMode)
        {
            var addrMode = dstAddr.AsAddrMode();
            assert(!addrMode.HasIndex);

            if (addrMode.BaseAddress is not { } baseAddress)
            {
                unreached();
                return;
            }

            dstAddrBaseReg = genConsumeReg(baseAddress);
            dstOffset = addrMode.Offset;
        }
        else
        {
            assert(dstAddr.Oper is GT_LCL_ADDR);
            var dstLocal = dstAddr.AsLclVarCommon();
            dstLclNum = dstLocal.LclNum;
            dstOffset = dstLocal.LclOffs;
        }

        var srcLclNum = BAD_VAR_NUM;
        var srcAddrBaseReg = REG_NA;
        var srcOffset = 0;
        var src = node.Data;

        assert(src.IsContained);

        if (src.Oper is GT_LCL_VAR or GT_LCL_FLD)
        {
            var srcLocal = src.AsLclVarCommon();
            srcLclNum = srcLocal.LclNum;
            srcOffset = srcLocal.LclOffs;
        }
        else
        {
            assert(src.Oper is GT_IND);
            var srcAddr = src.AsIndir().Addr;

            if (!srcAddr.IsContained)
            {
                srcAddrBaseReg = genConsumeReg(srcAddr);
            }
            else if (srcAddr.Oper.IsAddrMode)
            {
                var addrMode = srcAddr.AsAddrMode();
                assert(!addrMode.HasIndex);

                if (addrMode.BaseAddress is not { } baseAddress)
                {
                    unreached();
                    return;
                }

                srcAddrBaseReg = genConsumeReg(baseAddress);
                srcOffset = addrMode.Offset;
            }
            else
            {
                assert(srcAddr.Oper is GT_LCL_ADDR);
                var srcLocal = srcAddr.AsLclVarCommon();
                srcLclNum = srcLocal.LclNum;
                srcOffset = srcLocal.LclOffs;
            }
        }

        if (node.IsVolatile)
        {
            instGen_MemoryBarrier(BARRIER_FULL);
        }

        var emit = GetEmitter();
        var size = node.Layout.Size;

        assert(size <= int.MaxValue);
        assert(srcOffset < int.MaxValue - unchecked((int)size));
        assert(dstOffset < int.MaxValue - unchecked((int)size));

        var tempReg = InternalRegisters.Extract(node, new regMaskTP(SRBM_ALLINT));

        if (size >= (uint)(2 * REGSIZE_BYTES))
        {
            var tempReg2 = REG_RA;

            for (var regSize = (uint)(2 * REGSIZE_BYTES); size >= regSize;
                 size -= regSize, srcOffset += (int)regSize, dstOffset += (int)regSize)
            {
                if (srcLclNum != BAD_VAR_NUM)
                {
                    emit.emitIns_R_S(INS_ld, EA_8BYTE, tempReg, srcLclNum, srcOffset);
                    emit.emitIns_R_S(INS_ld, EA_8BYTE, tempReg2, srcLclNum, srcOffset + 8);
                }
                else
                {
                    emit.emitIns_R_R_I(INS_ld, EA_8BYTE, tempReg, srcAddrBaseReg, srcOffset);
                    emit.emitIns_R_R_I(INS_ld, EA_8BYTE, tempReg2, srcAddrBaseReg, srcOffset + 8);
                }

                if (dstLclNum != BAD_VAR_NUM)
                {
                    emit.emitIns_S_R(INS_sd, EA_8BYTE, tempReg, dstLclNum, dstOffset);
                    emit.emitIns_S_R(INS_sd, EA_8BYTE, tempReg2, dstLclNum, dstOffset + 8);
                }
                else
                {
                    emit.emitIns_R_R_I(INS_sd, EA_8BYTE, tempReg, dstAddrBaseReg, dstOffset);
                    emit.emitIns_R_R_I(INS_sd, EA_8BYTE, tempReg2, dstAddrBaseReg, dstOffset + 8);
                }
            }
        }

        for (var regSize = (uint)REGSIZE_BYTES; size > 0;
             size -= regSize, srcOffset += (int)regSize, dstOffset += (int)regSize)
        {
            while (regSize > size)
            {
                regSize /= 2;
            }

            instruction loadIns;
            instruction storeIns;
            emitAttr attr;

            switch (regSize)
            {
                case 1:
                {
                    loadIns = INS_lb;
                    storeIns = INS_sb;
                    attr = EA_4BYTE;
                    break;
                }
                case 2:
                {
                    loadIns = INS_lh;
                    storeIns = INS_sh;
                    attr = EA_4BYTE;
                    break;
                }
                case 4:
                {
                    loadIns = INS_lw;
                    storeIns = INS_sw;
                    attr = EA_4BYTE;
                    break;
                }
                case 8:
                {
                    loadIns = INS_ld;
                    storeIns = INS_sd;
                    attr = EA_8BYTE;
                    break;
                }
                default:
                {
                    unreached();
                    return;
                }
            }

            if (srcLclNum != BAD_VAR_NUM)
            {
                emit.emitIns_R_S(loadIns, attr, tempReg, srcLclNum, srcOffset);
            }
            else
            {
                emit.emitIns_R_R_I(loadIns, attr, tempReg, srcAddrBaseReg, srcOffset);
            }

            if (dstLclNum != BAD_VAR_NUM)
            {
                emit.emitIns_S_R(storeIns, attr, tempReg, dstLclNum, dstOffset);
            }
            else
            {
                emit.emitIns_R_R_I(storeIns, attr, tempReg, dstAddrBaseReg, dstOffset);
            }
        }

        if (node.IsVolatile)
        {
            instGen_MemoryBarrier(BARRIER_LOAD_ONLY);
        }
    }

    public void genCodeForInitBlkLoop(GenTreeBlk node)
    {
        var dstNode = node.Addr;
        _ = genConsumeReg(dstNode);
        var dstReg = dstNode.RegNum;

        if (node.IsVolatile)
        {
            instGen_MemoryBarrier();
        }

        var size = node.Layout.Size;
        assert((size >= TARGET_POINTER_SIZE) && ((size % TARGET_POINTER_SIZE) == 0));

        // Store the first pointer before forming the loop's potentially large offset to retain nullcheck behavior.
        Emitter.emitIns_R_R_I(INS_sd, EA_PTRSIZE, REG_R0, dstReg, 0);
        if (size > TARGET_POINTER_SIZE)
        {
            GCInfo.gcMarkRegPtrVal(dstReg, dstNode.Type);

            var tempReg = InternalRegisters.GetSingle(node);
            instGen_Set_Reg_To_Imm(EA_PTRSIZE, tempReg, unchecked((nint)(size - TARGET_POINTER_SIZE)));

            // tempReg = dstReg + tempReg (a new interior pointer, but in a nongc region).
            Emitter.emitIns_R_R_R(INS_add, EA_PTRSIZE, tempReg, dstReg, tempReg);

            var loop = genCreateTempLabel();
            genDefineTempLabel(loop);
            // TODO: add GC info for tempReg and remove the no-GC region.
            Emitter.emitDisableGC();

            Emitter.emitIns_R_R_I(INS_sd, EA_PTRSIZE, REG_R0, tempReg, 0);
            Emitter.emitIns_R_R_I(INS_addi, EA_PTRSIZE, tempReg, tempReg, -TARGET_POINTER_SIZE);
            Emitter.emitIns_J_cond_la(INS_bne, loop, tempReg, dstReg);
            Emitter.emitEnableGC();

            GCInfo.gcMarkRegSetNpt(genRegMask(dstReg));
        }
    }

    public void genCodeForInitBlkUnroll(GenTreeBlk node)
    {
        assert(node.Oper is GT_STORE_BLK);

        var dstLclNum = BAD_VAR_NUM;
        var dstAddrBaseReg = REG_NA;
        var dstOffset = 0;
        var dstAddr = node.Addr;

        if (!dstAddr.IsContained)
        {
            dstAddrBaseReg = genConsumeReg(dstAddr);
        }
        else if (dstAddr.Oper.IsAddrMode)
        {
            var addrMode = dstAddr.AsAddrMode();
            assert(!addrMode.HasIndex);
            assert(addrMode.HasBaseAddress);

            dstAddrBaseReg = genConsumeReg(addrMode.BaseAddress);
            dstOffset = addrMode.Offset;
        }
        else
        {
            assert(dstAddr.Oper is GT_LCL_ADDR);
            var dstLocal = dstAddr.AsLclVarCommon();
            dstLclNum = dstLocal.LclNum;
            dstOffset = dstLocal.LclOffs;
        }

        var src = node.Data;
        if (src.Oper is GT_INIT_VAL)
        {
            assert(src.IsContained);
            src = src.AsUnOp().Op1;
        }

        var srcReg = REG_R0;
        if (!src.IsContained)
        {
            srcReg = genConsumeReg(src);
        }
        else
        {
            assert(src.IsIntegralConst(0));
        }

        if (node.IsVolatile)
        {
            instGen_MemoryBarrier();
        }

        var size = node.Layout.Size;
        assert(size <= int.MaxValue);
        assert(dstOffset < int.MaxValue - unchecked((int)size));

        for (var regSize = (uint)(2 * REGSIZE_BYTES); size >= regSize; size -= regSize, dstOffset += (int)regSize)
        {
            if (dstLclNum != BAD_VAR_NUM)
            {
                Emitter.emitIns_S_R(INS_sd, EA_8BYTE, srcReg, dstLclNum, dstOffset);
                Emitter.emitIns_S_R(INS_sd, EA_8BYTE, srcReg, dstLclNum, dstOffset + 8);
            }
            else
            {
                Emitter.emitIns_R_R_I(INS_sd, EA_8BYTE, srcReg, dstAddrBaseReg, dstOffset);
                Emitter.emitIns_R_R_I(INS_sd, EA_8BYTE, srcReg, dstAddrBaseReg, dstOffset + 8);
            }
        }

        for (var regSize = (uint)REGSIZE_BYTES; size > 0; size -= regSize, dstOffset += (int)regSize)
        {
            while (regSize > size)
            {
                regSize /= 2;
            }

            instruction storeIns;
            emitAttr attr;

            switch (regSize)
            {
                case 1:
                {
                    storeIns = INS_sb;
                    attr = EA_4BYTE;
                    break;
                }
                case 2:
                {
                    storeIns = INS_sh;
                    attr = EA_4BYTE;
                    break;
                }
                case 4:
                {
                    storeIns = INS_sw;
                    attr = EA_4BYTE;
                    break;
                }
                case 8:
                {
                    storeIns = INS_sd;
                    attr = EA_8BYTE;
                    break;
                }
                default:
                {
                    unreached();
                    return;
                }
            }

            if (dstLclNum != BAD_VAR_NUM)
            {
                Emitter.emitIns_S_R(storeIns, attr, srcReg, dstLclNum, dstOffset);
            }
            else
            {
                Emitter.emitIns_R_R_I(storeIns, attr, srcReg, dstAddrBaseReg, dstOffset);
            }
        }
    }

    public void instGen_MemoryBarrier()
    {
        throw new FatalJitException(CORJIT_SKIPPED, "RISC-V64 memory barrier emission is not ported.");
    }
}
#endif
