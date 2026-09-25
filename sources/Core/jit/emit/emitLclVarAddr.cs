// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public struct emitLclVarAddr
{
    // emit.h: emitLclVarAddr uses 15 variable bits, 15 extra bits and a two-bit
    // tag. Large local numbers borrow seven extra bits, leaving eight for offset.
    private uint _data;

    private enum LclVarAddrTag : uint
    {
        LVA_STANDARD_ENCODING,
        LVA_LARGE_OFFSET,
        LVA_COMPILER_TEMP,
        LVA_LARGE_VARNUM,
    }

    public void initLclVarAddr(int varNum, uint offset)
    {
        LclVarAddrTag tag;
        uint variable;
        uint extra;

        if (varNum < 32768)
        {
            if (varNum >= 0)
            {
                if (offset < 32768)
                {
                    tag = LclVarAddrTag.LVA_STANDARD_ENCODING;
                    extra = offset;
                    variable = (uint)varNum;
                }
                else
                {
                    // Larger offsets would require giving up more variable-number bits.
                    if (offset >= 65536)
                    {
                        IMPL_LIMITATION("JIT doesn't support offsets larger than 65535 into valuetypes\n");
                    }

                    tag = LclVarAddrTag.LVA_LARGE_OFFSET;
                    extra = offset - 32768;
                    variable = (uint)varNum;
                }
            }
            else
            {
                if (varNum < -32767)
                {
                    IMPL_LIMITATION("JIT doesn't support more than 32767 Compiler Spill temps\n");
                }
                if (offset > 32767)
                {
                    IMPL_LIMITATION("JIT doesn't support offsets larger than 32767 into valuetypes for Compiler Spill temps\n");
                }

                tag = LclVarAddrTag.LVA_COMPILER_TEMP;
                extra = offset;
                variable = (uint)-varNum;
            }
        }
        else
        {
            if (offset >= 256)
            {
                IMPL_LIMITATION("JIT doesn't support offsets larger than 255 into valuetypes for local vars > 32767\n");
            }
            if (varNum >= 0x00400000)
            {
                IMPL_LIMITATION("JIT doesn't support more than 2^22 variables\n");
            }

            tag = LclVarAddrTag.LVA_LARGE_VARNUM;
            variable = (uint)varNum & 0x00007FFF;
            extra = ((uint)varNum & 0x003F8000) >> 15;
            extra |= offset << 7;
        }

        _data = variable | (extra << 15) | ((uint)tag << 30);
    }

    public readonly int lvaVarNum()
    {
        var variable = _data & 0x7FFF;
        var extra = (_data >> 15) & 0x7FFF;
        var tag = (LclVarAddrTag)(_data >> 30);

        switch (tag)
        {
            case LclVarAddrTag.LVA_COMPILER_TEMP:
            {
                return -(int)variable;
            }

            case LclVarAddrTag.LVA_LARGE_VARNUM:
            {
                return (int)(((extra & 0x007F) << 15) + variable);
            }

            default:
            {
                assert(tag is LclVarAddrTag.LVA_STANDARD_ENCODING or LclVarAddrTag.LVA_LARGE_OFFSET);
                return (int)variable;
            }
        }
    }

    public readonly uint lvaOffset()
    {
        var extra = (_data >> 15) & 0x7FFF;
        var tag = (LclVarAddrTag)(_data >> 30);

        switch (tag)
        {
            case LclVarAddrTag.LVA_LARGE_OFFSET:
            {
                return 32768 + extra;
            }

            case LclVarAddrTag.LVA_LARGE_VARNUM:
            {
                return (extra & 0x7F80) >> 7;
            }

            default:
            {
                assert(tag is LclVarAddrTag.LVA_STANDARD_ENCODING or LclVarAddrTag.LVA_COMPILER_TEMP);
                return extra;
            }
        }
    }
}
