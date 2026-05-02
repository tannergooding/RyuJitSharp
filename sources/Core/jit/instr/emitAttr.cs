// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.emitAttr;

namespace RyuJitSharp;

public enum emitAttr : uint
{
    EA_UNKNOWN = 0x000,

    EA_1BYTE = 0x001,

    EA_2BYTE = 0x002,

    EA_4BYTE = 0x004,

    EA_8BYTE = 0x008,

    EA_16BYTE = 0x010,

#if TARGET_ARM64
    EA_SCALABLE = 0x020,

    EA_SIZE_MASK = 0x03F,
#elif TARGET_XARCH
    EA_32BYTE = 0x020,

    EA_64BYTE = 0x040,

    EA_SIZE_MASK = 0x07F,
#else
    EA_SIZE_MASK = 0x01F,
#endif

#if TARGET_64BIT
    EA_PTRSIZE = EA_8BYTE,
#else
    EA_PTRSIZE = EA_4BYTE,
#endif

    EA_OFFSET_FLG = 0x080,

    // size == 0
    EA_OFFSET = EA_OFFSET_FLG | EA_PTRSIZE,

    EA_GCREF_FLG = 0x100,

    // size == -1
    EA_GCREF = EA_GCREF_FLG | EA_PTRSIZE,

    EA_BYREF_FLG = 0x200,

    // size == -2
    EA_BYREF = EA_BYREF_FLG | EA_PTRSIZE,

    /// <summary>Is the displacement of the instruction relocatable?</summary>
    EA_DSP_RELOC_FLG = 0x400,

    /// <summary>Is the immediate of the instruction relocatable?</summary>
    EA_CNS_RELOC_FLG = 0x800,

    /// <summary>Is the offset immediate that should be relocatable</summary>
    EA_CNS_SEC_RELOC = 0x1000,

    /// <summary>Is the tlsgd constant to pass to tls_get_addr(). Only on linux/x64/NativeAot</summary>
    EA_CNS_TLSGD_RELOC = 0x2000,
}
