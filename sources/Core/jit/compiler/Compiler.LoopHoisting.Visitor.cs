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
        public int SubtreeEnd;
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
        private readonly List<HoistValue> _values;
        private readonly List<int> _valueStack;
        private int _nextValue;
        private bool _hoisting;
        private GenTree? _hoistedTree;
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
            _values = [];
            _valueStack = [];
            _nextValue = 0;
            _hoisting = false;
            _hoistedTree = null;
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

        public void HoistBlock(BasicBlock block)
        {
            _currentBlock = block;
            var blockWeight = block.getBBWeight(_compiler);
            JITDUMP($"\n    HoistBlock {FMT_BB(block.bbNum)} " +
                $"(weight={refCntWtd2str(blockWeight, padForDecimalPlaces: true),6}) " +
                $"of loop L{_loop.Index:D2} (head: {FMT_BB(_loop.Header.bbNum)})\n");
            if (blockWeight < BB_UNITY_WEIGHT / 10)
            {
                JITDUMP("      block weight is too small to perform hoisting.\n");
            }
            else
            {
                for (var stmt = block.GetFirstNonPhiDef(); stmt is not null; stmt = stmt.NextStmt)
                {
                    var canHoistBefore = _canHoistSideEffects;
                    _ = WalkTree(ref stmt.RootNodeRef, null);

                    assert(_valueStack.Count == 1);
                    _valueStack.Clear();

                    // Classify bottom-up, then select in execution order so enclosing candidates win.
                    var canHoistAfter = _canHoistSideEffects;
                    _canHoistSideEffects = canHoistBefore;
                    _nextValue = 0;
                    _hoisting = true;
                    _ = WalkTree(ref stmt.RootNodeRef, null);
                    assert(_nextValue == _values.Count);
                    assert(_hoistedTree is null);
                    _hoisting = false;
                    _canHoistSideEffects &= canHoistAfter;
                    _values.Clear();
                }
            }

            assert(!_canHoistSideEffects || block == _loop.Header);
            _canHoistSideEffects = false;
        }

        public fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
        {
            JITDUMP($"----- Hoist {(_hoisting ? "selection" : "classification")} PreOrderVisit for " +
                $"[{use.TreeId:D6}] {use.Oper.Name}\n");
            if (_hoisting)
            {
                var value = _values[_nextValue++];
                assert(value.Node == use);

                if (value.Hoistable && (_canHoistSideEffects || ((use.Flags & GTF_EXCEPT) == 0)))
                {
                    JITDUMP($"      [{use.TreeId:D6}] {use.Oper.Name}: hoist selection: considering candidate\n");
                    var defExecuted = BitVecOps.IsMember(_traits, _defExecuted, _currentBlock!.bbPostorderNum);
                    if (_compiler.optHoistCandidate(use, _currentBlock, _loop, _context, defExecuted))
                    {
                        JITDUMP($"      [{use.TreeId:D6}] {use.Oper.Name}: hoist selection: accepted, skipping descendants\n");
                        _nextValue = value.SubtreeEnd;
                        _hoistedTree = use;
                        return WALK_SKIP_SUBTREES;
                    }
                }
                else if (!value.Hoistable)
                {
                    JITDUMP($"      [{use.TreeId:D6}] {use.Oper.Name}: hoist selection: " +
                        $"{(value.Invariant ? "not hoistable" : "not invariant")}: {value.FailReason}\n");
                }
                else
                {
                    JITDUMP($"      [{use.TreeId:D6}] {use.Oper.Name}: hoist selection: blocked by earlier exception\n");
                }

                return WALK_CONTINUE;
            }

            _values.Add(new HoistValue(use));
            _valueStack.Add(_values.Count - 1);
            return WALK_CONTINUE;
        }

        public fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user)
        {
            var tree = use;
            JITDUMP($"----- Hoist {(_hoisting ? "selection" : "classification")} PostOrderVisit for " +
                $"[{tree.TreeId:D6}] {tree.Oper.Name}\n");
            if (_hoisting)
            {
                if (_hoistedTree == tree)
                {
                    _hoistedTree = null;
                }
                else if (tree.MayThrow(_compiler))
                {
                    if (_canHoistSideEffects)
                    {
                        JITDUMP($"      [{tree.TreeId:D6}] {tree.Oper.Name}: " +
                            "unhoisted operation closes exception-ordering barrier\n");
                    }

                    _canHoistSideEffects = false;
                }

                return WALK_CONTINUE;
            }

            if (tree.Oper.IsLocalRead)
            {
                var local = tree.AsLclVarCommon();
                var invariant = local.HasSsaName;
                if (invariant)
                {
                    var definition = _compiler.lvaGetDesc(local.LclNum).GetPerSsaData(local.SsaNum).Block;
                    if (definition is null)
                    {
                        FatalJitException.ThrowFatalJitException(
        CORJIT_INTERNALERROR,
        FatalJitErrorCode.LoopHoistingRequiresAnSSADefinitionBlock
    );
                        return default!;
                    }
                    invariant = !_loop.ContainsBlock(definition);
                }
                invariant = invariant && IsTreeVNInvariant(tree);
                var top = _values[_valueStack[^1]];
                assert(top.Node == tree);
                top.SubtreeEnd = _values.Count;
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

                JITDUMP($"      [{tree.TreeId:D6}] {tree.Oper.Name}: " +
                    $"{(top.Invariant ? (top.Hoistable ? "hoistable" : "not hoistable") : "not invariant")}: " +
                    $"{top.FailReason}\n");
                return WALK_CONTINUE;
            }

            var cctorDependent = tree.Oper.IsIndir && (tree.Flags & GTF_IND_INITCLASS) != 0;
            var invariantTree = true;
            var failReason = "unknown";
            var childCount = 0;
            var currentValue = _valueStack.Count - 1;
            while (_values[_valueStack[currentValue - childCount]].Node != tree)
            {
                var child = _values[_valueStack[currentValue - childCount]];
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

                childCount++;
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
                if (!hoistable && tree.Oper is not GT_CALL && tree.MayThrow(_compiler))
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
                        if (helper.MutatesHeap ||
                            ((call._callMoreFlags & GTF_CALL_M_ALLOC_SIDE_EFFECTS) != 0) ||
                            (helper.MayRunCctor && (call.Flags & GTF_CALL_HOISTABLE) == 0) ||
                            (!hoistable && !helper.NoThrowFor(_compiler)))
                        {
                            _canHoistSideEffects = false;
                        }
                    }
                }
                else if (tree.RequiresAsgFlag)
                {
                    var visible = true;
                    if (tree.Oper.IsLocalStore)
                    {
                        var descriptor = _compiler.lvaGetDesc(tree.AsLclVarCommon().LclNum);
                        visible = descriptor.IsAddressExposed ||
                            (_currentBlock!.HasPotentialEHSuccs(_compiler) &&
                             (!descriptor.lvTracked || descriptor.IsLiveInOutOfHandler));
                    }

                    if (visible)
                    {
                        failReason = "store to globally visible memory";
                        hoistable = false;
                        _canHoistSideEffects = false;
                    }
                }
            }

            _valueStack.RemoveRange(_valueStack.Count - childCount, childCount);
            var result = _values[_valueStack[^1]];
            result.Hoistable = hoistable;
            result.CctorDependent = cctorDependent;
            result.Invariant = invariantTree;
            result.SubtreeEnd = _values.Count;
            if (!invariantTree || !hoistable)
            {
                result.FailReason = failReason;
            }

            return WALK_CONTINUE;
        }

        public fgWalkResult WalkTree(ref GenTree use, GenTree? user)
            => IGenTreeVisitor<LoopHoistVisitor>.WalkTree(ref this, ref use, user);
    }
}
