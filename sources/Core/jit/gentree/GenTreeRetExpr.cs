// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

/// <summary>Place holder for the return expression from an inline candidate (GT_RET_EXPR)</summary>
public sealed class GenTreeRetExpr : GenTree
{
    private GenTree _inlineCandidate;

    private GenTree? _substExpr;

    private BasicBlock? _substBB;

    public GenTreeRetExpr(var_types type, GenTreeCall inlineCandidate)
        : base(GT_RET_EXPR, type)
    {
        _inlineCandidate = inlineCandidate;
    }

    public GenTree InlineCandidate
    {
        get
        {
            return _inlineCandidate;
        }

        set
        {
            _inlineCandidate = value;
        }
    }

    /// <summary>Expression representing InlineCandidate's value (e.g. spill temp or expression from inlinee, or call itself for unsuccessful inlines).</summary>
    /// <remarks>
    ///   <para>Substituted by UpdateInlineReturnExpressionPlaceHolder.</para>
    ///   <para>This tree is null during the import that created the GenTreeRetExpr and is set later when handling the actual inline candidate.</para>
    /// </remarks>
    public GenTree? SubstExpr
    {
        get
        {
            return _substExpr;
        }

        set
        {
            _substExpr = value;
        }
    }

    /// <summary>The basic block that SubstExpr comes from, to enable propagating mandatory flags. null for cases where SubstExpr is not a tree from the inlinee.</summary>
    public BasicBlock? SubstBB
    {
        get
        {
            return _substBB;
        }

        set
        {
            _substBB = value;
        }
    }
}
