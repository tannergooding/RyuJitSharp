// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void instGen_Set_Reg_To_Zero(emitAttr size, regNumber reg, insFlags flags = INS_FLAGS_DONT_CARE)
    {
        assert(genIsValidIntOrFakeReg(reg));
        Emitter.emitIns_R_R(INS_xor, size, reg, reg);
        _regSet.verifyRegUsed(reg);
    }

    public void instGen_Set_Reg_To_Imm(emitAttr size, regNumber reg, nint imm, insFlags flags = INS_FLAGS_DONT_CARE
#if DEBUG
        , nuint targetHandle = 0, GenTreeFlags gtFlags = GTF_EMPTY
#endif
        )
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Integer immediate materialization outside AMD64 is not implemented.");
#else
        assert(!genIsValidFloatReg(reg));
        var origAttr = size;

        if (!_compiler.opts.compReloc)
        {
            size &= ~(EA_CNS_RELOC_FLG | EA_DSP_RELOC_FLG);
        }

        if ((imm == 0) && !EA_IS_RELOC(size))
        {
            instGen_Set_Reg_To_Zero(size, reg, flags);
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
                Emitter.emitIns_R_AI(INS_lea, size, reg, imm
#if DEBUG
                    , targetHandle, gtFlags
#endif
                    );
            }
            else
            {
                Emitter.emitIns_R_I(INS_mov, size, reg, imm, INS_OPTS_NONE
#if DEBUG
                    , targetHandle, gtFlags
#endif
                    );
            }
        }
        else
        {
            Emitter.emitIns_R_I(INS_mov, size, reg, imm, INS_OPTS_NONE
#if DEBUG
                , targetHandle, gtFlags
#endif
                );
        }

        _regSet.verifyRegUsed(reg);
#endif
    }

#if TARGET_AMD64
    public unsafe CorInfoReloc genAddrRelocTypeHint(nuint addr)
    {
        return _compiler.eeGetRelocTypeHint((void*)addr);
    }
#endif

    public bool genDataIndirAddrCanBeEncodedAsPCRelOffset(nuint addr)
    {
#if TARGET_AMD64
        return genAddrRelocTypeHint(addr) == CorInfoReloc.RELATIVE32;
#else
        return false;
#endif
    }
}
#endif
