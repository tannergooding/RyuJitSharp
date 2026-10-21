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
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Callee-save restoration is not supported by this dispatcher.");
            return;
        }
    }

    public uint genPopCalleeSavedRegistersFromMask(regMaskTP popRegs)
    {
        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Xarch callee-save restoration is not supported on this target.");
            return default!;
        }

        return genPopCalleeSavedRegistersFromMaskXarch(popRegs);
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
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Root epilog generation is not supported by this dispatcher.");
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
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "instGen_Return is not used on ARM64.");
            return;
        }
        if (Configuration.TARGET_XARCH)
        {
            instGen_ReturnXarch(stackArgumentSize);
            return;
        }

        NYI("instGen_Return");
        FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Return instruction generation is not implemented on this target.");
        return;
    }
}
