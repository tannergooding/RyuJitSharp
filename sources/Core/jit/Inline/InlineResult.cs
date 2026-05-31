// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed class InlineResult
{
    private Compiler _rootCompiler;

    private InlinePolicy _policy;

    private GenTreeCall? _call;

    private InlineContext? _inlineContext;

    /// <summary>immediate caller's handle</summary>
    private unsafe CORINFO_METHOD_HANDLE _caller;

    private unsafe CORINFO_METHOD_HANDLE _callee;

    /// <summary>estimated size of imported IL</summary>
    private int  _importedILSize;

    private string _description = "";

    private CorInfoInline _successResult;

    private bool _doNotReport;

    private bool _reportFailureAsVmFailure;

    /// <summary>Construct a new InlineResult to evaluate a particular method to see if it is inlineable.</summary>
    /// <param name="compiler"></param>
    /// <param name="method"></param>
    /// <param name="description"></param>
    /// <param name="doNotReport"></param>
    public unsafe InlineResult(Compiler compiler, CORINFO_METHOD_HANDLE method, string description, bool doNotReport = false)
    {
        _callee = method;
        _description = description;
        _doNotReport = doNotReport;

        var rootCompiler = compiler.impInlineRoot;
        _rootCompiler = rootCompiler;
        _policy = InlinePolicy.GetPolicy(rootCompiler, isPrejitRoot: true);

        if (!doNotReport)
        {
            var jitInfo = rootCompiler.info.compCompHnd;
            jitInfo->beginInlining(inlinerHnd: null, inlineeHnd: method);
        }
    }

    /// <summary>Construct a new InlineResult to help evaluate a particular call for inlining.</summary>
    /// <param name="compiler"></param>
    /// <param name="call"></param>
    /// <param name="stmt"></param>
    /// <param name="description"></param>
    /// <param name="doNotReport"></param>
    public unsafe InlineResult(Compiler compiler, GenTreeCall call, Statement? stmt, string description, bool doNotReport = false)
    {
        _call = call;
        _description = description;
        _doNotReport = doNotReport;

        // Set the compiler instance
        _rootCompiler = compiler.impInlineRoot;

        // Set the policy
        const bool isPrejitRoot = false;
        _policy = InlinePolicy.GetPolicy(_rootCompiler, isPrejitRoot);

        // Pass along some optional information to the policy.
        if (stmt is not null)
        {
            _inlineContext = stmt.DebugInfo.InlineContext;
            _policy.NoteContext(_inlineContext);

#if DEBUG
            _policy.NoteOffset(call._rawILOffset);
#else
            _policy.NoteOffset(stmt.DebugInfo.Location.Offset);
#endif
        }

        // Get method handle for caller. Note we use the
        // handle for the "immediate" caller here.
        _caller = compiler.info.compMethodHnd;

        // Get method handle for callee, if known
        if (_call._callType == CT_USER_FUNC)
        {
            _callee = _call._callMethHnd;
        }

        if (!_doNotReport)
        {
            _rootCompiler.info.compCompHnd->beginInlining(_caller, _callee);
        }
    }

    public int ImportedILSize
    {
        get
        {
            return _importedILSize;
        }

        set
        {
            _importedILSize = value;
        }
    }

    /// <summary>Has the policy determined this inline attempt is still viable?</summary>
    public bool IsCandidate => _policy.Decision.IsCandidate;

    /// <summary>Has the policy made a determination?</summary>
    public bool IsDecided => _policy.Decision.IsDecided;

    /// <summary>Has the policy determined this inline attempt is still viable and is a discretionary inline?</summary>
    public bool IsDiscretionaryCandidate => _policy.Decision.IsCandidate && (_policy.Observation is InlineObservation.CALLEE_IS_DISCRETIONARY_INLINE);

    /// <summary>Has the policy determined this inline should fail?</summary>
    public bool IsFailure => _policy.Decision.IsFailure;

    /// <summary>Has the policy determined this inline will fail, and that the callee should never be inlined?</summary>
    public bool IsNever => _policy.Decision.IsNever;

    /// <summary>Has the policy determined this inline will succeed?</summary>
    public bool IsSuccess => _policy.Decision.IsSuccess;

    /// <summary>Get the observation leading to this particular result</summary>
    public InlineObservation Observation => _policy.Observation;

    /// <summary>Get the policy that evaluated this result.</summary>
    public InlinePolicy Policy => _policy;

    public CorInfoInline Result
    {
        get
        {
            if (_reportFailureAsVmFailure)
            {
                return INLINE_CHECK_CAN_INLINE_VMFAIL;
            }

            if (_successResult != INLINE_PASS)
            {
                return _successResult;
            }
            return _policy.Decision.CorInfo;
        }

        set
        {
            _successResult = value;
        }
    }

    /// <summary>Determine if this inline is profitable</summary>
    /// <param name="methodInfo"></param>
    public unsafe void DetermineProfitability(in CORINFO_METHOD_INFO methodInfo)
        => _policy.DetermineProfitability(methodInfo);

    /// <summary>Make a true observation, and update internal state appropriately.</summary>
    /// <param name="observation"></param>
    /// <remarks>Caller is expected to call isFailure after this to see whether more observation is desired.</remarks>
    public void Note(InlineObservation observation)
        => _policy.NoteBool(observation, value: true);

    /// <summary>Make a boolean observation, and update internal state appropriately.</summary>
    /// <param name="observation"></param>
    /// <param name="value"></param>
    /// <remarks>Caller is expected to call isFailure after this to see whether more observation is desired.</remarks>
    public void NoteBool(InlineObservation observation, bool value)
        => _policy.NoteBool(observation, value);

    /// <summary>Make an observation with a double value</summary>
    /// <param name="observation"></param>
    /// <param name="value"></param>
    public void NoteDouble(InlineObservation observation, double value)
        => _policy.NoteDouble(observation, value);

    /// <summary>Make an observation that must lead to immediate failure.</summary>
    /// <param name="observation"></param>
    public void NoteFatal(InlineObservation observation)
    {
        _policy.NoteFatal(observation);
        assert(IsFailure);
    }

    /// <summary>Make an observation with an int value</summary>
    /// <param name="observation"></param>
    /// <param name="value"></param>
    public void NoteInt(InlineObservation observation, int value)
        => _policy.NoteInt(observation, value);

    /// <summary>NoteSuccess means the all the various checks have passed and the inline can happen.</summary>
    public void NoteSuccess()
    {
        assert(IsCandidate);
        _policy.NoteSuccess();
    }

    public void SetVMFailure()
    {
        _reportFailureAsVmFailure = true;
    }
}
