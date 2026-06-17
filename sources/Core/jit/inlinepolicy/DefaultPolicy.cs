// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.IO;

namespace RyuJitSharp;

public partial class DefaultPolicy : LegacyPolicy
{
    private const int MAX_BASIC_BLOCKS = 5;
    private const int SIZE_SCALE = 10;

    protected Compiler _rootCompiler;
    protected CodeSeqSM? _stateMachine;
    protected double _multiplier;
    protected int _codeSize;
    protected InlineCallsiteFrequency _callsiteFrequency;
    protected int _callsiteDepth;
    protected int _instructionCount;
    protected int _loadStoreCount;
    protected int _argFeedsTest;
    protected int _argFeedsConstantTest;
    protected int _argFeedsRangeCheck;
    protected int _constantArgFeedsConstantTest;
    protected int _calleeNativeSizeEstimate;
    protected int _callsiteNativeSizeEstimate;
    private Flags _flags;

    public DefaultPolicy(Compiler compiler, bool isPrejitRoot)
        : base(isPrejitRoot)
    {
        _rootCompiler = compiler;
    }

    public virtual int EstimatedTotalILSize => _codeSize;

#if DEBUG
    public override string Name => nameof(DefaultPolicy);
#endif

    protected bool ArgFeedsIsKnownConst
    {
        get
        {
            return (_flags & Flags.ArgFeedsIsKnownConst) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.ArgFeedsIsKnownConst) | (value ? Flags.ArgFeedsIsKnownConst : Flags.None);
        }
    }

    protected bool CallsiteIsInLoop
    {
        get
        {
            return (_flags & Flags.CallsiteIsInLoop) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.CallsiteIsInLoop) | (value ? Flags.CallsiteIsInLoop : Flags.None);
        }
    }

    protected bool CallsiteIsInTryRegion
    {
        get
        {
            return (_flags & Flags.CallsiteIsInTryRegion) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.CallsiteIsInTryRegion) | (value ? Flags.CallsiteIsInTryRegion : Flags.None);
        }
    }

    protected bool ConstArgFeedsIsKnownConst
    {
        get
        {
            return (_flags & Flags.ConstArgFeedsIsKnownConst) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.ConstArgFeedsIsKnownConst) | (value ? Flags.ConstArgFeedsIsKnownConst : Flags.None);
        }
    }

    protected bool HasSimd
    {
        get
        {
            return (_flags & Flags.HasSimd) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.HasSimd) | (value ? Flags.HasSimd : Flags.None);
        }
    }

    protected bool InsideThrowBlock
    {
        get
        {
            return (_flags & Flags.InsideThrowBlock) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.InsideThrowBlock) | (value ? Flags.InsideThrowBlock : Flags.None);
        }
    }

    protected bool IsForceInline
    {
        get
        {
            return (_flags & Flags.IsForceInline) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.IsForceInline) | (value ? Flags.IsForceInline : Flags.None);
        }
    }

    protected bool IsForceInlineKnown
    {
        get
        {
            return (_flags & Flags.IsForceInlineKnown) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.IsForceInlineKnown) | (value ? Flags.IsForceInlineKnown : Flags.None);
        }
    }

    protected bool IsFromPromotableValueClass
    {
        get
        {
            return (_flags & Flags.IsFromPromotableValueClass) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.IsFromPromotableValueClass) | (value ? Flags.IsFromPromotableValueClass : Flags.None);
        }
    }

    protected bool IsInstanceCtor
    {
        get
        {
            return (_flags & Flags.IsInstanceCtor) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.IsInstanceCtor) | (value ? Flags.IsInstanceCtor : Flags.None);
        }
    }

    protected bool IsIntrinsicType
    {
        get
        {
            return (_flags & Flags.IsIntrinsicType) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.IsIntrinsicType) | (value ? Flags.IsIntrinsicType : Flags.None);
        }
    }

    protected bool IsNoReturn
    {
        get
        {
            return (_flags & Flags.IsNoReturn) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.IsNoReturn) | (value ? Flags.IsNoReturn : Flags.None);
        }
    }

    protected bool IsNoReturnKnown
    {
        get
        {
            return (_flags & Flags.IsNoReturnKnown) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.IsNoReturnKnown) | (value ? Flags.IsNoReturnKnown : Flags.None);
        }
    }

    protected bool LooksLikeWrapperMethod
    {
        get
        {
            return (_flags & Flags.LooksLikeWrapperMethod) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.LooksLikeWrapperMethod) | (value ? Flags.LooksLikeWrapperMethod : Flags.None);
        }
    }

    protected bool MethodIsMostlyLoadStore
    {
        get
        {
            return (_flags & Flags.MethodIsMostlyLoadStore) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.MethodIsMostlyLoadStore) | (value ? Flags.MethodIsMostlyLoadStore : Flags.None);
        }
    }

    public override bool BudgetCheck()
    {
        if (_isPrejitRoot)
        {
            // Only relevant if we're actually inlining.
            return false;
        }

        // The strategy tracks the amout of inlining done so far, so it performs the actual check.
        var strategy = _rootCompiler._inlineStrategy;

        assert(strategy is not null);
        var overBudget = strategy.BudgetCheck(EstimatedTotalILSize);

        if (overBudget)
        {
            // If the candidate is a forceinline and the callsite is
            // not too deep, allow the inline even if it goes over budget.
            //
            // For now, "not too deep" means a top-level inline. Note
            // depth 0 is used for the root method, so inline candidate depth
            // will be 1 or more.

            assert(IsForceInlineKnown);
            assert(_callsiteDepth > 0);

            var allowOverBudget = IsForceInline && (_callsiteDepth <= strategy.MaxForceInlineDepth);
            var skipBudgetChecksSize = 12;

            if (!allowOverBudget && (_codeSize <= skipBudgetChecksSize))
            {
                // We don't want to give up on various getters/setters if we're running out of budget
                JITDUMP("Allowing over-budget for small methods\n");
                allowOverBudget = true;
            }

            if (!allowOverBudget && IsIntrinsicType && (strategy.OverBudgetIntrinsicInlineCount < InlineStrategy.MAX_OVER_BUDGET_INTRINSIC_INLINES))
            {
                // Callees from [Intrinsic]-marked types (e.g. Span<T>, Vector<T>, hardware intrinsic
                // ISA classes) need to be inlined for codegen quality even when we're out of budget.
                // Cap the number of such admissions per root method to keep JIT throughput bounded.
                JITDUMP($"Allowing over-budget for intrinsic types (count: {strategy.OverBudgetIntrinsicInlineCount})\n");
                strategy.NoteOverBudgetIntrinsicInline();
                allowOverBudget = true;
            }

            if (!allowOverBudget && IsNoReturnKnown && IsNoReturn)
            {
                // We're not going to inline no-return calls anyway
                JITDUMP("Allowing over-budget for known no-returns\n");
                allowOverBudget = true;
            }

            if (allowOverBudget)
            {
                JITDUMP("Allowing over-budget: top-level forceinline, no return call, or small inlinee\n");
            }
            else
            {
                return true;
            }
        }
        return false;
    }

    public override int CodeSizeEstimate()
    {
        if (_stateMachine is not null)
        {
            // This is not something the DefaultPolicy explicitly computed,
            // since it uses a blended evaluation model (mixing size and time
            // together for overall profitability). But it's effectively an
            // estimate of the size impact.
            return _calleeNativeSizeEstimate - _callsiteNativeSizeEstimate;
        }
        else
        {
            return 0;
        }
    }

    public override void DetermineProfitability(in CORINFO_METHOD_INFO methodInfo)
    {
        assert(_decision.IsCandidate);
        assert(_observation is InlineObservation.CALLEE_IS_DISCRETIONARY_INLINE);

        _calleeNativeSizeEstimate = DetermineNativeSizeEstimate();
        _callsiteNativeSizeEstimate = DetermineCallsiteNativeSizeEstimate(methodInfo);
        _multiplier = DetermineMultiplier();

        var threshold = (int)(_callsiteNativeSizeEstimate * _multiplier);

        // Note the DefaultPolicy estimates are scaled up by SIZE_SCALE
        JITDUMP($"\ncalleeNativeSizeEstimate={_calleeNativeSizeEstimate}\n");
        JITDUMP($"callsiteNativeSizeEstimate={_callsiteNativeSizeEstimate}\n");
        JITDUMP($"benefit multiplier={_multiplier}\n");
        JITDUMP($"threshold={threshold}\n");

        // Reject if callee size is over the threshold
        if (_calleeNativeSizeEstimate > threshold)
        {
            // Inline appears to be unprofitable
            _rootCompiler.JITLOG(LL_INFO100000, $"Native estimate for function size exceeds threshold for inlining {(double)(_calleeNativeSizeEstimate) / SIZE_SCALE} > {(double)(threshold) / SIZE_SCALE} (multiplier = {_multiplier})\n");

            // Fail the inline
            if (_isPrejitRoot)
            {
                SetNever(InlineObservation.CALLEE_NOT_PROFITABLE_INLINE);
            }
            else
            {
                SetFailure(InlineObservation.CALLSITE_NOT_PROFITABLE_INLINE);
            }
        }
        else
        {
            // Inline appears to be profitable
            _rootCompiler.JITLOG(LL_INFO100000, $"Native estimate for function size is within threshold for inlining {(double)(_calleeNativeSizeEstimate) / SIZE_SCALE} <= {(double)(threshold) / SIZE_SCALE} (multiplier = {_multiplier})\n");

            // Update candidacy
            if (_isPrejitRoot)
            {
                SetCandidate(InlineObservation.CALLEE_IS_PROFITABLE_INLINE);
            }
            else
            {
                SetCandidate(InlineObservation.CALLSITE_IS_PROFITABLE_INLINE);
            }
        }
    }

    public override void NoteBool(InlineObservation observation, bool value)
    {
        // Check the impact
        var impact = observation.Impact;

        // As a safeguard, all fatal impact must be
        // reported via NoteFatal.
        assert(impact is not InlineImpact.FATAL);

        // Handle most information here
        var isInformation = impact is InlineImpact.INFORMATION;
        var propagate = !isInformation;

        if (isInformation)
        {
            switch (observation)
            {
                case InlineObservation.CALLEE_IS_FORCE_INLINE:
                {
                    // We may make the force-inline observation more than
                    // once.  All observations should agree.
                    assert(!IsForceInlineKnown || (IsForceInline == value));
                    IsForceInline = value;
                    IsForceInlineKnown = true;
                    break;
                }

                case InlineObservation.CALLEE_IS_INTRINSIC_TYPE:
                {
                    IsIntrinsicType = value;
                    break;
                }

                case InlineObservation.CALLEE_IS_INSTANCE_CTOR:
                {
                    IsInstanceCtor = value;
                    break;
                }

                case InlineObservation.CALLEE_CLASS_PROMOTABLE:
                {
                    IsFromPromotableValueClass = value;
                    break;
                }

                case InlineObservation.CALLSITE_IN_TRY_REGION:
                {
                    CallsiteIsInTryRegion = value;
                    break;
                }

                case InlineObservation.CALLEE_HAS_SIMD:
                {
                    HasSimd = value;
                    break;
                }

                case InlineObservation.CALLEE_LOOKS_LIKE_WRAPPER:
                {
                    LooksLikeWrapperMethod = value;
                    break;
                }

                case InlineObservation.CALLEE_ARG_FEEDS_TEST:
                {
                    _argFeedsTest++;
                    break;
                }

                case InlineObservation.CALLEE_ARG_FEEDS_CONSTANT_TEST:
                {
                    _argFeedsConstantTest++;
                    break;
                }

                case InlineObservation.CALLEE_ARG_FEEDS_RANGE_CHECK:
                {
                    _argFeedsRangeCheck++;
                    break;
                }

                case InlineObservation.CALLEE_CONST_ARG_FEEDS_ISCONST:
                {
                    ConstArgFeedsIsKnownConst = true;
                    break;
                }

                case InlineObservation.CALLEE_ARG_FEEDS_ISCONST:
                {
                    ArgFeedsIsKnownConst = true;
                    break;
                }

                case InlineObservation.CALLEE_UNSUPPORTED_OPCODE:
                {
                    propagate = true;
                    break;
                }

                case InlineObservation.CALLSITE_CONSTANT_ARG_FEEDS_TEST:
                {
                    // We shouldn't see this for a prejit root since
                    // we don't know anything about callers.
                    assert(!_isPrejitRoot);
                    _constantArgFeedsConstantTest++;
                    break;
                }

                case InlineObservation.CALLEE_BEGIN_OPCODE_SCAN:
                {
                    // Set up the state machine, if this inline is
                    // discretionary and is still a candidate.
                    if (_decision.IsCandidate && (_observation is InlineObservation.CALLEE_IS_DISCRETIONARY_INLINE))
                    {
                        // Better not have a state machine already.
                        assert(_stateMachine is null);
                        _stateMachine = new CodeSeqSM();
                        _stateMachine.Start(_rootCompiler);
                    }
                    break;
                }

                case InlineObservation.CALLEE_END_OPCODE_SCAN:
                {
                    _stateMachine?.End();

                    // If this function is mostly loads and stores, we
                    // should try harder to inline it.  You can't just use
                    // the percentage test because if the method has 8
                    // instructions and 6 are loads, it's only 75% loads.
                    // This allows for CALL, RET, and one more non-ld/st
                    // instruction.

                    if (((_instructionCount - _loadStoreCount) < 4) || (((double)_loadStoreCount / (double)_instructionCount) > .90))
                    {
                        MethodIsMostlyLoadStore = true;
                    }

                    // Budget check.
                    //
                    // Conceptually this should happen when we
                    // observe the candidate's IL size.
                    //
                    // However, we do this here to avoid potential
                    // inconsistency between the state of the budget
                    // during candidate scan and the state when the IL is
                    // being scanned.
                    //
                    // Consider the case where we're just below the budget
                    // during candidate scan, and we have three possible
                    // inlines, any two of which put us over budget. We
                    // allow them all to become candidates. We then move
                    // on to inlining and the first two get inlined and
                    // put us over budget. Now the third can't be inlined
                    // anymore, but we have a policy that when we replay
                    // the candidate IL size during the inlining pass it
                    // "reestablishes" candidacy rather than alters
                    // candidacy ... so instead we bail out here.

                    var overBudget = BudgetCheck();

                    if (overBudget)
                    {
                        SetFailure(InlineObservation.CALLSITE_OVER_BUDGET);
                        return;
                    }
                    break;
                }

                case InlineObservation.CALLSITE_IN_LOOP:
                {
                    CallsiteIsInLoop = true;
                    break;
                }

                case InlineObservation.CALLEE_DOES_NOT_RETURN:
                {
                    IsNoReturn = value;
                    IsNoReturnKnown = true;
                    break;
                }

                case InlineObservation.CALLSITE_RARE_GC_STRUCT:
                {
                    // If this is a discretionary or always inline candidate
                    // with a gc struct, we may change our mind about inlining
                    // if the call site is rare, to avoid costs associated with
                    // zeroing the GC struct up in the root prolog.
                    if (_observation is InlineObservation.CALLEE_BELOW_ALWAYS_INLINE_SIZE)
                    {
                        assert(_callsiteFrequency is InlineCallsiteFrequency.UNUSED);
                        SetFailure(observation);
                        return;
                    }
                    else if (_observation is InlineObservation.CALLEE_IS_DISCRETIONARY_INLINE)
                    {
                        assert(_callsiteFrequency is InlineCallsiteFrequency.RARE);
                        SetFailure(observation);
                        return;
                    }
                    break;
                }

                case InlineObservation.CALLEE_HAS_PINNED_LOCALS:
                {
                    if (CallsiteIsInTryRegion)
                    {
                        // Inlining a method with pinned locals in a try
                        // region requires wrapping the inline body in a
                        // try/finally to ensure unpinning. Bail instead.
                        SetFailure(InlineObservation.CALLSITE_PIN_IN_TRY_REGION);
                        return;
                    }
                    break;
                }

                case InlineObservation.CALLEE_HAS_LOCALLOC:
                {
                    // We see this during the IL prescan. Ignore for now, we will
                    // bail out, if necessary, during importation
                    break;
                }

                case InlineObservation.CALLSITE_INSIDE_THROW_BLOCK:
                {
                    InsideThrowBlock = value;
                    break;
                }

                default:
                {
                    // Ignore the remainder for now
                    break;
                }
            }
        }

        if (propagate)
        {
            NoteInternal(observation);
        }
    }

    public override void NoteDouble(InlineObservation observation, double value)
    {
        assert(observation is InlineObservation.CALLSITE_PROFILE_FREQUENCY);
    }

    public override void NoteInt(InlineObservation observation, int value)
    {
        switch (observation)
        {
            case InlineObservation.CALLEE_MAXSTACK:
            {
                assert(IsForceInlineKnown);
                assert(value >= 0);

                var calleeMaxStack = value;

                if (!IsForceInline && (calleeMaxStack > SMALL_STACK_SIZE))
                {
                    SetNever(InlineObservation.CALLEE_MAXSTACK_TOO_BIG);
                }
                break;
            }

            case InlineObservation.CALLEE_NUMBER_OF_BASIC_BLOCKS:
            {
                assert(IsForceInlineKnown);
                assert(value > 0);
                assert(IsNoReturnKnown);

                // Let's be conservative for now and reject inlining of "no return" methods only
                // if the callee contains a single basic block. This covers most of the use cases
                // (typical throw helpers simply do "throw new X();" and so they have a single block)
                // without affecting more exotic cases (loops that do actual work for example) where
                // failure to inline could negatively impact code quality.

                var basicBlockCount = value;

                // CALLEE_IS_FORCE_INLINE overrides CALLEE_DOES_NOT_RETURN
                if (!IsForceInline)
                {
                    if (IsNoReturn && (basicBlockCount == 1))
                    {
                        SetNever(InlineObservation.CALLEE_DOES_NOT_RETURN);
                    }
                    else if (basicBlockCount > MAX_BASIC_BLOCKS)
                    {
                        SetNever(InlineObservation.CALLEE_TOO_MANY_BASIC_BLOCKS);
                    }
                }
                break;
            }

            case InlineObservation.CALLEE_IL_CODE_SIZE:
            {
                assert(IsForceInlineKnown);
                assert(value > 0);

                _codeSize = value;

                var inlineStrategy = _rootCompiler._inlineStrategy;
                assert(inlineStrategy is not null);

                var alwaysInlineSize = InlineStrategy.ALWAYS_INLINE_SIZE;
                var maxCodeSize = inlineStrategy.MaxInlineILSize;

                if (InsideThrowBlock)
                {
                    // Inline only small code in BBJ_THROW blocks, e.g. <= 8 bytes of IL
                    alwaysInlineSize /= 2;
                    maxCodeSize = int.Min(alwaysInlineSize + 1, maxCodeSize);
                }

                // Now that we know size and forceinline state,
                // update candidacy.
                if (IsForceInline)
                {
                    // Candidate based on force inline
                    SetCandidate(InlineObservation.CALLEE_IS_FORCE_INLINE);
                }
                else if (_codeSize <= alwaysInlineSize)
                {
                    // Candidate based on small size
                    SetCandidate(InlineObservation.CALLEE_BELOW_ALWAYS_INLINE_SIZE);
                }
                else if (_codeSize <= maxCodeSize)
                {
                    // Candidate, pending profitability evaluation
                    SetCandidate(InlineObservation.CALLEE_IS_DISCRETIONARY_INLINE);
                }
                else
                {
                    // Callee too big, not a candidate
                    SetNever(InlineObservation.CALLEE_TOO_MUCH_IL);
                }
                break;
            }

            case InlineObservation.CALLSITE_DEPTH:
            {
                assert(value >= 0);
                _callsiteDepth = value;

                var inlineStrategy = _rootCompiler._inlineStrategy;
                assert(inlineStrategy is not null);

                if (_callsiteDepth > inlineStrategy.MaxInlineDepth)
                {
                    SetFailure(InlineObservation.CALLSITE_IS_TOO_DEEP);
                }
                break;
            }

            case InlineObservation.CALLEE_OPCODE_NORMED:
            case InlineObservation.CALLEE_OPCODE:
            {
                _instructionCount++;
                var opcode = (OPCODE)(value);

                if (_stateMachine != null)
                {
                    var smOpcode = CodeSeqSM.MapToSMOpcode(opcode);

                    noway_assert(smOpcode < SM_COUNT);
                    noway_assert(smOpcode != SM_PREFIX_N);

                    if (observation == InlineObservation.CALLEE_OPCODE_NORMED)
                    {
                        if (smOpcode == SM_LDARGA_S)
                        {
                            smOpcode = SM_LDARGA_S_NORMED;
                        }
                        else if (smOpcode == SM_LDLOCA_S)
                        {
                            smOpcode = SM_LDLOCA_S_NORMED;
                        }
                    }

                    _stateMachine.Run(smOpcode, 0);
                }

                // Look for opcodes that imply loads and stores.
                // Logic here is as it is to match legacy behavior.
                if (opcode is (>= CEE_LDARG_0 and <= CEE_STLOC_S)
                           or (>= CEE_LDARG and CEE_STLOC)
                           or (>= CEE_LDNULL and <= CEE_LDC_R8)
                           or (>= CEE_LDIND_I1 and <= CEE_STIND_R8)
                           or (>= CEE_LDFLD and <= CEE_STOBJ)
                           or (>= CEE_LDELEMA and <= CEE_STELEM)
                           or CEE_POP)
                {
                    _loadStoreCount++;
                }

                break;
            }

            case InlineObservation.CALLSITE_FREQUENCY:
            {
                assert(_callsiteFrequency is InlineCallsiteFrequency.UNUSED);
                _callsiteFrequency = (InlineCallsiteFrequency)(value);
                assert(_callsiteFrequency is not InlineCallsiteFrequency.UNUSED);
                break;
            }

            default:
            {
                // Ignore all other information
                break;
            }
        }
    }

    public override void NoteSuccess()
    {
        assert(_decision.IsCandidate);
        _decision = InlineDecision.SUCCESS;
    }

#if DEBUG
    public override void OnDumpXml(StreamWriter stream, int indent = 0)
    {
        XATTR_R8(stream, _multiplier);
        XATTR_I4(stream, _codeSize);
        XATTR_I4(stream, (int)(_callsiteFrequency));
        XATTR_I4(stream, _callsiteDepth);
        XATTR_I4(stream, _instructionCount);
        XATTR_I4(stream, _loadStoreCount);
        XATTR_I4(stream, _argFeedsTest);
        XATTR_I4(stream, _argFeedsConstantTest);
        XATTR_I4(stream, _argFeedsRangeCheck);
        XATTR_I4(stream, _constantArgFeedsConstantTest);
        XATTR_I4(stream, _calleeNativeSizeEstimate);
        XATTR_I4(stream, _callsiteNativeSizeEstimate);
        XATTR_B(stream, IsForceInline);
        XATTR_B(stream, IsForceInlineKnown);
        XATTR_B(stream, IsInstanceCtor);
        XATTR_B(stream, IsFromPromotableValueClass);
        XATTR_B(stream, HasSimd);
        XATTR_B(stream, LooksLikeWrapperMethod);
        XATTR_B(stream, MethodIsMostlyLoadStore);
        XATTR_B(stream, CallsiteIsInTryRegion);
        XATTR_B(stream, CallsiteIsInLoop);
        XATTR_B(stream, IsNoReturn);
        XATTR_B(stream, IsNoReturnKnown);
        XATTR_B(stream, InsideThrowBlock);
        XATTR_B(stream, IsIntrinsicType);
    }
#endif

    public override bool PropagateNeverToRuntime()
    {
        var observation = _observation;

        if (observation == InlineObservation.CALLEE_DOES_NOT_RETURN)
        {
            // Do not propagate the "no return" observation. If we do this then future inlining
            // attempts will fail immediately without marking the call node as "no return".
            // This can have an adverse impact on caller's code quality as it may have to preserve
            // registers across the call.
            // TODO-Throughput: We should persist the "no return" information in the runtime
            // so we don't need to re-analyze the inlinee all the time.
            //
            return false;
        }

        var target = observation.Target;
        var impact = observation.Impact;

        if ((target is InlineTarget.CALLEE) && (impact is InlineImpact.FATAL))
        {
            // This callee will never inline.
            return true;
        }

        if (InsideThrowBlock)
        {
            // We inline only trivial methods inside BBJ_THROW call-sites - no need to record that.
            return false;
        }

        if (_rootCompiler.fgPgoDynamic)
        {
            // If dynamic pgo is active, only propagate noinline back to metadata
            // when there is a CALLEE FATAL observation. We want to make sure
            // not to block future inlines based on performance or throughput considerations.
            //
            // Note fgPgoDynamic (and hence dynamicPgo) is true iff TieredPGO is enabled globally.
            // In particular this value does not depend on the root method having PGO data.
            return false;
        }
        return true;
    }

    /// <summary>determine benefit multiplier for this inline</summary>
    /// <returns></returns>
    /// <remarks>uses the accumulated set of observations to compute a profitability boost for the inline candidate.</remarks>
    protected virtual double DetermineMultiplier()
    {
        var multiplier = 0.0;

        // Bump up the multiplier for instance constructors

        if (IsInstanceCtor)
        {
            multiplier += 1.5;
            JITDUMP($"\nmultiplier in instance constructors increased to {multiplier}.");
        }

        // Bump up the multiplier for methods in promotable struct

        if (IsFromPromotableValueClass)
        {
            multiplier += 3;
            JITDUMP($"\nmultiplier in methods of promotable struct increased to {multiplier}.");
        }

#if FEATURE_SIMD
        if (HasSimd)
        {
            multiplier += JitConfig.JitInlineSIMDMultiplier;
            JITDUMP($"\nInline candidate has SIMD type args, locals or return value.  Multiplier increased to {multiplier}.");
        }

#endif

        if (LooksLikeWrapperMethod)
        {
            multiplier += 1.0;
            JITDUMP($"\nInline candidate looks like a wrapper method.  Multiplier increased to {multiplier}.");
        }

        if (_argFeedsConstantTest > 0)
        {
            multiplier += 1.0;
            JITDUMP($"\nInline candidate has an arg that feeds a constant test.  Multiplier increased to {multiplier}.");
        }

        if (MethodIsMostlyLoadStore)
        {
            multiplier += 3.0;
            JITDUMP($"\nInline candidate is mostly loads and stores.  Multiplier increased to {multiplier}.");
        }

        if (_argFeedsRangeCheck > 0)
        {
            multiplier += 0.5;
            JITDUMP($"\nInline candidate has arg that feeds range check.  Multiplier increased to {multiplier}.");
        }

        if (_constantArgFeedsConstantTest > 0)
        {
            multiplier += 3.0;
            JITDUMP($"\nInline candidate has const arg that feeds a conditional.  Multiplier increased to {multiplier}.");
        }
        // For prejit roots we do not see the call sites. To be suitably optimistic
        // assume that call sites may pass constants.
        else if (_isPrejitRoot && ((_argFeedsConstantTest > 0) || (_argFeedsTest > 0)))
        {
            multiplier += 3.0;
            JITDUMP($"\nPrejit root candidate has arg that feeds a conditional.  Multiplier increased to {multiplier}.");
        }

        switch (_callsiteFrequency)
        {
            case InlineCallsiteFrequency.RARE:
            {
                // Note this one is not additive, it uses '=' instead of '+='
                multiplier = 1.3;
                JITDUMP($"\nInline candidate callsite is rare.  Multiplier limited to {multiplier}.");
                break;
            }

            case InlineCallsiteFrequency.BORING:
            {
                multiplier += 1.3;
                JITDUMP($"\nInline candidate callsite is boring.  Multiplier increased to {multiplier}.");
                break;
            }

            case InlineCallsiteFrequency.WARM:
            {
                multiplier += 2.0;
                JITDUMP($"\nInline candidate callsite is warm.  Multiplier increased to {multiplier}.");
                break;
            }

            case InlineCallsiteFrequency.LOOP:
            {
                multiplier += 3.0;
                JITDUMP($"\nInline candidate callsite is in a loop.  Multiplier increased to {multiplier}.");
                break;
            }

            case InlineCallsiteFrequency.HOT:
            {
                multiplier += 3.0;
                JITDUMP($"\nInline candidate callsite is hot.  Multiplier increased to {multiplier}.");
                break;
            }

            default:
            {
                NO_WAY("Unexpected callsite frequency");
                break;
            }
        }

#if DEBUG
        var additionalMultiplier = JitConfig.JitInlineAdditionalMultiplier;

        if (additionalMultiplier is not 0)
        {
            multiplier += additionalMultiplier;
            JITDUMP($"\nmultiplier increased via JitInlineAdditionalMultiplier={additionalMultiplier} to {multiplier}.");
        }

        if (_rootCompiler.compInlineStress())
        {
            multiplier += 10;
            JITDUMP($"\nmultiplier increased via inline stress to {multiplier}.");
        }
#endif

        return multiplier;
    }

    /// <summary>return estimated native code size for this inline candidate.</summary>
    /// <returns></returns>
    /// <remarks>
    ///   <para>This is an estimate for the size of the inlined callee. It does not include size impact on the caller side.</para>
    ///   <para>Uses the results of a state machine model for discretionary candidates. Should not be needed for forced or always candidates.</para>
    /// </remarks>
    protected int DetermineNativeSizeEstimate()
    {
        // Should be a discretionary candidate.
        assert(_stateMachine is not null);
        return _stateMachine.NativeSize;
    }

    /// <summary>estimate native size for the callsite.</summary>
    /// <param name="methodInfo">method info for the callee</param>
    /// <returns></returns>
    /// <remarks>Estimates the native size (in bytes, scaled up by 10x) for the call site. While the quality of the estimate here is questionable (especially for x64) it is being left as is for legacy compatibility.</remarks>
    protected unsafe int DetermineCallsiteNativeSizeEstimate(in CORINFO_METHOD_INFO methodInfo)
    {
        // Direct call take 5 native bytes; indirect call takes 6 native bytes.
        var callsiteSize = 55;

        if (methodInfo.args.hasImplicitThis())
        {
            // "mov" or "lea"
            callsiteSize += 30;
        }

        var argLst = methodInfo.args.args;
        var comp = _rootCompiler.info.compCompHnd;

        for (var i = 0; i < methodInfo.args.numArgs; i++, argLst = comp->getArgNext(argLst))
        {
            CORINFO_CLASS_HANDLE sigClass;
            var_types sigType;

            fixed (CORINFO_SIG_INFO* pArgs = &methodInfo.args)
            {
                sigType = strip(comp->getArgType(pArgs, argLst, &sigClass)).VarType;
            }

            if (sigType is TYP_STRUCT)
            {
                // IN0028: 00009B      lea     EAX, bword ptr [EBP-14H]
                // IN0029: 00009E      push    dword ptr [EAX+4]
                // IN002a: 0000A1      push    gword ptr [EAX]
                // IN002b: 0000A3      call    [MyStruct.staticGetX2(struct):int]

                // "lea     EAX, bword ptr [EBP-14H]"
                callsiteSize += 10;

                var opsz = roundUp(comp->getClassSize(sigClass), TARGET_POINTER_SIZE);
                var slots = opsz / TARGET_POINTER_SIZE;

                // "push    gword ptr [EAX+offs]  "
                callsiteSize += slots * 20;
            }
            else
            {
                // push by average takes 3 bytes.
                callsiteSize += 30;
            }
        }
        return callsiteSize;
    }
}
