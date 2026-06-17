// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.IO;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public abstract class InlinePolicy
{
    protected InlineDecision _decision;

    protected InlineObservation _observation;

    protected bool _isPrejitRoot;

#if DEBUG
    protected bool _isDataCollectionTarget;
#endif

    protected InlinePolicy(bool isPrejitRoot)
    {
        _isPrejitRoot = isPrejitRoot;
    }

    /// <summary>Get the current decision</summary>
    public InlineDecision Decision => _decision;

#if DEBUG
    /// <summary>True if this is the inline targeted by data collection</summary>
    public bool IsDataCollectionTarget => _isDataCollectionTarget;

    public abstract string Name { get; }
#endif

    /// <summary>Get the observation responsible for the result</summary>
    public InlineObservation Observation => _observation;

    /// <summary>Does Policy require a more precise IL scan?</summary>
    public virtual bool RequiresPreciseScan => false;

    /// <summary>Factory method for getting an InlinePolicy</summary>
    /// <param name="compiler">the compiler instance that will evaluate inlines</param>
    /// <param name="isPrejitRoot">true if this policy is evaluating a prejit root</param>
    /// <returns>InlinePolicy to use in evaluating an inline.</returns>
    /// <remarks>Determines which of the various policies should apply, and creates (or reuses) a policy instance to use.</remarks>
    public static unsafe InlinePolicy GetPolicy(Compiler compiler, bool isPrejitRoot)
    {
#if DEBUG
        var useRandomPolicyForStress = compiler.compRandomInlineStress();
        var useRandomPolicy = JitConfig.JitInlinePolicyRandom is not 0;

        // Optionally install the RandomPolicy.
        if (useRandomPolicyForStress || useRandomPolicy)
        {
            return new RandomPolicy(compiler, isPrejitRoot);
        }

        // Optionally install the ReplayPolicy.
        var useReplayPolicy = JitConfig.JitInlinePolicyReplay is not 0;

        if (useReplayPolicy)
        {
            return new ReplayPolicy(compiler, isPrejitRoot);
        }

        // Optionally install the SizePolicy.
        var useSizePolicy = JitConfig.JitInlinePolicySize is not 0;

        if (useSizePolicy)
        {
            return new SizePolicy(compiler, isPrejitRoot);
        }

        // Optionally install the FullPolicy.
        var useFullPolicy = JitConfig.JitInlinePolicyFull is not 0;

        if (useFullPolicy)
        {
            return new FullPolicy(compiler, isPrejitRoot);
        }

        // Optionally install the DiscretionaryPolicy.
        var useDiscretionaryPolicy = JitConfig.JitInlinePolicyDiscretionary is not 0;

        if (useDiscretionaryPolicy)
        {
            return new DiscretionaryPolicy(compiler, isPrejitRoot);
        }
#endif

        // Optionally install the ModelPolicy.
        var useModelPolicy = JitConfig.JitInlinePolicyModel is not 0;

        if (useModelPolicy)
        {
            return new ModelPolicy(compiler, isPrejitRoot);
        }

        // Optionally install the ProfilePolicy, if the method has profile data.
        var enableProfilePolicy = JitConfig.JitInlinePolicyProfile is not 0;
        var hasProfileData = compiler.fgIsUsingProfileWeights;

        if (enableProfilePolicy && hasProfileData)
        {
            return new ProfilePolicy(compiler, isPrejitRoot);
        }

        var isPrejit = compiler.IsAot;
        var isSpeedOpt = compiler.opts.jitFlags->IsSet(JitFlags.JIT_FLAG_SPEED_OPT);

        if ((JitConfig.JitExtDefaultPolicy is not 0))
        {
            if (isPrejitRoot || !isPrejit || (isPrejit && isSpeedOpt))
            {
                return new ExtendedDefaultPolicy(compiler, isPrejitRoot);
            }
        }

        return new DefaultPolicy(compiler, isPrejitRoot);
    }

    /// <summary>see if this inline would exceed the current budget</summary>
    /// <returns>True if inline would exceed the budget.</returns>
    public abstract bool BudgetCheck();

    /// <summary>estimated code size impact of the inline</summary>
    /// <returns>Estimated code size impact, in bytes * 10</returns>
    /// <remarks>Only meaningful for discretionary inlines (whether successful or not).  For always or force inlines the legacy policy doesn't estimate size impact.</remarks>
    public abstract int CodeSizeEstimate();

    /// <summary>determine if this inline is profitable</summary>
    /// <param name="methodInfo">method info for the callee</param>
    /// <remarks>
    ///   <para>A profitable inline is one that is projected to have a beneficial size/speed tradeoff.</para>
    ///   <para>It is expected that this method is only invoked for discretionary candidates, since it does not make sense to do this assessment for failed, always, or forced inlines.</para>
    /// </remarks>
    public abstract void DetermineProfitability(in CORINFO_METHOD_INFO methodInfo);

#if DEBUG
    public virtual void DumpData(StreamWriter stream) { }

    public virtual void DumpSchema(StreamWriter stream) { }

    /// <summary>Dump DefaultPolicy data as XML</summary>
    /// <param name="stream">stream to output to</param>
    /// <param name="indent">indent level</param>
    public virtual void DumpXml(StreamWriter stream, int indent = 0)
    {
        stream.Write($"{new string(' ', indent)}<{Name}");
        OnDumpXml(stream);
        stream.WriteLine(" />");
    }
#endif

    /// <summary>handle a boolean observation with non-fatal impact</summary>
    /// <param name="observation">the current obsevation</param>
    /// <param name="value">the value of the observation</param>
    public abstract void NoteBool(InlineObservation observation, bool value);

    public virtual void NoteContext(InlineContext? context) { }

    /// <summary>handle an observed double value</summary>
    /// <param name="observation">the current obsevation</param>
    /// <param name="value">the value being observed</param>
    public abstract void NoteDouble(InlineObservation observation, double value);

    /// <summary>Handle an observation with fatal impact</summary>
    /// <param name="observation">The current obsevation</param>
    public abstract void NoteFatal(InlineObservation observation);

    /// <summary>handle an observed integer value</summary>
    /// <param name="observation">the current obsevation</param>
    /// <param name="value">the value being observed</param>
    public abstract void NoteInt(InlineObservation observation, int value);

    public virtual void NoteOffset(IL_OFFSET offset) { }

#if DEBUG
    /// <summary>record reason for earlier inline failure</summary>
    /// <param name="observation">the current obsevation</param>
    /// <remarks>Used to "resurrect" failure observations from the early inline screen when building the inline context tree. Only used during debug modes.</remarks>
    public abstract void NotePriorFailure(InlineObservation observation);
#endif

    /// <summary>handle finishing all the inlining checks successfully</summary>
    public abstract void NoteSuccess();

#if DEBUG
    public virtual void OnDumpXml(StreamWriter stream, int indent = 0) { }
#endif

    /// <summary>determine if a never result should cause the method to be marked as un-inlinable.</summary>
    /// <returns></returns>
    public abstract bool PropagateNeverToRuntime();

    protected static void XATTR_B(StreamWriter stream, bool value, [CallerArgumentExpression(nameof(value))] string valueExpression = "")
    {
        if (value)
        {
            stream.Write($" {valueExpression}=\"{value}\"");
        }
    }

    protected static void XATTR_I4(StreamWriter stream, int value, [CallerArgumentExpression(nameof(value))] string valueExpression = "")
    {
        if (value is not 0)
        {
            stream.Write($" {valueExpression}=\"{value}\"");
        }
    }

    protected static void XATTR_R8(StreamWriter stream, double value, [CallerArgumentExpression(nameof(value))] string valueExpression = "")
    {
        if (double.Abs(value) > 0.01)
        {
            stream.Write($" {valueExpression}=\"{value:F2}\"");
        }
    }
}
