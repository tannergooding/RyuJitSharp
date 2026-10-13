// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForStoreBlkArmArch(GenTreeBlk blkOp)
    {
        assert(blkOp.Oper is GT_STORE_BLK);
        var isCopyBlk = blkOp.IsCopyBlkOp;

        switch (blkOp._kind)
        {
            case GenTreeBlk.BlkOpKindLoop:
            {
                assert(!isCopyBlk);
                genCodeForInitBlkLoopArmArch(blkOp);
                break;
            }

            case GenTreeBlk.BlkOpKindUnroll:
            case GenTreeBlk.BlkOpKindUnrollMemmove:
            {
                if (isCopyBlk)
                {
                    if (blkOp._gcUnsafe)
                    {
                        Emitter.emitDisableGC();
                    }

                    if (blkOp._kind is GenTreeBlk.BlkOpKindUnroll)
                    {
                        genCodeForCpBlkUnrollArmArch(blkOp);
                    }
                    else
                    {
                        assert(blkOp._kind is GenTreeBlk.BlkOpKindUnrollMemmove);
                        genCodeForMemmove(blkOp);
                    }

                    if (blkOp._gcUnsafe)
                    {
                        Emitter.emitEnableGC();
                    }
                }
                else
                {
                    assert(!blkOp._gcUnsafe);
                    genCodeForInitBlkUnrollArmArch(blkOp);
                }
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }
    }

    public void genCodeForInitBlkLoopArmArch(GenTreeBlk initBlkNode)
    {
        var dstNode = initBlkNode.Addr;
        var dstReg = genConsumeReg(dstNode);

        var zeroReg = Configuration.TARGET_ARM64 ? genGetZeroRegArm64() : genConsumeReg(initBlkNode.Data);

        if (initBlkNode.IsVolatile)
        {
            instGen_MemoryBarrier(BARRIER_STORE_ONLY);
        }

        var size = initBlkNode.Layout.Size;
        assert((size >= Configuration.TARGET_POINTER_SIZE) && ((size % Configuration.TARGET_POINTER_SIZE) == 0));

        // Store the first word before forming the loop's potentially large offset.
        Emitter.emitIns_R_R(INS_str, EA_PTRSIZE, zeroReg, dstReg);
        if (size > Configuration.TARGET_POINTER_SIZE)
        {
            GCInfo.gcMarkRegPtrVal(dstReg, dstNode.Type);

            var offsetReg = InternalRegisters.GetSingle(initBlkNode);
            instGen_Set_Reg_To_Imm(EA_PTRSIZE, offsetReg,
                unchecked((nint)(size - Configuration.TARGET_POINTER_SIZE)));

            var loop = genCreateTempLabel();
            genDefineTempLabel(loop);

            Emitter.emitIns_R_R_R(INS_str, EA_PTRSIZE, zeroReg, dstReg, offsetReg);
            if (Configuration.TARGET_ARM64)
            {
                Emitter.emitIns_R_R_I(INS_subs, EA_PTRSIZE, offsetReg, offsetReg, Configuration.TARGET_POINTER_SIZE);
            }
            else
            {
                Emitter.emitIns_R_R_I(INS_sub, EA_PTRSIZE, offsetReg, offsetReg,
                    Configuration.TARGET_POINTER_SIZE, INS_FLAGS_SET);
            }
            inst_JMP(EJ_ne, loop);

            GCInfo.gcMarkRegSetNpt(genRegMask(dstReg));
        }
    }
}
