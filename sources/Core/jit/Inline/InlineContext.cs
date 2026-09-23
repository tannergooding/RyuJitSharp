// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections;

namespace RyuJitSharp;

public sealed class InlineContext
{
    /// <summary>overall strategy</summary>
    internal InlineStrategy _inlineStrategy;

    /// <summary>logical caller (parent)</summary>
    internal InlineContext? _parent;

    /// <summary>first child</summary>
    internal InlineContext? _child;

    /// <summary>next child of the parent</summary>
    internal InlineContext? _sibling;

    /// <summary>address of IL buffer for the method</summary>
    internal unsafe byte* _code;

    /// <summary>handle to the method</summary>
    internal unsafe CORINFO_METHOD_HANDLE _callee;

    /// <summary>handle to the exact context</summary>
    internal unsafe CORINFO_CONTEXT_HANDLE _runtimeContext;

    /// <summary>profile data</summary>
    private PgoInfo _pgoInfo;

    /// <summary>size of IL buffer for the method</summary>
    internal int _ilSize;

    /// <summary>estimated size of imported IL</summary>
    private int _importedILSize;

    /// <summary>inlining statement location within parent</summary>
    internal ILLocation _location;

    /// <summary>IL offset of actual call instruction leading to the inline</summary>
    internal IL_OFFSET _actualCallOffset;

    /// <summary>what lead to this inline success or failure</summary>
    private InlineObservation _observation;

    /// <summary>in bytes * 10</summary>
    private int _codeSizeEstimate;

    /// <summary>Ordinal number of this inline</summary>
    private int _ordinal;

    internal Flags _flags;

#if DEBUG
    /// <summary>policy that evaluated this inline</summary>
    internal InlinePolicy? _policy;

    /// <summary>ID of the GenTreeCall in the parent</summary>
    internal int _treeId;

    /// <summary>Set of offsets where instructions begin</summary>
    internal BitArray _ilInstsSet;
#endif

    internal InlineContext(InlineStrategy strategy)
    {
        _inlineStrategy = strategy;
        _actualCallOffset = BAD_IL_OFFSET;
        _observation = InlineObservation.CALLEE_UNUSED_INITIAL;
        _flags = Flags.Success;

#if DEBUG
        _ilInstsSet = new BitArray(0);
#endif
    }

    public IL_OFFSET ActualCallOffset => _actualCallOffset;

    /// <summary>Get callee handle</summary>
    public unsafe CORINFO_METHOD_HANDLE Callee => _callee;

    /// <summary>Get the first child context</summary>
    public InlineContext? Child => _child;

    /// <summary>Get the code pointer for this context.</summary>
    public unsafe byte* Code => _code;

    /// <summary>Get the native code size estimate for this inline.</summary>
    public int CodeSizeEstimate => _codeSizeEstimate;

    public unsafe bool HasPgoInfo => (_pgoInfo.PgoSchema is not null) && (_pgoInfo.PgoSchemaCount > 0) && (_pgoInfo.PgoData is not null);

#if DEBUG
    public BitArray ILInstsSet => _ilInstsSet;
#endif

    /// <summary>Get the IL code size for this inline.</summary>
    public int ILSize => _ilSize;

    public int ImportedILSize => _importedILSize;

#if DEBUG
    public bool IsAsyncCall => (_flags & Flags.AsyncCall) is not 0;

    public bool IsDevirtualized => (_flags & Flags.Devirtualized) is not 0;

    public bool IsGuarded => (_flags & Flags.Guarded) is not 0;
#endif

    public bool IsRoot => _parent is null;

    /// <summary>True if this context describes a successful inline.</summary>
    public bool IsSuccess => (_flags & Flags.Success) is not 0;

#if DEBUG
    public bool IsUnboxed => (_flags & Flags.Unboxed) is not 0;
#endif

    public ILLocation Location => _location;

    /// <summary>Get the observation that supported or disqualified this inline.</summary>
    public InlineObservation Observation => _observation;

    public int Ordinal => _ordinal;

    public InlineContext? Parent => _parent;

    public PgoInfo PgoInfo
    {
        get
        {
            return _pgoInfo;
        }

        set
        {
            _pgoInfo = value;
        }
    }

    /// <summary>Get the callee's exact context handle</summary>
    public unsafe CORINFO_CONTEXT_HANDLE RuntimeContext => _runtimeContext;

    /// <summary>Get the sibling context.</summary>
    public InlineContext? Sibling => _sibling;

#if DEBUG
    /// <summary>Dump an InlineContext entry and all descendants to jitstdout</summary>
    /// <param name="verbose">indentation level for this node</param>
    /// <param name="indent">more verbose output if true</param>
    public unsafe void Dump(bool verbose, int indent = 0)
    {
        // Handle fact that siblings are in reverse order.
        _sibling?.Dump(verbose, indent);

        // We may not know callee name in some of the failing cases
        var compiler = _inlineStrategy.Compiler;
        var calleeName = "";

        if (_callee is null)
        {
            assert(!IsSuccess);
            calleeName = "<unknown>";
        }
        else
        {

#if DEBUG
            calleeName = compiler.eeGetMethodFullName(_callee);
#else
            calleeName = "callee";
#endif
        }

        var calleeToken = compiler.info.compCompHnd->getMethodDefFromMethod(_callee);

        // Dump this node
        if (_parent is null)
        {
            // Root method
            var policy = InlinePolicy.GetPolicy(compiler, isPrejitRoot: true);

            if (verbose)
            {
                jitprintf($"\nInlines into {calleeToken:X8} [via {policy.Name}] {calleeName}:\n");
            }
            else
            {
                jitprintf($"\nInlines into {calleeName}:\n");
            }
        }
        else
        {
            // Inline attempt.
            var inlineTarget = _observation.TargetString;
            var inlineReason = _observation.String;
            var inlineResult = IsSuccess ? "INLINED: " : "FAILED: ";
            var devirtualized = IsDevirtualized ? " DEVIRT" : "";
            var guarded = IsGuarded ? " GUARDED" : "";
            var unboxed = IsUnboxed ? " UNBOXED" : "";
            var asyncness = compiler.compIsAsync ? (IsAsyncCall ? " ASYNC" : " SYNC") : "";

            var offs = _actualCallOffset;

            if (verbose)
            {
                if (offs == BAD_IL_OFFSET)
                {
                    jitprintf($"{new string(
' ',
 indent)}[{FMT_INL_CTX(_ordinal)} IL=???? TR={_treeId:D6} {calleeToken:X8}] [{inlineResult}{inlineTarget}: {inlineReason}{guarded}{devirtualized}{unboxed}{asyncness}] {calleeName}\n");
                }
                else
                {
                    jitprintf($"{new string(
' ',
 indent)}[{FMT_INL_CTX(_ordinal)} IL={offs:D4} TR={_treeId:D6} {calleeToken:X8}] [{inlineResult}{inlineTarget}: {inlineReason}{guarded}{devirtualized}{unboxed}{asyncness}] {calleeName}\n");
                }
            }
            else
            {
                jitprintf($"{new string(' ', indent)}[{inlineResult}{inlineReason}{guarded}{devirtualized}{unboxed}{asyncness}] {calleeName}\n");
            }
        }

        // Recurse to first child
        _child?.Dump(verbose, indent + 2);
    }
#endif

    public void SetFailed(InlineResult result)
    {
        assert(result.Observation.IsValid);
        _observation = result.Observation;
        _importedILSize = result.ImportedILSize;
        _flags &= ~Flags.Success;

#if DEBUG
        _policy = result.Policy;
        _codeSizeEstimate = _policy.CodeSizeEstimate();
#endif

        _inlineStrategy.NoteOutcome(this);
    }

    public void SetSucceeded(InlineInfo info)
    {
        var inlineResult = info.inlineResult;

        assert(inlineResult.Observation.IsValid);
        _observation = inlineResult.Observation;
        _importedILSize = inlineResult.ImportedILSize;
        _flags |= Flags.Success;

#if DEBUG
        _policy = inlineResult.Policy;
        _codeSizeEstimate = _policy.CodeSizeEstimate();
#endif

        _ordinal = _inlineStrategy.InlineCount + 1;
        _inlineStrategy.NoteOutcome(this);
    }

    internal enum Flags : byte
    {
        None = 0,

        /// <summary>true if this was a successful inline</summary>
        Success = 1 << 0,

#if DEBUG
        /// <summary>true if this was a devirtualized call</summary>
        Devirtualized = 1 << 1,

        /// <summary>true if this was a guarded call</summary>
        Guarded = 1 << 2,

        /// <summary>true if this call now invokes the unboxed entry</summary>
        Unboxed = 1 << 3,

        AsyncCall = 1 << 4,
#endif
    }
}
