// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    private const int EMIT_MAX_PEEPHOLE_INS_COUNT = 32;

    private enum emitPeepholeResult
    {
        PEEPHOLE_ABORT,
        PEEPHOLE_CONTINUE,
    }

    private interface IPeepholeVisitor<TSelf>
        where TSelf : struct, IPeepholeVisitor<TSelf>
    {
        emitPeepholeResult Visit(instrDesc descriptor);
    }

    private struct UpperBitsZeroPeepholeVisitor : IPeepholeVisitor<UpperBitsZeroPeepholeVisitor>
    {
        private readonly regNumber _reg;
        private readonly emitAttr _size;

        public bool Result { get; private set; }

        public UpperBitsZeroPeepholeVisitor(regNumber reg, emitAttr size)
        {
            _reg = reg;
            _size = size;
        }

        public emitPeepholeResult Visit(instrDesc id)
        {
            if (!emitIsInstrWritingToReg(id, _reg))
            {
                return emitPeepholeResult.PEEPHOLE_CONTINUE;
            }

            switch (id.idIns())
            {
                case INS_call:
                case INS_cwde or INS_cdq or INS_movsx or INS_movsxd:
                {
                    return emitPeepholeResult.PEEPHOLE_ABORT;
                }

                case INS_movzx:
                {
                    if ((_size == EA_1BYTE) || (_size == EA_2BYTE))
                    {
                        Result = id.idOpSize() <= _size;
                    }
                    else if (_size == EA_4BYTE)
                    {
                        // MOVZX always zeroes the upper 32 bits.
                        Result = true;
                    }
                    return emitPeepholeResult.PEEPHOLE_ABORT;
                }

                default:
                {
                    break;
                }
            }

            if (_size == EA_4BYTE)
            {
                Result = id.idOpSize() == EA_4BYTE;
            }
            return emitPeepholeResult.PEEPHOLE_ABORT;
        }
    }

    private struct UpperBitsSignExtendedPeepholeVisitor : IPeepholeVisitor<UpperBitsSignExtendedPeepholeVisitor>
    {
        private readonly regNumber _reg;
        private readonly emitAttr _size;

        public bool Result { get; private set; }

        public UpperBitsSignExtendedPeepholeVisitor(regNumber reg, emitAttr size)
        {
            _reg = reg;
            _size = size;
        }

        public emitPeepholeResult Visit(instrDesc id)
        {
            if (!emitIsInstrWritingToReg(id, _reg))
            {
                return emitPeepholeResult.PEEPHOLE_CONTINUE;
            }

            switch (id.idIns())
            {
                case INS_call:
                {
                    return emitPeepholeResult.PEEPHOLE_ABORT;
                }

                case INS_movsx or INS_movsxd:
                {
                    if ((_size == EA_1BYTE) || (_size == EA_2BYTE))
                    {
                        Result = id.idOpSize() <= _size;
                    }
                    else if (_size == EA_4BYTE)
                    {
                        // MOVSX/MOVSXD always sign-extend to eight bytes (REX.W).
                        Result = true;
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

    private void emitPeepholeIterateLastInstrs<TVisitor>(ref TVisitor visitor)
        where TVisitor : struct, IPeepholeVisitor<TVisitor>
    {
        assert(emitCanPeepholeLastIns());
        if (Configuration.TARGET_XARCH)
        {
            if (!emitGetLastIns(out var curInsIG, out var id))
            {
                return;
            }

            for (var i = 0; i < EMIT_MAX_PEEPHOLE_INS_COUNT; i++)
            {
                assert(id is not null);
                assert(curInsIG is not null);

                switch (visitor.Visit(id))
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

            return;
        }

        var lastIns = emitLastIns;
        if (lastIns is null)
        {
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.PeepholeIterationRequiresALastInstruction);
            return;
        }
        _ = visitor.Visit(lastIns);
    }

    public bool AreUpperBitsZero(regNumber reg, emitAttr size)
    {
        if (!Configuration.TARGET_AMD64)
        {
            return false;
        }

        if (!genIsValidIntReg(reg))
        {
            return false;
        }

        if (!emitCanPeepholeLastIns())
        {
            return false;
        }

        var visitor = new UpperBitsZeroPeepholeVisitor(reg, size);
        emitPeepholeIterateLastInstrs(ref visitor);
        return visitor.Result;
    }

    public bool AreUpperBitsSignExtended(regNumber reg, emitAttr size)
    {
        if (!Configuration.TARGET_AMD64)
        {
            return false;
        }

        if (!genIsValidIntReg(reg))
        {
            return false;
        }

        if (!emitCanPeepholeLastIns())
        {
            return false;
        }

        var visitor = new UpperBitsSignExtendedPeepholeVisitor(reg, size);
        emitPeepholeIterateLastInstrs(ref visitor);
        return visitor.Result;
    }

    public static bool emitIsInstrWritingToReg(instrDesc id, regNumber reg)
    {
        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.InstructionWriteClassificationRequiresXarch);
            return default!;
        }

        assert(genIsValidIntReg(reg));
        var ins = id.idIns();
        var stringDestinationReg = Configuration.TARGET_X86 ? REG_X86_EDI : REG_X64_RDI;
        var stringSourceReg = Configuration.TARGET_X86 ? REG_X86_ESI : REG_X64_RSI;
        var stringCountReg = Configuration.TARGET_X86 ? REG_X86_ECX : REG_X64_RCX;

        var lastImulInstruction = Configuration.TARGET_AMD64 ? INS_imul_31 : INS_imul_SP;
        if (((uint)ins >= (uint)INS_imul_AX) && ((uint)ins <= (uint)lastImulInstruction) &&
            (reg == (regNumber)((uint)ins - (uint)INS_imul_AX)))
        {
            return true;
        }

        switch (ins)
        {
            case INS_call:
            {
                // Conservatively assume that a call writes every register.
                return true;
            }

            case INS_idiv or INS_div or INS_imulEAX or INS_mulEAX:
            {
                if ((reg == TargetRuntimeMetadata.IntegerReturnRegister) ||
                    (reg == TargetRuntimeMetadata.IntegerReturnRegister1))
                {
                    return true;
                }
                break;
            }

            case INS_cmpxchg:
            {
                if (reg == TargetRuntimeMetadata.IntegerReturnRegister)
                {
                    return true;
                }
                break;
            }

            case INS_movsb or INS_movsd or INS_movsq:
            {
                if ((ins == INS_movsq) && !Configuration.TARGET_AMD64)
                {
                    break;
                }

                if ((reg == stringDestinationReg) || (reg == stringSourceReg))
                {
                    return true;
                }
                break;
            }

            case INS_stosb or INS_stosd or INS_stosq:
            {
                if ((ins == INS_stosq) && !Configuration.TARGET_AMD64)
                {
                    break;
                }

                if (reg == stringDestinationReg)
                {
                    return true;
                }
                break;
            }

            case INS_r_movsb or INS_r_movsd or INS_r_movsq:
            {
                if ((ins == INS_r_movsq) && !Configuration.TARGET_AMD64)
                {
                    break;
                }

                if ((reg == stringDestinationReg) || (reg == stringSourceReg) || (reg == stringCountReg))
                {
                    return true;
                }
                break;
            }

            case INS_r_stosb or INS_r_stosd or INS_r_stosq:
            {
                if ((ins == INS_r_stosq) && !Configuration.TARGET_AMD64)
                {
                    break;
                }

                if ((reg == stringDestinationReg) || (reg == stringCountReg))
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

        if (Configuration.TARGET_64BIT)
        {
            switch (ins)
            {
                case INS_cwde:
                {
                    if (reg == TargetRuntimeMetadata.IntegerReturnRegister)
                    {
                        return true;
                    }
                    break;
                }

                case INS_cdq:
                {
                    if (reg == TargetRuntimeMetadata.IntegerReturnRegister1)
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
}
