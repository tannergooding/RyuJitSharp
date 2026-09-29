// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class GenTree
{
    public var_types GetRegTypeByIndex(int regIndex)
    {
#if FEATURE_MULTIREG_RET
        if (IsMultiRegCall)
        {
            return AsCall().ReturnTypeDesc.GetReturnRegType(checked((byte)regIndex));
        }

#if !TARGET_64BIT
        if (Oper.IsMultiRegOp)
        {
            return AsMultiRegOp().GetRegType(checked((byte)regIndex));
        }
#endif
#endif
#if FEATURE_HW_INTRINSICS
        if (Oper.IsHWIntrinsic)
        {
            assert(Type is TYP_STRUCT);
#if TARGET_ARM64
            if (AsHWIntrinsic().SimdSize == 16)
            {
                return TYP_SIMD16;
            }

            assert(AsHWIntrinsic().SimdSize == 8);
            return TYP_SIMD8;
#elif TARGET_XARCH
            return AsHWIntrinsic().GetOp(1).Type;
#endif
        }
#endif
        if (Oper.IsScalarLocal)
        {
            if (Type is TYP_LONG)
            {
                return TYP_INT;
            }

            assert(Type is TYP_STRUCT);
            assert((Flags & GTF_VAR_MULTIREG) != 0);
            assert(false, "GetRegTypeByIndex for LclVar requires GetFieldTypeByIndex and a Compiler.");
        }

        throw new FatalJitException("Invalid node type for GetRegTypeByIndex.");
    }

    public GenTreeFlags GetRegSpillFlagByIdx(int regIndex)
    {
#if FEATURE_MULTIREG_RET
        if (IsMultiRegCall)
        {
            return AsCall().GetRegSpillFlagByIdx(checked((byte)regIndex));
        }

#if !TARGET_64BIT
        if (Oper.IsMultiRegOp)
        {
            return AsMultiRegOp().GetRegSpillFlagByIdx(checked((byte)regIndex));
        }
#endif
#endif
#if FEATURE_HW_INTRINSICS
        if (Oper.IsHWIntrinsic)
        {
            return AsHWIntrinsic().GetRegSpillFlagByIdx(checked((byte)regIndex));
        }
#endif
        if (Oper.IsScalarLocal)
        {
            return AsLclVar().GetRegSpillFlagByIdx(checked((byte)regIndex));
        }

        throw new FatalJitException("Invalid node type for GetRegSpillFlagByIdx.");
    }

    public void SetRegSpillFlagByIdx(GenTreeFlags flags, int regIndex)
    {
#if FEATURE_MULTIREG_RET
        if (IsMultiRegCall)
        {
            AsCall().SetRegSpillFlagByIdx(flags, checked((byte)regIndex));
            return;
        }

#if !TARGET_64BIT
        if (Oper.IsMultiRegOp)
        {
            AsMultiRegOp().SetRegSpillFlagByIdx(flags, checked((byte)regIndex));
            return;
        }
#endif
#endif
#if FEATURE_HW_INTRINSICS
        if (Oper.IsHWIntrinsic)
        {
            AsHWIntrinsic().SetRegSpillFlagByIdx(flags, checked((byte)regIndex));
            return;
        }
#endif
        if (Oper.IsScalarLocal)
        {
            AsLclVar().SetRegSpillFlagByIdx(flags, checked((byte)regIndex));
            return;
        }

        assert(false, "Invalid node type for SetRegSpillFlagByIdx");
        throw new FatalJitException("Invalid node type for indexed register spill flags.");
    }
}
