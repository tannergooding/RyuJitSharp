// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public const StaticHelperReturnValue SHRV_STATIC_BASE_PTR = StaticHelperReturnValue.SHRV_STATIC_BASE_PTR;
    public const StaticHelperReturnValue SHRV_VOID = StaticHelperReturnValue.SHRV_VOID;

    public enum StaticHelperReturnValue
    {
        SHRV_STATIC_BASE_PTR,
        SHRV_VOID,
    }
}
