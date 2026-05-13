// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_HW_INTRINSICS
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

    public bool IsMemoryLoad()
    {
        // TODO: Port GenTreeHWIntrinsic.IsMemoryLoad
        return false;
    }

    /// <summary>Does this HWI node have memory store semantics</summary>
    /// <param name="addr">The address of the memory location affected by the intrinsic, if applicable.</param>
    /// <returns>Whether this intrinsic may mutate heap state and/or throw a NullReferenceException if the address is "null".</returns>
    public bool IsMemoryStore(out GenTree? addr)
    {
        addr = null;
        // TODO: Port GenTreeHWIntrinsic.IsMemoryStore
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
