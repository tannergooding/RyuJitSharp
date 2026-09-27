// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
namespace RyuJitSharp;

public sealed partial class Target
{
    public const ArgOrder ARG_ORDER_R2L = ArgOrder.ARG_ORDER_R2L;
    public const ArgOrder ARG_ORDER_L2R = ArgOrder.ARG_ORDER_L2R;

    public enum ArgOrder
    {
        ARG_ORDER_R2L,
        ARG_ORDER_L2R
    }
}
#endif
