// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

#if DEBUG
public sealed class AsyncStressPolicy : ExtendedDefaultPolicy
{
    private bool _isAsyncCall;
    private int _asyncStressIndex = -1;
    private uint _basicBlockCount;

    public AsyncStressPolicy(Compiler compiler, bool isPrejitRoot)
        : base(compiler, isPrejitRoot)
    {
    }

    public override string Name => nameof(AsyncStressPolicy);

    // Late-created candidates have no index and retain the normal heuristics.
    // Prejit roots have no callsite group but must remain available for inlining.
    private bool IsStressPicked => _isAsyncCall && (_isPrejitRoot || (_asyncStressIndex >= 0));

    public override void NoteBool(InlineObservation observation, bool value)
    {
        if (observation == InlineObservation.CALLEE_IS_ASYNC)
        {
            _isAsyncCall = value;
            return;
        }

        base.NoteBool(observation, value);
    }

    public override void NoteInt(InlineObservation observation, int value)
    {
        switch (observation)
        {
            case InlineObservation.CALLSITE_ASYNC_STRESS_INDEX:
            {
                _asyncStressIndex = value;
                return;
            }

            case InlineObservation.CALLEE_IL_CODE_SIZE:
            {
                assert(IsForceInlineKnown);
                assert(value != 0);
                _codeSize = value;

                var alwaysInlineSize = InlineStrategy.ALWAYS_INLINE_SIZE;

                if (InsideThrowBlock)
                {
                    alwaysInlineSize /= 2;
                }

                // Size-based SetNever decisions affect all callers. Defer them until
                // async selection is known, but retain the implementation's hard limit.
                if (unchecked((uint)_codeSize) > InlineStrategy.IMPLEMENTATION_MAX_INLINE_SIZE)
                {
                    SetNever(InlineObservation.CALLEE_TOO_MUCH_IL);
                }
                else if (IsForceInline)
                {
                    SetCandidate(InlineObservation.CALLEE_IS_FORCE_INLINE);
                }
                else if (unchecked((uint)_codeSize) <= alwaysInlineSize)
                {
                    SetCandidate(InlineObservation.CALLEE_BELOW_ALWAYS_INLINE_SIZE);
                }
                else
                {
                    SetCandidate(InlineObservation.CALLEE_IS_DISCRETIONARY_INLINE);
                }

                return;
            }

            case InlineObservation.CALLEE_NUMBER_OF_BASIC_BLOCKS:
            {
                _basicBlockCount = unchecked((uint)value);

                // No-return rejection is not a size limit.
                if (!IsForceInline && IsNoReturn && (value == 1))
                {
                    SetNever(InlineObservation.CALLEE_DOES_NOT_RETURN);
                }

                return;
            }
        }

        // In particular, MAXSTACK remains subject to the normal policy.
        base.NoteInt(observation, value);
    }

    public override bool BudgetCheck()
    {
        if (IsStressPicked)
        {
            return false;
        }

        return base.BudgetCheck();
    }

    public override void DetermineProfitability(in CORINFO_METHOD_INFO methodInfo)
    {
        if (!IsStressPicked)
        {
            base.NoteInt(InlineObservation.CALLEE_IL_CODE_SIZE, _codeSize);

            if (_decision.IsFailure)
            {
                return;
            }

            base.NoteInt(InlineObservation.CALLEE_NUMBER_OF_BASIC_BLOCKS, unchecked((int)_basicBlockCount));

            if (_decision.IsFailure)
            {
                return;
            }

            base.DetermineProfitability(methodInfo);
            return;
        }

        if (_isPrejitRoot)
        {
            SetCandidate(InlineObservation.CALLEE_IS_PROFITABLE_INLINE);
            return;
        }

        assert(_callsiteDepth > 0);

        if (unchecked((uint)_callsiteDepth) > unchecked((uint)JitConfig.JitStressAsyncInliningMaxDepth))
        {
            SetFailure(InlineObservation.CALLSITE_RANDOM_REJECT);
            return;
        }

        // The shuffled candidate index and depth both reduce the chance of inlining,
        // avoiding explosive expansion of bodies with many async calls.
        var pct = JitConfig.JitStressAsyncInliningPct / 100.0;
        var probability = Math.Pow(pct, unchecked((uint)_callsiteDepth + (uint)_asyncStressIndex));
        var strategy = _rootCompiler._inlineStrategy;
        assert(strategy is not null);
        var random = strategy.GetRandom(_rootCompiler.compAsyncInliningStressSeed());

        if (random.NextDouble() < probability)
        {
            SetCandidate(InlineObservation.CALLSITE_RANDOM_ACCEPT);
        }
        else
        {
            SetFailure(InlineObservation.CALLSITE_RANDOM_REJECT);
        }
    }
}
#endif
