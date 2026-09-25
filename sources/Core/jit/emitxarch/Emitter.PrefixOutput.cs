// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
namespace RyuJitSharp;

public partial class Emitter
{
    public unsafe uint emitOutputRexOrSimdPrefixIfNeeded(instruction ins, byte* dst, ref ulong code)
    {
        if (hasEvexPrefix(code))
        {
            var evexPrefix = emitExtractEvexPrefix(ins, ref code);
            assert(evexPrefix != 0);
            _ = emitOutputByte(dst, (long)((evexPrefix >> 24) & 0xFF));
            _ = emitOutputByte(dst + 1, (long)((evexPrefix >> 16) & 0xFF));
            _ = emitOutputByte(dst + 2, (long)((evexPrefix >> 8) & 0xFF));
            _ = emitOutputByte(dst + 3, (long)(evexPrefix & 0xFF));

            return 4;
        }
        #endif
        else if (hasVexPrefix(code))
        {
            var vexPrefix = emitExtractVexPrefix(ins, ref code);
            assert(vexPrefix != 0);

            // C5 requires inverted X/B set, W clear and map 1. R/vvvv/L/pp
            // remain unrestricted: native mask 0xFFFF7F80 selects just those constraints.
            if ((vexPrefix & 0xFFFF7F80) == 0x00C46100)
            {
                _ = emitOutputByte(dst, 0xC5);
                _ = emitOutputByte(dst + 1, (long)(((vexPrefix >> 8) & 0x80) | (vexPrefix & 0x7F)));

                return 2;
            }
            _ = emitOutputByte(dst, (long)((vexPrefix >> 16) & 0xFF));
            _ = emitOutputByte(dst + 1, (long)((vexPrefix >> 8) & 0xFF));
            _ = emitOutputByte(dst + 2, (long)(vexPrefix & 0xFF));

            return 3;
        }
        else if (hasRex2Prefix(code))
        {
            var rex2Prefix = (ushort)((code >> 32) & 0xFFFF);
            noway_assert((rex2Prefix >= 0xD500) && (rex2Prefix <= 0xD5FF));
            code &= 0xFFFFFFFF;
            uint emittedSize = 0;
            if ((code & 0xFF) == 0x0F)
            {
                // REX2 carries map 1, replacing the 0F byte in XX0F opcodes.
                code >>= 8;
            }

            var check = (byte)((code >> 24) & 0xFF);
            if (check == 0)
            {
                check = (byte)((code >> 16) & 0xFF);
                if ((check != 0) && isPrefix(check))
                {
                    code &= 0xFF00FFFF;
                    emittedSize += emitOutputByte(dst, check);
                    dst++;
                }
                if (check == 0x0F)
                {
                    code &= 0xFF00FFFF;
                }
            }
            else
            {
                var check2 = (byte)((code >> 16) & 0xFF);
                if (isPrefix(check2))
                {
                    assert(!isPrefix(check));
                    if (isPrefix(check))
                    {
                        code &= 0xFFFF;
                        emittedSize += emitOutputByte(dst, check2);
                        dst++;
                        emittedSize += emitOutputByte(dst, check);
                        dst++;
                    }
                    else
                    {
                        code &= 0xFFFF;
                        emittedSize += emitOutputByte(dst, check2);
                        dst++;
                    }
                }
            }

            emittedSize += emitOutputByte(dst, (rex2Prefix >> 8) & 0xFF);
            emittedSize += emitOutputByte(dst + 1, rex2Prefix & 0xFF);

            return emittedSize;
        }

#if TARGET_AMD64
        if (code > 0xFFFFFFFF)
        {
            var prefix = (byte)((code >> 32) & 0xFF);
            noway_assert((prefix >= 0x40) && (prefix <= 0x4F));
            code &= 0xFFFFFFFF;

            // REX must follow legacy prefixes embedded in the opcode. Preserve the
            // native 00113322 / 22114433 layout while moving a prefix into the output.
            var check = (byte)((code >> 24) & 0xFF);
            if (check == 0)
            {
                check = (byte)((code >> 16) & 0xFF);
                if ((check != 0) && isPrefix(check))
                {
                    code = ((ulong)prefix << 16) | (code & 0xFFFF);

                    return emitOutputByte(dst, check);
                }
            }
            else
            {
                var check2 = (byte)((code >> 16) & 0xFF);
                if (isPrefix(check2))
                {
                    assert(!isPrefix(check));
                    if (isPrefix(check))
                    {
                        code = ((ulong)prefix << 24) | ((ulong)check << 16) | (code & 0xFFFF);
                    }
                    else
                    {
                        code = ((ulong)check << 24) | ((ulong)prefix << 16) | (code & 0xFFFF);
                    }

                    return emitOutputByte(dst, check2);
                }
            }

            return emitOutputByte(dst, prefix);
        }
#endif

        return 0;
    }
}
