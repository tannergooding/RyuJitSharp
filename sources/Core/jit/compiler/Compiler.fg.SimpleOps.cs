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

    public GenTree fgOptimizeCast(GenTreeCast cast)
    {
        var source = cast.CastOp;
        if (varTypeIsIntegral(cast.Type) && varTypeIsIntegral(source.Type))
        {
            var sourceRange = IntegralRange.ForNode(source, this);
            var nonOverflowRange = IntegralRange.ForCastInput(cast);
            if (nonOverflowRange.Contains(sourceRange))
            {
                if (cast.Type.ActualType == source.Type.ActualType)
                {
                    return source;
                }

                cast.Flags &= ~GTF_OVERFLOW;
                cast.SetAllEffectsFlags(source);
                if ((source.Type.ActualType is TYP_INT) && (cast.Type is TYP_LONG) && sourceRange.IsNonNegative)
                {
                    cast.Flags |= GTF_UNSIGNED;
                }
            }

            if (cast.HasOverflowCheck)
            {
                return cast;
            }

            var castType = cast.CastType;
            if (varTypeIsSmall(castType) && (source.Oper is GT_CAST) && !source.HasOverflowCheck)
            {
                var widening = source.AsCast();
                if (varTypeIsLong(widening.CastType) && (widening.CastOp.Type.ActualType is TYP_INT))
                {
                    cast.Op1 = widening.CastOp;
                    source = cast.CastOp;
                }
            }

            if (varTypeIsSmall(castType) && (castType.Size == source.Type.Size) &&
                (source.Oper is GT_IND or GT_LCL_FLD))
            {
                source.ChangeType(castType);
                source._vnPair = cast._vnPair;
                return source;
            }

            if (opts.OptEnabled(CLFLG_TREETRANS) && (source.Type.Size > castType.Size) &&
                optNarrowTree(ref source, source.Type, castType, cast._vnPair, false))
            {
                _ = optNarrowTree(ref source, source.Type, castType, cast._vnPair, true);
                if ((source.Oper is GT_CAST) && (source.AsCast().CastType == source.AsCast().CastOp.Type.ActualType))
                {
                    source = source.AsCast().CastOp;
                }

                return source;
            }

            if (opts.OptimizationEnabled && (source.Oper is GT_CAST) && !source.HasOverflowCheck)
            {
                var sourceCastType = source.AsCast().CastType;
                if (varTypeIsSmall(sourceCastType) && (castType.Size <= sourceCastType.Size))
                {
                    cast.Op1 = source.AsCast().CastOp;
                }
            }
        }

        return cast;
    }

    public GenTree fgOptimizeCastOnStore(GenTree store)
    {
        assert(store.Oper.IsStore);
        var source = store.Data;
        if (source.Oper is not GT_CAST)
        {
            return store;
        }

        if (store.Oper is GT_STORE_LCL_VAR)
        {
            ref var variable = ref lvaGetDesc(store.AsLclVarCommon().LclNum);
            // Assertion propagation can assume normalized values for NOL
            // locals; only address exposure guarantees normalization on every use.
            if (!variable.lvNormalizeOnLoad || !variable.IsAddressExposed)
            {
                return store;
            }
        }

        if (source.HasOverflowCheck)
        {
            return store;
        }

        var cast = source.AsCast();
        var castToType = cast.CastType;
        var castFromType = cast.CastOp.Type;
        if (!varTypeIsSmall(store.Type) || !varTypeIsSmall(castToType) ||
            !varTypeIsIntegral(castFromType) || (castToType.Size < store.Type.Size))
        {
            return store;
        }

        if (castFromType.ActualType == castToType.ActualType)
        {
            store.DataRef = cast.CastOp;
        }
        else
        {
            cast.CastType = castToType.ActualType;
            store.DataRef = fgOptimizeCast(cast);
        }

        return store;
    }

    public static bool fgIsThrow(GenTree tree)
    {
        if (tree.Oper is not GT_CALL)
        {
            return false;
        }

        var call = tree.AsCall();
        if (call.IsHelperCall() && call.HelperNum.AlwaysThrow)
        {
            assert(call.IsNoReturn);
            noway_assert((call.Flags & GTF_EXCEPT) != 0);
            return true;
        }

        return false;
    }

    public bool fgIsCommaThrow(GenTree tree, bool forFolding = false)
    {
        if (forFolding && compStressCompile(STRESS_FOLD, 50))
        {
            return false;
        }

        return (tree.Oper is GT_COMMA) &&
            ((tree.Flags & (GTF_CALL | GTF_EXCEPT)) == (GTF_CALL | GTF_EXCEPT)) &&
            fgIsThrow(tree.AsOp().Op1);
    }

    public GenTree? fgPropagateCommaThrow(GenTree parent, GenTreeOp commaThrow, GenTreeFlags precedingSideEffects)
    {
        assert(fgGlobalMorph);
        assert(fgIsCommaThrow(commaThrow));
        if ((commaThrow.Flags & GTF_COLON_COND) == 0)
        {
            fgRemoveRestOfBlock = true;
        }

        if ((precedingSideEffects & GTF_ALL_EFFECT) == 0)
        {
            if (parent.Type is TYP_VOID)
            {
                return commaThrow.Op1;
            }

            if (parent.Type.ActualType != commaThrow.Type.ActualType)
            {
                commaThrow.Op2 = commaThrow.Op2.BashToZeroConst(parent.Type.ActualType);
                commaThrow.ChangeType(parent.Type.ActualType);
            }

            return commaThrow;
        }

        return null;
    }

    public GenTree fgMorphRetInd(GenTreeUnOp ret)
    {
        assert(ret.Oper is GT_RETURN or GT_SWIFT_ERROR_RET);
#if SWIFT_SUPPORT
        var value = ret.Oper is GT_SWIFT_ERROR_RET ? ret.AsOp().Op2 : ret.Op1;
#else
        var value = ret.Op1;
#endif
        assert(value.Oper is GT_LCL_FLD);
        var field = value.AsLclFld();
        var localNumber = field.LclNum;
        if (fgGlobalMorph && varTypeIsStruct(field.Type) && !lvaIsImplicitByRefLocal(localNumber))
        {
            ref var descriptor = ref lvaGetDesc(localNumber);
            var fieldSize = field.Size;
            var localSize = lvaLclExactSize(localNumber);
            assert(fieldSize <= localSize);
#if TARGET_64BIT
            var canFold = fieldSize == localSize;
#else
            // Long/double bitcasts are not supported on 32-bit targets.
            var canFold = (fieldSize == localSize) && (localSize <= REGSIZE_BYTES);
#endif
            if (canFold)
            {
                // Lowering handles differing return types. Keep the whole local
                // enregisterable instead of exposing it through a field access.
                assert(field.LclOffs == 0);
                return new GenTreeLclVar(GT_LCL_VAR, descriptor.Type, localNumber, data: null, field, NodeThreading.None);
            }
            else if (!descriptor.lvDoNotEnregister)
            {
                lvaSetVarDoNotEnregister(localNumber, DoNotEnregisterReason.BlockOpRet);
            }
        }

        return field;
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
