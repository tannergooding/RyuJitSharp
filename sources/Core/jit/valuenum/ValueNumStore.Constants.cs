// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public sealed partial class ValueNumStore
{
    private Dictionary<nint, nint>? _embeddedToCompileTimeHandleMap;
    private Dictionary<ValueNum, FieldSeq?>? _fieldAddressToFieldSeqMap;

    public void AddToEmbeddedHandleMap(nint embeddedHandle, nint compileTimeHandle)
    {
        _embeddedToCompileTimeHandleMap ??= [];
        _embeddedToCompileTimeHandleMap[embeddedHandle] = compileTimeHandle;
    }

    public bool EmbeddedHandleMapLookup(nint embeddedHandle, ref nint compileTimeHandle)
    {
        if ((_embeddedToCompileTimeHandleMap is not null) && _embeddedToCompileTimeHandleMap.TryGetValue(embeddedHandle, out var handle))
        {
            compileTimeHandle = handle;
            return true;
        }

        return false;
    }

    public unsafe bool IsVNTypeHandle(ValueNum vn, out CORINFO_CLASS_HANDLE handle)
    {
        nint compileTimeHandle = 0;
        if (IsVNTypeHandle(vn) && EmbeddedHandleMapLookup(ConstantValue<nint>(vn), ref compileTimeHandle) &&
            (compileTimeHandle != 0))
        {
            handle = (CORINFO_CLASS_HANDLE)compileTimeHandle;
            return true;
        }

        handle = NO_CLASS_HANDLE;
        return false;
    }

    public void AddToFieldAddressToFieldSeqMap(ValueNum address, FieldSeq? fieldSeq)
    {
        _fieldAddressToFieldSeqMap ??= [];
        _fieldAddressToFieldSeqMap[address] = fieldSeq;
    }

    public FieldSeq? GetFieldSeqFromAddress(ValueNum address) => _fieldAddressToFieldSeqMap?.GetValueOrDefault(address);

    private Dictionary<simd8_t, ValueNum>? _simd8CnsMap;
    private Dictionary<simd12_t, ValueNum>? _simd12CnsMap;
    private Dictionary<simd16_t, ValueNum>? _simd16CnsMap;
    private Dictionary<simd32_t, ValueNum>? _simd32CnsMap;
    private Dictionary<simd64_t, ValueNum>? _simd64CnsMap;
    private Dictionary<simdmask_t, ValueNum>? _simdMaskCnsMap;

    private static void EnsureSimdSupported()
    {
        if (!Configuration.FEATURE_SIMD)
        {
            JitExceptionHelpers.ThrowPlatformNotSupportedException(FatalJitErrorCode.SIMDIsNotEnabledForThisTarget);
        }
    }

    private static void EnsureXarchSimdSupported()
    {
        EnsureSimdSupported();
        if (!Configuration.TARGET_XARCH)
        {
            JitExceptionHelpers.ThrowPlatformNotSupportedException(FatalJitErrorCode.FixedWidth32ByteAnd64ByteVectorsAreOnlySupportedOnXarch);
        }
    }

    private static void EnsureArm64SimdSupported()
    {
        EnsureSimdSupported();
        if (!Configuration.TARGET_ARM64)
        {
            JitExceptionHelpers.ThrowPlatformNotSupportedException(FatalJitErrorCode.ScalableVectorsAreOnlySupportedOnARM64);
        }
    }

    public ValueNum VNForSimd8Con(in simd8_t value)
    {
        EnsureSimdSupported();
        return VnForConst(value, value, _simd8CnsMap ??= [], TYP_SIMD8);
    }

    public ValueNum VNForSimd12Con(in simd12_t value)
    {
        EnsureSimdSupported();
        return VnForConst(value, value, _simd12CnsMap ??= [], TYP_SIMD12);
    }

    public ValueNum VNForSimd16Con(in simd16_t value)
    {
        EnsureSimdSupported();
        return VnForConst(value, value, _simd16CnsMap ??= [], TYP_SIMD16);
    }

    public ValueNum VNForSimd32Con(in simd32_t value)
    {
        EnsureXarchSimdSupported();
        return VnForConst(value, value, _simd32CnsMap ??= [], TYP_SIMD32);
    }

    public ValueNum VNForSimd64Con(in simd64_t value)
    {
        EnsureXarchSimdSupported();
        return VnForConst(value, value, _simd64CnsMap ??= [], TYP_SIMD64);
    }

    public ValueNum VNForSimdScalableCon(in simdscalable_t value)
    {
        if (Configuration.TARGET_ARM64)
        {
            EnsureArm64SimdSupported();

            var constants = Arm64Data.GetSimdScalableCnsMap(this);
            return VnForConst(value, value, constants, TYP_SIMD_ARM64);
        }

        EnsureArm64SimdSupported();
        JitExceptionHelpers.ThrowUnreachableException();
        return default;
    }

    public ValueNum VNForSimdMaskCon(in simdmask_t value)
    {
        EnsureSimdSupported();
        if (!Configuration.FEATURE_MASKED_HW_INTRINSICS)
        {
            JitExceptionHelpers.ThrowPlatformNotSupportedException(FatalJitErrorCode.MaskedHardwareIntrinsicsAreNotEnabledForThisTarget);
        }

        if (Configuration.TARGET_ARM64)
        {
            return Arm64Data.VNForSimdMaskCon(this, simdmaskvalue_t.FromFixed(value));
        }

        return VnForConst(value, value, _simdMaskCnsMap ??= [], TYP_MASK);
    }

    public ValueNum VNForSimdMaskScalableCon(in simdmaskscalable_t value)
    {
        if (Configuration.TARGET_ARM64)
        {
            EnsureArm64SimdSupported();
            EnsureMaskedHardwareIntrinsicsAvailable();
            return Arm64Data.VNForSimdMaskCon(this, simdmaskvalue_t.FromScalable(value));
        }

        EnsureArm64SimdSupported();
        JitExceptionHelpers.ThrowUnreachableException();
        return default;
    }

    private T GetVectorConstant<T>(ValueNum vn, var_types type) where T : unmanaged
    {
        EnsureSimdSupported();
        assert(IsVNConstant(vn) && (TypeOfVN(vn) == type));
        return ((T[])_chunks[GetChunkNum(vn)].Defs)[ChunkOffset(vn)];
    }

    public simd8_t GetConstantSimd8(ValueNum vn) => GetVectorConstant<simd8_t>(vn, TYP_SIMD8);

    public simd12_t GetConstantSimd12(ValueNum vn) => GetVectorConstant<simd12_t>(vn, TYP_SIMD12);

    public simd16_t GetConstantSimd16(ValueNum vn) => GetVectorConstant<simd16_t>(vn, TYP_SIMD16);

    public simd32_t GetConstantSimd32(ValueNum vn)
    {
        EnsureXarchSimdSupported();
        return GetVectorConstant<simd32_t>(vn, TYP_SIMD32);
    }

    public simd64_t GetConstantSimd64(ValueNum vn)
    {
        EnsureXarchSimdSupported();
        return GetVectorConstant<simd64_t>(vn, TYP_SIMD64);
    }

    public simdscalable_t GetConstantSimdScalable(ValueNum vn)
    {
        if (Configuration.TARGET_ARM64)
        {
            EnsureArm64SimdSupported();
            return GetVectorConstant<simdscalable_t>(vn, TYP_SIMD_ARM64);
        }

        EnsureArm64SimdSupported();
        JitExceptionHelpers.ThrowUnreachableException();
        return default;
    }

    public simdmask_t GetConstantSimdMask(ValueNum vn)
    {
        EnsureSimdSupported();
        EnsureMaskedHardwareIntrinsicsAvailable();

        if (Configuration.TARGET_ARM64)
        {
            return Arm64Data.GetFixedSimdMask(this, vn);
        }

        return GetVectorConstant<simdmask_t>(vn, TYP_MASK);
    }

    public simdmaskscalable_t GetConstantSimdMaskScalable(ValueNum vn)
    {
        if (!Configuration.TARGET_ARM64)
        {
            JitExceptionHelpers.ThrowPlatformNotSupportedException(FatalJitErrorCode.ScalableMaskConstantsAreOnlySupportedOnARM64);
        }

        EnsureArm64SimdSupported();
        EnsureMaskedHardwareIntrinsicsAvailable();
        return Arm64Data.GetScalableSimdMask(this, vn);
    }

    public simd_t GetConstantSimd(ValueNum vn)
    {
        EnsureSimdSupported();
        assert(IsVNConstant(vn));
        var chunk = _chunks[GetChunkNum(vn)];
        var offset = ChunkOffset(vn);
        ReadOnlySpan<byte> bytes;
        switch (chunk.Type)
        {
            case TYP_SIMD8:
                bytes = MemoryMarshal.AsBytes(((simd8_t[])chunk.Defs).AsSpan(offset, 1));
                break;
            case TYP_SIMD12:
                bytes = MemoryMarshal.AsBytes(((simd12_t[])chunk.Defs).AsSpan(offset, 1));
                break;
            case TYP_SIMD16:
                bytes = MemoryMarshal.AsBytes(((simd16_t[])chunk.Defs).AsSpan(offset, 1));
                break;
            case TYP_SIMD32 when Configuration.TARGET_XARCH:
                bytes = MemoryMarshal.AsBytes(((simd32_t[])chunk.Defs).AsSpan(offset, 1));
                break;
            case TYP_SIMD32:
                JitExceptionHelpers.ThrowPlatformNotSupportedException(FatalJitErrorCode.FixedWidth32ByteVectorsAreOnlySupportedOnXarch);
                return default;
            case TYP_SIMD64 when Configuration.TARGET_XARCH:
                bytes = MemoryMarshal.AsBytes(((simd64_t[])chunk.Defs).AsSpan(offset, 1));
                break;
            case TYP_SIMD64:
                JitExceptionHelpers.ThrowPlatformNotSupportedException(FatalJitErrorCode.FixedWidth64ByteVectorsAreOnlySupportedOnXarch);
                return default;
            default:
                JitExceptionHelpers.ThrowUnreachableException(FatalJitErrorCode.UnsupportedValueNumberVectorStorageType);
                return default;
        }

        simd_t vector = default;
        bytes.CopyTo(vector.AsSpan<byte>());
        return vector;
    }

    public double GetConstantDouble(ValueNum vn)
    {
        assert(IsVNConstant(vn) && (TypeOfVN(vn) == TYP_DOUBLE));
        return ConstantValue<double>(vn);
    }

    public float GetConstantSingle(ValueNum vn)
    {
        assert(IsVNConstant(vn) && (TypeOfVN(vn) == TYP_FLOAT));
        return ConstantValue<float>(vn);
    }

    public ValueNum VNZeroForType(var_types type) {
    switch (type)
    {
        case TYP_BYTE or TYP_UBYTE or TYP_SHORT or TYP_USHORT or TYP_INT or TYP_UINT:
            return VNForIntCon(0);
        case TYP_LONG or TYP_ULONG:
            return VNForLongCon(0);
        case TYP_FLOAT:
            return VNForFloatCon(0);
        case TYP_DOUBLE:
            return VNForDoubleCon(0);
        case TYP_REF:
            return VNForNull();
        case TYP_BYREF:
            return VNForByrefCon(0);
        case TYP_SIMD8:
            return VNForSimd8Con(simd8_t.Zero);
        case TYP_SIMD12:
            return VNForSimd12Con(simd12_t.Zero);
        case TYP_SIMD16:
            return VNForSimd16Con(simd16_t.Zero);
        case TYP_SIMD32:
            return VNForSimd32Con(simd32_t.Zero);
        case TYP_SIMD64:
            return VNForSimd64Con(simd64_t.Zero);
        case TYP_SIMD_ARM64 when Configuration.TARGET_ARM64:
        {
            return VNForSimdScalableCon(simdscalable_t.Zero);
        }
        case TYP_SIMD_ARM64:
        {
            EnsureArm64SimdSupported();
            JitExceptionHelpers.ThrowUnreachableException();
            return default;
        }
        case TYP_MASK:
            return VNForSimdMaskCon(simdmask_t.Zero);
        default:
            JitExceptionHelpers.ThrowUnreachableException();
            return default;
    }
}
    public ValueNum VNForGenericCon(var_types type, ReadOnlySpan<byte> value)
    {
        switch (type)
        {
            case TYP_REF:
            {
                var handle = MemoryMarshal.Read<nint>(value);
                return handle == 0 ? VNForNull() : VNForHandle(handle, GTF_ICON_OBJ_HDL);
            }

            default:
            {
                switch (type)
                {
                    case TYP_BYTE:
                        return VNForIntCon(MemoryMarshal.Read<sbyte>(value));
                    case TYP_UBYTE:
                        return VNForIntCon(MemoryMarshal.Read<byte>(value));
                    case TYP_SHORT:
                        return VNForIntCon(MemoryMarshal.Read<short>(value));
                    case TYP_USHORT:
                        return VNForIntCon(MemoryMarshal.Read<ushort>(value));
                    case TYP_INT or TYP_UINT:
                        return VNForIntCon(MemoryMarshal.Read<int>(value));
                    case TYP_LONG or TYP_ULONG:
                        return VNForLongCon(MemoryMarshal.Read<long>(value));
                    case TYP_FLOAT:
                        return VNForFloatCon(MemoryMarshal.Read<float>(value));
                    case TYP_DOUBLE:
                        return VNForDoubleCon(MemoryMarshal.Read<double>(value));
                    case TYP_SIMD8 when Configuration.FEATURE_SIMD:
                        return VNForSimd8Con(MemoryMarshal.Read<simd8_t>(value));
                    case TYP_SIMD8:
                        JitExceptionHelpers.ThrowPlatformNotSupportedException(FatalJitErrorCode.SIMDIsNotEnabledForThisTarget);
                        return default;
                    case TYP_SIMD12 when Configuration.FEATURE_SIMD:
                        return VNForSimd12Con(MemoryMarshal.Read<simd12_t>(value));
                    case TYP_SIMD12:
                        JitExceptionHelpers.ThrowPlatformNotSupportedException(FatalJitErrorCode.SIMDIsNotEnabledForThisTarget);
                        return default;
                    case TYP_SIMD16 when Configuration.FEATURE_SIMD:
                        return VNForSimd16Con(MemoryMarshal.Read<simd16_t>(value));
                    case TYP_SIMD16:
                        JitExceptionHelpers.ThrowPlatformNotSupportedException(FatalJitErrorCode.SIMDIsNotEnabledForThisTarget);
                        return default;
                    case TYP_SIMD32 when Configuration.FEATURE_SIMD && Configuration.TARGET_XARCH:
                        return VNForSimd32Con(MemoryMarshal.Read<simd32_t>(value));
                    case TYP_SIMD32 when !Configuration.FEATURE_SIMD:
                        JitExceptionHelpers.ThrowPlatformNotSupportedException(FatalJitErrorCode.SIMDIsNotEnabledForThisTarget);
                        return default;
                    case TYP_SIMD32:
                        JitExceptionHelpers.ThrowPlatformNotSupportedException(FatalJitErrorCode.FixedWidth32ByteVectorsAreOnlySupportedOnXarch);
                        return default;
                    case TYP_SIMD64 when Configuration.FEATURE_SIMD && Configuration.TARGET_XARCH:
                        return VNForSimd64Con(MemoryMarshal.Read<simd64_t>(value));
                    case TYP_SIMD64 when !Configuration.FEATURE_SIMD:
                        JitExceptionHelpers.ThrowPlatformNotSupportedException(FatalJitErrorCode.SIMDIsNotEnabledForThisTarget);
                        return default;
                    case TYP_SIMD64:
                        JitExceptionHelpers.ThrowPlatformNotSupportedException(FatalJitErrorCode.FixedWidth64ByteVectorsAreOnlySupportedOnXarch);
                        return default;
                    case TYP_MASK when Configuration.FEATURE_SIMD && Configuration.FEATURE_MASKED_HW_INTRINSICS:
                        return VNForSimdMaskCon(MemoryMarshal.Read<simdmask_t>(value));
                    case TYP_MASK when !Configuration.FEATURE_SIMD:
                        JitExceptionHelpers.ThrowPlatformNotSupportedException(FatalJitErrorCode.SIMDIsNotEnabledForThisTarget);
                        return default;
                    case TYP_MASK:
                        JitExceptionHelpers.ThrowPlatformNotSupportedException(FatalJitErrorCode.MaskedHardwareIntrinsicsAreNotEnabledForThisTarget);
                        return default;
                    default:
                        JitExceptionHelpers.ThrowUnreachableException();
                        return default;
                }
            }
        }
    }

    public ValueNum VNIgnoreIntToLongCast(ValueNum vn)
    {
        if (TypeOfVN(vn) == TYP_LONG)
        {
            var source = NoVN;
            var castInfo = NoVN;
            if (IsVNBinFunc(vn, VNF_Cast, ref source, ref castInfo))
            {
                GetCastOperFromVN(castInfo, out var castToType, out var sourceIsUnsigned);
                if ((castToType.ActualType == TYP_LONG) && (TypeOfVN(source) == TYP_INT))
                {
                    if (!sourceIsUnsigned || IsVNNeverNegative(source))
                    {
                        return source;
                    }
                }
            }

            if (IsVNIntegralConstant(vn, out int constant))
            {
                return VNForIntCon(constant);
            }
        }

        return vn;
    }
}
