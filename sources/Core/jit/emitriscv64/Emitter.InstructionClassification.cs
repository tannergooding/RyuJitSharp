// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
namespace RyuJitSharp;

public partial class Emitter
{
    private const byte RISCV64_LD = 1;
    private const byte RISCV64_ST = 2;

    public bool emitInsIsLoad(instruction ins)
    {
        return ((uint)ins < (uint)CodeGen.instInfo.Length) &&
               ((CodeGen.instInfo[(int)ins] & RISCV64_LD) != 0);
    }

    public bool emitInsIsStore(instruction ins)
    {
        return ((uint)ins < (uint)CodeGen.instInfo.Length) &&
               ((CodeGen.instInfo[(int)ins] & RISCV64_ST) != 0);
    }

    public bool emitInsIsLoadOrStore(instruction ins)
    {
        return ((uint)ins < (uint)CodeGen.instInfo.Length) &&
               ((CodeGen.instInfo[(int)ins] & (RISCV64_LD | RISCV64_ST)) != 0);
    }

    public bool emitInsWritesToLclVarStackLoc(instrDesc id)
    {
        if (!id.idIsLclVar())
        {
            return false;
        }

        return id.idIns() switch
        {
            INS_sd or
            INS_sw or
            INS_sb or
            INS_sh => true,
            _ => false,
        };
    }
}
#endif
