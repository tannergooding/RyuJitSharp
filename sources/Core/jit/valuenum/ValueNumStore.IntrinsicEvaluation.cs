// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public sealed partial class ValueNumStore
{
    public ValueNumPair EvalMathFuncUnary(var_types type, NamedIntrinsic intrinsic, ValueNumPair argument)
    {
        var liberal = EvalMathFuncUnary(type, intrinsic, argument.Liberal);
        var conservative = argument.BothEqual()
            ? liberal
            : EvalMathFuncUnary(type, intrinsic, argument.Conservative);
        return new(liberal, conservative);
    }

    public ValueNumPair EvalMathFuncBinary(var_types type, NamedIntrinsic intrinsic, ValueNumPair left,
        ValueNumPair right)
    {
        var liberal = EvalMathFuncBinary(type, intrinsic, left.Liberal, right.Liberal);
        var conservative = left.BothEqual() && right.BothEqual()
            ? liberal
            : EvalMathFuncBinary(type, intrinsic, left.Conservative, right.Conservative);
        return new(liberal, conservative);
    }

    private const int SimdTypeBits = 8;
    private const int SimdTypeMask = (1 << SimdTypeBits) - 1;

    public ValueNum VNForSimdType(int simdSize, var_types simdBaseType, var_types auxiliaryType = TYP_UNKNOWN)
    {
        EnsureSimdSupported();
        var encodedTypes = var_typesExtensions.ToNativeOrdinal(simdBaseType);
        var auxiliaryTypeOrdinal = var_typesExtensions.ToNativeOrdinal(auxiliaryType);
        assert(encodedTypes <= SimdTypeMask && auxiliaryTypeOrdinal <= SimdTypeMask);
        if (auxiliaryType is not TYP_UNKNOWN)
        {
            encodedTypes |= (auxiliaryTypeOrdinal + 1) << SimdTypeBits;
        }

        var baseTypeVN = VNForIntCon(encodedTypes);
        var sizeVN = VNForIntCon(simdSize);
        return VNForFunc(TYP_REF, VNF_SimdType, sizeVN, baseTypeVN);
    }

    public ValueNum EvalMathFuncUnary(var_types type, NamedIntrinsic intrinsic, ValueNum argument)
    {
        assert(argument == VNNormalValue(argument));
        assert(_compiler.IsMathIntrinsic(intrinsic) || intrinsic is
            NI_PRIMITIVE_LeadingZeroCount or NI_PRIMITIVE_TrailingZeroCount or NI_PRIMITIVE_PopCount);

        // ReadyToRun avoids folding math operations implemented by calls, to minimize precision loss.
        if (IsVNConstant(argument) && (!_compiler.IsAot || _compiler.IsTargetIntrinsic(intrinsic)))
        {
            if (type is TYP_DOUBLE)
            {
                assert(TypeOfVN(argument) == type);
                var value = GetConstantDouble(argument);
                double __jitExceptionResult725;
                switch (intrinsic)
                {
                    case NI_System_Math_Abs:
                        __jitExceptionResult725 = Math.Abs(value);
                        break;
                    case NI_System_Math_Acos:
                        __jitExceptionResult725 = Math.Acos(value);
                        break;
                    case NI_System_Math_Acosh:
                        __jitExceptionResult725 = Math.Acosh(value);
                        break;
                    case NI_System_Math_Asin:
                        __jitExceptionResult725 = Math.Asin(value);
                        break;
                    case NI_System_Math_Asinh:
                        __jitExceptionResult725 = Math.Asinh(value);
                        break;
                    case NI_System_Math_Atan:
                        __jitExceptionResult725 = Math.Atan(value);
                        break;
                    case NI_System_Math_Atanh:
                        __jitExceptionResult725 = Math.Atanh(value);
                        break;
                    case NI_System_Math_Cbrt:
                        __jitExceptionResult725 = Math.Cbrt(value);
                        break;
                    case NI_System_Math_Ceiling:
                        __jitExceptionResult725 = Math.Ceiling(value);
                        break;
                    case NI_System_Math_Cos:
                        __jitExceptionResult725 = Math.Cos(value);
                        break;
                    case NI_System_Math_Cosh:
                        __jitExceptionResult725 = Math.Cosh(value);
                        break;
                    case NI_System_Math_Exp:
                        __jitExceptionResult725 = Math.Exp(value);
                        break;
                    case NI_System_Math_Floor:
                        __jitExceptionResult725 = Math.Floor(value);
                        break;
                    case NI_System_Math_Log:
                        __jitExceptionResult725 = Math.Log(value);
                        break;
                    case NI_System_Math_Log2:
                        __jitExceptionResult725 = Math.Log2(value);
                        break;
                    case NI_System_Math_Log10:
                        __jitExceptionResult725 = Math.Log10(value);
                        break;
                    case NI_System_Math_Sin:
                        __jitExceptionResult725 = Math.Sin(value);
                        break;
                    case NI_System_Math_Sinh:
                        __jitExceptionResult725 = Math.Sinh(value);
                        break;
                    case NI_System_Math_Round:
                        __jitExceptionResult725 = Math.Round(value);
                        break;
                    case NI_System_Math_Sqrt:
                        __jitExceptionResult725 = Math.Sqrt(value);
                        break;
                    case NI_System_Math_Tan:
                        __jitExceptionResult725 = Math.Tan(value);
                        break;
                    case NI_System_Math_Tanh:
                        __jitExceptionResult725 = Math.Tanh(value);
                        break;
                    case NI_System_Math_Truncate:
                        __jitExceptionResult725 = Math.Truncate(value);
                        break;
                    default:
                        if (Configuration.DETAILED_FATAL_ERROR_MESSAGES)
                        {
                            JitExceptionHelpers.ThrowInvalidOperationException(
                                FatalJitErrorCode.UnsupportedDoubleMathIntrinsicArgument,
                                intrinsic);
                        }
                        else
                        {
                            JitExceptionHelpers.ThrowInvalidOperationException(
                                FatalJitErrorCode.UnsupportedDoubleMathIntrinsicArgument);
                        }

                        __jitExceptionResult725 = default;
                        break;
                }
                var doubleResult = __jitExceptionResult725;
                return VNForDoubleCon(doubleResult);
            }

            if (type is TYP_FLOAT)
            {
                assert(TypeOfVN(argument) == type);
                var value = GetConstantSingle(argument);
                float __jitExceptionResult726;
                switch (intrinsic)
                {
                    case NI_System_Math_Abs:
                        __jitExceptionResult726 = MathF.Abs(value);
                        break;
                    case NI_System_Math_Acos:
                        __jitExceptionResult726 = MathF.Acos(value);
                        break;
                    case NI_System_Math_Acosh:
                        __jitExceptionResult726 = MathF.Acosh(value);
                        break;
                    case NI_System_Math_Asin:
                        __jitExceptionResult726 = MathF.Asin(value);
                        break;
                    case NI_System_Math_Asinh:
                        __jitExceptionResult726 = MathF.Asinh(value);
                        break;
                    case NI_System_Math_Atan:
                        __jitExceptionResult726 = MathF.Atan(value);
                        break;
                    case NI_System_Math_Atanh:
                        __jitExceptionResult726 = MathF.Atanh(value);
                        break;
                    case NI_System_Math_Cbrt:
                        __jitExceptionResult726 = MathF.Cbrt(value);
                        break;
                    case NI_System_Math_Ceiling:
                        __jitExceptionResult726 = MathF.Ceiling(value);
                        break;
                    case NI_System_Math_Cos:
                        __jitExceptionResult726 = MathF.Cos(value);
                        break;
                    case NI_System_Math_Cosh:
                        __jitExceptionResult726 = MathF.Cosh(value);
                        break;
                    case NI_System_Math_Exp:
                        __jitExceptionResult726 = MathF.Exp(value);
                        break;
                    case NI_System_Math_Floor:
                        __jitExceptionResult726 = MathF.Floor(value);
                        break;
                    case NI_System_Math_Log:
                        __jitExceptionResult726 = MathF.Log(value);
                        break;
                    case NI_System_Math_Log2:
                        __jitExceptionResult726 = MathF.Log2(value);
                        break;
                    case NI_System_Math_Log10:
                        __jitExceptionResult726 = MathF.Log10(value);
                        break;
                    case NI_System_Math_Sin:
                        __jitExceptionResult726 = MathF.Sin(value);
                        break;
                    case NI_System_Math_Sinh:
                        __jitExceptionResult726 = MathF.Sinh(value);
                        break;
                    case NI_System_Math_Round:
                        __jitExceptionResult726 = MathF.Round(value);
                        break;
                    case NI_System_Math_Sqrt:
                        __jitExceptionResult726 = MathF.Sqrt(value);
                        break;
                    case NI_System_Math_Tan:
                        __jitExceptionResult726 = MathF.Tan(value);
                        break;
                    case NI_System_Math_Tanh:
                        __jitExceptionResult726 = MathF.Tanh(value);
                        break;
                    case NI_System_Math_Truncate:
                        __jitExceptionResult726 = MathF.Truncate(value);
                        break;
                    default:
                        if (Configuration.DETAILED_FATAL_ERROR_MESSAGES)
                        {
                            JitExceptionHelpers.ThrowInvalidOperationException(
                                FatalJitErrorCode.UnsupportedFloatMathIntrinsicArgument,
                                intrinsic);
                        }
                        else
                        {
                            JitExceptionHelpers.ThrowInvalidOperationException(
                                FatalJitErrorCode.UnsupportedFloatMathIntrinsicArgument);
                        }

                        __jitExceptionResult726 = default;
                        break;
                }
                var floatResult = __jitExceptionResult726;
                return VNForFloatCon(floatResult);
            }

            assert((type is TYP_INT) || ((type is TYP_LONG) && (intrinsic is
                NI_PRIMITIVE_LeadingZeroCount or NI_PRIMITIVE_TrailingZeroCount or NI_PRIMITIVE_PopCount)));
            int result;
            if (intrinsic is NI_System_Math_ILogB or NI_System_Math_Round)
            {
                switch (intrinsic, TypeOfVN(argument))
                {
                    case (NI_System_Math_ILogB, TYP_DOUBLE):
                        result = Math.ILogB(GetConstantDouble(argument));
                        break;
                    case (NI_System_Math_ILogB, TYP_FLOAT):
                        result = MathF.ILogB(GetConstantSingle(argument));
                        break;
                    case (NI_System_Math_Round, TYP_DOUBLE):
                        result = unchecked((int)Math.Round(GetConstantDouble(argument)));
                        break;
                    case (NI_System_Math_Round, TYP_FLOAT):
                        result = unchecked((int)MathF.Round(GetConstantSingle(argument)));
                        break;
                    default:
                        if (Configuration.DETAILED_FATAL_ERROR_MESSAGES)
                        {
                            JitExceptionHelpers.ThrowInvalidOperationException(
                                FatalJitErrorCode.UnsupportedIntegralMathSourceArgument,
                                intrinsic);
                        }
                        else
                        {
                            JitExceptionHelpers.ThrowInvalidOperationException(
                                FatalJitErrorCode.UnsupportedIntegralMathSourceArgument);
                        }

                        result = default;
                        break;
                }
            }
            else
            {
                switch (intrinsic, TypeOfVN(argument))
                {
                    case (NI_PRIMITIVE_LeadingZeroCount, TYP_LONG):
                        result = BitOperations.LeadingZeroCount(unchecked((ulong)GetConstantInt64(argument)));
                        break;
                    case (NI_PRIMITIVE_LeadingZeroCount, TYP_INT):
                        result = BitOperations.LeadingZeroCount(unchecked((uint)GetConstantInt32(argument)));
                        break;
                    case (NI_PRIMITIVE_TrailingZeroCount, TYP_LONG):
                        result = BitOperations.TrailingZeroCount(unchecked((ulong)GetConstantInt64(argument)));
                        break;
                    case (NI_PRIMITIVE_TrailingZeroCount, TYP_INT):
                        result = BitOperations.TrailingZeroCount(unchecked((uint)GetConstantInt32(argument)));
                        break;
                    case (NI_PRIMITIVE_PopCount, TYP_LONG):
                        result = BitOperations.PopCount(unchecked((ulong)GetConstantInt64(argument)));
                        break;
                    case (NI_PRIMITIVE_PopCount, TYP_INT):
                        result = BitOperations.PopCount(unchecked((uint)GetConstantInt32(argument)));
                        break;
                    default:
                        if (Configuration.DETAILED_FATAL_ERROR_MESSAGES)
                        {
                            JitExceptionHelpers.ThrowInvalidOperationException(
                                FatalJitErrorCode.UnsupportedBitCountingIntrinsicArgument,
                                intrinsic);
                        }
                        else
                        {
                            JitExceptionHelpers.ThrowInvalidOperationException(
                                FatalJitErrorCode.UnsupportedBitCountingIntrinsicArgument);
                        }

                        result = default;
                        break;
                }
            }

            return type is TYP_LONG ? VNForLongCon(result) : VNForIntCon(result);
        }

        assert((type is TYP_DOUBLE or TYP_FLOAT) ||
            ((type is TYP_INT) && (intrinsic is NI_System_Math_ILogB or NI_System_Math_Round)) ||
            ((type is TYP_INT or TYP_LONG) && (intrinsic is
                NI_PRIMITIVE_LeadingZeroCount or NI_PRIMITIVE_TrailingZeroCount or NI_PRIMITIVE_PopCount)));

        global::RyuJitSharp.VNFunc __jitExceptionResult727;
        switch (intrinsic)
        {
            case NI_System_Math_Abs:
                __jitExceptionResult727 = VNF_Abs;
                break;
            case NI_System_Math_Acos:
                __jitExceptionResult727 = VNF_Acos;
                break;
            case NI_System_Math_Acosh:
                __jitExceptionResult727 = VNF_Acosh;
                break;
            case NI_System_Math_Asin:
                __jitExceptionResult727 = VNF_Asin;
                break;
            case NI_System_Math_Asinh:
                __jitExceptionResult727 = VNF_Asinh;
                break;
            case NI_System_Math_Atan:
                __jitExceptionResult727 = VNF_Atan;
                break;
            case NI_System_Math_Atanh:
                __jitExceptionResult727 = VNF_Atanh;
                break;
            case NI_System_Math_Cbrt:
                __jitExceptionResult727 = VNF_Cbrt;
                break;
            case NI_System_Math_Ceiling:
                __jitExceptionResult727 = VNF_Ceiling;
                break;
            case NI_System_Math_Cos:
                __jitExceptionResult727 = VNF_Cos;
                break;
            case NI_System_Math_Cosh:
                __jitExceptionResult727 = VNF_Cosh;
                break;
            case NI_System_Math_Exp:
                __jitExceptionResult727 = VNF_Exp;
                break;
            case NI_System_Math_Floor:
                __jitExceptionResult727 = VNF_Floor;
                break;
            case NI_System_Math_ILogB:
                __jitExceptionResult727 = VNF_ILogB;
                break;
            case NI_System_Math_Log:
                __jitExceptionResult727 = VNF_Log;
                break;
            case NI_System_Math_Log2:
                __jitExceptionResult727 = VNF_Log2;
                break;
            case NI_System_Math_Log10:
                __jitExceptionResult727 = VNF_Log10;
                break;
            case NI_System_Math_Round when type is TYP_DOUBLE:
                __jitExceptionResult727 = VNF_RoundDouble;
                break;
            case NI_System_Math_Round when type is TYP_INT:
                __jitExceptionResult727 = VNF_RoundInt32;
                break;
            case NI_System_Math_Round when type is TYP_FLOAT:
                __jitExceptionResult727 = VNF_RoundSingle;
                break;
            case NI_System_Math_Sin:
                __jitExceptionResult727 = VNF_Sin;
                break;
            case NI_System_Math_Sinh:
                __jitExceptionResult727 = VNF_Sinh;
                break;
            case NI_System_Math_Sqrt:
                __jitExceptionResult727 = VNF_Sqrt;
                break;
            case NI_System_Math_Tan:
                __jitExceptionResult727 = VNF_Tan;
                break;
            case NI_System_Math_Tanh:
                __jitExceptionResult727 = VNF_Tanh;
                break;
            case NI_System_Math_Truncate:
                __jitExceptionResult727 = VNF_Truncate;
                break;
            case NI_PRIMITIVE_LeadingZeroCount:
                __jitExceptionResult727 = VNF_LeadingZeroCount;
                break;
            case NI_PRIMITIVE_TrailingZeroCount:
                __jitExceptionResult727 = VNF_TrailingZeroCount;
                break;
            case NI_PRIMITIVE_PopCount:
                __jitExceptionResult727 = VNF_PopCount;
                break;
            default:
                if (Configuration.DETAILED_FATAL_ERROR_MESSAGES)
                {
                    JitExceptionHelpers.ThrowInvalidOperationException(
                        FatalJitErrorCode.UnsupportedUnaryMathIntrinsicArgument,
                        intrinsic);
                }
                else
                {
                    JitExceptionHelpers.ThrowInvalidOperationException(
                        FatalJitErrorCode.UnsupportedUnaryMathIntrinsicArgument);
                }

                __jitExceptionResult727 = default;
                break;
        }
        var func = __jitExceptionResult727;

        return VNForFunc(type, func, argument);
    }

    public ValueNum EvalMathFuncBinary(var_types type, NamedIntrinsic intrinsic, ValueNum left, ValueNum right)
    {
        assert(varTypeIsArithmetic(type));
        assert((left == VNNormalValue(left)) && (right == VNNormalValue(right)));
        assert(_compiler.IsMathIntrinsic(intrinsic));

        if (IsVNConstant(left) && IsVNConstant(right) &&
            (!_compiler.IsAot || _compiler.IsTargetIntrinsic(intrinsic)))
        {
            if (type is TYP_DOUBLE)
            {
                assert((TypeOfVN(left) == type) && (TypeOfVN(right) == type));
                var leftDouble = GetConstantDouble(left);
                var rightDouble = GetConstantDouble(right);
                double __jitExceptionResult728;
                switch (intrinsic)
                {
                    case NI_System_Math_Atan2:
                        __jitExceptionResult728 = Math.Atan2(leftDouble, rightDouble);
                        break;
                    // Match the ARM CRT subnormal fixup: https://github.com/dotnet/runtime/issues/12139.
                    case // Match the ARM CRT subnormal fixup: https://github.com/dotnet/runtime/issues/12139.
                    NI_System_Math_Pow:
                        __jitExceptionResult728 = (Configuration.TARGET_ARM || Configuration.TARGET_ARM64) && (rightDouble == 1.0) ? leftDouble : Math.Pow(leftDouble, rightDouble);
                        break;
                    case NI_System_Math_MaxNative when Configuration.TARGET_RISCV64 || Configuration.TARGET_WASM:
                        __jitExceptionResult728 = EvaluateMathMinMaxNative(leftDouble, rightDouble, true);
                        break;
                    case NI_System_Math_MinNative when Configuration.TARGET_RISCV64 || Configuration.TARGET_WASM:
                        __jitExceptionResult728 = EvaluateMathMinMaxNative(leftDouble, rightDouble, false);
                        break;
                    default:
                        if (Configuration.DETAILED_FATAL_ERROR_MESSAGES)
                        {
                            JitExceptionHelpers.ThrowInvalidOperationException(
                                FatalJitErrorCode.UnsupportedBinaryDoubleIntrinsicArgument,
                                intrinsic);
                        }
                        else
                        {
                            JitExceptionHelpers.ThrowInvalidOperationException(
                                FatalJitErrorCode.UnsupportedBinaryDoubleIntrinsicArgument);
                        }

                        __jitExceptionResult728 = default;
                        break;
                }
                var result = __jitExceptionResult728;
                return VNForDoubleCon(result);
            }

            if (type is TYP_FLOAT)
            {
                assert((TypeOfVN(left) == type) && (TypeOfVN(right) == type));
                var leftSingle = GetConstantSingle(left);
                var rightSingle = GetConstantSingle(right);
                float __jitExceptionResult729;
                switch (intrinsic)
                {
                    case NI_System_Math_Atan2:
                        __jitExceptionResult729 = MathF.Atan2(leftSingle, rightSingle);
                        break;
                    case NI_System_Math_Pow:
                        __jitExceptionResult729 = (Configuration.TARGET_ARM || Configuration.TARGET_ARM64) && (rightSingle == 1.0f) ? leftSingle : MathF.Pow(leftSingle, rightSingle);
                        break;
                    case NI_System_Math_MaxNative when Configuration.TARGET_RISCV64 || Configuration.TARGET_WASM:
                        __jitExceptionResult729 = EvaluateMathMinMaxNative(leftSingle, rightSingle, true);
                        break;
                    case NI_System_Math_MinNative when Configuration.TARGET_RISCV64 || Configuration.TARGET_WASM:
                        __jitExceptionResult729 = EvaluateMathMinMaxNative(leftSingle, rightSingle, false);
                        break;
                    default:
                        if (Configuration.DETAILED_FATAL_ERROR_MESSAGES)
                        {
                            JitExceptionHelpers.ThrowInvalidOperationException(
                                FatalJitErrorCode.UnsupportedBinaryFloatIntrinsicArgument,
                                intrinsic);
                        }
                        else
                        {
                            JitExceptionHelpers.ThrowInvalidOperationException(
                                FatalJitErrorCode.UnsupportedBinaryFloatIntrinsicArgument);
                        }

                        __jitExceptionResult729 = default;
                        break;
                }
                var result = __jitExceptionResult729;
                return VNForFloatCon(result);
            }

            if (Configuration.TARGET_RISCV64)
            {
                assert((TypeOfVN(left) == type) && (TypeOfVN(right) == type));
                var leftValue = GetConstantInt64(left);
                var rightValue = GetConstantInt64(right);
                long __jitExceptionResult730;
                switch (intrinsic)
                {
                    case NI_System_Math_Min:
                        __jitExceptionResult730 = leftValue < rightValue ? leftValue : rightValue;
                        break;
                    case NI_System_Math_Max:
                        __jitExceptionResult730 = leftValue > rightValue ? leftValue : rightValue;
                        break;
                    case NI_System_Math_MinUnsigned:
                        __jitExceptionResult730 = unchecked((ulong)leftValue) < unchecked((ulong)rightValue) ? leftValue : rightValue;
                        break;
                    case NI_System_Math_MaxUnsigned:
                        __jitExceptionResult730 = unchecked((ulong)leftValue) > unchecked((ulong)rightValue) ? leftValue : rightValue;
                        break;
                    default:
                        JitExceptionHelpers.ThrowUnreachableException();
                        __jitExceptionResult730 = default;
                        break;
                }
                var integerResult = __jitExceptionResult730;

                return type is TYP_LONG or TYP_ULONG
                    ? VNForLongCon(integerResult)
                    : VNForIntCon(unchecked((int)integerResult));
            }

            JitExceptionHelpers.ThrowUnreachableException();
        }

        global::RyuJitSharp.VNFunc __jitExceptionResult731;
        switch (intrinsic)
        {
            case NI_System_Math_Atan2:
                __jitExceptionResult731 = VNF_Atan2;
                break;
            case NI_System_Math_Max when Configuration.TARGET_RISCV64:
                __jitExceptionResult731 = VNF_MaxInt;
                break;
            case NI_System_Math_MaxUnsigned when Configuration.TARGET_RISCV64:
                __jitExceptionResult731 = VNF_MaxInt_UN;
                break;
            case NI_System_Math_Min when Configuration.TARGET_RISCV64:
                __jitExceptionResult731 = VNF_MinInt;
                break;
            case NI_System_Math_MinUnsigned when Configuration.TARGET_RISCV64:
                __jitExceptionResult731 = VNF_MinInt_UN;
                break;
            case NI_System_Math_MaxNative when Configuration.TARGET_WASM:
                __jitExceptionResult731 = VNF_Max;
                break;
            case NI_System_Math_MinNative when Configuration.TARGET_WASM:
                __jitExceptionResult731 = VNF_Min;
                break;
            case NI_System_Math_MaxNative when Configuration.TARGET_RISCV64:
                __jitExceptionResult731 = VNF_MaxNumber;
                break;
            case NI_System_Math_MinNative when Configuration.TARGET_RISCV64:
                __jitExceptionResult731 = VNF_MinNumber;
                break;
            case NI_System_Math_Pow:
                __jitExceptionResult731 = VNF_Pow;
                break;
            default:
                if (Configuration.DETAILED_FATAL_ERROR_MESSAGES)
                {
                    JitExceptionHelpers.ThrowInvalidOperationException(
                        FatalJitErrorCode.UnsupportedBinaryMathIntrinsicArgument,
                        intrinsic);
                }
                else
                {
                    JitExceptionHelpers.ThrowInvalidOperationException(
                        FatalJitErrorCode.UnsupportedBinaryMathIntrinsicArgument);
                }

                __jitExceptionResult731 = default;
                break;
        }
        var func = __jitExceptionResult731;
        return VNForFunc(type, func, left, right);
    }

    private static T EvaluateMathMinMaxNative<T>(T left, T right, bool maximum)
        where T : IFloatingPointIeee754<T>
    {
        if (!Configuration.TARGET_RISCV64 && !Configuration.TARGET_WASM)
        {
            JitExceptionHelpers.ThrowPlatformNotSupportedException(FatalJitErrorCode.NativeMinMaxEvaluationIsOnlySupportedOnRISCVAndWasm);
        }

        if (left != right)
        {
            var isNaN = Configuration.TARGET_WASM ? T.IsNaN(left) : T.IsNaN(right);
            if (!isNaN)
            {
                return maximum
                    ? (right < left ? left : right)
                    : (left < right ? left : right);
            }

            return left;
        }
        return T.IsNegative(maximum ? right : left) ? left : right;
    }

    private byte[] GetSimdArgumentBytes(var_types type, var_types baseType, ValueNum operand)
    {
        EnsureSimdSupported();
        if (varTypeIsSimd(TypeOfVN(operand)))
        {
            var input = GetConstantSimd(operand);
            return input.AsSpan<byte>()[..type.Size].ToArray();
        }

        var scalar = baseType switch {
            TYP_FLOAT => BitConverter.GetBytes(GetConstantSingle(operand)),
            TYP_DOUBLE => BitConverter.GetBytes(GetConstantDouble(operand)),
            TYP_LONG or TYP_ULONG => BitConverter.GetBytes(GetConstantInt64(operand)),
            _ => BitConverter.GetBytes(GetConstantInt32(operand)),
        };
        var result = new byte[type.Size];
        for (var index = 0; index <= result.Length - baseType.Size; index += baseType.Size)
        {
            scalar.AsSpan(0, baseType.Size).CopyTo(result.AsSpan(index));
        }
        return result;
    }

    private ValueNum EvaluateUnarySimdVN(genTreeOps oper, bool scalar, var_types type, var_types baseType, ValueNum operand)
    {
        var input = GetSimdArgumentBytes(type, baseType, operand);
        var result = new byte[type.Size];
        EvaluateUnarySimd(oper, scalar, baseType, result, input);
        return VNForGenericCon(type, result);
    }

    private ValueNum EvaluateBinarySimdVN(genTreeOps oper, bool scalar, var_types type, var_types baseType,
        ValueNum left, ValueNum right)
    {
        var leftVector = GetSimdArgumentBytes(type, baseType, left);
        var rightVector = GetSimdArgumentBytes(type, baseType, right);
        var result = new byte[type.Size];
        EvaluateBinarySimd(oper, scalar, baseType, result, leftVector, rightVector, type.Size);
        return VNForGenericCon(type, result);
    }

    private ValueNum EvaluateSimdGetElementVN(var_types baseType, ValueNum vector, int index)
    {
        var input = GetConstantSimd(vector);
        var elementSize = baseType.Size;
        return VNForGenericCon(baseType, input.AsSpan<byte>().Slice(index * elementSize, elementSize));
    }

    private ValueNum VNOneForSimdType(var_types type, var_types baseType)
    {
        var one = VNOneForType(baseType);
        return VNBroadcastForSimdType(type, baseType, one);
    }

    private bool VNIsVectorNaN(var_types type, var_types baseType, ValueNum vn)
    {
        if (!IsVNConstant(vn) || !varTypeIsSimd(TypeOfVN(vn)))
        {
            return false;
        }
        var input = GetConstantSimd(vn);
        var bytes = input.AsSpan<byte>()[..type.Size];
        for (var index = 0; index <= bytes.Length - baseType.Size; index += baseType.Size)
        {
            if (baseType is TYP_FLOAT)
            {
                if (!float.IsNaN(BitConverter.ToSingle(bytes.Slice(index, 4))))
                {
                    return false;
                }
            }
            else if (!double.IsNaN(BitConverter.ToDouble(bytes.Slice(index, 8))))
            {
                return false;
            }
        }
        return true;
    }

    private bool VNIsVectorNegativeZero(var_types type, var_types baseType, ValueNum vn)
    {
        if (!IsVNConstant(vn) || !varTypeIsSimd(TypeOfVN(vn)))
        {
            return false;
        }
        var input = GetConstantSimd(vn);
        var bytes = input.AsSpan<byte>()[..type.Size];
        for (var index = 0; index <= bytes.Length - baseType.Size; index += baseType.Size)
        {
            if (baseType is TYP_FLOAT)
            {
                if (BitConverter.ToInt32(bytes.Slice(index, 4)) != int.MinValue)
                {
                    return false;
                }
            }
            else if (BitConverter.ToInt64(bytes.Slice(index, 8)) != long.MinValue)
            {
                return false;
            }
        }
        return true;
    }

    public ValueNum EvalHWIntrinsicFunUnary(GenTreeHWIntrinsic tree, VNFunc func, ValueNum arg0VN,
        ValueNum resultTypeVN)
    {
        EnsureHardwareIntrinsicsSupported();
        var type = tree.Type;
        var baseType = tree.SimdBaseType;
        var simdSize = tree.SimdSize;
        var id = tree.HWIntrinsicId;

        if (IsVNConstant(arg0VN))
        {
            var oper = tree.GetOperForHWIntrinsicId(out var scalar);
            if (oper != GT_NONE)
            {
                if (type is TYP_MASK)
                {
                    if (!Configuration.FEATURE_MASKED_HW_INTRINSICS)
                    {
                        JitExceptionHelpers.ThrowPlatformNotSupportedException(FatalJitErrorCode.MaskedHardwareIntrinsicsAreNotEnabledForThisTarget);
                    }

                    if (Configuration.TARGET_ARM64 && Arm64Data.IsScalableSimdMask(this, arg0VN))
                    {
                        return VNForFunc(type, func, arg0VN, resultTypeVN);
                    }

                    var result = default(simdmask_t);
                    EvaluateUnaryMask(oper, scalar, baseType, simdSize, ref result, GetConstantSimdMask(arg0VN));
                    return VNForSimdMaskCon(result);
                }
                return EvaluateUnarySimdVN(oper, scalar, type, baseType, arg0VN);
            }

            if (tree.IsConvertMaskToVector)
            {
                var result = EvaluateSimdCvtMaskToVectorVN(type, baseType, arg0VN);
                return result != NoVN ? result : VNForFunc(type, func, arg0VN, resultTypeVN);
            }

            if (tree.IsConvertVectorToMask)
            {
                var vectorType = Compiler.GetSimdTypeForSize(simdSize);
                var result = EvaluateSimdCvtVectorToMaskVN(vectorType, baseType, arg0VN);
                return result != NoVN ? result : VNForFunc(type, func, arg0VN, resultTypeVN);
            }

            switch (id)
            {
                case NI_Vector_ExtractMostSignificantBits:
                case NI_X86Base_MoveMask when Configuration.TARGET_XARCH:
                case NI_AVX_MoveMask when Configuration.TARGET_XARCH:
                case NI_AVX2_MoveMask when Configuration.TARGET_XARCH:
                case NI_PackedSimd_Bitmask when Configuration.TARGET_WASM:
                {
                    if (!Configuration.FEATURE_MASKED_HW_INTRINSICS && !Configuration.TARGET_WASM)
                    {
                        JitExceptionHelpers.ThrowPlatformNotSupportedException(FatalJitErrorCode.MostSignificantBitExtractionIsNotSupportedForThisTarget);
                    }

                    assert((simdSize is 8 or 16) || (Configuration.TARGET_XARCH && (simdSize == 32)));
                    var input = GetConstantSimd(arg0VN);
                    var mask = default(simdmask_t);
                    EvaluateExtractMSB(baseType, ref mask, input.AsSpan<byte>()[..simdSize]);
                    var count = simdSize / baseType.Size;
                    assert(varTypeIsInt(type) && (count <= 32));
                    return VNForIntCon(unchecked((int)((ulong)mask.RawBits & (ulong)simdmask_t.GetBitMask(count))));
                }

                case NI_AVX512_MoveMask when Configuration.TARGET_XARCH:
                {
                    var count = simdSize / baseType.Size;
                    var mask = unchecked((ulong)GetConstantSimdMask(arg0VN).RawBits &
                        (ulong)simdmask_t.GetBitMask(count));
                    if (varTypeIsInt(type))
                    {
                        assert(count <= 32);
                        return VNForIntCon(unchecked((int)mask));
                    }

                    assert(varTypeIsLong(type));
                    return VNForLongCon(unchecked((long)mask));
                }

                case NI_ArmBase_LeadingZeroCount when Configuration.TARGET_ARM64:
                case NI_AVX2_LeadingZeroCount when Configuration.TARGET_XARCH:
                {
                    assert(!varTypeIsSmall(type) && !varTypeIsLong(type));
                    return VNForIntCon(BitOperations.LeadingZeroCount(unchecked((uint)GetConstantInt32(arg0VN))));
                }

                case NI_ArmBase_Arm64_LeadingZeroCount when Configuration.TARGET_ARM64:
                {
                    assert(varTypeIsInt(type));
                    return VNForIntCon(BitOperations.LeadingZeroCount(unchecked((ulong)GetConstantInt64(arg0VN))));
                }

                case NI_ArmBase_ReverseElementBits when Configuration.TARGET_ARM64:
                {
                    assert(!varTypeIsSmall(type) && !varTypeIsLong(type));
                    var value = unchecked((uint)GetConstantInt32(arg0VN));
                    return VNForIntCon(unchecked((int)Compiler.ReverseArm64Bits(value)));
                }

                case NI_ArmBase_Arm64_ReverseElementBits when Configuration.TARGET_ARM64:
                {
                    assert(varTypeIsLong(type));
                    var value = unchecked((ulong)GetConstantInt64(arg0VN));
                    return VNForLongCon(unchecked((long)Compiler.ReverseArm64Bits(value)));
                }

                case NI_Vector_ToVector128 when Configuration.TARGET_ARM64:
                case NI_Vector_ToVector128Unsafe when Configuration.TARGET_ARM64:
                {
                    var result = default(simd16_t);
                    result.v64[0] = GetConstantSimd8(arg0VN);
                    return VNForSimd16Con(result);
                }

                case NI_AVX2_X64_LeadingZeroCount when Configuration.TARGET_XARCH:
                {
                    assert(varTypeIsLong(type));
                    return VNForLongCon(BitOperations.LeadingZeroCount(unchecked((ulong)GetConstantInt64(arg0VN))));
                }

                case NI_AVX512_LeadingZeroCount when Configuration.TARGET_XARCH:
                {
                    return EvaluateUnarySimdVN(GT_LZCNT, false, type, baseType, arg0VN);
                }

                case NI_AVX2_TrailingZeroCount when Configuration.TARGET_XARCH:
                {
                    assert(!varTypeIsSmall(type) && !varTypeIsLong(type));
                    return VNForIntCon(BitOperations.TrailingZeroCount(unchecked((uint)GetConstantInt32(arg0VN))));
                }

                case NI_AVX2_X64_TrailingZeroCount when Configuration.TARGET_XARCH:
                {
                    assert(varTypeIsLong(type));
                    return VNForLongCon(BitOperations.TrailingZeroCount(unchecked((ulong)GetConstantInt64(arg0VN))));
                }

                case NI_X86Base_PopCount when Configuration.TARGET_XARCH:
                {
                    assert(!varTypeIsSmall(type) && !varTypeIsLong(type));
                    return VNForIntCon(BitOperations.PopCount(unchecked((uint)GetConstantInt32(arg0VN))));
                }

                case NI_X86Base_X64_PopCount when Configuration.TARGET_XARCH:
                {
                    assert(varTypeIsLong(type));
                    return VNForLongCon(BitOperations.PopCount(unchecked((ulong)GetConstantInt64(arg0VN))));
                }

                case NI_X86Base_BitScanForward when Configuration.TARGET_XARCH:
                case NI_X86Base_BitScanReverse when Configuration.TARGET_XARCH:
                {
                    assert(!varTypeIsSmall(type) && !varTypeIsLong(type));
                    var input = unchecked((uint)GetConstantInt32(arg0VN));
                    if (input != 0)
                    {
                        return VNForIntCon(id is NI_X86Base_BitScanForward
                            ? BitOperations.TrailingZeroCount(input)
                            : BitOperations.Log2(input));
                    }
                    break;
                }

                case NI_X86Base_X64_BitScanForward when Configuration.TARGET_XARCH:
                case NI_X86Base_X64_BitScanReverse when Configuration.TARGET_XARCH:
                {
                    assert(varTypeIsLong(type));
                    var input = unchecked((ulong)GetConstantInt64(arg0VN));
                    if (input != 0)
                    {
                        return VNForLongCon(id is NI_X86Base_X64_BitScanForward
                            ? BitOperations.TrailingZeroCount(input)
                            : BitOperations.Log2(input));
                    }
                    break;
                }

                case NI_Vector_ToVector256 when Configuration.TARGET_XARCH:
                case NI_Vector_ToVector256Unsafe when Configuration.TARGET_XARCH:
                case NI_Vector_ToVector512 when Configuration.TARGET_XARCH:
                case NI_Vector_ToVector512Unsafe when Configuration.TARGET_XARCH:
                case NI_Vector_AsVector128Unsafe:
                {
                    var input = GetConstantSimd(arg0VN);
                    var result = new byte[type.Size];
                    input.AsSpan<byte>()[..TypeOfVN(arg0VN).Size].CopyTo(result);
                    return VNForGenericCon(type, result);
                }

                case NI_Vector_GetLower when Configuration.TARGET_ARM64 || Configuration.TARGET_XARCH:
                case NI_Vector_GetLower128 when Configuration.TARGET_XARCH:
                case NI_Vector_AsVector2:
                {
                    var input = GetConstantSimd(arg0VN);
                    return VNForGenericCon(type, input.AsSpan<byte>()[..type.Size]);
                }

                case NI_Vector_GetUpper when Configuration.TARGET_ARM64 || Configuration.TARGET_XARCH:
                {
                    var input = GetConstantSimd(arg0VN);
                    return VNForGenericCon(type, input.AsSpan<byte>().Slice(type.Size, type.Size));
                }

                case NI_Vector_AsVector3:
                {
                    var input = GetConstantSimd(arg0VN);
                    return VNForGenericCon(type, input.AsSpan<byte>()[..type.Size]);
                }

                case NI_Vector_ToScalar:
                {
                    return EvaluateSimdGetElementVN(baseType, arg0VN, 0);
                }
            }
        }

        return VNForFunc(type, func, arg0VN, resultTypeVN);
    }

    public ValueNum EvalHWIntrinsicFunBinary(GenTreeHWIntrinsic tree, VNFunc func, ValueNum arg0VN,
        ValueNum arg1VN, ValueNum resultTypeVN)
    {
        EnsureHardwareIntrinsicsSupported();
        var type = tree.Type;
        var baseType = tree.SimdBaseType;
        var simdSize = tree.SimdSize;
        var id = tree.HWIntrinsicId;
        var constant = NoVN;
        var variable = NoVN;

        if (IsVNConstant(arg0VN))
        {
            constant = arg0VN;
            if (!IsVNConstant(arg1VN))
            {
                variable = arg1VN;
            }
        }
        else
        {
            variable = arg0VN;
            if (IsVNConstant(arg1VN))
            {
                constant = arg1VN;
            }
        }

        if (variable == NoVN)
        {
            assert(IsVNConstant(arg0VN) && IsVNConstant(arg1VN));
            var operation = tree.GetOperForHWIntrinsicId(out var scalar);
            if (operation != GT_NONE)
            {
                assert(operation is not (GT_AND_NOT or GT_OR_NOT or GT_XOR_NOT));
                if (type is TYP_MASK)
                {
                    if (!Configuration.FEATURE_MASKED_HW_INTRINSICS)
                    {
                        JitExceptionHelpers.ThrowPlatformNotSupportedException(FatalJitErrorCode.MaskedHardwareIntrinsicsAreNotEnabledForThisTarget);
                    }

                    if (TypeOfVN(arg0VN) is TYP_MASK)
                    {
                        if (Configuration.TARGET_ARM64 &&
                            (Arm64Data.IsScalableSimdMask(this, arg0VN) ||
                                Arm64Data.IsScalableSimdMask(this, arg1VN)))
                        {
                            return VNForFunc(type, func, arg0VN, arg1VN, resultTypeVN);
                        }

                        var result = default(simdmask_t);
                        EvaluateBinaryMask(operation, scalar, baseType, simdSize, ref result,
                            GetConstantSimdMask(arg0VN), GetConstantSimdMask(arg1VN));
                        return VNForSimdMaskCon(result);
                    }
                    var vectorType = Compiler.GetSimdTypeForSize(simdSize);
                    var vector = EvaluateBinarySimdVN(operation, scalar, vectorType, baseType, arg0VN, arg1VN);
                    var mask = EvaluateSimdCvtVectorToMaskVN(vectorType, baseType, vector);
                    return mask != NoVN ? mask : VNForFunc(type, func, arg0VN, arg1VN, resultTypeVN);
                }
                if (operation is GT_LSH or GT_RSH or GT_RSZ)
                {
                    if (Configuration.TARGET_XARCH)
                    {
                        if ((TypeOfVN(arg1VN) is TYP_SIMD16) && !HWIntrinsicInfo.IsVariableShift(id))
                        {
                            // Xarch reads the lower 64 bits; -1 denotes a shift beyond the element width.
                            var shift = GetConstantSimd16(arg1VN).u64[0];
                            if (shift >= (ulong)(baseType.Size * 8))
                            {
                                shift = ulong.MaxValue;
                            }

                            arg1VN = baseType.Size == 8
                                ? VNForLongCon(unchecked((long)shift))
                                : VNForIntCon(unchecked((int)shift));
                        }
                    }
                    else if (Configuration.TARGET_ARM64)
                    {
                        var auxiliaryType = tree.AuxiliaryType;
                        if ((auxiliaryType != TYP_UNKNOWN) && (auxiliaryType.Size != baseType.Size))
                        {
                            assert(auxiliaryType == TYP_ULONG);
                            assert(type == TYP_SIMD16);
                            var argument = GetConstantSimd16(arg1VN);
                            var narrowed = Compiler.NarrowAndDuplicateSimdLong(baseType, argument);
                            arg1VN = VNForSimd16Con(narrowed);
                        }
                    }
                }
                return EvaluateBinarySimdVN(operation, scalar, type, baseType, arg0VN, arg1VN);
            }

            switch (id)
            {
                case NI_Vector_GetElement:
                {
                    var vectorType = TypeOfVN(arg0VN);
                    var index = GetConstantInt32(arg1VN);
                    if (unchecked((uint)index) < (uint)GenTreeVecCon.ElementCount(vectorType.Size, baseType))
                    {
                        return EvaluateSimdGetElementVN(baseType, arg0VN, index);
                    }
                    break;
                }

                case NI_AdvSimd_MultiplyByScalar when Configuration.TARGET_ARM64:
                case NI_AdvSimd_Arm64_MultiplyByScalar when Configuration.TARGET_ARM64:
                {
                    arg1VN = EvaluateSimdGetElementVN(baseType, arg1VN, 0);
                    return EvaluateBinarySimdVN(GT_MUL, false, type, baseType, arg0VN, arg1VN);
                }

                case NI_Vector_WithLower when Configuration.TARGET_ARM64 || Configuration.TARGET_XARCH:
                case NI_Vector_WithUpper when Configuration.TARGET_ARM64 || Configuration.TARGET_XARCH:
                {
                    var result = GetSimdArgumentBytes(type, baseType, arg0VN);
                    var input = GetSimdArgumentBytes(Compiler.GetSimdTypeForSize(simdSize / 2), baseType, arg1VN);
                    input.CopyTo(result, id is NI_Vector_WithUpper ? result.Length / 2 : 0);
                    return VNForGenericCon(type, result);
                }
            }
        }
        else if (constant != NoVN)
        {
            var operation = tree.GetOperForHWIntrinsicId(out var scalar);
            assert(operation is not (GT_AND_NOT or GT_OR_NOT or GT_XOR_NOT));
            if (scalar)
            {
                operation = GT_NONE;
            }

            switch (operation)
            {
                case GT_ADD:
                {
                    if (varTypeIsFloating(baseType))
                    {
                        if (VNIsVectorNaN(type, baseType, constant))
                        {
                            return constant;
                        }
                        if (VNIsVectorNegativeZero(type, baseType, constant))
                        {
                            // Adding +0 can change -0 to +0, but adding -0 preserves either sign.
                            return variable;
                        }
                    }
                    else if (constant == VNZeroForType(type))
                    {
                        return variable;
                    }
                    break;
                }

                case GT_AND:
                {
                    if (constant == VNZeroForType(type))
                    {
                        return constant;
                    }
                    if (constant == VNAllBitsForType(type, simdSize))
                    {
                        return variable;
                    }
                    break;
                }

                case GT_DIV:
                {
                    if (varTypeIsFloating(baseType) && VNIsVectorNaN(type, baseType, constant))
                    {
                        return constant;
                    }
                    var one = varTypeIsSimd(TypeOfVN(arg1VN))
                        ? VNOneForSimdType(type, baseType) : VNOneForType(baseType);
                    if (arg1VN == one)
                    {
                        return arg0VN;
                    }
                    break;
                }

                case GT_EQ:
                case GT_NE:
                {
                    var vectorType = Compiler.GetSimdTypeForSize(simdSize);
                    if (varTypeIsFloating(baseType) && VNIsVectorNaN(vectorType, baseType, constant))
                    {
                        return operation is GT_EQ ? VNZeroForType(type) :
                            VNAllBitsForType(type, simdSize / baseType.Size);
                    }
                    if (!varTypeIsFloating(baseType) &&
                        ((operation is GT_EQ && constant == VNAllBitsForType(type, simdSize)) ||
                         (operation is GT_NE && constant == VNZeroForType(type))) &&
                        IsVectorPerElementMask(variable, baseType, simdSize))
                    {
                        return variable;
                    }
                    break;
                }

                case GT_GT:
                case GT_GE:
                case GT_LT:
                case GT_LE:
                {
                    var vectorType = Compiler.GetSimdTypeForSize(simdSize);
                    if (varTypeIsFloating(baseType) && VNIsVectorNaN(vectorType, baseType, constant))
                    {
                        return VNZeroForType(type);
                    }
                    if (varTypeIsUnsigned(baseType) && constant == VNZeroForType(vectorType))
                    {
                        if ((operation is GT_GT && constant == arg0VN) ||
                            (operation is GT_LT && constant == arg1VN))
                        {
                            return VNZeroForType(type);
                        }
                        if ((operation is GT_GE && constant == arg1VN) ||
                            (operation is GT_LE && constant == arg0VN))
                        {
                            return VNAllBitsForType(type, simdSize / baseType.Size);
                        }
                    }
                    break;
                }

                case GT_MUL:
                {
                    if (!varTypeIsFloating(baseType))
                    {
                        var zero = VNZeroForType(TypeOfVN(constant));
                        if (constant == zero)
                        {
                            return zero;
                        }
                    }
                    else if (VNIsVectorNaN(type, baseType, constant))
                    {
                        return constant;
                    }
                    var one = varTypeIsSimd(TypeOfVN(constant))
                        ? VNOneForSimdType(type, baseType) : VNOneForType(baseType);
                    if (constant == one)
                    {
                        return variable;
                    }
                    break;
                }

                case GT_OR:
                {
                    if (constant == VNZeroForType(type))
                    {
                        return variable;
                    }
                    if (constant == VNAllBitsForType(type, simdSize))
                    {
                        return constant;
                    }
                    break;
                }

                case GT_ROL:
                case GT_ROR:
                case GT_LSH:
                case GT_RSH:
                case GT_RSZ:
                {
                    if (constant == VNZeroForType(TypeOfVN(constant)))
                    {
                        return constant == arg1VN ? variable : constant;
                    }
                    break;
                }

                case GT_SUB:
                {
                    if (varTypeIsFloating(baseType) && VNIsVectorNaN(type, baseType, constant))
                    {
                        return constant;
                    }
                    if (arg1VN == VNZeroForType(type))
                    {
                        return variable;
                    }
                    break;
                }

                case GT_XOR:
                {
                    if (constant == VNZeroForType(type))
                    {
                        return variable;
                    }
                    break;
                }
            }

            switch (id)
            {
                case NI_AdvSimd_MultiplyByScalar when Configuration.TARGET_ARM64:
                case NI_AdvSimd_Arm64_MultiplyByScalar when Configuration.TARGET_ARM64:
                {
                    assert((TypeOfVN(arg0VN) == type) && (TypeOfVN(arg1VN) == TYP_SIMD8));
                    if (!varTypeIsFloating(baseType))
                    {
                        if (constant == arg0VN)
                        {
                            if (constant == VNZeroForType(type))
                            {
                                return constant;
                            }
                        }
                        else
                        {
                            assert(constant == arg1VN);
                            var element = EvaluateSimdGetElementVN(baseType, arg1VN, 0);
                            if (element == VNZeroForType(baseType))
                            {
                                return VNZeroForType(type);
                            }
                        }
                    }
                    else if (constant == arg0VN)
                    {
                        if (VNIsVectorNaN(type, baseType, constant))
                        {
                            return constant;
                        }
                    }
                    else
                    {
                        assert(constant == arg1VN);
                        var element = EvaluateSimdGetElementVN(baseType, arg1VN, 0);
                        var value = baseType == TYP_FLOAT
                            ? GetConstantSingle(element)
                            : GetConstantDouble(element);
                        if (double.IsNaN(value))
                        {
                            return VNBroadcastForSimdType(type, baseType, element);
                        }
                    }

                    if (IsVNConstant(arg1VN))
                    {
                        var element = EvaluateSimdGetElementVN(baseType, arg1VN, 0);
                        if (element == VNOneForType(baseType))
                        {
                            return arg0VN;
                        }
                    }
                    break;
                }

                case NI_Vector_op_Equality:
                case NI_Vector_op_Inequality:
                {
                    if (varTypeIsFloating(baseType) &&
                        VNIsVectorNaN(Compiler.GetSimdTypeForSize(simdSize), baseType, constant))
                    {
                        return id is NI_Vector_op_Equality ? VNZeroForType(type) : VNOneForType(type);
                    }
                    break;
                }
            }
        }
        else if (arg0VN == arg1VN)
        {
            var operation = tree.GetOperForHWIntrinsicId(out var scalar);
            assert(operation is not (GT_AND_NOT or GT_OR_NOT or GT_XOR_NOT));
            if (scalar)
            {
                operation = GT_NONE;
            }

            switch (operation)
            {
                case GT_AND:
                case GT_OR:
                {
                    return arg0VN;
                }

                case GT_EQ:
                case GT_GE:
                case GT_LE:
                {
                    if (varTypeIsIntegral(baseType))
                    {
                        return VNAllBitsForType(type, simdSize / baseType.Size);
                    }
                    break;
                }

                case GT_GT:
                case GT_LT:
                case GT_NE:
                {
                    if (varTypeIsIntegral(baseType))
                    {
                        return VNZeroForType(type);
                    }
                    break;
                }

                case GT_SUB:
                {
                    if (!varTypeIsFloating(baseType))
                    {
                        return VNZeroForType(type);
                    }
                    break;
                }

                case GT_XOR:
                {
                    return VNZeroForType(type);
                }
            }

            if (varTypeIsIntegral(baseType))
            {
                if (id is NI_Vector_op_Equality)
                {
                    return VNOneForType(type);
                }
                if (id is NI_Vector_op_Inequality)
                {
                    return VNZeroForType(type);
                }
            }
        }

        return VNForFunc(type, func, arg0VN, arg1VN, resultTypeVN);
    }

    public ValueNum EvalHWIntrinsicFunTernary(GenTreeHWIntrinsic tree, VNFunc func, ValueNum arg0VN,
        ValueNum arg1VN, ValueNum arg2VN, ValueNum resultTypeVN)
    {
        EnsureHardwareIntrinsicsSupported();
        var type = tree.Type;
        var baseType = tree.SimdBaseType;
        var simdSize = tree.SimdSize;
        var id = tree.HWIntrinsicId;

        switch (id)
        {
            case NI_Vector_ConditionalSelect when Configuration.TARGET_XARCH || Configuration.TARGET_WASM:
            case NI_AdvSimd_BitwiseSelect when Configuration.TARGET_ARM64:
            case NI_Sve_ConditionalSelect when Configuration.TARGET_ARM64:
            {
                if (IsVNConstant(arg0VN))
                {
                    if (arg0VN == VNZeroForType(type))
                    {
                        return arg2VN;
                    }

                    if (arg0VN == VNAllBitsForType(type, simdSize))
                    {
                        return arg1VN;
                    }
                    if (IsVNConstant(arg1VN) && IsVNConstant(arg2VN))
                    {
                        if (Configuration.TARGET_ARM64 && (id == NI_Sve_ConditionalSelect))
                        {
                            assert(TypeOfVN(arg0VN) == TYP_MASK);
                            assert(type == TYP_SIMD16);

                            var maskVN = EvaluateSimdCvtMaskToVectorVN(type, baseType, arg0VN);
                            if (maskVN == NoVN)
                            {
                                break;
                            }

                            var trueVN = EvaluateBinarySimdVN(GT_AND, false, type, baseType, arg1VN, maskVN);
                            var falseVN = EvaluateBinarySimdVN(GT_AND_NOT, false, type, baseType, arg2VN, maskVN);
                            return EvaluateBinarySimdVN(GT_OR, false, type, baseType, trueVN, falseVN);
                        }

                        var selected = EvaluateBinarySimdVN(GT_AND, false, type, baseType, arg1VN, arg0VN);
                        var rejected = EvaluateBinarySimdVN(GT_AND_NOT, false, type, baseType, arg2VN, arg0VN);
                        return EvaluateBinarySimdVN(GT_OR, false, type, baseType, selected, rejected);
                    }
                }
                else if (arg1VN == arg2VN)
                {
                    return arg1VN;
                }
                break;
            }

            case NI_Vector_WithElement when Configuration.FEATURE_SIMD:
            {
                if (IsVNConstant(arg0VN) && IsVNConstant(arg1VN) && IsVNConstant(arg2VN))
                {
                    var index = GetConstantInt32(arg1VN);
                    if (unchecked((uint)index) < (uint)GenTreeVecCon.ElementCount(type.Size, baseType))
                    {
                        if (varTypeIsFloating(baseType))
                        {
                            var element = baseType is TYP_FLOAT ? GetConstantSingle(arg2VN) : GetConstantDouble(arg2VN);
                            return EvaluateSimdWithElementFloating(type, baseType, arg0VN, index, element);
                        }

                        assert(varTypeIsIntegral(baseType));
                        var value = varTypeIsLong(baseType) ? GetConstantInt64(arg2VN) : GetConstantInt32(arg2VN);

                        return EvaluateSimdWithElementIntegral(type, baseType, arg0VN, index, value);
                    }
                }
                break;
            }

            case NI_X86Base_BlendVariable when Configuration.TARGET_XARCH:
            case NI_AVX_BlendVariable when Configuration.TARGET_XARCH:
            case NI_AVX2_BlendVariable when Configuration.TARGET_XARCH:
            case NI_AVX512_BlendVariableMask when Configuration.TARGET_XARCH:
            {
                if (IsVNConstant(arg2VN))
                {
                    var maskType = tree.GetOp(3).Type;
                    if (arg2VN == VNZeroForType(maskType))
                    {
                        return arg0VN;
                    }
                    if (arg2VN == VNAllBitsForType(maskType, simdSize / baseType.Size))
                    {
                        return arg1VN;
                    }
                }
                else if (arg0VN == arg1VN)
                {
                    return arg0VN;
                }
                break;
            }

            default:
            {
                break;
            }
        }

        return VNForFunc(type, func, arg0VN, arg1VN, arg2VN, resultTypeVN);
    }

    private ValueNum EvaluateSimdWithElementIntegral(var_types simdType, var_types baseType,
        ValueNum vectorVN, int index, long value)
    {
        assert(varTypeIsIntegral(baseType));
        assert(simdType == TypeOfVN(vectorVN));
        assert(IsVNConstant(vectorVN));
        assert(unchecked((uint)index) < (uint)GenTreeVecCon.ElementCount(simdType.Size, baseType));

        switch (simdType)
        {
            case TYP_SIMD8:
            case TYP_SIMD12:
            case TYP_SIMD16:
            case TYP_SIMD32 when Configuration.TARGET_XARCH:
            case TYP_SIMD64 when Configuration.TARGET_XARCH:
            {
                var vector = GetConstantSimd(vectorVN);
                var result = new byte[simdType.Size];
                vector.AsSpan<byte>()[..simdType.Size].CopyTo(result);
                var element = BitConverter.GetBytes(value);
                element.AsSpan(0, baseType.Size).CopyTo(result.AsSpan(index * baseType.Size));

                return VNForGenericCon(simdType, result);
            }

            default:
            {
                JitExceptionHelpers.ThrowUnreachableException();
                return default;
            }
        }
    }

    // Keep the native double-valued call boundary: collapsing float -> double -> float
    // to a copy would skip signaling-NaN quieting.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private ValueNum EvaluateSimdWithElementFloating(var_types simdType, var_types baseType,
        ValueNum vectorVN, int index, double value)
    {
        assert(varTypeIsFloating(baseType));
        assert(IsVNConstant(vectorVN));
        assert(simdType == TypeOfVN(vectorVN));
        assert(unchecked((uint)index) < (uint)GenTreeVecCon.ElementCount(simdType.Size, baseType));

        var vector = GetConstantSimd(vectorVN);
        var result = new byte[simdType.Size];
        vector.AsSpan<byte>()[..simdType.Size].CopyTo(result);
        var element = baseType is TYP_FLOAT ? BitConverter.GetBytes((float)value) : BitConverter.GetBytes(value);
        element.CopyTo(result, index * baseType.Size);

        return VNForGenericCon(simdType, result);
    }
}
