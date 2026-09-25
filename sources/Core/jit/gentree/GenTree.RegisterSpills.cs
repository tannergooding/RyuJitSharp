// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class GenTree
{
    public var_types GetRegTypeByIndex(int regIndex)
    {
#if TARGET_AMD64 && !UNIX_AMD64_ABI
#if FEATURE_HW_INTRINSICS
        if (Oper.IsHWIntrinsic)
        {
            assert(Type is TYP_STRUCT);
            return AsHWIntrinsic().GetOp(1).Type;
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
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Indexed register types outside Windows AMD64 are not implemented.");
#endif
    }

    public GenTreeFlags GetRegSpillFlagByIdx(int regIndex)
    {
#if TARGET_AMD64 && !UNIX_AMD64_ABI
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
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Indexed register spill flags outside Windows AMD64 are not implemented.");
#endif
    }

    public void SetRegSpillFlagByIdx(GenTreeFlags flags, int regIndex)
    {
#if TARGET_AMD64 && !UNIX_AMD64_ABI
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
#else
        NYI("GenTree.SetRegSpillFlagByIdx outside Windows AMD64");
        throw new FatalJitException("GenTree.SetRegSpillFlagByIdx outside Windows AMD64.");
#endif
    }
}
