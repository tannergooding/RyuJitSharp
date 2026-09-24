// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public readonly partial struct HWIntrinsicInfo
{
#if FEATURE_HW_INTRINSICS
    public static instruction lookupIns(GenTreeHWIntrinsic intrinsicNode, Compiler? compiler)
    {
        assert(intrinsicNode is not null);
        var intrinsic = intrinsicNode.HWIntrinsicId;
        var type = lookupCategory(intrinsic) is HW_Category_Scalar
            ? intrinsicNode.Type
            : intrinsicNode.SimdBaseType;

        return lookupIns(intrinsic, type, compiler);
    }
#endif
}
