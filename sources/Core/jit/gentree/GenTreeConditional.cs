// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed class GenTreeConditional : GenTreeOp
{
    private GenTree _cond;

    public GenTreeConditional(genTreeOps oper, var_types type, GenTree cond, GenTree op1, GenTree op2)
        : base(EnsureArm64SelectSupported(oper), type, op1, op2)
    {
        assert(oper.IsConditional);
        _cond = cond;
    }

    private static genTreeOps EnsureArm64SelectSupported(genTreeOps oper)
    {
        if ((oper is GT_SELECT_INC or GT_SELECT_INV or GT_SELECT_NEG) && !Configuration.TARGET_ARM64)
        {
            throw new FatalJitException(CORJIT_INTERNALERROR, "ARM64 conditional-select nodes are only supported on ARM64.");
        }

        return oper;
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
