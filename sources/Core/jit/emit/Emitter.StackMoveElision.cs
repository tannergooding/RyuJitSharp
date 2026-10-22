// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    public static bool IsMovInstruction(instruction ins)
    {
        if (Configuration.TARGET_XARCH)
        {
            return IsXarchMovInstruction(ins);
        }

        if (Configuration.TARGET_ARM)
        {
            return IsArm32MovInstruction(ins);
        }

        if (Configuration.TARGET_ARM64)
        {
            return IsArm64MovInstruction(ins);
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
            return IsLoongArch64MovInstruction(ins);
        }

        FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.MoveInstructionClassificationIsNotSupportedForThisTarget
    );
        return default!;
    }

    private static bool IsXarchMovInstruction(instruction ins)
    {
        // This is the native move-elision set, not all instructions with move-like semantics.
        return ins is INS_mov or INS_movapd or INS_movaps or INS_movd32 or INS_movd64 or
            INS_movdqa32 or INS_vmovdqa64 or INS_movdqu32 or INS_vmovdqu8 or INS_vmovdqu16 or
            INS_vmovdqu64 or INS_movq or INS_movsd_simd or INS_movss or INS_vmovsh or
            INS_movsx or INS_movupd or INS_movups or INS_movzx or INS_kmovb_msk or
            INS_kmovw_msk or INS_kmovd_msk or INS_kmovq_msk or INS_kmovb_gpr or
            INS_kmovw_gpr or INS_kmovd_gpr or INS_kmovq_gpr ||
            (Configuration.TARGET_64BIT && ins == INS_movsxd);
    }

    private static bool IsArm64MovInstruction(instruction ins)
    {
        return ins is INS_fmov or INS_mov or INS_sxtb or INS_sxth or INS_sxtw or INS_uxtb or INS_uxth or
            INS_sve_mov or INS_sve_movprfx;
    }

    private static bool IsLoongArch64MovInstruction(instruction ins)
    {
        return ins is INS_mov or INS_fmov_s or INS_fmov_d or INS_movgr2fr_w or INS_movgr2fr_d or
            INS_movfr2gr_s or INS_movfr2gr_d;
    }

    public bool HasSideEffect(instruction ins, emitAttr size)
    {
        RequireXarchStackMove();
        bool hasSideEffect;

        switch (ins)
        {
            case INS_mov:
            {
                // Narrow moves may zero-extend the source.
                hasSideEffect = size != EA_PTRSIZE;
                break;
            }

            case INS_movapd or INS_movaps or INS_movdqa32 or INS_movdqu32 or INS_movupd or INS_movups:
            {
                // VEX/EVEX moves clear the bits above their vector width.
                if (UseVexEncodings)
                {
                    hasSideEffect = UseEvexEncodings ? size != EA_64BYTE : size != EA_32BYTE;
                }
                else
                {
                    hasSideEffect = false;
                }
                break;
            }

            case INS_vmovdqa64 or INS_vmovdqu8 or INS_vmovdqu16 or INS_vmovdqu64:
            {
                assert(UseEvexEncodings);
                hasSideEffect = size != EA_64BYTE;
                break;
            }

            case INS_movd32 or INS_movd64:
            {
                hasSideEffect = true;
                break;
            }

            case INS_movsd_simd or INS_movss:
            {
                hasSideEffect = UseVexEncodings;
                break;
            }

            case INS_vmovsh or INS_movsx or INS_movzx or INS_movq:
            case INS_kmovb_msk or INS_kmovw_msk or INS_kmovd_msk:
            case INS_kmovb_gpr or INS_kmovw_gpr or INS_kmovd_gpr or INS_kmovq_gpr:
            {
                hasSideEffect = true;
                break;
            }

            case INS_movsxd:
            {
                if (!Configuration.TARGET_64BIT)
                {
                    FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.MOVSXDSideEffectClassificationIsOnlySupportedOn64BitXarch
    );
                    return default!;
                }

                hasSideEffect = true;
                break;
            }

            case INS_kmovq_msk:
            {
                hasSideEffect = false;
                break;
            }

            default:
            {
                FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.InvalidMoveSideEffectClassification);
                return default!;
            }
        }

        return hasSideEffect;
    }

    public bool IsRedundantStackMov(instruction ins, insFormat fmt, emitAttr size, regNumber ireg, int varx, int offs)
    {
        RequireXarchStackMove();
        assert(IsMovInstruction(ins));
        assert(fmt is IF_XARCH_SWR_RRD or IF_XARCH_RWR_SRD);
        assert(_compiler is not null);

        if (!_compiler.opts.OptimizationEnabled)
        {
            return false;
        }

        // Never elide an instruction that creates a GC-live value.
        if (EA_IS_GCREF(size) || EA_IS_BYREF(size))
        {
            return false;
        }

        if (!emitCanPeepholeLastIns())
        {
            return false;
        }

        assert(emitLastIns is not null);

        if ((emitLastIns.idIns() != ins) || (emitLastIns.idOpSize() != size))
        {
            return false;
        }

        if (emitLastIns.idInsFmt() is not (IF_XARCH_SWR_RRD or IF_XARCH_RWR_SRD))
        {
            return false;
        }

        var lastReg1 = emitLastIns.idReg1();
        var varNum = emitLastIns.idAddr().iiaLclVar.lvaVarNum();
        var lastOffs = unchecked((int)emitLastIns.idAddr().iiaLclVar.lvaOffset());
        var hasSideEffect = HasSideEffect(ins, size);

        if ((varNum == varx) && (lastReg1 == ireg) && (lastOffs == offs))
        {
            if ((((emitLastIns.idInsFmt() == IF_XARCH_RWR_SRD) && (fmt == IF_XARCH_SWR_RRD)) ||
                 ((emitLastIns.idInsFmt() == IF_XARCH_SWR_RRD) && (fmt == IF_XARCH_RWR_SRD))) && !hasSideEffect)
            {
                if (Configuration.DEBUG && _compiler.verbose)
                {
                    jitprintf("\n -- suppressing mov because last instruction already moved from dst to src and the mov has no side-effects.\n");
                }
                return true;
            }

            if (emitLastIns.idInsFmt() == fmt)
            {
                if (Configuration.DEBUG && _compiler.verbose)
                {
                    jitprintf("\n -- suppressing mov because last instruction already moved from src to dst.\n");
                }
                return true;
            }
        }

        return false;
    }

    private static void RequireXarchStackMove()
    {
        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.XarchStackMoveOptimizationIsNotSupportedForThisTarget
    );
            return;
        }
    }
}
