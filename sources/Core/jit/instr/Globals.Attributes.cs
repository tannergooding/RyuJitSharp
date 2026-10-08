// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static partial class Globals
{
    public static emitAttr EA_ATTR(int value) => (emitAttr)value;

    public static emitAttr EA_ATTR(uint value) => (emitAttr)value;

    public const emitAttr EA_4BYTE_DSP_RELOC = EA_4BYTE | EA_DSP_RELOC_FLG;

    public const emitAttr EA_PTR_DSP_RELOC = EA_PTRSIZE | EA_DSP_RELOC_FLG;

    public const emitAttr EA_HANDLE_CNS_RELOC = EA_PTRSIZE | EA_CNS_RELOC_FLG;

    public static emitAttr EA_SET_FLG(emitAttr attr, emitAttr flags) => (emitAttr)((int)attr | (int)flags);

    public static emitAttr EA_REMOVE_FLG(emitAttr attr, emitAttr flags) => attr & ~flags;

    public static emitAttr EA_SIZE(emitAttr attr) => attr & EA_SIZE_MASK;

    public static uint EA_SIZE_IN_BYTES(emitAttr attr) => (uint)EA_SIZE(attr);

    public static bool EA_IS_OFFSET(emitAttr attr) => (attr & EA_OFFSET_FLG) != 0;

    public static bool EA_IS_GCREF(emitAttr attr) => (attr & EA_GCREF_FLG) != 0;

    public static bool EA_IS_BYREF(emitAttr attr) => (attr & EA_BYREF_FLG) != 0;

    public static bool EA_IS_GCREF_OR_BYREF(emitAttr attr) => (attr & (EA_BYREF_FLG | EA_GCREF_FLG)) != 0;

    public static bool EA_IS_DSP_RELOC(emitAttr attr) => (attr & EA_DSP_RELOC_FLG) != 0;

    public static bool EA_IS_CNS_RELOC(emitAttr attr) => (attr & EA_CNS_RELOC_FLG) != 0;

    public static bool EA_IS_CNS_SEC_RELOC(emitAttr attr) => (attr & EA_CNS_SEC_RELOC) != 0;

    public static bool EA_IS_CNS_TLSGD_RELOC(emitAttr attr) => (attr & EA_CNS_TLSGD_RELOC) != 0;

    public static bool EA_IS_RELOC(emitAttr attr) => EA_IS_DSP_RELOC(attr) || EA_IS_CNS_RELOC(attr);

    public static emitAttr EA_TYPE(emitAttr attr) => attr & ~(EA_OFFSET_FLG | EA_DSP_RELOC_FLG | EA_CNS_RELOC_FLG);
}
