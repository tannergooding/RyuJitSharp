// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Numerics;

namespace RyuJitSharp;

public sealed partial class ValueNumStore
{
    public ValueNumPair EvalMathFuncUnary(var_types type, NamedIntrinsic intrinsic, ValueNumPair argument)
        => new(EvalMathFuncUnary(type, intrinsic, argument.Liberal),
            EvalMathFuncUnary(type, intrinsic, argument.Conservative));

    public ValueNumPair EvalMathFuncBinary(var_types type, NamedIntrinsic intrinsic, ValueNumPair left,
        ValueNumPair right)
        => new(EvalMathFuncBinary(type, intrinsic, left.Liberal, right.Liberal),
            EvalMathFuncBinary(type, intrinsic, left.Conservative, right.Conservative));

#if FEATURE_SIMD
    public ValueNum VNForSimdType(int simdSize, var_types simdBaseType)
    {
        var baseTypeVN = VNForIntCon((int)simdBaseType);
        var sizeVN = VNForIntCon(simdSize);
        return VNForFunc(TYP_REF, VNF_SimdType, sizeVN, baseTypeVN);
    }
#endif

    public ValueNum EvalMathFuncUnary(var_types type, NamedIntrinsic intrinsic, ValueNum argument)
    {
#if !TARGET_XARCH
        throw new NotImplementedException("Math intrinsic VN evaluation is not ported for this target.");
#else
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
                var doubleResult = intrinsic switch {
                    NI_System_Math_Abs => Math.Abs(value),
                    NI_System_Math_Acos => Math.Acos(value),
                    NI_System_Math_Acosh => Math.Acosh(value),
                    NI_System_Math_Asin => Math.Asin(value),
                    NI_System_Math_Asinh => Math.Asinh(value),
                    NI_System_Math_Atan => Math.Atan(value),
                    NI_System_Math_Atanh => Math.Atanh(value),
                    NI_System_Math_Cbrt => Math.Cbrt(value),
                    NI_System_Math_Ceiling => Math.Ceiling(value),
                    NI_System_Math_Cos => Math.Cos(value),
                    NI_System_Math_Cosh => Math.Cosh(value),
                    NI_System_Math_Exp => Math.Exp(value),
                    NI_System_Math_Floor => Math.Floor(value),
                    NI_System_Math_Log => Math.Log(value),
                    NI_System_Math_Log2 => Math.Log2(value),
                    NI_System_Math_Log10 => Math.Log10(value),
                    NI_System_Math_Sin => Math.Sin(value),
                    NI_System_Math_Sinh => Math.Sinh(value),
                    NI_System_Math_Round => Math.Round(value),
                    NI_System_Math_Sqrt => Math.Sqrt(value),
                    NI_System_Math_Tan => Math.Tan(value),
                    NI_System_Math_Tanh => Math.Tanh(value),
                    NI_System_Math_Truncate => Math.Truncate(value),
                    _ => throw new InvalidOperationException($"Unsupported double math intrinsic: {intrinsic}"),
                };
                return VNForDoubleCon(doubleResult);
            }

            if (type is TYP_FLOAT)
            {
                assert(TypeOfVN(argument) == type);
                var value = GetConstantSingle(argument);
                var floatResult = intrinsic switch {
                    NI_System_Math_Abs => MathF.Abs(value),
                    NI_System_Math_Acos => MathF.Acos(value),
                    NI_System_Math_Acosh => MathF.Acosh(value),
                    NI_System_Math_Asin => MathF.Asin(value),
                    NI_System_Math_Asinh => MathF.Asinh(value),
                    NI_System_Math_Atan => MathF.Atan(value),
                    NI_System_Math_Atanh => MathF.Atanh(value),
                    NI_System_Math_Cbrt => MathF.Cbrt(value),
                    NI_System_Math_Ceiling => MathF.Ceiling(value),
                    NI_System_Math_Cos => MathF.Cos(value),
                    NI_System_Math_Cosh => MathF.Cosh(value),
                    NI_System_Math_Exp => MathF.Exp(value),
                    NI_System_Math_Floor => MathF.Floor(value),
                    NI_System_Math_Log => MathF.Log(value),
                    NI_System_Math_Log2 => MathF.Log2(value),
                    NI_System_Math_Log10 => MathF.Log10(value),
                    NI_System_Math_Sin => MathF.Sin(value),
                    NI_System_Math_Sinh => MathF.Sinh(value),
                    NI_System_Math_Round => MathF.Round(value),
                    NI_System_Math_Sqrt => MathF.Sqrt(value),
                    NI_System_Math_Tan => MathF.Tan(value),
                    NI_System_Math_Tanh => MathF.Tanh(value),
                    NI_System_Math_Truncate => MathF.Truncate(value),
                    _ => throw new InvalidOperationException($"Unsupported float math intrinsic: {intrinsic}"),
                };
                return VNForFloatCon(floatResult);
            }

            assert(type is TYP_INT or TYP_LONG);
            int result;
            if (intrinsic is NI_System_Math_ILogB or NI_System_Math_Round)
            {
                result = (intrinsic, TypeOfVN(argument)) switch {
                    (NI_System_Math_ILogB, TYP_DOUBLE) => Math.ILogB(GetConstantDouble(argument)),
                    (NI_System_Math_ILogB, TYP_FLOAT) => MathF.ILogB(GetConstantSingle(argument)),
                    (NI_System_Math_Round, TYP_DOUBLE) => unchecked((int)Math.Round(GetConstantDouble(argument))),
                    (NI_System_Math_Round, TYP_FLOAT) => unchecked((int)MathF.Round(GetConstantSingle(argument))),
                    _ => throw new InvalidOperationException($"Unsupported integral math source: {intrinsic}"),
                };
            }
            else
            {
                result = (intrinsic, TypeOfVN(argument)) switch {
                    (NI_PRIMITIVE_LeadingZeroCount, TYP_LONG) => BitOperations.LeadingZeroCount(unchecked((ulong)GetConstantInt64(argument))),
                    (NI_PRIMITIVE_LeadingZeroCount, TYP_INT) => BitOperations.LeadingZeroCount(unchecked((uint)GetConstantInt32(argument))),
                    (NI_PRIMITIVE_TrailingZeroCount, TYP_LONG) => BitOperations.TrailingZeroCount(unchecked((ulong)GetConstantInt64(argument))),
                    (NI_PRIMITIVE_TrailingZeroCount, TYP_INT) => BitOperations.TrailingZeroCount(unchecked((uint)GetConstantInt32(argument))),
                    (NI_PRIMITIVE_PopCount, TYP_LONG) => BitOperations.PopCount(unchecked((ulong)GetConstantInt64(argument))),
                    (NI_PRIMITIVE_PopCount, TYP_INT) => BitOperations.PopCount(unchecked((uint)GetConstantInt32(argument))),
                    _ => throw new InvalidOperationException($"Unsupported bit-counting intrinsic: {intrinsic}"),
                };
            }

            return type is TYP_LONG ? VNForLongCon(result) : VNForIntCon(result);
        }

        var func = intrinsic switch {
            NI_System_Math_Abs => VNF_Abs,
            NI_System_Math_Acos => VNF_Acos,
            NI_System_Math_Acosh => VNF_Acosh,
            NI_System_Math_Asin => VNF_Asin,
            NI_System_Math_Asinh => VNF_Asinh,
            NI_System_Math_Atan => VNF_Atan,
            NI_System_Math_Atanh => VNF_Atanh,
            NI_System_Math_Cbrt => VNF_Cbrt,
            NI_System_Math_Ceiling => VNF_Ceiling,
            NI_System_Math_Cos => VNF_Cos,
            NI_System_Math_Cosh => VNF_Cosh,
            NI_System_Math_Exp => VNF_Exp,
            NI_System_Math_Floor => VNF_Floor,
            NI_System_Math_ILogB => VNF_ILogB,
            NI_System_Math_Log => VNF_Log,
            NI_System_Math_Log2 => VNF_Log2,
            NI_System_Math_Log10 => VNF_Log10,
            NI_System_Math_Round when type is TYP_DOUBLE => VNF_RoundDouble,
            NI_System_Math_Round when type is TYP_INT => VNF_RoundInt32,
            NI_System_Math_Round when type is TYP_FLOAT => VNF_RoundSingle,
            NI_System_Math_Sin => VNF_Sin,
            NI_System_Math_Sinh => VNF_Sinh,
            NI_System_Math_Sqrt => VNF_Sqrt,
            NI_System_Math_Tan => VNF_Tan,
            NI_System_Math_Tanh => VNF_Tanh,
            NI_System_Math_Truncate => VNF_Truncate,
            NI_PRIMITIVE_LeadingZeroCount => VNF_LeadingZeroCount,
            NI_PRIMITIVE_TrailingZeroCount => VNF_TrailingZeroCount,
            NI_PRIMITIVE_PopCount => VNF_PopCount,
            _ => throw new InvalidOperationException($"Unsupported unary math intrinsic: {intrinsic}"),
        };

        return VNForFunc(type, func, argument);
#endif
    }

    public ValueNum EvalMathFuncBinary(var_types type, NamedIntrinsic intrinsic, ValueNum left, ValueNum right)
    {
#if !TARGET_XARCH
        throw new NotImplementedException("Math intrinsic VN evaluation is not ported for this target.");
#else
        assert(varTypeIsArithmetic(type));
        assert((left == VNNormalValue(left)) && (right == VNNormalValue(right)));
        assert(_compiler.IsMathIntrinsic(intrinsic));

        if (IsVNConstant(left) && IsVNConstant(right) &&
            (!_compiler.IsAot || _compiler.IsTargetIntrinsic(intrinsic)))
        {
            if (type is TYP_DOUBLE)
            {
                assert((TypeOfVN(left) == type) && (TypeOfVN(right) == type));
                var result = intrinsic switch {
                    NI_System_Math_Atan2 => Math.Atan2(GetConstantDouble(left), GetConstantDouble(right)),
                    NI_System_Math_Pow => Math.Pow(GetConstantDouble(left), GetConstantDouble(right)),
                    _ => throw new InvalidOperationException($"Unsupported binary double intrinsic: {intrinsic}"),
                };
                return VNForDoubleCon(result);
            }

            if (type is TYP_FLOAT)
            {
                assert((TypeOfVN(left) == type) && (TypeOfVN(right) == type));
                var result = intrinsic switch {
                    NI_System_Math_Atan2 => MathF.Atan2(GetConstantSingle(left), GetConstantSingle(right)),
                    NI_System_Math_Pow => MathF.Pow(GetConstantSingle(left), GetConstantSingle(right)),
                    _ => throw new InvalidOperationException($"Unsupported binary float intrinsic: {intrinsic}"),
                };
                return VNForFloatCon(result);
            }

            unreached();
            return NoVN;
        }

        var func = intrinsic switch {
            NI_System_Math_Atan2 => VNF_Atan2,
            NI_System_Math_Pow => VNF_Pow,
            _ => throw new InvalidOperationException($"Unsupported binary math intrinsic: {intrinsic}"),
        };
        return VNForFunc(type, func, left, right);
#endif
    }

#if FEATURE_HW_INTRINSICS
    private byte[] GetSimdArgumentBytes(var_types type, var_types baseType, ValueNum operand)
    {
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
        for (var index = 0; index < result.Length; index += baseType.Size)
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
        var scalar = baseType switch {
            TYP_FLOAT => BitConverter.GetBytes(1.0f),
            TYP_DOUBLE => BitConverter.GetBytes(1.0),
            TYP_LONG or TYP_ULONG => BitConverter.GetBytes(1L),
            _ => BitConverter.GetBytes(1),
        };
        var result = new byte[type.Size];
        for (var index = 0; index < result.Length; index += baseType.Size)
        {
            scalar.AsSpan(0, baseType.Size).CopyTo(result.AsSpan(index));
        }
        return VNForGenericCon(type, result);
    }

    private bool VNIsVectorNaN(var_types type, var_types baseType, ValueNum vn)
    {
        if (!IsVNConstant(vn) || !varTypeIsSimd(TypeOfVN(vn)))
        {
            return false;
        }
        var input = GetConstantSimd(vn);
        var bytes = input.AsSpan<byte>()[..type.Size];
        for (var index = 0; index < bytes.Length; index += baseType.Size)
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
        for (var index = 0; index < bytes.Length; index += baseType.Size)
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
#if !TARGET_XARCH
        throw new NotImplementedException("HW intrinsic VN unary folding is not ported for this target.");
#else
        var type = tree.Type;
        var baseType = tree.SimdBaseType;
        var simdSize = tree.SimdSize;
        var id = tree.HWIntrinsicId;

        if (IsVNConstant(arg0VN))
        {
            var oper = tree.GetOperForHWIntrinsicId(out var scalar);
            if (oper != GT_NONE)
            {
#if FEATURE_MASKED_HW_INTRINSICS
                if (type is TYP_MASK)
                {
                    var result = default(simdmask_t);
                    EvaluateUnaryMask(oper, scalar, baseType, simdSize, ref result, GetConstantSimdMask(arg0VN));
                    return VNForSimdMaskCon(result);
                }
#endif
                return EvaluateUnarySimdVN(oper, scalar, type, baseType, arg0VN);
            }

            if (tree.IsConvertMaskToVector)
            {
                var result = new byte[type.Size];
                EvaluateSimdCvtMaskToVector(baseType, result, GetConstantSimdMask(arg0VN));
                return VNForGenericCon(type, result);
            }

            if (tree.IsConvertVectorToMask)
            {
                var value = GetConstantSimd(arg0VN);
                var result = default(simdmask_t);
                EvaluateSimdCvtVectorToMask(baseType, ref result, value.AsSpan<byte>()[..simdSize]);
                return VNForSimdMaskCon(result);
            }
#endif

            switch (id)
            {
#if FEATURE_MASKED_HW_INTRINSICS
                case NI_Vector_ExtractMostSignificantBits:
                case NI_X86Base_MoveMask:
                case NI_AVX_MoveMask:
                case NI_AVX2_MoveMask:
                {
                    var input = GetConstantSimd(arg0VN);
                    var mask = default(simdmask_t);
                    EvaluateExtractMSB(baseType, ref mask, input.AsSpan<byte>()[..simdSize]);
                    var count = simdSize / baseType.Size;
                    return VNForIntCon(unchecked((int)((ulong)mask.RawBits & (ulong)simdmask_t.GetBitMask(count))));
                }
#endif

                case NI_AVX512_MoveMask:
                {
                    var count = simdSize / baseType.Size;
                    var mask = (ulong)GetConstantSimdMask(arg0VN).RawBits & (ulong)simdmask_t.GetBitMask(count);
                    return varTypeIsInt(type) ? VNForIntCon(unchecked((int)mask)) : VNForLongCon(unchecked((long)mask));
                }

                case NI_AVX2_LeadingZeroCount:
                {
                    return VNForIntCon(BitOperations.LeadingZeroCount(unchecked((uint)GetConstantInt32(arg0VN))));
                }

                case NI_AVX2_X64_LeadingZeroCount:
                {
                    return VNForLongCon(BitOperations.LeadingZeroCount(unchecked((ulong)GetConstantInt64(arg0VN))));
                }

                case NI_AVX512_LeadingZeroCount:
                {
                    return EvaluateUnarySimdVN(GT_LZCNT, false, type, baseType, arg0VN);
                }

                case NI_AVX2_TrailingZeroCount:
                {
                    return VNForIntCon(BitOperations.TrailingZeroCount(unchecked((uint)GetConstantInt32(arg0VN))));
                }

                case NI_AVX2_X64_TrailingZeroCount:
                {
                    return VNForLongCon(BitOperations.TrailingZeroCount(unchecked((ulong)GetConstantInt64(arg0VN))));
                }

                case NI_X86Base_PopCount:
                {
                    return VNForIntCon(BitOperations.PopCount(unchecked((uint)GetConstantInt32(arg0VN))));
                }

                case NI_X86Base_X64_PopCount:
                {
                    return VNForLongCon(BitOperations.PopCount(unchecked((ulong)GetConstantInt64(arg0VN))));
                }

                case NI_X86Base_BitScanForward:
                case NI_X86Base_BitScanReverse:
                {
                    var input = unchecked((uint)GetConstantInt32(arg0VN));
                    if (input != 0)
                    {
                        return VNForIntCon(id is NI_X86Base_BitScanForward
                            ? BitOperations.TrailingZeroCount(input)
                            : BitOperations.Log2(input));
                    }
                    break;
                }

                case NI_X86Base_X64_BitScanForward:
                case NI_X86Base_X64_BitScanReverse:
                {
                    var input = unchecked((ulong)GetConstantInt64(arg0VN));
                    if (input != 0)
                    {
                        return VNForLongCon(id is NI_X86Base_X64_BitScanForward
                            ? BitOperations.TrailingZeroCount(input)
                            : BitOperations.Log2(input));
                    }
                    break;
                }

                case NI_Vector_ToVector256:
                case NI_Vector_ToVector256Unsafe:
                case NI_Vector_ToVector512:
                case NI_Vector_ToVector512Unsafe:
                case NI_Vector_AsVector128Unsafe:
                {
                    var input = GetConstantSimd(arg0VN);
                    return VNForGenericCon(type, input.AsSpan<byte>()[..type.Size]);
                }

                case NI_Vector_GetLower:
                case NI_Vector_GetLower128:
                case NI_Vector_AsVector2:
                {
                    var input = GetConstantSimd(arg0VN);
                    return VNForGenericCon(type, input.AsSpan<byte>()[..type.Size]);
                }

                case NI_Vector_GetUpper:
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
#endif
    }

    public ValueNum EvalHWIntrinsicFunBinary(GenTreeHWIntrinsic tree, VNFunc func, ValueNum arg0VN,
        ValueNum arg1VN, ValueNum resultTypeVN)
    {
#if !TARGET_XARCH
        throw new NotImplementedException("HW intrinsic VN binary folding is not ported for this target.");
#else
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
#if FEATURE_MASKED_HW_INTRINSICS
                if (type is TYP_MASK)
                {
                    if (TypeOfVN(arg0VN) is TYP_MASK)
                    {
                        var result = default(simdmask_t);
                        EvaluateBinaryMask(operation, scalar, baseType, simdSize, ref result,
                            GetConstantSimdMask(arg0VN), GetConstantSimdMask(arg1VN));
                        return VNForSimdMaskCon(result);
                    }
                    var vectorType = Compiler.GetSimdTypeForSize(simdSize);
                    var vector = EvaluateBinarySimdVN(operation, scalar, vectorType, baseType, arg0VN, arg1VN);
                    var input = GetConstantSimd(vector);
                    var mask = default(simdmask_t);
                    EvaluateSimdCvtVectorToMask(baseType, ref mask, input.AsSpan<byte>()[..simdSize]);
                    return VNForSimdMaskCon(mask);
                }
#endif
                if (operation is GT_LSH or GT_RSH or GT_RSZ &&
                    TypeOfVN(arg1VN) is TYP_SIMD16 && !HWIntrinsicInfo.IsVariableShift(id))
                {
                    // Xarch reads the lower 64 bits; -1 denotes a shift beyond the element width.
                    var shift = GetConstantSimd16(arg1VN).u64[0];
                    if (shift >= (ulong)(baseType.Size * 8))
                    {
                        shift = ulong.MaxValue;
                    }
                    arg1VN = baseType.Size == 8 ? VNForLongCon(unchecked((long)shift)) :
                        VNForIntCon(unchecked((int)shift));
                }
                return EvaluateBinarySimdVN(operation, scalar, type, baseType, arg0VN, arg1VN);
            }

            switch (id)
            {
                case NI_Vector_GetElement:
                {
                    var vectorType = TypeOfVN(arg0VN);
                    var index = GetConstantInt32(arg1VN);
                    if ((uint)index < (uint)GenTreeVecCon.ElementCount(vectorType.Size, baseType))
                    {
                        return EvaluateSimdGetElementVN(baseType, arg0VN, index);
                    }
                    break;
                }

                case NI_Vector_WithLower:
                case NI_Vector_WithUpper:
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
                        IsVectorPerElementMask(variable, baseType, simdSize))
                    {
                        if ((operation is GT_EQ && constant == VNAllBitsForType(type, simdSize)) ||
                            (operation is GT_NE && constant == VNZeroForType(type)))
                        {
                            return variable;
                        }
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

            if (varTypeIsFloating(baseType) &&
                VNIsVectorNaN(Compiler.GetSimdTypeForSize(simdSize), baseType, constant))
            {
                if (id is NI_Vector_op_Equality)
                {
                    return VNZeroForType(type);
                }
                if (id is NI_Vector_op_Inequality)
                {
                    return VNOneForType(type);
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
#endif
    }

#if FEATURE_MASKED_HW_INTRINSICS

    public ValueNum EvalHWIntrinsicFunTernary(GenTreeHWIntrinsic tree, VNFunc func, ValueNum arg0VN,
        ValueNum arg1VN, ValueNum arg2VN, ValueNum resultTypeVN)
    {
#if !TARGET_XARCH
        throw new NotImplementedException("HW intrinsic VN ternary folding is not ported for this target.");
#else
        var type = tree.Type;
        var baseType = tree.SimdBaseType;
        var simdSize = tree.SimdSize;

        switch (tree.HWIntrinsicId)
        {
            case NI_Vector_ConditionalSelect:
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

            case NI_Vector_WithElement:
            {
                if (IsVNConstant(arg0VN) && IsVNConstant(arg1VN) && IsVNConstant(arg2VN))
                {
                    var index = GetConstantInt32(arg1VN);
                    if ((uint)index < (uint)GenTreeVecCon.ElementCount(type.Size, baseType))
                    {
                        var vector = GetConstantSimd(arg0VN);
                        var result = new byte[type.Size];
                        vector.AsSpan<byte>()[..type.Size].CopyTo(result);
                        var scalar = baseType is TYP_FLOAT
                            ? VNForFloatCon(GetConstantSingle(arg2VN))
                            : baseType is TYP_DOUBLE ? VNForDoubleCon(GetConstantDouble(arg2VN)) : arg2VN;
                        var value = baseType switch {
                            TYP_FLOAT => BitConverter.GetBytes(GetConstantSingle(scalar)),
                            TYP_DOUBLE => BitConverter.GetBytes(GetConstantDouble(scalar)),
                            TYP_LONG or TYP_ULONG => BitConverter.GetBytes(GetConstantInt64(scalar)),
                            _ => BitConverter.GetBytes(GetConstantInt32(scalar)),
                        };
                        value.AsSpan(0, baseType.Size).CopyTo(result.AsSpan(index * baseType.Size));
                        return VNForGenericCon(type, result);
                    }
                }
                break;
            }

            case NI_X86Base_BlendVariable:
            case NI_AVX_BlendVariable:
            case NI_AVX2_BlendVariable:
            case NI_AVX512_BlendVariableMask:
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
        }

        return VNForFunc(type, func, arg0VN, arg1VN, arg2VN, resultTypeVN);
#endif
    }
#endif
}
