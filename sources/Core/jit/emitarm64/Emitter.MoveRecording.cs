// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns_Mov(instruction ins, emitAttr attr, regNumber dstReg, regNumber srcReg,
        bool canSkip, insOpts opt = INS_OPTS_NONE)
    {
        assert(IsMovInstruction(ins));
        var size = EA_SIZE(attr);
        insFormat fmt;
        switch (ins)
        {
            case INS_mov:
            {
                assert(insOptsNone(opt));
                if (IsRedundantMov(ins, size, dstReg, srcReg, canSkip))
                {
                    return;
                }

                if (isVectorRegister(dstReg))
                {
                    if (isVectorRegister(srcReg) && isValidVectorDatasize(size))
                    {
                        emitIns_R_R_R(INS_mov, size, dstReg, srcReg, srcReg);
                        return;
                    }
                    else
                    {
                        emitIns_R_R_I(INS_mov, size, dstReg, srcReg, 0);
                        return;
                    }
                }
                else if (isVectorRegister(srcReg))
                {
                    assert(isGeneralRegister(dstReg));
                    emitIns_R_R_I(INS_mov, size, dstReg, srcReg, 0);
                    return;
                }

                if ((dstReg == REG_SP) || (srcReg == REG_SP))
                {
                    assert(isGeneralRegisterOrSP(dstReg));
                    assert(isGeneralRegisterOrSP(srcReg));
                    dstReg = encodingSPtoZR(dstReg);
                    srcReg = encodingSPtoZR(srcReg);
                    fmt = IF_DR_2G;
                }
                else
                {
                    assert(insOptsNone(opt));
                    assert(isGeneralRegister(dstReg));
                    assert(isGeneralRegisterOrZR(srcReg));
                    fmt = IF_DR_2E;
                }
                break;
            }

            case INS_sxtw:
            {
                assert((size == EA_8BYTE) || (size == EA_4BYTE));
                goto case INS_sxtb;
            }

            case INS_sxtb:
            case INS_sxth:
            case INS_uxtb:
            case INS_uxth:
            {
                // Call generation can explicitly allow eliding these extensions.
                if (canSkip && (dstReg == srcReg))
                {
                    return;
                }
                assert(insOptsNone(opt));
                assert(isValidGeneralDatasize(size));
                assert(isGeneralRegister(dstReg));
                assert(isGeneralRegister(srcReg));
                fmt = IF_DR_2H;
                break;
            }

            case INS_fmov:
            {
                assert(isValidVectorElemsizeFloat(size));
                if (canSkip && (dstReg == srcReg))
                {
                    return;
                }
                if (isVectorRegister(dstReg))
                {
                    if (isVectorRegister(srcReg))
                    {
                        assert(insOptsNone(opt));
                        fmt = IF_DV_2G;
                    }
                    else
                    {
                        assert(isGeneralRegister(srcReg));
                        if (opt == INS_OPTS_NONE)
                        {
                            opt = (size == EA_4BYTE) ? INS_OPTS_4BYTE_TO_S : INS_OPTS_8BYTE_TO_D;
                        }
                        assert(insOptsConvertIntToFloat(opt));
                        fmt = IF_DV_2I;
                    }
                }
                else
                {
                    assert(isGeneralRegister(dstReg));
                    assert(isVectorRegister(srcReg));
                    if (opt == INS_OPTS_NONE)
                    {
                        opt = (size == EA_4BYTE) ? INS_OPTS_S_TO_4BYTE : INS_OPTS_D_TO_8BYTE;
                    }
                    assert(insOptsConvertFloatToInt(opt));
                    fmt = IF_DV_2H;
                }
                break;
            }

            default:
            {
                emitInsSve_Mov(ins, attr, dstReg, srcReg, canSkip, opt,
                    insSveMovOpts.INS_SVE_MOV_OPTS_UNPRED);
                return;
            }
        }
#pragma warning disable CA1508 // Retain the native instruction-format assertion.
        assert(fmt != IF_NONE);
#pragma warning restore CA1508

        var id = emitNewInstrSmall(attr);
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idInsOpt(opt);
        id.idReg1(dstReg);
        id.idReg2(srcReg);

        dispIns(id);
        appendToCurIG(id);
    }

    public static bool IsMovInstruction(instruction ins)
    {
        switch (ins)
        {
            case INS_fmov:
            case INS_mov:
            case INS_sxtb:
            case INS_sxth:
            case INS_sxtw:
            case INS_uxtb:
            case INS_uxth:
            case INS_sve_mov:
            case INS_sve_movprfx:
            {
                return true;
            }

            default:
            {
                return false;
            }
        }
    }

    public bool IsRedundantMov(instruction ins, emitAttr size, regNumber dst, regNumber src, bool canSkip)
    {
        assert(_compiler is not null);
        assert((ins == INS_mov) || (ins == INS_sve_mov) || (ins == INS_sve_movprfx));
        if (canSkip && (dst == src))
        {
            return true;
        }
        if (ins == INS_sve_movprfx)
        {
            return false;
        }
        if (!_compiler.opts.OptimizationEnabled)
        {
            return false;
        }

        var canOptimize = emitCanPeepholeLastIns();
        if (dst == src)
        {
            // A 32-bit scalar move clears the upper half, unlike a full-width move.
            if (isGeneralRegisterOrSP(dst) && (size == EA_8BYTE))
            {
                JITDUMP("\n -- suppressing mov because src and dst is same 8-byte register.\n");
                return true;
            }
            else if (isVectorRegister(dst) && (size == EA_16BYTE))
            {
                JITDUMP("\n -- suppressing mov because src and dst is same 16-byte register.\n");
                return true;
            }
            else if (isGeneralRegisterOrSP(dst) && (size == EA_4BYTE))
            {
                if (canOptimize)
                {
                    assert(emitLastIns is not null);
                    if ((emitLastIns.idReg1() == dst) && (emitLastIns.idOpSize() == size) &&
                        (emitLastIns.idIns() is INS_ldr or INS_ldrh or INS_ldrb))
                    {
                        JITDUMP("\n -- suppressing mov because ldr already cleared upper 4 bytes\n");
                        return true;
                    }
                }
            }
        }

        if (canOptimize)
        {
            assert(emitLastIns is not null);
            if ((emitLastIns.idIns() == INS_mov) && (emitLastIns.idOpSize() == size))
            {
                var prevDst = emitLastIns.idReg1();
                var prevSrc = emitLastIns.idReg2();
                var lastInsfmt = emitLastIns.idInsFmt();
                // Immediate MOV descriptors do not have a second source register.
                var isValidLastInsFormats = (lastInsfmt == IF_DV_3C) || (lastInsfmt == IF_DR_2E);
                if (isValidLastInsFormats && (prevDst == dst) && (prevSrc == src))
                {
                    assert(emitLastIns.idOpSize() == size);
                    JITDUMP("\n -- suppressing mov because previous instruction already moved from src to dst register.\n");
                    return true;
                }
                if ((prevDst == src) && (prevSrc == dst) && isValidLastInsFormats)
                {
                    if (size == EA_8BYTE)
                    {
                        if (isVectorRegister(src) == isVectorRegister(dst))
                        {
                            JITDUMP("\n -- suppressing mov because previous instruction already did an opposite move from dst to src register.\n");
                            return true;
                        }
                    }
                    else if (size == EA_16BYTE)
                    {
                        assert(isVectorRegister(src) && isVectorRegister(dst));
                        assert(lastInsfmt == IF_DV_3C);
                        JITDUMP("\n -- suppressing mov because previous instruction already did an opposite move from dst to src register.\n");
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private void emitInsSve_Mov(instruction ins, emitAttr attr, regNumber dstReg,
        regNumber srcReg, bool canSkip, insOpts opt)
    {
        emitInsSve_Mov(ins, attr, dstReg, srcReg, canSkip, opt,
            insSveMovOpts.INS_SVE_MOV_OPTS_UNPRED);
    }
}
#endif
