// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
#if DEBUG
    private static void CheckCallArg(GenTree arg)
    {
        if (!arg.IsValue && !arg.Oper.IsPutArgStk)
        {
            assert(arg.Oper.IsStore);
            return;
        }

        switch (arg.Oper)
        {
            case GT_FIELD_LIST:
            {
                var list = arg.AsFieldList();
                assert(list.IsContained);
                foreach (var use in list.Uses)
                {
#if HAS_FIXED_REGISTER_SET
                    assert(use.Node.Oper.IsPutArg);
#endif
                }
                break;
            }

            default:
            {
#if HAS_FIXED_REGISTER_SET
                assert(arg.Oper.IsPutArg);
#endif
                break;
            }
        }
    }

    private static void CheckCall(GenTreeCall call)
    {
        foreach (var arg in call.Args.EarlyArgs)
        {
            CheckCallArg(arg.EarlyNodeRef);
        }
        foreach (var arg in call.Args.LateArgs)
        {
            CheckCallArg(arg.LateNodeRef);
        }
    }

    private static void CheckNode(Compiler compiler, GenTree node)
    {
        switch (node.Oper)
        {
            case GT_CALL:
            {
                CheckCall(node.AsCall());
                break;
            }

#if FEATURE_SIMD
            case GT_HWINTRINSIC:
            {
#if !TARGET_WASM
                assert(node.Type is not TYP_SIMD12);
#endif
                break;
            }
#endif

            case GT_LCL_VAR:
            case GT_STORE_LCL_VAR:
            {
                ref var descriptor = ref compiler.lvaGetDesc(node.AsLclVar().LclNum);
#if FEATURE_SIMD && TARGET_64BIT
                if (node.Type is TYP_SIMD12)
                {
                    assert(compiler.lvaIsFieldOfDependentlyPromotedStruct(in descriptor) ||
                        (compiler.lvaLclStackHomeSize(node.AsLclVar().LclNum) == 12));
                }
#endif
                if (descriptor.lvPromoted)
                {
                    assert(descriptor.lvDoNotEnregister || ((node.Oper is GT_STORE_LCL_VAR) && descriptor.lvIsMultiRegDest));
                }
                break;
            }

            case GT_LCL_ADDR:
            {
                var address = node.AsLclVarCommon();
                ref var descriptor = ref compiler.lvaGetDesc(address.LclNum);
#if !TARGET_WASM
                if (((address.Flags & GTF_VAR_DEF) != 0) && descriptor.HasGCPtr)
                {
                    // An uncontained address definition cannot report a tracked
                    // scalar GC local becoming live through the indirect store.
                    assert(address.IsContained || !descriptor.lvTracked || varTypeIsStruct(descriptor.Type));
                }
#endif
                assert(descriptor.lvDoNotEnregister);
                break;
            }

            case GT_PHI:
            case GT_PHI_ARG:
            {
                assert(false, "Should not see phi nodes after rationalize");
                break;
            }

            case GT_LCL_FLD:
            case GT_STORE_LCL_FLD:
            {
                ref var descriptor = ref compiler.lvaGetDesc(node.AsLclFld().LclNum);
                assert(descriptor.lvDoNotEnregister);
                break;
            }
        }
    }

    private static bool CheckBlock(Compiler compiler, BasicBlock block)
    {
        assert(block.IsEmpty || block.IsLIR);
        foreach (var node in block)
        {
            CheckNode(compiler, node);
        }

        return true;
    }
#endif
}
