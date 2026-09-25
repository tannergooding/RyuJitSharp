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

    private void RequireSharedThrowHelperBlocks()
    {
        if (!_compiler.fgUseThrowHelperBlocks())
        {
            throw new FatalJitException(CORJIT_SKIPPED, "Inline throw-helper call generation is not implemented.");
        }
    }

    public void genJumpToSharedThrowHlpBlk(emitJumpKind jumpKind, SpecialCodeKind codeKind,
        BasicBlock? failBlock = null)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Shared throw-helper jumps require AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        RequireSharedThrowHelperBlocks();
        assert(_compiler.compCurBB is not null);
        BasicBlock? target;
        if (failBlock is not null)
        {
            target = failBlock;
#if DEBUG
            var add = _compiler.fgGetExcptnTarget(codeKind, _compiler.compCurBB);
            assert(add.acdUsed);
            assert(ReferenceEquals(target, add.acdDstBlk));
#endif
        }
        else
        {
            var add = _compiler.fgGetExcptnTarget(codeKind, _compiler.compCurBB);
            assert(add is not null);
            assert(add.acdUsed);
            target = add.acdDstBlk;
        }

        noway_assert(target is not null);
        inst_JMP(jumpKind, target);
#endif
    }

    public void genCheckOverflow(GenTree tree)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Overflow-check generation requires AMD64.");
#else
        noway_assert(tree.HasOverflowCheck);
        noway_assert(!varTypeIsSmall(tree.Type));
        var jumpKind = (tree.Flags & GTF_UNSIGNED) != 0 ? emitJumpKind.EJ_jb : emitJumpKind.EJ_jo;
        genJumpToSharedThrowHlpBlk(jumpKind, SCK_OVERFLOW);
#endif
    }
}
