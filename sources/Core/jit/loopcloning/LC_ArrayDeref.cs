// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.

using System.Collections.Generic;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public sealed class LC_ArrayDeref
{
    public readonly LC_Array Array;
    public List<LC_ArrayDeref>? Children;
    public readonly int Level;

    public LC_ArrayDeref(LC_Array array, int level)
    {
        Array = array;
        Level = level;
    }

    public int Lcl()
        => (Level == 0) ? Array.ArrIndex.ArrLcl : Array.ArrIndex.IndLcls[Level - 1];

    public bool HasChildren() => (Children is not null) && (Children.Count > 0);

    public void EnsureChildren() => Children ??= [];

    public LC_ArrayDeref? Find(int lcl) => Find(Children, lcl);

    public static LC_ArrayDeref? Find(List<LC_ArrayDeref>? children, int lcl)
    {
        if (children is null)
        {
            return null;
        }
        foreach (var child in children)
        {
            if (child.Lcl() == lcl)
            {
                return child;
            }
        }
        return null;
    }

#if DEBUG
    public void Print(int indent = 0)
    {
        jitprintf($"{new string(' ', 4 * indent)}V{Lcl():D2}, level {Level} => {{");
        if (Children is not null)
        {
            for (var i = 0; i < Children.Count; i++)
            {
                if (i > 0)
                {
                    jitprintf(",");
                }
                jitprintf("\n");
                Children[i].Print(indent + 1);
            }
        }
        jitprintf($"\n{new string(' ', 4 * indent)}}}");
    }
#endif

    public void DeriveLevelConditions(List<List<LC_Condition>> conditions)
    {
        if (Level == 0)
        {
            conditions[0].Add(new LC_Condition(GT_NE,
                new LC_Expr(LC_Ident.CreateVar(Lcl(), Array.ArrIndex.ArrType)),
                new LC_Expr(LC_Ident.CreateNull(Array.ArrIndex.ArrType))));
        }
        else
        {
            var length = Array;
            length.Oper = LC_Array.OperType.ArrLen;
            length.Dim = Level - 1;
            conditions[Level * 2 - 1].Add(new LC_Condition(GT_LT,
                new LC_Expr(LC_Ident.CreateVar(Lcl(), TYP_INT)),
                new LC_Expr(LC_Ident.CreateArrAccess(length)), asUnsigned: true));

            var element = Array;
            element.Dim = Level;
            conditions[Level * 2].Add(new LC_Condition(GT_NE,
                new LC_Expr(LC_Ident.CreateArrAccess(element)),
                new LC_Expr(LC_Ident.CreateNull())));
        }

        if (Children is { Count: > 0 } children)
        {
            foreach (var child in children)
            {
                child.DeriveLevelConditions(conditions);
            }
        }
    }
}
