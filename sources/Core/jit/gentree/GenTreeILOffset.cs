// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

/// <summary>In LIR there are no longer statements so debug information is inserted linearly using these nodes.</summary>
public sealed class GenTreeILOffset : GenTree
{
    private DebugInfo _stmtDebugInfo;

#if DEBUG
    private IL_OFFSET _stmtLastILOffset;
#endif

    public GenTreeILOffset(in DebugInfo stmtDebugInfo, IL_OFFSET stmtLastILOffset = BAD_IL_OFFSET)
        : base(GT_IL_OFFSET, TYP_VOID)
    {
        _stmtDebugInfo = stmtDebugInfo;

#if DEBUG
        _stmtLastILOffset = stmtLastILOffset;
#endif
    }

    public ref readonly DebugInfo StmtDebugInfo => ref _stmtDebugInfo;

#if DEBUG
    public IL_OFFSET StmtLastILOffset => _stmtLastILOffset;
#endif
}
