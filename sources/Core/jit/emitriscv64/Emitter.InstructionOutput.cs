// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
using System;
using System.Runtime.CompilerServices;
using static RyuJitSharp.GCInfo.GCtype;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp;

public partial class Emitter
{
    private const uint kInstructionOpcodeMask = 0x7f;
    private const uint kInstructionFunct3Mask = 0x7000;
    private const uint kInstructionFunct7Mask = 0xfe000000;
    private const uint kRegisterMask = 0x1f;

    private static bool Is32BitInstruction(ushort code)
    {
        return (code & 0x3) == 0x3;
    }

    private static uint castFloatOrIntegralReg(regNumber reg)
    {
        assert(isGeneralRegisterOrR0(reg) || isFloatReg(reg));

        return (uint)reg & kRegisterMask;
    }

    private unsafe byte emitOutput_Instr(byte* dst, uint code)
    {
        assert(dst != null);
        assert(Is32BitInstruction((ushort)code) || (code >> 16) == 0);

        if (Is32BitInstruction((ushort)code))
        {
            Unsafe.WriteUnaligned(unchecked(dst + writeableOffset), code);
            return sizeof(uint);
        }

        Unsafe.WriteUnaligned(unchecked(dst + writeableOffset), unchecked((ushort)code));
        return sizeof(ushort);
    }

#if DEBUG
    private static void emitOutput_RTypeInstr_SanityCheck(instruction ins, regNumber rd, regNumber rs1, regNumber rs2)
    {
        switch (ins)
        {
            case INS_add or INS_sub or INS_sll or INS_slt or INS_sltu or INS_xor or INS_srl or INS_sra or INS_or or
                INS_and or INS_addw or INS_subw or INS_sllw or INS_srlw or INS_sraw or INS_mul or INS_mulh or
                INS_mulhsu or INS_mulhu or INS_div or INS_divu or INS_rem or INS_remu or INS_mulw or INS_divw or
                INS_divuw or INS_remw or INS_remuw:
            {
                assert(isGeneralRegisterOrR0(rd));
                assert(isGeneralRegisterOrR0(rs1));
                assert(isGeneralRegisterOrR0(rs2));
                break;
            }
            case INS_fsgnj_s or INS_fsgnjn_s or INS_fsgnjx_s or INS_fmin_s or INS_fmax_s or INS_fsgnj_d or
                INS_fsgnjn_d or INS_fsgnjx_d or INS_fmin_d or INS_fmax_d:
            {
                assert(isFloatReg(rd));
                assert(isFloatReg(rs1));
                assert(isFloatReg(rs2));
                break;
            }
            case INS_feq_s or INS_feq_d or INS_flt_d or INS_flt_s or INS_fle_s or INS_fle_d:
            {
                assert(isGeneralRegisterOrR0(rd));
                assert(isFloatReg(rs1));
                assert(isFloatReg(rs2));
                break;
            }
            case INS_fmv_w_x or INS_fmv_d_x:
            {
                assert(isFloatReg(rd));
                assert(isGeneralRegisterOrR0(rs1));
                assert(rs2 == 0);
                break;
            }
            case INS_fmv_x_d or INS_fmv_x_w or INS_fclass_s or INS_fclass_d:
            {
                assert(isGeneralRegisterOrR0(rd));
                assert(isFloatReg(rs1));
                assert(rs2 == 0);
                break;
            }
            default:
            {
                NO_WAY("Illegal ins within emitOutput_RTypeInstr!");
                break;
            }
        }
    }

    private static void emitOutput_ITypeInstr_SanityCheck(
        instruction ins, regNumber rd, regNumber rs1, uint immediate, uint opcode)
    {
        switch (ins)
        {
            case INS_mov or INS_jalr or INS_lb or INS_lh or INS_lw or INS_lbu or INS_lhu or INS_addi or INS_slti or
                INS_sltiu or INS_xori or INS_ori or INS_andi or INS_lwu or INS_ld or INS_addiw or INS_csrrw or
                INS_csrrs or INS_csrrc:
            {
                assert(isGeneralRegisterOrR0(rd));
                assert(isGeneralRegisterOrR0(rs1));
                assert((opcode & kInstructionFunct7Mask) == 0);
                break;
            }
            case INS_flw or INS_fld:
            {
                assert(isFloatReg(rd));
                assert(isGeneralRegisterOrR0(rs1));
                assert((opcode & kInstructionFunct7Mask) == 0);
                break;
            }
            case INS_slli or INS_srli or INS_srai:
            {
                assert(immediate < 64);
                assert(isGeneralRegisterOrR0(rd));
                assert(isGeneralRegisterOrR0(rs1));
                break;
            }
            case INS_slliw or INS_srliw or INS_sraiw:
            {
                assert(immediate < 32);
                assert(isGeneralRegisterOrR0(rd));
                assert(isGeneralRegisterOrR0(rs1));
                break;
            }
            case INS_csrrwi or INS_csrrsi or INS_csrrci:
            {
                assert(isGeneralRegisterOrR0(rd));
                assert((uint)rs1 < 32);
                assert((opcode & kInstructionFunct7Mask) == 0);
                break;
            }
            case INS_fence:
            {
                assert(rd == REG_ZERO);
                assert(rs1 == REG_ZERO);
                var format = (long)(immediate >> 8);
                assert((format == 0) || (format == 0x8));
                assert((opcode & kInstructionFunct7Mask) == 0);
                break;
            }
            default:
            {
                NO_WAY("Illegal ins within emitOutput_ITypeInstr!");
                break;
            }
        }
    }

    private static void emitOutput_STypeInstr_SanityCheck(instruction ins, regNumber rs1, regNumber rs2)
    {
        switch (ins)
        {
            case INS_sb or INS_sh or INS_sw or INS_sd:
            {
                assert(isGeneralRegister(rs1));
                assert(isGeneralRegisterOrR0(rs2));
                break;
            }
            case INS_fsw or INS_fsd:
            {
                assert(isGeneralRegister(rs1));
                assert(isFloatReg(rs2));
                break;
            }
            default:
            {
                NO_WAY("Illegal ins within emitOutput_STypeInstr!");
                break;
            }
        }
    }

    private static void emitOutput_UTypeInstr_SanityCheck(instruction ins, regNumber rd)
    {
        switch (ins)
        {
            case INS_lui or INS_auipc:
            {
                assert(isGeneralRegisterOrR0(rd));
                break;
            }
            default:
            {
                NO_WAY("Illegal ins within emitOutput_UTypeInstr!");
                break;
            }
        }
    }

    private static void emitOutput_BTypeInstr_SanityCheck(instruction ins, regNumber rs1, regNumber rs2)
    {
        switch (ins)
        {
            case INS_beqz or INS_bnez:
            {
                assert((rs1 == REG_ZERO) || (rs2 == REG_ZERO));
                assert(isGeneralRegisterOrR0(rs1));
                assert(isGeneralRegisterOrR0(rs2));
                break;
            }
            case INS_beq or INS_bne or INS_blt or INS_bge or INS_bltu or INS_bgeu:
            {
                assert(isGeneralRegisterOrR0(rs1));
                assert(isGeneralRegisterOrR0(rs2));
                break;
            }
            default:
            {
                NO_WAY("Illegal ins within emitOutput_BTypeInstr!");
                break;
            }
        }
    }

    private static void emitOutput_JTypeInstr_SanityCheck(instruction ins, regNumber rd)
    {
        switch (ins)
        {
            case INS_j:
            {
                assert(rd == REG_ZERO);
                break;
            }
            case INS_jal:
            {
                assert(isGeneralRegisterOrR0(rd));
                break;
            }
            default:
            {
                NO_WAY("Illegal ins within emitOutput_JTypeInstr!");
                break;
            }
        }
    }
#endif

    private unsafe byte emitOutput_RTypeInstr(byte* dst, instruction ins, regNumber rd, regNumber rs1, regNumber rs2)
    {
        var insCode = emitInsCode(ins);
#if DEBUG
        emitOutput_RTypeInstr_SanityCheck(ins, rd, rs1, rs2);
#endif
        var opcode = insCode & kInstructionOpcodeMask;
        var funct3 = (insCode & kInstructionFunct3Mask) >> 12;
        var funct7 = (insCode & kInstructionFunct7Mask) >> 25;
        var code = insEncodeRTypeInstr(
            opcode,
            castFloatOrIntegralReg(rd),
            funct3,
            castFloatOrIntegralReg(rs1),
            castFloatOrIntegralReg(rs2),
            funct7);

        return emitOutput_Instr(dst, code);
    }

    private unsafe byte emitOutput_ITypeInstr(byte* dst, instruction ins, regNumber rd, regNumber rs1, uint imm12)
    {
        var insCode = emitInsCode(ins);
#if DEBUG
        emitOutput_ITypeInstr_SanityCheck(ins, rd, rs1, imm12, insCode);
#endif
        var opcode = insCode & kInstructionOpcodeMask;
        var funct3 = (insCode & kInstructionFunct3Mask) >> 12;
        var funct7 = (insCode & kInstructionFunct7Mask) >> 20;
        var code = insEncodeITypeInstr(
            opcode, castFloatOrIntegralReg(rd), funct3, castFloatOrIntegralReg(rs1), imm12 | funct7);

        return emitOutput_Instr(dst, code);
    }

    private unsafe byte emitOutput_STypeInstr(byte* dst, instruction ins, regNumber rs1, regNumber rs2, uint imm12)
    {
        var insCode = emitInsCode(ins);
#if DEBUG
        emitOutput_STypeInstr_SanityCheck(ins, rs1, rs2);
#endif
        var opcode = insCode & kInstructionOpcodeMask;
        var funct3 = (insCode & kInstructionFunct3Mask) >> 12;
        var code = insEncodeSTypeInstr(
            opcode, funct3, castFloatOrIntegralReg(rs1), castFloatOrIntegralReg(rs2), imm12);

        return emitOutput_Instr(dst, code);
    }

    private unsafe byte emitOutput_UTypeInstr(byte* dst, instruction ins, regNumber rd, uint imm20)
    {
        var insCode = emitInsCode(ins);
#if DEBUG
        emitOutput_UTypeInstr_SanityCheck(ins, rd);
#endif
        return emitOutput_Instr(dst, insEncodeUTypeInstr(insCode, castFloatOrIntegralReg(rd), imm20));
    }

    private unsafe byte emitOutput_BTypeInstr(byte* dst, instruction ins, regNumber rs1, regNumber rs2, uint imm13)
    {
        var insCode = emitInsCode(ins);
#if DEBUG
        emitOutput_BTypeInstr_SanityCheck(ins, rs1, rs2);
#endif
        var opcode = insCode & kInstructionOpcodeMask;
        var funct3 = (insCode & kInstructionFunct3Mask) >> 12;
        var code = insEncodeBTypeInstr(
            opcode, funct3, castFloatOrIntegralReg(rs1), castFloatOrIntegralReg(rs2), imm13);

        return emitOutput_Instr(dst, code);
    }

    private unsafe byte emitOutput_BTypeInstr_InvertComparation(
        byte* dst, instruction ins, regNumber rs1, regNumber rs2, uint imm13)
    {
        var insCode = emitInsCode(ins) ^ 0x1000;
#if DEBUG
        emitOutput_BTypeInstr_SanityCheck(ins, rs1, rs2);
#endif
        var opcode = insCode & kInstructionOpcodeMask;
        var funct3 = (insCode & kInstructionFunct3Mask) >> 12;
        var code = insEncodeBTypeInstr(
            opcode, funct3, castFloatOrIntegralReg(rs1), castFloatOrIntegralReg(rs2), imm13);

        return emitOutput_Instr(dst, code);
    }

    private unsafe byte emitOutput_JTypeInstr(byte* dst, instruction ins, regNumber rd, uint imm21)
    {
        var insCode = emitInsCode(ins);
#if DEBUG
        emitOutput_JTypeInstr_SanityCheck(ins, rd);
#endif
        return emitOutput_Instr(dst, insEncodeJTypeInstr(insCode, castFloatOrIntegralReg(rd), imm21));
    }

    private unsafe interface IEmitPolicy
    {
        void EmitRType(instruction ins, regNumber rd, regNumber rs1, regNumber rs2);
        void EmitIType(instruction ins, regNumber rd, regNumber rs1, uint imm12);
        void EmitSType(instruction ins, regNumber rs1, regNumber rs2, uint imm12);
        void EmitUType(instruction ins, regNumber rd, uint imm20);
        void EmitBType(instruction ins, regNumber rs1, regNumber rs2, uint imm13);
        void EmitBTypeInverted(instruction ins, regNumber rs1, regNumber rs2, uint imm13);
        void EmitJType(instruction ins, regNumber rd, uint imm21);
        void MarkGCRegDead(regNumber reg);
        unsafe void EmitRelocation(instrDesc id, int offset, byte* targetAddr);
    }

    private sealed unsafe class InstructionEncoder : IEmitPolicy
    {
        private readonly Emitter _emitter;
        private byte* _dst;

        public InstructionEncoder(Emitter emitter, byte* dst)
        {
            _emitter = emitter;
            _dst = dst;
        }

        public byte* Destination => _dst;

        public void EmitRType(instruction ins, regNumber rd, regNumber rs1, regNumber rs2)
        {
            _dst += _emitter.emitOutput_RTypeInstr(_dst, ins, rd, rs1, rs2);
        }

        public void EmitIType(instruction ins, regNumber rd, regNumber rs1, uint imm12)
        {
            _dst += _emitter.emitOutput_ITypeInstr(_dst, ins, rd, rs1, imm12);
        }

        public void EmitSType(instruction ins, regNumber rs1, regNumber rs2, uint imm12)
        {
            _dst += _emitter.emitOutput_STypeInstr(_dst, ins, rs1, rs2, imm12);
        }

        public void EmitUType(instruction ins, regNumber rd, uint imm20)
        {
            _dst += _emitter.emitOutput_UTypeInstr(_dst, ins, rd, imm20);
        }

        public void EmitBType(instruction ins, regNumber rs1, regNumber rs2, uint imm13)
        {
            _dst += _emitter.emitOutput_BTypeInstr(_dst, ins, rs1, rs2, imm13);
        }

        public void EmitBTypeInverted(instruction ins, regNumber rs1, regNumber rs2, uint imm13)
        {
            _dst += _emitter.emitOutput_BTypeInstr_InvertComparation(_dst, ins, rs1, rs2, imm13);
        }

        public void EmitJType(instruction ins, regNumber rd, uint imm21)
        {
            _dst += _emitter.emitOutput_JTypeInstr(_dst, ins, rd, imm21);
        }

        public void MarkGCRegDead(regNumber reg)
        {
            _emitter.emitGCregDeadUpd(reg, _dst);
        }

        public unsafe void EmitRelocation(instrDesc id, int offset, byte* targetAddr)
        {
            _emitter.emitRecordRelocation(_dst - offset, targetAddr, CorInfoReloc.RISCV64_CALL_PLT);
        }
    }

    private sealed unsafe class InstructionFormatter : IEmitPolicy
    {
        private readonly Emitter _emitter;
        private readonly instrDesc _id;

        public InstructionFormatter(Emitter emitter, instrDesc id)
        {
            _emitter = emitter;
            _id = id;
        }

        public void EmitRType(instruction ins, regNumber rd, regNumber rs1, regNumber rs2)
        {
            var code = insEncodeRTypeInstr(
                emitInsCode(ins) & kInstructionOpcodeMask,
                castFloatOrIntegralReg(rd),
                (emitInsCode(ins) & kInstructionFunct3Mask) >> 12,
                castFloatOrIntegralReg(rs1),
                castFloatOrIntegralReg(rs2),
                (emitInsCode(ins) & kInstructionFunct7Mask) >> 25);
            _emitter.emitDispInsNameRiscV64(code, _id);
        }

        public void EmitIType(instruction ins, regNumber rd, regNumber rs1, uint imm12)
        {
            var baseOpcode = emitInsCode(ins);
            var code = insEncodeITypeInstr(
                baseOpcode & kInstructionOpcodeMask,
                castFloatOrIntegralReg(rd),
                (baseOpcode & kInstructionFunct3Mask) >> 12,
                castFloatOrIntegralReg(rs1),
                imm12);
            _emitter.emitDispInsNameRiscV64(code, _id);
        }

        public void EmitSType(instruction ins, regNumber rs1, regNumber rs2, uint imm12)
        {
            var baseOpcode = emitInsCode(ins);
            var code = insEncodeSTypeInstr(
                baseOpcode & kInstructionOpcodeMask,
                (baseOpcode & kInstructionFunct3Mask) >> 12,
                castFloatOrIntegralReg(rs1),
                castFloatOrIntegralReg(rs2),
                imm12);
            _emitter.emitDispInsNameRiscV64(code, _id);
        }

        public void EmitUType(instruction ins, regNumber rd, uint imm20)
        {
            var code = insEncodeUTypeInstr(
                emitInsCode(ins) & kInstructionOpcodeMask,
                castFloatOrIntegralReg(rd),
                imm20);
            _emitter.emitDispInsNameRiscV64(code, _id);
        }

        public void EmitBType(instruction ins, regNumber rs1, regNumber rs2, uint imm13)
        {
            var baseOpcode = emitInsCode(ins);
            var code = insEncodeBTypeInstr(
                baseOpcode & kInstructionOpcodeMask,
                (baseOpcode & kInstructionFunct3Mask) >> 12,
                castFloatOrIntegralReg(rs1),
                castFloatOrIntegralReg(rs2),
                imm13);
            _emitter.emitDispInsNameRiscV64(code, _id);
        }

        public void EmitBTypeInverted(instruction ins, regNumber rs1, regNumber rs2, uint imm13)
        {
            var baseOpcode = emitInsCode(ins) ^ 0x1000;
            var code = insEncodeBTypeInstr(
                baseOpcode & kInstructionOpcodeMask,
                (baseOpcode & kInstructionFunct3Mask) >> 12,
                castFloatOrIntegralReg(rs1),
                castFloatOrIntegralReg(rs2),
                imm13);
            _emitter.emitDispInsNameRiscV64(code, _id);
        }

        public void EmitJType(instruction ins, regNumber rd, uint imm21)
        {
            var code = insEncodeJTypeInstr(
                emitInsCode(ins) & kInstructionOpcodeMask,
                castFloatOrIntegralReg(rd),
                imm21);
            _emitter.emitDispInsNameRiscV64(code, _id);
        }

        public void MarkGCRegDead(regNumber reg)
        {
        }

        public unsafe void EmitRelocation(instrDesc id, int offset, byte* targetAddr)
        {
        }
    }

    private void EmitLogic_OptsReloc(IEmitPolicy policy, instrDesc id)
    {
        var ins = id.idIns();
        var dataReg = id.idReg1();
        var addrReg = id.idReg2();

        assert((ins == INS_addi) || emitInsIsLoadOrStore(ins));
        policy.EmitUType(INS_auipc, addrReg, 0);
        policy.MarkGCRegDead(addrReg);
        if (emitInsIsStore(ins))
        {
            policy.EmitSType(ins, addrReg, dataReg, 0);
        }
        else
        {
            policy.EmitIType(ins, dataReg, addrReg, 0);
        }
    }

    private void EmitLogic_OptsRc(IEmitPolicy policy, instrDesc id, nint immediate)
    {
        var reg1 = id.idReg1();
        var tempReg = isFloatReg(reg1) ? codeGen.rsGetRsvdReg() : reg1;
        policy.EmitUType(INS_auipc, tempReg, UpperNBitsOfWordSignExtend(immediate, 20));
        policy.EmitIType(id.idIns(), reg1, tempReg, LowerNBitsOfWord(immediate, 12));
    }

    private static void EmitLogic_OptsRl(IEmitPolicy policy, instrDesc id, nint immediate)
    {
        var reg1 = id.idReg1();
        policy.EmitUType(INS_auipc, reg1, UpperNBitsOfWordSignExtend(immediate, 20));
        policy.EmitIType(INS_addi, reg1, reg1, LowerNBitsOfWord(immediate, 12));
    }

    private void EmitLogic_OptsJump(IEmitPolicy policy, instrDescJmp jmp, nint immediate)
    {
        var ins = jmp.idIns();
        if (jmp.idjShort)
        {
            assert(jmp.idCodeSize() == sizeof(uint));
            if (emitIsUncondJump(jmp))
            {
                policy.EmitJType(ins, jmp.idReg1(), TrimSignedToImm21(immediate));
            }
            else
            {
                policy.EmitBType(ins, jmp.idReg1(), jmp.idReg2(), TrimSignedToImm13(immediate));
            }
        }
        else if (emitIsUncondJump(jmp))
        {
            assert(jmp.idCodeSize() == 2 * sizeof(uint));
            assert(isValidSimm32(immediate));
            var linkReg = jmp.idReg1();
            var tempReg = linkReg == REG_ZERO ? codeGen.rsGetRsvdReg() : linkReg;
            policy.EmitUType(INS_auipc, tempReg, UpperNBitsOfWordSignExtend(immediate, 20));
            policy.EmitIType(INS_jalr, linkReg, tempReg, LowerNBitsOfWord(immediate, 12));
        }
        else
        {
            assert(!jmp.idInsIs(INS_beqz, INS_bnez) || (jmp.idReg2() == REG_ZERO));
            policy.EmitBTypeInverted(ins, jmp.idReg1(), jmp.idReg2(), jmp.idCodeSize());
            immediate -= sizeof(uint);
            if (jmp.idCodeSize() == 2 * sizeof(uint))
            {
                policy.EmitJType(INS_jal, REG_ZERO, TrimSignedToImm21(immediate));
            }
            else
            {
                assert(jmp.idCodeSize() == 3 * sizeof(uint));
                assert(isValidSimm32(immediate));
                var tempReg = codeGen.rsGetRsvdReg();
                policy.EmitUType(INS_auipc, tempReg, UpperNBitsOfWordSignExtend(immediate, 20));
                policy.EmitIType(INS_jalr, REG_ZERO, tempReg, LowerNBitsOfWord(immediate, 12));
            }
        }
    }

    private unsafe void EmitLogic_OptsC(IEmitPolicy policy, instrDesc id)
    {
        if (id.idIsCallRegPtr())
        {
            var offset = id.idSmallCns();
            policy.EmitIType(INS_jalr, id.idReg4(), id.idReg3(), TrimSignedToImm12(offset));
            return;
        }

        var address = unchecked((nuint)id.idAddr().iiaAddr);
        var linkReg = (regNumber)(address & 1);
        assert((linkReg == REG_ZERO) || (linkReg == REG_RA));
        address -= (uint)linkReg;
        assert((address & 1) == 0);
        var tempReg = linkReg == REG_ZERO ? REG_DEFAULT_HELPER_CALL_TARGET : REG_RA;
        policy.EmitUType(INS_auipc, tempReg, 0);
        policy.MarkGCRegDead(tempReg);
        policy.EmitIType(INS_jalr, linkReg, tempReg, 0);
        assert(id.idIsDspReloc());
        unsafe
        {
            policy.EmitRelocation(id, 2 * sizeof(uint), (byte*)address);
        }
    }

    private void EmitLogic_OptsI(IEmitPolicy policy, instrDescLoadImm idli)
    {
        var instructions = idli.ins;
        var values = idli.values;
        var reg = idli.idReg1();
        assert((reg != REG_NA) && (reg != REG_R0));

        var numberOfInstructions = GetLoadImmediateNumberOfInstructions(idli);
        for (var i = 0u; i < numberOfInstructions; i++)
        {
            if ((i == 0) && (instructions[i] == INS_lui))
            {
                assert(isValidSimm20(values[i]));
                policy.EmitUType(instructions[i], reg, unchecked((uint)values[i]) & 0xFFFFF);
            }
            else if ((i == 0) && (instructions[i] is INS_addiw or INS_addi))
            {
                assert(isValidSimm12(values[i]) ||
                       ((instructions[i] == INS_addiw) && isValidUimm12(values[i])));
                policy.EmitIType(instructions[i], reg, REG_R0, unchecked((uint)values[i]) & 0xFFF);
            }
            else if (i == 0)
            {
                assert(false, "First instruction must be lui / addiw / addi");
            }
            else if (instructions[i] is INS_addi or INS_addiw or INS_slli or INS_srli)
            {
                assert(isValidSimm12(values[i]) ||
                       ((instructions[i] == INS_addiw) && isValidUimm12(values[i])));
                policy.EmitIType(instructions[i], reg, reg, unchecked((uint)values[i]) & 0xFFF);
            }
            else
            {
                assert(false, "Remaining instructions must be addi / addiw / slli / srli");
            }
        }
    }

    private static uint GetLoadImmediateNumberOfInstructions(instrDescLoadImm idli)
    {
        return idli.idCodeSize() / sizeof(uint);
    }

    private unsafe byte* emitOutputInstr_OptsReloc(byte* dst, instrDesc id, out instruction ins)
    {
        var dstBase = dst;
        ins = id.idIns();
        var encoder = new InstructionEncoder(this, dst);
        EmitLogic_OptsReloc(encoder, id);
        var relocationType = emitInsIsStore(ins) ? CorInfoReloc.RISCV64_PCREL_S : CorInfoReloc.RISCV64_PCREL_I;
        emitRecordRelocation(dstBase, id.idAddr().iiaAddr, relocationType);

        return encoder.Destination;
    }

    private unsafe byte* emitOutputInstr_OptsRc(byte* dst, instrDesc id, out instruction ins)
    {
        assert(id.idAddr().iiaIsJitDataOffset());
        assert(id.idGCref() == GCT_NONE);

        var offset = id.idAddr().iiaGetJitDataOffset();
        assert(offset >= 0);
        assert((nuint)offset < emitDataSize());

        var dstBase = dst;
        ins = id.idIns();
        var reg1 = id.idReg1();
        assert(reg1 != REG_ZERO);
        assert(id.idCodeSize() == 2 * sizeof(uint));
        var immediate = id.idIsReloc() ? (nint)0 : (nint)(emitDataOffsetToPtr((uint)offset) - dst);
        if (!id.idIsReloc())
        {
            assert((immediate > 0) && ((immediate & 1) == 0));
            assert(isValidSimm32(immediate));
        }

        var tempReg = isFloatReg(reg1) ? codeGen.rsGetRsvdReg() : reg1;
        dst += emitOutput_UTypeInstr(
            dst, INS_auipc, tempReg, UpperNBitsOfWordSignExtend(immediate, 20));
        dst += emitOutput_ITypeInstr(dst, ins, reg1, tempReg, LowerNBitsOfWord(immediate, 12));
        if (id.idIsReloc())
        {
            emitRecordRelocation(dstBase, emitDataOffsetToPtr((uint)offset), CorInfoReloc.RISCV64_PCREL_I);
        }

        return dst;
    }

    private unsafe byte* emitOutputInstr_OptsRl(byte* dst, instrDesc id, out instruction ins)
    {
        if (!id.idIsBound())
        {
            var targetInsGroup = id.RiscVIGlabel;
            if (targetInsGroup is null)
            {
                var targetBlock = id.RiscVBBlabel
                    ?? throw new InvalidOperationException("An unbound RISC-V label requires a basic block.");
                targetInsGroup = emitCodeGetCookie(targetBlock)
                    ?? throw new InvalidOperationException("The RISC-V basic block has no instruction-group cookie.");
                id.RiscVIGlabel = targetInsGroup;
            }
            id.idSetIsBound();
        }

        var targetGroup = id.RiscVIGlabel
            ?? throw new InvalidOperationException("A bound RISC-V label requires an instruction group.");
        var immediate = (nint)((emitCodeBlock - dst) + targetGroup.igOffs);
        ins = INS_auipc;
        assert((immediate & 1) == 0);
        assert(isValidSimm32(immediate));

        var encoder = new InstructionEncoder(this, dst);
        EmitLogic_OptsRl(encoder, id, immediate);

        return encoder.Destination;
    }

    private unsafe byte* emitOutputInstr_OptsJump(byte* dst, instrDescJmp jmp, insGroup ig, out instruction ins)
    {
        var immediate = emitOutputInstrJumpDistance(dst, ig, jmp);
        assert((immediate & 1) == 0);
        assert(emitIsUncondJump(jmp) || emitIsCmpJump(jmp));
        ins = jmp.idIns();

        var encoder = new InstructionEncoder(this, dst);
        EmitLogic_OptsJump(encoder, jmp, immediate);

        return encoder.Destination;
    }

    private unsafe byte* emitOutputInstr_OptsC(byte* dst, instrDesc id, out nuint descriptorSize)
    {
        descriptorSize = id.idIsLargeCall()
            ? (nuint)instrDescCGCA.NativeSize
            : (nuint)INSTR_DESC_SIZE;
        assert(id.idIsLargeCall() || (!id.idIsLargeDsp() && !id.idIsLargeCns()));
        var codeSize = emitOutputCallRiscV64(dst, id);

        return dst + codeSize;
    }

    private unsafe byte* emitOutputInstr_OptsI(byte* dst, instrDesc id, out instruction ins)
    {
        assert(id.idInsOpt() == INS_OPTS_I);
        var idli = (instrDescLoadImm)id;
        var numberOfInstructions = GetLoadImmediateNumberOfInstructions(idli);
        ins = idli.ins[numberOfInstructions - 1];
        var encoder = new InstructionEncoder(this, dst);
        EmitLogic_OptsI(encoder, idli);

        return encoder.Destination;
    }

    private unsafe nint emitOutputInstrJumpDistance(byte* src, insGroup ig, instrDescJmp jmp)
    {
        var srcOffs = emitCurCodeOffs(src);
        var srcAddr = emitOffsetToPtr(srcOffs);
        var dstOffs = jmp.idjTargetIG?.igOffs
            ?? throw new InvalidOperationException("A RISC-V jump requires a target instruction group.");
        var dstAddr = emitOffsetToPtr(dstOffs);
        var distance = unchecked((nint)(dstAddr - srcAddr));

        if (dstOffs > srcOffs)
        {
            emitFwdJumps = true;
            if (!emitJumpCrossHotColdBoundary(srcOffs, dstOffs))
            {
                distance -= emitOffsAdj;
                dstOffs -= (uint)emitOffsAdj;
            }

            jmp.idjOffs = dstOffs;
            if (jmp.idjOffs != dstOffs)
            {
                throw new FatalJitException(CORJIT_IMPLLIMITATION, "Method is too large.");
            }
        }

        return distance;
    }

    private unsafe uint emitOutputCallRiscV64(byte* dst, instrDesc id)
    {
        regMaskTP gcrefRegs;
        regMaskTP byrefRegs;
        var compiler = _compiler ?? throw new InvalidOperationException("RISC-V call output requires an active compiler.");
        VARSET_TP gcVars = [];

        if (id.idIsLargeCall())
        {
            var idCall = (instrDescCGCA)id;
            gcrefRegs = idCall.idcGcrefRegs;
            byrefRegs = idCall.idcByrefRegs;
            VarSetOps.Assign(compiler, ref gcVars, idCall.idcGCvars);
        }
        else
        {
            assert(!id.idIsLargeDsp());
            assert(!id.idIsLargeCns());
            gcrefRegs = new regMaskTP((regMask)emitDecodeCallGCregs(id));
            byrefRegs = default;
            VarSetOps.AssignNoCopy(compiler, ref gcVars, VarSetOps.MakeEmpty(compiler));
        }

        emitUpdateLiveGCvars(gcVars, dst);
#if DEBUG
        if (compiler.opts.disasmWithGC)
        {
            emitDispGCVarDelta();
        }
#endif

        assert(id.idIns() == INS_jalr);
        var originalDst = dst;
        var encoder = new InstructionEncoder(this, dst);
        EmitLogic_OptsC(encoder, id);
        dst = encoder.Destination;

        if (id.idGCref() == GCT_GCREF)
        {
            gcrefRegs |= new regMaskTP(SRBM_INTRET);
        }
        else if (id.idGCref() == GCT_BYREF)
        {
            byrefRegs |= new regMaskTP(SRBM_INTRET);
        }

        if (id.idIsLargeCall())
        {
            var idCall = (instrDescCGCA)id;
            if (idCall.idSecondGCref() == GCT_GCREF)
            {
                gcrefRegs |= new regMaskTP(SRBM_INTRET_1);
            }
            else if (idCall.idSecondGCref() == GCT_BYREF)
            {
                byrefRegs |= new regMaskTP(SRBM_INTRET_1);
            }
            if (idCall.hasAsyncContinuationRet())
            {
                gcrefRegs |= new regMaskTP(SRBM_ASYNC_CONTINUATION_RET);
            }
        }

        if (gcrefRegs != new regMaskTP(emitThisGCrefRegs))
        {
            emitUpdateLiveGCregs(GCT_GCREF, gcrefRegs, dst);
            emitThisGCrefRegs = (regMask)gcrefRegs;
        }
        if (byrefRegs != new regMaskTP(emitThisByrefRegs))
        {
            emitUpdateLiveGCregs(GCT_BYREF, byrefRegs, dst);
            emitThisByrefRegs = (regMask)byrefRegs;
        }

        if (!id.idIsNoGC())
        {
            emitStackPop(dst, true, sizeof(uint), 0);
            if (!emitFullGCinfo)
            {
                emitRecordGCcall(dst, sizeof(uint));
            }
        }

        assert(dst > originalDst);
        return unchecked((uint)(dst - originalDst));
    }

    private unsafe nuint emitOutputInstrRiscV64(insGroup ig, instrDesc id, byte** dp)
    {
        var compiler = _compiler ?? throw new InvalidOperationException("RISC-V instruction output requires an active compiler.");
        assert(emitCurIG is not null);
        var dst = *dp;
        var dstAfterOne = dst + sizeof(uint);
        var originalDst = dst;
        var ins = id.idIns();
        nuint descriptorSize;

        switch (id.idInsOpt())
        {
            case INS_OPTS_RELOC:
            {
                dst = emitOutputInstr_OptsReloc(dst, id, out ins);
                descriptorSize = (nuint)INSTR_DESC_SIZE;
                break;
            }
            case INS_OPTS_RC:
            {
                dst = emitOutputInstr_OptsRc(dst, id, out ins);
                descriptorSize = (nuint)INSTR_DESC_SIZE;
                break;
            }
            case INS_OPTS_RL:
            {
                dst = emitOutputInstr_OptsRl(dst, id, out ins);
                descriptorSize = (nuint)INSTR_DESC_SIZE;
                break;
            }
            case INS_OPTS_JUMP:
            {
                dst = emitOutputInstr_OptsJump(dst, (instrDescJmp)id, ig, out ins);
                descriptorSize = (nuint)DescriptorSizes.Jump;
                break;
            }
            case INS_OPTS_C:
            {
                dst = emitOutputInstr_OptsC(dst, id, out descriptorSize);
                ins = INS_nop;
                break;
            }
            case INS_OPTS_I:
            {
                dst = emitOutputInstr_OptsI(dst, id, out ins);
                descriptorSize = (nuint)DescriptorSizes.RiscVLoadImmediate;
                break;
            }
            default:
            {
                dst += emitOutput_Instr(dst, id.idAddr().iiaGetInstrEncode());
                descriptorSize = (nuint)INSTR_DESC_SIZE;
                break;
            }
        }

        if (emitInsMayWriteToGCReg(ins))
        {
            if (id.idGCref() != GCT_NONE)
            {
                emitGCregLiveUpd(id.idGCref(), id.idReg1(), dst);
            }
            else
            {
                emitGCregDeadUpd(id.idReg1(), dstAfterOne);
            }
        }

        if (emitInsWritesToLclVarStackLoc(id))
        {
            var varNum = id.idAddr().iiaLclVar.lvaVarNum();
            var lclOffset = id.idAddr().iiaLclVar.lvaOffset();
            var offset = lclOffset - (lclOffset % TARGET_POINTER_SIZE);
            var address = compiler.lvaFrameAddress(varNum, out _);
            if (id.idGCref() != GCT_NONE)
            {
#if DEBUG
                emitGCvarLiveUpd(unchecked(address + (int)offset), varNum, id.idGCref(), dst, unchecked((uint)varNum));
#else
                emitGCvarLiveUpd(unchecked(address + (int)offset), varNum, id.idGCref(), dst);
#endif
            }
            else
            {
                var type = varNum >= 0
                    ? compiler.lvaGetDesc(varNum).Type
                    : (codeGen.RegSet.tmpFindNum(varNum)
                        ?? throw new InvalidOperationException("The RISC-V spill temporary must exist.")).tdTempType;
                if (type is TYP_REF or TYP_BYREF)
                {
#if DEBUG
                    emitGCvarDeadUpd(unchecked(address + (int)offset), dstAfterOne, unchecked((uint)varNum));
#else
                    emitGCvarDeadUpd(unchecked(address + (int)offset), dstAfterOne);
#endif
                }
            }
        }

#if DEBUG
        if (compiler.opts.disAsm || compiler.verbose)
        {
#if DUMP_GC_TABLES
            var displayOffsets = compiler.opts.dspGCtbls;
#else
            var displayOffsets = !compiler.opts.disDiffable;
#endif
            emitDispInsRiscV64(id, false, displayOffsets, true, emitCurCodeOffs(originalDst), *dp,
                (nuint)(dst - originalDst), ig);
        }

        var debugInfo = id.idDebugOnlyInfo()
            ?? throw new InvalidOperationException("RISC-V instruction output requires debug descriptor info.");
        if (compiler.compDebugBreak && (JitConfig.JitBreakEmitOutputInstr == debugInfo.idNum))
        {
            assert(false, "JitBreakEmitOutputInstr reached");
        }
        if (compiler.opts.disasmWithGC)
        {
            emitDispGCInfoDelta();
        }
#else
        if (compiler.opts.disAsm)
        {
            emitDispInsRiscV64(id, false, false, true, emitCurCodeOffs(originalDst), *dp,
                (nuint)(dst - originalDst), ig);
        }
#endif

        assert(*dp != dst);
        *dp = dst;

        return descriptorSize;
    }
}
#endif
