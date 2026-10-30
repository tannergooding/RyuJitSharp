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

    public static emitAttr EA_PTRSIZE => Configuration.TARGET_64BIT ? EA_64BIT_PTRSIZE : EA_32BIT_PTRSIZE;

    public static emitAttr EA_OFFSET => Configuration.TARGET_64BIT ? EA_64BIT_OFFSET : EA_32BIT_OFFSET;

    public static emitAttr EA_GCREF => Configuration.TARGET_64BIT ? EA_64BIT_GCREF : EA_32BIT_GCREF;

    public static emitAttr EA_BYREF => Configuration.TARGET_64BIT ? EA_64BIT_BYREF : EA_32BIT_BYREF;

    public static emitAttr EA_SIZE_MASK =>
        Configuration.TARGET_ARM64 ? EA_ARM64_SIZE_MASK :
        Configuration.TARGET_XARCH ? EA_XARCH_SIZE_MASK :
        EA_GENERIC_SIZE_MASK;

    public static emitAttr EA_PTR_DSP_RELOC => EA_PTRSIZE | EA_DSP_RELOC_FLG;

    public static emitAttr EA_HANDLE_CNS_RELOC => EA_PTRSIZE | EA_CNS_RELOC_FLG;

    public const emitAttr EA_SCALABLE_ARM64 = (emitAttr)0x020;

    public static emitAttr EA_SET_FLG(emitAttr attr, emitAttr flags) => (emitAttr)((int)attr | (int)flags);

    public static emitAttr EA_SIZE(emitAttr attr) => attr & EA_SIZE_MASK;

    public static uint EA_SIZE_IN_BYTES(emitAttr attr) => (uint)EA_SIZE(attr);

    public static bool EA_IS_GCREF(emitAttr attr) => (attr & EA_GCREF_FLG) != 0;

    public static bool EA_IS_BYREF(emitAttr attr) => (attr & EA_BYREF_FLG) != 0;

    public static bool EA_IS_GCREF_OR_BYREF(emitAttr attr) => (attr & (EA_BYREF_FLG | EA_GCREF_FLG)) != 0;

    public static bool EA_IS_DSP_RELOC(emitAttr attr) => (attr & EA_DSP_RELOC_FLG) != 0;

    public static bool EA_IS_CNS_RELOC(emitAttr attr) => (attr & EA_CNS_RELOC_FLG) != 0;

    public static bool EA_IS_CNS_SEC_RELOC(emitAttr attr) => (attr & EA_CNS_SEC_RELOC) != 0;

    public static bool EA_IS_CNS_TLSGD_RELOC(emitAttr attr) => (attr & EA_CNS_TLSGD_RELOC) != 0;

    public static bool EA_IS_RELOC(emitAttr attr) => EA_IS_DSP_RELOC(attr) || EA_IS_CNS_RELOC(attr);
}
