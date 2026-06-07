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

    public static int ElementCount(int simdSize, var_types simdBaseType)
    {
        return simdSize / simdBaseType.Size;
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
