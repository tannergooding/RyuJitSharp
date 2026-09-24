// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_HW_INTRINSICS && FEATURE_MASKED_HW_INTRINSICS
namespace RyuJitSharp;

public partial class Compiler
{
    public GenTree gtFoldExprConvertVecCnsToMask(GenTreeHWIntrinsic tree, GenTreeVecCon vecCon)
    {
        assert(tree.IsConvertVectorToMask);
        assert((vecCon == tree.GetOp(1)) || (vecCon == tree.GetOp(2)));
        assert(varTypeIsMask(tree.Type));

        var mskCon = gtNewMskConNode(default);
        switch (vecCon.Type)
        {
            case TYP_SIMD8:
            case TYP_SIMD12:
            case TYP_SIMD16:
#if TARGET_XARCH
            case TYP_SIMD32:
            case TYP_SIMD64:
#endif
            {
                EvaluateSimdCvtVectorToMask(tree.SimdBaseType, ref mskCon.SimdMaskVal,
                    vecCon.SimdVal.AsSpan<byte>()[..vecCon.Type.Size]);
                break;
            }

#if TARGET_ARM64
            case TYP_SIMD:
            {
                NYI("ARM64 scalable vector-to-mask constant folding");
                fatal(CORJIT_IMPLLIMITATION);
                return tree;
            }
#endif

            default:
            {
                unreached();
                break;
            }
        }

        return mskCon;
    }
}
#endif
