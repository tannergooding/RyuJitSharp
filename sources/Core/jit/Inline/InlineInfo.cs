// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

/// <summary>Provides detailed information about a particular inline candidate.</summary>
public sealed class InlineInfo
{
    /// <summary>The Compiler instance for the caller (i.e. the inliner)</summary>
    public Compiler? InlinerCompiler;

    /// <summary>The Compiler instance that is the root of the inlining tree of which the owner of "this" is a member.</summary>
    public Compiler? InlineRoot;

    public unsafe CORINFO_METHOD_HANDLE fncHandle;

    public unsafe InlineCandidateInfo? inlineCandidateInfo;

    public unsafe InlineContext? inlineContext;

    public unsafe InlineResult? inlineResult;
}
