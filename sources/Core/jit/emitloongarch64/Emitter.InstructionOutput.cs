// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64
using System.Runtime.CompilerServices;
using static RyuJitSharp.GCInfo.GCtype;
using static RyuJitSharp.Globals;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp;

public partial class Emitter
{
    private unsafe nuint emitOutputInstrLoongArch64(insGroup ig, instrDesc id, byte** dp)
    {
        var compiler = _compiler
            ?? throw new FatalJitException("LoongArch64 instruction output requires an active compiler.");
        var dst = *dp;
        var dstRW = unchecked(dst + writeableOffset);
        var dstRW2 = unchecked(dstRW + sizeof(uint));
        var ins = id.idIns();
        nuint descriptorSize;

        switch (id.idInsOpt())
        {
            case INS_OPTS_RELOC:
            {
                var reg = id.idReg1();
                emitOutputLoongArch64Instruction(dst, 0x1a000000 | unchecked((uint)reg));
                var secondInstruction = id.idIsCnsReloc() ? INS_addi_d : INS_ld_d;
                var code = emitInsCode(secondInstruction);
                code |= unchecked((uint)reg);
                code |= unchecked((uint)reg) << 5;
                emitOutputLoongArch64Instruction(dst + sizeof(uint), code);
                emitRecordRelocation(dst, id.idAddr().iiaAddr, CorInfoReloc.LOONGARCH64_PC);
                dstRW += 2 * sizeof(uint);
                dstRW2 += sizeof(uint);
                descriptorSize = (nuint)INSTR_DESC_SIZE;
                ins = secondInstruction;
                break;
            }
            case INS_OPTS_I:
            {
                EmitOutputLoongArch64Immediate(dst, id);
                dstRW += id.idCodeSize();
                descriptorSize = (nuint)INSTR_DESC_SIZE;
                ins = INS_ori;
                break;
            }
            case INS_OPTS_RC:
            {
                dstRW += EmitOutputLoongArch64ConstantAddress(dst, id, out ins);
                descriptorSize = (nuint)INSTR_DESC_SIZE;
                break;
            }
            case INS_OPTS_RL:
            {
                dstRW += EmitOutputLoongArch64LabelAddress(dst, id, out ins);
                descriptorSize = (nuint)INSTR_DESC_SIZE;
                break;
            }
            case INS_OPTS_JIRL:
            {
                dstRW += EmitOutputLoongArch64LongConditionalJump(dst, (instrDescJmp)id, out ins);
                descriptorSize = (nuint)DescriptorSizes.Jump;
                break;
            }
            case INS_OPTS_J_cond:
            case INS_OPTS_J:
            {
                var jump = (instrDescJmp)id;
                var immediate = jump.idAddr().iiaGetJmpOffset();
                emitOutputLoongArch64Instruction(dst, EncodeLoongArch64Branch(ins, id, immediate));
                dstRW += sizeof(uint);
                descriptorSize = (nuint)DescriptorSizes.Jump;
                break;
            }
            case INS_OPTS_C:
            {
                descriptorSize = id.idIsLargeCall()
                    ? (nuint)instrDescCGCA.NativeSize
                    : (nuint)INSTR_DESC_SIZE;
                assert(id.idIsLargeCall() || (!id.idIsLargeDsp() && !id.idIsLargeCns()));
                dstRW += emitOutputCallLoongArch64(dst, id);
                dstRW2 = dstRW;
                ins = INS_nop;
                break;
            }
            default:
            {
                emitOutputLoongArch64Instruction(dst, id.idAddr().iiaGetInstrEncode());
                dstRW += sizeof(uint);
                descriptorSize = unchecked((nuint)emitSizeOfInsDsc(id));
                break;
            }
        }

        if (emitInsMayWriteToGCReg(ins))
        {
            if (id.idGCref() != GCT_NONE)
            {
                emitGCregLiveUpd(id.idGCref(), id.idReg1(), dstRW2 - writeableOffset);
            }
            else
            {
                emitGCregDeadUpd(id.idReg1(), dstRW2 - writeableOffset);
            }
        }

        if (emitInsWritesToLclVarStackLoc(id))
        {
            var varNum = id.idAddr().iiaLclVar.lvaVarNum();
            var localOffset = id.idAddr().iiaLclVar.lvaOffset();
            var offset = localOffset - (localOffset % TARGET_POINTER_SIZE);
            var address = compiler.lvaFrameAddress(varNum, out _);
            if (id.idGCref() != GCT_NONE)
            {
#if DEBUG
                emitGCvarLiveUpd(unchecked(address + (int)offset), varNum, id.idGCref(),
                    dstRW2 - writeableOffset, unchecked((uint)varNum));
#else
                emitGCvarLiveUpd(unchecked(address + (int)offset), varNum, id.idGCref(), dstRW2 - writeableOffset);
#endif
            }
            else
            {
                var type = varNum >= 0
                    ? compiler.lvaGetDesc(varNum).Type
                    : (codeGen.RegSet.tmpFindNum(varNum)
                        ?? throw new FatalJitException("The LoongArch64 spill temporary must exist.")).tdTempType;
                if (type is TYP_REF or TYP_BYREF)
                {
#if DEBUG
                    emitGCvarDeadUpd(unchecked(address + (int)offset), dstRW2 - writeableOffset,
                        unchecked((uint)varNum));
#else
                    emitGCvarDeadUpd(unchecked(address + (int)offset), dstRW2 - writeableOffset);
#endif
                }
            }
        }

#if DEBUG
        if (compiler.opts.disAsm || compiler.verbose)
#else
        if (compiler.opts.disAsm)
#endif
        {
            var disassemblyAddress = unchecked(*dp + writeableOffset);
            while (disassemblyAddress != dstRW)
            {
                emitDisInsNameLoongArch64(
                    Unsafe.ReadUnaligned<uint>(disassemblyAddress),
                    disassemblyAddress,
                    id);
                disassemblyAddress += sizeof(uint);
            }
        }

#if DEBUG
        if (compiler.compDebugBreak)
        {
            var debugInfo = id.idDebugOnlyInfo()
                ?? throw new FatalJitException("LoongArch64 instruction output requires debug descriptor information.");
            if (JitConfig.JitBreakEmitOutputInstr == debugInfo.idNum)
            {
                assert(false, "JitBreakEmitOutputInstr reached");
            }
        }

        if (compiler.opts.disasmWithGC)
        {
            emitDispGCInfoDelta();
        }
#endif

        assert(dstRW != dst + writeableOffset);
        *dp = dstRW - writeableOffset;
        return descriptorSize;
    }

    private unsafe void EmitOutputLoongArch64Immediate(byte* dst, instrDesc id)
    {
        var immediate = unchecked((nint)id.idAddr().iiaAddr);
        var reg = id.idReg1();
        var codeSize = id.idCodeSize();
        uint code;

        switch (codeSize)
        {
            case 8:
            {
                if (id.idReg2() != REG_R0)
                {
                    code = emitInsCode(INS_addi_d);
                    code |= unchecked((uint)reg);
                    code |= (unchecked((uint)REG_R0) << 5);
                    code |= 0xfff << 10;
                    emitOutputLoongArch64Instruction(dst, code);
                    code = emitInsCode(INS_srli_d);
                    var shift = immediate == nint.MaxValue ? 1u : 32u;
                    code |= unchecked((uint)reg);
                    code |= unchecked((uint)reg) << 5;
                    code |= shift << 10;
                    emitOutputLoongArch64Instruction(dst + sizeof(uint), code);
                }
                else
                {
                    EmitOutputLoongArch64LuiOri(dst, reg, immediate);
                }
                break;
            }
            case 12:
            {
                EmitOutputLoongArch64LuiOri(dst, reg, immediate);
                code = emitInsCode(INS_lu32i_d);
                code |= unchecked((uint)reg);
                code |= (unchecked((uint)(immediate >> 32)) & 0xfffff) << 5;
                emitOutputLoongArch64Instruction(dst + 2 * sizeof(uint), code);
                break;
            }
            case 16:
            {
                EmitOutputLoongArch64LuiOri(dst, reg, immediate);
                code = emitInsCode(INS_lu32i_d);
                code |= unchecked((uint)reg);
                code |= (unchecked((uint)(immediate >> 32)) & 0xfffff) << 5;
                emitOutputLoongArch64Instruction(dst + 2 * sizeof(uint), code);
                code = emitInsCode(INS_lu52i_d);
                code |= unchecked((uint)reg);
                code |= unchecked((uint)reg) << 5;
                code |= (unchecked((uint)(immediate >> 52)) & 0xfff) << 10;
                emitOutputLoongArch64Instruction(dst + 3 * sizeof(uint), code);
                break;
            }
            default:
            {
                unreached();
                break;
            }
        }
    }

    private unsafe void EmitOutputLoongArch64LuiOri(byte* dst, regNumber reg, nint immediate)
    {
        var code = emitInsCode(INS_lu12i_w);
        code |= unchecked((uint)reg);
        code |= (unchecked((uint)(immediate >> 12)) & 0xfffff) << 5;
        emitOutputLoongArch64Instruction(dst, code);
        code = emitInsCode(INS_ori);
        code |= unchecked((uint)reg);
        code |= unchecked((uint)reg) << 5;
        code |= (unchecked((uint)immediate) & 0xfff) << 10;
        emitOutputLoongArch64Instruction(dst + sizeof(uint), code);
    }

    private unsafe uint EmitOutputLoongArch64ConstantAddress(byte* dst, instrDesc id, out instruction ins)
    {
        assert(id.idAddr().iiaIsJitDataOffset());
        assert(id.idGCref() == GCT_NONE);
        var dataOffset = id.idAddr().iiaGetJitDataOffset();
        assert(dataOffset >= 0);
        assert(unchecked((uint)dataOffset) < emitDataSize());

        var immediate = emitGetInsSC(id);
        assert((immediate >= 0) && (immediate < 0x4000));
        var dataAddress = emitDataOffsetToPtr(unchecked((uint)(dataOffset + (int)immediate)));
        var reg = id.idReg1();
        ins = id.idIns();

        if (ins == INS_b)
        {
            emitOutputLoongArch64Instruction(dst, 0x1a000000 | unchecked((uint)reg));
            ins = INS_addi_d;
            emitOutputLoongArch64Instruction(dst + sizeof(uint), 0x02c00000 |
                unchecked((uint)reg) | (unchecked((uint)reg) << 5));
            emitRecordRelocation(dst, dataAddress, CorInfoReloc.LOONGARCH64_PC);
            return 2 * sizeof(uint);
        }

        if (id.idIsReloc())
        {
            var relative = unchecked((nint)dataAddress - (nint)dst);
            assert(relative > 0 && (relative & 3) == 0);
            var adjustment = relative & 0x800;
            var rounded = unchecked(relative + adjustment);
            assert(isValidSimm20(rounded >> 12));
            var lowImmediate = (rounded & 0x7ff) - adjustment;
            var code = 0x1c000000u | 21u | ((unchecked((uint)rounded) & 0xfffff000) >> 7);
            emitOutputLoongArch64Instruction(dst, code);
            if (ins == INS_bl)
            {
                ins = INS_addi_d;
                code = 0x02c00000u | (21u << 5) | unchecked((uint)reg) |
                    ((unchecked((uint)lowImmediate) & 0xfff) << 10);
            }
            else
            {
                code = emitInsCode(ins);
                code |= unchecked((uint)reg) & 0x1f;
                code |= unchecked((uint)REG_R21) << 5;
                code |= (unchecked((uint)lowImmediate) & 0xfff) << 10;
            }
            emitOutputLoongArch64Instruction(dst + sizeof(uint), code);
            return 2 * sizeof(uint);
        }

        var address = unchecked((nint)dataAddress);
        if (ins == INS_bl)
        {
            assert(unchecked((nuint)(address >> 32)) <= 0x7ffff);
            var code = emitInsCode(INS_lu12i_w);
            code |= unchecked((uint)REG_R21);
            code |= (unchecked((uint)(address >> 12)) & 0xfffff) << 5;
            emitOutputLoongArch64Instruction(dst, code);
            code = emitInsCode(INS_ori);
            code |= unchecked((uint)reg);
            code |= unchecked((uint)REG_R21) << 5;
            code |= (unchecked((uint)address) & 0xfff) << 10;
            emitOutputLoongArch64Instruction(dst + sizeof(uint), code);
            code = emitInsCode(INS_lu32i_d);
            code |= unchecked((uint)reg);
            code |= (unchecked((uint)(address >> 32)) & 0x7ffff) << 5;
            emitOutputLoongArch64Instruction(dst + 2 * sizeof(uint), code);
            ins = INS_lu32i_d;
        }
        else
        {
            var adjustment = address & 0x800;
            var rounded = unchecked(address + adjustment);
            var lowImmediate = (rounded & 0x7ff) - adjustment;
            assert(unchecked((nuint)(rounded >> 32)) <= 0x7ffff);
            var code = emitInsCode(INS_lu12i_w);
            code |= unchecked((uint)REG_R21);
            code |= (unchecked((uint)(rounded >> 12)) & 0xfffff) << 5;
            emitOutputLoongArch64Instruction(dst, code);
            code = emitInsCode(INS_lu32i_d);
            code |= unchecked((uint)REG_R21);
            code |= (unchecked((uint)(rounded >> 32)) & 0x7ffff) << 5;
            emitOutputLoongArch64Instruction(dst + sizeof(uint), code);
            code = emitInsCode(ins);
            code |= unchecked((uint)reg) & 0x1f;
            code |= unchecked((uint)REG_R21) << 5;
            code |= (unchecked((uint)lowImmediate) & 0xfff) << 10;
            emitOutputLoongArch64Instruction(dst + 2 * sizeof(uint), code);
        }

        return 3 * sizeof(uint);
    }

    private unsafe uint EmitOutputLoongArch64LabelAddress(
        byte* dst,
        instrDesc id,
        out instruction ins)
    {
        if (!id.idIsBound())
        {
            var targetGroup = id.LoongArchIGlabel;
            if (targetGroup is null)
            {
                var targetBlock = id.LoongArchBBlabel
                    ?? throw new FatalJitException("An unbound LoongArch64 label requires a basic-block target.");
                targetGroup = emitCodeGetCookie(targetBlock)
                    ?? throw new FatalJitException("A LoongArch64 label has no instruction-group cookie.");
                id.LoongArchIGlabel = targetGroup;
            }
            id.idSetIsBound();
        }

        var group = id.LoongArchIGlabel
            ?? throw new FatalJitException("A bound LoongArch64 label requires an instruction group.");
        var reg = id.idReg1();
        assert(isGeneralRegister(reg));
        ins = id.idIns();

        if (id.idIsReloc())
        {
            var immediate = unchecked((nint)(emitCodeBlock + group.igOffs - dst));
            assert((immediate & 3) == 0);
            var adjustment = immediate & 0x800;
            var rounded = unchecked(immediate + adjustment);
            assert(isValidSimm20(rounded >> 12));
            var lowImmediate = (rounded & 0x7ff) - adjustment;
            var relocCode = 0x1c000000u | unchecked((uint)reg) |
                ((unchecked((uint)rounded) & 0xfffff000) >> 7);
            emitOutputLoongArch64Instruction(dst, relocCode);
            ins = INS_addi_d;
            relocCode = 0x02c00000u | unchecked((uint)reg) | (unchecked((uint)reg) << 5) |
                ((unchecked((uint)lowImmediate) & 0xfff) << 10);
            emitOutputLoongArch64Instruction(dst + sizeof(uint), relocCode);
            return 2 * sizeof(uint);
        }

        var address = unchecked((nint)(emitCodeBlock + group.igOffs));
        assert(unchecked((nuint)(address >> 32)) <= 0x7ffff);
        var code = emitInsCode(INS_lu12i_w);
        code |= unchecked((uint)REG_R21);
        code |= (unchecked((uint)(address >> 12)) & 0xfffff) << 5;
        emitOutputLoongArch64Instruction(dst, code);
        code = emitInsCode(INS_ori);
        code |= unchecked((uint)reg);
        code |= unchecked((uint)REG_R21) << 5;
        code |= (unchecked((uint)address) & 0xfff) << 10;
        emitOutputLoongArch64Instruction(dst + sizeof(uint), code);
        code = emitInsCode(INS_lu32i_d);
        code |= unchecked((uint)reg);
        code |= (unchecked((uint)(address >> 32)) & 0x7ffff) << 5;
        emitOutputLoongArch64Instruction(dst + 2 * sizeof(uint), code);
        ins = INS_lu32i_d;
        return 3 * sizeof(uint);
    }

    private unsafe uint EmitOutputLoongArch64LongConditionalJump(
        byte* dst,
        instrDescJmp jump,
        out instruction ins)
    {
        var immediate = unchecked(jump.idAddr().iiaGetJmpOffset() - sizeof(uint));
        assert((immediate & 3) == 0);
        ins = jump.idIns();
        assert(jump.idCodeSize() > sizeof(uint));

        if (jump.idCodeSize() != 8)
        {
            unreached();
            return 0;
        }

        var reg1 = jump.idReg1();
        var reg2 = jump.idReg2();
        uint code;
        if (ins is INS_beq or INS_bne && immediate >= -0x400000 && immediate < 0x400000)
        {
            code = emitInsCode(INS_xor);
            code |= unchecked((uint)REG_R21);
            code |= unchecked((uint)reg1) << 5;
            code |= unchecked((uint)reg2) << 10;
            emitOutputLoongArch64Instruction(dst, code);

            code = emitInsCode(ins == INS_beq ? INS_beqz : INS_bnez);
            code |= unchecked((uint)REG_R21) << 5;
            code |= (unchecked((uint)immediate) << 8) & 0x3fffc00;
            code |= (unchecked((uint)(immediate >> 18)) & 0x1f);
            emitOutputLoongArch64Instruction(dst + sizeof(uint), code);
        }
        else
        {
            instruction inverse;
            if (ins is INS_beq or INS_bne or INS_blt or INS_bge or INS_bltu or INS_bgeu)
            {
                inverse = (instruction)((int)ins ^ 1);
                code = emitInsCode(inverse);
                code |= unchecked((uint)reg1) << 5;
                code |= unchecked((uint)reg2);
            }
            else if (ins is INS_bceqz or INS_bcnez)
            {
                inverse = (instruction)((int)ins ^ 1);
                code = emitInsCode(inverse);
                code |= unchecked((uint)reg1) << 5;
            }
            else
            {
                unreached();
                return 0;
            }

            code |= 0x800;
            emitOutputLoongArch64Instruction(dst, code);
            code = emitInsCode(INS_b);
            code |= (unchecked((uint)(immediate >> 18)) & 0x3ff);
            code |= (unchecked((uint)immediate) << 8) & 0x3fffc00;
            emitOutputLoongArch64Instruction(dst + sizeof(uint), code);
        }

        return 2 * sizeof(uint);
    }

    private static uint EncodeLoongArch64Branch(instruction ins, instrDesc id, nint immediate)
    {
        assert((immediate & 3) == 0);
        var code = emitInsCode(ins);
        if (ins is INS_b or INS_bl)
        {
            code |= (unchecked((uint)(immediate >> 18)) & 0x3ff);
            code |= (unchecked((uint)(immediate >> 2)) & 0xffff) << 10;
        }
        else if (ins is INS_bnez or INS_beqz or INS_bcnez or INS_bceqz)
        {
            code |= unchecked((uint)id.idReg1()) << 5;
            code |= (unchecked((uint)immediate) << 8) & 0x3fffc00;
            code |= (unchecked((uint)(immediate >> 18)) & 0x1f);
        }
        else
        {
            assert(ins is INS_beq or INS_bne or INS_blt or INS_bltu or INS_bge or INS_bgeu);
            code |= unchecked((uint)id.idReg1()) << 5;
            code |= unchecked((uint)id.idReg2());
            code |= (unchecked((uint)immediate) << 8) & 0x3fffc00;
        }
        return code;
    }
}
#endif
