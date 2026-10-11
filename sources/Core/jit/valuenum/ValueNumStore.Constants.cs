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
    private Dictionary<simdscalable_t, ValueNum>? _simdScalableCnsMap;
    private Dictionary<simdmaskvalue_t, ValueNum>? _simdMaskCnsMap;

    private static void EnsureSimdSupported()
    {
        if (!Configuration.FEATURE_SIMD)
        {
            throw new PlatformNotSupportedException("SIMD is not enabled for this target.");
        }
    }

    private static void EnsureXarchSimdSupported()
    {
        EnsureSimdSupported();
        if (!Configuration.TARGET_XARCH)
        {
            throw new PlatformNotSupportedException(
                "Fixed-width 32-byte and 64-byte vectors are only supported on xarch.");
        }
    }

    private static void EnsureArm64SimdSupported()
    {
        EnsureSimdSupported();
        if (!Configuration.TARGET_ARM64)
        {
            throw new PlatformNotSupportedException("Scalable vectors are only supported on ARM64.");
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
        EnsureArm64SimdSupported();
        return VnForConst(value, value, _simdScalableCnsMap ??= [], TYP_SIMD_ARM64);
    }

    public ValueNum VNForSimdMaskCon(in simdmask_t value)
    {
        EnsureSimdSupported();
        if (!Configuration.FEATURE_MASKED_HW_INTRINSICS)
        {
            throw new PlatformNotSupportedException("Masked hardware intrinsics are not enabled for this target.");
        }

        var storage = simdmaskvalue_t.FromFixed(value);

        return VnForConst(storage, storage, _simdMaskCnsMap ??= [], TYP_MASK);
    }

    public ValueNum VNForSimdMaskScalableCon(in simdmaskscalable_t value)
    {
        EnsureArm64SimdSupported();
        var storage = simdmaskvalue_t.FromScalable(value);

        return VnForConst(storage, storage, _simdMaskCnsMap ??= [], TYP_MASK);
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
        EnsureArm64SimdSupported();
        return GetVectorConstant<simdscalable_t>(vn, TYP_SIMD_ARM64);
    }

    public simdmaskvalue_t GetConstantSimdMaskValue(ValueNum vn)
    {
        EnsureSimdSupported();
        if (!Configuration.FEATURE_MASKED_HW_INTRINSICS)
        {
            throw new PlatformNotSupportedException("Masked hardware intrinsics are not enabled for this target.");
        }

        return GetVectorConstant<simdmaskvalue_t>(vn, TYP_MASK);
    }

    public simdmask_t GetConstantSimdMask(ValueNum vn)
    {
        var storage = GetConstantSimdMaskValue(vn);
        if (Configuration.TARGET_ARM64)
        {
            noway_assert(!storage.IsScalable);
        }

        return storage.Fixed;
    }

    public simdmaskscalable_t GetConstantSimdMaskScalable(ValueNum vn)
    {
        if (!Configuration.TARGET_ARM64)
        {
            throw new PlatformNotSupportedException("Scalable mask constants are only supported on ARM64.");
        }

        var storage = GetConstantSimdMaskValue(vn);
        noway_assert(storage.IsScalable);

        return storage.Scalable;
    }

    public simd_t GetConstantSimd(ValueNum vn)
    {
        EnsureSimdSupported();
        assert(IsVNConstant(vn));
        var chunk = _chunks[GetChunkNum(vn)];
        var offset = ChunkOffset(vn);
        ReadOnlySpan<byte> bytes = chunk.Type switch {
            TYP_SIMD8 => MemoryMarshal.AsBytes(((simd8_t[])chunk.Defs).AsSpan(offset, 1)),
            TYP_SIMD12 => MemoryMarshal.AsBytes(((simd12_t[])chunk.Defs).AsSpan(offset, 1)),
            TYP_SIMD16 => MemoryMarshal.AsBytes(((simd16_t[])chunk.Defs).AsSpan(offset, 1)),
            TYP_SIMD32 when Configuration.TARGET_XARCH
                => MemoryMarshal.AsBytes(((simd32_t[])chunk.Defs).AsSpan(offset, 1)),
            TYP_SIMD32
                => throw new PlatformNotSupportedException(
                    "Fixed-width 32-byte vectors are only supported on xarch."),
            TYP_SIMD64 when Configuration.TARGET_XARCH
                => MemoryMarshal.AsBytes(((simd64_t[])chunk.Defs).AsSpan(offset, 1)),
            TYP_SIMD64
                => throw new PlatformNotSupportedException(
                    "Fixed-width 64-byte vectors are only supported on xarch."),
            _ => throw new UnreachableException(),
        };
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

    public ValueNum VNZeroForType(var_types type) => type switch {
        TYP_BYTE or TYP_UBYTE or TYP_SHORT or TYP_USHORT or TYP_INT or TYP_UINT => VNForIntCon(0),
        TYP_LONG or TYP_ULONG => VNForLongCon(0),
        TYP_FLOAT => VNForFloatCon(0),
        TYP_DOUBLE => VNForDoubleCon(0),
        TYP_REF => VNForNull(),
        TYP_BYREF => VNForByrefCon(0),
        TYP_SIMD8 => VNForSimd8Con(simd8_t.Zero),
        TYP_SIMD12 => VNForSimd12Con(simd12_t.Zero),
        TYP_SIMD16 => VNForSimd16Con(simd16_t.Zero),
        TYP_SIMD32 => VNForSimd32Con(simd32_t.Zero),
        TYP_SIMD64 => VNForSimd64Con(simd64_t.Zero),
        TYP_SIMD_ARM64 => VNForSimdScalableCon(simdscalable_t.Zero),
        TYP_MASK => VNForSimdMaskCon(simdmask_t.Zero),
        _ => throw new UnreachableException(),
    };

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
                return type switch {
                    TYP_BYTE => VNForIntCon(MemoryMarshal.Read<sbyte>(value)),
                    TYP_UBYTE => VNForIntCon(MemoryMarshal.Read<byte>(value)),
                    TYP_SHORT => VNForIntCon(MemoryMarshal.Read<short>(value)),
                    TYP_USHORT => VNForIntCon(MemoryMarshal.Read<ushort>(value)),
                    TYP_INT or TYP_UINT => VNForIntCon(MemoryMarshal.Read<int>(value)),
                    TYP_LONG or TYP_ULONG => VNForLongCon(MemoryMarshal.Read<long>(value)),
                    TYP_FLOAT => VNForFloatCon(MemoryMarshal.Read<float>(value)),
                    TYP_DOUBLE => VNForDoubleCon(MemoryMarshal.Read<double>(value)),
                    TYP_SIMD8 when Configuration.FEATURE_SIMD
                        => VNForSimd8Con(MemoryMarshal.Read<simd8_t>(value)),
                    TYP_SIMD8
                        => throw new PlatformNotSupportedException("SIMD is not enabled for this target."),
                    TYP_SIMD12 when Configuration.FEATURE_SIMD
                        => VNForSimd12Con(MemoryMarshal.Read<simd12_t>(value)),
                    TYP_SIMD12
                        => throw new PlatformNotSupportedException("SIMD is not enabled for this target."),
                    TYP_SIMD16 when Configuration.FEATURE_SIMD
                        => VNForSimd16Con(MemoryMarshal.Read<simd16_t>(value)),
                    TYP_SIMD16
                        => throw new PlatformNotSupportedException("SIMD is not enabled for this target."),
                    TYP_SIMD32 when Configuration.FEATURE_SIMD && Configuration.TARGET_XARCH
                        => VNForSimd32Con(MemoryMarshal.Read<simd32_t>(value)),
                    TYP_SIMD32 when !Configuration.FEATURE_SIMD
                        => throw new PlatformNotSupportedException("SIMD is not enabled for this target."),
                    TYP_SIMD32
                        => throw new PlatformNotSupportedException(
                            "Fixed-width 32-byte vectors are only supported on xarch."),
                    TYP_SIMD64 when Configuration.FEATURE_SIMD && Configuration.TARGET_XARCH
                        => VNForSimd64Con(MemoryMarshal.Read<simd64_t>(value)),
                    TYP_SIMD64 when !Configuration.FEATURE_SIMD
                        => throw new PlatformNotSupportedException("SIMD is not enabled for this target."),
                    TYP_SIMD64
                        => throw new PlatformNotSupportedException(
                            "Fixed-width 64-byte vectors are only supported on xarch."),
                    TYP_MASK when Configuration.FEATURE_SIMD && Configuration.FEATURE_MASKED_HW_INTRINSICS
                        => VNForSimdMaskCon(MemoryMarshal.Read<simdmask_t>(value)),
                    TYP_MASK when !Configuration.FEATURE_SIMD
                        => throw new PlatformNotSupportedException("SIMD is not enabled for this target."),
                    TYP_MASK
                        => throw new PlatformNotSupportedException(
                            "Masked hardware intrinsics are not enabled for this target."),
                    _ => throw new UnreachableException(),
                };
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
