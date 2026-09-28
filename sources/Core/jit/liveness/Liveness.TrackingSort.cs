// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Liveness<TLiveness>
    where TLiveness : ILivenessPolicy
{
    internal static void SortTrackedCandidates<TLess>(Span<int> values, TLess less)
        where TLess : struct, ILocalLess
    {
        SortNative(values, less);
    }
}
