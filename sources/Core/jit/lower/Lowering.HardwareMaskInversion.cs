// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
#if FEATURE_HW_INTRINSICS && TARGET_XARCH
    private bool TryInvertMask(GenTree node, byte simdSize, var_types simdBaseType)
    {
        assert(node.Type is TYP_MASK);
        if (node is GenTreeMskCon mask)
        {
            var elementCount = simdSize / simdBaseType.Size;
            mask.SimdMaskVal.u64[0] = unchecked((ulong)~mask.SimdMaskVal.RawBits) &
                unchecked((ulong)simdmask_t.GetBitMask(elementCount));
            return true;
        }

        if (node is GenTreeHWIntrinsic intrinsic)
        {
            if (intrinsic.HWIntrinsicId is NI_AVX512_OrMask)
            {
                var elementSize = intrinsic.SimdBaseType.Size;
                var first = intrinsic.GetOp(1);
                var second = intrinsic.GetOp(2);
                var transform = false;

                if (second is GenTreeHWIntrinsic notSecond &&
                    notSecond.HWIntrinsicId is NI_AVX512_NotMask &&
                    notSecond.SimdBaseType.Size == elementSize)
                {
                    second = notSecond.GetOp(1);
                    BlockRange().Remove(notSecond);
                    transform = true;
                }
                else if (first is GenTreeHWIntrinsic notFirst &&
                    notFirst.HWIntrinsicId is NI_AVX512_NotMask &&
                    notFirst.SimdBaseType.Size == elementSize)
                {
                    first = notFirst.GetOp(1);
                    BlockRange().Remove(notFirst);
                    (first, second) = (second, first);
                    transform = true;
                }

                if (transform)
                {
                    intrinsic.ChangeHWIntrinsicId(NI_AVX512_AndNotMask, first, second);
                    return true;
                }
            }

            var operation = intrinsic.GetOperForHWIntrinsicId(out var isScalar, getEffectiveOp: true);
            if (operation.IsCompare)
            {
                var reversed = CompilerInstance.GetHWIntrinsicIdForCmpOp(operation, TYP_MASK,
                    intrinsic.GetOp(1), intrinsic.GetOp(2), intrinsic.SimdBaseType, intrinsic.SimdSize,
                    isScalar, reverseCond: true);
                if (reversed is not NI_Illegal)
                {
                    intrinsic.ChangeHWIntrinsicId(reversed);
                    return true;
                }
            }
        }
        return false;
    }
#endif
}
