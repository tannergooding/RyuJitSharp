// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, optimizemaskconversions.cpp.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;

namespace RyuJitSharp;

public partial class Compiler
{
    private PhaseStatus fgOptimizeMaskConversions()
    {
#if FEATURE_MASKED_HW_INTRINSICS
        if (opts.OptimizationDisabled)
        {
            JITDUMP("Skipping. Optimizations Disabled\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }

#if DEBUG
        if (JitConfig.JitDoOptimizeMaskConversions == 0)
        {
            JITDUMP("Skipping. Disable by config option\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }
#endif

        if (!compMaskConvertUsed)
        {
            JITDUMP("Skipping. There are no converts of locals\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }

        var weights = new Dictionary<int, MaskConversionsWeight>();
        var foundConversion = false;
        JITDUMP("\n");

        foreach (var block in Blocks)
        {
            foreach (var statement in block.Statements)
            {
                _ = statement.VisitLogicalLocalOccurrencesViaLocalsTreeList(occurrence => {
                    if (varTypeIsSimdOrMask(lvaGetDesc(occurrence.LclNum).Type))
                    {
                        var visitor = new MaskConversionsCheckVisitor(this, block.getBBWeight(this), weights);
                        var root = statement.RootNode;
                        _ = visitor.WalkTree(ref root, null);
                        foundConversion |= visitor.FoundConversions;
                        return GenTree.VisitResult.Abort;
                    }
                    return GenTree.VisitResult.Continue;
                });
            }
        }

        if (!foundConversion)
        {
            JITDUMP("Done. No conversions of locals found.\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }

        JITDUMP("\n");
        foreach (var block in Blocks)
        {
            foreach (var statement in block.Statements)
            {
                _ = statement.VisitLogicalLocalOccurrencesViaLocalsTreeList(occurrence => {
                    if ((occurrence.Node.Oper is not GT_LCL_ADDR) &&
                        varTypeIsSimdOrMask(occurrence.GetAccessType(this)))
                    {
                        var visitor = new MaskConversionsUpdateVisitor(this, weights);
                        var root = statement.RootNode;
                        _ = visitor.WalkTree(ref root, null);

                        if (visitor.UpdatedConversions)
                        {
                            fgSequenceLocals(statement);
                        }
                        return GenTree.VisitResult.Abort;
                    }
                    return GenTree.VisitResult.Continue;
                });
            }
        }

        return PhaseStatus.MODIFIED_EVERYTHING;
#else
        return PhaseStatus.MODIFIED_NOTHING;
#endif
    }

#if FEATURE_MASKED_HW_INTRINSICS
    // Compare weighted instruction costs across every definition/use of a local,
    // not just a single live range. Ties retain the vector representation.
    private sealed class MaskConversionsWeight
    {
        public double CurrentCost;
        public double SwitchCost;
        public bool Invalid;
        public var_types SimdBaseType = TYP_UNDEF;
        public byte SimdSize;

        private const double CostOfConvertMaskToVector = 1.0;
#if TARGET_ARM64
        private const double CostOfConvertVectorToMask = 2.0;
#else
        private const double CostOfConvertVectorToMask = 1.0;
#endif

        public void UpdateWeight(bool isStore, bool hasConvert, double blockWeight)
        {
            if (hasConvert)
            {
                var cost = isStore ? CostOfConvertMaskToVector : CostOfConvertVectorToMask;
                cost *= blockWeight;
                JITDUMP($"Incrementing currentCost by {cost:F2}. ");
                CurrentCost += cost;
            }
            else
            {
                var cost = isStore ? CostOfConvertVectorToMask : CostOfConvertMaskToVector;
                cost *= blockWeight;
                JITDUMP($"Incrementing switchCost by {cost:F2}. ");
                SwitchCost += cost;
            }

            DumpTotalWeight();
        }

        public void InvalidateWeight()
        {
            JITDUMP("Invalidating weight. ");
            Invalid = true;
            DumpTotalWeight();
        }

        public void DumpTotalWeight()
        {
            JITDUMP($"Weighting: {(Invalid ? "Invalid" : "")}{{{CurrentCost:F2}c {SwitchCost:F2}s}}\n");
        }

        public void CacheSimdTypes(GenTreeHWIntrinsic op, int lclNum)
        {
            var newSimdBaseType = op.SimdBaseType;
            var newSimdSize = op.SimdSize;
            assert(newSimdBaseType is not TYP_UNDEF);

            if (SimdBaseType is TYP_UNDEF)
            {
                SimdBaseType = newSimdBaseType;
                SimdSize = newSimdSize;
            }
            else if ((SimdBaseType != newSimdBaseType) || (SimdSize != newSimdSize))
            {
                JITDUMP($"Local V{lclNum:D2} has different types: ({(int)SimdBaseType}, {SimdSize}) vs ({(int)newSimdBaseType}, {newSimdSize}). ");
                InvalidateWeight();
            }
        }
    }

    private struct MaskConversionsCheckVisitor : IGenTreeVisitor<MaskConversionsCheckVisitor>
    {
        private readonly Compiler _compiler;
        private readonly double _blockWeight;
        private readonly Dictionary<int, MaskConversionsWeight> _weights;
        private readonly GenTreeStack _ancestors;

        public MaskConversionsCheckVisitor(Compiler compiler, double blockWeight, Dictionary<int, MaskConversionsWeight> weights)
        {
            _compiler = compiler;
            _blockWeight = blockWeight;
            _weights = weights;
            _ancestors = [];
        }

        public static bool DoPreOrder => true;
        public static bool UseExecutionOrder => true;
        public static bool DoLclVarsOnly => true;
        public bool FoundConversions;

        public fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
        {
            var local = use.AsLclVarCommon();
            ref var descriptor = ref _compiler.lvaGetDesc(local.LclNum);
            if (!varTypeIsSimdOrMask(descriptor.Type))
            {
                return WALK_CONTINUE;
            }

            if (!_weights.TryGetValue(local.LclNum, out var weight))
            {
                weight = new MaskConversionsWeight();
                _weights.Add(local.LclNum, weight);
            }

#if DEBUG
            JITDUMP($"{local.Oper.Name} V{local.LclNum:D2} at [{local.TreeId:D6}] ");
#endif
            GenTreeHWIntrinsic? convertOp = null;
            var isLocalStore = false;
            var isLocalUse = false;
            var hasConversion = false;

            switch (local.Oper)
            {
                case GT_STORE_LCL_VAR:
                {
                    isLocalStore = true;
                    if (local.Data.IsConvertMaskToVector)
                    {
                        convertOp = local.Data.AsHWIntrinsic();
                        hasConversion = true;
                    }
                    break;
                }

                case GT_LCL_VAR:
                {
                    isLocalUse = true;
                    if ((user is not null) && user.IsConvertVectorToMask)
                    {
                        convertOp = user.AsHWIntrinsic();
                        hasConversion = true;
                    }
                    break;
                }

                default:
                {
                    weight.InvalidateWeight();
                    JITDUMP("is unhandled. ");
                    return WALK_CONTINUE;
                }
            }

            if (isLocalStore || isLocalUse)
            {
                if (descriptor.IsAddressExposed)
                {
                    JITDUMP("is address exposed. ");
                    weight.InvalidateWeight();
                    return WALK_CONTINUE;
                }

                if (descriptor.lvIsStructField)
                {
                    JITDUMP("is struct field. ");
                    weight.InvalidateWeight();
                    return WALK_CONTINUE;
                }

                // A mask retains one bit per lane. Vector uses, incoming parameters
                // and OSR locals may contain other bits that conversion would lose.
                if (isLocalUse && !hasConversion)
                {
                    JITDUMP("is used as vector. ");
                    weight.InvalidateWeight();
                    return WALK_CONTINUE;
                }
                else if (descriptor.lvIsParam)
                {
                    JITDUMP("is parameter. ");
                    weight.InvalidateWeight();
                    return WALK_CONTINUE;
                }
                else if (descriptor.lvIsOSRLocal)
                {
                    JITDUMP("is OSR local. ");
                    weight.InvalidateWeight();
                    return WALK_CONTINUE;
                }

                JITDUMP($"has {(hasConversion ? "mask" : "no")} conversion. ");
                weight.UpdateWeight(isLocalStore, hasConversion, _blockWeight);
                if (hasConversion)
                {
                    assert(convertOp is not null);
                    weight.CacheSimdTypes(convertOp, local.LclNum);
                }
                FoundConversions |= hasConversion;
            }

            return WALK_CONTINUE;
        }

        public readonly fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user) => WALK_CONTINUE;

        public fgWalkResult WalkTree(ref GenTree use, GenTree? user)
            => IGenTreeVisitor<MaskConversionsCheckVisitor>.WalkTree(ref this, ref use, user, _ancestors);
    }

    private struct MaskConversionsUpdateVisitor : IGenTreeVisitor<MaskConversionsUpdateVisitor>
    {
        private readonly Compiler _compiler;
        private readonly Dictionary<int, MaskConversionsWeight> _weights;
        private readonly GenTreeStack _ancestors;

        public MaskConversionsUpdateVisitor(Compiler compiler, Dictionary<int, MaskConversionsWeight> weights)
        {
            _compiler = compiler;
            _weights = weights;
            _ancestors = [];
        }

        public static bool DoPostOrder => true;
        public static bool UseExecutionOrder => true;
        public bool UpdatedConversions;

        public readonly fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user) => WALK_CONTINUE;

        public fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user)
        {
#if TARGET_ARM64
            const int ConvertVectorToMaskValueOp = 2;
#else
            const int ConvertVectorToMaskValueOp = 1;
#endif
            GenTreeLclVarCommon local;
            var isLocalStore = false;
            var isLocalUse = false;
            var addConversion = false;
            var removeConversion = false;

            if ((use.Oper is GT_STORE_LCL_VAR) && use.AsLclVarCommon().Data.IsConvertMaskToVector)
            {
                local = use.AsLclVarCommon();
                isLocalStore = true;
                removeConversion = true;
            }
            else if ((use.Oper is GT_STORE_LCL_VAR) && !use.AsLclVarCommon().Data.IsConvertMaskToVector)
            {
                local = use.AsLclVarCommon();
                isLocalStore = true;
                addConversion = true;
            }
            else if (use.IsConvertVectorToMask &&
                     (use.AsHWIntrinsic().GetOp(ConvertVectorToMaskValueOp).Oper is GT_LCL_VAR))
            {
                local = use.AsHWIntrinsic().GetOp(ConvertVectorToMaskValueOp).AsLclVarCommon();
                isLocalUse = true;
                removeConversion = true;
            }
            else if ((use.Oper is GT_LCL_VAR) && ((user is null) || !user.IsConvertVectorToMask))
            {
                local = use.AsLclVar();
                isLocalUse = true;
                addConversion = true;
            }
            else
            {
                return WALK_CONTINUE;
            }

            assert(isLocalStore != isLocalUse);
            assert(addConversion != removeConversion);
            if (!_weights.TryGetValue(local.LclNum, out var weight))
            {
                return WALK_CONTINUE;
            }

            if ((weight.CurrentCost <= weight.SwitchCost) || weight.Invalid)
            {
#if DEBUG
                JITDUMP($"Local {(isLocalStore ? "store" : "use")} V{local.LclNum:D2} at [{local.TreeId:D6}] will not be converted. ");
#endif
                weight.DumpTotalWeight();
                return WALK_CONTINUE;
            }

#if DEBUG
            JITDUMP($"Local {(isLocalStore ? "store" : "use")} V{local.LclNum:D2} at [{local.TreeId:D6}] will be converted. ");
#endif
            weight.DumpTotalWeight();
            assert(local.Type is not TYP_MASK);
            var originalType = local.Type;
            local.Type = TYP_MASK;
            ref var descriptor = ref _compiler.lvaGetDesc(local.LclNum);
            assert(varTypeIsSimdOrMask(descriptor.Type));
            descriptor.Type = TYP_MASK;

            if (isLocalStore && removeConversion)
            {
                local.DataRef = local.Data.AsHWIntrinsic().GetOp(1);
            }
            else if (isLocalStore && addConversion)
            {
                assert(weight.SimdBaseType is not TYP_UNDEF);
                local.DataRef = _compiler.gtNewSimdCvtVectorToMaskNode(TYP_MASK, local.Data, weight.SimdBaseType, weight.SimdSize);
            }
            else if (isLocalUse && removeConversion)
            {
                use = local;
            }
            else if (isLocalUse && addConversion)
            {
                assert(weight.SimdBaseType is not TYP_UNDEF);
                use = _compiler.gtNewSimdCvtMaskToVectorNode(originalType, local, weight.SimdBaseType, weight.SimdSize);
            }

#if DEBUG
            JITDUMP($"Updated {(isLocalStore ? "store" : "use")} V{local.LclNum:D2} at [{local.TreeId:D6}] to mask ({(addConversion ? "added" : "removed")} conversion)\n");
#endif
            DISPTREE(use);
            UpdatedConversions = true;
            return WALK_CONTINUE;
        }

        public fgWalkResult WalkTree(ref GenTree use, GenTree? user)
            => IGenTreeVisitor<MaskConversionsUpdateVisitor>.WalkTree(ref this, ref use, user, _ancestors);
    }
#endif
}
