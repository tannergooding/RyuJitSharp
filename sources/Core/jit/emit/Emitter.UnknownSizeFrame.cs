// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.Arm64FrameFormats;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp;

public partial class Emitter
{
    // Suffixed APIs record the scalable frame's ARM64 forms without relying on
    // target-specific overloads or changing the native instruction formats.
    private static void RequireArm64FrameRecording()
    {
        if (!Configuration.TARGET_ARM64 || !Configuration.FEATURE_SIMD)
        {
            throw new FatalJitException(CORJIT_SKIPPED, "Unknown-size frame recording requires SIMD-enabled ARM64.");
        }
    }

    private static bool IsArm64FrameGeneralRegister(regNumber reg) => reg is >= REG_ARM64_R0 and <= REG_ARM64_LR;

    private static regNumber EncodeArm64FrameSP(regNumber reg) => reg == REG_ARM64_SP ? REG_ARM64_ZR : reg;

    internal void emitIns_MovArm64Frame(emitAttr attr, regNumber dstReg, regNumber srcReg)
    {
        RequireArm64FrameRecording();
        assert(EA_SIZE(attr) == EA_8BYTE);
        insFormat fmt;
        if ((dstReg == REG_ARM64_SP) || (srcReg == REG_ARM64_SP))
        {
            assert(IsArm64FrameGeneralRegister(dstReg) || (dstReg == REG_ARM64_SP));
            assert(IsArm64FrameGeneralRegister(srcReg) || (srcReg == REG_ARM64_SP));
            dstReg = EncodeArm64FrameSP(dstReg);
            srcReg = EncodeArm64FrameSP(srcReg);
            fmt = IF_DR_2G;
        }
        else
        {
            assert(IsArm64FrameGeneralRegister(dstReg));
            assert(IsArm64FrameGeneralRegister(srcReg) || (srcReg == REG_ARM64_ZR));
            fmt = IF_DR_2E;
        }

        var id = emitNewInstrSmall(attr);
        id.idIns(INS_mov);
        id.idInsFmt(fmt);
        id.idInsOpt(INS_OPTS_NONE);
        id.idReg1(dstReg);
        id.idReg2(srcReg);

        dispIns(id);
        appendToCurIG(id);
    }

    internal void emitIns_R_IArm64Frame(instruction ins, emitAttr attr, regNumber reg, long imm,
        insOpts opt = INS_OPTS_NONE)
    {
        RequireArm64FrameRecording();
        insFormat fmt;
        switch (ins)
        {
            case INS_mov:
            {
                assert(EA_SIZE(attr) == EA_8BYTE);
                assert(opt == INS_OPTS_NONE);
                if (TargetCostEstimation.TryEncodeArm64HalfwordImmediate(imm, EA_8BYTE, out var halfword, out var value))
                {
                    assert(IsArm64FrameGeneralRegister(reg));
                    imm = (halfword << 16) | value;
                    fmt = IF_DI_1B;
                }
                else if (TargetCostEstimation.TryEncodeArm64HalfwordImmediate(~imm, EA_8BYTE, out halfword, out value))
                {
                    assert(IsArm64FrameGeneralRegister(reg));
                    imm = (halfword << 16) | value;
                    ins = INS_movn;
                    fmt = IF_DI_1B;
                }
                else if (TargetCostEstimation.TryEncodeArm64BitMaskImmediate(imm, EA_8BYTE, out var n, out var r, out var s))
                {
                    assert(IsArm64FrameGeneralRegister(reg) || (reg == REG_ARM64_SP));
                    reg = EncodeArm64FrameSP(reg);
                    imm = (n << 12) | (r << 6) | s;
                    fmt = IF_DI_1D;
                }
                else
                {
                    throw new FatalJitException(CORJIT_IMPLLIMITATION, "The ARM64 frame count cannot be encoded by a single move.");
                }

                break;
            }

            case INS_sve_rdvl:
            {
                assert(EA_SIZE(attr) == EA_8BYTE);
                assert(opt == INS_OPTS_NONE);
                assert(IsArm64FrameGeneralRegister(reg));
                assert(imm is >= -32 and <= 31);
                fmt = IF_SVE_BC_1A;
                break;
            }

            case INS_sve_mov:
            {
                assert(EA_SIZE(attr) == EA_SCALABLE_ARM64);
                assert(opt == INS_OPTS_SCALABLE_B);
                assert(reg is >= REG_ARM64_V0 and <= REG_ARM64_V31);
                assert(imm is >= -128 and <= 127);
                fmt = IF_SVE_EB_1A;
                break;
            }

            default:
            {
                throw new FatalJitException(CORJIT_IMPLLIMITATION, "Unsupported ARM64 unknown-size-frame immediate instruction.");
            }
        }

        var id = emitNewInstrSC(attr, (nint)imm);
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idInsOpt(opt);
        id.idReg1(reg);
        if (ins is INS_sve_mov or INS_sve_rdvl)
        {
            id.idHasShift(false);
        }

#if DEBUG
        if (ins is INS_mov or INS_movn)
        {
            var debugInfo = id.idDebugOnlyInfo();
            assert(debugInfo is not null);
            debugInfo.idMemCookie = 0;
            debugInfo.idFlags = GTF_EMPTY;
        }
#endif
        dispIns(id);
        appendToCurIG(id);
    }

    internal void emitIns_MovHalfwordArm64Frame(instruction ins, regNumber reg, ushort imm, int shift)
    {
        RequireArm64FrameRecording();
        assert(ins is INS_movz or INS_movn or INS_movk);
        assert(IsArm64FrameGeneralRegister(reg));
        assert((shift is >= 0 and <= 48) && ((shift % 16) == 0));

        var id = emitNewInstrSC(EA_8BYTE, (nint)(((uint)shift / 16 << 16) | imm));
        id.idIns(ins);
        id.idInsFmt(IF_DI_1B);
        id.idInsOpt(INS_OPTS_LSL);
        id.idReg1(reg);
#if DEBUG
        var debugInfo = id.idDebugOnlyInfo();
        assert(debugInfo is not null);
        debugInfo.idFlags = GTF_EMPTY;
        debugInfo.idMemCookie = 0;
#endif

        dispIns(id);
        appendToCurIG(id);
    }

    internal void emitIns_R_R_IArm64Frame(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2,
        long imm, insOpts opt = INS_OPTS_NONE)
    {
        RequireArm64FrameRecording();
        insFormat fmt;
        switch (ins)
        {
            case INS_sve_addvl:
            {
                assert(EA_SIZE(attr) == EA_8BYTE);
                assert(opt == INS_OPTS_NONE);
                assert(IsArm64FrameGeneralRegister(reg1) || (reg1 == REG_ARM64_SP));
                assert(IsArm64FrameGeneralRegister(reg2) || (reg2 == REG_ARM64_SP));
                assert(imm is >= -32 and <= 31);
                reg1 = EncodeArm64FrameSP(reg1);
                reg2 = EncodeArm64FrameSP(reg2);
                fmt = IF_SVE_BB_2A;
                break;
            }

            case INS_sve_str:
            {
                assert(EA_SIZE(attr) == EA_SCALABLE_ARM64);
                assert(opt == INS_OPTS_NONE);
                assert(reg1 is >= REG_ARM64_V0 and <= REG_ARM64_V31);
                assert(IsArm64FrameGeneralRegister(reg2) || (reg2 == REG_ARM64_SP));
                assert(imm is >= -256 and <= 255);
                reg2 = EncodeArm64FrameSP(reg2);
                fmt = IF_SVE_JH_2A;
                break;
            }

            case INS_cmp:
            {
                assert(EA_SIZE(attr) == EA_8BYTE);
                assert(opt == INS_OPTS_UXTX);
                assert(IsArm64FrameGeneralRegister(reg1) || (reg1 == REG_ARM64_SP));
                assert(IsArm64FrameGeneralRegister(reg2));
                assert(imm is >= 0 and <= 4);
                reg1 = EncodeArm64FrameSP(reg1);
                fmt = IF_DR_2C;
                break;
            }

            default:
            {
                throw new FatalJitException(CORJIT_IMPLLIMITATION, "Unsupported ARM64 unknown-size-frame register-pair instruction.");
            }
        }

        var id = emitNewInstrSC(attr, (nint)imm);
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idInsOpt(opt);
        id.idReg1(reg1);
        id.idReg2(reg2);
        if (ins is INS_sve_addvl or INS_sve_str)
        {
            id.idHasShift(false);
        }

        dispIns(id);
        appendToCurIG(id);
    }

    internal void emitIns_R_R_R_RArm64Frame(instruction ins, emitAttr attr,
        regNumber reg1, regNumber reg2, regNumber reg3, regNumber reg4)
    {
        RequireArm64FrameRecording();
        assert(ins == INS_msub);
        assert(EA_SIZE(attr) == EA_8BYTE);
        assert(IsArm64FrameGeneralRegister(reg1));
        assert(IsArm64FrameGeneralRegister(reg2));
        assert(IsArm64FrameGeneralRegister(reg3));
        assert(IsArm64FrameGeneralRegister(reg4));

        var id = emitNewInstr(attr);
        id.idIns(ins);
        id.idInsFmt(IF_DR_4A);
        id.idInsOpt(INS_OPTS_NONE);
        id.idReg1(reg1);
        id.idReg2(reg2);
        id.idReg3(reg3);
        id.idReg4(reg4);

        dispIns(id);
        appendToCurIG(id);
    }

    internal void emitIns_JArm64Frame(instruction ins, BasicBlock dst)
    {
        RequireArm64FrameRecording();
        assert(ins == INS_bne);
        assert(dst.HasFlag(BBF_HAS_LABEL));
        assert(_compiler is not null);
        assert(_compiler.compCurBB is not null);

        var id = emitNewInstrJmp();
        id.idIns(ins);
        id.idInsFmt(IF_LARGEJMP);
        id.idjTarget = dst;
        id.idjShort = false;
        id.idjKeepLong = _compiler.fgInDifferentRegions(_compiler.compCurBB, dst);
#if DEBUG
        if (_compiler.opts.compLongAddress)
        {
            id.idjKeepLong = true;
        }
#endif
        id.idjIG = emitCurIG;
        id.idjOffs = unchecked((uint)emitCurIGsize);
        id.idjNext = emitCurIGjmpList;
        emitCurIGjmpList = id;
#if EMITTER_STATS
        emitTotalIGjmps = unchecked(emitTotalIGjmps + 1);
#endif

        dispIns(id);
        appendToCurIG(id);
    }
}
