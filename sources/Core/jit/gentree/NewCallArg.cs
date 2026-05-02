// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;

namespace RyuJitSharp;

public readonly struct NewCallArg
{
    private readonly GenTree _node;

    private readonly ClassLayout? _signatureLayout;

    private readonly var_types _signatureType;

    private readonly WellKnownArg _wellKnownArg;

    private NewCallArg(GenTree node, var_types signatureType)
    {
        _node = node;
        _signatureType = signatureType;
    }

    private NewCallArg(GenTree node, var_types signatureType, ClassLayout signatureLayout)
    {
        _node = node;
        _signatureLayout = signatureLayout;
        _signatureType = signatureType;
    }

    private NewCallArg(in NewCallArg value, WellKnownArg wellKnownArg)
    {
        _node = value._node;
        _signatureLayout = value._signatureLayout;
        _signatureType = value._signatureType;
        _wellKnownArg = wellKnownArg;
    }

    /// <summary>The node being passed.</summary>
    public GenTree Node => _node;

    /// <summary>The class layout if SignatureType.IsStruct.</summary>
    public ClassLayout? SignatureLayout => _signatureLayout;

    /// <summary>The signature type of the node.</summary>
    public var_types SignatureType => _signatureType;

    /// <summary>The type of well known arg.</summary>
    public WellKnownArg WellKnownArg => _wellKnownArg;

    public static NewCallArg CreateForPrimitive(GenTree node) => CreateForPrimitive(node, node.Type);

    public static NewCallArg CreateForPrimitive(GenTree node, var_types signatureType)
    {
        assert(!varTypeIsStruct(node.Type) && !varTypeIsStruct(signatureType));

        var arg = new NewCallArg(node, signatureType);
        arg.ValidateTypes();

        return arg;
    }

    public static NewCallArg CreateForStruct(GenTree node, var_types signatureType, ClassLayout signatureLayout)
    {
        assert(varTypeIsStruct(node.Type) && varTypeIsStruct(signatureType));

        var arg = new NewCallArg(node, signatureType, signatureLayout);
        arg.ValidateTypes();

        return arg;
    }

    [Conditional("DEBUG")]
    public void ValidateTypes()
    {
        assert(Compiler.impCheckImplicitArgumentCoercion(_signatureType, _node.Type));

        if (varTypeIsStruct(_signatureType))
        {
            assert(_signatureLayout is not null);
            assert(_signatureType == _node.Type);

            if ((_signatureType == TYP_STRUCT) && !_node.Oper.IsFieldList)
            {
                assert(JitTls.Compiler is not null);
                assert(ClassLayout.AreCompatible(_signatureLayout, _node.GetLayout(JitTls.Compiler)));
            }
        }
    }

    public readonly NewCallArg WithWellKnownArg(WellKnownArg wellKnownArg) => new NewCallArg(this, wellKnownArg);
}
