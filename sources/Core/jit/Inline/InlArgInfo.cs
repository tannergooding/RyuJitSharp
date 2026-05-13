// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct InlArgInfo
{
    /// <summary>the caller argument</summary>
    public CallArg arg;

    /// <summary>tmp node created, if it may be replaced with actual arg</summary>
    public GenTree? argBashTmpNode;

    /// <summary>the argument tmp number</summary>
    public int argTmpNum;

    private Flags _flags;

    /// <summary>is this arg used at all?</summary>
    public bool argIsUsed
    {
        readonly get
        {
            return (_flags & Flags.IsUsed) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.IsUsed) | (value ? Flags.IsUsed : Flags.None);
        }
    }

    /// <summary>the argument is a constant or a local variable address</summary>
    public bool argIsInvariant
    {
        readonly get
        {
            return (_flags & Flags.IsInvariant) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.IsInvariant) | (value ? Flags.IsInvariant : Flags.None);
        }
    }

    /// <summary>the argument is a local variable</summary>
    public bool argIsLclVar
    {
        readonly get
        {
            return (_flags & Flags.IsLclVar) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.IsLclVar) | (value ? Flags.IsLclVar : Flags.None);
        }
    }

    /// <summary>the argument is the 'this' pointer</summary>
    public bool argIsThis
    {
        readonly get
        {
            return (_flags & Flags.IsThis) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.IsThis) | (value ? Flags.IsThis : Flags.None);
        }
    }

    /// <summary>the argument has side effects</summary>
    public bool argHasSideEff
    {
        readonly get
        {
            return (_flags & Flags.HasSideEff) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.HasSideEff) | (value ? Flags.HasSideEff : Flags.None);
        }
    }

    /// <summary>the argument has a global ref</summary>
    public bool argHasGlobRef
    {
        readonly get
        {
            return (_flags & Flags.HasGlobRef) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.HasGlobRef) | (value ? Flags.HasGlobRef : Flags.None);
        }
    }

    /// <summary>the argument value depends on an aliased caller local</summary>
    public bool argHasCallerLocalRef
    {
        readonly get
        {
            return (_flags & Flags.HasCallerLocalRef) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.HasCallerLocalRef) | (value ? Flags.HasCallerLocalRef : Flags.None);
        }
    }

    /// <summary>the argument will be evaluated to a temp</summary>
    public bool argHasTmp
    {
        readonly get
        {
            return (_flags & Flags.HasTmp) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.HasTmp) | (value ? Flags.HasTmp : Flags.None);
        }
    }

    /// <summary>Is there LDARGA(s) operation on this argument?</summary>
    public bool argHasLdargaOp
    {
        readonly get
        {
            return (_flags & Flags.HasLdargaOp) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.HasLdargaOp) | (value ? Flags.HasLdargaOp : Flags.None);
        }
    }

    /// <summary>Is there STARG(s) operation on this argument?</summary>
    public bool argHasStargOp
    {
        readonly get
        {
            return (_flags & Flags.HasStargOp) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.HasStargOp) | (value ? Flags.HasStargOp : Flags.None);
        }
    }

    /// <summary>Is this arg an address of a struct local or a normed struct local or a field in them?</summary>
    public bool argIsByRefToStructLocal
    {
        readonly get
        {
            return (_flags & Flags.IsByRefToStructLocal) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.IsByRefToStructLocal) | (value ? Flags.IsByRefToStructLocal : Flags.None);
        }
    }

    /// <summary>Is this arg of an exact class?</summary>
    public bool argIsExact
    {
        readonly get
        {
            return (_flags & Flags.IsExact) != 0;
        }

        set
        {
            _flags = (_flags & ~Flags.IsExact) | (value ? Flags.IsExact : Flags.None);
        }
    }
}
