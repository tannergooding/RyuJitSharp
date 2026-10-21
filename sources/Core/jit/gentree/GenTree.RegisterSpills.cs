// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class GenTree
{
    public var_types GetRegTypeByIndex(int regIndex)
    {
        if (Configuration.FEATURE_MULTIREG_RET)
        {
            if (IsMultiRegCall)
            {
                return AsCall().ReturnTypeDesc.GetReturnRegType(checked((byte)regIndex));
            }

            if (Configuration.TARGET_32BIT && Oper.IsMultiRegOp)
            {
                return AsMultiRegOp().GetRegType(checked((byte)regIndex));
            }
        }

        if (Configuration.FEATURE_HW_INTRINSICS && Oper.IsHWIntrinsic)
        {
            assert(Type is TYP_STRUCT);
            if (Configuration.TARGET_ARM64)
            {
                if (AsHWIntrinsic().SimdSize == 16)
                {
                    return TYP_SIMD16;
                }

                assert(AsHWIntrinsic().SimdSize == 8);
                return TYP_SIMD8;
            }

            if (Configuration.TARGET_XARCH)
            {
                return AsHWIntrinsic().GetOp(1).Type;
            }
        }
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

        FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.DirectFailureMessage228);
        return default!;
    }

    public GenTreeFlags GetRegSpillFlagByIdx(int regIndex)
    {
        if (Configuration.FEATURE_MULTIREG_RET)
        {
            if (IsMultiRegCall)
            {
                return AsCall().GetRegSpillFlagByIdx(checked((byte)regIndex));
            }

            if (Configuration.TARGET_32BIT && Oper.IsMultiRegOp)
            {
                return AsMultiRegOp().GetRegSpillFlagByIdx(checked((byte)regIndex));
            }
        }

        if (Configuration.FEATURE_HW_INTRINSICS && Oper.IsHWIntrinsic)
        {
            return AsHWIntrinsic().GetRegSpillFlagByIdx(checked((byte)regIndex));
        }
        if (Oper.IsScalarLocal)
        {
            return AsLclVar().GetRegSpillFlagByIdx(checked((byte)regIndex));
        }

        FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.DirectFailureMessage229);
        return default!;
    }

    public void SetRegSpillFlagByIdx(GenTreeFlags flags, int regIndex)
    {
        if (Configuration.FEATURE_MULTIREG_RET)
        {
            if (IsMultiRegCall)
            {
                AsCall().SetRegSpillFlagByIdx(flags, checked((byte)regIndex));
                return;
            }

            if (Configuration.TARGET_32BIT && Oper.IsMultiRegOp)
            {
                AsMultiRegOp().SetRegSpillFlagByIdx(flags, checked((byte)regIndex));
                return;
            }
        }

        if (Configuration.FEATURE_HW_INTRINSICS && Oper.IsHWIntrinsic)
        {
            AsHWIntrinsic().SetRegSpillFlagByIdx(flags, checked((byte)regIndex));
            return;
        }
        if (Oper.IsScalarLocal)
        {
            AsLclVar().SetRegSpillFlagByIdx(flags, checked((byte)regIndex));
            return;
        }

        assert(false, "Invalid node type for SetRegSpillFlagByIdx");
        FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.DirectFailureMessage230);
        return;
    }
}
