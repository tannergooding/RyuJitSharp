// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public struct GCSafePointSuccessorEnumerator
{
    private const int InlineSuccessorCount = 2;

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

        var numSuccs = 0;
        InlineArray2<BasicBlock> inlineSuccessors = default;

        _ = block.VisitRegularSuccs(comp, (succ) => {
            if (numSuccs < InlineSuccessorCount)
            {
                inlineSuccessors[numSuccs] = succ;
            }

            numSuccs++;

            return BasicBlockVisit.Continue;
        });

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
            var foundSuccs = 0;
            var successors = new BasicBlock[numSuccs];

            _ = block.VisitRegularSuccs(comp, (succ) => {
                assert(foundSuccs < numSuccs);
                successors[foundSuccs++] = succ;

                return BasicBlockVisit.Continue;
            });

            assert(foundSuccs == numSuccs);
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
