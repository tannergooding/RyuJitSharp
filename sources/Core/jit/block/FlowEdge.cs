// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;

namespace RyuJitSharp;

public sealed class FlowEdge
{
    // The next predecessor edge in the list, null for end of list.
    private FlowEdge? _nextPredEdge;

    // The source of the control flow
    private BasicBlock _sourceBlock;

    // The destination of the control flow
    private BasicBlock _destBlock;

    // Likelihood that _sourceBlock transfers control along this edge.
    // Values in range [0..1]
    private weight_t _likelihood;

    // The count of duplicate "edges" (used for switch stmts or degenerate branches)
    private int _dupCount;

    // Convenience flag for phases that need to track edge visitation
    private bool _visited;

    // Indicates if _likelihood was determined using profile synthesis's heuristics
    private bool _heuristicBasedLikelihood;

    // True if likelihood has been set
#if DEBUG
    private bool _likelihoodSet;
#endif

    public FlowEdge(BasicBlock sourceBlock, BasicBlock destBlock, FlowEdge? rest)
    {
        _nextPredEdge = rest;
        _sourceBlock = sourceBlock;
        _destBlock = destBlock;
    }

    public BasicBlock DestinationBlock
    {
        get
        {
            return _destBlock;
        }

        set
        {
            _destBlock = value;
        }
    }

    public int DupCount => _dupCount;

#if DEBUG
    public bool hasLikelihood => _likelihoodSet;
#endif

    public bool isHeuristicBased
    {
        get
        {
            return _heuristicBasedLikelihood;
        }

        set
        {
            _heuristicBasedLikelihood = value;
        }
    }

    public weight_t Likelihood
    {
        get
        {
#if DEBUG
            assert(Debugger.IsAttached || _likelihoodSet);
#endif
            return _likelihood;
        }

        set
        {
            assert(value >= 0.0);
            assert(value <= 1.0);

#if DEBUG
            if (_likelihoodSet)
            {
                JITDUMP($"setting likelihood of {FMT_BB(_sourceBlock.bbNum)} -> {FMT_BB(_destBlock.bbNum)} from {FMT_WT(_likelihood)} to {FMT_WT(value)}\n");
            }
            else
            {
                JITDUMP($"setting likelihood of {FMT_BB(_sourceBlock.bbNum)} -> {FMT_BB(_destBlock.bbNum)} to {FMT_WT(value)}\n");
            }

            _likelihoodSet = true;
#endif

            _likelihood = value;
        }
    }

    public weight_t LikelyWeight
    {
        get
        {
#if DEBUG
            assert(_likelihoodSet);
#endif
            return _likelihood * _sourceBlock.bbWeight;
        }
    }

    public FlowEdge? NextPredEdge
    {
        get
        {
            return _nextPredEdge;
        }

        set
        {
            _nextPredEdge = value;
        }
    }

#nullable disable
    public ref FlowEdge NextPredEdgeRef => ref _nextPredEdge;
#nullable restore

    public BasicBlock SourceBlock
    {
        get
        {
            return _sourceBlock;
        }

        set
        {
            _sourceBlock = value;
        }
    }

    /// <summary>adjust the likelihood of a flow edge </summary>
    /// <param name="addedLikelihood">value in range [-likelihood, 1.0 - likelihood] to add to current likelihood.</param>
    public void AddLikelihood(weight_t addedLikelihood)
    {
#if DEBUG
        assert(_likelihoodSet);
#endif

        var newLikelihood = _likelihood + addedLikelihood;

        // Tolerate slight overflow or underflow
        const weight_t eps = 0.0001;

        if ((newLikelihood < 0) && (newLikelihood > -eps))
        {
            newLikelihood = 0.0;
        }
        else if ((newLikelihood > 1) && (newLikelihood < 1 + eps))
        {
            newLikelihood = 1.0;
        }

        assert(newLikelihood >= 0.0);
        assert(newLikelihood <= 1.0);

        JITDUMP($"updating likelihood of {FMT_BB(_sourceBlock.bbNum)} -> {FMT_BB(_destBlock.bbNum)} from {FMT_WT(_likelihood)} to {FMT_WT(newLikelihood)}\n");
        _likelihood = newLikelihood;
    }

    public void decrementDupCount(int dupCount = 1)
    {
        assert(_dupCount >= dupCount);
        _dupCount -= dupCount;
    }

    public void incrementDupCount(int dupCount = 1)
    {
        _dupCount += dupCount;
    }
}
