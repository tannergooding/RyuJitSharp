// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public abstract class InlinePolicy
{
    protected InlineDecision m_Decision;

    protected InlineObservation m_Observation;

    protected bool m_IsPrejitRoot;

#if DEBUG
    protected bool m_IsDataCollectionTarget;
#endif

    /// <summary>Get the current decision</summary>
    public InlineDecision Decision => m_Decision;

    /// <summary>Get the observation responsible for the result</summary>
    public InlineObservation Observation => m_Observation;

    /// <summary>Does Policy require a more precise IL scan?</summary>
    public virtual bool RequiresPreciseScan => false;

    public static InlinePolicy GetPolicy(Compiler compiler, bool isPrejitRoot)
    {
        // TODO: Port InlinePolicy.GetPolicy;
        return null!;
    }

    public abstract unsafe void DetermineProfitability(in CORINFO_METHOD_INFO methodInfo);

    public abstract void NoteBool(InlineObservation observation, bool value);

    public abstract void NoteDouble(InlineObservation observation, double value);

    public abstract void NoteFatal(InlineObservation observation);

    public abstract void NoteInt(InlineObservation observation, int value);

    public abstract void NoteSuccess();
}
