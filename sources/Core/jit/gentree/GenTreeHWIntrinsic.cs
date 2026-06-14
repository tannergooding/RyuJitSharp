// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_HW_INTRINSICS
using System.Diagnostics.CodeAnalysis;

namespace RyuJitSharp;

public sealed class GenTreeHWIntrinsic : GenTreeJitIntrinsic
{
    public GenTreeHWIntrinsic(var_types type, NamedIntrinsic hwIntrinsicId, var_types simdBaseType, byte simdSize, params GenTree[] operands)
        : base(GT_HWINTRINSIC, type, simdBaseType, simdSize, operands)
    {
        Initialize(hwIntrinsicId);
    }

    public NamedIntrinsic HWIntrinsicId => _hwIntrinsicId;

#if TARGET_ARM64
    public bool IsCreate => _hwIntrinsicId is NI_Vector64_Create or NI_Vector128_Create;
#elif TARGET_XARCH
    public bool IsCreate => _hwIntrinsicId is NI_Vector128_Create or NI_Vector256_Create or NI_Vector512_Create;
#endif

    /// <summary>Does this HWI node have memory load or store semantics?</summary>
    public bool IsMemoryLoadOrStore => IsMemoryLoad() || IsMemoryStore(out _);

    /// <summary>Does this node have memory store or barrier semantics?</summary>
    public bool IsMemoryStoreOrBarrier
    {
        get
        {
            if (IsMemoryStore(out _))
            {
                return true;
            }

#if TARGET_XARCH
            // TODO: Port GenTreeHWIntrinsic.IsMemoryStoreOrBarrier
            // var intrinsicId = _hwIntrinsicId;
            // 
            // if (HWIntrinsicInfo.HasSpecialSideEffect_Barrier(intrinsicId))
            // {
            //     return true;
            // }
#endif

            return false;
        }
    }

    private void Initialize(NamedIntrinsic intrinsicId)
    {
        // TODO: Port Initialize
    }

    public bool IsMemoryLoad() => IsMemoryLoad(out _);

    public bool IsMemoryLoad([NotNullWhen(true)] out GenTree? addr)
    {
#if TARGET_XARCH || TARGET_ARM64
        var intrinsicId = HWIntrinsicId;
        var category = HWIntrinsicInfo.lookupCategory(intrinsicId);

        if (category is HW_Category_MemoryLoad)
        {
            switch (intrinsicId)
            {
#if TARGET_XARCH
                case NI_X86Base_LoadLow:
                case NI_X86Base_LoadHigh:
                {
                    addr = GetOp(2);
                    break;
                }
#endif

#if TARGET_ARM64
                case NI_AdvSimd_LoadAndInsertScalar:
                case NI_AdvSimd_LoadAndInsertScalarVector64x2:
                case NI_AdvSimd_LoadAndInsertScalarVector64x3:
                case NI_AdvSimd_LoadAndInsertScalarVector64x4:
                case NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x2:
                case NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x3:
                case NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x4:
                {
                    addr = GetOp(3);
                    break;
                }

                case NI_Sve_GatherVector:
                case NI_Sve_GatherVectorByteZeroExtend:
                case NI_Sve_GatherVectorByteZeroExtendFirstFaulting:
                case NI_Sve_GatherVectorFirstFaulting:
                case NI_Sve_GatherVectorInt16SignExtend:
                case NI_Sve_GatherVectorInt16SignExtendFirstFaulting:
                case NI_Sve_GatherVectorInt16WithByteOffsetsSignExtend:
                case NI_Sve_GatherVectorInt16WithByteOffsetsSignExtendFirstFaulting:
                case NI_Sve_GatherVectorInt32SignExtend:
                case NI_Sve_GatherVectorInt32SignExtendFirstFaulting:
                case NI_Sve_GatherVectorInt32WithByteOffsetsSignExtend:
                case NI_Sve_GatherVectorInt32WithByteOffsetsSignExtendFirstFaulting:
                case NI_Sve_GatherVectorSByteSignExtend:
                case NI_Sve_GatherVectorSByteSignExtendFirstFaulting:
                case NI_Sve_GatherVectorUInt16WithByteOffsetsZeroExtend:
                case NI_Sve_GatherVectorUInt16WithByteOffsetsZeroExtendFirstFaulting:
                case NI_Sve_GatherVectorUInt16ZeroExtend:
                case NI_Sve_GatherVectorUInt16ZeroExtendFirstFaulting:
                case NI_Sve_GatherVectorUInt32WithByteOffsetsZeroExtend:
                case NI_Sve_GatherVectorUInt32WithByteOffsetsZeroExtendFirstFaulting:
                case NI_Sve_GatherVectorUInt32ZeroExtend:
                case NI_Sve_GatherVectorUInt32ZeroExtendFirstFaulting:
                case NI_Sve_GatherVectorWithByteOffsets:
                case NI_Sve_GatherVectorWithByteOffsetFirstFaulting:
                case NI_Sve_LoadVector:
                case NI_Sve_LoadVectorNonTemporal:
                case NI_Sve_LoadVector128AndReplicateToVector:
                case NI_Sve_LoadVectorByteZeroExtendFirstFaulting:
                case NI_Sve_LoadVectorByteZeroExtendToInt16:
                case NI_Sve_LoadVectorByteZeroExtendToInt32:
                case NI_Sve_LoadVectorByteZeroExtendToInt64:
                case NI_Sve_LoadVectorByteZeroExtendToUInt16:
                case NI_Sve_LoadVectorByteZeroExtendToUInt32:
                case NI_Sve_LoadVectorByteZeroExtendToUInt64:
                case NI_Sve_LoadVectorFirstFaulting:
                case NI_Sve_LoadVectorInt16SignExtendFirstFaulting:
                case NI_Sve_LoadVectorInt16SignExtendToInt32:
                case NI_Sve_LoadVectorInt16SignExtendToInt64:
                case NI_Sve_LoadVectorInt16SignExtendToUInt32:
                case NI_Sve_LoadVectorInt16SignExtendToUInt64:
                case NI_Sve_LoadVectorInt32SignExtendFirstFaulting:
                case NI_Sve_LoadVectorInt32SignExtendToInt64:
                case NI_Sve_LoadVectorInt32SignExtendToUInt64:
                case NI_Sve_LoadVectorSByteSignExtendFirstFaulting:
                case NI_Sve_LoadVectorSByteSignExtendToInt16:
                case NI_Sve_LoadVectorSByteSignExtendToInt32:
                case NI_Sve_LoadVectorSByteSignExtendToInt64:
                case NI_Sve_LoadVectorSByteSignExtendToUInt16:
                case NI_Sve_LoadVectorSByteSignExtendToUInt32:
                case NI_Sve_LoadVectorSByteSignExtendToUInt64:
                case NI_Sve_LoadVectorUInt16ZeroExtendFirstFaulting:
                case NI_Sve_LoadVectorUInt16ZeroExtendToInt32:
                case NI_Sve_LoadVectorUInt16ZeroExtendToInt64:
                case NI_Sve_LoadVectorUInt16ZeroExtendToUInt32:
                case NI_Sve_LoadVectorUInt16ZeroExtendToUInt64:
                case NI_Sve_LoadVectorUInt32ZeroExtendFirstFaulting:
                case NI_Sve_LoadVectorUInt32ZeroExtendToInt64:
                case NI_Sve_LoadVectorUInt32ZeroExtendToUInt64:
                case NI_Sve_Load2xVectorAndUnzip:
                case NI_Sve_Load3xVectorAndUnzip:
                case NI_Sve_Load4xVectorAndUnzip:
                case NI_Sve_LoadVectorByteNonFaultingZeroExtendToInt16:
                case NI_Sve_LoadVectorByteNonFaultingZeroExtendToInt32:
                case NI_Sve_LoadVectorByteNonFaultingZeroExtendToInt64:
                case NI_Sve_LoadVectorByteNonFaultingZeroExtendToUInt16:
                case NI_Sve_LoadVectorByteNonFaultingZeroExtendToUInt32:
                case NI_Sve_LoadVectorByteNonFaultingZeroExtendToUInt64:
                case NI_Sve_LoadVectorInt16NonFaultingSignExtendToInt32:
                case NI_Sve_LoadVectorInt16NonFaultingSignExtendToInt64:
                case NI_Sve_LoadVectorInt16NonFaultingSignExtendToUInt32:
                case NI_Sve_LoadVectorInt16NonFaultingSignExtendToUInt64:
                case NI_Sve_LoadVectorInt32NonFaultingSignExtendToInt64:
                case NI_Sve_LoadVectorInt32NonFaultingSignExtendToUInt64:
                case NI_Sve_LoadVectorNonFaulting:
                case NI_Sve_LoadVectorSByteNonFaultingSignExtendToInt16:
                case NI_Sve_LoadVectorSByteNonFaultingSignExtendToInt32:
                case NI_Sve_LoadVectorSByteNonFaultingSignExtendToInt64:
                case NI_Sve_LoadVectorSByteNonFaultingSignExtendToUInt16:
                case NI_Sve_LoadVectorSByteNonFaultingSignExtendToUInt32:
                case NI_Sve_LoadVectorSByteNonFaultingSignExtendToUInt64:
                case NI_Sve_LoadVectorUInt16NonFaultingZeroExtendToInt32:
                case NI_Sve_LoadVectorUInt16NonFaultingZeroExtendToInt64:
                case NI_Sve_LoadVectorUInt16NonFaultingZeroExtendToUInt32:
                case NI_Sve_LoadVectorUInt16NonFaultingZeroExtendToUInt64:
                case NI_Sve_LoadVectorUInt32NonFaultingZeroExtendToInt64:
                case NI_Sve_LoadVectorUInt32NonFaultingZeroExtendToUInt64:
                case NI_Sve2_GatherVectorByteZeroExtendNonTemporal:
                case NI_Sve2_GatherVectorInt16SignExtendNonTemporal:
                case NI_Sve2_GatherVectorInt16WithByteOffsetsSignExtendNonTemporal:
                case NI_Sve2_GatherVectorInt32SignExtendNonTemporal:
                case NI_Sve2_GatherVectorInt32WithByteOffsetsSignExtendNonTemporal:
                case NI_Sve2_GatherVectorNonTemporal:
                case NI_Sve2_GatherVectorSByteSignExtendNonTemporal:
                case NI_Sve2_GatherVectorUInt16WithByteOffsetsZeroExtendNonTemporal:
                case NI_Sve2_GatherVectorUInt16ZeroExtendNonTemporal:
                case NI_Sve2_GatherVectorUInt32WithByteOffsetsZeroExtendNonTemporal:
                case NI_Sve2_GatherVectorUInt32ZeroExtendNonTemporal:
                case NI_Sve2_GatherVectorWithByteOffsetsNonTemporal:
                {
                    addr = GetOp(2);
                    break;
                }
#endif

                default:
                {
                    addr = GetOp(1);
                    break;
                }
            }
        }
#if TARGET_XARCH
        else if (HWIntrinsicInfo.MaybeMemoryLoad(intrinsicId))
        {
            // Some intrinsics (without HW_Category_MemoryLoad) also have MemoryLoad semantics
            // This is generally because they have both vector and pointer overloads, e.g.,
            // * Vector128<byte> BroadcastScalarToVector128(Vector128<byte> value)
            // * Vector128<byte> BroadcastScalarToVector128(byte* source)

            if (category is HW_Category_SimpleSIMD or HW_Category_SIMDScalar)
            {
                assert(Operands.Length is 1);

                switch (intrinsicId)
                {
                    case NI_X86Base_ConvertToVector128Int16:
                    case NI_X86Base_ConvertToVector128Int32:
                    case NI_X86Base_ConvertToVector128Int64:
                    case NI_AVX2_BroadcastScalarToVector128:
                    case NI_AVX2_BroadcastScalarToVector256:
                    case NI_AVX2_ConvertToVector256Int16:
                    case NI_AVX2_ConvertToVector256Int32:
                    case NI_AVX2_ConvertToVector256Int64:
                    {
                        if (AuxiliaryType is TYP_U_IMPL)
                        {
                            addr = GetOp(1);
                        }
                        else
                        {
                            assert(AuxiliaryType is TYP_UNKNOWN);
                            addr = null;
                        }
                        break;
                    }

                    default:
                    {
                        unreached();
                        addr = null;
                        break;
                    }
                }
            }
            else if (category is HW_Category_IMM)
            {
                switch (intrinsicId)
                {
                    case NI_AVX2_GatherVector128:
                    case NI_AVX2_GatherVector256:
                    {
                        addr = GetOp(1);
                        break;
                    }

                    case NI_AVX2_GatherMaskVector128:
                    case NI_AVX2_GatherMaskVector256:
                    {
                        addr = GetOp(2);
                        break;
                    }

                    default:
                    {
                        addr = null;
                        break;
                    }
                }
            }
            else
            {
                addr = null;
            }
        }
#endif
        else
        {
            addr = null;
        }
#endif

        if (addr is not null)
        {
#if TARGET_ARM64
            assert(AreContiguous(NI_Sve_GatherVector, NI_Sve_GatherVectorByteZeroExtend,
                                 NI_Sve_GatherVectorByteZeroExtendFirstFaulting, NI_Sve_GatherVectorFirstFaulting,
                                 NI_Sve_GatherVectorInt16SignExtend, NI_Sve_GatherVectorInt16SignExtendFirstFaulting,
                                 NI_Sve_GatherVectorInt16WithByteOffsetsSignExtend,
                                 NI_Sve_GatherVectorInt16WithByteOffsetsSignExtendFirstFaulting,
                                 NI_Sve_GatherVectorInt32SignExtend, NI_Sve_GatherVectorInt32SignExtendFirstFaulting,
                                 NI_Sve_GatherVectorInt32WithByteOffsetsSignExtend,
                                 NI_Sve_GatherVectorInt32WithByteOffsetsSignExtendFirstFaulting,
                                 NI_Sve_GatherVectorSByteSignExtend, NI_Sve_GatherVectorSByteSignExtendFirstFaulting,
                                 NI_Sve_GatherVectorUInt16WithByteOffsetsZeroExtend,
                                 NI_Sve_GatherVectorUInt16WithByteOffsetsZeroExtendFirstFaulting,
                                 NI_Sve_GatherVectorUInt16ZeroExtend, NI_Sve_GatherVectorUInt16ZeroExtendFirstFaulting,
                                 NI_Sve_GatherVectorUInt32WithByteOffsetsZeroExtend,
                                 NI_Sve_GatherVectorUInt32WithByteOffsetsZeroExtendFirstFaulting,
                                 NI_Sve_GatherVectorUInt32ZeroExtend, NI_Sve_GatherVectorUInt32ZeroExtendFirstFaulting));

            assert(AreContiguous(NI_Sve2_GatherVectorByteZeroExtendNonTemporal,
                                 NI_Sve2_GatherVectorInt16SignExtendNonTemporal,
                                 NI_Sve2_GatherVectorInt16WithByteOffsetsSignExtendNonTemporal,
                                 NI_Sve2_GatherVectorInt32SignExtendNonTemporal,
                                 NI_Sve2_GatherVectorInt32WithByteOffsetsSignExtendNonTemporal,
                                 NI_Sve2_GatherVectorNonTemporal, NI_Sve2_GatherVectorSByteSignExtendNonTemporal,
                                 NI_Sve2_GatherVectorUInt16WithByteOffsetsZeroExtendNonTemporal,
                                 NI_Sve2_GatherVectorUInt16ZeroExtendNonTemporal,
                                 NI_Sve2_GatherVectorUInt32WithByteOffsetsZeroExtendNonTemporal,
                                 NI_Sve2_GatherVectorUInt32ZeroExtendNonTemporal,
                                 NI_Sve2_GatherVectorWithByteOffsetsNonTemporal));

            var isSveGatherLoad = (intrinsicId is >= NI_Sve_GatherVector and <= NI_Sve_GatherVectorUInt32ZeroExtendFirstFaulting);
            var isSve2GatherLoad = (intrinsicId is >= NI_Sve2_GatherVectorByteZeroExtendNonTemporal and <= NI_Sve2_GatherVectorWithByteOffsetsNonTemporal);
            assert(varTypeIsI(addr.Type) || (varTypeIsSimd(addr.Type) && (isSveGatherLoad || isSve2GatherLoad)));
#else
            assert(varTypeIsI(addr.Type));
#endif
            return true;
        }
        return false;
    }

    /// <summary>Does this HWI node have memory store semantics</summary>
    /// <param name="addr">The address of the memory location affected by the intrinsic, if applicable.</param>
    /// <returns>Whether this intrinsic may mutate heap state and/or throw a NullReferenceException if the address is "null".</returns>
    public bool IsMemoryStore([NotNullWhen(true)] out GenTree? addr)
    {
#if TARGET_XARCH || TARGET_ARM64
        var intrinsicId = HWIntrinsicId;
        var category = HWIntrinsicInfo.lookupCategory(intrinsicId);

        if (category is HW_Category_MemoryStore)
        {
            switch (intrinsicId)
            {
#if TARGET_XARCH
                case NI_X86Base_MaskMove:
                {
                    addr = GetOp(3);
                    break;
                }

#elif TARGET_ARM64
                case NI_Sve_StoreAndZip:
                case NI_Sve_StoreAndZipx2:
                case NI_Sve_StoreAndZipx3:
                case NI_Sve_StoreAndZipx4:
                case NI_Sve_StoreNarrowing:
                case NI_Sve_StoreNonTemporal:
                {
                    addr = GetOp(2);
                    break;
                }

                case NI_Sve_Scatter:
                case NI_Sve_Scatter16BitNarrowing:
                case NI_Sve_Scatter16BitWithByteOffsetsNarrowing:
                case NI_Sve_Scatter32BitNarrowing:
                case NI_Sve_Scatter32BitWithByteOffsetsNarrowing:
                case NI_Sve_Scatter8BitNarrowing:
                case NI_Sve_Scatter8BitWithByteOffsetsNarrowing:
                case NI_Sve_ScatterWithByteOffsets:
                case NI_Sve2_Scatter16BitNarrowingNonTemporal:
                case NI_Sve2_Scatter16BitWithByteOffsetsNarrowingNonTemporal:
                case NI_Sve2_Scatter32BitNarrowingNonTemporal:
                case NI_Sve2_Scatter32BitWithByteOffsetsNarrowingNonTemporal:
                case NI_Sve2_Scatter8BitNarrowingNonTemporal:
                case NI_Sve2_Scatter8BitWithByteOffsetsNarrowingNonTemporal:
                case NI_Sve2_ScatterNonTemporal:
                case NI_Sve2_ScatterWithByteOffsetsNonTemporal:
                {
                    addr = GetOp(2);
                    break;
                }
#endif

                default:
                {
                    addr = GetOp(1);
                    break;
                }
            }
        }
#if TARGET_XARCH
        else if (HWIntrinsicInfo.MaybeMemoryStore(intrinsicId) && (category is HW_Category_IMM or HW_Category_Scalar))
        {
            // Some intrinsics (without HW_Category_MemoryStore) also have MemoryStore semantics

            // Bmi2/Bmi2.X64.MultiplyNoFlags may return the lower half result by a out argument
            // unsafe ulong MultiplyNoFlags(ulong left, ulong right, ulong* low)
            //
            // So, the 3-argument form is MemoryStore
            if (Operands.Length is 3)
            {
                switch (intrinsicId)
                {
                    case NI_AVX2_MultiplyNoFlags:
                    case NI_AVX2_X64_MultiplyNoFlags:
                    {
                        addr = GetOp(3);
                        break;
                    }

                    default:
                    {
                        addr = null;
                        break;
                    }
                }
            }
            else
            {
                addr = null;
            }
        }
#endif
        else
        {
            addr = null;
        }
#endif

        if (addr is not null)
        {
#if TARGET_ARM64
            assert(varTypeIsI(addr.Type) || (varTypeIsSimd(addr.Type) && ((intrinsicId >= NI_Sve_Scatter))));
#else
            assert(varTypeIsI(addr.Type));
#endif
            return true;
        }

        return false;
    }

    /// <summary>Check whether the operation requires GTF_CALL flag regardless of the children's flags.</summary>
    public bool RequiresCallFlag()
    {
        var intrinsicId = HWIntrinsicId;

        if (HWIntrinsicInfo.HasSpecialSideEffect(intrinsicId))
        {
            switch (intrinsicId)
            {
#if TARGET_XARCH
                case NI_X86Base_Pause:
                case NI_X86Base_Prefetch0:
                case NI_X86Base_Prefetch1:
                case NI_X86Base_Prefetch2:
                case NI_X86Base_PrefetchNonTemporal:
                {
                    return true;
                }
#endif

#if TARGET_ARM64
                case NI_ArmBase_Yield:
                case NI_Sve_GatherPrefetch16Bit:
                case NI_Sve_GatherPrefetch32Bit:
                case NI_Sve_GatherPrefetch64Bit:
                case NI_Sve_GatherPrefetch8Bit:
                case NI_Sve_GetFfrByte:
                case NI_Sve_GetFfrDouble:
                case NI_Sve_GetFfrInt16:
                case NI_Sve_GetFfrInt32:
                case NI_Sve_GetFfrInt64:
                case NI_Sve_GetFfrSByte:
                case NI_Sve_GetFfrSingle:
                case NI_Sve_GetFfrUInt16:
                case NI_Sve_GetFfrUInt32:
                case NI_Sve_GetFfrUInt64:
                case NI_Sve_Prefetch16Bit:
                case NI_Sve_Prefetch32Bit:
                case NI_Sve_Prefetch64Bit:
                case NI_Sve_Prefetch8Bit:
                case NI_Sve_SetFfr:
                {
                    return true;
                }
#endif

                default:
                {
                    break;
                }
            }
        }

        return IsUserCall;
    }
}
#endif
