// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

/// <summary>provides basic information about a particular inline candidate.</summary>
/// <remarks>Calls can start out as GDV candidates and turn into inline candidates</remarks>
public sealed class InlineCandidateInfo : HandleHistogramProfileCandidateInfo
{
    public unsafe CORINFO_CLASS_HANDLE guardedClassHandle;

    public unsafe CORINFO_METHOD_HANDLE guardedMethodHandle;

    public unsafe CORINFO_METHOD_HANDLE guardedMethodUnboxedEntryHandle;

    public CORINFO_LOOKUP guardedMethodInstParamLookup;

#if FEATURE_READYTORUN
    public CORINFO_RESOLVED_TOKEN guardedMethodResolvedToken;

    public CORINFO_RESOLVED_TOKEN guardedMethodUnboxedResolvedToken;
#endif

    public int likelihood;

    public bool needsMethodContext;

    public unsafe CORINFO_METHOD_INFO methInfo;

    /// <summary>the logical IL caller of this inlinee.</summary>
    public unsafe CORINFO_METHOD_HANDLE ilCallerHandle;

    public unsafe CORINFO_CLASS_HANDLE clsHandle;

    /// <summary>Context handle to use when inlining.</summary>
    public unsafe CORINFO_CONTEXT_HANDLE exactContextHandle;

    /// <summary>Method handle of the call before any GDV/Inlining evaluation</summary>
    public unsafe CORINFO_METHOD_HANDLE originalMethodHandle;

    /// <summary>The GT_RET_EXPR node linking back to the inline candidate.</summary>
    public GenTreeRetExpr? retExpr;

    public int preexistingSpillTemp;

    public CorInfoFlag clsAttr;

    public CorInfoFlag methAttr;

    public CorInfoInitClassResult initClassResult;

    public bool exactContextNeedsRuntimeLookup;

    public InlineContext? inlinersContext;
}
