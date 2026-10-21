// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns_R_I(instruction ins, emitAttr attr, regNumber reg, nint val,
        insOpts instOptions = INS_OPTS_NONE, nuint targetHandle = 0, GenTreeFlags gtFlags = GTF_EMPTY)
    {
        if (Configuration.TARGET_ARM)
        {
            if (!insOptsNone(instOptions))
            {
                FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
                    "ARM32 register-immediate instruction options are not ported.");
                return;
            }

            if (Configuration.DEBUG && (targetHandle != 0))
            {
                FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
                    "ARM32 register-immediate recording does not accept a target handle.");
                return;
            }

            recordArm32InsRI(ins, attr, reg, unchecked((int)val), INS_FLAGS_DONT_CARE, gtFlags);
            return;
        }

        if (Configuration.TARGET_ARM64)
        {
            emitIns_R_IArm64(ins, attr, reg, val, instOptions,
                targetHandle: targetHandle, gtFlags: gtFlags);
            return;
        }

        if (Configuration.TARGET_RISCV64)
        {
            var code = emitInsCodeRiscV64(ins);

            switch (ins)
            {
                case INS_lui:
                case INS_auipc:
                {
                    assert(reg != TargetRuntimeMetadata.Register0);
                    assert(isGeneralRegister(reg));
                    assert(isValidSimm20(val));

                    code |= unchecked((uint)reg) << 7;
                    code |= (unchecked((uint)val) & 0xFFFFF) << 12;
                    break;
                }
                case INS_jal:
                {
                    assert(isGeneralRegisterOrR0(reg));
                    assert(isValidSimm21(val));

                    code |= unchecked((uint)reg) << 7;
                    code |= (unchecked((uint)(val >> 12)) & 0xFF) << 12;
                    code |= (unchecked((uint)(val >> 11)) & 0x1) << 20;
                    code |= (unchecked((uint)(val >> 1)) & 0x3FF) << 21;
                    code |= (unchecked((uint)(val >> 20)) & 0x1) << 31;
                    break;
                }
                default:
                {
                    NO_WAY("illegal ins within emitIns_R_I!");
                    return;
                }
            }

            var riscvInstr = emitNewInstr(attr);
            riscvInstr.idIns(ins);
            riscvInstr.idReg1(reg);
            riscvInstr.idAddr().iiaInstrEncode = code;
            riscvInstr.idCodeSize(4);

            dispIns(riscvInstr);
            appendToCurIG(riscvInstr);
            return;
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
            emitInsRILoongArch64(ins, attr, reg, val, instOptions);
            return;
        }

        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
                "Register-immediate instruction recording requires xarch.");
            return;
        }

        if (Configuration.TARGET_AMD64)
        {
            RequireSupportedInstructionRecording();
        }
        else
        {
            val = unchecked((int)val);
            if (Configuration.DEBUG)
            {
                targetHandle = unchecked((uint)targetHandle);
            }
        }

        assert(_compiler is not null);
        var size = EA_SIZE(attr);

        // SSE2/AVX R_I instructions can specify a vector operand size.
        assert((size <= EA_PTRSIZE) || IsSimdInstruction(ins));
        noway_assert(emitVerifyEncodable(ins, size, reg));

        // Only mov reg, imm64 takes a full eight-byte immediate. All other opcodes
        // take a sign-extended four-byte immediate.
        if (Configuration.TARGET_AMD64)
        {
            noway_assert((size < EA_8BYTE) || (ins == INS_mov) ||
                ((unchecked((int)val) == val) && !EA_IS_CNS_RELOC(attr)));
        }

        uint sz;
        var fmt = emitInsModeFormat(ins, IF_XARCH_RRD_CNS);
        var valInByte = ImmCanUseSByteEncoding(ins, val);

        // BT reg,imm needs special handling because its immediate is always a byte.
        assert(ins != INS_bt);

        // Retain the historical additional emitInsSize call for SIMD byte immediates;
        // proving it redundant requires auditing the different prefix paths.
        var isSimdInsAndValInByte = false;

        switch (ins)
        {
            case INS_mov:
            {
                if (Configuration.TARGET_AMD64)
                {
                    // A non-relocatable zero-extended imm32 can use mov r32, imm32.
                    if ((size > EA_4BYTE) && ((unchecked((ulong)val) & 0xFFFFFFFF00000000UL) == 0) &&
                        !EA_IS_CNS_RELOC(attr))
                    {
                        attr = size = EA_4BYTE;
                    }

                    if (size > EA_4BYTE)
                    {
                        sz = 9; // The REX prefix is counted below.
                        break;
                    }
                }

                sz = 5;
                break;
            }

            case INS_rcl_N or INS_rcr_N or INS_rol_N or INS_ror_N or INS_shl_N or INS_shr_N or INS_sar_N:
            {
                assert(val != 1);
                fmt = IF_XARCH_RRW_SHF;
                sz = 3;
                val &= 0x7F;
                break;
            }

            default:
            {
                if (EA_IS_CNS_RELOC(attr))
                {
                    valInByte = false;
                }

                if (valInByte)
                {
                    if (IsSimdInstruction(ins))
                    {
                        sz = 1;
                        isSimdInsAndValInByte = true;
                    }
                    else if ((size == EA_1BYTE) && (reg == TargetRuntimeMetadata.IntegerReturnRegister) &&
                        !IsThreeOperandImulInstruction(ins))
                    {
                        sz = 2;
                    }
                    else
                    {
                        sz = 3;
                    }
                }
                else
                {
                    assert(!IsSimdInstruction(ins));
                    if ((reg == TargetRuntimeMetadata.IntegerReturnRegister) && !IsThreeOperandImulInstruction(ins))
                    {
                        sz = 1;
                    }
                    else
                    {
                        sz = 2;
                    }

                    if (Configuration.TARGET_AMD64 && size > EA_4BYTE)
                    {
                        sz += 4;
                    }
                    else
                    {
                        sz += EA_SIZE_IN_BYTES(attr);
                    }
                }
                break;
            }
        }

        instrDesc id;
        if (_compiler.IsTargetAbi(CORINFO_NATIVEAOT_ABI))
        {
            if ((attr & EA_CNS_SEC_RELOC) != 0)
            {
                id = emitNewInstrCns(attr, val);
                id.idAddr().iiaSecRel = true;
            }
            else
            {
                id = emitNewInstrSC(attr, val);
            }
        }
        else
        {
            id = emitNewInstrSC(attr, val);
        }

        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idReg1(reg);
        if (Configuration.DEBUG)
        {
            var debugInfo = id.idDebugOnlyInfo();
            assert(debugInfo is not null);
            debugInfo.idFlags = gtFlags;
            debugInfo.idMemCookie = unchecked((nint)targetHandle);
        }
        SetEvexNfIfNeeded(id, instOptions);
        SetEvexDFVIfNeeded(id, instOptions);

        if (isSimdInsAndValInByte)
        {
            var includeRexPrefixSize = true;

            // Decide whether REX will be counted below without counting it twice.
            if (IsExtendedReg(reg, attr) || TakesRexWPrefix(id) || instrIsExtendedReg3opImul(ins))
            {
                includeRexPrefixSize = false;
            }

            sz += emitInsSize(id, insCodeMI(ins), includeRexPrefixSize);
        }

        sz += emitGetAdjustedSize(id, insCodeMI(ins));

        if (Configuration.TARGET_AMD64)
        {
            if ((reg == TargetRuntimeMetadata.IntegerReturnRegister) &&
                !IsThreeOperandImulInstruction(ins) && TakesEvexPrefix(id))
            {
                // The accumulator form is not promoted into EVEX space; use MI.
                sz += 1;
            }

            if ((ins == INS_test) && (reg == TargetRuntimeMetadata.IntegerReturnRegister) && TakesRex2Prefix(id))
            {
                // TEST's accumulator form is not REX2 compatible.
                sz -= (size == EA_8BYTE) ? 1u : 2u;
            }
        }

        // Three-operand IMUL has an implicit destination encoded in its opcode.
        if (IsExtendedReg(reg, attr) || TakesRexWPrefix(id) || instrIsExtendedReg3opImul(ins))
        {
            sz += emitGetRexPrefixSize(id, ins);
        }

        id.idCodeSize(sz);
        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);

        if (Configuration.TARGET_X86 && reg == REG_X86_ESP)
        {
            emitAdjustStackDepth(ins, val);
        }
    }

    public void emitIns_R_I(instruction ins, emitAttr attr, regNumber reg, nint val, insFlags flags,
        GenTreeFlags gtFlags = GTF_EMPTY)
    {
        if (!Configuration.TARGET_ARM)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
                "ARM32 register-immediate instruction flags are not supported for this target.");
            return;
        }

        recordArm32InsRI(ins, attr, reg, unchecked((int)val), flags, gtFlags);
    }

    public void emitIns_R_R(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2,
        insOpts instOptions = INS_OPTS_NONE)
    {
        if (Configuration.TARGET_ARM)
        {
            if (!insOptsNone(instOptions))
            {
                FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "ARM32 register-register instruction options are not ported.");
                return;
            }

            recordArm32InsRR(ins, attr, reg1, reg2, INS_FLAGS_DONT_CARE, INS_OPTS_NONE);
            return;
        }

        if (Configuration.TARGET_ARM64)
        {
            emitIns_R_RArm64(ins, attr, reg1, reg2, instOptions);
            return;
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
            emitInsRRLLoongArch64(ins, attr, reg1, reg2);
            return;
        }

        if (Configuration.TARGET_RISCV64)
        {
            if (!insOptsNone(instOptions))
            {
                FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
                    "RISC-V register-register instruction options are not implemented.");
                return;
            }

            emitIns_R_RRiscV64(ins, attr, reg1, reg2);
            return;
        }

        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Register-register instruction recording requires xarch.");
            return;
        }

        if (Configuration.TARGET_AMD64)
        {
            RequireSupportedInstructionRecording();
        }

        if (IsMovInstruction(ins))
        {
            assert(false, "Please use emitIns_Mov() to correctly handle move elision");
            _ = emitIns_Mov(ins, attr, reg1, reg2, canSkip: false);
        }

        // The ND slot is shared with other features, so check instruction compatibility.
        var useNDD = ((instOptions & INS_OPTS_EVEX_nd_MASK) != 0) && IsApxNddEncodableInstruction(ins);
        var size = EA_SIZE(attr);
        assert(size <= EA_64BYTE);
        noway_assert(emitVerifyEncodable(ins, size, reg1, reg2));
        var fmt = (ins == INS_xchg) ? IF_XARCH_RRW_RRW : emitInsModeFormat(ins, IF_XARCH_RRD_RRD, useNDD);

        var id = emitNewInstrSmall(attr);
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idReg1(reg1);
        id.idReg2(reg2);

        SetEvexNdIfNeeded(id, instOptions);
        SetEvexNfIfNeeded(id, instOptions);
        SetEvexDFVIfNeeded(id, instOptions);
        SetApxPpxIfNeeded(id, instOptions);
        SetEvexEmbRoundIfNeeded(id, instOptions);
        SetEvexEmbMaskIfNeeded(id, instOptions);

        var sz = emitInsSizeRR(id);
        id.idCodeSize(sz);
        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);
    }
}
