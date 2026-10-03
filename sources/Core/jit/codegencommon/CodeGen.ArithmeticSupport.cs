// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public static bool genIsSameLocalVar(GenTree op1, GenTree op2)
    {
        var first = op1.SkipCopyOrReload;
        var second = op2.SkipCopyOrReload;
        return (first.Oper is GT_LCL_VAR) && (second.Oper is GT_LCL_VAR) &&
            (first.AsLclVar().LclNum == second.AsLclVar().LclNum);
    }

    public void genJumpToThrowHlpBlk(emitJumpKind jumpKind, SpecialCodeKind codeKind,
        BasicBlock? failBlock = null)
    {
#if TARGET_WASM
        throw new FatalJitException(CORJIT_SKIPPED, "Wasm throw-helper generation uses a separate target path.");
#else
#if TARGET_AMD64
        Emitter.RequireSupportedInstructionRecording();
#endif
        var useThrowHelperBlocks = _compiler.fgUseThrowHelperBlocks();
#if UNIX_X86_ABI
        // Funclets must throw inline so their frames can be unwound.
        useThrowHelperBlocks = useThrowHelperBlocks && (_compiler.funCurrentFunc().funKind == FuncKind.FUNC_ROOT);
#endif
        if (useThrowHelperBlocks)
        {
            assert(_compiler.compCurBB is not null);
            BasicBlock? target;
            if (failBlock is not null)
            {
                target = failBlock;
#if DEBUG
                var add = _compiler.fgGetExcptnTarget(codeKind, _compiler.compCurBB);
                assert(add.acdUsed);
                assert(ReferenceEquals(target, add.acdDstBlk));
#if !FEATURE_FIXED_OUT_ARGS
                assert(add.acdStkLvlInit || IsFramePointerUsed);
#endif
#endif
            }
            else
            {
                var add = _compiler.fgGetExcptnTarget(codeKind, _compiler.compCurBB);
                assert(add is not null);
                assert(add.acdUsed);
                target = add.acdDstBlk;
#if DEBUG && !FEATURE_FIXED_OUT_ARGS
                assert(add.acdStkLvlInit || IsFramePointerUsed);
#endif
            }

            noway_assert(target is not null);
            inst_JMP(jumpKind, target);
        }
        else
        {
            BasicBlock? target = null;
            var reverseJumpKind = RyuJitSharp.Emitter.emitReverseJumpKind(jumpKind);
            if (reverseJumpKind != jumpKind)
            {
                target = genCreateTempLabel();
                inst_JMP(reverseJumpKind, target);
            }

            genEmitHelperCall(Compiler.acdHelper(codeKind), 0, EA_UNKNOWN);
            if (target is not null)
            {
                assert(reverseJumpKind != jumpKind);
                genDefineTempLabel(target);
            }
        }
#endif
    }

    public void genCheckOverflow(GenTree tree)
    {
#if TARGET_WASM || TARGET_LOONGARCH64 || TARGET_RISCV64
        throw new FatalJitException(CORJIT_SKIPPED, "Overflow checking uses a separate target path.");
#else
        noway_assert(tree.HasOverflowCheck);
        noway_assert(!varTypeIsSmall(tree.Type));
        emitJumpKind jumpKind;
#if TARGET_ARM64
        if (tree.Oper is GT_MUL)
        {
            jumpKind = emitJumpKind.EJ_ne;
        }
        else
#endif
        {
            var unsigned = (tree.Flags & GTF_UNSIGNED) != 0;
#if TARGET_XARCH
            jumpKind = unsigned ? emitJumpKind.EJ_jb : emitJumpKind.EJ_jo;
#elif TARGET_ARMARCH
            jumpKind = unsigned ? emitJumpKind.EJ_lo : emitJumpKind.EJ_vs;
            if ((jumpKind == emitJumpKind.EJ_lo) && (tree.Oper is not GT_SUB))
            {
                jumpKind = emitJumpKind.EJ_hs;
            }
#else
            throw new FatalJitException(CORJIT_SKIPPED, "Overflow checking is not implemented for this target.");
#endif
        }

        genJumpToThrowHlpBlk(jumpKind, SCK_OVERFLOW);
#endif
    }

#if !TARGET_XARCH && !TARGET_WASM
    public void inst_JMP(emitJumpKind jump, BasicBlock target, bool isRemovableJmpCandidate = false)
    {
#if TARGET_ARM64
#if !FEATURE_FIXED_OUT_ARGS
        assert((target.bbTgtStkDepth * sizeof(int) == genStackLevel) || IsFramePointerUsed);
#endif
        Emitter.emitIns_J(RyuJitSharp.Emitter.emitJumpKindToIns(jump), target);
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Jump instruction generation is not implemented for this target.");
#endif
    }

#endif

#if !TARGET_XARCH && !TARGET_ARM64 && !TARGET_LOONGARCH64
    public void genEmitHelperCall(CorInfoHelpFunc helper, int argSize, emitAttr retSize,
        regNumber callTargetReg = REG_NA)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Helper call generation is not implemented for this target.");
    }
#endif
}
