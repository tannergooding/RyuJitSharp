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
        : base(EnsureArm64BitfieldSupported(oper), type, op1)
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
        else if (Configuration.DEBUG)
        {
            // Binary operators, with a few exceptions, require a non-null second argument.
            assert(IsNullOp2Legal);
        }
    }

    internal GenTreeOp(genTreeOps oper, var_types type, GenTree? op1, GenTree? op2, GenTree source, NodeThreading threading)
        : base(EnsureArm64BitfieldSupported(oper), type, op1, source, threading)
    {
        _op2 = op2;
        assert(!oper.IsCompare || varTypeIsIntegral(type));
    }

    private static genTreeOps EnsureArm64BitfieldSupported(genTreeOps oper)
    {
        if ((oper is GT_BFIZ or GT_BFX) && !Configuration.TARGET_ARM64)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_INTERNALERROR,
        FatalJitErrorCode.ARM64BitfieldNodesAreOnlySupportedOnARM64
    );
            return default!;
        }

        return oper;
    }

    public ref GenTree ReturnValueRef()
    {
        assert(Oper is GT_RETURN or GT_RETFILT or GT_SWIFT_ERROR_RET);
        if (Configuration.SWIFT_SUPPORT && (Oper is GT_SWIFT_ERROR_RET))
        {
            return ref Op2Ref;
        }
        return ref Op1Ref;
    }

    public GenTree GetReturnValue()
    {
        assert(Oper is GT_RETURN or GT_RETFILT or GT_SWIFT_ERROR_RET);
        if (Configuration.SWIFT_SUPPORT && (Oper is GT_SWIFT_ERROR_RET))
        {
            return Op2;
        }
        return Op1;
    }

    public void SetReturnValue(GenTree retVal)
    {
        assert(Oper is GT_RETURN or GT_RETFILT or GT_SWIFT_ERROR_RET);
        if (Configuration.SWIFT_SUPPORT && (Oper is GT_SWIFT_ERROR_RET))
        {
            Op2 = retVal;
        }
        else
        {
            Op1 = retVal;
        }
    }

    public bool IsNullOp2Legal => !Oper.IsBinary || Oper switch {
        GT_INTRINSIC => true,
        GT_LEA => true,

        GT_SELECT_NEGCC => true,
        GT_SELECT_INCCC => true,

        GT_SWIFT_ERROR_RET => Configuration.SWIFT_SUPPORT && Type is TYP_VOID,

        _ => false,
    };

    public GenTree Op2
    {
        get
        {
            if (Configuration.DEBUG)
            {
                assert(Debugger.IsAttached || (_op2 is not null) || IsNullOp2Legal);
            }

            return _op2!;
        }

        set
        {
            _op2 = value;
        }
    }

    // A contained memory operand establishes the legal comparison width even when small operand types differ.
    public int GetCompareSize()
    {
        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.ComparisonOperandSizesAreOnlySupportedOnXarchTargets);
            return default!;
        }

        assert(Oper.IsCompare || Oper is GT_CMP or GT_TEST or GT_BT);

        var op1Type = Op1.Type;
        var op2Type = Op2.Type;
        if (Oper is GT_BITTEST_EQ or GT_BITTEST_NE or GT_BT)
        {
            return op1Type.ActualType.Size;
        }

        if (Op1.IsContained && !Op1.Oper.IsCnsIntOrI)
        {
            return op1Type.Size;
        }

        if (Op2.IsContained && !Op2.Oper.IsCnsIntOrI)
        {
            return op2Type.Size;
        }

        if (op1Type == op2Type)
        {
            return op1Type.Size;
        }

        var size = op1Type.Size == 8 && op2Type.Size == 8 ? 8 : 4;
        assert(size >= op1Type.Size && size >= op2Type.Size);
        return size;
    }

    public bool IsValidLongMul()
    {
        assert(Oper is GT_MUL);
        var op1 = Op1;
        var op2 = Op2;
        if (Type is not TYP_LONG)
        {
            return false;
        }

        assert(op1.Type is TYP_LONG);
        assert(op2.Type is TYP_LONG);
        if ((op1 is not GenTreeCast firstCast) || (firstCast.CastOp.Type.ActualType is not TYP_INT))
        {
            return false;
        }

        if (!((op2 is GenTreeCast secondCast) && (secondCast.CastOp.Type.ActualType is TYP_INT)) &&
            !(op2.Oper.IsIntegralConst && FitsIn(TYP_INT, op2.AsIntConCommon().IntegralValue)))
        {
            return false;
        }

        if (op1.HasOverflowCheck || op2.HasOverflowCheckEx)
        {
            return false;
        }

        if (HasOverflowCheck)
        {
            long GetMaxValue(GenTree operand)
            {
                if (operand is GenTreeCast cast)
                {
                    if (cast.IsUnsigned)
                    {
                        return cast.CastOp.Type switch {
                            TYP_UBYTE => byte.MaxValue,
                            TYP_USHORT => ushort.MaxValue,
                            _ => uint.MaxValue,
                        };
                    }

                    // A sign extension can denote UINT64_MAX for an unsigned multiply.
                    return IsUnsigned ? -1L : int.MinValue;
                }

                return operand.AsIntConCommon().IntegralValue;
            }

            var maxOp1 = GetMaxValue(op1);
            var maxOp2 = GetMaxValue(op2);
            var fits = IsUnsigned
                ? CheckedOps.TryMulUns(maxOp1, maxOp2, out _)
                : CheckedOps.TryMul(maxOp1, maxOp2, out _);
            if (!fits)
            {
                return false;
            }
        }

        var op1ZeroExtends = firstCast.IsUnsigned;
        var op2ZeroExtends = op2.Oper is GT_CAST ? op2.AsCast().IsUnsigned : op2.AsIntConCommon().IntegralValue >= 0;
        var op2AnyExtensionIsSuitable = op2.Oper.IsIntegralConst && op2ZeroExtends;
        if ((op1ZeroExtends != op2ZeroExtends) && !op2AnyExtensionIsSuitable)
        {
            return false;
        }

        return true;
    }

    // The 32-bit lowering path must preserve the MUL_LONG-compatible tree shape from morph through decomposition.
    [System.Diagnostics.Conditional("DEBUG")]
    public void DebugCheckLongMul()
    {
        if (Configuration.TARGET_64BIT || Configuration.TARGET_WASM)
        {
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.LongMultiplyShapeChecksAreOnlySupportedOnNonWasm32BitTargets);
            return;
        }

        assert(Oper is GT_MUL);
        assert(Is64RsltMul);
        assert(Type is TYP_LONG);
        assert(!HasOverflowCheck);

        var op1 = Op1;
        var op2 = Op2;

        assert(op1.Type is TYP_LONG);
        assert(op2.Type is TYP_LONG);

        assert((op1.Oper is GT_CAST) && (op1.AsCast().CastOp.Type.ActualType is TYP_INT));
        assert(!op1.HasOverflowCheck);

        assert(((op2.Oper is GT_CAST) && (op2.AsCast().CastOp.Type.ActualType is TYP_INT)) ||
            (op2.Oper.IsIntegralConst && FitsIn(TYP_INT, op2.AsIntConCommon().IntegralValue)));
        assert(!op2.HasOverflowCheckEx);

        var op1ZeroExtends = op1.AsCast().IsUnsigned;
        var op2ZeroExtends = op2.Oper is GT_CAST ? op2.AsCast().IsUnsigned : op2.AsIntConCommon().IntegralValue >= 0;
        var op2AnyExtensionIsSuitable = op2.Oper.IsIntegralConst && op2ZeroExtends;
        assert((op1ZeroExtends == op2ZeroExtends) || op2AnyExtensionIsSuitable);

        assert(op1.AsCast().IsUnsigned == IsUnsigned);
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

        if (Configuration.TARGET_ARM64 && (Oper is GT_MOD or GT_UMOD))
        {
            return false;
        }
        var isSigned = Oper is GT_DIV or GT_MOD;
        var dividend = Op1.EffectiveVal;
        var divisor = Op2.EffectiveVal;
        if (Configuration.TARGET_32BIT && (dividend.Oper is GT_LONG))
        {
            return false;
        }
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

        if (Configuration.TARGET_32BIT)
        {
            divisorValue = unchecked((nint)Target.CastToSignedSize((long)divisorValue));
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

            var magnitude = unchecked((ulong)(divisorValue >= 0 ? divisorValue : -divisorValue));
            if (Configuration.TARGET_64BIT
                ? ulong.IsPow2(magnitude)
                : uint.IsPow2(unchecked((uint)magnitude)))
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

            if (Configuration.TARGET_64BIT
                ? ulong.IsPow2(unchecked((ulong)divisorValue))
                : uint.IsPow2(unchecked((uint)divisorValue)))
            {
                return true;
            }
        }

        if (Oper is GT_DIV or GT_UDIV)
        {
            if (isSigned)
            {
                if (((Type is TYP_INT) && (divisorValue == int.MinValue)) ||
                    (Configuration.TARGET_64BIT && (Type is TYP_LONG) && (divisorValue == long.MinValue)))
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

        // Reciprocal multiplication also handles negative magic divisors.
        // ARM32 has no GT_MULHI support for this path.
        if ((Configuration.TARGET_XARCH || Configuration.TARGET_ARM64 || Configuration.TARGET_LOONGARCH64 ||
            Configuration.TARGET_RISCV64) &&
            !compiler.MinOpts && (!isSigned || (divisorValue >= 3) || (divisorValue <= -3)))
        {
            return true;
        }
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
