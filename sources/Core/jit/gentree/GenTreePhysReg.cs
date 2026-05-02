// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

/// <summary>node representing a read from a physical register</summary>
public sealed class GenTreePhysReg : GenTree
{
    // physregs need a field beyond GetRegNum() because
    // GetRegNum() indicates the destination (and can be changed)
    // whereas reg indicates the source
    private readonly regNumber _srcReg;

    public GenTreePhysReg(regNumber srcReg, var_types type = TYP_I_IMPL)
        : base(GT_PHYSREG, type)
    {
        _srcReg = srcReg;
    }

    public regNumber SrcReg => _srcReg;
}
