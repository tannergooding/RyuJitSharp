// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public unsafe partial class Globals
{
    public const int LL_EVERYTHING = 10;

    /// <summary>Can be expected to generate 1,000,000 logs per small but not trivial run.</summary>
    public const int LL_INFO1000000 = 9;

    /// <summary>Can be expected to generate 100,000 logs per small but not trivial run.</summary>
    public const int LL_INFO100000 = 8;

    /// <summary>Can be expected to generate 10,000 logs per small but not trivial run.</summary>
    public const int LL_INFO10000 = 7;

    /// <summary>Can be expected to generate 1,000 logs per small but not trivial run.</summary>
    public const int LL_INFO1000 = 6;

    /// <summary>Can be expected to generate 100 logs per small but not trivial run.</summary>
    public const int LL_INFO100 = 5;

    /// <summary>Can be expected to generate 10 logs per small but not trivial run.</summary>
    public const int LL_INFO10 = 4;

    public const int LL_WARNING = 3;

    public const int LL_ERROR = 2;

    public const int LL_FATALERROR = 1;

    /// <summary>Impossible to turn off (log level never negative).</summary>
    public const int LL_ALWAYS = 0;
}
