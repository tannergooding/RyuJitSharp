// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
#if !TARGET_ARM64
    public bool emitIns_Mov(instruction ins, emitAttr attr, regNumber dstReg, regNumber srcReg,
#if TARGET_ARM
        bool canSkip, insFlags flags = INS_FLAGS_DONT_CARE)
#else
        bool canSkip, bool useApxNdd = false)
#endif
    {
#if TARGET_ARM
        return recordArm32InsMov(ins, attr, dstReg, srcReg, canSkip, flags);
#elif TARGET_LOONGARCH64
        assert(!useApxNdd);
        emitIns_MovLoongArch64(ins, attr, dstReg, srcReg, canSkip);
        return true;
#elif TARGET_RISCV64
        return emitIns_MovRiscV64(ins, attr, dstReg, srcReg, canSkip);
#elif !TARGET_XARCH
        throw new FatalJitException(CORJIT_SKIPPED, "Register move recording requires xarch.");
#else
        assert(IsMovInstruction(ins));
#if TARGET_AMD64
        RequireSupportedInstructionRecording();
#endif
#if DEBUG
        switch (ins)
        {
            case INS_mov or INS_movsx or INS_movzx:
            {
                assert(dstReg.IsIntReg && srcReg.IsIntReg);
                break;
            }

#if TARGET_AMD64
            case INS_movsxd:
            {
                assert(dstReg.IsIntReg && srcReg.IsIntReg);
                break;
            }
#endif

            case INS_movapd or INS_movaps or INS_movdqa32 or INS_vmovdqa64 or INS_movdqu32 or
                INS_vmovdqu8 or INS_vmovdqu16 or INS_vmovdqu64 or INS_movsd_simd or INS_movss or
                INS_vmovsh or INS_movupd or INS_movups:
            {
                assert(dstReg.IsFltReg && srcReg.IsFltReg);
                break;
            }

            case INS_movq:
            {
                assert(dstReg.IsFltReg && srcReg.IsFltReg);
                break;
            }

            case INS_movd32 or INS_movd64:
            {
                assert(dstReg.IsFltReg != srcReg.IsFltReg);
                break;
            }

            case INS_kmovb_msk or INS_kmovw_msk or INS_kmovd_msk or INS_kmovq_msk:
            {
                assert((isMaskReg(dstReg) || isMaskReg(srcReg)) && !dstReg.IsIntReg && !srcReg.IsIntReg);
                break;
            }

            case INS_kmovb_gpr or INS_kmovw_gpr or INS_kmovd_gpr or INS_kmovq_gpr:
            {
                assert(dstReg.IsIntReg || srcReg.IsIntReg);
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }
#endif
        var size = EA_SIZE(attr);
        assert(size <= EA_64BYTE);
        noway_assert(emitVerifyEncodable(ins, size, dstReg, srcReg));
        var fmt = emitInsModeFormat(ins, IF_RRD_RRD);

        if (IsRedundantMov(ins, fmt, attr, dstReg, srcReg, canSkip))
        {
            return false;
        }

        if (EmitMovsxAsCwde(ins, size, dstReg, srcReg))
        {
            return false;
        }

#if TARGET_AMD64
        if (useApxNdd)
        {
            // The move is required, but its APX NDD-aware caller will handle it.
            return true;
        }
#endif

        var id = emitNewInstrSmall(attr);
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idReg1(dstReg);
        id.idReg2(srcReg);
        var sz = emitInsSizeRR(id);
        id.idCodeSize(sz);
        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);

        return true;
#endif
    }
#endif

#if TARGET_XARCH
    public bool EmitMovsxAsCwde(instruction ins, emitAttr size, regNumber dst, regNumber src)
    {
        if ((src == REG_EAX) && (src == dst))
        {
            // movsxd rax,eax and movsx eax,ax have shorter accumulator forms.
#if TARGET_64BIT
            if ((ins == INS_movsxd) && (size == EA_4BYTE))
            {
                emitIns(INS_cwde, EA_8BYTE);
                return true;
            }
#endif

            if ((ins == INS_movsx) && (size == EA_2BYTE))
            {
                emitIns(INS_cwde, EA_4BYTE);
                return true;
            }
        }

        return false;
    }

    public bool IsRedundantMov(instruction ins, insFormat fmt, emitAttr size, regNumber dst, regNumber src,
        bool canIgnoreSideEffects)
    {
        assert(IsMovInstruction(ins));
        assert(_compiler is not null);

        if (canIgnoreSideEffects && (dst == src))
        {
            // Historically these explicit elisions also occurred in minopts and
            // for moves with extension/clearing side effects. Preserve that contract.
            return true;
        }

        if (!_compiler.opts.OptimizationEnabled)
        {
            return false;
        }

        // Never remove an instruction that creates a GC-live value.
        if (EA_IS_GCREF(size) || EA_IS_BYREF(size))
        {
            return false;
        }

        var hasSideEffect = HasSideEffect(ins, size);

        if (dst == src)
        {
            if (!hasSideEffect)
            {
#if DEBUG
                if (_compiler.verbose)
                {
                    jitprintf("\n -- suppressing mov because src and dst is same register and the mov has no side-effects.\n");
                }
#endif
                return true;
            }

#if TARGET_64BIT
            switch (ins)
            {
                case INS_movzx:
                {
                    if (AreUpperBitsZero(src, size))
                    {
#if DEBUG
                        if (_compiler.verbose)
                        {
                            jitprintf("\n -- suppressing movzx because upper bits are zero.\n");
                        }
#endif
                        return true;
                    }
                    break;
                }

                case INS_movsx or INS_movsxd:
                {
                    if (AreUpperBitsSignExtended(src, size))
                    {
#if DEBUG
                        if (_compiler.verbose)
                        {
                            jitprintf("\n -- suppressing movsx or movsxd because upper bits are sign-extended.\n");
                        }
#endif
                        return true;
                    }
                    break;
                }

                case INS_mov:
                {
                    if ((size == EA_4BYTE) && AreUpperBitsZero(src, size))
                    {
#if DEBUG
                        if (_compiler.verbose)
                        {
                            jitprintf("\n -- suppressing mov because upper bits are zero.\n");
                        }
#endif
                        return true;
                    }
                    break;
                }

                default:
                {
                    break;
                }
            }
#endif
        }

        if (!emitCanPeepholeLastIns())
        {
            return false;
        }

        assert(emitLastIns is not null);
        if ((emitLastIns.idIns() != ins) || (emitLastIns.idOpSize() != size) || (emitLastIns.idInsFmt() != fmt))
        {
            return false;
        }

        var lastDst = emitLastIns.idReg1();
        var lastSrc = emitLastIns.idReg2();

        if ((lastDst == dst) && (lastSrc == src))
        {
#if DEBUG
            if (_compiler.verbose)
            {
                jitprintf("\n -- suppressing mov because last instruction already moved from src to dst register.\n");
            }
#endif
            return true;
        }

        if ((lastDst == src) && (lastSrc == dst) && !hasSideEffect)
        {
#if DEBUG
            if (_compiler.verbose)
            {
                jitprintf("\n -- suppressing mov because last instruction already moved from dst to src register and the mov has no side-effects.\n");
            }
#endif
            return true;
        }

        return false;
    }
#endif

}
