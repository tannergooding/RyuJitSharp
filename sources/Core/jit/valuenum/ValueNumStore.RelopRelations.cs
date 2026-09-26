// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, valuenum.h and valuenum.cpp.

namespace RyuJitSharp;

public sealed partial class ValueNumStore
{
    public enum VN_RELATION_KIND
    {
        VRK_Inferred,
        VRK_Same,
        VRK_Swap,
        VRK_Reverse,
        VRK_SwapReverse,
    }

    public genTreeOps VNRelopToGenTreeOp(VNFunc func, out bool isUnsigned)
    {
        isUnsigned = false;

        switch (func)
        {
            case VNF_LT:
            {
                return GT_LT;
            }

            case VNF_LE:
            {
                return GT_LE;
            }

            case VNF_GE:
            {
                return GT_GE;
            }

            case VNF_GT:
            {
                return GT_GT;
            }

            case VNF_EQ:
            {
                return GT_EQ;
            }

            case VNF_NE:
            {
                return GT_NE;
            }

            case VNF_LT_UN:
            {
                isUnsigned = true;
                return GT_LT;
            }

            case VNF_LE_UN:
            {
                isUnsigned = true;
                return GT_LE;
            }

            case VNF_GE_UN:
            {
                isUnsigned = true;
                return GT_GE;
            }

            case VNF_GT_UN:
            {
                isUnsigned = true;
                return GT_GT;
            }

            default:
            {
                return GT_NONE;
            }
        }
    }

    public ValueNum GetRelatedRelop(ValueNum vn, VN_RELATION_KIND relation)
    {
        assert(vn == VNNormalValue(vn));

        if (relation is VN_RELATION_KIND.VRK_Same)
        {
            return vn;
        }

        if ((relation is VN_RELATION_KIND.VRK_Inferred) || (vn == NoVN))
        {
            return NoVN;
        }

        var app = new VNFuncApp();
        if (!GetVNFunc(vn, ref app) || (app.Arity != 2) || varTypeIsFloating(TypeOfVN(app.GetArg(0))))
        {
            return NoVN;
        }

        var reverse = relation is VN_RELATION_KIND.VRK_Reverse or VN_RELATION_KIND.VRK_SwapReverse;
        var swap = relation is VN_RELATION_KIND.VRK_Swap or VN_RELATION_KIND.VRK_SwapReverse;
        var func = app.Func;

        if (swap)
        {
            func = SwapRelop(func);
            if (func == VNF_MemOpaque)
            {
                return NoVN;
            }
        }

        if (reverse)
        {
            if (func >= VNF_Boundary)
            {
                func = func switch {
                    VNF_LT_UN => VNF_GE_UN,
                    VNF_LE_UN => VNF_GT_UN,
                    VNF_GE_UN => VNF_LT_UN,
                    VNF_GT_UN => VNF_LE_UN,
                    _ => VNF_MemOpaque,
                };
                if (func == VNF_MemOpaque)
                {
                    return NoVN;
                }
            }
            else
            {
                var oper = (genTreeOps)func;
                if (!oper.IsCompare)
                {
                    return NoVN;
                }

                func = (VNFunc)oper.ReverseRelop;
            }
        }

        return VNForFunc(TYP_INT, func, app.GetArg(swap ? 1 : 0), app.GetArg(swap ? 0 : 1));
    }

    public static bool VNFuncIsSignedComparison(VNFunc func)
        => (func < VNF_Boundary) && ((genTreeOps)func).IsCompare;

#if DEBUG
    public static string VNFuncName(VNFunc func) => FuncName(func);
#endif

    public static string VNRelationString(VN_RELATION_KIND relation)
        => relation switch {
            VN_RELATION_KIND.VRK_Inferred => "inferred",
            VN_RELATION_KIND.VRK_Same => "same",
            VN_RELATION_KIND.VRK_Reverse => "reversed",
            VN_RELATION_KIND.VRK_Swap => "swapped",
            VN_RELATION_KIND.VRK_SwapReverse => "swapped and reversed",
            _ => "unknown vn relation",
        };
}
