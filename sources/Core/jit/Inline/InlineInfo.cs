// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;
using static RyuJitSharp.ICorDebugInfo;

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

    public uint argCnt;

    public inlArgInfoInlineArray inlArgInfo;

    public InlArgInfo[]? inlInstParamArgInfo;

    /// <summary>map local# -> temp# (-1 if unused)</summary>
    public lclTmpNumInlineArray lclTmpNum;

    /// <summary>type information from local sig</summary>
    public lclVarInfoInlineArray lclVarInfo;

    /// <summary>Number of TYP_REF and TYP_BYREF locals</summary>
    public uint numberOfGcRefLocals;

    public bool thisDereferencedFirst;

#if FEATURE_SIMD
    public bool hasSIMDTypeArgLocalOrReturn;
#endif

    /// <summary>The GT_CALL node to be inlined.</summary>
    public GenTreeCall? iciCall;

    /// <summary>The statement iciCall is in.</summary>
    public Statement? iciStmt;

    /// <summary>The basic block iciStmt is in.</summary>
    public BasicBlock? iciBlock;

    public bool HasGcRefLocals => numberOfGcRefLocals > 0;

    [InlineArray(MAX_INL_ARGS + 1)]
    public struct inlArgInfoInlineArray
    {
        public InlArgInfo e0;
    }

    [InlineArray(MAX_INL_LCLS)]
    public struct lclTmpNumInlineArray
    {
        public int e0;
    }

    [InlineArray(MAX_INL_LCLS + MAX_INL_ARGS + 1)]
    public struct lclVarInfoInlineArray
    {
        public InlLclVarInfo e0;
    }
}
