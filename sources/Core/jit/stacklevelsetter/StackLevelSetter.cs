// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;

namespace RyuJitSharp;

public sealed class StackLevelSetter : Phase
{
    private readonly bool _throwHelperBlocksUsed;

    public StackLevelSetter(Compiler compiler)
        : base(compiler, PHASE_STACK_LEVEL_SETTER)
    {
        _throwHelperBlocksUsed = compiler.fgUseThrowHelperBlocks();
        var codeGen = compiler.codeGen;
        assert(codeGen is not null);
        codeGen.ResetWritePhaseForFramePointerRequired();
    }

    protected override PhaseStatus DoPhase()
    {
        var compiler = CompilerInstance;
        ProcessBlocks();

        var madeChanges = false;
        compiler.compUsesThrowHelper = false;

        if (compiler.fgHasAddCodeDscMap)
        {
            if (compiler.opts.OptimizationEnabled)
            {
                foreach (var add in new List<Compiler.AddCodeDsc>(compiler.fgGetAddCodeDscMap().Values))
                {
                    if (add.acdUsed)
                    {
                        compiler.fgCreateThrowHelperBlockCode(add);
                        compiler.compUsesThrowHelper = true;
                    }
                    else
                    {
                        var block = add.acdDstBlk;
                        assert(block is not null);
                        assert(block.IsEmpty);
                        JITDUMP($"Throw help block {FMT_BB(block.bbNum)} is unused\n");
                        block.RemoveFlags(BBF_DONT_REMOVE);
                        _ = compiler.fgRemoveBlock(block, unreachable: true);
                    }
                    madeChanges = true;
                }
            }
            else
            {
                foreach (var add in compiler.fgGetAddCodeDscMap().Values)
                {
                    compiler.compUsesThrowHelper = true;
                    add.acdUsed = true;
                    compiler.fgCreateThrowHelperBlockCode(add);
                    madeChanges = true;
                }
            }
        }

        compiler.fgRngChkThrowAdded = true;
        return madeChanges ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING;
    }

    private void ProcessBlocks()
    {
#if TARGET_AMD64
        if (!_throwHelperBlocksUsed)
        {
            return;
        }

        var compiler = CompilerInstance;
        for (var block = compiler.fgFirstBB; block is not null; block = block.Next)
        {
            ProcessBlock(block);
        }
#else
        throw new NotImplementedException("Non-AMD64 stack-level traversal is not ported.");
#endif
    }

    private void ProcessBlock(BasicBlock block)
    {
#if TARGET_AMD64
        for (var node = block.LastLIRNode; node is not null; node = node.Prev)
        {
            if (_throwHelperBlocksUsed && MayUseThrowHelperBlock(node))
            {
                SetThrowHelperBlocks(node, block);
            }
        }
#else
        throw new NotImplementedException("Non-AMD64 stack-level block traversal is not ported.");
#endif
    }

    private bool MayUseThrowHelperBlock(GenTree node)
        => ((node.Flags & GTF_EXCEPT) != 0) && node.MayThrow(CompilerInstance);

    private void SetThrowHelperBlocks(GenTree node, BasicBlock block)
    {
        assert(MayUseThrowHelperBlock(node));

        switch (node.Oper)
        {
            case GT_BOUNDS_CHECK:
            {
                SetThrowHelperBlock(node.AsBoundsChk().ThrowKind, block);
                break;
            }

#if FEATURE_HW_INTRINSICS && TARGET_XARCH
            case GT_HWINTRINSIC:
            {
                if (node.AsHWIntrinsic().HWIntrinsicId is NI_Vector_op_Division)
                {
                    SetThrowHelperBlock(SCK_DIV_BY_ZERO, block);
                    SetThrowHelperBlock(SCK_OVERFLOW, block);
                }
                break;
            }
#endif
            case GT_INDEX_ADDR:
            {
                if (node.AsIndexAddr().IsBoundsChecked)
                {
                    SetThrowHelperBlock(SCK_RNGCHK_FAIL, block);
                }
                break;
            }

            case GT_CKFINITE:
            {
                SetThrowHelperBlock(SCK_ARITH_EXCPN, block);
                break;
            }
        }

        if (node.HasOverflowCheckEx)
        {
            SetThrowHelperBlock(SCK_OVERFLOW, block);
        }
    }

    private void SetThrowHelperBlock(SpecialCodeKind kind, BasicBlock block)
    {
        var add = CompilerInstance.fgGetExcptnTarget(kind, block, createIfNeeded: true);
        add.acdUsed = true;
    }
}
