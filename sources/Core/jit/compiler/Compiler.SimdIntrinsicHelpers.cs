// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public partial class Compiler
{
    public GenTreeHWIntrinsic gtNewSimdHWIntrinsicNode(
        var_types type,
        NamedIntrinsic hwIntrinsicId,
        var_types simdBaseType,
        byte simdSize,
        params GenTree[] operands)
    {
        if (!Configuration.FEATURE_HW_INTRINSICS || !Configuration.FEATURE_SIMD)
        {
            throw new FatalJitException(CORJIT_IMPLLIMITATION, "SIMD hardware intrinsics are unsupported on this target.");
        }

        return new GenTreeHWIntrinsic(type, hwIntrinsicId, simdBaseType, simdSize, operands);
    }

    public GenTreeHWIntrinsic gtNewScalarHWIntrinsicNode(
        var_types type,
        NamedIntrinsic hwIntrinsicId,
        params GenTree[] operands)
    {
        if (!Configuration.FEATURE_HW_INTRINSICS)
        {
            throw new FatalJitException(CORJIT_IMPLLIMITATION, "Hardware intrinsics are unsupported on this target.");
        }

        return new GenTreeHWIntrinsic(type, hwIntrinsicId, TYP_UNKNOWN, simdSize: 0, operands);
    }

    /// <summary>Creates a new simd CreateScalar node</summary>
    /// <param name="type">The return type of SIMD node being created</param>
    /// <param name="op1">The value of element 0 of the simd value</param>
    /// <param name="simdBaseType">The base type of SIMD type of the intrinsic</param>
    /// <param name="simdSize">The size of the SIMD type of the intrinsic</param>
    /// <returns>The created CreateScalar node</returns>
    public GenTree gtNewSimdCreateScalarNode(var_types type, GenTree op1, var_types simdBaseType, byte simdSize)
    {
        if (!Configuration.FEATURE_HW_INTRINSICS || !Configuration.FEATURE_SIMD)
        {
            throw new FatalJitException(CORJIT_IMPLLIMITATION, "SIMD scalar creation is unsupported on this target.");
        }

        if (op1.Oper.IsConst)
        {
            var vecCon = gtNewVconNode(type);

            if (op1.Oper.IsIntegralConst)
            {
                vecCon.SetElementIntegral(simdBaseType, 0, op1.AsIntConCommon().IntegralValue);
            }
            else
            {
                assert(op1.Oper.IsCnsFltOrDbl);
                vecCon.SetElementFloating(simdBaseType, 0, op1.AsDblCon().DconVal);
            }

            return vecCon;
        }

        NamedIntrinsic hwIntrinsicId;

        if (Configuration.TARGET_ARM64)
        {
            hwIntrinsicId = ((simdSize is 8) && (simdBaseType.Size is 8))
                ? NI_Vector_Create
                : NI_Vector_CreateScalar;
        }
        else if (Configuration.TARGET_XARCH || Configuration.TARGET_WASM)
        {
            hwIntrinsicId = NI_Vector_CreateScalar;
        }
        else
        {
            throw new FatalJitException(CORJIT_IMPLLIMITATION, "SIMD scalar creation is unsupported on this target.");
        }

        return gtNewSimdHWIntrinsicNode(type, hwIntrinsicId, simdBaseType, simdSize, op1);
    }

    /// <summary>Creates a new simd ToScalar node.</summary>
    /// <param name="type">The return type of SIMD node being created.</param>
    /// <param name="op1">The SIMD operand.</param>
    /// <param name="simdBaseType">The base type of SIMD type of the intrinsic.</param>
    /// <param name="simdSize">The size of the SIMD type of the intrinsic.</param>
    /// <returns>The created node that has the ToScalar implementation.</returns>
    public GenTree gtNewSimdToScalarNode(var_types type, GenTree op1, var_types simdBaseType, byte simdSize)
    {
        if (!Configuration.FEATURE_HW_INTRINSICS || !Configuration.FEATURE_SIMD)
        {
            throw new FatalJitException(CORJIT_IMPLLIMITATION, "SIMD scalar extraction is unsupported on this target.");
        }

        if (!Configuration.TARGET_XARCH && !Configuration.TARGET_ARM64 && !Configuration.TARGET_WASM)
        {
            throw new FatalJitException(CORJIT_IMPLLIMITATION, "SIMD scalar extraction is unsupported on this target.");
        }

        assert(varTypeIsArithmetic(type));

        assert(op1 is not null);
        assert(varTypeIsSimd(op1.Type));

        assert(varTypeIsArithmetic(simdBaseType));

        return gtNewSimdHWIntrinsicNode(type, NI_Vector_ToScalar, simdBaseType, simdSize, op1);
    }

    internal static TSimd NarrowAndDuplicateSimdLong<TSimd>(var_types baseType, in TSimd value)
        where TSimd : unmanaged
    {
        TSimd result = default;
        var source = value;
        var sourceWords = MemoryMarshal.Cast<TSimd, ulong>(MemoryMarshal.CreateReadOnlySpan(ref source, 1));
        var destination = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref result, 1));

        switch (baseType)
        {
            case TYP_BYTE:
            case TYP_UBYTE:
            {
                for (var index = 0; index < destination.Length; index++)
                {
                    destination[index] = (byte)Math.Min(sourceWords[index / 8], byte.MaxValue);
                }
                break;
            }

            case TYP_SHORT:
            case TYP_USHORT:
            {
                var elements = MemoryMarshal.Cast<byte, ushort>(destination);
                for (var index = 0; index < elements.Length; index++)
                {
                    elements[index] = (ushort)Math.Min(sourceWords[index / 4], ushort.MaxValue);
                }
                break;
            }

            case TYP_FLOAT:
            case TYP_INT:
            case TYP_UINT:
            {
                var elements = MemoryMarshal.Cast<byte, uint>(destination);
                for (var index = 0; index < elements.Length; index++)
                {
                    elements[index] = (uint)Math.Min(sourceWords[index / 2], uint.MaxValue);
                }
                break;
            }

            case TYP_DOUBLE:
            case TYP_LONG:
            case TYP_ULONG:
            {
                result = value;
                break;
            }

            default:
            {
                unreached();
                throw new System.Diagnostics.UnreachableException();
            }
        }

        return result;
    }

    internal static uint ReverseArm64Bits(uint value)
    {
        // Reverse each byte's 1-, 2-, and 4-bit groups, then reverse byte order.
        value = ((value & 0x5555_5555u) << 1) | ((value >> 1) & 0x5555_5555u);
        value = ((value & 0x3333_3333u) << 2) | ((value >> 2) & 0x3333_3333u);
        value = ((value & 0x0F0F_0F0Fu) << 4) | ((value >> 4) & 0x0F0F_0F0Fu);
        return BinaryPrimitives.ReverseEndianness(value);
    }

    internal static ulong ReverseArm64Bits(ulong value)
    {
        value = ((value & 0x5555_5555_5555_5555UL) << 1) | ((value >> 1) & 0x5555_5555_5555_5555UL);
        value = ((value & 0x3333_3333_3333_3333UL) << 2) | ((value >> 2) & 0x3333_3333_3333_3333UL);
        value = ((value & 0x0F0F_0F0F_0F0F_0F0FUL) << 4) | ((value >> 4) & 0x0F0F_0F0F_0F0F_0F0FUL);
        return BinaryPrimitives.ReverseEndianness(value);
    }
}
