// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.InteropServices;

namespace RyuJitSharp;

public partial class Emitter
{
    public abstract partial class instrDesc
    {
        private idAddrUnion _idAddrUnion;

#if TARGET_64BIT
        [StructLayout(LayoutKind.Explicit, Size = 8)]
#else
        [StructLayout(LayoutKind.Explicit, Size = 4)]
#endif
        public partial struct idAddrUnion
        {
            [FieldOffset(0)]
            internal int iiaEncodedInstrCount;

#if TARGET_LOONGARCH64
            [FieldOffset(4)]
#else
            [FieldOffset(0)]
#endif
            public emitLclVarAddr iiaLclVar;

#if TARGET_XARCH
            [FieldOffset(0)]
            public emitAddrMode iiaAddrMode;
#endif

#if TARGET_XARCH || TARGET_ARM
            [FieldOffset(0)]
            internal uint iiaRegisterBits;
#elif TARGET_ARM64
            [FieldOffset(4)]
            internal uint iiaRegisterBits;

            [FieldOffset(0)]
            internal insSvePattern iiaSvePattern;
#elif TARGET_RISCV64
            // Two six-bit register fields precede the aligned instruction code.
            [FieldOffset(4)]
            public uint iiaInstrEncode;
#endif

#if TARGET_XARCH
            [FieldOffset(0)]
            public bool iiaSecRel;
#endif

            public readonly bool iiaHasInstrCount()
            {
                return (iiaEncodedInstrCount & (int)iaut_MASK) == (int)iaut_INST_COUNT;
            }

            public readonly int iiaGetInstrCount()
            {
                assert(iiaHasInstrCount());
                return iiaEncodedInstrCount >> (int)iaut_SHIFT;
            }

            public void iiaSetInstrCount(int count)
            {
                assert(count is > -10 and < 10);
                iiaEncodedInstrCount = (count << (int)iaut_SHIFT) | (int)iaut_INST_COUNT;
            }
        }

        public ref idAddrUnion idAddr()
        {
            assert(!idIsSmallDsc());
            return ref _idAddrUnion;
        }
    }
}
