// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    private insGroup emitBindJump(instrDescJmp jump)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Jump target binding requires AMD64.");
#else
        assert(!jump.idIsBound());
        var block = jump.idjTarget ?? throw new FatalJitException("An unbound jump requires a basic-block target.");
        assert(block.HasFlag(BBF_HAS_LABEL));
        var target = emitCodeGetCookie(block)
            ?? throw new FatalJitException("A jump target requires an emitter label cookie.");
        jump.idjTargetIG = target;
        jump.idSetIsBound();
        return target;
#endif
    }

#if DEBUG && TARGET_AMD64
    private void emitCheckFuncletBranch(instrDescJmp jump, insGroup jumpIG)
    {
        var compiler = _compiler ?? throw new FatalJitException("Funclet branch checking requires an active compiler.");
        assert(jump.idIsBound());
        if (jump.idIns() == INS_lea)
        {
            return;
        }

        var targetIG = jump.idjTargetIG
            ?? throw new FatalJitException("A bound branch requires an instruction-group target.");
        if (targetIG.igFuncIdx == jumpIG.igFuncIdx)
        {
            return;
        }

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
            assert(targetIG.igFuncIdx == jumpIG.igFuncIdx);
        }
    }
#endif
}
