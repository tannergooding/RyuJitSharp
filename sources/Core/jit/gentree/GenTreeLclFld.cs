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
        assert(oper is GT_LCL_FLD or GT_LCL_ADDR);
        _lclOffs = lclOffs;
        _layout = layout;
    }

    public GenTreeLclFld(var_types type, int lclNum, ushort lclOffs, GenTree data, ClassLayout? layout)
        : base(GT_STORE_LCL_FLD, type, lclNum, data)
    {
        _lclOffs = lclOffs;
        _layout = layout;
    }

#if TARGET_ARM
    /// <summary>check if the field needs a special handling on arm.</summary>
    public bool IsOffsetMisaligned => varTypeIsFloating(Type) && ((_lclOffs % TYP_FLOAT.EmitSize) is not 0);
#endif

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

    public int Size => ValueSize.ExactSize;

    public ValueSize ValueSize
    {
        get
        {
            if (Type is TYP_STRUCT)
            {
                assert(_layout is not null);
                return new ValueSize(_layout.Size);
            }
            else
            {
                return ValueSize.FromJitType(Type);
            }
        }
    }

    /// <summary>Check for a GT_LCL_FLD whose type is a different size than the lclVar.</summary>
    /// <param name="compiler">the Compiler object.</param>
    /// <returns>Returns "true" iff 'this' is a GT_LCL_FLD or GT_STORE_LCL_FLD on which the type is not the same size as the type of the GT_LCL_VAR</returns>
    public bool IsPartial(Compiler compiler)
    {
        return (Oper is GT_LCL_FLD or GT_STORE_LCL_FLD)
            && (compiler.lvaGetDesc(LclNum).lvValueSize != ValueSize);
    }
}
