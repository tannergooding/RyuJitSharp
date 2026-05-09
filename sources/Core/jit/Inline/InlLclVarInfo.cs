// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

/// <summary>describes inline candidate argument and local variable properties.</summary>
public partial struct InlLclVarInfo
{
    /// <summary>Type handle from the signature. Available for structs and REFs.</summary>
    public unsafe CORINFO_CLASS_HANDLE lclTypeHandle;

    /// <summary>Type from the signature.</summary>
    public var_types lclTypeInfo;

    private Flags _flags;

    /// <summary>Is there LDLOCA(s) operation on this local?</summary>
    public bool lclHasLdlocaOp
    {
        readonly get
        {
            return (_flags & Flags.HasLdlocaOp) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.HasLdlocaOp) | (value ? Flags.HasLdlocaOp : Flags.None);
        }
    }

    /// <summary>Is there a STLOC on this local?</summary>
    public bool lclHasStlocOp
    {
        readonly get
        {
            return (_flags & Flags.HasStlocOp) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.HasStlocOp) | (value ? Flags.HasStlocOp : Flags.None);
        }
    }

    /// <summary>Is there more than one STLOC on this local</summary>
    public bool lclHasMultipleStlocOp
    {
        readonly get
        {
            return (_flags & Flags.HasMultipleStlocOp) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.HasMultipleStlocOp) | (value ? Flags.HasMultipleStlocOp : Flags.None);
        }
    }

    public bool lclIsPinned
    {
        readonly get
        {
            return (_flags & Flags.IsPinned) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.IsPinned) | (value ? Flags.IsPinned : Flags.None);
        }
    }
}
