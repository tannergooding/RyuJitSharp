// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;
using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public partial class Compiler
{
    private void optHoistLoopBlocks(FlowGraphNaturalLoop loop, BitVecTraits traits, BitVec defExecuted,
        LoopHoistContext context)
    {
        var visitor = new LoopHoistVisitor(this, loop, context, traits, defExecuted);
        _ = loop.VisitLoopBlocksReversePostOrder(block => {
            visitor.HoistBlock(block);
            return BasicBlockVisit.Continue;
        });
        context.ResetHoistedInCurLoop();
    }

    private sealed class HoistValue(GenTree node)
    {
        public GenTree Node { get; } = node;
        public bool Hoistable;
        public bool CctorDependent;
        public bool Invariant;
        public string FailReason = "unset";
    }

    private struct LoopHoistVisitor : IGenTreeVisitor<LoopHoistVisitor>
    {
        public static bool DoPreOrder => true;
        public static bool DoPostOrder => true;
        public static bool UseExecutionOrder => true;

        private readonly Compiler _compiler;
        private readonly FlowGraphNaturalLoop _loop;
        private readonly LoopHoistContext _context;
        private readonly BitVecTraits _traits;
        private readonly BitVec _defExecuted;
        private readonly GenTreeStack _ancestors;
        private readonly List<HoistValue> _values;
        private bool _canHoistSideEffects;
        private BasicBlock? _currentBlock;

        public LoopHoistVisitor(Compiler compiler, FlowGraphNaturalLoop loop,
            LoopHoistContext context, BitVecTraits traits, BitVec defExecuted)
        {
            _compiler = compiler;
            _loop = loop;
            _context = context;
            _traits = traits;
            _defExecuted = defExecuted;
            _ancestors = [];
            _values = [];
            _canHoistSideEffects = true;
        }

        private readonly bool IsNodeHoistable(GenTree node)
        {
            if (node.Type is TYP_STRUCT)
            {
                return false;
            }
            if (node.Oper is GT_NULLCHECK)
            {
                return true;
            }
            if ((node.Flags & GTF_ORDER_SIDEEFF) != 0)
            {
                return false;
            }

            return _compiler.optIsCSEcandidate(node);
        }

        private readonly bool IsTreeVNInvariant(GenTree tree)
        {
            var invariant = _compiler.optVNIsLoopInvariant(tree._vnPair.Liberal, _loop,
                _context.CurLoopVnInvariantCache);
            return invariant && IsTreeLoopMemoryInvariant(tree);
        }

        private readonly bool IsTreeLoopMemoryInvariant(GenTree tree)
        {
            if (tree.Oper is GT_CALL)
            {
                return true;
            }

            if (_compiler.NodeToLoopMemoryBlockMap.TryGetValue(tree, out var entryBlock))
            {
                foreach (var kind in new AllMemoryKinds())
                {
                    var memoryVN = _compiler.GetMemoryPerSsaData(entryBlock.bbMemorySsaNumIn[(int)kind])
                        ._vnPair.Liberal;
                    if (!_compiler.optVNIsLoopInvariant(memoryVN, _loop,
                        _context.CurLoopVnInvariantCache))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private readonly bool IsHoistableOverExcepSibling(GenTree node, bool siblingHasExcep)
        {
#if DEBUG
            JITDUMP($"      [{node.TreeId:D6}]");
#endif
            if ((node.Flags & GTF_ALL_EFFECT) != 0 && siblingHasExcep)
            {
                JITDUMP(" not hoistable: cannot move past node that throws exception.\n");
                return false;
            }

            JITDUMP(" hoistable\n");
            return true;
        }

        public void HoistBlock(BasicBlock block)
        {
            _currentBlock = block;
            var blockWeight = block.getBBWeight(_compiler);
#if DEBUG
            JITDUMP($"\n    HoistBlock {FMT_BB(block.bbNum)} " +
                $"(weight={refCntWtd2str(blockWeight, padForDecimalPlaces: true),6}) " +
                $"of loop L{_loop.Index:D2} (head: {FMT_BB(_loop.Header.bbNum)})\n");
#endif
            if (blockWeight < BB_UNITY_WEIGHT / 10)
            {
                JITDUMP("      block weight is too small to perform hoisting.\n");
            }
            else
            {
                for (var stmt = block.GetFirstNonPhiDef(); stmt is not null; stmt = stmt.NextStmt)
                {
                    _ = WalkTree(ref stmt.RootNodeRef, null);
                    var top = _values[^1];
                    assert(top.Node == stmt.RootNode);
                    if (top.Hoistable)
                    {
                        var defExecuted = BitVecOps.IsMember(_traits, _defExecuted, block.bbPostorderNum);
                        _compiler.optHoistCandidate(top.Node, block, _loop, _context, defExecuted);
                    }
                    else
                    {
#if DEBUG
                        JITDUMP($"      [{top.Node.TreeId:D6}] " +
                            $"{(top.Invariant ? "not hoistable" : "not invariant")}: {top.FailReason}\n");
#endif
                    }
                    _values.Clear();
                }
            }

            assert(!_canHoistSideEffects || block == _loop.Header);
            _canHoistSideEffects = false;
        }

        public readonly fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
        {
#if DEBUG
            JITDUMP($"----- PreOrderVisit for [{use.TreeId:D6}] {use.Oper}\n");
#endif
            _values.Add(new HoistValue(use));
            return WALK_CONTINUE;
        }

        public fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user)
        {
            var tree = use;
#if DEBUG
            JITDUMP($"----- PostOrderVisit for [{tree.TreeId:D6}] {tree.Oper}\n");
#endif
            if (tree.Oper.IsLocalRead)
            {
                var local = tree.AsLclVarCommon();
                var invariant = local.HasSsaName;
                if (invariant)
                {
                    var definition = _compiler.lvaGetDesc(local.LclNum).GetPerSsaData(local.SsaNum).Block
                        ?? throw new FatalJitException("Loop hoisting requires an SSA definition block.");
                    invariant = !_loop.ContainsBlock(definition);
                }
                invariant = invariant && IsTreeVNInvariant(tree);
                var top = _values[^1];
                assert(top.Node == tree);
                if (invariant)
                {
                    top.Invariant = true;
                    top.Hoistable = IsNodeHoistable(tree);
                }

                if (!invariant)
                {
                    top.FailReason = "local, not rvalue / not in SSA / defined within current loop";
                }
                else if (!top.Hoistable)
                {
                    top.FailReason = "not handled by hoisting or CSE";
                }

#if DEBUG
                JITDUMP($"      [{tree.TreeId:D6}] {tree.Oper}: " +
                    $"{(top.Invariant ? (top.Hoistable ? "hoistable" : "not hoistable") : "not invariant")}: " +
                    $"{top.FailReason}\n");
#endif
                return WALK_CONTINUE;
            }

            var cctorDependent = tree.Oper.IsIndir && (tree.Flags & GTF_IND_INITCLASS) != 0;
            var invariantTree = true;
            var hasHoistableChildren = false;
            var failReason = "unknown";
            var parent = _values.Count - 1;
            while (_values[parent].Node != tree)
            {
                var child = _values[parent];
                hasHoistableChildren |= child.Hoistable;
                if (!child.Invariant)
                {
                    invariantTree = false;
                    failReason = "variant child";
                }
                if (child.CctorDependent)
                {
                    cctorDependent = true;
                    if (tree.Oper is GT_COMMA && child.Node == tree.AsOp().Op2 &&
                        tree.AsOp().Op1 is GenTreeCall call &&
                        call.IsHelperCall() && call.HelperNum.MayRunCctor)
                    {
                        cctorDependent = false;
                        assert(!child.Hoistable);
                    }
                }
                parent--;
            }

            var hoistable = invariantTree && !cctorDependent;
            if (invariantTree && !hoistable)
            {
                failReason = "cctor dependent";
            }

            if (invariantTree)
            {
                if (hoistable)
                {
                    hoistable = IsNodeHoistable(tree);
                    if (!hoistable)
                    {
                        failReason = "not handled by hoisting or CSE";
                    }
                }

                if (hoistable && tree.Oper is GT_CALL)
                {
                    var call = tree.AsCall();
                    if (!call.IsHelperCall())
                    {
                        failReason = "non-helper call";
                        hoistable = false;
                    }
                    else if (!call.HelperNum.IsPure)
                    {
                        failReason = "impure helper call";
                        hoistable = false;
                    }
                    else if (call.HelperNum.MayRunCctor && (call.Flags & GTF_CALL_HOISTABLE) == 0)
                    {
                        failReason = "non-hoistable helper call";
                        hoistable = false;
                    }
                }

                if (hoistable && !_canHoistSideEffects && (tree.Flags & GTF_EXCEPT) != 0)
                {
                    failReason = "side effect ordering constraint";
                    hoistable = false;
                }

                invariantTree = IsTreeVNInvariant(tree);
                if (!invariantTree)
                {
                    failReason = "tree VN is loop variant";
                    hoistable = false;
                }
            }

            if (_canHoistSideEffects)
            {
                if (!invariantTree && tree.Oper is not GT_CALL && tree.MayThrow(_compiler))
                {
                    assert(!hoistable);
                    _canHoistSideEffects = false;
                }

                if (tree.Oper is GT_CALL)
                {
                    var call = tree.AsCall();
                    if (!call.IsHelperCall())
                    {
                        _canHoistSideEffects = false;
                    }
                    else
                    {
                        var helper = call.HelperNum;
                        if (helper.MutatesHeap || (helper.MayRunCctor && (call.Flags & GTF_CALL_HOISTABLE) == 0) ||
                            (!invariantTree && !helper.NoThrow))
                        {
                            _canHoistSideEffects = false;
                        }
                    }
                }
                else if (tree.RequiresAsgFlag)
                {
                    var visible = !tree.Oper.IsLocalStore ||
                        _compiler.lvaGetDesc(tree.AsLclVarCommon().LclNum).IsAddressExposed;
                    if (visible)
                    {
                        failReason = "store to globally visible memory";
                        hoistable = false;
                        _canHoistSideEffects = false;
                    }
                }
            }

            if (!hoistable && hasHoistableChildren)
            {
                var visitedCurrent = false;
                var isComma = tree.Oper is GT_COMMA;
                var hasExcep = false;
                foreach (var value in _values)
                {
                    if (value.Hoistable)
                    {
                        assert(value.Node != tree);
                        if (IsHoistableOverExcepSibling(value.Node, hasExcep))
                        {
                            var block = _currentBlock!;
                            var defExecuted = BitVecOps.IsMember(_traits, _defExecuted, block.bbPostorderNum);
                            _compiler.optHoistCandidate(value.Node, block, _loop, _context, defExecuted);
                        }
                        value.Hoistable = false;
                        value.Invariant = false;
                    }
                    else if (value.Node != tree)
                    {
                        if (visitedCurrent && isComma)
                        {
                            hasExcep = (tree.Flags & GTF_EXCEPT) != 0;
                        }
#if DEBUG
                        JITDUMP($"      [{value.Node.TreeId:D6}] " +
                            $"{(value.Invariant ? "not hoistable" : "not invariant")}: {value.FailReason}\n");
#endif
                    }
                    else
                    {
                        visitedCurrent = true;
#if DEBUG
                        JITDUMP($"      [{value.Node.TreeId:D6}] not hoistable : current node\n");
#endif
                    }
                }
            }

            _values.RemoveRange(parent + 1, _values.Count - parent - 1);
            var result = _values[parent];
            result.Hoistable = hoistable;
            result.CctorDependent = cctorDependent;
            result.Invariant = invariantTree;
            if (!invariantTree || !hoistable)
            {
                result.FailReason = failReason;
            }

            return WALK_CONTINUE;
        }

        public fgWalkResult WalkTree(ref GenTree use, GenTree? user)
            => IGenTreeVisitor<LoopHoistVisitor>.WalkTree(ref this, ref use, user, _ancestors);
    }
}
