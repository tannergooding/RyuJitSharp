// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

// In the current design, we never instantiate GenTreeUnOp: it exists only to be
// used as a base class.  For unary operators, we instantiate GenTreeOp, with a NULL second
// argument.  We check that this is true dynamically.  We could tighten this and get static
// checking, but that would entail accessing the first child of a unary operator via something
// like gtUnOp.gtOp1 instead of AsOp()->gtOp1.
public abstract class GenTreeUnOp : GenTree
{
    private GenTree? _op1;

    protected GenTreeUnOp(genTreeOps oper, var_types type)
        : base(oper, type)
    {
    }

    protected GenTreeUnOp(genTreeOps oper, var_types type, GenTree? op1)
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
            assert(IsNullOp1Legal);
        }
    }

#if DEBUG
    public bool IsNullOp1Legal => Oper switch {
        GT_LEA => true,
        GT_RETFILT => true,
        GT_FIELD_ADDR => true,
        GT_RETURN => Type is TYP_VOID,
        _ => false,
    };
#endif

    public GenTree? Op1
    {
        get
        {
            return _op1;
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
