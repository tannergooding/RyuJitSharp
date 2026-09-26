// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics;

namespace RyuJitSharp;

public sealed partial class ValueNumStore
{
    private static bool VNFuncIsNumericCast(VNFunc func) => func is VNF_Cast or VNF_CastOvf;

    private static bool VNFuncIsOverflowArithmetic(VNFunc func)
        => func is >= VNF_ADD_OVF and <= VNF_MUL_UN_OVF;

    private static bool VNFuncIsComparison(VNFunc func)
        => func >= VNF_Boundary
            ? func is VNF_GT_UN or VNF_GE_UN or VNF_LT_UN or VNF_LE_UN
            : ((genTreeOps)func).IsCompare;

    private bool VNEvalCanFoldBinaryFunc(var_types type, VNFunc func, ValueNum arg0VN, ValueNum arg1VN)
    {
        if (!IsVNConstant(arg0VN) || !IsVNConstant(arg1VN))
        {
            return false;
        }

        if (func < VNF_Boundary)
        {
            switch (func)
            {
                case VNF_ADD:
                case VNF_SUB:
                case VNF_MUL:
                case VNF_DIV:
                case VNF_MOD:
                case VNF_UDIV:
                case VNF_UMOD:
                case VNF_AND:
                case VNF_OR:
                case VNF_XOR:
                case VNF_LSH:
                case VNF_RSH:
                case VNF_RSZ:
                case VNF_ROL:
                case VNF_ROR:
                {
                    if (_compiler.opts.compReloc && (IsVNHandle(arg0VN) || IsVNHandle(arg1VN)))
                    {
                        return false;
                    }
                    break;
                }

                case VNF_EQ:
                case VNF_NE:
                case VNF_GT:
                case VNF_GE:
                case VNF_LT:
                case VNF_LE:
                {
                    break;
                }

                default:
                {
                    return false;
                }
            }
        }
        else
        {
            switch (func)
            {
                case VNF_GT_UN:
                case VNF_GE_UN:
                case VNF_LT_UN:
                case VNF_LE_UN:
                case VNF_ADD_OVF:
                case VNF_SUB_OVF:
                case VNF_MUL_OVF:
                case VNF_ADD_UN_OVF:
                case VNF_SUB_UN_OVF:
                case VNF_MUL_UN_OVF:
                {
                    if (_compiler.opts.compReloc && (IsVNHandle(arg0VN) || IsVNHandle(arg1VN)))
                    {
                        return false;
                    }
                    break;
                }

                case VNF_Cast:
                case VNF_CastOvf:
                {
                    if ((type != TYP_I_IMPL) && IsVNHandle(arg0VN))
                    {
                        return false;
                    }
                    break;
                }

                case VNF_BitCast:
                {
                    if (!varTypeIsArithmetic(type) || IsVNHandle(arg0VN))
                    {
                        return false;
                    }
                    break;
                }

                default:
                {
                    return false;
                }
            }
        }

        var arg0IsFloating = varTypeIsFloating(TypeOfVN(arg0VN));
        var arg1IsFloating = varTypeIsFloating(TypeOfVN(arg1VN));
        if (!VNFuncIsNumericCast(func) && (func != VNF_BitCast) && (arg0IsFloating != arg1IsFloating))
        {
            return false;
        }

        return type != TYP_BYREF;
    }

    private bool VNEvalShouldFold(var_types type, VNFunc func, ValueNum arg0VN, ValueNum arg1VN)
    {
        assert(type != TYP_BYREF);
        if (varTypeIsFloating(type))
        {
            return true;
        }

        if (func is VNF_DIV or VNF_UDIV or VNF_MOD or VNF_UMOD)
        {
            if (type is not (TYP_INT or TYP_LONG))
            {
                assert(false);
                return false;
            }

            if ((TypeOfVN(arg0VN) != type) || (TypeOfVN(arg1VN) != type))
            {
                return false;
            }

            var divisor = CoercedConstantValue<long>(arg1VN);
            if (divisor == 0)
            {
                return false;
            }

            if ((func is VNF_DIV or VNF_MOD) && (divisor == -1))
            {
                // On x64 idiv also traps on MIN % -1, so VN leaves that expression intact.
                var dividend = CoercedConstantValue<long>(arg0VN);
                return dividend != (type == TYP_INT ? int.MinValue : long.MinValue);
            }
        }

        if (VNFuncIsOverflowArithmetic(func))
        {
            if (type == TYP_INT)
            {
                var left = ConstantValue<int>(arg0VN);
                var right = ConstantValue<int>(arg1VN);
                return func switch {
                    VNF_ADD_OVF => CheckedOps.TryAdd(left, right, out _),
                    VNF_SUB_OVF => CheckedOps.TrySub(left, right, out _),
                    VNF_MUL_OVF => CheckedOps.TryMul(left, right, out _),
                    VNF_ADD_UN_OVF => CheckedOps.TryAddUns(left, right, out _),
                    VNF_SUB_UN_OVF => CheckedOps.TrySubUns(left, right, out _),
                    VNF_MUL_UN_OVF => CheckedOps.TryMulUns(left, right, out _),
                    _ => throw new UnreachableException(),
                };
            }

            if (type == TYP_LONG)
            {
                var left = ConstantValue<long>(arg0VN);
                var right = ConstantValue<long>(arg1VN);
                return func switch {
                    VNF_ADD_OVF => CheckedOps.TryAdd(left, right, out _),
                    VNF_SUB_OVF => CheckedOps.TrySub(left, right, out _),
                    VNF_MUL_OVF => CheckedOps.TryMul(left, right, out _),
                    VNF_ADD_UN_OVF => CheckedOps.TryAddUns(left, right, out _),
                    VNF_SUB_UN_OVF => CheckedOps.TrySubUns(left, right, out _),
                    VNF_MUL_UN_OVF => CheckedOps.TryMulUns(left, right, out _),
                    _ => throw new UnreachableException(),
                };
            }

            assert(false);
            return false;
        }

        if (VNFuncIsNumericCast(func))
        {
            var castFromType = TypeOfVN(arg0VN);
            if ((func == VNF_CastOvf) || varTypeIsFloating(castFromType))
            {
                GetCastOperFromVN(arg1VN, out var castToType, out var fromUnsigned);
                return castFromType switch {
                    TYP_INT => !CheckedOps.CastFromIntOverflows(GetConstantInt32(arg0VN), castToType, fromUnsigned),
                    TYP_LONG => !CheckedOps.CastFromLongOverflows(GetConstantInt64(arg0VN), castToType, fromUnsigned),
                    TYP_FLOAT => !CheckedOps.CastFromFloatOverflows(GetConstantSingle(arg0VN), castToType),
                    TYP_DOUBLE => !CheckedOps.CastFromDoubleOverflows(GetConstantDouble(arg0VN), castToType),
                    _ => false,
                };
            }
        }

        return true;
    }

    private ValueNum EvalFuncForConstantArgs(var_types type, VNFunc func, ValueNum arg0VN, ValueNum arg1VN)
    {
        assert(VNEvalCanFoldBinaryFunc(type, func, arg0VN, arg1VN));

        if (VNFuncIsNumericCast(func))
        {
            return EvalCastForConstantArgs(type, func, arg0VN, arg1VN);
        }

        if (func == VNF_BitCast)
        {
            return EvalBitCastForConstantArgs(type, arg0VN);
        }

        var arg0Type = TypeOfVN(arg0VN);
        var arg1Type = TypeOfVN(arg1VN);
        if (varTypeIsFloating(arg0Type) && varTypeIsFloating(arg1Type))
        {
            return EvalFuncForConstantFPArgs(type, func, arg0VN, arg1VN);
        }

        assert(!varTypeIsFloating(arg0Type) && !varTypeIsFloating(arg1Type));
        if (varTypeIsSmall(type))
        {
            type = TYP_INT;
        }

        if (arg0Type == arg1Type)
        {
            if (arg0Type == TYP_INT)
            {
                assert(type == TYP_INT);
                var left = ConstantValue<int>(arg0VN);
                var right = ConstantValue<int>(arg1VN);
                return VNForIntCon(VNFuncIsComparison(func) ?
                    EvalComparison(func, left, right) : EvalOp(func, left, right));
            }

            if (arg0Type == TYP_LONG)
            {
                var left = ConstantValue<long>(arg0VN);
                var right = ConstantValue<long>(arg1VN);
                if (VNFuncIsComparison(func))
                {
                    assert(type == TYP_INT);
                    return VNForIntCon(EvalComparison(func, left, right));
                }

                assert(type == TYP_LONG);
                return VNForLongCon(EvalOp(func, left, right));
            }

            assert(arg0Type is TYP_REF or TYP_BYREF);
            var nativeLeft = ConstantValue<nuint>(arg0VN);
            var nativeRight = ConstantValue<nuint>(arg1VN);
            if (VNFuncIsComparison(func))
            {
                assert(type == TYP_INT);
                return VNForIntCon(EvalComparison(func, nativeLeft, nativeRight));
            }

            var nativeResult = EvalOp(func, nativeLeft, nativeRight);
            if (type == TYP_INT)
            {
                return VNForIntCon(unchecked((int)nativeResult));
            }

            assert(type is TYP_BYREF or TYP_I_IMPL);
            return VNForByrefCon(nativeResult);
        }

        // Mixed integral/ref/byref operands are evaluated at the oracle's INT64 width.
        var left64 = GetConstantInt64(arg0VN);
        var right64 = GetConstantInt64(arg1VN);
        if (VNFuncIsComparison(func))
        {
            assert(type == TYP_INT);
            return VNForIntCon(EvalComparison(func, left64, right64));
        }

        var result64 = EvalOp(func, left64, right64);
        if (type == TYP_INT)
        {
            return VNForIntCon(unchecked((int)result64));
        }

        return type switch {
            TYP_LONG => VNForLongCon(result64),
            TYP_BYREF => VNForByrefCon(unchecked((nuint)result64)),
            TYP_REF when result64 == 0 => VNForNull(),
            _ => throw new UnreachableException(),
        };
    }

    private ValueNum EvalFuncForConstantFPArgs(var_types type, VNFunc func, ValueNum arg0VN, ValueNum arg1VN)
    {
        assert(VNEvalCanFoldBinaryFunc(type, func, arg0VN, arg1VN));

        var arg0Type = TypeOfVN(arg0VN);
        var arg1Type = TypeOfVN(arg1VN);
        assert(varTypeIsFloating(arg0Type) && (arg0Type == arg1Type));

        if (VNFuncIsComparison(func))
        {
            assert(type.ActualType == TYP_INT);
            return arg0Type == TYP_FLOAT
                ? VNForIntCon(EvalComparison(func, GetConstantSingle(arg0VN), GetConstantSingle(arg1VN)))
                : VNForIntCon(EvalComparison(func, GetConstantDouble(arg0VN), GetConstantDouble(arg1VN)));
        }

        assert(varTypeIsFloating(type) && (arg0Type == type));
        if (type == TYP_FLOAT)
        {
            return VNForFloatCon(EvalOp(func, GetConstantSingle(arg0VN), GetConstantSingle(arg1VN)));
        }

        assert(type == TYP_DOUBLE);
        return VNForDoubleCon(EvalOp(func, GetConstantDouble(arg0VN), GetConstantDouble(arg1VN)));
    }
}
