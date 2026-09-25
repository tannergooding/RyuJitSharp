// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns_R_A_I(instruction ins, emitAttr attr, regNumber reg1, GenTreeIndir indir, int ival,
        insOpts instOptions = INS_OPTS_NONE)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Register-memory-immediate instruction recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        noway_assert(emitVerifyEncodable(ins, EA_SIZE(attr), reg1));
        assert(IsSimdInstruction(ins));

        var offs = indir.Offset;
        var id = emitNewInstrAmdCns(attr, offs, ival);
        id.idIns(ins);
        id.idReg1(reg1);
        emitHandleMemOp(indir, id, emitInsModeFormat(ins, IF_RRD_ARD_CNS), ins);
        ulong code = hasCodeMI(ins) ? insCodeMI(ins) : insCodeRM(ins);

        SetEvexBroadcastIfNeeded(id, instOptions);
        SetEvexEmbMaskIfNeeded(id, instOptions);
        var sz = emitInsSizeAM(id, code, ival);
        id.idCodeSize(sz);

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);
#endif
    }

    public unsafe void emitIns_R_C_I(instruction ins, emitAttr attr, regNumber reg1,
        CORINFO_FIELD_HANDLE fldHnd, int offs, int ival, insOpts instOptions = INS_OPTS_NONE)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Register-field-immediate instruction recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        if (!jitStaticFldIsGlobAddr(fldHnd))
        {
            attr |= EA_DSP_RELOC_FLG;
        }

        noway_assert(emitVerifyEncodable(ins, EA_SIZE(attr), reg1));
        assert(IsSimdInstruction(ins));
        var id = emitNewInstrCnsDsp(attr, ival, offs);
        id.idIns(ins);
        id.idInsFmt(emitInsModeFormat(ins, IF_RRD_MRD_CNS));
        id.idReg1(reg1);
        id.idAddr().iiaFieldHnd = fldHnd;
        ulong code = hasCodeMI(ins) ? insCodeMI(ins) : insCodeRM(ins);

        SetEvexBroadcastIfNeeded(id, instOptions);
        SetEvexEmbMaskIfNeeded(id, instOptions);
        var sz = emitInsSizeCV(id, code, ival);
        id.idCodeSize(sz);

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);
#endif
    }

    public void emitIns_R_S_I(instruction ins, emitAttr attr, regNumber reg1, int varx, int offs, int ival,
        insOpts instOptions = INS_OPTS_NONE)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Register-stack-immediate instruction recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
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

    public void emitIns_R_R_A_I(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2,
        GenTreeIndir indir, int ival, insFormat fmt, insOpts instOptions = INS_OPTS_NONE)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Two-register memory-immediate recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        assert(IsSimdInstruction(ins));
        assert(IsThreeOperandAVXInstruction(ins));

        var offs = indir.Offset;
        var id = emitNewInstrAmdCns(attr, offs, ival);
        id.idIns(ins);
        id.idReg1(reg1);
        id.idReg2(reg2);
        emitHandleMemOp(indir, id, fmt, ins);

        SetEvexBroadcastIfNeeded(id, instOptions);
        SetEvexEmbMaskIfNeeded(id, instOptions);
        var sz = emitInsSizeAM(id, insCodeRM(ins), ival);
        id.idCodeSize(sz);

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);
#endif
    }

    public unsafe void emitIns_R_R_C_I(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2,
        CORINFO_FIELD_HANDLE fldHnd, int offs, int ival, insOpts instOptions = INS_OPTS_NONE)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Two-register field-immediate recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        assert(IsSimdInstruction(ins));
        assert(IsThreeOperandAVXInstruction(ins));
        if (!jitStaticFldIsGlobAddr(fldHnd))
        {
            attr |= EA_DSP_RELOC_FLG;
        }

        var id = emitNewInstrCnsDsp(attr, ival, offs);
        id.idIns(ins);
        id.idInsFmt(IF_RWR_RRD_MRD_CNS);
        id.idReg1(reg1);
        id.idReg2(reg2);
        id.idAddr().iiaFieldHnd = fldHnd;

        SetEvexBroadcastIfNeeded(id, instOptions);
        SetEvexEmbMaskIfNeeded(id, instOptions);
        var sz = emitInsSizeCV(id, insCodeRM(ins), ival);
        id.idCodeSize(sz);

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);
#endif
    }

    public void emitIns_R_R_S_I(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2,
        int varx, int offs, int ival, insOpts instOptions = INS_OPTS_NONE)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Two-register stack-immediate recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        assert(IsSimdInstruction(ins));
        assert(IsThreeOperandAVXInstruction(ins));

        var id = emitNewInstrCns(attr, ival);
        id.idIns(ins);
        id.idInsFmt(IF_RWR_RRD_SRD_CNS);
        id.idReg1(reg1);
        id.idReg2(reg2);
        id.idAddr().iiaLclVar.initLclVarAddr(varx, unchecked((uint)offs));

        SetEvexBroadcastIfNeeded(id, instOptions);
        SetEvexEmbMaskIfNeeded(id, instOptions);
#if DEBUG
        var debugInfo = id.idDebugOnlyInfo();
        assert(debugInfo is not null);
        debugInfo.idVarRefOffs = unchecked((uint)emitVarRefOffs);
#endif
        var sz = emitInsSizeSV(id, insCodeRM(ins), varx, offs, ival);
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
