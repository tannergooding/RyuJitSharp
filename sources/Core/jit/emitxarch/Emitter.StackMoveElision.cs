// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_AMD64
    public static bool IsMovInstruction(instruction ins)
    {
        // This is the native move-elision set, not all instructions with move-like semantics.
        return ins is INS_mov or INS_movapd or INS_movaps or INS_movd32 or INS_movd64 or
            INS_movdqa32 or INS_vmovdqa64 or INS_movdqu32 or INS_vmovdqu8 or INS_vmovdqu16 or
            INS_vmovdqu64 or INS_movq or INS_movsd_simd or INS_movss or INS_vmovsh or
            INS_movsx or INS_movupd or INS_movups or INS_movzx or INS_kmovb_msk or
            INS_kmovw_msk or INS_kmovd_msk or INS_kmovq_msk or INS_kmovb_gpr or
            INS_kmovw_gpr or INS_kmovd_gpr or INS_kmovq_gpr or INS_movsxd;
    }

    public bool HasSideEffect(instruction ins, emitAttr size)
    {
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

            case INS_vmovsh or INS_movsx or INS_movzx or INS_movq or INS_movsxd:
            case INS_kmovb_msk or INS_kmovw_msk or INS_kmovd_msk:
            case INS_kmovb_gpr or INS_kmovw_gpr or INS_kmovd_gpr or INS_kmovq_gpr:
            {
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
                throw new FatalJitException("Invalid move instruction for side-effect classification.");
            }
        }

        return hasSideEffect;
    }

    private static bool isInsIGSafeForPeepholeOptimization(insGroup prevInsIG, insGroup curInsIG)
    {
        if (prevInsIG == curInsIG)
        {
            return true;
        }

        return ((curInsIG.igFlags & InsGroupFlags.Extend) != 0) &&
            ((prevInsIG.igFlags & InsGroupFlags.NoGCInterrupt) == (curInsIG.igFlags & InsGroupFlags.NoGCInterrupt));
    }

    private bool emitCanPeepholeLastIns()
    {
        assert(emitHasLastIns() == (emitLastInsIG is not null));

        if (!emitHasLastIns() || emitForceNewIG)
        {
            return false;
        }

        assert(emitLastInsIG is not null);
        assert(emitCurIG is not null);
        return isInsIGSafeForPeepholeOptimization(emitLastInsIG, emitCurIG);
    }

    public bool IsRedundantStackMov(instruction ins, insFormat fmt, emitAttr size, regNumber ireg, int varx, int offs)
    {
        assert(IsMovInstruction(ins));
        assert(fmt is IF_SWR_RRD or IF_RWR_SRD);
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

        if (emitLastIns.idInsFmt() is not (IF_SWR_RRD or IF_RWR_SRD))
        {
            return false;
        }

        var lastReg1 = emitLastIns.idReg1();
        var varNum = emitLastIns.idAddr().iiaLclVar.lvaVarNum();
        var lastOffs = emitLastIns.idAddr().iiaLclVar.lvaOffset();
        var hasSideEffect = HasSideEffect(ins, size);

        if ((varNum == varx) && (lastReg1 == ireg) && (lastOffs == offs))
        {
            if ((((emitLastIns.idInsFmt() == IF_RWR_SRD) && (fmt == IF_SWR_RRD)) ||
                 ((emitLastIns.idInsFmt() == IF_SWR_RRD) && (fmt == IF_RWR_SRD))) && !hasSideEffect)
            {
#if DEBUG
                if (_compiler.verbose)
                {
                    jitprintf("\n -- suppressing mov because last instruction already moved from dst to src and the mov has no side-effects.\n");
                }
#endif
                return true;
            }

            if (emitLastIns.idInsFmt() == fmt)
            {
#if DEBUG
                if (_compiler.verbose)
                {
                    jitprintf("\n -- suppressing mov because last instruction already moved from src to dst.\n");
                }
#endif
                return true;
            }
        }

        return false;
    }
#endif
}
