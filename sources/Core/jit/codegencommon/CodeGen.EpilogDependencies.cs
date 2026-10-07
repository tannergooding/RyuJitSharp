// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime. Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private bool doubleAlignOrFramePointerUsed()
    {
#if DOUBLE_ALIGN
        return IsFramePointerUsed || _compiler.genDoubleAlign;
#else
        return IsFramePointerUsed;
#endif
    }

#if !TARGET_XARCH
    public unsafe void genPopCalleeSavedRegisters(bool jmpEpilog = false)
    {
#if TARGET_ARM
        genPopCalleeSavedRegistersArmCore(jmpEpilog);
#elif TARGET_RISCV64
        genPopCalleeSavedRegistersRiscV(jmpEpilog);
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Callee-save restoration requires xarch.");
#endif
    }

    public uint genPopCalleeSavedRegistersFromMask(regMaskTP popRegs)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Callee-save restoration requires xarch.");
    }

    public unsafe void genFnEpilog(BasicBlock block)
    {
#if TARGET_WASM
        genFnEpilogWasm(block);
#elif TARGET_ARMARCH
        genFnEpilogArmArch(block);
#elif TARGET_RISCV64
        genFnEpilogRiscV(block);
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Root epilog generation requires xarch.");
#endif
    }

    public void instGen_Return(uint stackArgumentSize)
    {
#if TARGET_ARM
        // ARM emits the return as part of the register-restoring epilog pop.
#elif TARGET_ARM64
        unreached();
        throw new FatalJitException(CORJIT_SKIPPED, "instGen_Return is not used on ARM64.");
#else
        NYI("instGen_Return");
        throw new FatalJitException(CORJIT_SKIPPED, "Return instruction generation is not implemented on this target.");
#endif
    }
#endif
}
