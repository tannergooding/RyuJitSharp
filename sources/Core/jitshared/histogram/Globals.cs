// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;

namespace RyuJitSharp;

public partial class Globals
{
    /// <summary>Maximum number of buckets in a histogram (including overflow bucket)</summary>
    public const int HISTOGRAM_MAX_SIZE_COUNT = 64;
}
