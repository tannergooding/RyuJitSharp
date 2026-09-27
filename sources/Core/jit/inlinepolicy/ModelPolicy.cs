// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed class ModelPolicy : DiscretionaryPolicy
{
    public ModelPolicy(Compiler compiler, bool isPrejitRoot)
        : base(compiler, isPrejitRoot)
    {
    }

#if DEBUG
    public override string Name => nameof(ModelPolicy);
#endif

    public override bool PropagateNeverToRuntime()
    {
        return true;
    }

    public override void NoteInt(InlineObservation observation, int value)
    {
        base.NoteInt(observation, value);

        if (_decision.IsFailure)
        {
            return;
        }

        // The model's early IL-size cutoff is independent of the legacy size limit.
        if (!IsForceInline && (observation is InlineObservation.CALLEE_IL_CODE_SIZE) && (value >= 120))
        {
            SetNever(InlineObservation.CALLEE_TOO_MUCH_IL);
        }
    }

    public override void DetermineProfitability(in CORINFO_METHOD_INFO methodInfo)
    {
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
        }
        else
        {
            var perCallBenefit = -((double)_perCallInstructionEstimate / _modelCodeSizeEstimate);
            var callSiteWeight = 1.0;

            switch (_callsiteFrequency)
            {
                case InlineCallsiteFrequency.RARE:
                {
                    callSiteWeight = 0.1;
                    break;
                }

                case InlineCallsiteFrequency.BORING:
                {
                    callSiteWeight = 1.0;
                    break;
                }

                case InlineCallsiteFrequency.WARM:
                {
                    callSiteWeight = 1.5;
                    break;
                }

                case InlineCallsiteFrequency.LOOP:
                case InlineCallsiteFrequency.HOT:
                {
                    callSiteWeight = 3.0;
                    break;
                }

                default:
                {
                    assert(false);
                    break;
                }
            }

            var benefit = callSiteWeight * perCallBenefit;
            var threshold = 0.20;
            var shouldInline = benefit > threshold;

            _rootCompiler.JITLOG(LL_INFO100000,
                $"Inline {(shouldInline ? "is" : "is not")} profitable: benefit={formatFloat(benefit, "g6")} " +
                $"(weight={formatFloat(callSiteWeight, "g6")}, percall={formatFloat((double)_perCallInstructionEstimate / SIZE_SCALE, "g6")}, " +
                $"size={formatFloat((double)_modelCodeSizeEstimate / SIZE_SCALE, "g6")})\n");

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
}
