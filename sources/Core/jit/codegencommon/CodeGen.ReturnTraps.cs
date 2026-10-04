// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public unsafe void genCodeForReturnTrap(GenTreeUnOp tree)
    {
#if TARGET_LOONGARCH64 || TARGET_RISCV64
        assert(tree.Oper is GT_RETURNTRAP);
        var data = tree.Op1;
        // This is a conditional call to CORINFO_HELP_STOP_FOR_GC based on the contents of data.
        genConsumeRegs(data);

        var skipLabel = genCreateTempLabel();
        Emitter.emitIns_J_cond_la(INS_beq, skipLabel, data.RegNum, REG_R0);

        var callParams = new EmitCallParams();
        var helperFunction = _compiler.compGetHelperFtn(CORINFO_HELP_STOP_FOR_GC);
        if (helperFunction.accessType is IAT_VALUE)
        {
            callParams.addr = helperFunction.addr;
            callParams.callType = EC_FUNC_TOKEN;
#if TARGET_RISCV64
            callParams.ireg = callParams.isJump ? rsGetRsvdReg() : REG_RA;
#endif
        }
        else
        {
            callParams.addr = null;
            callParams.callType = EC_INDIR_R;
            callParams.ireg = REG_DEFAULT_HELPER_CALL_TARGET;
#if TARGET_LOONGARCH64
            assert(helperFunction.accessType is IAT_PVALUE);
            var helperAddress = unchecked((nint)helperFunction.addr);
            if (_compiler.opts.compReloc)
            {
                Emitter.emitIns_R_AI(INS_bl, EA_PTR_DSP_RELOC, callParams.ireg, helperAddress);
            }
            else
            {
                Emitter.emitIns_R_I(INS_lu12i_w, EA_PTRSIZE, callParams.ireg,
                    (helperAddress & unchecked((nint)0xfffff000)) >> 12);
                Emitter.emitIns_R_I(INS_lu32i_d, EA_PTRSIZE, callParams.ireg, helperAddress >> 32);
                Emitter.emitIns_R_R_I(INS_ldptr_d, EA_PTRSIZE, callParams.ireg, callParams.ireg,
                    (helperAddress & 0xfff) >> 2);
            }
#elif TARGET_RISCV64
            Emitter.emitIns_R_R_Addr(
                INS_ld,
                EA_PTRSIZE,
                callParams.ireg,
                callParams.ireg,
                helperFunction.addr);
#endif
            _regSet.verifyRegUsed(callParams.ireg);
        }

#if TARGET_LOONGARCH64
        // TODO-LOONGARCH64: can optimize further !!!
        // TODO-LOONGARCH64: Why does this not use genEmitHelperCall?
#else
        // TODO-RISCV64: can optimize further !!!
        // TODO-RISCV64: Why does this not use genEmitHelperCall?
#endif
        callParams.methHnd = Compiler.eeFindHelper(CORINFO_HELP_STOP_FOR_GC);
        genEmitCallWithCurrentGC(ref callParams);

        var killMask = _compiler.compHelperCallKillSet(CORINFO_HELP_STOP_FOR_GC);
        _regSet.verifyRegistersUsed(killMask);
        genDefineTempLabel(skipLabel);
#elif TARGET_ARM64
        assert(tree.Oper is GT_RETURNTRAP);
        var data = tree.Op1;
        genConsumeRegs(data);
        Emitter.emitIns_R_I(INS_cmp, EA_4BYTE, data.RegNum, 0);

        var skipLabel = genCreateTempLabel();
        inst_JMP(EJ_eq, skipLabel);
        genEmitHelperCall(CORINFO_HELP_STOP_FOR_GC, 0, EA_UNKNOWN);
        genDefineTempLabel(skipLabel);
#elif !TARGET_XARCH
        throw new FatalJitException(CORJIT_SKIPPED, "Return-trap generation requires AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(tree.Oper == GT_RETURNTRAP);
        var data = tree.Op1;
        genConsumeRegs(data);
        var zero = new GenTreeIntCon(TYP_INT, 0) { IsContained = true };
        _ = Emitter.emitInsBinary(INS_cmp, EA_4BYTE, data, zero);

        var skipLabel = genCreateTempLabel();
        inst_JMP(EJ_je, skipLabel);
        var tempReg = _internalRegisters.GetSingle(tree, new regMaskTP(_compiler.SRBM_ALLINT));
        assert(genIsValidIntReg(tempReg));
        genEmitHelperCall(CORINFO_HELP_STOP_FOR_GC, 0, EA_UNKNOWN, tempReg);
        genDefineTempLabel(skipLabel);
#endif
    }
}
