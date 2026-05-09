// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics.CodeAnalysis;

namespace RyuJitSharp;

public sealed class GenTreeFieldAddr : GenTreeUnOp
{
    private readonly unsafe CORINFO_FIELD_HANDLE _fldHnd;
    private readonly int _fldOffset;
    private FieldAddrFlags _flags;

#if FEATURE_READYTORUN
    private CORINFO_CONST_LOOKUP _fieldLookup;
#endif

    public unsafe GenTreeFieldAddr(var_types type, GenTree obj, CORINFO_FIELD_HANDLE fldHnd, int offs)
        : base(GT_FIELD_ADDR, type, obj)
    {
        _fldHnd = fldHnd;
        _fldOffset = offs;
    }

    public unsafe CORINFO_FIELD_HANDLE FldHnd => _fldHnd;

    // The object this field belongs to. Will be "null" for static fields.
    // Note that this is an address, i. e. for struct fields it will be ADDR(STRUCT).
    public GenTree? FldObj => Op1;

    public int FldOffset => _fldOffset;

    [MemberNotNullWhen(true, nameof(FldObj))]
    public bool IsInstance => Op1 is not null;

#if FEATURE_READYTORUN
    public unsafe bool IsOffsetKnown => (_fieldLookup.addr is null);
#else
    public unsafe bool IsOffsetKnown => true;
#endif

    public bool IsSpanLength
    {
        get
        {
            // This is limited to span length today rather than a more general "IsNeverNegative"
            // to help avoid confusion around propagating the value to promoted lcl vars.
            //
            // Extending this support more in the future will require additional work and
            // considerations to help ensure it is correctly used since people may want
            // or intend to use this as more of a "point in time" feature like GTF_IND_NONNULL
            return (_flags & FieldAddrFlags.IsSpanLength) != 0;
        }

        set
        {
            _flags = (_flags & ~FieldAddrFlags.IsSpanLength) | (value ? FieldAddrFlags.IsSpanLength : FieldAddrFlags.None);
        }
    }

    public bool IsStatic => Op1 is null;

    public bool IsTlsStatic
    {
        get
        {
            assert(((Flags & GTF_FLD_TLS) == 0) || IsStatic);
            return (Flags & GTF_FLD_TLS) != 0;
        }
    }

    public bool MayOverlap => (_flags & FieldAddrFlags.MayOverlap) != 0;

    private enum FieldAddrFlags : byte
    {
        None = 0,
        MayOverlap = 1 << 0,
        IsSpanLength = 1 << 1,
    }
}
