// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if !TARGET_WASM
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    //------------------------------------------------------------------------
    // genSetBlockSize: Ensure that the block size is in the given register
    //
    // Arguments:
    //    blkNode - The block node
    //    sizeReg - The register into which the block's size should go
    //
    public void genSetBlockSize(GenTreeBlk blkNode, regNumber sizeReg)
    {
        if (sizeReg != REG_NA)
        {
            assert((InternalRegisters.GetAll(blkNode) & genRegMask(sizeReg)).IsNonEmpty,
                conditionExpression: "(internalRegisters.GetAll(blkNode) & genRegMask(sizeReg)) != 0");
            // This can go via helper which takes the size as a native uint.
            instGen_Set_Reg_To_Imm(EA_PTRSIZE, sizeReg, unchecked((nint)(nuint)blkNode.Size));
        }
    }

    //------------------------------------------------------------------------
    // genConsumeBlockSrc: Consume the source address register of a block node, if any.
    //
    // Arguments:
    //    blkNode - The block node
    public void genConsumeBlockSrc(GenTreeBlk blkNode)
    {
        var src = blkNode.Data;
        if (blkNode.IsCopyBlkOp)
        {
            // For a CopyBlk we need the address of the source.
            assert(src.IsContained, conditionExpression: "src->isContained()");
            if (src.Oper is GT_IND)
            {
                src = src.AsUnOp().Op1;
            }
            else
            {
                // This must be a local.
                // For this case, there is no source address register, as it is a
                // stack-based address.
                assert(src.Oper.IsLocal, conditionExpression: "src->OperIsLocal()");

                return;
            }
        }
        else
        {
            if (src.Oper.IsInitVal)
            {
                src = src.AsUnOp().Op1;
            }
        }

        _ = genConsumeReg(src);
    }

    //------------------------------------------------------------------------
    // genSetBlockSrc: Ensure that the block source is in its allocated register.
    //
    // Arguments:
    //    blkNode - The block node
    //    srcReg  - The register in which to set the source (address or init val).
    //
    public void genSetBlockSrc(GenTreeBlk blkNode, regNumber srcReg)
    {
        var src = blkNode.Data;
        if (blkNode.IsCopyBlkOp)
        {
            // For a CopyBlk we need the address of the source.
            if (src.Oper is GT_IND)
            {
                src = src.AsUnOp().Op1;
            }
            else
            {
                // This must be a local struct.
                // Load its address into srcReg.
                var varNum = src.AsLclVarCommon().LclNum;
                var offset = src.AsLclVarCommon().LclOffs;
                Emitter.emitIns_R_S(INS_lea, EA_BYREF, srcReg, varNum, offset);

                return;
            }
        }
        else
        {
            if (src.Oper.IsInitVal)
            {
                src = src.AsUnOp().Op1;
            }
        }

        genCopyRegIfNeeded(src, srcReg);
    }

    //------------------------------------------------------------------------
    // genConsumeBlockOp: Ensure that the block's operands are enregistered
    //                    as needed.
    // Arguments:
    //    blkNode - The block node
    //
    // Notes:
    //    This ensures that the operands are consumed in the proper order to
    //    obey liveness modeling.
    public void genConsumeBlockOp(GenTreeBlk blkNode, regNumber dstReg, regNumber srcReg, regNumber sizeReg)
    {
        // We have to consume the registers, and perform any copies, in the actual execution order: dst, src, size.
        //
        // Note that the register allocator ensures that the registers ON THE NODES will not interfere
        // with one another if consumed (i.e. reloaded or moved to their ASSIGNED reg) in execution order.
        // Further, it ensures that they will not interfere with one another if they are then copied
        // to the REQUIRED register (if a fixed register requirement) in execution order.  This requires,
        // then, that we first consume all the operands, then do any necessary moves.

        var dstAddr = blkNode.Addr;

        // First, consume all the sources in order, and verify that registers have been allocated appropriately,
        // based on the 'gtBlkOpKind'.

        // The destination is always in a register; 'genConsumeReg' asserts that.
        _ = genConsumeReg(dstAddr);
        // The source may be a local or in a register; 'genConsumeBlockSrc' will check that.
        genConsumeBlockSrc(blkNode);

        // Next, perform any necessary moves.
        genCopyRegIfNeeded(dstAddr, dstReg);
        genSetBlockSrc(blkNode, srcReg);
        genSetBlockSize(blkNode, sizeReg);
    }

#if TARGET_LOONGARCH64 || TARGET_RISCV64
    public void instGen_Set_Reg_To_Imm(emitAttr size, regNumber reg, nint imm,
        insFlags flags = INS_FLAGS_DONT_CARE
#if DEBUG
        , nuint targetHandle = 0, GenTreeFlags gtFlags = GTF_EMPTY
#endif
        )
    {
        throw new System.NotSupportedException("instGen_Set_Reg_To_Imm for this target is not yet ported.");
    }
#endif
}
#endif
