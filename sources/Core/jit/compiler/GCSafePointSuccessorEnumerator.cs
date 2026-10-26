// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public struct GCSafePointSuccessorEnumerator
{
    private const int InlineSuccessorCount = 2;

    private struct InlineSuccessorVisitor : IBasicBlockVisitor<InlineSuccessorVisitor>
    {
        private InlineArray2<BasicBlock> _successors;
        private int _count;

        public readonly int Count => _count;

        public readonly InlineArray2<BasicBlock> Successors => _successors;

        public BasicBlockVisit Visit(BasicBlock successor)
        {
            if (_count < InlineSuccessorCount)
            {
                _successors[_count] = successor;
            }

            _count++;
            return BasicBlockVisit.Continue;
        }
    }

    private struct SuccessorArrayVisitor : IBasicBlockVisitor<SuccessorArrayVisitor>
    {
        private readonly BasicBlock[] _successors;
        private int _count;

        public SuccessorArrayVisitor(BasicBlock[] successors)
        {
            _successors = successors;
            _count = 0;
        }

        public readonly int Count => _count;

        public BasicBlockVisit Visit(BasicBlock successor)
        {
            assert(_count < _successors.Length);
            _successors[_count++] = successor;
            return BasicBlockVisit.Continue;
        }
    }

    private BasicBlock _block;

    private InlineArray2<BasicBlock> _inlineSuccessors;
    private BasicBlock[]? _successors;

    private int _numSuccs;
    private int _curSucc = -1;

    // Constructs an enumerator of successors to be used for checking for GC
    // safe point cycles.
    public GCSafePointSuccessorEnumerator(Compiler comp, BasicBlock block)
    {
        _block = block;

        var inlineSuccessorVisitor = new InlineSuccessorVisitor();
        _ = block.VisitRegularSuccs(comp, ref inlineSuccessorVisitor);
        var numSuccs = inlineSuccessorVisitor.Count;
        var inlineSuccessors = inlineSuccessorVisitor.Successors;

        if (numSuccs is 0)
        {
            if (block.EndsWithTailCallOrJmp(comp, true))
            {
                // This tail call might combine with other tail calls to form a
                // loop. Add a pseudo successor back to the entry to model this.

                assert(comp.fgFirstBB is not null);
                _inlineSuccessors[0] = comp.fgFirstBB;

                _numSuccs = 1;
                return;
            }
        }
        else
        {
            assert(!block.EndsWithTailCallOrJmp(comp, true));
        }

        if (numSuccs > InlineSuccessorCount)
        {
            var successors = new BasicBlock[numSuccs];
            var successorArrayVisitor = new SuccessorArrayVisitor(successors);
            _ = block.VisitRegularSuccs(comp, ref successorArrayVisitor);
            assert(successorArrayVisitor.Count == numSuccs);
            _successors = successors;
        }
        else
        {
            _inlineSuccessors = inlineSuccessors;
        }

        _numSuccs = numSuccs;
    }

    /// <summary>Gets the block whose successors are enumerated.</summary>
    public readonly BasicBlock Block => _block;

    /// <summary>Returns the next available successor or `null` if there are no more successors.</summary>
    public BasicBlock? NextSuccessor
    {
        get
        {
            _curSucc++;

            if (_curSucc >= _numSuccs)
            {
                return null;
            }

            if (_successors is not null)
            {
                return _successors[_curSucc];
            }

            return _inlineSuccessors[_curSucc];
        }
    }
}
