// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
namespace RyuJitSharp;

public partial class Emitter
{
    // emit.h:624-635 packs both eight-bit register numbers, two scale bits,
    // and the signed 14-bit displacement into the descriptor's address word.
    public struct emitAddrMode
    {
        private uint _bits;

        public regNumber amBaseReg
        {
            readonly get
            {
                return (regNumber)(_bits & 0xFF);
            }
            set
            {
                _bits = (_bits & ~0xFFu) | ((uint)value & 0xFF);
            }
        }

        public regNumber amIndxReg
        {
            readonly get
            {
                return (regNumber)((_bits >> 8) & 0xFF);
            }
            set
            {
                _bits = (_bits & ~(0xFFu << 8)) | (((uint)value & 0xFF) << 8);
            }
        }

        public uint amScale
        {
            readonly get
            {
                return (_bits >> 16) & 3;
            }
            set
            {
                _bits = (_bits & ~(3u << 16)) | ((value & 3) << 16);
            }
        }

        public int amDisp
        {
            readonly get
            {
                return unchecked((int)_bits) >> 18;
            }
            set
            {
                _bits = (_bits & 0x3_FFFFu) | (unchecked((uint)value) << 18);
            }
        }
    }
}
#endif
