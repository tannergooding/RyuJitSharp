// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_AMD64
    public static bool IsCCMP(instruction ins) => (ins >= FIRST_CCMP_INSTRUCTION) && (ins <= LAST_CCMP_INSTRUCTION);

    public bool IsApxNfEncodableInstruction(instruction ins)
    {
        if (!UsePromotedEvexEncodings)
        {
            return false;
        }

        return IsApxNfCompatibleInstruction(ins);
    }

    public void SetEvexBroadcastIfNeeded(instrDesc id, insOpts instOptions)
    {
        assert(id.idHasMem());

        if ((instOptions & INS_OPTS_EVEX_eb) != INS_OPTS_NONE)
        {
            assert(UseEvexEncodings);
            id.idSetEvexBroadcastBit();
        }
    }

    public void SetEvexEmbRoundIfNeeded(instrDesc id, insOpts instOptions)
    {
        assert(!id.idHasMem());
        if ((instOptions & INS_OPTS_EVEX_b_MASK) != INS_OPTS_NONE)
        {
            // Without a memory operand EVEX.b selects embedded rounding.
            assert(UseEvexEncodings);
            id.idSetEvexbContext((uint)instOptions);
        }
    }

    public void SetEvexNdIfNeeded(instrDesc id, insOpts instOptions)
    {
        if ((instOptions & INS_OPTS_EVEX_nd_MASK) != 0)
        {
            assert(UsePromotedEvexEncodings);
            assert(IsApxNddEncodableInstruction(id.idIns()));
            id.idSetEvexNdContext();
        }
        else
        {
            assert((instOptions & INS_OPTS_EVEX_nd_MASK) == 0);
        }
    }

    public void SetEvexNfIfNeeded(instrDesc id, insOpts instOptions)
    {
        if ((instOptions & INS_OPTS_EVEX_nf_MASK) != 0)
        {
            assert(UsePromotedEvexEncodings);
            assert(IsApxNfEncodableInstruction(id.idIns()));
            id.idSetEvexNfContext();
        }
        else
        {
            assert((instOptions & INS_OPTS_EVEX_nf_MASK) == 0);
        }
    }

    public static void SetApxPpxIfNeeded(instrDesc id, insOpts instOptions)
    {
        if ((instOptions & INS_OPTS_APX_ppx_MASK) != 0)
        {
            assert(HasApxPpx(id.idIns()));
            id.idSetApxPpxContext();
        }
    }

    public void SetEvexDFVIfNeeded(instrDesc id, insOpts instOptions)
    {
        if ((instOptions & INS_OPTS_EVEX_dfv_MASK) != 0)
        {
            assert(UsePromotedEvexEncodings);
            assert(IsCCMP(id.idIns()) || IsCTEST(id.idIns()));
            id.idSetEvexDFV((uint)instOptions);
        }
    }
#endif
}
