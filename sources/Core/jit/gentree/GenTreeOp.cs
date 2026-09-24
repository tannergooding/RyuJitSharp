// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
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

    internal GenTreeOp(genTreeOps oper, var_types type, GenTree? op1, GenTree? op2, GenTree source, NodeThreading threading)
        : base(oper, type, op1, source, threading)
    {
        _op2 = op2;
        assert(!oper.IsCompare || varTypeIsIntegral(type));
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
            assert(Debugger.IsAttached || (_op2 is not null) || IsNullOp2Legal);
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

    public bool UsesDivideByConstOptimized(Compiler compiler)
    {
        if (!compiler.opts.OptimizationEnabled || (Oper is not GT_DIV and not GT_MOD and not GT_UDIV and not GT_UMOD))
        {
            return false;
        }

#if TARGET_ARM64
        if (Oper is GT_MOD or GT_UMOD)
        {
            return false;
        }
#endif
        var isSigned = Oper is GT_DIV or GT_MOD;
        var dividend = Op1.EffectiveVal;
        var divisor = Op2.EffectiveVal;
#if !TARGET_64BIT
        if (dividend.Oper is GT_LONG)
        {
            return false;
        }
#endif
        // Constant operands should fold earlier; remaining pairs may throw.
        if (dividend.Oper.IsCnsIntOrI)
        {
            return false;
        }

        nint divisorValue;
        if (divisor.Oper.IsCnsIntOrI)
        {
            divisorValue = divisor.AsIntCon().IconValue;
        }
        else if ((compiler.vnStore is not null) && compiler.vnStore.IsVNConstant(divisor._vnPair.Liberal))
        {
            divisorValue = compiler.vnStore.CoercedConstantValue<nint>(divisor._vnPair.Liberal);
        }
        else
        {
            return false;
        }

        if (divisorValue == 0)
        {
            return false;
        }
        else if (isSigned)
        {
            // Preserve the minimum-value / -1 overflow exception.
            if (divisorValue == -1)
            {
                return false;
            }

            var magnitude = unchecked((nuint)(divisorValue >= 0 ? divisorValue : -divisorValue));
            if (nuint.IsPow2(magnitude))
            {
                return true;
            }
        }
        else
        {
            // Int constants are sign-extended in native-sized storage.
            if (Type is TYP_INT)
            {
                divisorValue &= unchecked((nint)uint.MaxValue);
            }

            if (nuint.IsPow2(unchecked((nuint)divisorValue)))
            {
                return true;
            }
        }

        if (Oper is GT_DIV or GT_UDIV)
        {
            if (isSigned)
            {
                if (((Type is TYP_INT) && (divisorValue == int.MinValue))
#if TARGET_64BIT
                    || ((Type is TYP_LONG) && (divisorValue == long.MinValue))
#endif
                )
                {
                    return true;
                }
            }
            else if (((Type is TYP_INT) && (unchecked((uint)divisorValue) > (uint.MaxValue / 2))) ||
                ((Type is TYP_LONG) && (unchecked((ulong)divisorValue) > (ulong.MaxValue / 2))))
            {
                return true;
            }
        }

#if TARGET_XARCH || TARGET_ARM64 || TARGET_LOONGARCH64 || TARGET_RISCV64
        // Reciprocal multiplication also handles negative magic divisors.
        // ARM32 has no GT_MULHI support for this path.
        if (!compiler.opts.MinOpts && (!isSigned || (divisorValue >= 3) || (divisorValue <= -3)))
        {
            return true;
        }
#endif
        return false;
    }

    public void CheckDivideByConstOptimized(Compiler compiler)
    {
        if (UsesDivideByConstOptimized(compiler))
        {
            var divisor = Op2.EffectiveVal;
            if (divisor.Oper is GT_CNS_INT)
            {
                divisor.Flags |= GTF_DONT_CSE;
            }
        }
    }

    public void ReverseRelop()
    {
        _oper = _oper.ReverseRelop;

        // Flip the GTF_RELOP_NAN_UN bit
        //     a ord b   === (a != NaN && b != NaN)
        //     a unord b === (a == NaN || b == NaN)
        // => !(a ord b) === (a unord b)

        if (varTypeIsFloating(Op1.Type))
        {
            Flags ^= GTF_RELOP_NAN_UN;
        }
    }

    public void SwapRelop()
    {
        _oper = _oper.SwapRelop;
        (Op1, Op2) = (Op2, Op1);
    }
}
