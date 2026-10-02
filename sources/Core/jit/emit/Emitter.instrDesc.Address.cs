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
#endif

#if TARGET_XARCH
            [FieldOffset(0)]
            public bool iiaSecRel;
#endif
        }

        public ref idAddrUnion idAddr()
        {
            assert(!idIsSmallDsc());
            return ref _idAddrUnion;
        }
    }
}
