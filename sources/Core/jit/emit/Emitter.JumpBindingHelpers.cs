// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    private unsafe insGroup emitBindJump(instrDescJmp jump)
    {
        if (Configuration.TARGET_LOONGARCH64)
        {
            assert(!jump.idIsBound());
            var targetBlock = jump.idjTarget;
            if (targetBlock is null)
            {
                FatalJitException.ThrowFatalJitException("An unbound LoongArch64 jump requires a basic-block target.");
                return default!;
            }
            assert(targetBlock.HasFlag(BBF_HAS_LABEL));
            var targetGroup = emitCodeGetCookie(targetBlock);
            if (targetGroup is null)
            {
                FatalJitException.ThrowFatalJitException("A LoongArch64 jump target has no instruction-group cookie.");
                return default!;
            }
            jump.LoongArchIGlabel = targetGroup;
            jump.idjTargetIG = targetGroup;
            jump.idSetIsBound();
            return targetGroup;
        }

        assert(!jump.idIsBound());
        var block = jump.idjTarget;
        if (block is null)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.UnboundJumpMissingBasicBlockTarget);
            return default!;
        }
        if (Configuration.DEBUG)
        {
            var compiler = _compiler;
            if (compiler is null)
            {
                FatalJitException.ThrowFatalJitException("Jump target binding requires an active compiler.");
                return default!;
            }

            if (compiler.verbose)
            {
                jitprintf("Binding: ");
                emitDispIns(jump, false, false, false);
                jitprintf($"Binding L_M{unchecked((uint)compiler.compMethodID):D3}_{FMT_BB(block.bbNum)}");
            }
        }

        var target = emitCodeGetCookie(block);
        if (Configuration.DEBUG)
        {
            var compiler = _compiler;
            if (compiler is null)
            {
                FatalJitException.ThrowFatalJitException("Jump target binding requires an active compiler.");
                return default!;
            }

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
        }

        assert(block.HasFlag(BBF_HAS_LABEL));
        var boundTarget = target;
        if (boundTarget is null)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.JumpTargetMissingEmitterLabelCookie);
            return default!;
        }
        jump.idjTargetIG = boundTarget;
        jump.idSetIsBound();
        return boundTarget;
    }

    private void emitCheckFuncletBranch(instrDescJmp jump, insGroup jumpIG)
    {
        if (Configuration.TARGET_LOONGARCH64 || Configuration.TARGET_RISCV64)
        {
            // Native does not yet record the debug information needed for these targets.
            return;
        }

        if (Configuration.DEBUG)
        {
            assert(jump.idIsBound());
        }

        if (Configuration.DEBUG)
        {
            if (Configuration.TARGET_XARCH && jump.idIns() == INS_lea)
            {
                return;
            }

            if (Configuration.TARGET_ARM64 && (emitIsLoadLabel(jump) || emitIsLoadConstant(jump)))
            {
                return;
            }

            var targetIG = jump.idjTargetIG;
            if (targetIG is null)
            {
                FatalJitException.ThrowFatalJitException("A bound branch requires an instruction-group target.");
                return;
            }
            if (targetIG.igFuncIdx == jumpIG.igFuncIdx)
            {
                return;
            }

            var compiler = _compiler;
            if (compiler is null)
            {
                FatalJitException.ThrowFatalJitException("Funclet branch checking requires an active compiler.");
                return;
            }
            var debugInfo = jump.idDebugOnlyInfo();
            if (debugInfo is null)
            {
                FatalJitException.ThrowFatalJitException("Funclet branch validation requires debug information.");
                return;
            }
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
        }
    }

    private static bool emitIsCondJump(instrDesc jump)
    {
        if (Configuration.TARGET_ARM64)
        {
            return jump.idInsFmt() is insFormat.IF_ARM64_BI_0B or insFormat.IF_ARM64_BI_1A
                or insFormat.IF_ARM64_BI_1B or insFormat.IF_ARM64_LARGEJMP;
        }

        if (Configuration.TARGET_ARM)
        {
            return jump.idInsFmt() is insFormat.IF_ARM32_T2_J1 or insFormat.IF_ARM32_T1_K or insFormat.IF_ARM32_LARGEJMP;
        }

        FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "ARM jump-format classification is not supported for this target.");
        return default!;
    }

    private static bool emitIsLoadLabel(instrDesc jump)
    {
        if (Configuration.TARGET_ARM64)
        {
            return jump.idInsFmt() is insFormat.IF_ARM64_DI_1E or insFormat.IF_ARM64_LARGEADR;
        }

        if (Configuration.TARGET_ARM)
        {
            return jump.idInsFmt() is insFormat.IF_ARM32_T2_M1 or insFormat.IF_ARM32_T1_J3 or insFormat.IF_ARM32_T2_N1;
        }

        FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "ARM label-load classification is not supported for this target.");
        return default!;
    }
    private static bool emitIsUncondJump(instrDesc jump)
    {
        if (Configuration.TARGET_ARM)
        {
            return jump.idInsFmt() is insFormat.IF_ARM32_T2_J2 or insFormat.IF_ARM32_T1_M;
        }

        if (Configuration.TARGET_ARM64)
        {
            return jump.idInsFmt() == insFormat.IF_ARM64_BI_0A;
        }

        if (Configuration.TARGET_RISCV64)
        {
            return emitIsUncondJump(jump.idIns());
        }

        FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
            "Unconditional jump classification is not supported for this target.");
        return default!;
    }

    private static bool emitIsUncondJump(instruction ins)
    {
        if (!Configuration.TARGET_RISCV64)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
                "RISC-V jump classification is not supported for this target.");
            return default!;
        }

        return ins is INS_j or INS_jal;
    }

    private static bool emitIsCmpJump(instrDesc jump)
    {
        if (Configuration.TARGET_ARM)
        {
            return jump.idInsFmt() == insFormat.IF_ARM32_T1_I;
        }

        if (Configuration.TARGET_RISCV64)
        {
            return emitIsCmpJump(jump.idIns());
        }

        FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
            "Compare-jump classification is not supported for this target.");
        return default!;
    }

    private static bool emitIsCmpJump(instruction ins)
    {
        if (!Configuration.TARGET_RISCV64)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
                "RISC-V compare-jump classification is not supported for this target.");
            return default!;
        }

        return ins is INS_beqz or INS_bnez or INS_bne or INS_beq
            or INS_blt or INS_bltu or INS_bge or INS_bgeu;
    }

    private static void emitSetMediumJump(instrDescJmp jump)
    {
        if (Configuration.TARGET_ARM)
        {
            if (jump.idjKeepLong)
            {
                return;
            }

            if (Configuration.DEBUG_EMIT)
            {
                var debugInfo = jump.idDebugOnlyInfo();
                assert(debugInfo is not null);
                if ((debugInfo.idNum == unchecked((uint)DebugJumpSelector.INTERESTING_JUMP_NUM)) ||
                    (DebugJumpSelector.INTERESTING_JUMP_NUM == 0))
                {
                    jitprintf($"[9] Converting jump {debugInfo.idNum} to medium\n");
                }
            }

            assert(emitIsCondJump(jump));
            jump.idInsFmt(insFormat.IF_ARM32_T2_J1);
            jump.idjShort = false;
            jump.idInsSize(emitInsSize(jump.idInsFmt()));
            return;
        }

        if (Configuration.TARGET_RISCV64)
        {
            if (!jump.idjKeepLong)
            {
                assert(emitIsCmpJump(jump));
                jump.idCodeSize(8);
                jump.idjShort = false;
            }

            return;
        }

        FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
            "Medium-jump selection is not supported for this target.");
        return;
    }

    private static bool emitIsLoadConstant(instrDesc jump)
    {
        if (!Configuration.TARGET_ARM64)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
                "ARM64 constant-load classification is not supported for this target.");
            return default!;
        }

        return jump.idInsFmt() is insFormat.IF_ARM64_LS_1A or insFormat.IF_ARM64_LARGELDC;
    }

    private static void emitSetShortJumpNonXarch(instrDescJmp jump)
    {
        if (Configuration.TARGET_ARM64)
        {
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
                        format = insFormat.IF_ARM64_BI_1A;
                        break;
                    }

                    case INS_tbz:
                    case INS_tbnz:
                    {
                        format = insFormat.IF_ARM64_BI_1B;
                        break;
                    }

                    default:
                    {
                        format = insFormat.IF_ARM64_BI_0B;
                        break;
                    }
                }
            }
            else if (emitIsLoadLabel(jump))
            {
                format = insFormat.IF_ARM64_DI_1E;
            }
            else if (emitIsLoadConstant(jump))
            {
                format = insFormat.IF_ARM64_LS_1A;
            }
            else
            {
                unreached();
            }

            jump.idInsFmt(format);
            jump.idjShort = true;
            return;
        }

        if (Configuration.TARGET_ARM)
        {
            if (jump.idjKeepLong)
            {
                return;
            }

            if (emitIsCondJump(jump))
            {
                jump.idInsFmt(insFormat.IF_ARM32_T1_K);
            }
            else if (emitIsCmpJump(jump))
            {
                assert(jump.idjShort);
                return;
            }
            else if (emitIsUncondJump(jump))
            {
                jump.idInsFmt(insFormat.IF_ARM32_T1_M);
            }
            else if (emitIsLoadLabel(jump))
            {
                return;
            }
            else
            {
                unreached();
            }

            jump.idjShort = true;
            if (Configuration.DEBUG_EMIT)
            {
                var debugInfo = jump.idDebugOnlyInfo();
                assert(debugInfo is not null);
                if ((debugInfo.idNum == unchecked((uint)DebugJumpSelector.INTERESTING_JUMP_NUM)) ||
                    (DebugJumpSelector.INTERESTING_JUMP_NUM == 0))
                {
                    jitprintf($"[8] Converting jump {debugInfo.idNum} to short\n");
                }
            }
            jump.idInsSize(emitInsSize(jump.idInsFmt()));
            return;
        }

        if (Configuration.TARGET_WASM)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, "Wasm has no short-jump instruction form.");
            return;
        }

        if (Configuration.TARGET_RISCV64)
        {
            if (!jump.idjKeepLong)
            {
                assert(emitIsCmpJump(jump) || emitIsUncondJump(jump));
                jump.idCodeSize(4);
                jump.idjShort = true;
            }

            return;
        }

        FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Non-xarch short-jump selection is not ported.");
        return;
    }

    private static void emitSetShortJump(instrDescJmp jump)
    {
        if (Configuration.TARGET_XARCH)
        {
            emitSetShortJumpXarch(jump);
            return;
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
                "Short-jump selection is implemented by the LoongArch64 binder.");
            return;
        }

        emitSetShortJumpNonXarch(jump);
    }
}
