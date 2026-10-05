// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
namespace RyuJitSharp;

public partial class Emitter
{
    private static bool canEncodeLoadOrStorePairOffset(long imm, emitAttr attr)
    {
        assert((attr == EA_4BYTE) || (attr == EA_8BYTE) || (attr == EA_16BYTE));

        var size = EA_SIZE_IN_BYTES(attr);
        return ((imm % size) == 0) && (imm >= (-64 * size)) && (imm < (64 * size));
    }

    private static emitAttr optGetDatasize(insOpts arrangement)
    {
        if ((arrangement == INS_OPTS_8B) || (arrangement == INS_OPTS_4H) ||
            (arrangement == INS_OPTS_2S) || (arrangement == INS_OPTS_1D))
        {
            return EA_8BYTE;
        }

        if ((arrangement == INS_OPTS_16B) || (arrangement == INS_OPTS_8H) ||
            (arrangement == INS_OPTS_4S) || (arrangement == INS_OPTS_2D))
        {
            return EA_16BYTE;
        }

        assert(false, " invalid 'arrangement' value");
        return EA_UNKNOWN;
    }

    private static insOpts optWidenElemsizeArrangement(insOpts arrangement)
    {
        if ((arrangement == INS_OPTS_8B) || (arrangement == INS_OPTS_16B))
        {
            return INS_OPTS_8H;
        }

        if ((arrangement == INS_OPTS_4H) || (arrangement == INS_OPTS_8H))
        {
            return INS_OPTS_4S;
        }

        if ((arrangement == INS_OPTS_2S) || (arrangement == INS_OPTS_4S))
        {
            return INS_OPTS_2D;
        }

        assert(false, " invalid 'arrangement' value");
        return INS_OPTS_NONE;
    }

    private static emitAttr widenDatasize(emitAttr datasize)
    {
        if (datasize == EA_1BYTE)
        {
            return EA_2BYTE;
        }

        if (datasize == EA_2BYTE)
        {
            return EA_4BYTE;
        }

        if (datasize == EA_4BYTE)
        {
            return EA_8BYTE;
        }

        assert(false, " invalid 'datasize' value");
        return EA_UNKNOWN;
    }

    private static insOpts optWidenDstArrangement(insOpts srcArrangement)
    {
        var dstArrangement = INS_OPTS_NONE;
        switch (srcArrangement)
        {
            case INS_OPTS_8B:
            {
                dstArrangement = INS_OPTS_4H;
                break;
            }

            case INS_OPTS_16B:
            {
                dstArrangement = INS_OPTS_8H;
                break;
            }

            case INS_OPTS_4H:
            {
                dstArrangement = INS_OPTS_2S;
                break;
            }

            case INS_OPTS_8H:
            {
                dstArrangement = INS_OPTS_4S;
                break;
            }

            case INS_OPTS_2S:
            {
                dstArrangement = INS_OPTS_1D;
                break;
            }

            case INS_OPTS_4S:
            {
                dstArrangement = INS_OPTS_2D;
                break;
            }

            default:
            {
                assert(false, " invalid 'srcArrangement' value");
                break;
            }
        }

        return dstArrangement;
    }

    private static emitAttr emitInsTargetRegSize(instrDesc id)
    {
        var ins = id.idIns();
        var result = EA_UNKNOWN;
        switch (ins)
        {
            case INS_ldxrb:
            case INS_ldarb:
            case INS_ldaprb:
            case INS_ldaxrb:
            case INS_stxrb:
            case INS_stlrb:
            case INS_stlxrb:
            case INS_ldrb:
            case INS_strb:
            case INS_ldurb:
            case INS_ldapurb:
            case INS_sturb:
            case INS_stlurb:
            case INS_ldxrh:
            case INS_ldarh:
            case INS_ldaprh:
            case INS_ldaxrh:
            case INS_stxrh:
            case INS_stlrh:
            case INS_stlxrh:
            case INS_ldrh:
            case INS_strh:
            case INS_ldurh:
            case INS_sturh:
            case INS_ldapurh:
            case INS_stlurh:
            {
                result = EA_4BYTE;
                break;
            }

            case INS_ldrsb:
            case INS_ldursb:
            case INS_ldrsh:
            case INS_ldursh:
            {
                result = id.idOpSize() == EA_8BYTE ? EA_8BYTE : EA_4BYTE;
                break;
            }

            case INS_ldrsw:
            case INS_ldursw:
            case INS_ldpsw:
            {
                result = EA_8BYTE;
                break;
            }

            case INS_ldp:
            case INS_stp:
            case INS_ldnp:
            case INS_stnp:
            case INS_ldxr:
            case INS_ldar:
            case INS_ldapr:
            case INS_ldaxr:
            case INS_stxr:
            case INS_stlr:
            case INS_stlxr:
            case INS_ldr:
            case INS_str:
            case INS_ldur:
            case INS_stur:
            case INS_ldapur:
            case INS_stlur:
            {
                result = id.idOpSize();
                break;
            }

            default:
            {
                NO_WAY("unexpected instruction");
                break;
            }
        }

        return result;
    }

    private static emitAttr emitInsLoadStoreSize(instrDesc id)
    {
        var ins = id.idIns();
        var result = EA_UNKNOWN;
        switch (ins)
        {
            case INS_ldarb:
            case INS_ldaprb:
            case INS_stlrb:
            case INS_ldrb:
            case INS_strb:
            case INS_ldurb:
            case INS_ldapurb:
            case INS_sturb:
            case INS_stlurb:
            case INS_ldrsb:
            case INS_ldursb:
            {
                result = EA_1BYTE;
                break;
            }

            case INS_ldarh:
            case INS_ldaprh:
            case INS_stlrh:
            case INS_ldrh:
            case INS_strh:
            case INS_ldurh:
            case INS_sturh:
            case INS_ldrsh:
            case INS_ldursh:
            case INS_ldapurh:
            case INS_stlurh:
            {
                result = EA_2BYTE;
                break;
            }

            case INS_ldrsw:
            case INS_ldursw:
            case INS_ldpsw:
            {
                result = EA_4BYTE;
                break;
            }

            case INS_ldp:
            case INS_stp:
            case INS_ldnp:
            case INS_stnp:
            case INS_ldar:
            case INS_ldapr:
            case INS_stlr:
            case INS_ldr:
            case INS_str:
            case INS_ldur:
            case INS_stur:
            case INS_ldapur:
            case INS_stlur:
            {
                result = id.idOpSize();
                break;
            }

            default:
            {
                NO_WAY("unexpected instruction");
                break;
            }
        }

        return result;
    }
}
#endif
