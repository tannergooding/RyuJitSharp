// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_SIMD
using System;

namespace RyuJitSharp;

public sealed class GenTreeVecCon : GenTree
{
    private simd_t _simdVal;

    public GenTreeVecCon(var_types type)
        : base(GT_CNS_VEC, type)
    {
        assert(varTypeIsSimd(type));
    }

    public bool IsAllBitsSet => Type switch {
        TYP_SIMD8 => _simdVal.v64[0].IsAllBitsSet,
        TYP_SIMD12 => _simdVal.v64[0].IsAllBitsSet && (_simdVal.u32[2] == uint.MaxValue),
        TYP_SIMD16 => _simdVal.v128[0].IsAllBitsSet,
#if TARGET_XARCH
        TYP_SIMD32 => _simdVal.v256[0].IsAllBitsSet,
        TYP_SIMD64 => _simdVal.IsAllBitsSet,
#endif
        _ => false,
    };

    public bool IsZero => Type switch {
        TYP_SIMD8 => _simdVal.v64[0].IsZero,
        TYP_SIMD12 => _simdVal.v64[0].IsZero && (_simdVal.u32[2] == 0),
        TYP_SIMD16 => _simdVal.v128[0].IsZero,
#if TARGET_XARCH
        TYP_SIMD32 => _simdVal.v256[0].IsZero,
        TYP_SIMD64 => _simdVal.IsZero,
#endif
        _ => false,
    };

    public ref simd_t SimdVal => ref _simdVal;

    public static bool Equals(GenTreeVecCon left, GenTreeVecCon right)
    {
        if (left.Type != right.Type)
        {
            return false;
        }

        switch (left.Type)
        {
            case TYP_SIMD8:
            case TYP_SIMD12:
            case TYP_SIMD16:
#if TARGET_XARCH
            case TYP_SIMD32:
            case TYP_SIMD64:
#endif
            {
                // Compare active bytes, including NaN payloads and signed zero, but not SIMD12 padding.
                return left._simdVal.AsSpan<byte>()[..left.Type.Size].SequenceEqual(right._simdVal.AsSpan<byte>()[..right.Type.Size]);
            }

#if TARGET_ARM64
            case TYP_SIMD:
            {
                NYI("ARM64 scalable vector constant comparison");
                fatal(CORJIT_IMPLLIMITATION);
                return false;
            }
#endif

            default:
            {
                unreached();

                return false;
            }
        }
    }

    public static int ElementCount(int simdSize, var_types simdBaseType)
    {
        return simdSize / simdBaseType.Size;
    }

#if FEATURE_HW_INTRINSICS
    public static bool IsHWIntrinsicCreateConstant(GenTreeHWIntrinsic node, ref simd_t simdVal)
    {
        var intrinsic = node.HWIntrinsicId;
        var simdBaseType = node.SimdBaseType;
        var simdSize = node.SimdSize;
        var argCount = node.Operands.Length;
        var constantCount = 0;

        switch (intrinsic)
        {
            case NI_Vector_Create:
            case NI_Vector_CreateScalar:
            case NI_Vector_CreateScalarUnsafe:
            {
                simdVal = default;
                if ((argCount == 1) && HandleArgForHWIntrinsicCreate(node.GetOp(1), 0, ref simdVal, simdBaseType))
                {
                    // Native chooses a broadcast for CreateScalarUnsafe; CreateScalar leaves upper lanes zero.
                    if (intrinsic != NI_Vector_CreateScalar)
                    {
                        for (var index = 1; index < ElementCount(simdSize, simdBaseType); index++)
                        {
                            _ = HandleArgForHWIntrinsicCreate(node.GetOp(1), index, ref simdVal, simdBaseType);
                        }
                    }

                    constantCount = 1;
                }
                else
                {
                    for (var index = 1; index <= argCount; index++)
                    {
                        if (HandleArgForHWIntrinsicCreate(node.GetOp(index), index - 1, ref simdVal, simdBaseType))
                        {
                            constantCount++;
                        }
                    }
                }

                assert((argCount == 1) || (argCount == ElementCount(simdSize, simdBaseType)));
                return argCount == constantCount;
            }

            default:
            {
                return false;
            }
        }
    }

    public static bool HandleArgForHWIntrinsicCreate(GenTree arg, int argIndex, ref simd_t simdVal, var_types baseType)
    {
        switch (baseType)
        {
            case TYP_BYTE:
            case TYP_UBYTE:
            {
                if (arg.Oper.IsCnsIntOrI)
                {
                    simdVal.i8[argIndex] = unchecked((sbyte)arg.AsIntCon().IconValue);
                    return true;
                }

                assert(simdVal.i8[argIndex] == 0);
                break;
            }

            case TYP_SHORT:
            case TYP_USHORT:
            {
                if (arg.Oper.IsCnsIntOrI)
                {
                    simdVal.i16[argIndex] = unchecked((short)arg.AsIntCon().IconValue);
                    return true;
                }

                assert(simdVal.i16[argIndex] == 0);
                break;
            }

            case TYP_INT:
            case TYP_UINT:
            {
                if (arg.Oper.IsCnsIntOrI)
                {
                    simdVal.i32[argIndex] = unchecked((int)arg.AsIntCon().IconValue);
                    return true;
                }

                assert(simdVal.i32[argIndex] == 0);
                break;
            }

            case TYP_LONG:
            case TYP_ULONG:
            {
                if (arg.Oper.IsIntegralConst)
                {
                    simdVal.i64[argIndex] = arg.AsIntConCommon().IntegralValue;
                    return true;
                }
#if !TARGET_64BIT
                else if ((arg.Oper == GT_LONG) && arg.AsOp().Op1.Oper.IsCnsIntOrI && arg.AsOp().Op2.Oper.IsCnsIntOrI)
                {
                    var value = (long)arg.AsOp().Op2.AsIntCon().IconValue;
                    value <<= 32;
                    value |= unchecked((uint)arg.AsOp().Op1.AsIntCon().IconValue);
                    simdVal.i64[argIndex] = value;
                    return true;
                }
#endif

                assert(simdVal.i64[argIndex] == 0);
                break;
            }

            case TYP_FLOAT:
            {
                if (arg.Oper.IsCnsFltOrDbl)
                {
                    simdVal.f32[argIndex] = (float)arg.AsDblCon().DconVal;
                    return true;
                }

                // Test the bits so a negative zero is not mistaken for an untouched lane.
                assert(simdVal.i32[argIndex] == 0);
                break;
            }

            case TYP_DOUBLE:
            {
                if (arg.Oper.IsCnsFltOrDbl)
                {
                    simdVal.f64[argIndex] = arg.AsDblCon().DconVal;
                    return true;
                }

                assert(simdVal.i64[argIndex] == 0);
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }

        return false;
    }
#endif

    public void EvaluateBinaryInPlace(genTreeOps oper, bool scalar, var_types baseType, GenTreeVecCon other)
    {
        switch (Type)
        {
            case TYP_SIMD8:
            case TYP_SIMD12:
            case TYP_SIMD16:
#if TARGET_XARCH
            case TYP_SIMD32:
            case TYP_SIMD64:
#endif
            {
                simd_t result = default;
                var activeValue = _simdVal.AsSpan<byte>()[..Type.Size];
                var activeOther = other._simdVal.AsSpan<byte>()[..Type.Size];
                var activeResult = result.AsSpan<byte>()[..Type.Size];
                EvaluateBinarySimd(oper, scalar, baseType, activeResult, activeValue, activeOther, Type.Size);
                activeResult.CopyTo(activeValue);
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }
    }

    public bool TryEvaluateUnaryInPlace(genTreeOps oper, bool scalar, var_types baseType)
    {
        switch (Type)
        {
            case TYP_SIMD8:
            case TYP_SIMD12:
            case TYP_SIMD16:
#if TARGET_XARCH
            case TYP_SIMD32:
            case TYP_SIMD64:
#endif
            {
                simd_t result = default;
                var activeValue = _simdVal.AsSpan<byte>()[..Type.Size];
                var activeResult = result.AsSpan<byte>()[..Type.Size];
                EvaluateUnarySimd(oper, scalar, baseType, activeResult, activeValue);
                activeResult.CopyTo(activeValue);
                return true;
            }

#if TARGET_ARM64
            case TYP_SIMD:
            {
                NYI("ARM64 scalable vector unary evaluation");
                fatal(CORJIT_IMPLLIMITATION);
                return false;
            }
#endif

            default:
            {
                unreached();
                return false;
            }
        }
    }

    /// <summary>Evaluates this constant using a broadcast</summary>
    /// <param name="simdBaseType">the base type of the constant being checked</param>
    /// <param name="scalar">the value to broadcast as part of the evaluation</param>
    public void EvaluateBroadcastInPlace(var_types simdBaseType, double scalar)
    {
        var elementCount = ElementCount(Type.Size, simdBaseType);

        switch (simdBaseType)
        {
            case TYP_FLOAT:
            {
                _simdVal.AsSpan<float>()[..elementCount].Fill((float)(scalar));
                break;
            }

            case TYP_DOUBLE:
            {
                _simdVal.AsSpan<double>()[..elementCount].Fill(scalar);
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }
    }

    /// <summary>Evaluates this constant using a broadcast</summary>
    /// <param name="simdBaseType">the base type of the constant being checked</param>
    /// <param name="scalar">the value to broadcast as part of the evaluation</param>
    public void EvaluateBroadcastInPlace(var_types simdBaseType, long scalar)
    {
        var elementCount = ElementCount(Type.Size, simdBaseType);

        switch (simdBaseType)
        {
            case TYP_BYTE:
            {
                _simdVal.AsSpan<sbyte>()[..elementCount].Fill((sbyte)(scalar));
                break;
            }

            case TYP_UBYTE:
            {
                _simdVal.AsSpan<byte>()[..elementCount].Fill((byte)(scalar));
                break;
            }

            case TYP_SHORT:
            {
                _simdVal.AsSpan<short>()[..elementCount].Fill((short)(scalar));
                break;
            }

            case TYP_USHORT:
            {
                _simdVal.AsSpan<ushort>()[..elementCount].Fill((ushort)(scalar));
                break;
            }

            case TYP_INT:
            {
                _simdVal.AsSpan<int>()[..elementCount].Fill((int)(scalar));
                break;
            }

            case TYP_UINT:
            {
                _simdVal.AsSpan<uint>()[..elementCount].Fill((uint)(scalar));
                break;
            }

            case TYP_LONG:
            {
                _simdVal.AsSpan<long>()[..elementCount].Fill(scalar);
                break;
            }

            case TYP_ULONG:
            {
                _simdVal.AsSpan<ulong>()[..elementCount].Fill((ulong)(scalar));
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }
    }

    public double GetElementFloating(var_types simdBaseType, int index)
    {
        var elementCount = ElementCount(Type.Size, simdBaseType);

        switch (simdBaseType)
        {
            case TYP_FLOAT:
            {
                return _simdVal.AsSpan<float>()[..elementCount][index];
            }

            case TYP_DOUBLE:
            {
                return _simdVal.AsSpan<double>()[..elementCount][index];
            }

            default:
            {
                unreached();
                return default;
            }
        }
    }

    public long GetElementIntegral(var_types simdBaseType, int index)
    {
        var elementCount = ElementCount(Type.Size, simdBaseType);

        switch (simdBaseType)
        {
            case TYP_BYTE:
            {
                return _simdVal.AsSpan<sbyte>()[..elementCount][index];
            }

            case TYP_UBYTE:
            {
                return _simdVal.AsSpan<byte>()[..elementCount][index];
            }

            case TYP_SHORT:
            {
                return _simdVal.AsSpan<short>()[..elementCount][index];
            }

            case TYP_USHORT:
            {
                return _simdVal.AsSpan<ushort>()[..elementCount][index];
            }

            case TYP_INT:
            {
                return _simdVal.AsSpan<int>()[..elementCount][index];
            }

            case TYP_UINT:
            {
                return _simdVal.AsSpan<uint>()[..elementCount][index];
            }

            case TYP_LONG:
            case TYP_ULONG:
            {
                return _simdVal.AsSpan<long>()[..elementCount][index];
            }

            default:
            {
                unreached();
                return default;
            }
        }
    }

    public bool IsBroadcast(var_types simdBaseType)
    {
        var elementCount = ElementCount(Type.Size, simdBaseType);

        switch (simdBaseType)
        {
            case TYP_BYTE:
            case TYP_UBYTE:
            {
                return !_simdVal.AsSpan<byte>()[..elementCount].ContainsAnyExcept(_simdVal.u8[0]);
            }

            case TYP_SHORT:
            case TYP_USHORT:
            {
                return !_simdVal.AsSpan<ushort>()[..elementCount].ContainsAnyExcept(_simdVal.u16[0]);
            }

            case TYP_FLOAT:
            case TYP_INT:
            case TYP_UINT:
            {
                return !_simdVal.AsSpan<uint>()[..elementCount].ContainsAnyExcept(_simdVal.u32[0]);
            }

            case TYP_DOUBLE:
            case TYP_LONG:
            case TYP_ULONG:
            {
                return !_simdVal.AsSpan<ulong>()[..elementCount].ContainsAnyExcept(_simdVal.u64[0]);
            }

            default:
            {
                return false;
            }
        }
    }

    public bool IsElementZero(var_types simdBaseType, int index)
    {
        var integralType = simdBaseType switch {
            TYP_FLOAT => TYP_INT,
            TYP_DOUBLE => TYP_LONG,
            _ => simdBaseType,
        };

        return GetElementIntegral(integralType, index) == 0;
    }

    public bool IsElementOne(var_types simdBaseType, int index)
    {
        return varTypeIsFloating(simdBaseType)
            ? GetElementFloating(simdBaseType, index) == 1
            : GetElementIntegral(simdBaseType, index) == 1;
    }

    public bool IsScalarZero(var_types simdBaseType) => IsElementZero(simdBaseType, 0);

    public bool IsScalarOne(var_types simdBaseType) => IsElementOne(simdBaseType, 0);

    public bool IsNaN(var_types simdBaseType)
    {
        var elementCount = ElementCount(Type.Size, simdBaseType);

        for (var i = 0; i < elementCount; i++)
        {
            var element = GetElementFloating(simdBaseType, i);

            if (!double.IsNaN(element))
            {
                return false;
            }
        }
        return true;
    }

    public bool ContainsNaN(var_types simdBaseType)
    {
        assert(varTypeIsFloating(simdBaseType));
        var elementCount = ElementCount(Type.Size, simdBaseType);

        for (var i = 0; i < elementCount; i++)
        {
            if (double.IsNaN(GetElementFloating(simdBaseType, i)))
            {
                return true;
            }
        }

        return false;
    }

    public bool IsNegativeZero(var_types simdBaseType)
    {
        var elementCount = ElementCount(Type.Size, simdBaseType);

        for (var i = 0; i < elementCount; i++)
        {
            var element = GetElementFloating(simdBaseType, i);

            if ((element != 0.0) || !double.IsNegative(element))
            {
                return false;
            }
        }
        return true;
    }

    public bool ContainsNegativeZero(var_types simdBaseType)
    {
        assert(varTypeIsFloating(simdBaseType));
        var elementCount = ElementCount(Type.Size, simdBaseType);

        for (var i = 0; i < elementCount; i++)
        {
            var element = GetElementFloating(simdBaseType, i);

            if ((element == 0.0) && double.IsNegative(element))
            {
                return true;
            }
        }

        return false;
    }

    public bool ContainsPositiveZero(var_types simdBaseType)
    {
        assert(varTypeIsFloating(simdBaseType));
        var elementCount = ElementCount(Type.Size, simdBaseType);

        for (var i = 0; i < elementCount; i++)
        {
            var element = GetElementFloating(simdBaseType, i);

            if ((element == 0.0) && !double.IsNegative(element))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns an all-bits-set mask for lanes containing the requested signed zero.</summary>
    public simd_t GetFloatingZeroMask(var_types simdBaseType, bool isNegativeZero)
    {
        assert(varTypeIsFloating(simdBaseType));

        var elementCount = ElementCount(Type.Size, simdBaseType);
        var result = default(simd_t);

        switch (simdBaseType)
        {
            case TYP_FLOAT:
            {
                for (var i = 0; i < elementCount; i++)
                {
                    var element = GetElementFloating(simdBaseType, i);

                    if ((element == 0.0) && (double.IsNegative(element) == isNegativeZero))
                    {
                        result.u32[i] = uint.MaxValue;
                    }
                }

                break;
            }

            case TYP_DOUBLE:
            {
                for (var i = 0; i < elementCount; i++)
                {
                    var element = GetElementFloating(simdBaseType, i);

                    if ((element == 0.0) && (double.IsNegative(element) == isNegativeZero))
                    {
                        result.u64[i] = ulong.MaxValue;
                    }
                }

                break;
            }

            default:
            {
                unreached();
                break;
            }
        }

        return result;
    }

    public void SetElementFloating(var_types simdBaseType, int index, double value)
    {
        var elementCount = ElementCount(Type.Size, simdBaseType);

        switch (simdBaseType)
        {
            case TYP_FLOAT:
            {
                _simdVal.AsSpan<float>()[..elementCount][index] = (float)(value);
                break;
            }

            case TYP_DOUBLE:
            {
                _simdVal.AsSpan<double>()[..elementCount][index] = value;
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }
    }

    public void SetElementIntegral(var_types simdBaseType, int index, long value)
    {
        var elementCount = ElementCount(Type.Size, simdBaseType);

        switch (simdBaseType)
        {
            case TYP_BYTE:
            {
                _simdVal.AsSpan<sbyte>()[..elementCount][index] = (sbyte)(value);
                break;
            }

            case TYP_UBYTE:
            {
                _simdVal.AsSpan<byte>()[..elementCount][index] = (byte)(value);
                break;
            }

            case TYP_SHORT:
            {
                _simdVal.AsSpan<short>()[..elementCount][index] = (short)(value);
                break;
            }

            case TYP_USHORT:
            {
                _simdVal.AsSpan<ushort>()[..elementCount][index] = (ushort)(value);
                break;
            }

            case TYP_INT:
            {
                _simdVal.AsSpan<int>()[..elementCount][index] = (int)(value);
                break;
            }

            case TYP_UINT:
            {
                _simdVal.AsSpan<uint>()[..elementCount][index] = (uint)(value);
                break;
            }

            case TYP_LONG:
            case TYP_ULONG:
            {
                _simdVal.AsSpan<long>()[..elementCount][index] = value;
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }
    }
}
#endif
