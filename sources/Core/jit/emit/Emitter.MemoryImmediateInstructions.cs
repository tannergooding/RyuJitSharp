// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    public unsafe void emitIns_C_I(instruction ins, emitAttr attr, CORINFO_FIELD_HANDLE fldHnd, int offs, int val,
        insOpts instOptions = INS_OPTS_NONE)
    {
#if TARGET_ARM64
        NYI("emitIns_C_I");
#elif !TARGET_XARCH
        throw new FatalJitException(CORJIT_SKIPPED, "Static-field immediate instruction recording requires xarch.");
#else
#if TARGET_AMD64
        RequireSupportedInstructionRecording();
#endif
        if (!jitStaticFldIsGlobAddr(fldHnd))
        {
            attr |= EA_DSP_RELOC_FLG;
        }

        insFormat fmt;
        switch (ins)
        {
            case INS_rcl_N or INS_rcr_N or INS_rol_N or INS_ror_N or INS_shl_N or INS_shr_N or INS_sar_N:
            {
                assert(val != 1);
                fmt = IF_MRW_SHF;
                val &= 0x7F;
                break;
            }

            default:
            {
                fmt = emitInsModeFormat(ins, IF_MRD_CNS);
                break;
            }
        }

        var id = emitNewInstrCnsDsp(attr, val, offs);
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idAddr().iiaFieldHnd = fldHnd;
        var sz = emitInsSizeCV(id, insCodeMI(ins), val);
        id.idCodeSize(sz);

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);
#endif
    }

    public unsafe void emitIns_C_R_I(instruction ins, emitAttr attr, CORINFO_FIELD_HANDLE fldHnd, int offs,
        regNumber reg, int ival)
    {
#if !TARGET_XARCH
        throw new FatalJitException(CORJIT_SKIPPED, "Static-field register-immediate instruction recording requires xarch.");
#else
#if TARGET_AMD64
        RequireSupportedInstructionRecording();
#endif
        assert(IsSimdInstruction(ins));
        assert(reg != REG_NA);
        if (!jitStaticFldIsGlobAddr(fldHnd))
        {
            attr |= EA_DSP_RELOC_FLG;
        }

        var id = emitNewInstrCnsDsp(attr, ival, offs);
        id.idIns(ins);
        id.idInsFmt(emitInsModeFormat(ins, IF_MRD_RRD_CNS));
        id.idReg1(reg);
        id.idAddr().iiaFieldHnd = fldHnd;
        var sz = emitInsSizeCV(id, insCodeMR(ins), ival);
        id.idCodeSize(sz);

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);
#endif
    }

    public void emitIns_R_S_I(instruction ins, emitAttr attr, regNumber reg1, int varx, int offs, int ival,
        insOpts instOptions = INS_OPTS_NONE)
    {
#if !TARGET_XARCH
        throw new FatalJitException(CORJIT_SKIPPED, "Register-stack-immediate instruction recording requires xarch.");
#else
#if TARGET_AMD64
        RequireSupportedInstructionRecording();
#endif
        noway_assert(emitVerifyEncodable(ins, EA_SIZE(attr), reg1));
        assert(IsSimdInstruction(ins));

        var id = emitNewInstrCns(attr, ival);
        id.idIns(ins);
        id.idInsFmt(emitInsModeFormat(ins, IF_RRD_SRD_CNS));
        id.idReg1(reg1);
        id.idAddr().iiaLclVar.initLclVarAddr(varx, unchecked((uint)offs));
#if DEBUG
        var debugInfo = id.idDebugOnlyInfo();
        assert(debugInfo is not null);
        debugInfo.idVarRefOffs = unchecked((uint)emitVarRefOffs);
#endif
        ulong code = hasCodeMI(ins) ? insCodeMI(ins) : insCodeRM(ins);
        SetEvexBroadcastIfNeeded(id, instOptions);
        SetEvexEmbMaskIfNeeded(id, instOptions);
        var sz = emitInsSizeSV(id, code, varx, offs, ival);
        id.idCodeSize(sz);

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);
#endif
    }

#if TARGET_AMD64
    public uint emitInsSizeCV(instrDesc id, ulong code, int val)
    {
        var ins = id.idIns();
        var valSize = EA_SIZE_IN_BYTES(id.idOpSize());
        var valInByte = ImmCanUseSByteEncoding(ins, val);

        // Only mov reg,imm64 accepts an eight-byte immediate. Other instructions
        // sign-extend a dword, which cannot carry an eight-byte relocation.
        noway_assert((valSize <= sizeof(int)) || !id.idIsCnsReloc());
        if (valSize > sizeof(int))
        {
            valSize = sizeof(int);
        }

        if (id.idIsCnsReloc())
        {
            valInByte = false;
            assert(valSize == sizeof(int));
        }

        if (valInByte)
        {
            valSize = 1;
        }
        else
        {
            assert(!IsSimdInstruction(ins));
        }

        return valSize + emitInsSizeCV(id, code);
    }
#endif
}
