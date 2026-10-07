// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_LOONGARCH64 || TARGET_RISCV64
using static RyuJitSharp.GenTreeFlags;

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns_J_cond_la(instruction ins, BasicBlock target, regNumber reg1, regNumber reg2)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Target conditional-branch recording is not implemented.");
    }

    public void emitIns_R_I(instruction ins, emitAttr attr, regNumber reg, nint imm)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Target register-immediate recording is not implemented.");
    }

    public void emitIns_R_R(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Target two-register instruction recording is not implemented.");
    }

#if TARGET_RISCV64
    public unsafe void emitIns_R_AI(
        instruction ins,
        emitAttr attr,
        regNumber dataReg,
        regNumber addrReg,
        nint disp)
    {
        var compiler = _compiler ?? throw new FatalJitException("Relocated-address instruction recording requires an active compiler.");

        assert(EA_IS_RELOC(attr));
        assert(compiler.opts.compReloc ||
            (compiler.eeGetRelocTypeHint((void*)disp) is CorInfoReloc.RELATIVE32));
        assert(ins is INS_addi || emitInsIsLoadOrStore(ins));
        assert(emitInsIsStore(ins) || isFloatReg(dataReg) || (dataReg is REG_ZERO) || (dataReg == addrReg));
        assert(isGeneralRegister(addrReg));

        var id = emitNewInstr(attr);
        id.idIns(ins);
        id.idReg1(dataReg);
        id.idReg2(addrReg);
        id.idInsOpt(INS_OPTS_RELOC);
        id.idAddr().iiaAddr = (byte*)disp;
        id.idCodeSize(8);

        dispIns(id);
        appendToCurIG(id);
    }

    public void emitIns_J_cond_la(instruction ins, BasicBlock target, regNumber reg)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "RISC-V one-register conditional-branch recording is not implemented.");
    }

    public static bool isValidSimm12(nint value)
    {
        return (-2048 <= value) && (value < 2048);
    }

    public void emitIns_Mov(emitAttr attr, regNumber dstReg, regNumber srcReg, bool canSkip)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "RISC-V64 register move recording is not ported.");
    }

    public unsafe void emitIns_R_R_Addr(
        instruction ins,
        emitAttr attr,
        regNumber regDest,
        regNumber regAddr,
        void* addr)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "RISC-V64 helper-address load recording is not ported.");
    }
#endif
}
#endif
