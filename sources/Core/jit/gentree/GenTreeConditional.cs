// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed class GenTreeConditional : GenTreeOp
{
    private GenTree _cond;

    public GenTreeConditional(genTreeOps oper, var_types type, GenTree cond, GenTree op1, GenTree op2)
        : base(oper, type, op1, op2)
    {
        assert(oper.IsConditional);
         _cond = cond;
    }

    public GenTree Cond => _cond;

    public static bool Equals(GenTreeConditional op1, GenTreeConditional op2)
    {
        return Compare(op1._cond, op2._cond) && Compare(op1.Op1, op2.Op1) && Compare(op1.Op2, op2.Op2);
    }

#nullable disable
    public ref GenTree CondRef => ref _cond;
#nullable restore
}
