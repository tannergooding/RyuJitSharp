// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp;

public partial class Emitter
{
    private bool TryEmitLclAddrAsMov(emitAttr attr, regNumber ireg, int varx, int offs)
    {
        var compiler = _compiler;
        if (compiler is null)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.LocalAddressEmissionMissingCompiler);
            return default!;
        }
        if (compiler.lvaDoneFrameLayout != Compiler.FINAL_FRAME_LAYOUT)
        {
            return false;
        }

        var address = compiler.lvaFrameAddress(varx, out var framePointerBased);
        if (!Configuration.FEATURE_FIXED_OUT_ARGS && !framePointerBased)
        {
            return false;
        }

        if (unchecked(address + offs) != 0)
        {
            return false;
        }

        _ = emitIns_Mov(INS_mov, attr, ireg, framePointerBased ? REG_FPBASE : REG_SPBASE, canSkip: false);
        return true;
    }

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

        if ((ins is INS_lea) && TryEmitLclAddrAsMov(attr, ireg, varx, offs))
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
