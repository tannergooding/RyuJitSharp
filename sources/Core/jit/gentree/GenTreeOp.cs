// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;

namespace RyuJitSharp;

public class GenTreeOp : GenTreeUnOp
{
    private GenTree? _op2;

    internal GenTreeOp(genTreeOps oper, var_types type, GenTree? op1, GenTree? op2)
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
#if DEBUG
            // Binary operators, with a few exceptions, require a non-null second argument.
            assert(IsNullOp2Legal);
#endif
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

    public GenTree Op2
    {
        get
        {
#if DEBUG
            assert(Debugger.IsAttached || (_op2 is not null) || !IsNullOp2Legal);
#endif
            return _op2!;
        }

        set
        {
            _op2 = value;
        }
    }

#nullable disable
    public ref GenTree Op2Ref => ref _op2;
#nullable restore

    /// <summary>returns true if the given tree is known to possibly overflow on a division.</summary>
    /// <param name="comp">Compiler object, needed for IsNeverNegativeOne</param>
    /// <returns>true if the given tree is known to possibly overflow on a division</returns>
    /// <remarks>
    ///   <para>Only valid for integral types.</para>
    ///   <para>Only valid for signed-div/signed-mod.</para>
    /// </remarks>
    public bool CanDivOrModPossiblyOverflow(Compiler comp)
    {
        assert(Oper is GT_DIV or GT_MOD);
        assert(varTypeIsIntegral(Type));

        if ((Flags & GTF_DIV_MOD_NO_OVERFLOW) != 0)
        {
            return false;
        }

        var op1 = Op1.SkipCopyOrReload;
        var op2 = Op2.SkipCopyOrReload;

        // If the divisor is known to never be '-1', we cannot overflow.
        if (op2.IsNeverNegativeOne(comp))
        {
            return false;
        }

        // If the dividend is a constant with a minimum value with respect to the division's type, then we might overflow
        // as we do not know if the divisor will be '-1' or not at this point.
        if (op1.Oper.IsIntegralConst)
        {
            var intConCommon = op1.AsIntConCommon();

            if ((Type is TYP_INT) && intConCommon.IsIntegralConst(int.MinValue))
            {
                return true;
            }
            else if ((Type is TYP_LONG) && (intConCommon.IntegralValue == long.MinValue))
            {
                return true;
            }

            // Dividend is not a minimum value; therefore we cannot overflow.
            return false;
        }

        // Not enough known information; therefore we might overflow.
        return true;
    }
}
