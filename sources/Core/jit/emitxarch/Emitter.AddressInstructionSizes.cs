// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_AMD64
    private static bool baseRegisterRequiresSibByte(regNumber reg)
    {
        return reg is REG_ESP or REG_R12 or REG_R20 or REG_R28;
    }

    private static bool baseRegisterRequiresDisplacement(regNumber reg)
    {
        return reg is REG_EBP or REG_R13 or REG_R21 or REG_R29;
    }

    private static bool isPrefetch(instruction ins)
    {
        return ins is INS_prefetcht0 or INS_prefetcht1 or INS_prefetcht2 or INS_prefetchnta;
    }

    private static bool insIsCMOV(instruction ins)
    {
        return (ins >= INS_cmovo) && (ins <= INS_cmovg);
    }

    public static emitAttr emitDecodeScale(uint ensz)
    {
        assert(ensz < 4);
        return emitSizeDecode[(int)ensz];
    }

    public uint emitInsSizeAM(instrDesc id, ulong code)
    {
        assert(id.idIns() != INS_invalid);
        var ins = id.idIns();
        var attrSize = id.idOpSize();
        // The displacement field is in an unusual place for (tail-)calls.
        var dsp = (ins is INS_call or INS_tail_i_jmp) ? emitGetInsCIdisp(id) : emitGetInsAmdAny(id);
        var dspInByte = unchecked((sbyte)dsp) == dsp;
        var dspIsZero = dsp == 0;
        regNumber reg;
        regNumber rgx;

        // Only address-mode formats use this arm of the address union. The label
        // and generic-memory RMW formats below also reach this sizing routine.
        if (id.idHasMemAdr())
        {
            reg = id.idAddr().iiaAddrMode.amBaseReg;
            rgx = id.idAddr().iiaAddrMode.amIndxReg;
        }
        else
        {
            reg = REG_NA;
            rgx = REG_NA;
#if DEBUG
            switch (id.idInsFmt())
            {
                case IF_RWR_LABEL or IF_MRW_CNS or IF_MRW_RRD or IF_MRW_SHF:
                {
                    break;
                }

                default:
                {
                    assert(false, "Unexpected insFormat in emitInsSizeAMD");
                    reg = id.idAddr().iiaAddrMode.amBaseReg;
                    rgx = id.idAddr().iiaAddrMode.amIndxReg;
                    break;
                }
            }
#endif
        }

        if (id.idIsDspReloc())
        {
            // Relocations cannot use disp8 or assume the displacement remains zero.
            dspInByte = false;
            dspIsZero = false;
        }
        else if (IsEvexEncodableInstruction(ins))
        {
            if (TryEvexCompressDisp8Byte(id, dsp, out _, out dspInByte) && hasTupleTypeInfo(ins))
            {
                SetEvexCompressedDisplacement(id);
            }
        }

        uint size;
        if ((code & 0xFF000000) != 0)
        {
            size = 4;
        }
        else if ((code & 0x00FF0000) != 0)
        {
            // BT supports 16-bit operands, but this path does not handle its 66 prefix.
            assert(ins != INS_bt);
            assert((attrSize == EA_4BYTE) || (attrSize == EA_PTRSIZE) ||
                (attrSize == EA_16BYTE) || (attrSize == EA_32BYTE) || (attrSize == EA_64BYTE) ||
                (ins is INS_movzx or INS_movsx or INS_vmovsh or INS_cmpxchg) ||
                IsKMOVInstruction(ins) || isPrefetch(ins) || insIsCMOV(ins));
            size = ((attrSize == EA_2BYTE) && (ins == INS_cmpxchg)) ? 4u : 3u;
        }
        else
        {
            size = 2;
        }

        size += emitGetAdjustedSize(id, code);

        if (hasRexPrefix(code))
        {
            size += emitGetRexPrefixSize(id, ins);
        }
        else if (TakesRexWPrefix(id))
        {
            size += emitGetRexPrefixSize(id, ins);
        }
        else if (IsExtendedReg(reg, EA_PTRSIZE) || IsExtendedReg(rgx, EA_PTRSIZE) ||
            ((ins != INS_call) && (IsExtendedReg(id.idReg1(), attrSize) || IsExtendedReg(id.idReg2(), attrSize))))
        {
            size += emitGetRexPrefixSize(id, ins);
        }

        if (rgx == REG_NA)
        {
            // [reg+disp]
            if (reg == REG_NA)
            {
                // [disp] uses disp32; non-relocatable addresses also require SIB.
                size += sizeof(int);
                if (!id.idIsDspReloc())
                {
                    size++;
                }

                return size;
            }

            if ((ins is INS_call or INS_tail_i_jmp) && id.idIsCallRegPtr())
            {
                assert(dsp == 0);
                return size;
            }

            if (baseRegisterRequiresSibByte(reg))
            {
                size++;
            }

            if (dspIsZero && !baseRegisterRequiresDisplacement(reg))
            {
                return size;
            }

            size += dspInByte ? 1u : sizeof(int);
        }
        else
        {
            size++;

            if (emitDecodeScale(id.idAddr().iiaAddrMode.amScale) > EA_1BYTE)
            {
                if (reg != REG_NA)
                {
                    // [reg + {2/4/8} * rgx + disp]
                    if (dspIsZero && !baseRegisterRequiresDisplacement(reg))
                    {
                        // No displacement is required.
                    }
                    else
                    {
                        size += dspInByte ? 1u : sizeof(int);
                    }
                }
                else
                {
                    // [{2/4/8} * rgx + disp] always requires disp32.
                    size += sizeof(int);
                }
            }
            else
            {
                // When we are using the SIB or VSIB format with EBP or R13 as a base, we must emit at least
                // a 1 byte displacement (this is a special case in the encoding to allow for the case of no
                // base register at all). In order to avoid this when we have no scaling, we can reverse the
                // registers so that we don't have to add that extra byte. However, we can't do that if the
                // index register is a vector, such as for a gather instruction.
                if (dspIsZero && baseRegisterRequiresDisplacement(reg) &&
                    !baseRegisterRequiresDisplacement(rgx) && !rgx.IsFltReg)
                {
                    var tmp = reg;
                    id.idAddr().iiaAddrMode.amBaseReg = reg = rgx;
                    id.idAddr().iiaAddrMode.amIndxReg = rgx = tmp;
                }

                // [reg + rgx + disp]
                if (dspIsZero && !baseRegisterRequiresDisplacement(reg))
                {
                    // No displacement is required.
                }
                else
                {
                    size += dspInByte ? 1u : sizeof(int);
                }
            }
        }

        return size;
    }

    public uint emitInsSizeAM(instrDesc id, ulong code, int val)
    {
        assert(id.idIns() != INS_invalid);
        var ins = id.idIns();
        var valSize = EA_SIZE_IN_BYTES(id.idOpSize());
        var valInByte = ImmCanUseSByteEncoding(ins, val);

        // BT mem,reg has poor performance; BT mem,imm would need special handling
        // because its immediate is always encoded in a byte.
        assert(ins != INS_bt);
        noway_assert((valSize <= sizeof(int)) || !id.idIsCnsReloc());

        if (valSize > sizeof(int))
        {
            valSize = sizeof(int);
        }

        if (id.idIsCnsReloc())
        {
            valInByte = false;
            assert(valSize == sizeof(int));
        }

        if (valInByte)
        {
            valSize = 1;
        }
        else
        {
            assert(!IsSimdInstruction(ins));
        }

        return valSize + emitInsSizeAM(id, code);
    }
#endif
}
