// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static partial class Globals
{
    public static emitAttr EA_SIZE(emitAttr attr) => attr & EA_SIZE_MASK;

    public static bool EA_IS_GCREF(emitAttr attr) => (attr & EA_GCREF_FLG) != 0;

    public static bool EA_IS_BYREF(emitAttr attr) => (attr & EA_BYREF_FLG) != 0;

    public static bool EA_IS_DSP_RELOC(emitAttr attr) => (attr & EA_DSP_RELOC_FLG) != 0;

    public static bool EA_IS_CNS_RELOC(emitAttr attr) => (attr & EA_CNS_RELOC_FLG) != 0;
}
