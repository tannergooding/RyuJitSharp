// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void instGen_Set_Reg_To_Imm(emitAttr size, regNumber reg, nint imm,
        insFlags flags = insFlags.INS_FLAGS_DONT_CARE
#if DEBUG
        , nuint targetHandle = 0, GenTreeFlags gtFlags = GTF_EMPTY
#endif
        )
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM64 codegen immediate materialization is not ported.");
    }

    public void instGen_Set_Reg_To_Base_Plus_Imm(emitAttr size, regNumber dstReg, regNumber baseReg, nint imm,
        insFlags flags = insFlags.INS_FLAGS_DONT_CARE
#if DEBUG
        , nuint targetHandle = 0, GenTreeFlags gtFlags = GTF_EMPTY
#endif
        )
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM64 codegen base-plus-immediate materialization is not ported.");
    }
}
#endif
