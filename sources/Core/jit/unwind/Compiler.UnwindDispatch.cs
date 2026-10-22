// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public void unwindPush(regNumber reg)
    {
        if (Configuration.TARGET_X86)
        {
            unwindPushX86(reg);
        }
        else if (Configuration.TARGET_AMD64)
        {
            unwindPushAmd64(reg);
        }
        else
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.PushUnwindRecordingIsNotImplementedForThisTarget
    );
            return;
        }
    }

    public void unwindPush2(regNumber reg1, regNumber reg2)
    {
        if (Configuration.TARGET_AMD64)
        {
            unwindPush2Amd64(reg1, reg2);
        }
        else
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.PairedPushUnwindRecordingIsNotImplementedForThisTarget
    );
            return;
        }
    }

    public void unwindPacSignLR()
    {
        if (Configuration.TARGET_ARM64)
        {
            unwindPacSignLRArm64();
        }
        else
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.PointerAuthenticationUnwindRecordingIsNotImplementedForThisTarget
    );
            return;
        }
    }

    public void unwindReturn(regNumber reg)
    {
        if (Configuration.TARGET_ARM64)
        {
            unwindReturnArm64(reg);
        }
        else if (Configuration.TARGET_LOONGARCH64)
        {
            unwindReturnLoongArch64(reg);
        }
        else if (Configuration.TARGET_RISCV64)
        {
            unwindReturnRiscV64(reg);
        }
        else
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.ReturnUnwindRecordingIsNotImplementedForThisTarget
    );
            return;
        }
    }

    public void unwindAllocStack(uint size)
    {
        if (Configuration.TARGET_X86)
        {
            unwindAllocStackX86(size);
        }
        else if (Configuration.TARGET_AMD64)
        {
            unwindAllocStackAmd64(size);
        }
        else if (Configuration.TARGET_ARM64)
        {
            unwindAllocStackArm64(size);
        }
        else if (Configuration.TARGET_ARM)
        {
            unwindAllocStackArm(size);
        }
        else if (Configuration.TARGET_LOONGARCH64)
        {
            unwindAllocStackLoongArch64(size);
        }
        else if (Configuration.TARGET_RISCV64)
        {
            unwindAllocStackRiscV64(size);
        }
        else if (Configuration.TARGET_WASM)
        {
            unwindAllocStackWasm(size);
        }
        else
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.StackUnwindRecordingIsNotImplementedForThisTarget
    );
            return;
        }
    }

    public void unwindSetFrameReg(regNumber reg, uint offset)
    {
        if (Configuration.TARGET_X86)
        {
            unwindSetFrameRegX86(reg, offset);
        }
        else if (Configuration.TARGET_AMD64)
        {
            unwindSetFrameRegAmd64(reg, offset);
        }
        else if (Configuration.TARGET_ARM64)
        {
            unwindSetFrameRegArm64(reg, offset);
        }
        else if (Configuration.TARGET_ARM)
        {
            unwindSetFrameRegArm(reg, offset);
        }
        else if (Configuration.TARGET_LOONGARCH64)
        {
            unwindSetFrameRegLoongArch64(reg, offset);
        }
        else if (Configuration.TARGET_RISCV64)
        {
            unwindSetFrameRegRiscV64(reg, offset);
        }
        else
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.FrameRegisterUnwindRecordingIsNotImplementedForThisTarget
    );
            return;
        }
    }

    public void unwindSaveReg(regNumber reg, uint offset)
    {
        if (Configuration.TARGET_X86)
        {
            unwindSaveRegX86(reg, offset);
        }
        else if (Configuration.TARGET_AMD64)
        {
            unwindSaveRegAmd64(reg, offset);
        }
        else if (Configuration.TARGET_ARM64)
        {
            unwindSaveRegArm64(reg, offset);
        }
        else if (Configuration.TARGET_ARM)
        {
            unwindSaveRegArm(reg, offset);
        }
        else if (Configuration.TARGET_LOONGARCH64)
        {
            unwindSaveRegLoongArch64(reg, offset);
        }
        else if (Configuration.TARGET_RISCV64)
        {
            unwindSaveRegRiscV64(reg, offset);
        }
        else
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.RegisterSaveUnwindRecordingIsNotImplementedForThisTarget
    );
            return;
        }
    }

    public void unwindSaveReg(regNumber reg, int offset)
    {
        if (Configuration.TARGET_ARM64)
        {
            unwindSaveRegArm64(reg, offset);
        }
        else if (Configuration.TARGET_LOONGARCH64)
        {
            unwindSaveRegLoongArch64(reg, offset);
        }
        else if (Configuration.TARGET_RISCV64)
        {
            unwindSaveRegRiscV64(reg, offset);
        }
        else
        {
            unwindSaveReg(reg, unchecked((uint)offset));
        }
    }

    public void unwindNop()
    {
        if (Configuration.TARGET_ARM64)
        {
            unwindNopArm64();
        }
        else if (Configuration.TARGET_LOONGARCH64)
        {
            unwindNopLoongArch64();
        }
        else if (Configuration.TARGET_RISCV64)
        {
            unwindNopRiscV64();
        }
        else
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.UnwindPaddingIsNotImplementedForThisTarget);
            return;
        }
    }

    public void unwindSaveRegPair(regNumber reg1, regNumber reg2, int offset)
    {
        if (Configuration.TARGET_ARM64)
        {
            unwindSaveRegPairArm64(reg1, reg2, offset);
        }
        else if (Configuration.TARGET_LOONGARCH64)
        {
            unwindSaveRegPairLoongArch64(reg1, reg2, offset);
        }
        else if (Configuration.TARGET_RISCV64)
        {
            unwindSaveRegPairRiscV64(reg1, reg2, offset);
        }
        else
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.RegisterPairUnwindRecordingIsNotImplementedForThisTarget
    );
            return;
        }
    }

    public void unwindSaveRegPairPreindexed(regNumber reg1, regNumber reg2, int offset)
    {
        if (Configuration.TARGET_ARM64)
        {
            unwindSaveRegPairPreindexedArm64(reg1, reg2, offset);
        }
        else
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.PreindexedRegisterPairUnwindRecordingIsNotImplementedForThisTarget
    );
            return;
        }
    }

    public void unwindSaveRegPreindexed(regNumber reg, int offset)
    {
        if (Configuration.TARGET_ARM64)
        {
            unwindSaveRegPreindexedArm64(reg, offset);
        }
        else
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.PreindexedRegisterUnwindRecordingIsNotImplementedForThisTarget
    );
            return;
        }
    }
}
