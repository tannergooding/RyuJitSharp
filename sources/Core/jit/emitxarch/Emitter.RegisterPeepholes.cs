// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_AMD64
    private const int EMIT_MAX_PEEPHOLE_INS_COUNT = 32;

    private enum emitPeepholeResult
    {
        PEEPHOLE_ABORT,
        PEEPHOLE_CONTINUE,
    }

    private bool emitGetLastIns(out insGroup? group, out instrDesc? descriptor)
    {
        for (var ig = emitIGlast; ig is not null; ig = ig.igPrev)
        {
            if (ig.igLastIns is not null)
            {
                group = ig;
                descriptor = ig.igLastIns;
                return true;
            }
        }

        group = null;
        descriptor = null;
        return false;
    }

    private bool emitPrevID(ref insGroup? ig, ref instrDesc? id)
    {
        assert(ig is not null);
        assert(id is not null);
        var idPrevSize = id.idPrevSize();

        if (idPrevSize != 0)
        {
            // Managed storage preserves native descriptor order and logical offsets.
            // Active groups use the recording buffer; saved groups use their snapshot.
            var previousIndex = id.StorageIndex - 1;
            instrDesc previous;
            if ((ig == emitCurIG) && emitCurIGnonEmpty())
            {
                assert(emitCurIGfreeBase is not null);
                previous = emitCurIGfreeBase[previousIndex];
            }
            else
            {
                assert(ig.igData is not null);
                previous = ig.igData[previousIndex];
            }

            assert(previous.StorageOffset + idPrevSize == id.StorageOffset);
            id = previous;
            return true;
        }

        for (ig = ig.igPrev; ig is not null; ig = ig.igPrev)
        {
            if (ig.igLastIns is not null)
            {
                assert(ig.igInsCnt > 0);
                id = ig.igLastIns;
                return true;
            }

            assert(ig.igInsCnt == 0);
        }

        return false;
    }

    private void emitPeepholeIterateLastInstrs(Func<instrDesc, emitPeepholeResult> action)
    {
        assert(emitCanPeepholeLastIns());
        if (!emitGetLastIns(out var curInsIG, out var id))
        {
            return;
        }

        for (var i = 0; i < EMIT_MAX_PEEPHOLE_INS_COUNT; i++)
        {
            assert(id is not null);
            assert(curInsIG is not null);

            switch (action(id))
            {
                case emitPeepholeResult.PEEPHOLE_ABORT:
                {
                    return;
                }

                case emitPeepholeResult.PEEPHOLE_CONTINUE:
                {
                    var savedInsIG = curInsIG;
                    if (emitPrevID(ref curInsIG, ref id))
                    {
                        assert(curInsIG is not null);
                        if (isInsIGSafeForPeepholeOptimization(curInsIG, savedInsIG))
                        {
                            continue;
                        }
                        else
                        {
                            return;
                        }
                    }
                    return;
                }

                default:
                {
                    unreached();
                    break;
                }
            }
        }
    }

    public bool AreUpperBitsZero(regNumber reg, emitAttr size)
    {
        if (!genIsValidIntReg(reg))
        {
            return false;
        }

        if (!emitCanPeepholeLastIns())
        {
            return false;
        }

        var result = false;
        emitPeepholeIterateLastInstrs(id =>
        {
            if (emitIsInstrWritingToReg(id, reg))
            {
                switch (id.idIns())
                {
                    case INS_call:
                    case INS_cwde or INS_cdq or INS_movsx or INS_movsxd:
                    {
                        return emitPeepholeResult.PEEPHOLE_ABORT;
                    }

                    case INS_movzx:
                    {
                        if ((size == EA_1BYTE) || (size == EA_2BYTE))
                        {
                            result = id.idOpSize() <= size;
                        }
                        else if (size == EA_4BYTE)
                        {
                            // MOVZX always zeroes the upper 32 bits.
                            result = true;
                        }
                        return emitPeepholeResult.PEEPHOLE_ABORT;
                    }

                    default:
                    {
                        break;
                    }
                }

                if (size == EA_4BYTE)
                {
                    result = id.idOpSize() == EA_4BYTE;
                }
                return emitPeepholeResult.PEEPHOLE_ABORT;
            }
            else
            {
                return emitPeepholeResult.PEEPHOLE_CONTINUE;
            }
        });

        return result;
    }

    public bool AreUpperBitsSignExtended(regNumber reg, emitAttr size)
    {
        if (!genIsValidIntReg(reg))
        {
            return false;
        }

        if (!emitCanPeepholeLastIns())
        {
            return false;
        }

        var result = false;
        emitPeepholeIterateLastInstrs(id =>
        {
            if (emitIsInstrWritingToReg(id, reg))
            {
                switch (id.idIns())
                {
                    case INS_call:
                    {
                        return emitPeepholeResult.PEEPHOLE_ABORT;
                    }

                    case INS_movsx or INS_movsxd:
                    {
                        if ((size == EA_1BYTE) || (size == EA_2BYTE))
                        {
                            result = id.idOpSize() <= size;
                        }
                        else if (size == EA_4BYTE)
                        {
                            // MOVSX/MOVSXD always sign-extend to eight bytes (REX.W).
                            result = true;
                        }
                        break;
                    }

                    default:
                    {
                        break;
                    }
                }
                return emitPeepholeResult.PEEPHOLE_ABORT;
            }
            else
            {
                return emitPeepholeResult.PEEPHOLE_CONTINUE;
            }
        });

        return result;
    }

    public static bool emitIsInstrWritingToReg(instrDesc id, regNumber reg)
    {
        assert(genIsValidIntReg(reg));
        var ins = id.idIns();

        switch (ins)
        {
            case INS_call:
            {
                // Conservatively assume that a call writes every register.
                return true;
            }

            case >= INS_imul_AX and <= INS_imul_31:
            {
                if (reg == (regNumber)(ins - INS_imul_AX))
                {
                    return true;
                }
                break;
            }

            case INS_idiv or INS_div or INS_imulEAX or INS_mulEAX:
            {
                if ((reg == REG_RAX) || (reg == REG_RDX))
                {
                    return true;
                }
                break;
            }

            case INS_cmpxchg:
            {
                if (reg == REG_RAX)
                {
                    return true;
                }
                break;
            }

            case INS_movsb or INS_movsd or INS_movsq:
            {
                if ((reg == REG_RDI) || (reg == REG_RSI))
                {
                    return true;
                }
                break;
            }

            case INS_stosb or INS_stosd or INS_stosq:
            {
                if (reg == REG_RDI)
                {
                    return true;
                }
                break;
            }

            case INS_r_movsb or INS_r_movsd or INS_r_movsq:
            {
                if ((reg == REG_RDI) || (reg == REG_RSI) || (reg == REG_RCX))
                {
                    return true;
                }
                break;
            }

            case INS_r_stosb or INS_r_stosd or INS_r_stosq:
            {
                if ((reg == REG_RDI) || (reg == REG_RCX))
                {
                    return true;
                }
                break;
            }

            default:
            {
                break;
            }
        }

        switch (ins)
        {
            case INS_cwde:
            {
                if (reg == REG_RAX)
                {
                    return true;
                }
                break;
            }

            case INS_cdq:
            {
                if (reg == REG_RDX)
                {
                    return true;
                }
                break;
            }

            default:
            {
                break;
            }
        }

        if (id.idIsReg1Write() && (id.idReg1() == reg))
        {
            return true;
        }

        if (id.idIsReg2Write() && (id.idReg2() == reg))
        {
            return true;
        }

        assert(!id.idIsReg3Write());
        assert(!id.idIsReg4Write());
        return false;
    }
#endif
}
