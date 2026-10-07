// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, redundantbranchopts.cpp.

namespace RyuJitSharp;

public partial class Compiler
{
    private struct OptRedundantBranchesDomTreeVisitor(Compiler compiler)
        : IDomTreeVisitor<OptRedundantBranchesDomTreeVisitor>
    {
        private readonly Compiler _compiler = compiler;
        public bool MadeChanges { get; private set; }

        public readonly void Begin()
        {
        }

        public readonly void PreOrderVisit(BasicBlock block)
        {
        }

        public void PostOrderVisit(BasicBlock block)
        {
            if (block.HasFlag(BBF_REMOVED) || (block.Kind is not BBJ_COND))
            {
                return;
            }

            var changed = _compiler.optRedundantRelop(block);
            var falseTarget = block.FalseTarget;
            var trueTarget = block.TrueTarget;
            changed |= _compiler.optRedundantBranch(block);
            if (block.Kind is BBJ_COND)
            {
                changed |= _compiler.optRedundantDominatingBranch(block);
            }

            if (changed && (block.Kind is BBJ_COND) && (block.CountOfInEdges > 0))
            {
                JITDUMP($"Will retry RBO in {FMT_BB(block.bbNum)} after partial optimization\n");
                changed |= _compiler.optRedundantBranch(block);
            }

            if (changed && (falseTarget.CountOfInEdges == 0))
            {
                foreach (var successor in falseTarget.Succs)
                {
                    JITDUMP($"Will retry RBO in {FMT_BB(successor.bbNum)}; " +
                        $"pred {FMT_BB(falseTarget.bbNum)} now unreachable\n");
                    _ = _compiler.optRedundantBranch(successor);
                }
            }

            if (changed && (trueTarget.CountOfInEdges == 0))
            {
                foreach (var successor in trueTarget.Succs)
                {
                    JITDUMP($"Will retry RBO in {FMT_BB(successor.bbNum)}; " +
                        $"pred {FMT_BB(trueTarget.bbNum)} now unreachable\n");
                    _ = _compiler.optRedundantBranch(successor);
                }
            }

            MadeChanges |= changed;
        }

        public readonly void End()
        {
        }

        public void WalkTree(FlowGraphDominatorTree tree)
            => IDomTreeVisitor<OptRedundantBranchesDomTreeVisitor>.WalkTree(ref this, _compiler, tree);
    }

    public PhaseStatus optRedundantBranches()
    {
#if DEBUG
        if (verbose)
        {
            fgDispBasicBlocks(verboseTrees);
        }
#endif
        optReachableBitVecTraits = null;
        var visitor = new OptRedundantBranchesDomTreeVisitor(this);
        assert(_domTree is not null);
        visitor.WalkTree(_domTree);

        if (visitor.MadeChanges)
        {
            foreach (var block in Blocks)
            {
                block.RemoveFlags(BBF_STALE_PREDICATE);
            }
        }

#if DEBUG
        if (verbose && visitor.MadeChanges)
        {
            fgDispBasicBlocks(verboseTrees);
        }
#endif
        fgInvalidateDfsTree();
        return visitor.MadeChanges ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING;
    }

    private bool optRedundantBranch(BasicBlock block)
    {
        JITDUMP($"\n--- Trying RBO in {FMT_BB(block.bbNum)} ---\n");
        var statement = block.LastStmt;
        if (statement is null)
        {
            return false;
        }

        var jumpTree = statement.RootNode;
        if (jumpTree.Oper is not GT_JTRUE)
        {
            return false;
        }

        var tree = jumpTree.AsUnOp().Op1;
        if (!tree.Oper.IsCompare)
        {
            return false;
        }

        assert(vnStore is not null);
        var domBlock = block.bbIDom;
        var relopValue = -1;
        var treeExcVN = ValueNumStore.NoVN;
        var domCmpExcVN = ValueNumStore.NoVN;
        var matchCount = 0;
        const int matchLimit = 4;
        vnStore.VNUnpackExc(tree._vnPair.Liberal, out var treeNormVN, out treeExcVN);

        if (vnStore.IsVNConstant(treeNormVN))
        {
            relopValue = treeNormVN == vnStore.VNZeroForType(TYP_INT) ? 0 : 1;
#if DEBUG
            JITDUMP($"Relop [{tree.TreeId:D6}] {FMT_BB(block.bbNum)} has known value " +
                $"{(relopValue == 0 ? "false" : "true")}\n ");
#endif
        }
        else if (domBlock is null)
        {
            return false;
        }
#if DEBUG
        else
        {
            JITDUMP($"Relop [{tree.TreeId:D6}] {FMT_BB(block.bbNum)} value unknown, trying inference\n");
        }
#endif

        var trySpeculativeDom = false;
        while ((relopValue == -1) && !trySpeculativeDom)
        {
            if (domBlock is null)
            {
                domBlock = fgGetDomSpeculatively(block);
                if (domBlock == block.bbIDom)
                {
                    break;
                }

                trySpeculativeDom = true;
            }

            if (domBlock is null)
            {
                break;
            }

            if ((domBlock.Kind is BBJ_COND) && !domBlock.HasFlag(BBF_STALE_PREDICATE))
            {
                var domJumpTree = domBlock.LastStmt?.RootNode;
                assert(domJumpTree is not null && domJumpTree.Oper is GT_JTRUE);
                var domCmpTree = domJumpTree.AsUnOp().Op1;
                if (domCmpTree.Oper.IsCompare)
                {
                    var info = new RelopImplicationInfo {
                        TreeNormVN = treeNormVN,
                    };
                    vnStore.VNUnpackExc(domCmpTree._vnPair.Liberal,
                        out info.DomCmpNormVN, out domCmpExcVN);
                    optRelopImpliesRelop(ref info);
                    if (info.CanInfer)
                    {
                        if (++matchCount > matchLimit)
                        {
                            JITDUMP($"Bailing out; {matchCount} matches found w/o optimizing\n");
                            break;
                        }

                        var trueSuccessor = domBlock.TrueTarget;
                        var falseSuccessor = domBlock.FalseTarget;
#if DEBUG
                        if (info.VnRelation is ValueNumStore.VN_RELATION_KIND.VRK_Inferred)
                        {
                            JITDUMP($"\nDominator {FMT_BB(domBlock.bbNum)} of {FMT_BB(block.bbNum)} can infer value of dominated relop\n");
                        }
                        else
                        {
                            JITDUMP($"\nDominator {FMT_BB(domBlock.bbNum)} of {FMT_BB(block.bbNum)} " +
                                $"has relop with {ValueNumStore.VNRelationString(info.VnRelation)} liberal VN\n");
                        }
                        DISPTREE(domCmpTree);
                        JITDUMP(" Redundant compare; current relop:\n");
                        DISPTREE(tree);
#endif
                        var trueReaches = optReachable(trueSuccessor, block, domBlock);
                        var falseReaches = optReachable(falseSuccessor, block, domBlock);
                        if (trueReaches && falseReaches && info.CanInferFromTrue && info.CanInferFromFalse)
                        {
                            if (trySpeculativeDom)
                            {
                                break;
                            }

                            if (optJumpThreadDom(block, domBlock, !info.ReverseSense,
                                domCmpExcVN, treeExcVN))
                            {
                                return true;
                            }
                        }
                        else if (trueReaches && !falseReaches && info.CanInferFromTrue)
                        {
                            relopValue = info.ReverseSense ? 0 : 1;
#if DEBUG
                            JITDUMP($"True successor {FMT_BB(trueSuccessor.bbNum)} of {FMT_BB(domBlock.bbNum)} " +
                                $"reaches, relop [{tree.TreeId:D6}] must be {(relopValue == 1 ? "true" : "false")}\n");
#endif
                            break;
                        }
                        else if (falseReaches && !trueReaches && info.CanInferFromFalse)
                        {
                            relopValue = info.ReverseSense ? 1 : 0;
#if DEBUG
                            JITDUMP($"False successor {FMT_BB(falseSuccessor.bbNum)} of {FMT_BB(domBlock.bbNum)} " +
                                $"reaches, relop [{tree.TreeId:D6}] must be {(relopValue == 0 ? "false" : "true")}\n");
#endif
                            break;
                        }
                        else if (!falseReaches && !trueReaches)
                        {
                            JITDUMP("inference failed -- no apparent path, will stop looking\n");
                            break;
                        }
                        else
                        {
                            JITDUMP("inference failed -- will keep looking higher\n");
                        }
                    }
                }
            }

            domBlock = domBlock.bbIDom;
        }

        if (relopValue == -1)
        {
            return optJumpThreadPhi(block, tree, treeNormVN);
        }

        if (((tree.Flags & GTF_EXCEPT) != 0) && block.hasTryIndex)
        {
            JITDUMP("Current relop has exception side effect and is in a try, so we won't optimize\n");
            return false;
        }

        var keepTree = (tree.Flags & GTF_SIDE_EFFECT) != 0;
        if (keepTree && ((tree.Flags & GTF_SIDE_EFFECT) == GTF_EXCEPT) &&
            vnStore.VNExcIsSubset(domCmpExcVN, treeExcVN))
        {
            keepTree = false;
        }

        if (keepTree)
        {
            JITDUMP("Current relop has side effects, keeping it, unused\n");
            jumpTree.AsUnOp().Op1 = gtNewCommaNode(TYP_INT, tree, gtNewIconNode(TYP_INT, relopValue));
        }
        else
        {
            var constant = new GenTreeIntCon(TYP_INT, relopValue, null, tree, NodeThreading.AllTrees);
            fgUpdateConstTreeValueNumber(constant);
            constant.Flags &= ~GTF_ALL_EFFECT;
            jumpTree.AsUnOp().Op1 = constant;
        }

        JITDUMP($"\nRedundant branch opt in {FMT_BB(block.bbNum)}:\n");
        _ = fgMorphBlockStmt(block, statement, allowFGChange: true,
            invalidateDFSTreeOnFGChange: false,
            message: $"{nameof(Compiler)}::{nameof(optRedundantBranch)}");
        Metrics.RedundantBranchesEliminated++;
        return true;
    }
}
