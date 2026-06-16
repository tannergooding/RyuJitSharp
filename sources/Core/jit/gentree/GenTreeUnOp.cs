// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;

namespace RyuJitSharp;

// In the current design, we never instantiate GenTreeUnOp: it exists only to be
// used as a base class.  For unary operators, we instantiate GenTreeOp, with a null second
// argument.  We check that this is true dynamically.  We could tighten this and get static
// checking, but that would entail accessing the first child of a unary operator via something
// like gtUnOp.gtOp1 instead of AsOp()->gtOp1.
public class GenTreeUnOp : GenTree
{
    private GenTree? _op1;

    internal GenTreeUnOp(genTreeOps oper, var_types type, GenTree? op1)
        : base(oper, type)
    {
        _op1 = op1;

        if (op1 is not null)
        {
            // Propagate effects flags from child.
            Flags |= (op1.Flags & GTF_ALL_EFFECT);
        }
        else
        {
#if DEBUG
            assert(IsNullOp1Legal);
#endif
        }
    }

#if DEBUG
    public bool IsNullOp1Legal => !Oper.IsSimple || Oper switch {
        GT_LEA => true,
        GT_RETFILT => true,
        GT_FIELD_ADDR => true,
        GT_RETURN => Type is TYP_VOID,
        _ => false,
    };
#endif

    public bool IsUnsigned
    {
        get
        {
            return ((Flags & GTF_UNSIGNED) != 0);
        }

        set
        {
#if TARGET_64BIT
            assert(Oper.IsCompare || Oper.IsMul || (Oper is GT_ADD or GT_SUB or GT_CAST));
#else
            assert(Oper.IsCompare || Oper.IsMul || (Oper is GT_ADD or GT_SUB or GT_CAST or GT_ADD_HI or GT_SUB_HI));
#endif

            Flags |= GTF_UNSIGNED;
        }
    }

    public GenTree Op1
    {
        get
        {
#if DEBUG
            assert(Debugger.IsAttached || (_op1 is not null) || !IsNullOp1Legal);
#endif
            return _op1!;
        }

        set
        {
            _op1 = value;
        }
    }

#nullable disable
    public ref GenTree Op1Ref => ref _op1;
#nullable restore
}
