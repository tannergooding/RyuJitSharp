// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void instGen_Set_Reg_To_ImmXarch(
        emitAttr size,
        regNumber reg,
        nint imm,
        insFlags flags = INS_FLAGS_DONT_CARE,
        nuint targetHandle = 0,
        GenTreeFlags gtFlags = GTF_EMPTY)
    {
        assert(!genIsValidFloatReg(reg));
        var origAttr = size;

        if (!_compiler.opts.compReloc)
        {
            size &= ~(EA_CNS_RELOC_FLG | EA_DSP_RELOC_FLG);
        }

        if ((imm == 0) && !EA_IS_RELOC(size))
        {
            instGen_Set_Reg_To_Zero(size, reg);
        }
        else if (EA_IS_RELOC(origAttr) && genDataIndirAddrCanBeEncodedAsPCRelOffset(unchecked((nuint)imm)))
        {
            // Only originally relocatable constants can select LEA: address placement alone
            // must not change instruction selection between compilations.
            if (EA_IS_CNS_TLSGD_RELOC(origAttr))
            {
                // NativeAOT's TLS linker-relaxation sequence requires this prefix.
                Emitter.emitIns_Data16();
            }

            if (!EA_IS_CNS_SEC_RELOC(origAttr))
            {
                size = (size & ~EA_CNS_RELOC_FLG) | EA_DSP_RELOC_FLG;
                Emitter.emitIns_R_AI(INS_lea, size, reg, imm, targetHandle, gtFlags);
            }
            else
            {
                Emitter.emitIns_R_I(INS_mov, size, reg, imm, INS_OPTS_NONE, targetHandle, gtFlags);
            }
        }
        else
        {
            Emitter.emitIns_R_I(INS_mov, size, reg, imm, INS_OPTS_NONE, targetHandle, gtFlags);
        }

        _regSet.verifyRegUsed(reg);
    }

    public unsafe CorInfoReloc genAddrRelocTypeHint(nuint addr)
    {
        if (!Configuration.TARGET_AMD64)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.AddressRelocationHintsRequireAMD64);
            return default!;
        }

        return _compiler.eeGetRelocTypeHint((void*)addr);
    }

    public bool genDataIndirAddrCanBeEncodedAsPCRelOffset(nuint addr)
    {
        if (!Configuration.TARGET_AMD64)
        {
            return false;
        }

        return genAddrRelocTypeHint(addr) == CorInfoReloc.RELATIVE32;
    }
}
