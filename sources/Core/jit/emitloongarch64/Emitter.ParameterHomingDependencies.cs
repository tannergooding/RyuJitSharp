// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64
namespace RyuJitSharp;

public partial class Emitter
{
    // Arithmetic right shift rejects negative values and bits outside the unsigned immediate field.
    public static bool isValidUimm11(nint value)
    {
        return (value >> 11) == 0;
    }

    public static bool isValidUimm12(nint value)
    {
        return (value >> 12) == 0;
    }

    public static bool isValidSimm12(nint value)
    {
        return (-2048 <= value) && (value < 2048);
    }

    public static bool isGeneralRegister(regNumber reg)
    {
        return (reg >= Globals.REG_INT_FIRST) && (reg <= Globals.REG_INT_LAST);
    }

    public void emitIns_I_la(emitAttr attr, regNumber reg, nint immediate)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "LoongArch64 address constant recording is not ported.");
    }
}
#endif
