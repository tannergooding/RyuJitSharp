// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public enum InlineImpact
{
    /// <summary>inlining impossible, unsafe to evaluate further</summary>
    FATAL,

    /// <summary>inlining impossible for fundamental reasons, deeper exploration safe</summary>
    FUNDAMENTAL,

    /// <summary>inlining impossible because of jit limitations, deeper exploration safe</summary>
    LIMITATION,

    /// <summary>inlining inadvisable because of performance concerns</summary>
    PERFORMANCE,

    /// <summary>policy-free observation to provide data for later decision making</summary>
    INFORMATION,
}
