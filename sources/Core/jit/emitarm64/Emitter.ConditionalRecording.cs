// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    private struct condFlagsImm
    {
        // Native emitarm64.h packs imm5:flags:cond into bits 12..0.
        public uint immCFVal;

        public insCond cond
        {
            readonly get
            {
                return (insCond)(immCFVal & 0xF);
            }
            set
            {
                immCFVal = (immCFVal & ~0xFu) | ((uint)value & 0xF);
            }
        }

        public insCFlags flags
        {
            readonly get
            {
                return (insCFlags)((immCFVal >> 4) & 0xF);
            }
            set
            {
                immCFVal = (immCFVal & ~0xF0u) | (((uint)value & 0xF) << 4);
            }
        }

        public uint imm5
        {
            readonly get
            {
                return (immCFVal >> 8) & 0x1F;
            }
            set
            {
                immCFVal = (immCFVal & ~0x1F00u) | ((value & 0x1F) << 8);
            }
        }
    }

    public void emitIns_R_COND(instruction ins, emitAttr attr, regNumber reg, insCond cond)
    {
        insFormat fmt;
        condFlagsImm cfi = default;
        switch (ins)
        {
            case INS_cset:
            case INS_csetm:
            {
                assert(isGeneralRegister(reg));
                cfi.cond = cond;
                fmt = IF_DR_1D;
                break;
            }

            default:
            {
                unreached();
                return;
            }
        }
#pragma warning disable CA1508 // Preserve the native post-dispatch format invariant.
        assert(fmt != IF_NONE);
#pragma warning restore CA1508
        assert(isValidImmCond((nint)cfi.immCFVal));

        var id = emitNewInstrSC(attr, (nint)cfi.immCFVal);
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idInsOpt(INS_OPTS_NONE);
        id.idReg1(reg);

        dispIns(id);
        appendToCurIG(id);
    }

    public void emitIns_R_R_COND(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2, insCond cond)
    {
        insFormat fmt;
        condFlagsImm cfi = default;
        switch (ins)
        {
            case INS_cinc:
            case INS_cinv:
            case INS_cneg:
            {
                assert(isGeneralRegister(reg1));
                assert(isGeneralRegisterOrZR(reg2));
                cfi.cond = cond;
                fmt = IF_DR_2D;
                break;
            }

            default:
            {
                unreached();
                return;
            }
        }
#pragma warning disable CA1508 // Preserve the native post-dispatch format invariant.
        assert(fmt != IF_NONE);
#pragma warning restore CA1508
        assert(isValidImmCond((nint)cfi.immCFVal));

        var id = emitNewInstrSC(attr, (nint)cfi.immCFVal);
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idInsOpt(INS_OPTS_NONE);
        id.idReg1(reg1);
        id.idReg2(reg2);

        dispIns(id);
        appendToCurIG(id);
    }

    public void emitIns_R_R_R_COND(instruction ins, emitAttr attr, regNumber reg1,
        regNumber reg2, regNumber reg3, insCond cond)
    {
        insFormat fmt;
        condFlagsImm cfi = default;
        switch (ins)
        {
            case INS_csel:
            case INS_csinc:
            case INS_csinv:
            case INS_csneg:
            {
                assert(isGeneralRegister(reg1));
                assert(isGeneralRegisterOrZR(reg2));
                assert(isGeneralRegisterOrZR(reg3));
                cfi.cond = cond;
                fmt = IF_DR_3D;
                break;
            }

            default:
            {
                unreached();
                return;
            }
        }
#pragma warning disable CA1508 // Preserve the native post-dispatch format invariant.
        assert(fmt != IF_NONE);
#pragma warning restore CA1508
        assert(isValidImmCond((nint)cfi.immCFVal));

        var id = emitNewInstr(attr);
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idInsOpt(INS_OPTS_NONE);
        id.idReg1(reg1);
        id.idReg2(reg2);
        id.idReg3(reg3);
        id.idSmallCns((nint)cfi.immCFVal);

        dispIns(id);
        appendToCurIG(id);
    }

    public void emitIns_R_R_FLAGS_COND(instruction ins, emitAttr attr,
        regNumber reg1, regNumber reg2, insCFlags flags, insCond cond)
    {
        insFormat fmt;
        condFlagsImm cfi = default;
        switch (ins)
        {
            case INS_ccmp:
            case INS_ccmn:
            {
                assert(isGeneralRegister(reg1));
                assert(isGeneralRegister(reg2));
                cfi.flags = flags;
                cfi.cond = cond;
                fmt = IF_DR_2I;
                break;
            }

            default:
            {
                unreached();
                return;
            }
        }
#pragma warning disable CA1508 // Preserve the native post-dispatch format invariant.
        assert(fmt != IF_NONE);
#pragma warning restore CA1508
        assert(isValidImmCondFlags((nint)cfi.immCFVal));

        var id = emitNewInstrSC(attr, (nint)cfi.immCFVal);
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idInsOpt(INS_OPTS_NONE);
        id.idReg1(reg1);
        id.idReg2(reg2);

        dispIns(id);
        appendToCurIG(id);
    }

    public void emitIns_R_I_FLAGS_COND(instruction ins, emitAttr attr,
        regNumber reg, nint imm, insCFlags flags, insCond cond)
    {
        var fmt = IF_NONE;
        condFlagsImm cfi = default;
        switch (ins)
        {
            case INS_ccmp:
            case INS_ccmn:
            {
                assert(isGeneralRegister(reg));
                if (imm < 0)
                {
                    ins = insReverse(ins);
                    imm = unchecked(-imm);
                }
                if (isValidUimm(imm, 5))
                {
                    cfi.imm5 = unchecked((uint)imm);
                    cfi.flags = flags;
                    cfi.cond = cond;
                    fmt = IF_DI_1F;
                }
                else
                {
                    assert(false, "Instruction cannot be encoded: ccmp/ccmn imm5");
                }
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }
        assert(fmt != IF_NONE);
        assert(isValidImmCondFlagsImm5((nint)cfi.immCFVal));

        var id = emitNewInstrSC(attr, (nint)cfi.immCFVal);
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idInsOpt(INS_OPTS_NONE);
        id.idReg1(reg);

        dispIns(id);
        appendToCurIG(id);
    }

    public void emitIns_BARR(instruction ins, insBarrier barrier)
    {
        insFormat fmt;
        nint imm;
        switch (ins)
        {
            case INS_dsb:
            case INS_dmb:
            case INS_isb:
            {
                fmt = IF_SI_0B;
                imm = (nint)barrier;
                break;
            }

            default:
            {
                unreached();
                return;
            }
        }
#pragma warning disable CA1508 // Preserve the native post-dispatch format invariant.
        assert(fmt != IF_NONE);
#pragma warning restore CA1508

        var id = emitNewInstrSC(EA_8BYTE, imm);
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idInsOpt(INS_OPTS_NONE);

        dispIns(id);
        appendToCurIG(id);
    }
}
#endif
