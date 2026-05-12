// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public enum ContinuationContextHandling
{
    /// <summary>No special handling of SynchronizationContext/TaskScheduler is required.</summary>
    None,

    /// <summary>Continue on SynchronizationContext/TaskScheduler</summary>
    ContinueOnCapturedContext,

    /// <summary>Continue on thread pool thread</summary>
    ContinueOnThreadPool,
}
