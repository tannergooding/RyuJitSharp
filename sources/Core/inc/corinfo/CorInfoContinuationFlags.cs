// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.CorInfoContinuationFlags;
using System;

namespace RyuJitSharp;

// Keep in sync with ContinuationFlags enum in BCL sources
[Flags]
public enum CorInfoContinuationFlags
{
    /// <summary>If this bit is set the continuation should continue on the thread pool.</summary>
    CORINFO_CONTINUATION_CONTINUE_ON_THREAD_POOL = 1 << 0,

    /// <summary>If this bit is set the continuation context is a SynchronizationContext that we should continue on.</summary>
    CORINFO_CONTINUATION_CONTINUE_ON_CAPTURED_SYNCHRONIZATION_CONTEXT = 1 << 1,

    /// <summary>If this bit is set the continuation context is a TaskScheduler that we should continue on.</summary>
    CORINFO_CONTINUATION_CONTINUE_ON_CAPTURED_TASK_SCHEDULER = 1 << 2,

    // The flags encode where in the continuation various members are stored.
    // If the encoded index == 0, it means no such member is present.
    // Otherwise the exact offset of the member is computed as
    //   OFFSETOF__CORINFO_Continuation__data + (index - 1) * PointerSize
    //
    CORINFO_CONTINUATION_EXCEPTION_INDEX_FIRST_BIT = 3,

    CORINFO_CONTINUATION_EXCEPTION_INDEX_NUM_BITS = 2,

    CORINFO_CONTINUATION_CONTEXT_INDEX_FIRST_BIT = 5,

    CORINFO_CONTINUATION_CONTEXT_INDEX_NUM_BITS = 2,

    // For JIT, the continuation stores space for every possible type of
    // async callee's result. We need to represent the offset to each of
    // these, so we allocate the rest of the bits for this.
    CORINFO_CONTINUATION_RESULT_INDEX_FIRST_BIT = 7,

    CORINFO_CONTINUATION_RESULT_INDEX_NUM_BITS = 25,
}
