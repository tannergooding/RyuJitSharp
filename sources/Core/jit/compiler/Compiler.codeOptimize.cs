// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public const codeOptimize BLENDED_CODE = codeOptimize.BLENDED_CODE;
    public const codeOptimize SMALL_CODE = codeOptimize.SMALL_CODE;
    public const codeOptimize FAST_CODE = codeOptimize.FAST_CODE;
    public const codeOptimize COUNT_OPT_CODE = codeOptimize.COUNT_OPT_CODE;

    public enum codeOptimize
    {
        BLENDED_CODE,
        SMALL_CODE,
        FAST_CODE,
        COUNT_OPT_CODE
    }
}
