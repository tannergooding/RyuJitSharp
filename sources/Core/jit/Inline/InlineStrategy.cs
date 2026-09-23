// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace RyuJitSharp;

public sealed class InlineStrategy
{
    public const int ALWAYS_INLINE_SIZE = 16;

    public const ushort IMPLEMENTATION_MAX_INLINE_SIZE  = ushort.MaxValue;

    public const int IMPLEMENTATION_MAX_INLINE_DEPTH = 1000;

    /// <summary>Maximum number of over-budget [Intrinsic]-type inlines allowed per root method.</summary>
    public const int MAX_OVER_BUDGET_INTRINSIC_INLINES = 50;

    // When the root method or an already-imported inlinee references a
    // Vector*/HW-intrinsic IsSupported / IsHardwareAccelerated property,
    // multiply the initial inline time budget by this factor (one-shot).
    // Methods with SIMD ISA fallbacks tend to be IL-heavy, and inlining one
    // such callee can otherwise consume the budget for trivial helpers
    // (e.g., Span.Slice, property getters) that follow.
    public const long SIMD_BUDGET_BOOST_MULTIPLIER = 5;

#if DEBUG
    private static bool s_HasDumpedDataHeader;
    private static bool s_HasDumpedXmlHeader;
    private static object? s_XmlWriterLock;
#endif

    private Compiler _compiler;
    private InlineContext? _rootContext;
    private InlinePolicy? _lastSuccessfulPolicy;
    private InlineContext? _lastContext;
    private InlineDecision _prejitRootDecision;
    private InlineObservation _prejitRootObservation;
    private int _callCount;
    private int _candidateCount;
    private int _alwaysCandidateCount;
    private int _forceCandidateCount;
    private int _discretionaryCandidateCount;
    private int _unprofitableCandidateCount;
    private int _importCount;
    private int _inlineCount;
    private int _maxInlineSize;
    private int _maxInlineDepth;
    private int _maxForceInlineDepth;
    private int _overBudgetIntrinsicInlineCount;
    private int _initialTimeBudget;
    private int _initialTimeEstimate;
    private int _currentTimeBudget;
    private int _currentTimeEstimate;
    private int _initialSizeEstimate;
    private int _currentSizeEstimate;
    private bool _hasForceViaDiscretionary;
    private bool _hasHardwareIntrinsicCheck;

#if DEBUG
    private int _methodXmlFilePosition;
    private CLRRandom? _random;
#endif

    public InlineStrategy(Compiler compiler)
    {
        _compiler = compiler;
        _maxInlineSize = DEFAULT_MAX_INLINE_SIZE;
        _maxInlineDepth = DEFAULT_MAX_INLINE_DEPTH;
        _maxForceInlineDepth = DEFAULT_MAX_FORCE_INLINE_DEPTH;

        // Verify compiler is a root compiler instance
        assert(_compiler.impInlineRoot == _compiler);

#if DEBUG
        // Possibly modify the max inline size.
        //
        // Default value of JitInlineSize is the same as our default.
        // So normally this next line does not change the size.
        _maxInlineSize = JitConfig.JitInlineSize;

        // Up the max size under stress
        if (_compiler.compInlineStress())
        {
            _maxInlineSize *= 10;
        }

        // But don't overdo it
        if (_maxInlineSize > IMPLEMENTATION_MAX_INLINE_SIZE)
        {
            _maxInlineSize = IMPLEMENTATION_MAX_INLINE_SIZE;
        }

        // Verify: not too small, not too big.
        assert(_maxInlineSize >= ALWAYS_INLINE_SIZE);
        assert(_maxInlineSize <= IMPLEMENTATION_MAX_INLINE_SIZE);

        // Possibly modify the max inline depth
        //
        // Default value of JitInlineDepth is the same as our default.
        // So normally this next line does not change the size.
        _maxInlineDepth = JitConfig.JitInlineDepth;

        // But don't overdo it
        if (_maxInlineDepth > IMPLEMENTATION_MAX_INLINE_DEPTH)
        {
            _maxInlineDepth = IMPLEMENTATION_MAX_INLINE_DEPTH;
        }

        // Possibly modify the max force inline depth
        //
        // Default value of JitForceInlineDepth is the same as our default.
        // So normally this next line does not change the size.
        _maxForceInlineDepth = JitConfig.JitForceInlineDepth;

        // But don't overdo it
        if (_maxForceInlineDepth > _maxInlineDepth)
        {
            _maxForceInlineDepth = _maxInlineDepth;
        }
#endif
    }

    public Compiler Compiler => _compiler;

    /// <summary>Return the current code size estimate for this method</summary>
    public int CurrentSizeEstimate => _currentSizeEstimate;

    public bool HasObservedHardwareIntrinsicCheck => _hasHardwareIntrinsicCheck;

    /// <summary>Return number of import attempts</summary>
    public int ImportCount => _importCount;

    /// <summary>Return the initial code size estimate for this method</summary>
    public int InitialSizeEstimate => _initialSizeEstimate;

    /// <summary>Number of successful inlines into the root</summary>
    public int InlineCount => _inlineCount;

    /// <summary>Context for the last successful inline, or root if no inlines</summary>
    public InlineContext? LastContext => _lastContext;

    /// <summary>Get depth of maximum allowable force inline</summary>
    public int MaxForceInlineDepth => _maxForceInlineDepth;

    /// <summary>Get IL size for maximum allowable inline</summary>
    public int MaxInlineILSize => _maxInlineSize;

    /// <summary>Get depth of maximum allowable inline</summary>
    public int MaxInlineDepth => _maxInlineDepth;

    /// <summary>Number of over-budget inlines admitted because the callee was on an [Intrinsic] type.</summary>
    public int OverBudgetIntrinsicInlineCount => _overBudgetIntrinsicInlineCount;

    /// <summary>get the InlineContext for the root method</summary>
    /// <remarks>Also initializes the jit time estimate and budget.</remarks>
    public InlineContext RootContext
    {
        get
        {
            var rootContext = _rootContext;
            rootContext ??= CreateRootContext();
            return rootContext;
        }
    }

    /// <summary>return true if an inline of this size would likely exceed the jit time budget for this method</summary>
    /// <param name="ilSize">size of the method's IL</param>
    /// <returns>true if the inline would go over budget</returns>
    /// <remarks>Presumes all IL in the method will be imported.</remarks>
    public bool BudgetCheck(int ilSize)
    {
        var timeDelta = EstimateInlineTime(ilSize);
        var result = (timeDelta + _currentTimeEstimate) > _currentTimeBudget;

        if (result)
        {
            JITDUMP($"\nBudgetCheck: for IL Size {ilSize}, timeDelta {timeDelta} +  currentEstimate {_currentTimeEstimate} > currentBudget {_currentTimeBudget}\n");
        }
        return result;
    }

#if DEBUG
    /// <summary>dump description of inline behavior</summary>
    /// <param name="verbose">print more details such as final budget values and IL offsets</param>
    public void Dump(bool verbose)
    {
        assert(_rootContext is not null);
        _rootContext.Dump(verbose);

        if (!verbose)
        {
            return;
        }

        jitprintf($"Budget: initialTime={_initialTimeEstimate}, finalTime={_currentTimeEstimate}, initialBudget={_initialTimeBudget}, currentBudget={_currentTimeBudget}\n");

        if (_currentTimeBudget > _initialTimeBudget)
        {
            jitprintf($"Budget: increased by {_currentTimeBudget - _initialTimeBudget} because of force inlines\n");
        }

        if (_currentTimeEstimate > _currentTimeBudget)
        {
            jitprintf($"Budget: went over budget by {_currentTimeEstimate - _currentTimeBudget}\n");
        }

        if (_hasForceViaDiscretionary)
        {
            jitprintf("Budget: discretionary inline caused a force inline\n");
        }

        jitprintf($"Budget: initialSize={_initialSizeEstimate}, finalSize={_currentSizeEstimate}\n");
    }
#endif

    // Dump csv header for inline stats to indicated file.
    public static void DumpCsvHeader(StreamWriter streamWriter)
    {
        streamWriter.Write("\"InlineCalls\",");
        streamWriter.Write("\"InlineCandidates\",");
        streamWriter.Write("\"InlineAlways\",");
        streamWriter.Write("\"InlineForce\",");
        streamWriter.Write("\"InlineDiscretionary\",");
        streamWriter.Write("\"InlineUnprofitable\",");
        streamWriter.Write("\"InlineEarlyFail\",");
        streamWriter.Write("\"InlineImport\",");
        streamWriter.Write("\"InlineLateFail\",");
        streamWriter.Write("\"InlineSuccess\",");
    }

    // Dump csv data for inline stats to indicated file.
    public void DumpCsvData(StreamWriter streamWriter)
    {
        streamWriter.Write($"{_callCount},");
        streamWriter.Write($"{_candidateCount},");
        streamWriter.Write($"{_alwaysCandidateCount},");
        streamWriter.Write($"{_forceCandidateCount},");
        streamWriter.Write($"{_discretionaryCandidateCount},");
        streamWriter.Write($"{_unprofitableCandidateCount},");

        // Early failures are cases where candates are rejected between
        // the time the jit invokes the inlinee compiler and the time it
        // starts to import the inlinee IL.
        //
        // So they are "cheaper" that late failures.

        var profitableCandidateCount = _discretionaryCandidateCount - _unprofitableCandidateCount;

        var earlyFailCount = (_candidateCount - _alwaysCandidateCount) - (_forceCandidateCount + profitableCandidateCount);

        streamWriter.Write($"{earlyFailCount},");

        var lateFailCount = _importCount - _inlineCount;

        streamWriter.Write($"{_importCount},");
        streamWriter.Write($"{lateFailCount},");
        streamWriter.Write($"{_inlineCount},");
    }

#if DEBUG
    public CLRRandom GetRandom(int optionalSeed = 0)
    {
        var random = _random;

        if (random is null)
        {
            random = CreateRandom(optionalSeed);
            _random = random;
        }
        return random;
    }

    private static ConfigMethodRange s_inlingDisabledRange;
#endif

    /// <summary>allow strategy to disable inlining in the method being jitted</summary>
    /// <returns></returns>
    /// <remarks>
    ///   <para>Only will return true in debug or special release builds.</para>
    ///   <para>Expects JitNoInlineRange to be set to the hashes of methods where inlining is disabled.</para>
    /// </remarks>
    public unsafe bool IsInliningDisabled()
    {
#if DEBUG
        if (!s_inlingDisabledRange.IsInit)
        {
            var pNoInlineRangeUtf8 = JitConfig.JitNoInlineRange;

            if (pNoInlineRangeUtf8 is null)
            {
                s_inlingDisabledRange.EnsureInit(null, 0);
                return false;
            }

            // If we have a config string we have at least one entry.  Count
            // number of spaces in our config string to see if there are
            // more. Number of ranges we need is 2x that value.
            var entryCount = 1;

            for (var p = pNoInlineRangeUtf8; p[0] is not (byte)('\0'); p++)
            {
                if (p[0] == (byte)(' '))
                {
                    entryCount++;
                }
            }

            s_inlingDisabledRange.EnsureInit(pNoInlineRangeUtf8, 2 * entryCount);
            assert(!s_inlingDisabledRange.Error);
        }
        return s_inlingDisabledRange.Contains(_compiler.info.compMethodHash());
#else
        return false;
#endif
    }

    // Create context for the specified inline candidate contained in the specified statement.
    public unsafe InlineContext NewContext(InlineContext parentContext, Statement stmt, GenTreeCall call)
    {
        var context = new InlineContext(this) {
            _inlineStrategy = this,
            _parent = parentContext,
            _sibling = parentContext._child
        };
        parentContext._child = context;

        // The inline context should always match the parent context we are seeing
        // here.
        assert(parentContext == call._inlineContext);

        if (call.IsInlineCandidate)
        {
            var info = call.SingleInlineCandidateInfo;
            assert(info is not null);

            context._code = info.methInfo.ILCode;
            context._ilSize = info.methInfo.ILCodeSize;
            context._actualCallOffset = info.ilOffset;
            context._runtimeContext = info.exactContextHandle;

#if DEBUG
            // All inline candidates should get their own statements that have
            // appropriate debug info (or no debug info).
            var diInlineContext = stmt.DebugInfo.InlineContext;
            assert((diInlineContext is null) || (diInlineContext == parentContext));
#endif
        }
        else
        {
#if DEBUG
            // Should only get here in debug builds
            context._actualCallOffset = call._rawILOffset;
#endif
        }

        // We currently store both the statement location (used when reporting
        // only-style mappings) and the actual call offset (used when reporting the
        // inline tree for rich debug info).
        // These are not always the same, consider e.g.
        // ldarg.0
        // call <foo>
        // which becomes a single statement where the IL location points to the
        // ldarg instruction.
        context._location = stmt.DebugInfo.Location;
        context._callee = call._callMethHnd;

#if DEBUG
        context._flags |= call.IsDevirtualized ? InlineContext.Flags.Devirtualized : InlineContext.Flags.None;
        context._flags |= call.IsGuarded ? InlineContext.Flags.Guarded : InlineContext.Flags.None;
        context._flags |= call.IsUnboxed ? InlineContext.Flags.Unboxed : InlineContext.Flags.None;
        context._flags |= call.IsAsync ? InlineContext.Flags.AsyncCall : InlineContext.Flags.None;
        context._treeId = call.TreeId;
#endif

        return context;
    }

    /// <summary>Inform strategy that a candidate has passed screening and that the jit will attempt to inline.</summary>
    public void NoteAttempt(InlineResult result)
    {
        assert(result.IsCandidate);
        var obs = result.Observation;

        if (obs == InlineObservation.CALLEE_BELOW_ALWAYS_INLINE_SIZE)
        {
            _alwaysCandidateCount++;
        }
        else if (obs == InlineObservation.CALLEE_IS_FORCE_INLINE)
        {
            _forceCandidateCount++;
        }
        else
        {
            _discretionaryCandidateCount++;
        }
    }

    /// <summary>Inform strategy that there's another call</summary>
    public void NoteCall() => _callCount++;

    /// <summary>Inform strategy that there's a new inline candidate.</summary>
    public void NoteCandidate() => _candidateCount++;

    /// <summary>record that the root method or an already-imported inlinee references a HW-intrinsic IsSupported / IsHardwareAccelerated capability check, and grow the inline time budget on the first such observation per root method.</summary>
    /// <remarks>
    ///   <para>Methods with SIMD paths typically carry several ISA-specific fallbacks (e.g. Vector512/Vector256/Vector128/scalar variants), making them IL-heavy. Inlining one such callee can otherwise consume nearly the entire inline time budget for the root method, blocking subsequent inlines of trivial helpers (Span.Slice, property getters, etc.).</para>
    ///   <para>The boost is one-shot per root method and monotonic: it never lowers the current budget (preserving any prior growth from force inlines).</para>
    /// </remarks>
    public void NoteHardwareIntrinsicCheckObserved()
    {
        if (_hasHardwareIntrinsicCheck)
        {
            return;
        }

        _hasHardwareIntrinsicCheck = true;

        // Compute the boosted budget in 64-bit to avoid signed overflow when
        // an unusually large JitInlineBudget is configured.
        var boosted64 = _initialTimeBudget * SIMD_BUDGET_BOOST_MULTIPLIER;
        var boosted = (boosted64 > int.MaxValue) ? int.MaxValue : (int)(boosted64);

        if (_currentTimeBudget < boosted)
        {
            JITDUMP($"\nBudget: HW intrinsic IsSupported/IsHardwareAccelerated check observed; boosting inline time budget from {_currentTimeBudget} to {boosted} (initial={_initialTimeBudget}, multiplier={SIMD_BUDGET_BOOST_MULTIPLIER})\n");
            _currentTimeBudget = boosted;
        }
    }

    /// <summary>Inform strategy that jit is about to import the inlinee IL.</summary>
    public void NoteImport() => _importCount++;

    /// <summary>do bookkeeping for an inline</summary>
    /// <param name="context">context for the inline</param>
    internal void NoteOutcome(InlineContext context)
    {
        // Note we can't generally count up failures here -- we only
        // create contexts for failures in debug modes, and even then
        // we may not get them all.
        if (context.IsSuccess)
        {
            _inlineCount++;

#if DEBUG
            // Keep track of the inline targeted for data collection or,
            // if we don't have one (yet), the last successful inline.
            var updateLast = (_lastSuccessfulPolicy is null) || !_lastSuccessfulPolicy.IsDataCollectionTarget;

            if (updateLast)
            {
                _lastContext = context;
                _lastSuccessfulPolicy = context._policy;
            }
            else
            {
                // We only expect one inline to be a data collection target.
                assert(context._policy is not null);
                assert(!context._policy.IsDataCollectionTarget);
            }
#endif

            // Budget update.
            //
            // If callee is a force inline, increase budget, provided all
            // parent contexts are likewise force inlines.
            //
            // If callee is discretionary or has a discretionary ancestor,
            // increase expense.

            var currentContext = context;
            var isForceInline = false;

            while (currentContext != _rootContext)
            {
                assert(currentContext is not null);
                var observation = currentContext.Observation;

                if (observation != InlineObservation.CALLEE_IS_FORCE_INLINE)
                {
                    if (isForceInline)
                    {
                        // Interesting case where discretionary inlines pull
                        // in a force inline...
                        _hasForceViaDiscretionary = true;
                    }

                    isForceInline = false;
                    break;
                }

                isForceInline = true;
                currentContext = currentContext.Parent;
            }

            var timeDelta = EstimateTime(context);

            if (isForceInline)
            {
                // Update budget since this inline was forced.
                // Only allow budget to increase.

                if (timeDelta > 0)
                {
                    _currentTimeBudget += timeDelta;
                }
            }

            // Update time estimate.
            _currentTimeEstimate += timeDelta;

            // Update size estimate.
            //
            // Sometimes estimates don't make sense. Don't let the method size go negative.
            var sizeDelta = EstimateSize(context);

            if ((_currentSizeEstimate + sizeDelta) <= 0)
            {
                sizeDelta = 0;
            }

            // Update the code size estimate.
            _currentSizeEstimate += sizeDelta;
        }
    }

    /// <summary>Note an over-budget inline that was admitted due to the callee's [Intrinsic] type.</summary>
    public void NoteOverBudgetIntrinsicInline() => _overBudgetIntrinsicInlineCount++;

    /// <summary>Inform strategy about the inline decision for a prejit root</summary>
    public void NotePrejitDecision(InlineResult r)
    {
        _prejitRootDecision = r.Policy.Decision;
        _prejitRootObservation = r.Policy.Observation;
    }

    /// <summary>Inform strategy that a candidate was assessed and determined to be unprofitable.</summary>
    public void NoteUnprofitable() => _unprofitableCandidateCount++;

#if DEBUG
    private CLRRandom CreateRandom(int optionalSeed)
    {
        var externalSeed = optionalSeed;

        if (_compiler.compRandomInlineStress())
        {
            externalSeed = JitStressLevel;

            // We can set DOTNET_JitStressModeNames without setting DOTNET_JitStress,
            // but we need external seed to be non-zero.
            if (externalSeed is 0)
            {
                externalSeed = 2;
            }
        }

        var randomPolicyFlag = JitConfig.JitInlinePolicyRandom;

        if (randomPolicyFlag is not 0)
        {
            externalSeed = randomPolicyFlag;
        }

        var internalSeed = _compiler.info.compMethodHash();

        assert(externalSeed is not 0);
        assert(internalSeed is not 0);

        var seed = externalSeed ^ internalSeed;
        JITDUMP($"\n*** Using random seed ext({externalSeed}) ^ int({internalSeed}) = {seed}\n");
        return new CLRRandom(seed);
    }
#endif

    [MemberNotNull(nameof(_rootContext), nameof(_lastContext))]
    private InlineContext CreateRootContext()
    {
        var rootContext = NewRoot();
        _rootContext = rootContext;

        // Estimate how long the jit will take if there's no inlining done to this method.
        var initialTimeEstimate = EstimateTime(rootContext);

        _initialTimeEstimate = initialTimeEstimate;
        _currentTimeEstimate = initialTimeEstimate;

        // Set the initial budget for inlining. Note this is
        // deliberately set very high and is intended to catch
        // only pathological runaway inline cases.
        var budget = JitConfig.JitInlineBudget;

        if (budget != DEFAULT_INLINE_BUDGET)
        {
            JITDUMP($"Using non-default inline budget {budget}\n");
        }

        var initialTimeBudget = budget * initialTimeEstimate;

        _initialTimeBudget = initialTimeBudget;
        _currentTimeBudget = initialTimeBudget;

        // Estimate the code size  if there's no inlining
        var initialSizeEstimate = EstimateSize(rootContext);

        _initialSizeEstimate = initialSizeEstimate;
        _currentSizeEstimate = initialSizeEstimate;

        // Sanity check
        assert(_currentTimeEstimate > 0);
        assert(_currentSizeEstimate > 0);

        // Cache as the "last" context created
        _lastContext = rootContext;

        return rootContext;
    }

    /// <summary>construct an InlineContext for the root method.</summary>
    /// <returns>InlineContext for use as the root context</returns>
    /// <remarks>We leave <see cref="InlineContext._code" /> as <c>null</c> here (rather than the IL buffer address of the root method) to preserve existing behavior, which is to allow one recursive inline.</remarks>
    private unsafe InlineContext NewRoot()
    {
        var rootContext = new InlineContext(this) {
            _ilSize = _compiler.info.compILCodeSize,
            _code = _compiler.info.compCode,
            _callee = _compiler.info.compMethodHnd,

            // May fail to block recursion for normal methods
            // Might need the actual context handle here
            _runtimeContext = METHOD_BEING_COMPILED_CONTEXT(),
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
    private int EstimateInlineTime(int ilSize) => -14 + (2 * ilSize);

    /// <summary>estimate jit time for method of this size with no inlining.</summary>
    /// <param name="ilSize">size of the method's IL</param>
    /// <returns>Nominal estimate of jit time.</returns>
    /// <remarks>Based on observational data. Time is nominally microseconds.</remarks>
    private int EstimateRootTime(int ilSize) => 60 + (3 * ilSize);

    /// <summary>estimate impact of this inline on the method size</summary>
    /// <param name="context">context describing this inline</param>
    /// <returns>Nominal estimate of method size (bytes * 10)</returns>
    private int EstimateSize(InlineContext context)
    {
        // Prediction varies for root and inlines.
        if (context == _rootContext)
        {
            // Simple linear models based on observations show root method
            // native code size is fairly well predicted by IL size.
            //
            // Model below is for x64 on windows.
            var ilSize = context.ILSize;
            var estimate = (1312 + 228 * ilSize) / 10;
            return estimate;
        }
        else
        {
            // Use context's code size estimate.
            return context.CodeSizeEstimate;
        }
    }

    /// <summary>estimate impact of this inline on the method jit time</summary>
    /// <param name="context">context describing this inline</param>
    /// <returns>Nominal estimate of jit time.</returns>
    private int EstimateTime(InlineContext context)
    {
        // Simple linear models based on observations show time is fairly well predicted by IL size.
        // Prediction varies for root and inlines.

        if (context == _rootContext)
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
