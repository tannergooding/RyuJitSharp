// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Globals
{
    /// <summary>does not include obj pointer</summary>
    public const int MAX_INL_ARGS = 32;

    public const int MAX_INL_LCLS = 32;

    public static string FMT_INL_CTX(int ordinal) => $"INL{ordinal:D2}";
}
