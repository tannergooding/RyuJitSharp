// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public class GenTreeOp : GenTreeUnOp
{
    private GenTree? _op2;

    public GenTreeOp(genTreeOps oper, var_types type, GenTree? op1, GenTree? op2)
        : base(oper, type, op1)
    {
        _op2 = op2;

        // comparisons are always integral types
        assert(!oper.IsCompare || varTypeIsIntegral(type));

        if (op2 is not null)
        {
            // Unary operators, on the other hand, require a null second argument.
            assert(!oper.IsUnary);

            // Propagate effects flags from child.  (UnOp handled this for first child.)
            Flags |= (op2.Flags & GTF_ALL_EFFECT);
        }
        else
        {
            // Binary operators, with a few exceptions, require a non-null second argument.
            assert(IsNullOp2Legal);
        }
    }

    // A small set of types are unary operators with optional arguments.
    // We use this constructor to build those.
    public GenTreeOp(genTreeOps oper, var_types type)
        : base(oper, type)
    {
        // Unary operators with optional arguments:
        assert((oper is GT_RETURN or GT_RETFILT) || oper.IsBlk);
    }

#if DEBUG
    public bool IsNullOp2Legal => !Oper.IsBinary || Oper switch {
        GT_INTRINSIC => true,
        GT_LEA => true,

#if TARGET_ARM64
        GT_SELECT_NEGCC => true,
        GT_SELECT_INCCC => true,
#endif

#if SWIFT_SUPPORT
        GT_SWIFT_ERROR_RET => Type is TYP_VOID,
#endif

        _ => false,
    };
#endif

    public GenTree? Op2
    {
        get
        {
            return _op2;
        }

        set
        {
            _op2 = value;
        }
    }

#nullable disable
    public ref GenTree Op2Ref => ref _op2;
#nullable restore
}
