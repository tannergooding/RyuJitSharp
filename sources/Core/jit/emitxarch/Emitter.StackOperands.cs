// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns_S(instruction ins, emitAttr attr, int varx, int offs)
    {
#if !TARGET_XARCH
        throw new FatalJitException(CORJIT_SKIPPED, "Stack instruction recording requires xarch.");
#else
#if TARGET_AMD64
        RequireSupportedInstructionRecording();
#endif
        var id = emitNewInstr(attr);
        var fmt = emitInsModeFormat(ins, IF_SRD);
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idAddr().iiaLclVar.initLclVarAddr(varx, unchecked((uint)offs));

        var sz = emitInsSizeSV(id, insCodeMR(ins), varx, offs);
        id.idCodeSize(sz);
#if DEBUG
        var debugInfo = id.idDebugOnlyInfo();
        assert(debugInfo is not null);
        debugInfo.idVarRefOffs = unchecked((uint)emitVarRefOffs);
#endif
        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);

#if TARGET_X86
        emitAdjustStackDepthPushPop(ins);
#endif
#endif
    }

    public void emitIns_S_I(instruction ins, emitAttr attr, int varx, int offs, int val)
    {
#if !TARGET_XARCH
        throw new FatalJitException(CORJIT_SKIPPED, "Stack-immediate instruction recording requires xarch.");
#else
#if TARGET_AMD64
        RequireSupportedInstructionRecording();
        noway_assert((EA_SIZE(attr) < EA_8BYTE) || !EA_IS_CNS_RELOC(attr));
#endif

        insFormat fmt;
        switch (ins)
        {
            case INS_rcl_N or INS_rcr_N or INS_rol_N or INS_ror_N or INS_shl_N or INS_shr_N or INS_sar_N:
            {
                assert(val != 1);
                fmt = IF_SRW_SHF;
                val &= 0x7F;
                break;
            }

            default:
            {
                fmt = emitInsModeFormat(ins, IF_SRD_CNS);
                break;
            }
        }

        var id = emitNewInstrCns(attr, val);
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idAddr().iiaLclVar.initLclVarAddr(varx, unchecked((uint)offs));

        var sz = emitInsSizeSV(id, insCodeMI(ins), varx, offs, val);
        id.idCodeSize(sz);
#if DEBUG
        var debugInfo = id.idDebugOnlyInfo();
        assert(debugInfo is not null);
        debugInfo.idVarRefOffs = unchecked((uint)emitVarRefOffs);
#endif
        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);
#endif
    }
}
