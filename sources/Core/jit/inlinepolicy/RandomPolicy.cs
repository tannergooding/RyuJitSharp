// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
namespace RyuJitSharp;

public sealed class RandomPolicy : DiscretionaryPolicy
{
    private readonly CLRRandom _random;

    public RandomPolicy(Compiler compiler, bool isPrejitRoot)
        : base(compiler, isPrejitRoot)
    {
        var strategy = compiler._inlineStrategy;
        assert(strategy is not null);
        _random = strategy.GetRandom();
    }

    public override string Name => nameof(RandomPolicy);

    public override void NoteInt(InlineObservation observation, int value)
    {
        if (observation is InlineObservation.CALLEE_IL_CODE_SIZE)
        {
            assert(IsForceInlineKnown);
            assert(value != 0);
            _codeSize = value;
            SetCandidate(IsForceInline
                ? InlineObservation.CALLEE_IS_FORCE_INLINE
                : InlineObservation.CALLEE_IS_DISCRETIONARY_INLINE);
            return;
        }

        base.NoteInt(observation, value);
    }

    public override void DetermineProfitability(in CORINFO_METHOD_INFO methodInfo)
    {
        assert(_decision.IsCandidate);
        assert(_observation is InlineObservation.CALLEE_IS_DISCRETIONARY_INLINE);

        if (BudgetCheck())
        {
            SetFailure(InlineObservation.CALLSITE_OVER_BUDGET);
            return;
        }

        if (JitConfig.JitInlineDumpData != 0)
        {
            MethodInfoObservations(methodInfo);
            EstimateCodeSize();
            EstimatePerformanceImpact();
        }

        var codeSize = unchecked((uint)_codeSize);
        uint threshold = codeSize switch {
            <= 16 => 75,
            <= 30 => 50,
            <= 40 => 40,
            <= 50 => 30,
            <= 75 => 20,
            <= 100 => 10,
            <= 200 => 5,
            _ => 1,
        };
        var randomValue = _random.Next(1, 100);

        if (randomValue > threshold)
        {
            _rootCompiler.JITLOG(LL_INFO100000, $"Random rejection (r={randomValue} > t={threshold})\n");

            if (_isPrejitRoot)
            {
                SetNever(InlineObservation.CALLEE_RANDOM_REJECT);
            }
            else
            {
                SetFailure(InlineObservation.CALLSITE_RANDOM_REJECT);
            }
        }
        else
        {
            _rootCompiler.JITLOG(LL_INFO100000, $"Random acceptance (r={randomValue} <= t={threshold})\n");
            SetCandidate(_isPrejitRoot
                ? InlineObservation.CALLEE_RANDOM_ACCEPT
                : InlineObservation.CALLSITE_RANDOM_ACCEPT);
        }
    }
}
#endif
