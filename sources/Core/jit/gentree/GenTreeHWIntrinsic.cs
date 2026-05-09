// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_HW_INTRINSICS
using System;

namespace RyuJitSharp;

public sealed class GenTreeHWIntrinsic : GenTreeJitIntrinsic
{
    public GenTreeHWIntrinsic(var_types type, NamedIntrinsic hwIntrinsicId, var_types simdBaseType, byte simdSize, params ReadOnlySpan<GenTree> operands)
        : base(GT_HWINTRINSIC, type, simdBaseType, simdSize, operands)
    {
        Initialize(hwIntrinsicId);
    }

    public NamedIntrinsic HWIntrinsicId => _hwIntrinsicId;

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
}
#endif
