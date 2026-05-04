// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics.CodeAnalysis;

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
            rootContext ??= CreateRootContext();
            return rootContext;
        }
    }

    /// <summary>Inform strategy about the inline decision for a prejit root</summary>
    public void NotePrejitDecision(InlineResult r)
    {
        m_PrejitRootDecision = r.Policy.Decision;
        m_PrejitRootObservation = r.Policy.Observation;
    }

    [MemberNotNull(nameof(m_RootContext), nameof(m_LastContext))]
    private InlineContext CreateRootContext()
    {
        var rootContext = NewRoot();
        m_RootContext = rootContext;

        // Estimate how long the jit will take if there's no inlining done to this method.
        var initialTimeEstimate = EstimateTime(rootContext);

        m_InitialTimeEstimate = initialTimeEstimate;
        m_CurrentTimeEstimate = initialTimeEstimate;

        // Set the initial budget for inlining. Note this is
        // deliberately set very high and is intended to catch
        // only pathological runaway inline cases.
        var budget = JitConfig[ConfigInteger.JitInlineBudget];

        if (budget != DEFAULT_INLINE_BUDGET)
        {
            JITDUMP("Using non-default inline budget %u\n", budget);
        }

        var initialTimeBudget = budget * initialTimeEstimate;

        m_InitialTimeBudget = initialTimeBudget;
        m_CurrentTimeBudget = initialTimeBudget;

        // Estimate the code size  if there's no inlining
        var initialSizeEstimate = EstimateSize(rootContext);

        m_InitialSizeEstimate = initialSizeEstimate;
        m_CurrentSizeEstimate = initialSizeEstimate;

        // Sanity check
        assert(m_CurrentTimeEstimate > 0);
        assert(m_CurrentSizeEstimate > 0);

        // Cache as the "last" context created
        m_LastContext = rootContext;

        return rootContext;
    }

    /// <summary>construct an InlineContext for the root method.</summary>
    /// <returns>InlineContext for use as the root context</returns>
    /// <remarks>We leave <see cref="InlineContext.m_Code" /> as <c>null</c> here (rather than the IL buffer address of the root method) to preserve existing behavior, which is to allow one recursive inline.</remarks>
    private unsafe InlineContext NewRoot()
    {
        var rootContext = new InlineContext(this) {
            m_ILSize = m_compiler.info.compILCodeSize,
            m_Code = m_compiler.info.compCode,
            m_Callee = m_compiler.info.compMethodHnd,

            // May fail to block recursion for normal methods
            // Might need the actual context handle here
            m_RuntimeContext = METHOD_BEING_COMPILED_CONTEXT(),
        };
        return rootContext;
    }

    /// <summary>estimate time impact on jitting for an inline of this size.</summary>
    /// <param name="ilSize">size of the method's IL</param>
    /// <returns>Nominal increase in jit time.</returns>
    /// <remarks>
    ///   <para>Based on observational data. Time is nominally microseconds.</para>
    ///   <para>Small inlines will make the jit a bit faster.</para>
    /// </remarks>
    private int EstimateInlineTime(uint ilSize) => -14 + unchecked((int)(2 * ilSize));

    /// <summary>estimate jit time for method of this size with no inlining.</summary>
    /// <param name="ilSize">size of the method's IL</param>
    /// <returns>Nominal estimate of jit time.</returns>
    /// <remarks>Based on observational data. Time is nominally microseconds.</remarks>
    private int EstimateRootTime(uint ilSize) => 60 + unchecked((int)(3 * ilSize));

    /// <summary>estimate impact of this inline on the method size</summary>
    /// <param name="context">context describing this inline</param>
    /// <returns>Nominal estimate of method size (bytes * 10)</returns>
    private int EstimateSize(InlineContext context)
    {
        // Prediction varies for root and inlines.
        if (context == m_RootContext)
        {
            // Simple linear models based on observations show root method
            // native code size is fairly well predicted by IL size.
            //
            // Model below is for x64 on windows.
            var ilSize = context.ILSize;
            var estimate = (1312 + 228 * ilSize) / 10;
            return unchecked((int)(estimate));
        }
        else
        {
            // Use context's code size estimate.
            return unchecked((int)(context.CodeSizeEstimate));
        }
    }

    /// <summary>estimate impact of this inline on the method jit time</summary>
    /// <param name="context">context describing this inline</param>
    /// <returns>Nominal estimate of jit time.</returns>
    private int EstimateTime(InlineContext context)
    {
        // Simple linear models based on observations show time is fairly well predicted by IL size.
        // Prediction varies for root and inlines.

        if (context == m_RootContext)
        {
            return EstimateRootTime(context.ILSize);
        }
        else
        {
            // Use amount of IL actually imported
            return EstimateInlineTime(context.ImportedILSize);
        }
    }
}
