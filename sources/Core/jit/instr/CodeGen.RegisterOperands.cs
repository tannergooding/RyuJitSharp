// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void inst_RV_RV(instruction ins, regNumber reg1, regNumber reg2, var_types type = TYP_I_IMPL,
        emitAttr size = EA_UNKNOWN, insFlags flags = INS_FLAGS_DONT_CARE)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Register/register instruction generation requires AMD64.");
#else
        if (size == EA_UNKNOWN)
        {
            size = type.EmitActualSize;
        }

        Emitter.emitIns_R_R(ins, size, reg1, reg2);
#endif
    }

    public void inst_RV(instruction ins, regNumber reg, var_types type, emitAttr size = EA_UNKNOWN)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Single-register instruction generation requires AMD64.");
#else
        if (size == EA_UNKNOWN)
        {
            size = type.EmitActualSize;
        }

        Emitter.emitIns_R(ins, size, reg);
#endif
    }

    public void inst_RV_IV(instruction ins, regNumber reg, nint value, emitAttr size,
        insFlags flags = INS_FLAGS_DONT_CARE)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Register-immediate instruction generation requires AMD64.");
#else
        if ((size == EA_8BYTE) && (ins == INS_mov) && ((unchecked((ulong)value) & 0xFFFFFFFF00000000UL) == 0))
        {
            size = EA_4BYTE;
            Emitter.emitIns_R_I(ins, size, reg, value);
        }
        else if ((EA_SIZE(size) == EA_8BYTE) && (ins != INS_mov) &&
            ((unchecked((int)value) != value) || EA_IS_CNS_RELOC(size)))
        {
            assert(false, "Invalid immediate for inst_RV_IV");
        }
        else
        {
            Emitter.emitIns_R_I(ins, size, reg, value);
        }
#endif
    }
}
