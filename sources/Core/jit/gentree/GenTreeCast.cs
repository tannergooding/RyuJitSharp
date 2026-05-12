// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed class GenTreeCast : GenTreeOp
{
    private readonly var_types _castType;

    public GenTreeCast(var_types type, GenTree op, bool fromUnsigned, var_types castType)
        : base(GT_CAST, type, op, null)
    {
        _castType = castType;

        // We do not allow casts from floating point types to be treated as from
        // unsigned to avoid bugs related to wrong GTF_UNSIGNED in case the
        // CastOp's type changes.
        assert(!varTypeIsFloating(op.Type) || !fromUnsigned);

        Flags |= (fromUnsigned ? GTF_UNSIGNED : GTF_EMPTY);
    }

    public GenTree CastOp => Op1;

    public var_types CastType => _castType;
}
