// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_AMD64
    public void SetEvexEmbMaskIfNeeded(instrDesc id, insOpts instOptions)
    {
        if ((instOptions & INS_OPTS_EVEX_aaa_MASK) != 0)
        {
            assert(UseEvexEncodings);
            id.idSetEvexAaaContext((uint)instOptions);
        }

        if ((instOptions & INS_OPTS_EVEX_z_MASK) == INS_OPTS_EVEX_em_zero)
        {
            assert(UseEvexEncodings);
            id.idSetEvexZContext();
        }
    }

#if DEBUG
    private static void emitInsSanityCheck(instrDesc id)
    {
        var idOp = (ID_OPS)emitFmtToOps[(int)id.idInsFmt()];

        if ((idOp == ID_OP_SCNS) && id.idIsLargeCns())
        {
            idOp = ID_OP_CNS;
        }

        if (id.idIsDspReloc())
        {
            assert(idOp is ID_OP_NONE or ID_OP_AMD or ID_OP_DSP or ID_OP_DSP_CNS or
                ID_OP_AMD_CNS or ID_OP_SPEC or ID_OP_CALL or ID_OP_JMP or ID_OP_LBL);
        }

        if (id.idIsCnsReloc())
        {
            assert(idOp is ID_OP_CNS or ID_OP_AMD_CNS or ID_OP_DSP_CNS or
                ID_OP_SPEC or ID_OP_CALL or ID_OP_JMP);
        }
    }
#endif
#endif
}
