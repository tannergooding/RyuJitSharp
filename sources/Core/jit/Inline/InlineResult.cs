// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed class InlineResult
{
    private Compiler m_RootCompiler;

    private InlinePolicy m_Policy;

    private GenTreeCall? m_Call;

    private InlineContext? m_InlineContext;

    /// <summary>immediate caller's handle</summary>
    private unsafe CORINFO_METHOD_HANDLE m_Caller;

    private unsafe CORINFO_METHOD_HANDLE m_Callee;

    /// <summary>estimated size of imported IL</summary>
    private uint  m_ImportedILSize;

    private string m_Description = "";

    private CorInfoInline m_successResult;

    private bool m_DoNotReport;

    private bool m_reportFailureAsVmFailure;

    /// <summary>Construct a new InlineResult to evaluate a particular method to see if it is inlineable.</summary>
    /// <param name="compiler"></param>
    /// <param name="method"></param>
    /// <param name="description"></param>
    /// <param name="doNotReport"></param>
    public unsafe InlineResult(Compiler compiler, CORINFO_METHOD_HANDLE method, string description, bool doNotReport = false)
    {
        m_Callee = method;
        m_Description = description;
        m_DoNotReport = doNotReport;

        var rootCompiler = compiler.impInlineRoot;
        m_RootCompiler = rootCompiler;
        m_Policy = InlinePolicy.GetPolicy(rootCompiler, isPrejitRoot: true);

        if (!doNotReport)
        {
            var jitInfo = rootCompiler.info.compCompHnd;
            jitInfo->beginInlining(inlinerHnd: null, inlineeHnd: method);
        }
    }

    /// <summary>Has the policy determined this inline attempt is still viable?</summary>
    public bool IsCandidate => m_Policy.Decision.IsCandidate;

    /// <summary>Has the policy made a determination?</summary>
    public bool IsDecided => m_Policy.Decision.IsDecided;

    /// <summary>Has the policy determined this inline attempt is still viable and is a discretionary inline?</summary>
    public bool IsDiscretionaryCandidate => m_Policy.Decision.IsCandidate && (m_Policy.Observation is InlineObservation.CALLEE_IS_DISCRETIONARY_INLINE);

    /// <summary>Has the policy determined this inline should fail?</summary>
    public bool IsFailure => m_Policy.Decision.IsFailure;

    /// <summary>Has the policy determined this inline will fail, and that the callee should never be inlined?</summary>
    public bool IsNever => m_Policy.Decision.IsNever;

    /// <summary>Has the policy determined this inline will succeed?</summary>
    public bool IsSuccess => m_Policy.Decision.IsSuccess;

    /// <summary>Get the observation leading to this particular result</summary>
    public InlineObservation Observation => m_Policy.Observation;

    /// <summary>Get the policy that evaluated this result.</summary>
    public InlinePolicy Policy => m_Policy;

    public CorInfoInline Result
    {
        get
        {
            if (m_reportFailureAsVmFailure)
            {
                return INLINE_CHECK_CAN_INLINE_VMFAIL;
            }

            if (m_successResult != INLINE_PASS)
            {
                return m_successResult;
            }
            return m_Policy.Decision.CorInfo;
        }

        set
        {
            m_successResult = value;
        }
    }

    /// <summary>Determine if this inline is profitable</summary>
    /// <param name="methodInfo"></param>
    public unsafe void DetermineProfitability(CORINFO_METHOD_INFO* methodInfo)
        => m_Policy.DetermineProfitability(methodInfo);

    /// <summary>Make a true observation, and update internal state appropriately.</summary>
    /// <param name="observation"></param>
    /// <remarks>Caller is expected to call isFailure after this to see whether more observation is desired.</remarks>
    public void Note(InlineObservation observation)
        => m_Policy.NoteBool(observation, value: true);

    /// <summary>Make a boolean observation, and update internal state appropriately.</summary>
    /// <param name="observation"></param>
    /// <param name="value"></param>
    /// <remarks>Caller is expected to call isFailure after this to see whether more observation is desired.</remarks>
    public void NoteBool(InlineObservation observation, bool value)
        => m_Policy.NoteBool(observation, value);

    /// <summary>Make an observation with a double value</summary>
    /// <param name="observation"></param>
    /// <param name="value"></param>
    public void NoteDouble(InlineObservation observation, double value)
        => m_Policy.NoteDouble(observation, value);

    /// <summary>Make an observation that must lead to immediate failure.</summary>
    /// <param name="observation"></param>
    public void NoteFatal(InlineObservation observation)
    {
        m_Policy.NoteFatal(observation);
        assert(IsFailure);
    }

    /// <summary>Make an observation with an int value</summary>
    /// <param name="observation"></param>
    /// <param name="value"></param>
    public void NoteInt(InlineObservation observation, int value)
        => m_Policy.NoteInt(observation, value);

    public void NoteInt(InlineObservation observation, uint value)
        => m_Policy.NoteInt(observation, (int)(value));

    /// <summary>NoteSuccess means the all the various checks have passed and the inline can happen.</summary>
    public void NoteSuccess()
    {
        assert(IsCandidate);
        m_Policy.NoteSuccess();
    }
}
