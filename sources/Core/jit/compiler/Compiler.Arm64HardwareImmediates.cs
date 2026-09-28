// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_HW_INTRINSICS && TARGET_ARM64
namespace RyuJitSharp;

public partial class Compiler
{
    public unsafe void getHWIntrinsicImmTypes(NamedIntrinsic intrinsic, in CORINFO_SIG_INFO signature,
        int number, ref byte size, ref var_types baseType)
    {
        var sig = signature;
        var argument = sig.args;
        if (HWIntrinsicInfo.lookupCategory(intrinsic) is HW_Category_SIMDByIndexedElement)
        {
            assert(number == 1);
            switch (sig.numArgs)
            {
                case 4:
                {
                    argument = info.compCompHnd->getArgNext(argument);
                    goto case 3;
                }
                case 3:
                {
                    argument = info.compCompHnd->getArgNext(argument);
                    goto case 2;
                }
                case 2:
                {
                    var type = info.compCompHnd->getArgClass(&sig, argument);
                    baseType = getBaseTypeAndSizeOfSimdType(type, out var byteCount);
                    size = (byte)byteCount;
                    break;
                }
                default:
                {
                    unreached();
                    break;
                }
            }
        }
        else if ((intrinsic is NI_AdvSimd_Arm64_InsertSelectedScalar) && (number == 2))
        {
            argument = info.compCompHnd->getArgNext(argument);
            argument = info.compCompHnd->getArgNext(argument);
            var type = info.compCompHnd->getArgClass(&sig, argument);
            baseType = getBaseTypeAndSizeOfSimdType(type, out var byteCount);
            size = (byte)byteCount;
        }
    }
}
#endif
