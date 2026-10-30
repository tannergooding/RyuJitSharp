// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime. Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private bool doubleAlignOrFramePointerUsed()
    {
        return IsFramePointerUsed || (Configuration.DOUBLE_ALIGN && _compiler.genDoubleAlign);
    }

    public unsafe void genPopCalleeSavedRegisters(bool jmpEpilog = false)
    {
        if (Configuration.TARGET_ARM)
        {
            genPopCalleeSavedRegistersArmCore(jmpEpilog);
        }
        else if (Configuration.TARGET_RISCV64)
        {
            genPopCalleeSavedRegistersRiscV(jmpEpilog);
        }
        else if (Configuration.TARGET_LOONGARCH64)
        {
            genPopCalleeSavedRegistersLoongArch64(jmpEpilog);
        }
        else
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.CalleeSaveRestorationIsNotSupportedByThisDispatcher
    );
            return;
        }
    }

    public unsafe void genFnEpilog(BasicBlock block)
    {
        if (Configuration.TARGET_WASM)
        {
            genFnEpilogWasm(block);
        }
        else if (Configuration.TARGET_ARMARCH)
        {
            genFnEpilogArmArch(block);
        }
        else if (Configuration.TARGET_RISCV64)
        {
            genFnEpilogRiscV(block);
        }
        else if (Configuration.TARGET_LOONGARCH64)
        {
            genFnEpilogLoongArch64(block);
        }
        else if (Configuration.TARGET_XARCH)
        {
            genFnEpilogXarch(block);
        }
        else
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.RootEpilogGenerationIsNotSupportedByThisDispatcher
    );
            return;
        }
    }

    public void instGen_Return(uint stackArgumentSize)
    {
        if (Configuration.TARGET_ARM)
        {
            // ARM emits the return as part of the register-restoring epilog pop.
            return;
        }
        if (Configuration.TARGET_ARM64)
        {
            unreached();
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.InstGenReturnIsNotUsedOnARM64);
            return;
        }
        if (Configuration.TARGET_XARCH)
        {
            instGen_ReturnXarch(stackArgumentSize);
            return;
        }

        NYI("instGen_Return");
        FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.ReturnInstructionGenerationIsNotImplementedOnThisTarget
    );
        return;
    }
}
