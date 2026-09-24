// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.

using System.Collections.Generic;
using System.Numerics;

namespace RyuJitSharp;

internal readonly record struct LocalEqualsLocalAddrAssertion(int DestLclNum, int AddressLclNum, uint AddressOffset)
{
#if DEBUG
    internal void Print() => jitprintf($"V{DestLclNum:D2} = &V{AddressLclNum:D2}[+{AddressOffset:D3}]");
#endif
}

internal sealed class LocalEqualsLocalAddrAssertions
{
    private readonly Compiler _compiler;
    private readonly LoopDefinitions _loopDefs;
    private readonly List<LocalEqualsLocalAddrAssertion> _assertions = [];
    private readonly Dictionary<LocalEqualsLocalAddrAssertion, int> _map = [];
    private readonly ulong[] _lclAssertions;
    private readonly ulong[] _outgoingAssertions;
    private readonly ulong[] _alwaysTrueAssertions;
    private readonly BitVec _localsToExpose;

    internal ulong CurrentAssertions;
    internal ulong AlwaysAssertions;

    internal LocalEqualsLocalAddrAssertions(Compiler compiler, LoopDefinitions loopDefs)
    {
        _compiler = compiler;
        _loopDefs = loopDefs;
        _lclAssertions = new ulong[compiler.lvaCount];
        var dfs = compiler._dfsTree;
        assert(dfs is not null);
        _outgoingAssertions = new ulong[dfs.PostOrderCount];
        _alwaysTrueAssertions = new ulong[dfs.PostOrderCount];
        _localsToExpose = BitVecOps.MakeEmpty(new BitVecTraits(compiler, compiler.lvaCount));
    }

    internal BitVec GetLocalsToExpose() => _localsToExpose;

    internal bool IsMarkedForExposure(int lclNum)
    {
        var traits = new BitVecTraits(_compiler, _compiler.lvaCount);

        if (BitVecOps.IsMember(traits, _localsToExpose, lclNum))
        {
            return true;
        }

        ref var varDsc = ref _compiler.lvaGetDesc(lclNum);
        return varDsc.lvIsStructField && BitVecOps.IsMember(traits, _localsToExpose, varDsc.lvParentLcl);
    }

    internal void StartBlock(BasicBlock block)
    {
        CurrentAssertions = 0;

        if (_assertions.Count is 0)
        {
            AlwaysAssertions = 0;
            return;
        }

        var preds = _compiler.BlockPredsWithEH(block);
        var first = true;
        FlowGraphNaturalLoop? loop = null;
        var assertionMap = _compiler.bbIsHandlerBeg(block) ? _alwaysTrueAssertions : _outgoingAssertions;
        var dfs = _compiler._dfsTree;
        assert(dfs is not null);

        for (var predEdge = preds; predEdge is not null; predEdge = predEdge.NextPredEdge)
        {
            var pred = predEdge.SourceBlock;

            if (!dfs.Contains(pred))
            {
                // Implicit exceptional edges can originate in unreachable blocks.
                continue;
            }

            if (pred.bbPostorderNum <= block.bbPostorderNum)
            {
                var loops = _compiler._loops;
                assert(loops is not null);
                loop = loops.GetLoopByHeader(block);

                if ((loop is not null) && loop.ContainsBlock(pred))
                {
                    JITDUMP($"Ignoring loop backedge {FMT_BB(pred.bbNum)}->{FMT_BB(block.bbNum)}\n");
                    continue;
                }

                JITDUMP($"Found non-loop backedge {FMT_BB(pred.bbNum)}->{FMT_BB(block.bbNum)}, clearing assertions\n");
                CurrentAssertions = 0;
                break;
            }

            var previousAssertions = assertionMap[pred.bbPostorderNum];

            if (first)
            {
                CurrentAssertions = previousAssertions;
                first = false;
            }
            else
            {
                CurrentAssertions &= previousAssertions;
            }
        }

        AlwaysAssertions = CurrentAssertions;

        if ((loop is not null) && (CurrentAssertions is not 0))
        {
            JITDUMP($"Block {FMT_BB(block.bbNum)} is a loop header; clearing assertions about defined locals\n");
            _loopDefs.VisitDefinedLocalNums(loop, lclNum => {
                JITDUMP($"  V{lclNum:D2}");
                Clear(lclNum);
            });
            JITDUMP("\n");
        }

#if DEBUG
        if (CurrentAssertions is not 0)
        {
            JITDUMP($"{FMT_BB(block.bbNum)} incoming assertions:\n");
            var assertions = CurrentAssertions;

            do
            {
                var index = BitOperations.TrailingZeroCount(assertions);
                JITDUMP($"  A{index:D2}: ");

                if (_compiler.verbose)
                {
                    _assertions[index].Print();
                }

                JITDUMP("\n");
                assertions ^= 1UL << index;
            }
            while (assertions is not 0);
        }
#endif
    }

    internal void EndBlock(BasicBlock block)
    {
        _outgoingAssertions[block.bbPostorderNum] = CurrentAssertions;
        _alwaysTrueAssertions[block.bbPostorderNum] = AlwaysAssertions;
    }

    internal void OnExposed(int lclNum)
    {
        JITDUMP($"On exposed: V{lclNum:D2}\n");
        BitVecOps.AddElemD(new BitVecTraits(_compiler, _compiler.lvaCount), _localsToExpose, lclNum);
    }

    internal void Record(int dstLclNum, int srcLclNum, uint srcOffs)
    {
        var assertion = new LocalEqualsLocalAddrAssertion(dstLclNum, srcLclNum, srcOffs);
        int index;

        if (_assertions.Count >= 64)
        {
            if (!_map.TryGetValue(assertion, out index))
            {
                JITDUMP("Out of assertion space; dropping assertion ");
#if DEBUG
                if (_compiler.verbose)
                {
                    assertion.Print();
                }
#endif
                JITDUMP("\n");
                return;
            }
        }
        else
        {
            if (!_map.TryGetValue(assertion, out index))
            {
                index = _assertions.Count;
                _map.Add(assertion, index);
                _assertions.Add(assertion);
                _lclAssertions[dstLclNum] |= 1UL << index;
                JITDUMP($"Adding new assertion A{index:D2} ");
            }
            else
            {
                JITDUMP($"Adding existing assertion A{index:D2} ");
            }

#if DEBUG
            if (_compiler.verbose)
            {
                assertion.Print();
            }
#endif
            JITDUMP("\n");
        }

        CurrentAssertions |= 1UL << index;
    }

    internal void Clear(int dstLclNum)
    {
        CurrentAssertions &= ~_lclAssertions[dstLclNum];
        AlwaysAssertions &= CurrentAssertions;
    }

    internal LocalEqualsLocalAddrAssertion? GetCurrentAssertion(int lclNum)
    {
        var current = CurrentAssertions & _lclAssertions[lclNum];
        assert((current is 0) || BitOperations.IsPow2(current));
        return current is 0 ? null : _assertions[BitOperations.TrailingZeroCount(current)];
    }

    internal BitVec GetLocalsWithAssertions()
    {
        var traits = new BitVecTraits(_compiler, _compiler.lvaCount);
        var result = BitVecOps.MakeEmpty(traits);

        foreach (var assertion in _assertions)
        {
            BitVecOps.AddElemD(traits, result, assertion.DestLclNum);
        }

        return result;
    }
}
