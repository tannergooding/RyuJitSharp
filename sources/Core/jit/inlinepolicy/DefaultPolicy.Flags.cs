// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class DefaultPolicy
{
    [Flags]
    private enum Flags : ushort
    {
        None = 0,
        IsForceInline = 1 << 0,
        IsForceInlineKnown = 1 << 1,
        IsInstanceCtor = 1 << 2,
        IsFromPromotableValueClass = 1 << 3,
        HasSimd = 1 << 4,
        LooksLikeWrapperMethod = 1 << 5,
        MethodIsMostlyLoadStore = 1 << 6,
        CallsiteIsInTryRegion = 1 << 7,
        CallsiteIsInLoop = 1 << 8,
        IsNoReturn = 1 << 9,
        IsNoReturnKnown = 1 << 10,
        ConstArgFeedsIsKnownConst = 1 << 11,
        ArgFeedsIsKnownConst = 1 << 12,
        InsideThrowBlock = 1 << 13,
        IsIntrinsicType = 1 << 14,
    }
}
