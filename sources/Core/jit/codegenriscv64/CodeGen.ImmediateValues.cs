// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void instGen_Set_Reg_To_Imm(emitAttr size, regNumber reg, nint imm,
        insFlags flags = INS_FLAGS_DONT_CARE
#if DEBUG
        , nuint targetHandle = 0, GenTreeFlags gtFlags = GTF_EMPTY
#endif
        )
    {
        assert(genIsValidIntReg(reg));
        if (EA_IS_RELOC(size))
        {
            Emitter.emitIns_R_AI(INS_addi, size, reg, reg, imm);
        }
        else
        {
            _ = Emitter.emitLoadImmediate(true, size, reg, imm);
        }

        _regSet.verifyRegUsed(reg);
    }
}
#endif
