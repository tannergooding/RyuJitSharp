// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed class ProfilePolicy : DiscretionaryPolicy
{
    public ProfilePolicy(Compiler compiler, bool isPrejitRoot)
        : base(compiler, isPrejitRoot)
    {
    }

#if DEBUG
    public override string Name => nameof(ProfilePolicy);
#endif

    public override void NoteInt(InlineObservation observation, int value)
    {
        base.NoteInt(observation, value);

        if (_decision.IsFailure)
        {
            return;
        }

        if (!IsForceInline && (observation is InlineObservation.CALLEE_IL_CODE_SIZE) && (value >= 1000))
        {
            SetNever(InlineObservation.CALLEE_TOO_MUCH_IL);
            return;
        }

        if (observation is InlineObservation.CALLEE_NUMBER_OF_BASIC_BLOCKS)
        {
            assert(IsForceInlineKnown);
            assert(IsNoReturnKnown);
            assert(value > 0);

            if (!IsForceInline && IsNoReturn && (value == 1))
            {
                SetNever(InlineObservation.CALLEE_DOES_NOT_RETURN);
                return;
            }

            if (!_hasProfileWeights && !IsForceInline && (value > 5))
            {
                SetNever(InlineObservation.CALLEE_TOO_MANY_BASIC_BLOCKS);
            }
        }
    }

    public override void DetermineProfitability(in CORINFO_METHOD_INFO methodInfo)
    {
        if (!_hasProfileWeights)
        {
            SetFailure(InlineObservation.CALLSITE_NOT_PROFITABLE_INLINE);
            return;
        }

        MethodInfoObservations(methodInfo);
        EstimateCodeSize();
        EstimatePerformanceImpact();

        if (_modelCodeSizeEstimate <= 0)
        {
            _rootCompiler.JITLOG(LL_INFO100000,
                $"Inline profitable, will decrease code size by {formatFloat((double)-_modelCodeSizeEstimate / SIZE_SCALE, "g6")} bytes\n");
            SetCandidate(_isPrejitRoot
                ? InlineObservation.CALLEE_IS_SIZE_DECREASING_INLINE
                : InlineObservation.CALLSITE_IS_SIZE_DECREASING_INLINE);
            return;
        }

        JITDUMP("Have profile data for call site...\n");

        var perCallBenefit = -((double)_perCallInstructionEstimate / _modelCodeSizeEstimate);
        var localBenefit = perCallBenefit * _profileFrequency;
        var globalImportance = 1.0;
        var benefit = globalImportance * localBenefit;
        var threshold = JitConfig.JitInlinePolicyProfileThreshold / 256.0;
        var shouldInline = benefit > threshold;

        _rootCompiler.JITLOG(LL_INFO100000,
            $"Inline {(shouldInline ? "is" : "is not")} profitable: benefit={formatFloat(benefit, "g6")} " +
            $"(perCall={formatFloat(perCallBenefit, "g6")}, local={formatFloat(localBenefit, "g6")}, " +
            $"global={formatFloat(globalImportance, "g6")}, size={formatFloat((double)_modelCodeSizeEstimate / SIZE_SCALE, "g6")})\n");

        if (!shouldInline)
        {
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
            SetCandidate(_isPrejitRoot
                ? InlineObservation.CALLEE_IS_PROFITABLE_INLINE
                : InlineObservation.CALLSITE_IS_PROFITABLE_INLINE);
        }
    }
}
