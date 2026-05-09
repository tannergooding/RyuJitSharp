// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;

namespace RyuJitSharp;

public sealed class FlowEdge
{
    // The next predecessor edge in the list, null for end of list.
    private FlowEdge? m_nextPredEdge;

    // The source of the control flow
    private BasicBlock m_sourceBlock;

    // The destination of the control flow
    private BasicBlock m_destBlock;

    // Likelihood that m_sourceBlock transfers control along this edge.
    // Values in range [0..1]
    private weight_t m_likelihood;

    // The count of duplicate "edges" (used for switch stmts or degenerate branches)
    private int m_dupCount;

    // Convenience flag for phases that need to track edge visitation
    private bool m_visited;

    // Indicates if m_likelihood was determined using profile synthesis's heuristics
    private bool m_heuristicBasedLikelihood;

    // True if likelihood has been set
#if DEBUG
    private bool m_likelihoodSet;
#endif

    public FlowEdge(BasicBlock sourceBlock, BasicBlock destBlock, FlowEdge? rest)
    {
        m_nextPredEdge = rest;
        m_sourceBlock = sourceBlock;
        m_destBlock = destBlock;
    }

    public BasicBlock DestinationBlock
    {
        get
        {
            return m_destBlock;
        }

        set
        {
            m_destBlock = value;
        }
    }

    public int DupCount => m_dupCount;

#if DEBUG
    public bool hasLikelihood => m_likelihoodSet;
#endif

    public bool isHeuristicBased
    {
        get
        {
            return m_heuristicBasedLikelihood;
        }

        set
        {
            m_heuristicBasedLikelihood = value;
        }
    }

    public weight_t Likelihood
    {
        get
        {
#if DEBUG
            assert(Debugger.IsAttached || m_likelihoodSet);
#endif
            return m_likelihood;
        }

        set
        {
            assert(value >= 0.0);
            assert(value <= 1.0);

#if DEBUG
            if (m_likelihoodSet)
            {
                JITDUMP($"setting likelihood of {FMT_BB(m_sourceBlock.bbNum)} -> {FMT_BB(m_destBlock.bbNum)} from {FMT_WT(m_likelihood)} to {FMT_WT(value)}\n");
            }
            else
            {
                JITDUMP($"setting likelihood of {FMT_BB(m_sourceBlock.bbNum)} -> {FMT_BB(m_destBlock.bbNum)} to {FMT_WT(value)}\n");
            }

            m_likelihoodSet = true;
#endif

            m_likelihood = value;
        }
    }

    public FlowEdge? NextPredEdge
    {
        get
        {
            return m_nextPredEdge;
        }

        set
        {
            m_nextPredEdge = value;
        }
    }

#nullable disable
    public ref FlowEdge NextPredEdgeRef => ref m_nextPredEdge;
#nullable restore

    public BasicBlock SourceBlock
    {
        get
        {
            return m_sourceBlock;
        }

        set
        {
            m_sourceBlock = value;
        }
    }

    //------------------------------------------------------------------------
    // addLikelihood: 
    //
    // Arguments:
    //   addedLikelihood -- 
    //
    /// <summary>adjust the likelihood of a flow edge </summary>
    /// <param name="addedLikelihood">value in range [-likelihood, 1.0 - likelihood] to add to current likelihood.</param>
    public void AddLikelihood(weight_t addedLikelihood)
    {
#if DEBUG
        assert(m_likelihoodSet);
#endif

        var newLikelihood = m_likelihood + addedLikelihood;

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

        JITDUMP($"updating likelihood of {FMT_BB(m_sourceBlock.bbNum)} -> {FMT_BB(m_destBlock.bbNum)} from {FMT_WT(m_likelihood)} to {FMT_WT(newLikelihood)}\n");
        m_likelihood = newLikelihood;
    }

    public void decrementDupCount(int dupCount = 1)
    {
        m_dupCount -= dupCount;
    }

    public void incrementDupCount(int dupCount = 1)
    {
        assert(m_dupCount >= dupCount);
        m_dupCount += dupCount;
    }
}
