// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForLockAdd(GenTreeOp node)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Locked addition generation requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(node.Oper is GT_LOCKADD);
        var addr = node.Op1;
        var data = node.Op2;
        var size = data.Type.EmitActualSize;
        assert(addr.IsUsedFromReg);
        assert(data.IsUsedFromReg || data.IsContainedIntOrIImmed);
        assert((size == EA_4BYTE) || (size == EA_PTRSIZE));

        genConsumeOperands(node);
        instGen(INS_lock);
        if (data.IsContainedIntOrIImmed)
        {
            var imm = unchecked((int)data.AsIntCon().IconValue);
            assert(imm == data.AsIntCon().IconValue);
            if (imm == 1)
            {
                Emitter.emitIns_AR(INS_inc, size, addr.RegNum, 0, INS_OPTS_EVEX_NoApxPromotion);
            }
            else if (imm == -1)
            {
                Emitter.emitIns_AR(INS_dec, size, addr.RegNum, 0, INS_OPTS_EVEX_NoApxPromotion);
            }
            else
            {
                Emitter.emitIns_I_AR(INS_add, size, imm, addr.RegNum, 0, INS_OPTS_EVEX_NoApxPromotion);
            }
        }
        else
        {
            Emitter.emitIns_AR_R(INS_add, size, data.RegNum, addr.RegNum, 0, INS_OPTS_EVEX_NoApxPromotion);
        }
#endif
    }

    public void genLockedInstructions(GenTreeOp node)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Locked instruction generation requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(node.Oper is GT_XADD or GT_XCHG or GT_XORR or GT_XAND);
        assert((node.Oper is GT_XCHG) || !varTypeIsSmall(node.Type));
        var addr = node.Op1;
        var data = node.Op2;
        var size = node.Type.EmitSize;
        assert(addr.IsUsedFromReg && data.IsUsedFromReg);
        assert((size <= EA_PTRSIZE) || (size == EA_GCREF));
        genConsumeOperands(node);

        if (node.Oper is GT_XORR or GT_XAND)
        {
            var ins = (node.Oper is GT_XORR) ? INS_or : INS_and;
            if (node.IsUnusedValue)
            {
                instGen(INS_lock);
                Emitter.emitIns_AR_R(ins, size, data.RegNum, addr.RegNum, 0, INS_OPTS_EVEX_NoApxPromotion);
            }
            else
            {
                // CMPXCHG retries keep the address live across the backedge and return the old value in RAX.
                _gcInfo.gcMarkRegPtrVal(addr.RegNum, addr.Type);
                var tmpReg = InternalRegisters.GetSingle(node);
                Emitter.emitIns_R_AR(INS_mov, size, REG_RAX, addr.RegNum, 0);
                var loop = genCreateTempLabel();
                genDefineTempLabel(loop);
                Emitter.emitIns_Mov(INS_mov, size, tmpReg, REG_RAX, canSkip: false);
                Emitter.emitIns_R_R(ins, size, tmpReg, data.RegNum);
                instGen(INS_lock);
                Emitter.emitIns_AR_R(INS_cmpxchg, size, tmpReg, addr.RegNum, 0);
                inst_JMP(EJ_jne, loop);

                _gcInfo.gcMarkRegSetNpt(regMaskTP.CreateFromRegNum(addr.RegNum, addr.RegNum.SingleTypeMask));
                inst_Mov(node.Type, node.RegNum, REG_RAX, canSkip: true);
                genProduceReg(node);
            }
            return;
        }

        assert((node.RegNum != addr.RegNum) || (node.RegNum == data.RegNum));
        Emitter.emitIns_Mov(INS_mov, size, node.RegNum, data.RegNum, canSkip: true);
        var instruction = (node.Oper is GT_XADD) ? INS_xadd : INS_xchg;
        if (instruction != INS_xchg)
        {
            instGen(INS_lock);
        }
        Emitter.emitIns_AR_R(instruction, size, node.RegNum, addr.RegNum, 0);

        if (varTypeIsSmall(node.Type))
        {
            var mov = varTypeIsSigned(node.Type) ? INS_movsx : INS_movzx;
            Emitter.emitIns_Mov(mov, size, node.RegNum, node.RegNum, canSkip: false);
        }
        genProduceReg(node);
#endif
    }

    public void genCodeForCmpXchg(GenTreeCmpXchg tree)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Compare-exchange generation requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(tree.Oper is GT_CMPXCHG);
        var targetType = tree.Type;
        var targetReg = tree.RegNum;
        var size = tree.Type.EmitSize;
        var location = tree.Addr;
        var value = tree.Data;
        var comparand = tree.Comparand;
        assert(location.RegNum is not REG_NA and not REG_RAX);
        assert(value.RegNum is not REG_NA and not REG_RAX);

        genConsumeReg(location);
        genConsumeReg(value);
        genConsumeReg(comparand);
        // Consume all operands first: a GT_COPY may still need the original RAX.
        inst_Mov(comparand.Type, REG_RAX, comparand.RegNum, canSkip: true);
        instGen(INS_lock);
        Emitter.emitIns_AR_R(INS_cmpxchg, size, value.RegNum, location.RegNum, 0);

        if (varTypeIsSmall(targetType))
        {
            var mov = varTypeIsSigned(targetType) ? INS_movsx : INS_movzx;
            Emitter.emitIns_Mov(mov, size, targetReg, REG_RAX, canSkip: false);
        }
        else
        {
            inst_Mov(targetType, targetReg, REG_RAX, canSkip: true);
        }
        genProduceReg(tree);
#endif
    }

    public void instGen_MemoryBarrier(BarrierKind barrierKind)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Memory barrier generation requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
#if DEBUG
        if (JitConfig.JitNoMemoryBarriers == 1)
        {
            return;
        }
#endif
        // Load-only and store-only barriers require no instructions on xarch.
        if (barrierKind == BARRIER_FULL)
        {
            instGen(INS_lock);
            Emitter.emitIns_I_AR(INS_or, EA_4BYTE, 0, REG_SPBASE, 0, INS_OPTS_EVEX_NoApxPromotion);
        }
#endif
    }
}
