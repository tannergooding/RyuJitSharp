// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    private unsafe insGroup emitBindJump(instrDescJmp jump)
    {
#if TARGET_LOONGARCH64
        throw new FatalJitException(CORJIT_SKIPPED, "LoongArch64 jump target binding is not ported.");
#else
        assert(!jump.idIsBound());
        var block = jump.idjTarget ?? throw new FatalJitException("An unbound jump requires a basic-block target.");
#if DEBUG
        var compiler = _compiler ?? throw new FatalJitException("Jump target binding requires an active compiler.");
        if (compiler.verbose)
        {
            jitprintf("Binding: ");
            emitDispIns(jump, false, false, false);
            jitprintf($"Binding L_M{unchecked((uint)compiler.compMethodID):D3}_{FMT_BB(block.bbNum)}");
        }
#endif

        var target = emitCodeGetCookie(block);
#if DEBUG
        if (compiler.verbose)
        {
            if (target is not null)
            {
                jitprintf($" to {emitLabelString(target)}\n");
            }
            else
            {
                jitprintf($"-- ERROR, no emitter cookie for {FMT_BB(block.bbNum)}; it is probably missing BBF_HAS_LABEL.\n");
            }
        }
#endif

        assert(block.HasFlag(BBF_HAS_LABEL));
        var boundTarget = target ?? throw new FatalJitException("A jump target requires an emitter label cookie.");
        jump.idjTargetIG = boundTarget;
        jump.idSetIsBound();
        return boundTarget;
#endif
    }

    private void emitCheckFuncletBranch(instrDescJmp jump, insGroup jumpIG)
    {
#if TARGET_LOONGARCH64 || TARGET_RISCV64
        // Native does not yet record the debug information needed for these targets.
        return;
#elif DEBUG
        assert(jump.idIsBound());
#if TARGET_XARCH
        if (jump.idIns() == INS_lea)
        {
            return;
        }
#elif TARGET_ARM64
        if (emitIsLoadLabel(jump) || emitIsLoadConstant(jump))
        {
            return;
        }
#endif

        var targetIG = jump.idjTargetIG
            ?? throw new FatalJitException("A bound branch requires an instruction-group target.");
        if (targetIG.igFuncIdx == jumpIG.igFuncIdx)
        {
            return;
        }

        var compiler = _compiler ?? throw new FatalJitException("Funclet branch checking requires an active compiler.");
        var debugInfo = jump.idDebugOnlyInfo()
            ?? throw new FatalJitException("Funclet branch validation requires debug information.");
        if (debugInfo.idFinallyCall)
        {
            assert(targetIG.igFuncIdx > 0);
            var targetFunc = compiler.funGetFunc(targetIG.igFuncIdx);
            assert(targetFunc.funKind == FuncKind.FUNC_HANDLER);
            var targetEH = compiler.ehGetDsc(targetFunc.funEHIndex);
            assert(targetEH.HasFinallyHandler);
            var targetBlock = targetFunc.GetStartBlock(compiler);
            assert(compiler.bbIsFuncletBeg(targetBlock));
            assert(ReferenceEquals(targetIG, emitCodeGetCookie(targetBlock)));
            assert(targetIG.igFuncIdx == compiler.funGetFuncIdx(targetBlock));
        }
        else if (debugInfo.idCatchRet)
        {
            var sourceFunc = compiler.funGetFunc(jumpIG.igFuncIdx);
            assert(sourceFunc.funKind == FuncKind.FUNC_HANDLER);
            var sourceEH = compiler.ehGetDsc(sourceFunc.funEHIndex);
            assert(sourceEH.HasCatchHandler);
            var targetFunc = compiler.funGetFunc(targetIG.igFuncIdx);
            if (targetFunc.funKind == FuncKind.FUNC_HANDLER)
            {
                assert(sourceEH.ebdEnclosingHndIndex == targetFunc.funEHIndex);
            }
            else
            {
                assert(targetFunc.funKind == FuncKind.FUNC_ROOT);
                assert(sourceEH.ebdEnclosingHndIndex == EHblkDsc.NO_ENCLOSING_INDEX);
            }
        }
        else
        {
            jitprintf("Hit an illegal branch between funclets!");
            assert(targetIG.igFuncIdx == jumpIG.igFuncIdx);
        }
#endif
    }

#if TARGET_ARM || TARGET_ARM64
    private static bool emitIsCondJump(instrDesc jump)
    {
#if TARGET_ARM64
        return jump.idInsFmt() is insFormat.IF_BI_0B or insFormat.IF_BI_1A
            or insFormat.IF_BI_1B or insFormat.IF_LARGEJMP;
#else
        throw new FatalJitException(CORJIT_SKIPPED, "ARM jump-format classification is not ported.");
#endif
    }

    private static bool emitIsLoadLabel(instrDesc jump)
    {
#if TARGET_ARM64
        return jump.idInsFmt() is insFormat.IF_DI_1E or insFormat.IF_LARGEADR;
#else
        throw new FatalJitException(CORJIT_SKIPPED, "ARM label-load classification is not ported.");
#endif
    }
#endif

#if TARGET_ARM || TARGET_ARM64 || TARGET_RISCV64
    private static bool emitIsUncondJump(instrDesc jump)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Non-xarch unconditional-jump classification is not ported.");
    }
#endif

#if TARGET_ARM || TARGET_RISCV64
    private static bool emitIsCmpJump(instrDesc jump)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM/RISC-V compare-and-jump classification is not ported.");
    }

    private static void emitSetMediumJump(instrDescJmp jump)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM/RISC-V medium-jump selection is not ported.");
    }
#endif

#if TARGET_ARM64
    private static bool emitIsLoadConstant(instrDesc jump)
    {
        return jump.idInsFmt() is insFormat.IF_LS_1A or insFormat.IF_LARGELDC;
    }
#endif

#if !TARGET_XARCH && !TARGET_LOONGARCH64
    private static void emitSetShortJump(instrDescJmp jump)
    {
#if TARGET_ARM64
        if (jump.idjKeepLong)
        {
            return;
        }

        var format = insFormat.IF_NONE;
        if (emitIsCondJump(jump))
        {
            switch (jump.idIns())
            {
                case INS_cbz:
                case INS_cbnz:
                {
                    format = insFormat.IF_BI_1A;
                    break;
                }

                case INS_tbz:
                case INS_tbnz:
                {
                    format = insFormat.IF_BI_1B;
                    break;
                }

                default:
                {
                    format = insFormat.IF_BI_0B;
                    break;
                }
            }
        }
        else if (emitIsLoadLabel(jump))
        {
            format = insFormat.IF_DI_1E;
        }
        else if (emitIsLoadConstant(jump))
        {
            format = insFormat.IF_LS_1A;
        }
        else
        {
            unreached();
        }

        jump.idInsFmt(format);
        jump.idjShort = true;
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Non-xarch short-jump selection is not ported.");
#endif
    }
#endif
}
