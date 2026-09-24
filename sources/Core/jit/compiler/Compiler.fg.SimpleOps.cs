// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.

namespace RyuJitSharp;

public partial class Compiler
{
    public bool fgTryReplaceStructLocalWithFields(ref GenTree use)
    {
        if (use.Oper is not GT_LCL_VAR)
        {
            return false;
        }

        ref var variable = ref lvaGetDesc(use.AsLclVar().LclNum);
        if (variable.lvDoNotEnregister || !variable.lvPromoted)
        {
            return false;
        }

        use = fgMorphLclToFieldList(use.AsLclVar());
        return true;
    }

    public GenTree? fgMorphFinalizeIndir(GenTreeIndir indirection)
    {
        assert(indirection.Oper.IsIndir);
        var address = indirection.Addr;
#if TARGET_ARM
        if (varTypeIsFloating(indirection.Type))
        {
            var effectiveAddress = address.EffectiveVal;
            gtPeelOffsets(ref effectiveAddress, out var offset);
            if (((offset % TYP_FLOAT.Size) != 0) ||
                (effectiveAddress.Oper.IsCnsIntOrI && ((effectiveAddress.AsIntConCommon().IconValue % TYP_FLOAT.Size) != 0)))
            {
                indirection.Flags |= GTF_IND_UNALIGNED;
            }
        }
#endif
        if (!indirection.IsVolatile && (indirection.Type is not TYP_STRUCT) && (address.Oper is GT_LCL_ADDR))
        {
            var local = address.AsLclFld();
            var offset = local.LclOffs;
            if (!IsWideAccess(local.LclNum, offset, indirection.ValueSize))
            {
                // These operators share GenTreeLclFld; retagging preserves
                // logical identity without changing the managed node type.
                local.ChangeType(indirection.Type);
                if (indirection.Oper is GT_STOREIND)
                {
                    var value = indirection.Data;
                    local.SetOper(GT_STORE_LCL_FLD);
                    local.DataRef = value;
                    local.Flags |= GTF_ASG | GTF_VAR_DEF;
                    local.AddAllEffectsFlags(value);
                }
                else
                {
                    assert(indirection.Oper is GT_IND);
                    local.SetOper(GT_LCL_FLD);
                }

                local.LclOffs = offset;
                local._vnPair = indirection._vnPair;
                local.AddAllEffectsFlags(indirection.Flags & GTF_GLOB_REF);
                if ((local.Oper is GT_STORE_LCL_FLD) && local.IsPartial(this))
                {
                    local.Flags |= GTF_VAR_USEASG;
                }

                return local;
            }
        }

        return null;
    }

    public GenTree? fgOptimizeBitCast(GenTreeUnOp bitCast)
    {
        if (opts.OptimizationDisabled)
        {
            return null;
        }

        var source = bitCast.Op1;
        if ((source.Oper is GT_IND or GT_LCL_FLD) && (source.Type.Size == bitCast.Type.Size))
        {
            source.ChangeType(bitCast.Type);
            source._vnPair = bitCast._vnPair;
            return source;
        }

        return null;
    }
}
