// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
namespace RyuJitSharp;

public sealed partial class CodeGen
{
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
