// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class GenTree
{
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
