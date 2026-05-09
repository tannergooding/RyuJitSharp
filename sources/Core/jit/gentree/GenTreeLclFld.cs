// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;

namespace RyuJitSharp;

public sealed class GenTreeLclFld : GenTreeLclVarCommon
{
    private ushort _lclOffs;

    // The struct layout for this local field.
    private ClassLayout? _layout;

    public GenTreeLclFld(genTreeOps oper, var_types type, int lclNum, ushort lclOffs, ClassLayout? layout = null)
        : base(oper, type, lclNum)
    {
        _lclOffs = lclOffs;
        _layout = layout;
    }

    public GenTreeLclFld(var_types type, int lclNum, ushort lclOffs, GenTree data, ClassLayout layout)
        : base(GT_STORE_LCL_FLD, type, lclNum, data)
    {
        _lclOffs = lclOffs;
        _layout = layout;
    }

    /// <summary>offset into the variable to access</summary>
    public ushort LclOffs
    {
        get
        {
            return _lclOffs;
        }

        set
        {
            _lclOffs = value;
        }
    }

    public ClassLayout? Layout
    {
        get
        {
            assert(Debugger.IsAttached || (Type is not TYP_STRUCT) || (_layout is not null));
            return _layout;
        }
        set
        {
            _layout = value;
        }
    }
}
