// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    private insFormat emitInsFormat(instruction ins)
    {
        assert((uint)ins < (uint)s_instructionFormats.Length);
        assert(s_instructionFormats[(int)ins] != IF_NONE);
        return s_instructionFormats[(int)ins];
    }

    private bool emitInsIsLoadOrStore(instruction ins)
    {
        if ((uint)ins < (uint)CodeGen.instInfo.Length)
        {
            return (CodeGen.instInfo[(int)ins] & (CodeGen.LD | CodeGen.ST)) != 0;
        }

        return false;
    }

    public void emitIns(instruction ins)
    {
        var id = emitNewInstrSmall(EA_8BYTE);
        var format = emitInsFormat(ins);

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
                    assert(format == IF_PC_0A);
                    break;
                }

                case INS_retaa:
                case INS_retab:
                {
                    assert(format == IF_BR_0A);
                    break;
                }

                default:
                {
                    assert(format == IF_SN_0A);
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
#endif
