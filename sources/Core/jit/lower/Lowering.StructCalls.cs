// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private unsafe void LowerCallStruct(GenTreeCall call)
    {
#if WINDOWS_AMD64_ABI
        assert(varTypeIsStruct(call.Type));
        if (call.HasMultiRegRetVal)
        {
            return;
        }

        // Windows x64 has no HFA returns; use the shared ABI classifier.
        var compiler = CompilerInstance;
        var returnType = compiler.GetReturnTypeForStruct(call.RetClsHnd, call.UnmanagedCallConv, out _);
        assert(returnType is not TYP_STRUCT and not TYP_UNKNOWN);
        var originalType = call.Type;
        call.Type = returnType.ActualType;

        if (BlockRange().TryGetUse(call, out var callUse))
        {
            var user = callUse.User();
            switch (user.Oper)
            {
                case GT_RETURN:
                case GT_STORE_LCL_VAR:
                case GT_STORE_BLK:
                {
                    // The user's lowering handles its remaining struct representation.
                    assert((user.Type == originalType) || varTypeIsSimd(user.Type));
                    break;
                }

                case GT_STORE_LCL_FLD:
                {
                    assert((user.Type == originalType) || (returnType == user.Type));
                    break;
                }

                case GT_CALL:
                case GT_FIELD_LIST:
                {
                    assert(varTypeIsSimd(originalType));
                    break;
                }

                case GT_STOREIND:
                {
#if FEATURE_SIMD
                    if (varTypeIsSimd(user.Type))
                    {
                        user.ChangeType(returnType);
                        break;
                    }
#endif
                    // The importer separately retypes helper-call results.
                    assert((user.Type is TYP_REF) ||
                        ((user.Type is TYP_I_IMPL) && compiler.IsTargetAbi(CORINFO_NATIVEAOT_ABI)));
                    assert(call.IsHelperCall());
                    assert(returnType == user.Type);
                    break;
                }

#if FEATURE_HW_INTRINSICS
                case GT_HWINTRINSIC:
                {
                    if (!varTypeUsesSameRegType(returnType, originalType))
                    {
                        var bitCast = compiler.gtNewBitCastNode(originalType, call);
                        BlockRange().InsertAfter(call, bitCast);
                        callUse.ReplaceWith(bitCast);
                        ContainCheckBitCast(bitCast);
                    }
                    break;
                }
#endif

                default:
                {
                    unreached();
                    break;
                }
            }
        }
#else
        throw new NotImplementedException("LowerCallStruct outside Windows AMD64 is not ported.");
#endif
    }
}
