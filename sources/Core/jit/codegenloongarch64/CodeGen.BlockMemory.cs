// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64
using System;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForStoreBlk(GenTreeBlk node)
    {
        assert(node.Oper is GT_STORE_BLK);

        if (node._gcUnsafe)
        {
            Emitter.emitDisableGC();
        }

        var isCopyBlk = node.IsCopyBlkOp;

        switch (node._kind)
        {
            case GenTreeBlk.BlkOpKindLoop:
            {
                assert(!isCopyBlk);
                genCodeForInitBlkLoop(node);
                break;
            }

            case GenTreeBlk.BlkOpKindUnroll:
            {
                if (isCopyBlk)
                {
                    genCodeForCpBlkUnroll(node);
                }
                else
                {
                    genCodeForInitBlkUnroll(node);
                }

                break;
            }

            default:
            {
                unreached();
                break;
            }
        }

        if (node._gcUnsafe)
        {
            Emitter.emitEnableGC();
        }
    }

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

            var baseAddress = addrMode.BaseAddress ??
                throw new InvalidOperationException("A block-copy destination address mode requires a base.");
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
                var baseAddress = addrMode.BaseAddress ??
                    throw new InvalidOperationException("A block-copy source address mode requires a base.");
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
            instGen_MemoryBarrier();
        }

        var size = node.Layout.Size;
        assert(size <= int.MaxValue);
        assert(srcOffset < int.MaxValue - unchecked((int)size));
        assert(dstOffset < int.MaxValue - unchecked((int)size));

        var tempReg = InternalRegisters.Extract(node, new regMaskTP(SRBM_ALLINT));

        if (size >= (uint)(2 * REGSIZE_BYTES))
        {
            var tempReg2 = REG_R21;

            for (var regSize = (uint)(2 * REGSIZE_BYTES); size >= regSize;
                 size -= regSize, srcOffset += (int)regSize, dstOffset += (int)regSize)
            {
                if (srcLclNum != BAD_VAR_NUM)
                {
                    Emitter.emitIns_R_S(INS_ld_d, EA_8BYTE, tempReg, srcLclNum, srcOffset);
                    Emitter.emitIns_R_S(INS_ld_d, EA_8BYTE, tempReg2, srcLclNum, srcOffset + 8);
                }
                else
                {
                    Emitter.emitIns_R_R_I(INS_ld_d, EA_8BYTE, tempReg, srcAddrBaseReg, srcOffset);
                    Emitter.emitIns_R_R_I(INS_ld_d, EA_8BYTE, tempReg2, srcAddrBaseReg, srcOffset + 8);
                }

                if (dstLclNum != BAD_VAR_NUM)
                {
                    Emitter.emitIns_S_R(INS_st_d, EA_8BYTE, tempReg, dstLclNum, dstOffset);
                    Emitter.emitIns_S_R(INS_st_d, EA_8BYTE, tempReg2, dstLclNum, dstOffset + 8);
                }
                else
                {
                    Emitter.emitIns_R_R_I(INS_st_d, EA_8BYTE, tempReg, dstAddrBaseReg, dstOffset);
                    Emitter.emitIns_R_R_I(INS_st_d, EA_8BYTE, tempReg2, dstAddrBaseReg, dstOffset + 8);
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
                    loadIns = INS_ld_b;
                    storeIns = INS_st_b;
                    attr = EA_4BYTE;
                    break;
                }

                case 2:
                {
                    loadIns = INS_ld_h;
                    storeIns = INS_st_h;
                    attr = EA_4BYTE;
                    break;
                }

                case 4:
                {
                    loadIns = INS_ld_w;
                    storeIns = INS_st_w;
                    attr = EA_4BYTE;
                    break;
                }

                case 8:
                {
                    loadIns = INS_ld_d;
                    storeIns = INS_st_d;
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
                Emitter.emitIns_R_S(loadIns, attr, tempReg, srcLclNum, srcOffset);
            }
            else
            {
                Emitter.emitIns_R_R_I(loadIns, attr, tempReg, srcAddrBaseReg, srcOffset);
            }

            if (dstLclNum != BAD_VAR_NUM)
            {
                Emitter.emitIns_S_R(storeIns, attr, tempReg, dstLclNum, dstOffset);
            }
            else
            {
                Emitter.emitIns_R_R_I(storeIns, attr, tempReg, dstAddrBaseReg, dstOffset);
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

        // Zero the first pointer as a nullcheck before accessing a potentially large offset.
        Emitter.emitIns_R_R_I(INS_st_d, EA_PTRSIZE, REG_R0, dstReg, 0);
        if (size > TARGET_POINTER_SIZE)
        {
            GCInfo.gcMarkRegPtrVal(dstReg, dstNode.Type);

            var offsetReg = InternalRegisters.GetSingle(node);
            instGen_Set_Reg_To_Imm(EA_PTRSIZE, offsetReg, unchecked((nint)(size - TARGET_POINTER_SIZE)));

            Emitter.emitIns_R_R_R(INS_stx_d, EA_PTRSIZE, REG_R0, dstReg, offsetReg);
            Emitter.emitIns_R_R_I(INS_addi_d, EA_PTRSIZE, offsetReg, offsetReg, -8);
            // Branch back two instructions; the loop keeps the base pointer live, not an interior pointer.
            Emitter.emitIns_R_I(INS_bnez, EA_8BYTE, offsetReg, -2 << 2);

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
                Emitter.emitIns_S_R(INS_st_d, EA_8BYTE, srcReg, dstLclNum, dstOffset);
                Emitter.emitIns_S_R(INS_st_d, EA_8BYTE, srcReg, dstLclNum, dstOffset + 8);
            }
            else
            {
                Emitter.emitIns_R_R_I(INS_st_d, EA_8BYTE, srcReg, dstAddrBaseReg, dstOffset);
                Emitter.emitIns_R_R_I(INS_st_d, EA_8BYTE, srcReg, dstAddrBaseReg, dstOffset + 8);
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
                    storeIns = INS_st_b;
                    attr = EA_4BYTE;
                    break;
                }
                case 2:
                {
                    storeIns = INS_st_h;
                    attr = EA_4BYTE;
                    break;
                }
                case 4:
                {
                    storeIns = INS_st_w;
                    attr = EA_4BYTE;
                    break;
                }
                case 8:
                {
                    storeIns = INS_st_d;
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
        instGen_MemoryBarrier(BARRIER_FULL);
    }

    public void instGen_MemoryBarrier(BarrierKind barrierKind)
    {
#if DEBUG
        if (JitConfig.JitNoMemoryBarriers == 1)
        {
            return;
        }
#endif

        // The pinned instr.h maps every LoongArch64 barrier kind to the full-barrier hint, zero.
        // TODO-LOONGARCH64: Use the exact barrier type depending on the CPU.
        Emitter.emitIns_I(INS_dbar, EA_4BYTE, 0);
    }
}
#endif
