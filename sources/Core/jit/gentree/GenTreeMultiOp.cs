// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

/// <summary>A node with a flexible count of operands stored in an array.</summary>
public abstract class GenTreeMultiOp : GenTree
{
    private readonly GenTree[] _operands;

    // template<unsigned InlineOperandCount, typename...Operands>
    protected GenTreeMultiOp(genTreeOps oper, var_types type, params ReadOnlySpan<GenTree> operands)
        : base(oper, type)
    {
        _operands = operands.ToArray();

        foreach (var operand in operands)
        {
            assert(operand is not null);
            Flags |= (operand.Flags & GTF_ALL_EFFECT);
        }
    }

#if FEATURE_HW_INTRINSICS
    public bool IsUserCall => Oper.IsHWIntrinsic && ((Flags & GTF_HW_USER_CALL) is not 0);
#else
    public bool IsUserCall => false;
#endif

    public Span<GenTree> Operands => _operands;

    public GenTree GetOp(int index) => _operands[index - 1];

#nullable disable
    public ref GenTree GetOpRef(int index) => ref _operands[index - 1];
#nullable enable

    public void SetOp(int index, GenTree value)
    {
        _operands[index - 1] = value;
    }
}
