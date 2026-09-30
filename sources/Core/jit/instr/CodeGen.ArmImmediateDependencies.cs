// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public bool arm_Valid_Imm_For_Instr(instruction ins, int imm, insFlags flags)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM32 instruction immediate validation is not implemented.");
    }

    public void instGen_Set_Reg_To_Imm(emitAttr size, regNumber reg, nint imm,
        insFlags flags = INS_FLAGS_DONT_CARE
#if DEBUG
        , nuint targetHandle = 0, GenTreeFlags gtFlags = GTF_EMPTY
#endif
        )
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM32 register immediate materialization is not implemented.");
    }
}
#endif
