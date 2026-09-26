// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class ValueNumStore
{
    private readonly record struct RelatedRelopEntry(VNFunc Relop0, VNFunc Relop1, int ConstantValue, VNFunc JointRelop);

    private static readonly RelatedRelopEntry[] s_relatedRelopTable_AND = [
        new(VNF_EQ, VNF_EQ, -1, VNF_EQ),
        new(VNF_EQ, VNF_NE, 0, VNF_COUNT),
        new(VNF_EQ, VNF_LE, -1, VNF_EQ),
        new(VNF_EQ, VNF_LT, 0, VNF_COUNT),
        new(VNF_EQ, VNF_GT, 0, VNF_COUNT),
        new(VNF_EQ, VNF_GE, -1, VNF_EQ),
        new(VNF_EQ, VNF_LE_UN, -1, VNF_EQ),
        new(VNF_EQ, VNF_LT_UN, 0, VNF_COUNT),
        new(VNF_EQ, VNF_GT_UN, 0, VNF_COUNT),
        new(VNF_EQ, VNF_GE_UN, -1, VNF_EQ),
        new(VNF_NE, VNF_EQ, 0, VNF_COUNT),
        new(VNF_NE, VNF_NE, -1, VNF_NE),
        new(VNF_NE, VNF_LE, -1, VNF_LT),
        new(VNF_NE, VNF_LT, -1, VNF_LT),
        new(VNF_NE, VNF_GT, -1, VNF_GT),
        new(VNF_NE, VNF_GE, -1, VNF_GT),
        new(VNF_NE, VNF_LE_UN, -1, VNF_LT_UN),
        new(VNF_NE, VNF_LT_UN, -1, VNF_LT_UN),
        new(VNF_NE, VNF_GT_UN, -1, VNF_GT_UN),
        new(VNF_NE, VNF_GE_UN, -1, VNF_GT_UN),
        new(VNF_LE, VNF_EQ, -1, VNF_EQ),
        new(VNF_LE, VNF_NE, -1, VNF_LT),
        new(VNF_LE, VNF_LE, -1, VNF_LE),
        new(VNF_LE, VNF_LT, -1, VNF_LT),
        new(VNF_LE, VNF_GT, 0, VNF_COUNT),
        new(VNF_LE, VNF_GE, -1, VNF_EQ),
        new(VNF_LT, VNF_EQ, 0, VNF_COUNT),
        new(VNF_LT, VNF_NE, -1, VNF_LT),
        new(VNF_LT, VNF_LE, -1, VNF_LT),
        new(VNF_LT, VNF_LT, -1, VNF_LT),
        new(VNF_LT, VNF_GT, 0, VNF_COUNT),
        new(VNF_LT, VNF_GE, 0, VNF_COUNT),
        new(VNF_GT, VNF_EQ, 0, VNF_COUNT),
        new(VNF_GT, VNF_NE, -1, VNF_GT),
        new(VNF_GT, VNF_LE, 0, VNF_COUNT),
        new(VNF_GT, VNF_LT, 0, VNF_COUNT),
        new(VNF_GT, VNF_GT, -1, VNF_GT),
        new(VNF_GT, VNF_GE, -1, VNF_GT),
        new(VNF_GE, VNF_EQ, -1, VNF_EQ),
        new(VNF_GE, VNF_NE, -1, VNF_GT),
        new(VNF_GE, VNF_LE, -1, VNF_EQ),
        new(VNF_GE, VNF_LT, 0, VNF_COUNT),
        new(VNF_GE, VNF_GT, -1, VNF_GT),
        new(VNF_GE, VNF_GE, -1, VNF_GE),
        new(VNF_LE_UN, VNF_EQ, -1, VNF_EQ),
        new(VNF_LE_UN, VNF_NE, -1, VNF_LT_UN),
        new(VNF_LE_UN, VNF_LE_UN, -1, VNF_LE_UN),
        new(VNF_LE_UN, VNF_LT_UN, -1, VNF_LT_UN),
        new(VNF_LE_UN, VNF_GT_UN, 0, VNF_COUNT),
        new(VNF_LE_UN, VNF_GE_UN, -1, VNF_EQ),
        new(VNF_LT_UN, VNF_EQ, 0, VNF_COUNT),
        new(VNF_LT_UN, VNF_NE, -1, VNF_LT_UN),
        new(VNF_LT_UN, VNF_LE_UN, -1, VNF_LT_UN),
        new(VNF_LT_UN, VNF_LT_UN, -1, VNF_LT_UN),
        new(VNF_LT_UN, VNF_GT_UN, 0, VNF_COUNT),
        new(VNF_LT_UN, VNF_GE_UN, 0, VNF_COUNT),
        new(VNF_GT_UN, VNF_EQ, 0, VNF_COUNT),
        new(VNF_GT_UN, VNF_NE, -1, VNF_GT_UN),
        new(VNF_GT_UN, VNF_LE_UN, 0, VNF_COUNT),
        new(VNF_GT_UN, VNF_LT_UN, 0, VNF_COUNT),
        new(VNF_GT_UN, VNF_GT_UN, -1, VNF_GT_UN),
        new(VNF_GT_UN, VNF_GE_UN, -1, VNF_GT_UN),
        new(VNF_GE_UN, VNF_EQ, -1, VNF_EQ),
        new(VNF_GE_UN, VNF_NE, -1, VNF_GT_UN),
        new(VNF_GE_UN, VNF_LE_UN, -1, VNF_EQ),
        new(VNF_GE_UN, VNF_LT_UN, 0, VNF_COUNT),
        new(VNF_GE_UN, VNF_GT_UN, -1, VNF_GT_UN),
        new(VNF_GE_UN, VNF_GE_UN, -1, VNF_GE_UN),
    ];

    private static readonly RelatedRelopEntry[] s_relatedRelopTable_OR = [
        new(VNF_EQ, VNF_EQ, -1, VNF_EQ),
        new(VNF_EQ, VNF_NE, 1, VNF_COUNT),
        new(VNF_EQ, VNF_LE, -1, VNF_LE),
        new(VNF_EQ, VNF_LT, -1, VNF_LE),
        new(VNF_EQ, VNF_GT, -1, VNF_GE),
        new(VNF_EQ, VNF_GE, -1, VNF_GE),
        new(VNF_EQ, VNF_LE_UN, -1, VNF_LE_UN),
        new(VNF_EQ, VNF_LT_UN, -1, VNF_LE_UN),
        new(VNF_EQ, VNF_GT_UN, -1, VNF_GE_UN),
        new(VNF_EQ, VNF_GE_UN, -1, VNF_GE_UN),
        new(VNF_NE, VNF_EQ, 1, VNF_COUNT),
        new(VNF_NE, VNF_NE, -1, VNF_NE),
        new(VNF_NE, VNF_LE, 1, VNF_COUNT),
        new(VNF_NE, VNF_LT, -1, VNF_NE),
        new(VNF_NE, VNF_GT, -1, VNF_NE),
        new(VNF_NE, VNF_GE, 1, VNF_COUNT),
        new(VNF_NE, VNF_LE_UN, 1, VNF_COUNT),
        new(VNF_NE, VNF_LT_UN, -1, VNF_NE),
        new(VNF_NE, VNF_GT_UN, -1, VNF_NE),
        new(VNF_NE, VNF_GE_UN, 1, VNF_COUNT),
        new(VNF_LE, VNF_EQ, -1, VNF_LE),
        new(VNF_LE, VNF_NE, 1, VNF_COUNT),
        new(VNF_LE, VNF_LE, -1, VNF_LE),
        new(VNF_LE, VNF_LT, -1, VNF_LE),
        new(VNF_LE, VNF_GT, 1, VNF_COUNT),
        new(VNF_LE, VNF_GE, 1, VNF_COUNT),
        new(VNF_LT, VNF_EQ, -1, VNF_LE),
        new(VNF_LT, VNF_NE, -1, VNF_NE),
        new(VNF_LT, VNF_LE, -1, VNF_LE),
        new(VNF_LT, VNF_LT, -1, VNF_LT),
        new(VNF_LT, VNF_GT, -1, VNF_NE),
        new(VNF_LT, VNF_GE, 1, VNF_COUNT),
        new(VNF_GT, VNF_EQ, -1, VNF_GE),
        new(VNF_GT, VNF_NE, -1, VNF_NE),
        new(VNF_GT, VNF_LE, 1, VNF_COUNT),
        new(VNF_GT, VNF_LT, -1, VNF_NE),
        new(VNF_GT, VNF_GT, -1, VNF_GT),
        new(VNF_GT, VNF_GE, -1, VNF_GE),
        new(VNF_GE, VNF_EQ, -1, VNF_GE),
        new(VNF_GE, VNF_NE, 1, VNF_COUNT),
        new(VNF_GE, VNF_LE, 1, VNF_COUNT),
        new(VNF_GE, VNF_LT, 1, VNF_COUNT),
        new(VNF_GE, VNF_GT, -1, VNF_GE),
        new(VNF_GE, VNF_GE, -1, VNF_GE),
        new(VNF_LE_UN, VNF_EQ, -1, VNF_LE_UN),
        new(VNF_LE_UN, VNF_NE, 1, VNF_COUNT),
        new(VNF_LE_UN, VNF_LE_UN, -1, VNF_LE_UN),
        new(VNF_LE_UN, VNF_LT_UN, -1, VNF_LE_UN),
        new(VNF_LE_UN, VNF_GT_UN, 1, VNF_COUNT),
        new(VNF_LE_UN, VNF_GE_UN, 1, VNF_COUNT),
        new(VNF_LT_UN, VNF_EQ, -1, VNF_LE_UN),
        new(VNF_LT_UN, VNF_NE, -1, VNF_NE),
        new(VNF_LT_UN, VNF_LE_UN, -1, VNF_LE_UN),
        new(VNF_LT_UN, VNF_LT_UN, -1, VNF_LT_UN),
        new(VNF_LT_UN, VNF_GT_UN, -1, VNF_NE),
        new(VNF_LT_UN, VNF_GE_UN, 1, VNF_COUNT),
        new(VNF_GT_UN, VNF_EQ, -1, VNF_GE_UN),
        new(VNF_GT_UN, VNF_NE, -1, VNF_NE),
        new(VNF_GT_UN, VNF_LE_UN, 1, VNF_COUNT),
        new(VNF_GT_UN, VNF_LT_UN, -1, VNF_NE),
        new(VNF_GT_UN, VNF_GT_UN, -1, VNF_GT_UN),
        new(VNF_GT_UN, VNF_GE_UN, -1, VNF_GE_UN),
        new(VNF_GE_UN, VNF_EQ, -1, VNF_GE_UN),
        new(VNF_GE_UN, VNF_NE, 1, VNF_COUNT),
        new(VNF_GE_UN, VNF_LE_UN, 1, VNF_COUNT),
        new(VNF_GE_UN, VNF_LT_UN, 1, VNF_COUNT),
        new(VNF_GE_UN, VNF_GT_UN, -1, VNF_GE_UN),
        new(VNF_GE_UN, VNF_GE_UN, -1, VNF_GE_UN),
    ];

    private ValueNum CombineRelatedRelops(var_types type, VNFunc func, ValueNum arg0VN, ValueNum arg1VN)
    {
        var first = new VNFuncApp();
        if (!GetVNFunc(arg0VN, ref first) || !VNFuncIsComparison(first.Func) ||
            varTypeIsFloating(TypeOfVN(first.GetArg(0))))
        {
            return NoVN;
        }

        var second = new VNFuncApp();
        if (!GetVNFunc(arg1VN, ref second) || !VNFuncIsComparison(second.Func) ||
            (first.GetArg(0) != second.GetArg(0)) || (first.GetArg(1) != second.GetArg(1)))
        {
            return NoVN;
        }

        var table = func == VNF_AND ? s_relatedRelopTable_AND : s_relatedRelopTable_OR;
        foreach (var entry in table)
        {
            if (((entry.Relop0 == first.Func) && (entry.Relop1 == second.Func)) ||
                ((entry.Relop0 == second.Func) && (entry.Relop1 == first.Func)))
            {
                return entry.ConstantValue switch {
                    1 => VNOneForType(type),
                    0 => VNZeroForType(type),
                    _ => VNForFunc(type, entry.JointRelop, first.GetArg(0), first.GetArg(1)),
                };
            }
        }

        return NoVN;
    }
}
