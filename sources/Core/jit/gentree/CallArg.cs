// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CallArg
{
    private GenTree _earlyNode;
    private GenTree? _lateNode;
    private CallArg? _next;
    private CallArg? _lateNext;

    // The class layout for the signature type (when varTypeIsStruct(SignatureType)).
    private ClassLayout? _signatureLayout;

    // The type of the argument in the signature.
    private var_types _signatureType;

    // The type of well-known argument this is.
    private WellKnownArg _wellKnownArg;

    private Flags _flags;

    private AbiPassingInformation _abiInfo;

    public CallArg(in NewCallArg arg)
    {
        _earlyNode = arg.Node;
        _wellKnownArg = arg.WellKnownArg;
        _signatureType = arg.SignatureType;
        _signatureLayout = arg.SignatureLayout;
    }

    public ref readonly AbiPassingInformation AbiInfo => ref _abiInfo;

    public GenTree EarlyNode
    {
        get
        {
            return _earlyNode;
        }

        set
        {
            _earlyNode = value;
        }
    }

#nullable disable
    public ref GenTree EarlyNodeRef => ref _earlyNode;
#nullable restore

    /// <summary>Check if this is an argument that is added late, by `DetermineArgAbiInformation`.</summary>
    /// <remarks>
    ///   <para>These arguments must be removed if ABI information needs to be reclassified by calling `DetermineArgAbiInformation` as otherwise they will be readded. See `CallArgs.ResetFinalArgsAndAbiInfo`.</para>
    ///   <para>Note that the 'late' here is separate from CallArg.GetLateNode and friends. Late here refers to this being an argument that is added by morph instead of the importer.</para>
    /// </remarks>
    public bool IsArgAddedLate => _wellKnownArg switch {
        WellKnownArg.WrapperDelegateCell => true,
        WellKnownArg.VirtualStubCell => true,
        WellKnownArg.PInvokeCookie => true,
        WellKnownArg.PInvokeTarget => true,
        WellKnownArg.R2RIndirectionCell => true,
        _ => false,
    };

    /// <summary>Check if this is an argument that can be treated as user-defined (in IL).</summary>
    /// <remarks>"this" and ShiftLow/ShiftHigh are recognized as user-defined</remarks>
    public bool IsUserArg => _wellKnownArg switch {
        WellKnownArg.None => true,
        WellKnownArg.ThisPointer => true,
        WellKnownArg.ShiftLow => true,
        WellKnownArg.ShiftHigh => true,
        _ => false,
    };

    public CallArg? LateNext
    {
        get
        {
            return _lateNext;
        }
        set
        {
            _lateNext = value;
        }
    }

#nullable disable
    public ref CallArg LateNextRef => ref _lateNext;
#nullable restore

    public GenTree? LateNode
    {
        get
        {
            return _lateNode;
        }

        set
        {
            _lateNode = value;
        }
    }

#nullable disable
    public ref GenTree LateNodeRef => ref _lateNode;
#nullable restore

    public CallArg? Next
    {
        get
        {
            return _next;
        }

        set
        {
            _next = value;
        }
    }

#nullable disable
    public ref CallArg NextRef => ref _next;
#nullable restore

    // Get the real argument node, i.e. not a setup or placeholder node.
    // This is the same as GetEarlyNode() until morph.
    // After lowering, this is a PUTARG_* node.
    public GenTree Node => (_lateNode is null) ? _earlyNode : _lateNode;

#nullable disable
    public ref GenTree NodeRef => ref ((_lateNode is null) ? ref _earlyNode : ref _lateNode);
#nullable restore

    public unsafe CORINFO_CLASS_HANDLE SignatureClassHandle => (_signatureLayout is not null) ? _signatureLayout.ClassHandle : NO_CLASS_HANDLE;

    public ClassLayout? SignatureLayout => _signatureLayout;

    public var_types SignatureType => _signatureType;

    public WellKnownArg WellKnownArg => _wellKnownArg;

    /// <summary>True when we must replace this argument with a placeholder node.</summary>
    internal bool NeedPlace
    {
        get
        {
            return (_flags & Flags.NeedPlace) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.NeedPlace) | (value ? Flags.NeedPlace : Flags.None);
        }
    }

    /// <summary>True when we force this argument's evaluation into a temp LclVar.</summary>
    internal bool NeedTmp
    {
        get
        {
            return (_flags & Flags.NeedTmp) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.NeedTmp) | (value ? Flags.NeedTmp : Flags.None);
        }
    }

    /// <summary>True when we have decided the evaluation order for this argument in LateArgs</summary>
    internal bool Processed

    {
        get
        {
            return (_flags & Flags.Processed) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.Processed) | (value ? Flags.Processed : Flags.None);
        }
    }

#if DEBUG
    /// <summary>Dump information about a CallArg to jitstdout.</summary>
    public void Dump()
    {
        jitprintf($"CallArg[[{Node.TreeId:D6}].{Node.Oper}");
        jitprintf($" {_signatureType.Name}");
        jitprintf($" ({(_abiInfo.IsPassedByReference ? "By ref" : "By value")})");
        jitprintf($", {_abiInfo.NumSegments} segments:");

        foreach (ref readonly var segment in _abiInfo.Segments)
        {
            jitprintf(" <");
            segment.Dump();
            jitprintf(">");
        }

        if (NeedPlace)
        {
            jitprintf(", needPlace");
        }

        if (Processed)
        {
            jitprintf(", processed");
        }

        if (_wellKnownArg is not WellKnownArg.None)
        {
            jitprintf($", wellKnown[{_wellKnownArg}]");
        }
        jitprintf("]\n");
    }
#endif
}
