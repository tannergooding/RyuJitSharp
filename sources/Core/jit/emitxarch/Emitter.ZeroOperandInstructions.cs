// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public partial class Emitter
{
    private const uint MAX_ENCODED_SIZE = 15;

    public void emitIns_Nop(uint size)
    {
        if (Configuration.TARGET_AMD64)
        {
            RequireSupportedInstructionRecording();
        }
        assert(size <= MAX_ENCODED_SIZE);
        var id = emitNewInstr(EA_4BYTE);
        id.idIns(INS_nop);
        id.idInsFmt(IF_NONE);
        id.idCodeSize(size);

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)size);
    }

    public void emitInsXarch(instruction ins)
    {
        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.XarchInstructionRecordingRequiresXarch);
            return;
        }

        if (Configuration.TARGET_AMD64)
        {
            RequireSupportedInstructionRecording();
        }

        var id = emitNewInstr(EA_4BYTE);
        var code = insCodeMR(ins);
        assert((ins is INS_cdq or INS_int3 or INS_lock or INS_leave or INS_movsb or INS_movsd
            or INS_nop or INS_r_movsb or INS_r_movsd or INS_r_stosb or INS_r_stosd or INS_ret
            or INS_sahf or INS_stosb or INS_stosd)
            || (Configuration.TARGET_AMD64 && (ins is INS_movsq or INS_r_movsq or INS_r_stosq or INS_stosq))
            || (ins is INS_vzeroupper or INS_lfence or INS_mfence or INS_sfence or INS_pause or INS_serialize));
        assert(!hasRexPrefix(code));

        uint size;
        if ((code & 0xFF000000) != 0)
        {
            size = 2; // Native TODO-XArch-Bug?: Should this be 4, or should this case assert?
        }
        else if ((code & 0x00FF0000) != 0)
        {
            size = 3;
        }
        else if ((code & 0x0000FF00) != 0)
        {
            size = 2;
        }
        else
        {
            size = 1;
        }

        // vzeroupper includes its two-byte VEX prefix in its MR code.
        assert((ins != INS_vzeroupper) || (size == 3));
        id.idIns(ins);
        id.idInsFmt(IF_NONE);
        id.idCodeSize(size);

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)size);
    }

    public void emitIns(instruction ins, emitAttr attr)
    {
        if (Configuration.TARGET_AMD64)
        {
            RequireSupportedInstructionRecording();
        }
        var id = emitNewInstr(attr);
        var code = insCodeMR(ins);
        assert(ins is INS_cdq or INS_cwde);
        assert((code & 0xFFFFFF00) == 0);
        uint size = 1;
        id.idIns(ins);
        id.idInsFmt(IF_NONE);

        size += emitGetAdjustedSize(id, code);
        if (TakesRexWPrefix(id))
        {
            size += emitGetRexPrefixSize(id, ins);
        }
        id.idCodeSize(size);

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)size);
    }
}
