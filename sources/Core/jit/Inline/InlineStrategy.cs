// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed class InlineStrategy
{
    public const int ALWAYS_INLINE_SIZE = 16;

    public const ushort IMPLEMENTATION_MAX_INLINE_SIZE  = ushort.MaxValue;

    public const int IMPLEMENTATION_MAX_INLINE_DEPTH = 1000;

#if DEBUG
    private static bool s_HasDumpedDataHeader;
    private static bool s_HasDumpedXmlHeader;
    private static object? s_XmlWriterLock;
#endif

    private Compiler m_compiler;
    private InlineContext? m_RootContext;
    private InlinePolicy? m_LastSuccessfulPolicy;
    private InlineContext? m_LastContext;
    private InlineDecision m_PrejitRootDecision;
    private InlineObservation m_PrejitRootObservation;
    private uint m_CallCount;
    private uint m_CandidateCount;
    private uint m_AlwaysCandidateCount;
    private uint m_ForceCandidateCount;
    private uint m_DiscretionaryCandidateCount;
    private uint m_UnprofitableCandidateCount;
    private uint m_ImportCount;
    private uint m_InlineCount;
    private uint m_MaxInlineSize;
    private uint m_MaxInlineDepth;
    private uint m_MaxForceInlineDepth;
    private uint m_OverBudgetIntrinsicInlineCount;
    private int m_InitialTimeBudget;
    private int m_InitialTimeEstimate;
    private int m_CurrentTimeBudget;
    private int m_CurrentTimeEstimate;
    private int m_InitialSizeEstimate;
    private int m_CurrentSizeEstimate;
    private bool m_HasForceViaDiscretionary;

#if DEBUG
    private int m_MethodXmlFilePosition;
    private Random? m_Random;
#endif

    public InlineStrategy(Compiler compiler)
    {
        m_compiler = compiler;
        m_MaxInlineSize = DEFAULT_MAX_INLINE_SIZE;
        m_MaxInlineDepth = DEFAULT_MAX_INLINE_DEPTH;
        m_MaxForceInlineDepth = DEFAULT_MAX_FORCE_INLINE_DEPTH;

        // Verify compiler is a root compiler instance
        assert(m_compiler.impInlineRoot == m_compiler);

#if DEBUG
        // Possibly modify the max inline size.
        //
        // Default value of JitInlineSize is the same as our default.
        // So normally this next line does not change the size.
        m_MaxInlineSize = unchecked((uint)(JitConfig[ConfigInteger.JitInlineSize]));

        // Up the max size under stress
        if (m_compiler.compInlineStress())
        {
            m_MaxInlineSize *= 10;
        }

        // But don't overdo it
        if (m_MaxInlineSize > IMPLEMENTATION_MAX_INLINE_SIZE)
        {
            m_MaxInlineSize = IMPLEMENTATION_MAX_INLINE_SIZE;
        }

        // Verify: not too small, not too big.
        assert(m_MaxInlineSize >= ALWAYS_INLINE_SIZE);
        assert(m_MaxInlineSize <= IMPLEMENTATION_MAX_INLINE_SIZE);

        // Possibly modify the max inline depth
        //
        // Default value of JitInlineDepth is the same as our default.
        // So normally this next line does not change the size.
        m_MaxInlineDepth = unchecked((uint)(JitConfig[ConfigInteger.JitInlineDepth]));

        // But don't overdo it
        if (m_MaxInlineDepth > IMPLEMENTATION_MAX_INLINE_DEPTH)
        {
            m_MaxInlineDepth = IMPLEMENTATION_MAX_INLINE_DEPTH;
        }

        // Possibly modify the max force inline depth
        //
        // Default value of JitForceInlineDepth is the same as our default.
        // So normally this next line does not change the size.
        m_MaxForceInlineDepth = unchecked((uint)(JitConfig[ConfigInteger.JitForceInlineDepth]));

        // But don't overdo it
        if (m_MaxForceInlineDepth > m_MaxInlineDepth)
        {
            m_MaxForceInlineDepth = m_MaxInlineDepth;
        }
#endif
    }

    public Compiler Compiler => m_compiler;

    /// <summary>Context for the last successful inline, or root if no inlines</summary>
    public InlineContext? LastContext => m_LastContext;

    /// <summary>get the InlineContext for the root method</summary>
    /// <remarks>Also initializes the jit time estimate and budget.</remarks>
    public InlineContext RootContext
    {
        get
        {
            var rootContext = m_RootContext;

            if (rootContext is null)
            {
                // Allocate on first demand.
                rootContext = CreateRootContext();
                m_RootContext = rootContext;
            }
            return rootContext;
        }
    }

    /// <summary>Inform strategy about the inline decision for a prejit root</summary>
    public void NotePrejitDecision(InlineResult r)
    {
        m_PrejitRootDecision = r.Policy.Decision;
        m_PrejitRootObservation = r.Policy.Observation;
    }

    private InlineContext CreateRootContext()
    {
        var rootContext = NewRoot();

        // Estimate how long the jit will take if there's no inlining
        // done to this method.
        m_InitialTimeEstimate = EstimateTime(rootContext);
        m_CurrentTimeEstimate = m_InitialTimeEstimate;

        // Set the initial budget for inlining. Note this is
        // deliberately set very high and is intended to catch
        // only pathological runaway inline cases.
        var budget = JitConfig[ConfigInteger.JitInlineBudget];

        if (budget != DEFAULT_INLINE_BUDGET)
        {
            JITDUMP("Using non-default inline budget %u\n", budget);
        }

        m_InitialTimeBudget = budget * m_InitialTimeEstimate;
        m_CurrentTimeBudget = m_InitialTimeBudget;

        // Estimate the code size  if there's no inlining
        m_InitialSizeEstimate = EstimateSize(rootContext);
        m_CurrentSizeEstimate = m_InitialSizeEstimate;

        // Sanity check
        assert(m_CurrentTimeEstimate > 0);
        assert(m_CurrentSizeEstimate > 0);

        // Cache as the "last" context created
        m_LastContext = rootContext;

        return rootContext;
    }

    /// <summary>Create a context for the root method.</summary>
    /// <returns></returns>
    private InlineContext NewRoot()
    {
        // TODO: Port InlineStrategy.NewRoot
        return null!;
    }

    /// <summary>Estimate native code size change because of this inline.</summary>
    /// <param name="context"></param>
    /// <returns></returns>
    private int EstimateSize(InlineContext context)
    {
        // TODO: Port InlineStrategy.EstimateSize
        return 0;
    }

    /// <summary>Estimate the jit time change because of this inline.</summary>
    /// <param name="context"></param>
    /// <returns></returns>
    private int EstimateTime(InlineContext context)
    {
        // TODO: Port InlineStrategy.EstimateTime
        return 0;
    }
}
