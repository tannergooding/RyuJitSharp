// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64 || TARGET_AMD64
namespace RyuJitSharp;

public sealed class GenTreeCCMP : GenTreeOpCC
{
    private readonly insCFlags _flagsVal;

    public GenTreeCCMP(var_types type, GenCondition condition, GenTree op1, GenTree op2, insCFlags flagsVal)
        : base(GT_CCMP, type, condition, op1, op2)
    {
        _flagsVal = flagsVal;
    }

    public insCFlags FlagsVal => _flagsVal;
}
#endif
