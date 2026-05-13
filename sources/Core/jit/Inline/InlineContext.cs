// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections;

namespace RyuJitSharp;

public sealed class InlineContext
{
    /// <summary>overall strategy</summary>
    private InlineStrategy _inlineStrategy;

    /// <summary>logical caller (parent)</summary>
    private InlineContext? _parent;

    /// <summary>first child</summary>
    private InlineContext? _child;

    /// <summary>next child of the parent</summary>
    private InlineContext? _sibling;

    /// <summary>address of IL buffer for the method</summary>
    internal unsafe byte* _code;

    /// <summary>handle to the method</summary>
    internal unsafe CORINFO_METHOD_HANDLE _callee;

    /// <summary>handle to the exact context</summary>
    internal unsafe CORINFO_CONTEXT_HANDLE _runtimeContext;

    /// <summary>profile data</summary>
    private PgoInfo _pgoInfo;

    /// <summary>size of IL buffer for the method</summary>
    internal int _ilSize;

    /// <summary>estimated size of imported IL</summary>
    private int _importedILSize;

    /// <summary>inlining statement location within parent</summary>
    private ILLocation _location;

    /// <summary>IL offset of actual call instruction leading to the inline</summary>
    private IL_OFFSET _actualCallOffset;

    /// <summary>what lead to this inline success or failure</summary>
    private InlineObservation _observation;

    /// <summary>in bytes * 10</summary>
    private int _codeSizeEstimate;

    /// <summary>Ordinal number of this inline</summary>
    private int _ordinal;

    private Flags _flags;

#if DEBUG
    /// <summary>policy that evaluated this inline</summary>
    private InlinePolicy? _policy;

    /// <summary>ID of the GenTreeCall in the parent</summary>
    private int _treeId;

    /// <summary>Set of offsets where instructions begin</summary>
    internal BitArray _ilInstsSet;
#endif

    internal InlineContext(InlineStrategy strategy)
    {
        _inlineStrategy = strategy;
        _actualCallOffset = BAD_IL_OFFSET;
        _observation = InlineObservation.CALLEE_UNUSED_INITIAL;
        _flags = Flags.Success;

#if DEBUG
        _ilInstsSet = new BitArray(0);
#endif
    }

    /// <summary>Get the native code size estimate for this inline.</summary>
    public int CodeSizeEstimate => _codeSizeEstimate;

    public unsafe bool HasPgoInfo => (_pgoInfo.PgoSchema is not null) && (_pgoInfo.PgoSchemaCount > 0) && (_pgoInfo.PgoData is not null);

#if DEBUG
    public BitArray ILInstsSet => _ilInstsSet;
#endif

    /// <summary>Get the IL code size for this inline.</summary>
    public int ILSize => _ilSize;

    public int ImportedILSize => _importedILSize;

    public bool IsRoot => _parent is null;

    public ILLocation Location => _location;

    public int Ordinal => _ordinal;

    public InlineContext? Parent => _parent;

    public PgoInfo PgoInfo
    {
        get
        {
            return _pgoInfo;
        }

        set
        {
            _pgoInfo = value;
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
