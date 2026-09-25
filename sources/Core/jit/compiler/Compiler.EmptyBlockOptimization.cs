// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public partial class Compiler
{
    /// <summary>Optimize an empty block, removing it when the flow graph and EH contracts permit.</summary>
    public bool fgOptimizeEmptyBlock(BasicBlock block)
    {
        assert(block.isEmpty());

        // We shouldn't churn the flowgraph after doing hot/cold splitting.
        assert(fgFirstColdBlock is null);

        var madeChanges = false;
        var bPrev = block.Prev;

        switch (block.Kind)
        {
            case BBJ_COND:
            case BBJ_SWITCH:
            {
                noway_assert(false, "Conditional or switch block with empty body!");
                break;
            }

            case BBJ_THROW:
            case BBJ_CALLFINALLY:
            case BBJ_CALLFINALLYRET:
            case BBJ_RETURN:
            case BBJ_EHCATCHRET:
            case BBJ_EHFINALLYRET:
            case BBJ_EHFAULTRET:
            case BBJ_EHFILTERRET:
            {
                // Leave these as-is. Some compilers put multiple returns at the end;
                // resolving that requires predecessor information.
                break;
            }

            case BBJ_ALWAYS:
            {
                if (bPrev is null)
                {
                    assert(block == fgFirstBB);
                    if (!block.JumpsToNext || !fgCanCompactInitBlock())
                    {
                        break;
                    }
                }

                // A self-loop represents while (true) {}.
                if (block.Target == block)
                {
                    break;
                }

                if ((block == fgFirstBB) && !fgCanCompactInitBlock())
                {
                    break;
                }

                if (opts.IsOSR && (block == fgEntryBB))
                {
                    break;
                }

                // A catch return address must remain in the correct EH region so
                // thread-abort exceptions can be re-raised there.
                var succBlock = block.Target;
                if (!BasicBlock.sameEHRegion(block, succBlock))
                {
                    var okToMerge = true;
                    foreach (var predBlock in block.PredBlocks)
                    {
                        if (predBlock.Kind is BBJ_EHCATCHRET)
                        {
                            assert(predBlock.Target == block);
                            okToMerge = false;
                            break;
                        }
                    }

                    if (!okToMerge)
                    {
                        var nop = new GenTree(GT_NO_OP, TYP_VOID);
                        if (block.IsLIR)
                        {
                            block.InsertAtEnd(nop);
                            var range = new LIR.ReadOnlyRange(nop, nop);
                            assert(_pLowering is not null);
                            _pLowering.LowerRange(block, range);
                        }
                        else
                        {
                            var nopStmt = gtNewStmt(nop);
                            fgInsertStmtAtEnd(block, nopStmt);
                            if (fgNodeThreading is NodeThreading.AllTrees)
                            {
                                fgSetStmtSeq(nopStmt);
                            }

                            gtSetStmtInfo(nopStmt);
                        }

                        madeChanges = true;
#if DEBUG
                        if (verbose)
                        {
                            jitprintf($"\nKeeping empty block {FMT_BB(block.bbNum)} - it is the target of a catch return\n");
                        }
#endif
                        break;
                    }
                }

                if (!ehCanDeleteEmptyBlock(block))
                {
                    break;
                }

                if (block.IsFirst && block.IsLast)
                {
                    assert(block == fgFirstBB);
                    assert(block == fgLastBB);
                    assert(bPrev is null);
                    break;
                }

                // fgComputeCalledCount expects the first non-internal block to have
                // profile weight when profile weights are in use.
                if (fgIsUsingProfileWeights && block.hasProfileWeight && !block.HasFlag(BBF_INTERNAL))
                {
                    var bNext = block.Next;
                    if ((bNext is null) || bNext.HasFlag(BBF_INTERNAL) || !bNext.hasProfileWeight)
                    {
                        var curBB = bPrev;
                        while ((curBB is not null) && curBB.HasFlag(BBF_INTERNAL))
                        {
                            curBB = curBB.Prev;
                        }

                        if (curBB is null)
                        {
                            break;
                        }
                    }
                }

                compCurBB = block;
                _ = fgRemoveBlock(block, unreachable: false);
                madeChanges = true;
                break;
            }

            default:
            {
                noway_assert(false, "Unexpected bbKind");
                break;
            }
        }

        return madeChanges;
    }
}
