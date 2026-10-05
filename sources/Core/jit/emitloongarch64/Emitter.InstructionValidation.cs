// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64
namespace RyuJitSharp;

public partial class Emitter
{
    public static bool isValidSimm20(nint value)
    {
        const int ImmediateLimit = 1 << 19;
        return (-ImmediateLimit <= value) && (value < ImmediateLimit);
    }

    public static uint getBitWidth(emitAttr size)
    {
        assert((int)size <= (int)EA_8BYTE);
        return unchecked((uint)size * (uint)BITS_PER_BYTE);
    }

    public static bool isGeneralRegisterOrR0(regNumber reg)
    {
        return (reg >= REG_FIRST) && (reg <= REG_INT_LAST);
    }

    public static bool isFloatReg(regNumber reg)
    {
        return (reg >= REG_FP_FIRST) && (reg <= REG_FP_LAST);
    }

#if FEATURE_SIMD
    public static bool isVectorRegister(regNumber reg)
    {
        return isFloatReg(reg);
    }

    public static bool isValidVectorDatasize(emitAttr size)
    {
        return (size == EA_8BYTE) || (size == EA_16BYTE) || (size == EA_32BYTE);
    }

    public static bool isValidVectorElemsize(emitAttr size)
    {
        return (size == EA_16BYTE) || (size == EA_8BYTE) || (size == EA_4BYTE) || (size == EA_2BYTE) ||
               (size == EA_1BYTE);
    }
#endif
}
#endif
