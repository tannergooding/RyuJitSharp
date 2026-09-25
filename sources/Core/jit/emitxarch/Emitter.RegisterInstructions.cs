// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns_R_I(instruction ins, emitAttr attr, regNumber reg, nint val,
        insOpts instOptions = INS_OPTS_NONE
#if DEBUG
        , nuint targetHandle = 0, GenTreeFlags gtFlags = GTF_EMPTY
#endif
        )
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Register-immediate instruction recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        assert(_compiler is not null);
        var size = EA_SIZE(attr);

        // SSE2/AVX R_I instructions can specify a vector operand size.
        assert((size <= EA_PTRSIZE) || IsSimdInstruction(ins));
        noway_assert(emitVerifyEncodable(ins, size, reg));

        // Only mov reg, imm64 takes a full eight-byte immediate. All other opcodes
        // take a sign-extended four-byte immediate.
        noway_assert((size < EA_8BYTE) || (ins == INS_mov) ||
            ((unchecked((int)val) == val) && !EA_IS_CNS_RELOC(attr)));

        uint sz;
        var fmt = emitInsModeFormat(ins, IF_RRD_CNS);
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

                sz = 5;
                break;
            }

            case INS_rcl_N or INS_rcr_N or INS_rol_N or INS_ror_N or INS_shl_N or INS_shr_N or INS_sar_N:
            {
                assert(val != 1);
                fmt = IF_RRW_SHF;
                sz = 3;
                val &= 0x7F;
                valInByte = true;
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
                    else if ((size == EA_1BYTE) && (reg == REG_EAX) && !instrIs3opImul(ins))
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
                    sz = ((reg == REG_EAX) && !instrIs3opImul(ins)) ? 1u : 2u;
                    sz += (size > EA_4BYTE) ? 4u : EA_SIZE_IN_BYTES(attr);
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
#if DEBUG
        var debugInfo = id.idDebugOnlyInfo();
        assert(debugInfo is not null);
        debugInfo.idFlags = gtFlags;
        debugInfo.idMemCookie = unchecked((nint)targetHandle);
#endif
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

        if ((reg == REG_EAX) && !instrIs3opImul(ins) && TakesEvexPrefix(id))
        {
            // The accumulator form is not promoted into EVEX space; use MI.
            sz += 1;
        }

        if ((ins == INS_test) && (reg == REG_EAX) && TakesRex2Prefix(id))
        {
            // TEST's accumulator form is not REX2 compatible.
            sz -= (size == EA_8BYTE) ? 1u : 2u;
        }

        // Three-operand IMUL has an implicit destination encoded in its opcode.
        if (IsExtendedReg(reg, attr) || TakesRexWPrefix(id) || instrIsExtendedReg3opImul(ins))
        {
            sz += emitGetRexPrefixSize(id, ins);
        }

        id.idCodeSize(sz);
        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);

        // Native emitAdjustStackDepth is empty with AMD64's FEATURE_FIXED_OUT_ARGS.
#endif
    }

    public void emitIns_R_R(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2,
        insOpts instOptions = INS_OPTS_NONE)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Register-register instruction recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        if (IsMovInstruction(ins))
        {
            assert(false, "Please use emitIns_Mov() to correctly handle move elision");
            emitIns_Mov(ins, attr, reg1, reg2, canSkip: false);
        }

        // The ND slot is shared with other features, so check instruction compatibility.
        var useNDD = ((instOptions & INS_OPTS_EVEX_nd_MASK) != 0) && IsApxNddEncodableInstruction(ins);
        var size = EA_SIZE(attr);
        assert(size <= EA_64BYTE);
        noway_assert(emitVerifyEncodable(ins, size, reg1, reg2));
        var fmt = (ins == INS_xchg) ? IF_RRW_RRW : emitInsModeFormat(ins, IF_RRD_RRD, useNDD);

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
#endif
    }
}
