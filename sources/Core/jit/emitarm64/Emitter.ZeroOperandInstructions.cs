// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    private insFormat emitInsFormatArm64(instruction ins)
    {
        RequireArm64Target();
        assert((uint)InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins) < (uint)s_instructionFormats_ARM64.Length);
        assert(s_instructionFormats_ARM64[InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)] != IF_NONE,
            "s_instructionFormats[InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)] != IF_NONE");
        return s_instructionFormats_ARM64[InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)];
    }

    private bool emitInsIsLoadArm64(instruction ins)
    {
        RequireArm64Target();
        if (InstructionOrdinalMaps.TryGetNativeInstructionOrdinal(ins, out var nativeOrdinal) &&
            (uint)nativeOrdinal < (uint)CodeGen.instInfo.Length)
        {
            return (((byte)CodeGen.instInfo[nativeOrdinal] & CodeGen.LD) != 0);
        }

        return false;
    }

    public void emitInsArm64(instruction ins)
    {
        RequireArm64Target();
        var id = emitNewInstrSmall(EA_8BYTE);
        var format = emitInsFormatArm64(ins);

        if (ins != INS_BREAKPOINT)
        {
            switch (ins)
            {
                case INS_autia1716:
                case INS_autiasp:
                case INS_autiaz:
                case INS_autib1716:
                case INS_autibsp:
                case INS_autibz:
                case INS_pacia1716:
                case INS_paciasp:
                case INS_paciaz:
                case INS_pacib1716:
                case INS_pacibsp:
                case INS_pacibz:
                case INS_xpaclri:
                {
                    assert(format == IF_ARM64_PC_0A);
                    break;
                }

                case INS_retaa:
                case INS_retab:
                {
                    assert(format == IF_ARM64_BR_0A);
                    break;
                }

                default:
                {
                    assert(format == IF_ARM64_SN_0A);
                    break;
                }
            }
        }

        id.idIns(ins);
        id.idInsFmt(format);
        dispIns(id);
        appendToCurIG(id);
    }
}
