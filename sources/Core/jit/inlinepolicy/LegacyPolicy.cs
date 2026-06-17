// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

/// <summary>encapsulates the common legality and ability checks the inliner must make.</summary>
/// <remarks> Generally speaking, the legal policy expects the inlining attempt to fail fast when a fatal or equivalent observation is made. So once an observation causes failure, no more observations are expected. However for the prejit scan case (where the jit is not actually inlining, but is assessing a method's general inlinability) the legal policy allows multiple failing observations provided they have the same impact. Only the first observation that puts the policy into a failing state is remembered. Transitions from failing states to candidate or success states are not allowed.</remarks>
public abstract class LegacyPolicy : InlinePolicy
{
    protected LegacyPolicy(bool isPrejitRoot)
        : base(isPrejitRoot)
    {
    }

    public override void NoteFatal(InlineObservation observation)
    {
        // As a safeguard, all fatal impact must be
        // reported via NoteFatal.
        assert(observation.Impact == InlineImpact.FATAL);
        NoteInternal(observation);
        assert(_decision.IsFailure);
    }

#if DEBUG
    public override void NotePriorFailure(InlineObservation observation)
    {
        NoteInternal(observation);
        assert(_decision.IsFailure);
    }
#endif

    /// <summary>helper for handling an observation</summary>
    /// <param name="observation">The current observation</param>
    protected void NoteInternal(InlineObservation observation)
    {
        // Note any INFORMATION that reaches here will now cause failure.
        // Non-fatal INFORMATION observations must be handled higher up.
        var target = observation.Target;

        if (target == InlineTarget.CALLEE)
        {
            SetNever(observation);
        }
        else
        {
            SetFailure(observation);
        }
    }

    /// <summary>helper updating candidacy</summary>
    /// <param name="obs">the current observation</param>
    /// <remarks>Candidate observations are handled here. If the inline has already failed, they're ignored. If there's already a candidate reason, this new reason trumps it.</remarks>
    protected void SetCandidate(InlineObservation obs)
    {
        // Ignore if this inline is going to fail.
        if (_decision.IsFailure)
        {
            return;
        }

        // We should not have declared success yet.
        assert(!_decision.IsSuccess);

        // Update, overriding any previous candidacy.
        _decision = InlineDecision.CANDIDATE;
        _observation = obs;
    }

    /// <summary>helper for setting a failing decision</summary>
    /// <param name="observation">the current observation</param>
    protected void SetFailure(InlineObservation observation)
    {
        // Expect a valid observation
        assert(observation.IsValid);

        switch (_decision)
        {
            case InlineDecision.FAILURE:
            {
                // Repeated failure only ok if evaluating a prejit root
                // (since we can't fail fast because we're not inlining)
                // or if inlining and the observation is CALLSITE_TOO_MANY_LOCALS
                // (since we can't fail fast from lvaGrabTemp).
                assert(_isPrejitRoot || (observation is InlineObservation.CALLSITE_TOO_MANY_LOCALS));
                break;
            }

            case InlineDecision.UNDECIDED:
            case InlineDecision.CANDIDATE:
            {
                _decision = InlineDecision.FAILURE;
                _observation = observation;
                break;
            }

            default:
            {
                // SUCCESS, NEVER, or ??
                NO_WAY("Unexpected m_Decision");
                break;
            }
        }
    }

    /// <summary>helper for setting a never decision</summary>
    /// <param name="observation">the current observation</param>
    protected void SetNever(InlineObservation observation)
    {
        // Expect a valid observation
        assert(observation.IsValid);

        switch (_decision)
        {
            case InlineDecision.NEVER:
            {
                // Repeated never only ok if evaluating a prejit root
                assert(_isPrejitRoot);
                break;
            }

            case InlineDecision.UNDECIDED:
            case InlineDecision.CANDIDATE:
            {
                _decision = InlineDecision.NEVER;
                _observation = observation;
                break;
            }

            default:
            {
                // SUCCESS, FAILURE or ??
                NO_WAY("Unexpected _decision");
                break;
            }
        }
    }
}
