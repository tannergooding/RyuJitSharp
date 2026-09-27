// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
using System.Collections.Generic;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public partial class Compiler
{
    public void fgDebugCheckInitBB()
    {
        assert(fgFirstBB is not null);
        assert(!fgFirstBB.hasTryIndex);
        assert(fgFirstBB.bbPreds is null);
        assert(!opts.compDbgCode || fgFirstBB.HasFlag(BBF_INTERNAL));
    }

    public void fgDebugCheckFlowGraphAnnotations()
    {
        var dfsTree = _dfsTree;
        if (dfsTree is null)
        {
            assert(_loops is null);
            assert((_domTree is null) && (_domFrontiers is null));
            assert(_reachabilitySets is null);
            return;
        }

        static void VisitPreorder(BasicBlock block, int index)
        {
            assert(block.bbPreorderNum == index);
        }

        void VisitPostorder(BasicBlock block, int index)
        {
            assert(block.bbPostorderNum == index);
            assert(dfsTree.GetPostOrder(index) == block);
        }

        assert(fgFirstBB is not null);
        var entries = new List<BasicBlock> { fgFirstBB };
        if (fgEntryBB is not null)
        {
            assert(opts.IsOSR);
            entries.Add(fgEntryBB);
        }

        if ((genReturnBB is not null) && !fgGlobalMorphDone)
        {
            entries.Add(genReturnBB);
        }

        var count = fgRunDfs(VisitPreorder, VisitPostorder, static (block, successor) => { },
            entries, dfsTree.IsProfileAware);
        assert(dfsTree.PostOrderCount == count);

        assert((_loops is null) || (_loops.DfsTree == dfsTree));
        assert((_domTree is null) || (_domTree.GetDfsTree() == dfsTree));
        assert((_domFrontiers is null) || (_domFrontiers.GetDomTree() == _domTree));
        assert((_reachabilitySets is null) || (_reachabilitySets.GetDfsTree() == dfsTree));
    }

    public void fgDebugCheckLoops()
    {
        if (_loops is null)
        {
            return;
        }

        if (optLoopsCanonical)
        {
            foreach (var loop in _loops.InReversePostOrder())
            {
                assert(loop.EntryEdges.Length == 1);
                assert(loop.EntryEdge(0).SourceBlock.Kind is BBJ_ALWAYS);
                assert(!bbIsTryBeg(loop.Header));
                assert(loop.BackEdges.Length == 1);

                _ = loop.VisitRegularExitBlocks(exit => {
                    foreach (var predecessor in exit.PredBlocks)
                    {
                        if (!loop.ContainsBlock(predecessor))
                        {
                            JITDUMP($"Loop L{loop.Index:D2} exit {FMT_BB(exit.bbNum)} has non-loop predecessor {FMT_BB(predecessor.bbNum)}\n");
                            assert(false, "Loop exit has non-loop predecessor");
                        }
                    }

                    return BasicBlockVisit.Continue;
                });
            }
        }
    }

    public void fgDebugCheckNodesUniqueness()
    {
        var walker = new UniquenessCheckWalker(this);
        foreach (var block in Blocks)
        {
            if (block.IsLIR)
            {
                for (var tree = block.FirstNode; tree is not null; tree = tree.Next)
                {
                    walker.CheckTreeId(tree.TreeId);
                }
            }
            else if (fgNodeThreading is NodeThreading.AllTrees)
            {
                foreach (var statement in block.Statements)
                {
                    foreach (var tree in statement.TreeList)
                    {
                        walker.CheckTreeId(tree.TreeId);
                    }
                }
            }
            else
            {
                foreach (var statement in block.Statements)
                {
                    _ = walker.WalkTree(ref statement.RootNodeRef, user: null);
                }
            }
        }
    }

    public void fgDebugCheckLinkedLocals()
    {
        if (fgNodeThreading is not NodeThreading.AllLocals)
        {
            return;
        }

        var sequencer = new DebugLocalSequencer(this);
        foreach (var block in Blocks)
        {
            foreach (var statement in block.Statements)
            {
                var first = statement.TreeListBegin;
                CheckDoublyLinkedList(first);
                sequencer.Sequence(statement);
                var expected = sequencer.Locals;
                var success = true;

                if (expected.Count > 0)
                {
                    success &= (statement.TreeListBegin == expected[0]) && (statement.TreeListEnd == expected[^1]);
                }
                else
                {
                    success &= (statement.TreeListBegin is null) && (statement.TreeListEnd is null);
                }

                var nodeIndex = 0;
                for (var current = first; current is not null; current = current.Next)
                {
                    success &= current.Oper.IsAnyLocal;
                    success &= (nodeIndex < expected.Count) && (current == expected[nodeIndex]);
                    nodeIndex++;
                }

                success &= nodeIndex == expected.Count;

                if (!success && verbose)
                {
                    jitprintf("Locals are improperly linked in the following statement:\n");
                    DISPSTMT(statement);
                    jitprintf("\nExpected:\n");
                    var prefix = "  ";
                    foreach (var node in expected)
                    {
                        jitprintf($"{prefix}[{node.TreeId:D6}]");
                        prefix = " -> ";
                    }

                    jitprintf("\n\nActual:\n");
                    prefix = "  ";
                    for (var current = first; current is not null; current = current.Next)
                    {
                        jitprintf($"{prefix}[{current.TreeId:D6}]");
                        prefix = " -> ";
                    }

                    jitprintf("\n");
                }

                assert(success, "Locals are improperly linked!");
            }
        }
    }

    private struct UniquenessCheckWalker : IGenTreeVisitor<UniquenessCheckWalker>
    {
        private readonly Compiler _compiler;
        private readonly BitVecTraits _traits;
        private readonly BitVec _nodes;
        private readonly GenTreeStack _ancestors;

        public UniquenessCheckWalker(Compiler compiler)
        {
            _compiler = compiler;
            _traits = new BitVecTraits(compiler, compiler.compGenTreeID);
            _nodes = BitVecOps.MakeEmpty(_traits);
            _ancestors = [];
        }

        public static bool DoPreOrder => true;

        public readonly void CheckTreeId(int treeId)
        {
            if (!BitVecOps.TryAddElemD(_traits, _nodes, treeId))
            {
                if (_compiler.verbose)
                {
                    jitprintf($"Duplicate gtTreeID was found: {treeId}\n");
                }

                assert(false, "Duplicate gtTreeID was found");
            }
        }

        public readonly fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
        {
            CheckTreeId(use.TreeId);
            return WALK_CONTINUE;
        }

        public readonly fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user) => WALK_CONTINUE;

        public fgWalkResult WalkTree(ref GenTree use, GenTree? user)
            => IGenTreeVisitor<UniquenessCheckWalker>.WalkTree(ref this, ref use, user, _ancestors);
    }

    private struct DebugLocalSequencer : IGenTreeVisitor<DebugLocalSequencer>
    {
        private readonly Compiler _compiler;
        private readonly GenTreeStack _ancestors;

        public DebugLocalSequencer(Compiler compiler)
        {
            _compiler = compiler;
            _ancestors = [];
            Locals = [];
        }

        public static bool DoPostOrder => true;

        public static bool UseExecutionOrder => true;

        public readonly List<GenTree> Locals { get; }

        public void Sequence(Statement statement)
        {
            Locals.Clear();
            _ = WalkTree(ref statement.RootNodeRef, user: null);
        }

        public readonly fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user) => WALK_CONTINUE;

        public readonly fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user)
        {
            var node = use;
            if (node.Oper.IsAnyLocal &&
                ((user is null) || !user.Oper.IsCall || !IsDefinedByCall(user.AsCall(), node)))
            {
                Locals.Add(node);
            }

            if (node.Oper.IsCall)
            {
                var locals = Locals;
                _ = node.VisitPhysicalLocalDefNodes(_compiler, definition => {
                    assert(definition.Oper.IsAnyLocal);
                    locals.Add(definition);
                    return GenTree.VisitResult.Continue;
                });
            }

            return WALK_CONTINUE;
        }

        private readonly bool IsDefinedByCall(GenTreeCall call, GenTree node)
            => call.VisitPhysicalLocalDefNodes(_compiler, definition => node == definition
                ? GenTree.VisitResult.Abort : GenTree.VisitResult.Continue) is GenTree.VisitResult.Abort;

        public fgWalkResult WalkTree(ref GenTree use, GenTree? user)
            => IGenTreeVisitor<DebugLocalSequencer>.WalkTree(ref this, ref use, user, _ancestors);
    }
}
#endif
