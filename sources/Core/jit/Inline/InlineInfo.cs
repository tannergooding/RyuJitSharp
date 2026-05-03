// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

/// <summary>Provides detailed information about a particular inline candidate.</summary>
public sealed class InlineInfo
{
    /// <summary>The Compiler instance for the caller (i.e. the inliner)</summary>
    public Compiler InlinerCompiler = null!;

    /// <summary>The Compiler instance that is the root of the inlining tree of which the owner of "this" is a member.</summary>
    public Compiler InlineRoot = null!;

    public unsafe CORINFO_METHOD_HANDLE fncHandle;

    public unsafe InlineCandidateInfo inlineCandidateInfo = null!;

    public unsafe InlineContext inlineContext = null!;

    public unsafe InlineResult inlineResult = null!;

    public unsafe CORINFO_CLASS_HANDLE retExprClassHnd;

    public bool retExprClassHndIsExact;

    /// <summary>The context handle that will be passed to impTokenLookupContextHandle in Inlinee's Compiler.</summary>
    public unsafe CORINFO_CONTEXT_HANDLE tokenLookupContextHandle;
}
