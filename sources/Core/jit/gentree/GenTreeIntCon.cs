// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed class GenTreeIntCon : GenTreeIntConCommon
{
    // The InitializeArray intrinsic needs to go back to the newarray statement
    // to find the class handle of the array so that we can get its size.  However,
    // in AOT mode, the handle in that statement does not correspond to the compile
    // time handle (rather it lets you get a handle at run-time).  In that case, we also
    // need to store a compile time handle, which goes in this gtCompileTimeHandle field.
    private nint _compileTimeHandle;

    // TODO-Cleanup: It's not clear what characterizes the cases where the field
    // above is used.  It may be that its uses and those of the "gtFieldSeq" field below
    // are mutually exclusive, and they could be put in a union.  Or else we should separate
    // this type into three subtypes.

    // If this constant represents the offset of one or more fields, "gtFieldSeq" represents that sequence of fields.
    private readonly FieldSeq? _fieldSeq;

#if DEBUG
    // If the value represents target address (for a field or call), holds the handle of the field (or call).
    private nuint _targetHandle;
#endif

    public GenTreeIntCon(var_types type, nint value)
        : base(GT_CNS_INT, type)
    {
        _value.Icon = value;
    }

    public GenTreeIntCon(var_types type, nint value, FieldSeq fields)
        : base(GT_CNS_INT, type)
    {
        _value.Icon = value;
        _fieldSeq = fields;
    }

    public nint CompileTimeHandle
    {
        get
        {
            return _compileTimeHandle;
        }

        set
        {
            _compileTimeHandle = value;
        }
    }

    public FieldSeq? FieldSeq => _fieldSeq;

    public nint IconVal => _value.Icon;

#if DEBUG
    public nuint TargetHandle
    {
        get
        {
            return _targetHandle;
        }

        set
        {
            _targetHandle = value;
        }
    }
#endif
}
