// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public partial class Emitter
{
    public regNumber emitInsBinary(instruction ins, emitAttr attr, GenTree dst, GenTree src)
    {
        if (Configuration.TARGET_XARCH)
        {
            return emitInsBinary(ins, attr, dst, src, regNumberExtensions.NotAvailable);
        }

        if (Configuration.TARGET_ARM64)
        {
            return emitInsBinaryArm64(ins, attr, dst, src);
        }

        if (Configuration.TARGET_ARM)
        {
            return emitInsBinaryArm32(ins, attr, dst, src);
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.LoongArch64BinaryInstructionRecordingIsUnusedAndNotPorted
    );
            return default!;
        }

        FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.BinaryOperandRecordingIsNotSupportedForThisTarget
    );
        return default!;
    }

    public regNumber emitInsTernary(instruction ins, emitAttr attr, GenTree dst, GenTree src1, GenTree src2)
    {
        if (Configuration.TARGET_ARM64)
        {
            return emitInsTernaryArm64(ins, attr, dst, src1, src2);
        }

        if (Configuration.TARGET_ARM)
        {
            return emitInsTernaryArm32(ins, attr, dst, src1, src2);
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
            return emitInsTernaryLoongArch64(ins, attr, dst, src1, src2);
        }

        if (Configuration.TARGET_RISCV64)
        {
            return emitInsTernaryRiscV64(ins, attr, dst, src1, src2);
        }

        FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.TernaryInstructionRecordingIsNotSupportedForThisTarget
    );
        return default!;
    }
}
