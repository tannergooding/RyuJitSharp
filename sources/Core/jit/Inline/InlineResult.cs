// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed class InlineResult
{
    // Make an observation that must lead to immediate failure.
    public void NoteFatal(InlineObservation observation)
    {
        // TODO: Port NoteFatal
    }
}
