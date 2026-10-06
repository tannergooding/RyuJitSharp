// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    private void emitIns_JArm32(instruction ins, BasicBlock dst, bool keepShort)
    {
        assert(dst.HasFlag(BBF_HAS_LABEL));

        var format = IF_NONE;
        switch (ins)
        {
            case INS_b:
            case INS_bl:
            {
                format = IF_T2_J2;
                break;
            }

            case INS_beq:
            case INS_bne:
            case INS_bhs:
            case INS_blo:
            case INS_bmi:
            case INS_bpl:
            case INS_bvs:
            case INS_bvc:
            case INS_bhi:
            case INS_bls:
            case INS_bge:
            case INS_blt:
            case INS_bgt:
            case INS_ble:
            {
                format = IF_LARGEJMP;
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }

        assert(_compiler is not null);
        assert(_compiler.compCurBB is not null);

        var id = emitNewInstrJmp();
        var size = emitInsSize(format);

        id.idIns(ins);
        id.idInsFmt(format);
        id.idInsSize(size);
        id.idjTarget = dst;

        if (keepShort)
        {
            id.idjKeepLong = false;
            emitSetShortJump(id);
        }
        else
        {
            id.idjShort = false;
            id.idjKeepLong = (ins == INS_bl) || _compiler.fgInDifferentRegions(_compiler.compCurBB, dst);
#if DEBUG
            if (_compiler.opts.compLongAddress)
            {
                id.idjKeepLong = true;
            }
#endif
        }

#if DEBUG
        if ((ins == INS_bl) && (_compiler.compCurBB.Kind == BBJ_CALLFINALLY))
        {
            var debugInfo = id.idDebugOnlyInfo();
            assert(debugInfo is not null);
            debugInfo.idFinallyCall = true;
        }
#endif

        id.idjIG = emitCurIG;
        id.idjOffs = unchecked((uint)emitCurIGsize);
        id.idjNext = emitCurIGjmpList;
        emitCurIGjmpList = id;
#if EMITTER_STATS
        emitTotalIGjmps = unchecked(emitTotalIGjmps + 1);
#endif

        if (!id.idjKeepLong)
        {
            var targetGroup = emitCodeGetCookie(dst);
            if (targetGroup is not null)
            {
                assert(JMP_SIZE_SMALL == JCC_SIZE_SMALL);

                var sourceOffset = unchecked((uint)emitCurCodeOffset + (uint)emitCurIGsize);
                var jumpDistance = unchecked((int)(sourceOffset - targetGroup.igOffs));
                assert(jumpDistance >= 0);
                // Thumb branches observe the PC four bytes past the instruction address.
                jumpDistance = unchecked(jumpDistance + 4);
                var negativeJumpDistance = unchecked(-jumpDistance);

                switch (format)
                {
                    case IF_T2_J2:
                    {
                        if (JMP_DIST_SMALL_MAX_NEG <= negativeJumpDistance)
                        {
                            emitSetShortJump(id);
                        }
                        break;
                    }

                    case IF_LARGEJMP:
                    {
                        if (JCC_DIST_SMALL_MAX_NEG <= negativeJumpDistance)
                        {
                            emitSetShortJump(id);
                        }
                        else if (JCC_DIST_MEDIUM_MAX_NEG <= negativeJumpDistance)
                        {
                            emitSetMediumJump(id);
                        }
                        break;
                    }

                    default:
                    {
                        unreached();
                        break;
                    }
                }
            }
        }

        dispIns(id);
        appendToCurIG(id);
    }
}
#endif
