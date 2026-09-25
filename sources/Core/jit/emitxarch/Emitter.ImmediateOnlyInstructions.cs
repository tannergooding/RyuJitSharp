// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns_I(instruction ins, emitAttr attr, nint val)
    {
#if !TARGET_AMD64 || UNIX_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Immediate-only instruction recording requires Windows AMD64.");
#else
        RequireSupportedInstructionRecording();
        var valInByte = unchecked((sbyte)val) == val;
        noway_assert((EA_SIZE(attr) < EA_8BYTE) || !EA_IS_CNS_RELOC(attr));

        if (EA_IS_CNS_RELOC(attr))
        {
            valInByte = false;
        }

        uint size;
        switch (ins)
        {
            case INS_loop:
            case INS_jge:
            {
                size = 2;
                break;
            }

            case INS_ret:
            {
                size = 3;
                break;
            }

            case INS_push_hide:
            case INS_push:
            {
                size = valInByte ? 2u : 5u;
                break;
            }

            default:
            {
                throw new FatalJitException("Unexpected immediate-only instruction.");
            }
        }

        var id = emitNewInstrSC(attr, val);
        id.idIns(ins);
        id.idInsFmt(insFormat.IF_CNS);
        size += emitGetAdjustedSize(id, insCodeMI(ins));
        id.idCodeSize(size);
        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)size);
        // Native emitAdjustStackDepthPushPop is empty with AMD64's FEATURE_FIXED_OUT_ARGS.
#endif
    }
}
