// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed class GenTreeBox : GenTreeUnOp
{
    private readonly Statement _defStmtWhenInlinedBoxValue;
    private readonly Statement _copyStmtWhenInlinedBoxValue;

    public GenTreeBox(var_types type, GenTree boxOp, Statement defStmtWhenInlinedBoxValue, Statement copyStmtWhenInlinedBoxValue)
        : base(GT_BOX, type, boxOp)
    {
        _defStmtWhenInlinedBoxValue = defStmtWhenInlinedBoxValue;
        _copyStmtWhenInlinedBoxValue = copyStmtWhenInlinedBoxValue;
    }

    /// <summary>This is the statement that contains the definition tree when the node is an inlined GT_BOX on a value type</summary>
    public Statement DefStmtWhenInlinedBoxValue => _defStmtWhenInlinedBoxValue;

    /// <summary>This is the statement that copies from the value being boxed to the box payload</summary>
    public Statement CopyStmtWhenInlinedBoxValue => _copyStmtWhenInlinedBoxValue;
}
