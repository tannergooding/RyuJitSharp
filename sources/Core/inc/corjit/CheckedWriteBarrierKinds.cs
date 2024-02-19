// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.CheckedWriteBarrierKinds;

namespace RyuJitSharp;

// We have a performance-investigation mode (defined by the FEATURE_USE_ASM_GC_WRITE_BARRIERS and
// FEATURE_COUNT_GC_WRITE_BARRIER preprocessor symbols) in which the JIT adds an argument of this
// enumeration to checked write barrier calls in order to classify them.
public enum CheckedWriteBarrierKinds
{
    /// <summary>Not one of the ones below.</summary>
    CWBKind_Unclassified,

    /// <summary>Store through a return buffer pointer argument.</summary>
    CWBKind_RetBuf,

    /// <summary>Store through a by-ref argument (not an implicit return buffer).</summary>
    CWBKind_ByRefArg,

    /// <summary>Store through a by-ref local variable.</summary>
    CWBKind_OtherByRefLocal,

    /// <summary>Store through the address of a local (arguably a bug that this happens at all).</summary>
    CWBKind_AddrOfLocal,
}
