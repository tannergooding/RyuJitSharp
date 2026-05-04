// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed class InlineContext
{
    /// <summary>overall strategy</summary>
    private InlineStrategy m_InlineStrategy;

    /// <summary>logical caller (parent)</summary>
    private InlineContext? m_Parent;

    /// <summary>first child</summary>
    private InlineContext? m_Child;

    /// <summary>next child of the parent</summary>
    private InlineContext? m_Sibling;

    /// <summary>address of IL buffer for the method</summary>
    internal unsafe byte* m_Code;

    /// <summary>handle to the method</summary>
    internal unsafe CORINFO_METHOD_HANDLE m_Callee;

    /// <summary>handle to the exact context</summary>
    internal unsafe CORINFO_CONTEXT_HANDLE m_RuntimeContext;

    /// <summary>profile data</summary>
    private PgoInfo m_PgoInfo;

    /// <summary>size of IL buffer for the method</summary>
    internal uint m_ILSize;

    /// <summary>estimated size of imported IL</summary>
    private uint m_ImportedILSize;

    /// <summary>inlining statement location within parent</summary>
    private ILLocation m_Location;

    /// <summary>IL offset of actual call instruction leading to the inline</summary>
    private IL_OFFSET m_ActualCallOffset;

    /// <summary>what lead to this inline success or failure</summary>
    private InlineObservation m_Observation;

    /// <summary>in bytes * 10</summary>
    private int m_CodeSizeEstimate;

    /// <summary>Ordinal number of this inline</summary>
    private uint m_Ordinal;

    private Flags m_Flags;

#if DEBUG
    /// <summary>policy that evaluated this inline</summary>
    private InlinePolicy? m_Policy;

    /// <summary>ID of the GenTreeCall in the parent</summary>
    private uint m_TreeID;

    /// <summary>Set of offsets where instructions begin</summary>
    private unsafe void* m_ILInstsSet; // FixedBitVect
#endif

    internal InlineContext(InlineStrategy strategy)
    {
        m_InlineStrategy = strategy;
        m_ActualCallOffset = BAD_IL_OFFSET;
        m_Observation = InlineObservation.CALLEE_UNUSED_INITIAL;
        m_Flags = Flags.Success;
    }

    /// <summary>Get the native code size estimate for this inline.</summary>
    public uint CodeSizeEstimate => unchecked((uint)(m_CodeSizeEstimate));

    public unsafe bool HasPgoInfo => (m_PgoInfo.PgoSchema is not null) && (m_PgoInfo.PgoSchemaCount > 0) && (m_PgoInfo.PgoData is not null);

    /// <summary>Get the IL code size for this inline.</summary>
    public uint ILSize => m_ILSize;

    public uint ImportedILSize => m_ImportedILSize;

    public PgoInfo PgoInfo
    {
        get
        {
            return m_PgoInfo;
        }

        set
        {
            m_PgoInfo = value;
        }
    }

private enum Flags : byte
    {
        None = 0,

        /// <summary>true if this was a successful inline</summary>
        Success = 1 << 0,

#if DEBUG
        /// <summary>true if this was a devirtualized call</summary>
        Devirtualized = 1 << 1,

        /// <summary>true if this was a guarded call</summary>
        Guarded = 1 << 2,

        /// <summary>true if this call now invokes the unboxed entry</summary>
        Unboxed = 1 << 3,
#endif
    }
}
