// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Target
{
    public const string TgtCpuName = "x64";

    public const ArgOrder TgtArgOrder = ARG_ORDER_R2L;

    public const ArgOrder TgtUnmanagedArgOrder = ARG_ORDER_R2L;
}
