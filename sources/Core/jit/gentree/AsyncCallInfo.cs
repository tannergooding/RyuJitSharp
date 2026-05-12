// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public struct AsyncCallInfo
{
    /// <summary>DebugInfo with SOURCE_TYPE_ASYNC pointing at the await call IL instruction</summary>
    public DebugInfo CallAsyncDebugInfo;

    /// <summary>Behavior where we continue for each call depends on how it was configured and whether it is a task await or custom await. This field records that behavior.</summary>
    public ContinuationContextHandling ContinuationContextHandling;

    /// <summary>Tail awaits do not generate suspension points and the JIT instead directly returns the callee's continuation to the caller.</summary>
    public bool IsTailAwait;

    public readonly bool NeedsToSaveAndRestoreExecutionContext => true;
}
