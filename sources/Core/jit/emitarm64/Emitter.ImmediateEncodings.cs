// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
namespace RyuJitSharp;

public partial class Emitter
{
    private struct bitMaskImm
    {
        public uint immNRS;

        public uint immS
        {
            readonly get
            {
                return immNRS & 0x3F;
            }
            set
            {
                immNRS = (immNRS & ~0x3Fu) | (value & 0x3F);
            }
        }

        public uint immR
        {
            readonly get
            {
                return (immNRS >> 6) & 0x3F;
            }
            set
            {
                immNRS = (immNRS & ~(0x3Fu << 6)) | ((value & 0x3F) << 6);
            }
        }

        public uint immN
        {
            readonly get
            {
                return (immNRS >> 12) & 1;
            }
            set
            {
                immNRS = (immNRS & ~(1u << 12)) | ((value & 1) << 12);
            }
        }
    }

    private struct halfwordImm
    {
        public uint immHWVal;

        public uint immVal
        {
            readonly get
            {
                return immHWVal & 0xFFFF;
            }
            set
            {
                immHWVal = (immHWVal & ~0xFFFFu) | (value & 0xFFFF);
            }
        }

        public uint immHW
        {
            readonly get
            {
                return (immHWVal >> 16) & 3;
            }
            set
            {
                immHWVal = (immHWVal & ~(3u << 16)) | ((value & 3) << 16);
            }
        }
    }

    private struct byteShiftedImm
    {
        public uint immBSVal;

        public uint immVal
        {
            readonly get
            {
                return immBSVal & 0xFF;
            }
            set
            {
                immBSVal = (immBSVal & ~0xFFu) | (value & 0xFF);
            }
        }

        public uint immBY
        {
            readonly get
            {
                return (immBSVal >> 8) & 3;
            }
            set
            {
                immBSVal = (immBSVal & ~(3u << 8)) | ((value & 3) << 8);
            }
        }

        public uint immOnes
        {
            readonly get
            {
                return (immBSVal >> 10) & 1;
            }
            set
            {
                immBSVal = (immBSVal & ~(1u << 10)) | ((value & 1) << 10);
            }
        }
    }

    private static ulong Replicate_helper(ulong value, uint width, emitAttr size)
    {
        var immWidth = getBitWidth(size);
        assert(width <= immWidth);
        var result = value;
        var filledBits = width;
        while (filledBits < immWidth)
        {
            value <<= (int)width;
            result |= value;
            filledBits += width;
        }

        return result;
    }

    private static long emitDecodeBitMaskImm(bitMaskImm bmImm, emitAttr size)
    {
        var N = bmImm.immN;
        var R = bmImm.immR;
        var S = bmImm.immS;
        uint elemWidth = 64;
        if (N == 0)
        {
            elemWidth = 32;
            for (var bitNum = 5; bitNum > 0; bitNum--)
            {
                var oneBit = elemWidth;
                if ((S & oneBit) == 0)
                {
                    break;
                }
                elemWidth /= 2;
            }
        }
        else
        {
            assert(size == EA_8BYTE);
        }

        var maskSR = elemWidth - 1;
        S &= maskSR;
        R &= maskSR;
        S++;
        assert(S < elemWidth);

        var welem = (1UL << (int)S) - 1;
        var wmask = ROR_helper(welem, (int)R, (int)elemWidth);
        wmask = Replicate_helper(wmask, elemWidth, size);

        return unchecked((long)wmask);
    }

    private static long emitDecodeHalfwordImm(halfwordImm hwImm, emitAttr size)
    {
        assert(isValidGeneralDatasize(size));
        var hw = hwImm.immHW;
        var val = (long)hwImm.immVal;
        assert((hw <= 1) || (size == EA_8BYTE));
        var result = val << (16 * (int)hw);

        return result;
    }

    private static uint emitDecodeByteShiftedImm(byteShiftedImm bsImm, emitAttr size)
    {
        var onesShift = bsImm.immOnes == 1;
        var bySh = bsImm.immBY;
        var result = bsImm.immVal;
        if (bySh > 0)
        {
            assert((size == EA_2BYTE) || (size == EA_4BYTE));
            if (size == EA_2BYTE)
            {
                assert(bySh < 2);
            }
            else
            {
                assert(bySh < 4);
            }
            result <<= 8 * (int)bySh;
            if (onesShift)
            {
                result |= (1u << (8 * (int)bySh)) - 1;
            }
        }

        return result;
    }

    private static bool isValidImmBSVal(nuint value, emitAttr size)
    {
        return (value >= 0) && (value < 0x800);
    }

    private static bool isGeneralRegisterOrSP(regNumber reg)
    {
        return isGeneralRegister(reg) || (reg == REG_SP);
    }

    private static regNumber encodingSPtoZR(regNumber reg)
    {
        return (reg == REG_SP) ? REG_ZR : reg;
    }

    private static instruction insReverse(instruction ins)
    {
        return ins switch
        {
            INS_add => INS_sub,
            INS_adds => INS_subs,
            INS_sub => INS_add,
            INS_subs => INS_adds,
            INS_cmp => INS_cmn,
            INS_cmn => INS_cmp,
            INS_ccmp => INS_ccmn,
            INS_ccmn => INS_ccmp,
            _ => INS_invalid,
        };
    }
}
#endif
