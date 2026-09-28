// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;
#if FEATURE_HW_INTRINSICS
using static RyuJitSharp.HWIntrinsicCategory;
#endif

namespace RyuJitSharp;

public abstract partial class CSE_HeuristicCommon
{
    protected readonly Compiler m_compiler;
    protected readonly Compiler.codeOptimize codeOptKind;
    protected readonly bool enableConstCSE;

    protected CSE_HeuristicCommon(Compiler compiler)
    {
        m_compiler = compiler;
        codeOptKind = compiler.compCodeOpt;
        enableConstCSE = Compiler.optConstantCSEEnabled();
        cntCalleeTrashInt = compiler.CNT_CALLEE_TRASH_INT;
        cntCalleeTrashFlt = compiler.CNT_CALLEE_TRASH_FLOAT;
        cntCalleeTrashMsk = compiler.CNT_CALLEE_TRASH_MASK;
#if DEBUG
        JITDUMP($"CONST CSE is {(enableConstCSE ? "enabled" : "disabled")}\n");
#endif
    }

    public virtual void Initialize()
    {
    }

    public virtual bool ConsiderTree(GenTree tree, bool isReturn) => false;

    public bool CanConsiderTree(GenTree tree, bool isReturn)
    {
        if (tree.Oper.IsIntegralConst && !enableConstCSE &&
            !(tree.Oper is GT_CNS_INT &&
              tree.AsIntCon().IsIconHandle(GTF_ICON_STATIC_HDL, GTF_ICON_CLASS_HDL, GTF_ICON_STR_HDL)) &&
            !(tree.Oper is GT_CNS_INT && tree.AsIntCon().IsIconHandle(GTF_ICON_OBJ_HDL)))
        {
            return false;
        }

        if (varTypeIsStruct(tree.Type) && !varTypeIsSimd(tree.Type))
        {
            // CSE stores for non-SIMD struct returns are not fully re-morphed.
            if (isReturn || tree.IsMultiRegNode)
            {
                return false;
            }
        }

        if ((tree.Flags & (GTF_ASG | GTF_DONT_CSE)) != 0 || tree.Type is TYP_VOID)
        {
            return false;
        }

        var cost = codeOptKind is Compiler.SMALL_CODE ? tree.CostSz : tree.CostEx;
        if (cost < Compiler.CseMinCost)
        {
            return false;
        }

        switch (tree.Oper)
        {
            case GT_CALL:
            {
                var call = tree.AsCall();
                if ((call.IsHelperCall() && call.HelperNum.IsAllocator) ||
                    m_compiler.gtTreeHasSideEffects(tree, GTF_PERSISTENT_SIDE_EFFECTS, ignoreCctors: true))
                {
                    return false;
                }
                break;
            }

            case GT_IND:
            {
                if (tree.AsIndir().Addr.Oper is GT_ARR_ELEM)
                {
                    return false;
                }
                break;
            }

            case GT_CNS_LNG:
#if !TARGET_64BIT
            {
                return false;
            }
#endif
            case GT_CNS_INT:
            case GT_CNS_DBL:
            case GT_CNS_STR:
#if FEATURE_SIMD
            case GT_CNS_VEC:
#endif
#if FEATURE_MASKED_HW_INTRINSICS
            case GT_CNS_MSK:
#endif
            case GT_ARR_ELEM:
            case GT_ARR_LENGTH:
            case GT_MDARR_LENGTH:
            case GT_MDARR_LOWER_BOUND:
            case GT_NEG:
            case GT_NOT:
            case GT_BSWAP:
            case GT_BSWAP16:
            case GT_BITCAST:
            case GT_SUB:
            case GT_DIV:
            case GT_MOD:
            case GT_UDIV:
            case GT_UMOD:
            case GT_OR:
            case GT_AND:
            case GT_XOR:
            case GT_RSH:
            case GT_RSZ:
            case GT_ROL:
            case GT_ROR:
            case GT_EQ:
            case GT_NE:
            case GT_LT:
            case GT_LE:
            case GT_GE:
            case GT_GT:
            case GT_INTRINSIC:
            {
                break;
            }

            case GT_ADD:
            case GT_MUL:
            case GT_LSH:
            case GT_CAST:
            {
                if (tree.IsPartOfAddressMode)
                {
                    return false;
                }
                break;
            }

#if FEATURE_HW_INTRINSICS
            case GT_HWINTRINSIC:
            {
                var intrinsic = tree.AsHWIntrinsic();
                var category = HWIntrinsicInfo.lookupCategory(intrinsic.HWIntrinsicId);
#if TARGET_XARCH
                if (category is not (HW_Category_SimpleSIMD or HW_Category_IMM or HW_Category_Scalar or
                    HW_Category_SIMDScalar or HW_Category_Helper))
                {
                    return false;
                }
#elif TARGET_ARM64
                if (category is not (HW_Category_SIMD or HW_Category_SIMDByIndexedElement or
                    HW_Category_ShiftLeftByImmediate or HW_Category_ShiftRightByImmediate or
                    HW_Category_Scalar or HW_Category_Helper))
                {
                    return false;
                }
#else
                throw new System.NotImplementedException("CSE_HeuristicCommon.CanConsiderTree target intrinsic categories");
#endif
                if (intrinsic.IsMemoryStore(out _) || intrinsic.IsMemoryLoad())
                {
                    return false;
                }
                break;
            }
#endif

            case GT_BLK:
            case GT_LCL_FLD:
            {
                if (!varTypeIsEnregisterable(tree.Type))
                {
                    return false;
                }
                break;
            }

            case GT_COMMA:
            {
                if (tree.EffectiveVal.Oper is GT_FIELD_LIST)
                {
                    return false;
                }
                break;
            }

            default:
            {
                return false;
            }
        }

        var store = m_compiler.vnStore;
        assert(store is not null);

        var valueVN = store.VNNormalValue(tree._vnPair.Liberal);
        if ((valueVN is >= ValueNumStore.RecursiveVN and < 3) && (valueVN != ValueNumStore.VNForNull()))
        {
            return false;
        }

        // Assertion propagation uses the conservative VN for constant non-leaf expressions.
        if (!tree.Oper.IsLeaf && store.IsVNConstant(store.VNNormalValue(tree._vnPair.Conservative)))
        {
            return false;
        }

        return true;
    }
}

public sealed partial class CSE_Heuristic : CSE_HeuristicCommon
{
    public CSE_Heuristic(Compiler compiler) : base(compiler)
    {
    }

    public override bool ConsiderTree(GenTree tree, bool isReturn)
        => CanConsiderTree(tree, isReturn);
}
