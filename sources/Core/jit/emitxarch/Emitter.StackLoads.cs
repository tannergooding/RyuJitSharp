// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns_R_S(instruction ins, emitAttr attr, regNumber ireg, int varx, int offs,
        insOpts instOptions = INS_OPTS_NONE)
    {
        if (Configuration.TARGET_AMD64)
        {
            RequireSupportedInstructionRecording();
        }
        noway_assert(emitVerifyEncodable(ins, EA_SIZE(attr), ireg));
        var fmt = emitInsModeFormat(ins, IF_XARCH_RRD_SRD);

        if (IsMovInstruction(ins) && IsRedundantStackMov(ins, fmt, attr, ireg, varx, offs))
        {
            return;
        }

        var id = emitNewInstr(attr);
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idReg1(ireg);
        id.idAddr().iiaInitLclVarAddr(varx, unchecked((uint)offs));

        SetEvexBroadcastIfNeeded(id, instOptions);
        SetEvexEmbMaskIfNeeded(id, instOptions);
        SetEvexNfIfNeeded(id, instOptions);
        SetEvexDFVIfNeeded(id, instOptions);

        var sz = emitInsSizeSV(id, insCodeRM(ins), varx, offs);
        id.idCodeSize(sz);
        if (Configuration.DEBUG)
        {
            var debugInfo = id.idDebugOnlyInfo();
            assert(debugInfo is not null);
            debugInfo!.idVarRefOffs = unchecked((uint)emitVarRefOffs);
        }
        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);
    }
}
