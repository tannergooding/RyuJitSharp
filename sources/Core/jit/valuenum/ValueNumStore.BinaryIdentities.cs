// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class ValueNumStore
{
    private ValueNum EvalUsingMathIdentity(var_types type, VNFunc func, ValueNum arg0VN, ValueNum arg1VN)
    {
        var result = NoVN;
        if (type == TYP_BYREF)
        {
            return result;
        }

        if (VNFuncIsComparison(func) && (TypeOfVN(arg0VN) == TYP_LONG) && (TypeOfVN(arg1VN) == TYP_LONG))
        {
            var newArg0 = VNIgnoreIntToLongCast(arg0VN);
            var newArg1 = VNIgnoreIntToLongCast(arg1VN);
            if ((TypeOfVN(newArg0) == TYP_INT) && (TypeOfVN(newArg1) == TYP_INT))
            {
                return VNForFunc(TYP_INT, func, newArg0, newArg1);
            }
        }

        var constant = NoVN;
        var operand = NoVN;
        if (IsVNConstant(arg0VN))
        {
            constant = arg0VN;
            operand = arg1VN;
        }
        else if (IsVNConstant(arg1VN))
        {
            constant = arg1VN;
            operand = arg0VN;
        }

        if (func < VNF_Boundary)
        {
            switch (func)
            {
                case VNF_ADD:
                {
                    return IdentityForAddition(type, arg0VN, arg1VN, constant, operand, false);
                }

                case VNF_SUB:
                {
                    return IdentityForSubtraction(type, arg0VN, arg1VN, constant, operand, false);
                }

                case VNF_MUL:
                {
                    return IdentityForMultiplication(type, constant, operand);
                }

                case VNF_DIV:
                case VNF_UDIV:
                {
                    if (arg1VN == VNOneForType(type))
                    {
                        result = arg0VN;
                    }

                    if (constant != NoVN && varTypeIsFloating(type) && IsFloatingNaN(type, constant))
                    {
                        return constant;
                    }
                    break;
                }

                case VNF_OR:
                case VNF_AND:
                {
                    var zero = VNZeroForType(type);
                    if (constant == zero)
                    {
                        return func == VNF_OR ? operand : zero;
                    }

                    var allBits = VNAllBitsForType(type, 1);
                    if (constant == allBits)
                    {
                        return func == VNF_OR ? constant : operand;
                    }

                    if (arg0VN == arg1VN)
                    {
                        return arg0VN;
                    }

                    var app = new VNFuncApp();
                    var isRelop = (func == VNF_OR) && GetVNFunc(arg0VN, ref app) && VNFuncIsComparison(app.Func);
                    if (!isRelop)
                    {
                        var notArg0 = VNForFunc(type, VNF_NOT, arg0VN);
                        if (notArg0 == arg1VN)
                        {
                            return func == VNF_OR ? VNAllBitsForType(type, 1) : zero;
                        }
                    }
                    return CombineRelatedRelops(type, func, arg0VN, arg1VN);
                }

                case VNF_XOR:
                {
                    var zero = VNZeroForType(type);
                    if (constant == zero)
                    {
                        return operand;
                    }

                    if (arg0VN == arg1VN)
                    {
                        return zero;
                    }
                    break;
                }

                case VNF_LSH:
                case VNF_RSH:
                case VNF_RSZ:
                case VNF_ROL:
                case VNF_ROR:
                {
                    var zero = VNZeroForType(type);
                    if (arg1VN == zero)
                    {
                        return arg0VN;
                    }

                    if (arg0VN == zero)
                    {
                        return zero;
                    }
                    break;
                }

                case VNF_EQ:
                {
                    var zero = VNZeroForType(type);
                    if ((constant == VNForNull()) && IsKnownNonNull(operand))
                    {
                        return VNZeroForType(type);
                    }

                    if (IsVNRelop(operand))
                    {
                        if (constant == VNOneForType(type))
                        {
                            return operand;
                        }

                        if (constant == zero)
                        {
                            var reverse = GetReverseRelop(operand);
                            if (reverse != NoVN)
                            {
                                return reverse;
                            }
                        }
                    }

                    goto case VNF_GE;
                }

                case VNF_GE:
                case VNF_LE:
                {
                    var zero = VNZeroForType(type);
                    var opType = TypeOfVN(arg0VN);
                    if (varTypeIsFloating(opType))
                    {
                        return (constant != NoVN) && IsFloatingNaN(opType, constant)
                            ? zero : NoVN;
                    }

                    assert(varTypeIsIntegralOrI(opType));
                    if (arg0VN == arg1VN)
                    {
                        return VNOneForType(type);
                    }

                    if (((func == VNF_GE) && (arg1VN == VNZeroForType(TypeOfVN(arg1VN))) &&
                            IsVNNeverNegative(arg0VN)) ||
                        ((func == VNF_LE) && (arg0VN == VNZeroForType(opType)) &&
                            IsVNNeverNegative(arg1VN)))
                    {
                        return VNOneForType(type);
                    }
                    break;
                }

                case VNF_NE:
                {
                    var zero = VNZeroForType(type);
                    if (IsVNRelop(operand))
                    {
                        if (constant == zero)
                        {
                            return operand;
                        }

                        if (constant == VNOneForType(type))
                        {
                            var reverse = GetReverseRelop(operand);
                            if (reverse != NoVN)
                            {
                                return reverse;
                            }
                        }
                    }

                    var opType = TypeOfVN(arg0VN);
                    if (varTypeIsFloating(opType))
                    {
                        return (constant != NoVN) && IsFloatingNaN(opType, constant)
                            ? VNOneForType(type) : NoVN;
                    }

                    assert(varTypeIsIntegralOrI(opType));
                    if ((constant == VNForNull()) && IsKnownNonNull(operand))
                    {
                        return VNOneForType(type);
                    }

                    if (arg0VN == arg1VN)
                    {
                        return zero;
                    }
                    break;
                }

                case VNF_GT:
                case VNF_LT:
                {
                    var zero = VNZeroForType(type);
                    if (arg0VN == arg1VN)
                    {
                        result = zero;
                    }

                    var opType = TypeOfVN(arg0VN);
                    if (varTypeIsFloating(opType))
                    {
                        if ((constant != NoVN) && IsFloatingNaN(opType, constant))
                        {
                            result = zero;
                        }
                        break;
                    }

                    assert(varTypeIsIntegralOrI(opType));
                    if (((func == VNF_LT) && (arg1VN == VNZeroForType(TypeOfVN(arg1VN))) &&
                            IsVNNeverNegative(arg0VN)) ||
                        ((func == VNF_GT) && (arg0VN == VNZeroForType(opType)) &&
                            IsVNNeverNegative(arg1VN)))
                    {
                        result = zero;
                    }
                    break;
                }
            }
        }
        else
        {
            switch (func)
            {
                case VNF_ADD_OVF:
                case VNF_ADD_UN_OVF:
                {
                    return IdentityForAddition(type, arg0VN, arg1VN, constant, operand, true);
                }

                case VNF_SUB_OVF:
                case VNF_SUB_UN_OVF:
                {
                    return IdentityForSubtraction(type, arg0VN, arg1VN, constant, operand, true);
                }

                case VNF_MUL_OVF:
                case VNF_MUL_UN_OVF:
                {
                    return IdentityForMultiplication(type, constant, operand);
                }

                case VNF_LT_UN:
                case VNF_GT_UN:
                {
                    if (func == VNF_LT_UN)
                    {
                        (arg0VN, arg1VN) = (arg1VN, arg0VN);
                    }

                    var zero = VNZeroForType(type);
                    var opType = TypeOfVN(arg0VN);
                    if (varTypeIsFloating(opType))
                    {
                        return (constant != NoVN) && IsFloatingNaN(opType, constant)
                            ? VNOneForType(type) : NoVN;
                    }

                    assert(varTypeIsIntegralOrI(opType));
                    if ((arg0VN == VNZeroForType(opType)) || (arg0VN == arg1VN))
                    {
                        result = zero;
                    }

                    if (IsVNNeverNegative(arg0VN) && IsVNNeverNegative(arg1VN))
                    {
                        result = VNForFunc(type, VNF_GT, arg0VN, arg1VN);
                    }
                    break;
                }

                case VNF_GE_UN:
                case VNF_LE_UN:
                {
                    if (func == VNF_GE_UN)
                    {
                        (arg0VN, arg1VN) = (arg1VN, arg0VN);
                    }

                    if (arg0VN == arg1VN)
                    {
                        return VNOneForType(type);
                    }

                    if (varTypeIsIntegralOrI(TypeOfVN(arg0VN)) &&
                        (arg0VN == VNZeroForType(TypeOfVN(arg0VN))))
                    {
                        return VNOneForType(type);
                    }

                    if (IsVNNeverNegative(arg0VN) && IsVNNeverNegative(arg1VN))
                    {
                        result = VNForFunc(type, VNF_LE, arg0VN, arg1VN);
                    }
                    break;
                }
            }
        }

        return result;
    }

    private bool IsFloatingNaN(var_types type, ValueNum vn)
        => type == TYP_FLOAT ? float.IsNaN(GetConstantSingle(vn)) : double.IsNaN(GetConstantDouble(vn));

    private bool IsFloatingNegativeZero(var_types type, ValueNum vn)
    {
        var value = type == TYP_FLOAT ? GetConstantSingle(vn) : GetConstantDouble(vn);
        return (value == 0) && double.IsNegative(value);
    }

    private bool IsFloatingPositiveZero(var_types type, ValueNum vn)
    {
        var value = type == TYP_FLOAT ? GetConstantSingle(vn) : GetConstantDouble(vn);
        return (value == 0) && double.IsPositive(value);
    }

    private ValueNum IdentityForAddition(var_types type, ValueNum arg0VN, ValueNum arg1VN,
        ValueNum constant, ValueNum operand, bool overflow)
    {
        var zero = VNZeroForType(type);
        if (!varTypeIsFloating(type))
        {
            if (constant == zero)
            {
                return operand;
            }

            if (!overflow)
            {
                for (var index = 0; index < 2; index++)
                {
                    var subtraction = index == 0 ? arg0VN : arg1VN;
                    var other = index == 0 ? arg1VN : arg0VN;
                    var left = NoVN;
                    var right = NoVN;
                    if (IsVNBinFunc(subtraction, VNF_SUB, ref left, ref right) && (right == other))
                    {
                        return left;
                    }
                }
            }
        }
        else if (constant != NoVN)
        {
            if (IsFloatingNaN(type, constant))
            {
                return constant;
            }

            if (IsFloatingNegativeZero(type, constant))
            {
                return operand;
            }
        }

        return NoVN;
    }

    private ValueNum IdentityForSubtraction(var_types type, ValueNum arg0VN, ValueNum arg1VN,
        ValueNum constant, ValueNum operand, bool overflow)
    {
        var zero = VNZeroForType(type);
        if (!varTypeIsFloating(type))
        {
            if (arg1VN == zero)
            {
                return arg0VN;
            }

            if (arg0VN == arg1VN)
            {
                return zero;
            }

            if (!overflow)
            {
                var first = NoVN;
                var second = NoVN;
                if (IsVNBinFunc(arg1VN, VNF_ADD, ref first, ref second))
                {
                    if (first == arg0VN)
                    {
                        return VNForFunc(type, VNF_NEG, second);
                    }

                    if (second == arg0VN)
                    {
                        return VNForFunc(type, VNF_NEG, first);
                    }
                }

                if (IsVNBinFunc(arg1VN, VNF_SUB, ref first, ref second) && (first == arg0VN))
                {
                    return second;
                }

                if (IsVNBinFunc(arg0VN, VNF_ADD, ref first, ref second))
                {
                    if (first == arg1VN)
                    {
                        return second;
                    }

                    if (second == arg1VN)
                    {
                        return first;
                    }

                    var third = NoVN;
                    var fourth = NoVN;
                    if (IsVNBinFunc(arg1VN, VNF_ADD, ref third, ref fourth))
                    {
                        for (var a = 0; a < 2; a++)
                        {
                            for (var b = 0; b < 2; b++)
                            {
                                if ((a == 0 ? first : second) == (b == 0 ? third : fourth))
                                {
                                    return VNForFunc(type, VNF_SUB,
                                        a == 0 ? second : first, b == 0 ? fourth : third);
                                }
                            }
                        }
                    }
                }
            }
        }
        else if (constant != NoVN)
        {
            if (IsFloatingNaN(type, constant))
            {
                return constant;
            }

            if ((constant == arg1VN) && IsFloatingPositiveZero(type, constant))
            {
                return operand;
            }
        }

        return NoVN;
    }

    private ValueNum IdentityForMultiplication(var_types type, ValueNum constant, ValueNum operand)
    {
        var zero = VNZeroForType(type);
        if (constant == VNOneForType(type))
        {
            return operand;
        }

        if (!varTypeIsFloating(type))
        {
            return constant == zero ? zero : NoVN;
        }

        return (constant != NoVN) && IsFloatingNaN(type, constant) ? constant : NoVN;
    }
}
