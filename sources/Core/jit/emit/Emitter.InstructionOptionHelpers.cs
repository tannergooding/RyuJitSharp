// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    public static bool insOptsNone(insOpts opt)
    {
        return opt == INS_OPTS_NONE;
    }

    public static uint getBitWidth(emitAttr size)
    {
        assert((int)size <= (int)EA_8BYTE);
        return unchecked((uint)size * (uint)BITS_PER_BYTE);
    }

    public static bool isValidUimm11(nint value)
    {
        return (value >> 11) == 0;
    }

    public static bool isValidUimm12(nint value)
    {
        return (value >> 12) == 0;
    }

    public static bool isValidSimm20(nint value)
    {
        if (!Configuration.TARGET_LOONGARCH64 && !Configuration.TARGET_RISCV64)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.Signed20BitImmediatesAreNotSupportedForThisTarget
    );
            return default!;
        }

        const int ImmediateLimit = 1 << 19;
        return (-ImmediateLimit <= value) && (value < ImmediateLimit);
    }

    public static bool isGeneralRegisterOrR0(regNumber reg)
    {
        if (!Configuration.TARGET_LOONGARCH64 && !Configuration.TARGET_RISCV64)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.GeneralRegisterOrZeroValidationIsNotSupportedForThisTarget
    );
            return default!;
        }

        return (reg >= TargetRuntimeMetadata.FirstRegister) &&
            (reg <= TargetRuntimeMetadata.IntegerRegisterLast);
    }

    public static bool isValidVectorIndex(emitAttr datasize, emitAttr elemsize, nint index)
    {
        if (Configuration.TARGET_ARM64)
        {
            return isValidVectorIndexArm64(datasize, elemsize, index);
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
            return isValidVectorIndexLoongArch64(datasize, elemsize, index);
        }

        FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.VectorIndexValidationIsNotSupportedForThisTarget
    );
        return default!;
    }

    public static bool isFloatReg(regNumber reg)
    {
        return !Configuration.TARGET_WASM &&
            (reg >= TargetRuntimeMetadata.FloatingRegisterFirst) &&
            (reg <= TargetRuntimeMetadata.FloatingRegisterLast);
    }

    public static bool isMaskReg(regNumber reg)
    {
        return (Configuration.TARGET_XARCH || Configuration.TARGET_ARM64) &&
            (reg >= TargetRuntimeMetadata.MaskRegisterFirst) &&
            (reg <= TargetRuntimeMetadata.MaskRegisterLast);
    }

    public static bool isVectorRegister(regNumber reg)
    {
        return (reg >= TargetRuntimeMetadata.FloatingRegisterFirst)
            && (reg <= TargetRuntimeMetadata.FloatingRegisterLast);
    }
}
