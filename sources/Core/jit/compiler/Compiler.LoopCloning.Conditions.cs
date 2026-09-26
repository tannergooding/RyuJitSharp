// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public partial class Compiler
{
    private unsafe bool optDeriveLoopCloningConditions(FlowGraphNaturalLoop loop, LoopCloneContext context)
    {
        JITDUMP("------------------------------------------------------------\n");
        JITDUMP($"Deriving cloning conditions for L{loop.Index:D2}\n");
        var loopNum = loop.Index;
        var options = context.GetLoopOptInfo(loopNum)!;
        assert(options.Count > 0);
        var hasSpans = false;
        var checkIteration = false;
        foreach (var option in options)
        {
            switch (option)
            {
                case LcJaggedArrayOptInfo:
                case LcMdArrayOptInfo:
                {
                    checkIteration = true;
                    break;
                }
                case LcSpanOptInfo:
                {
                    checkIteration = true;
                    hasSpans = true;
                    break;
                }
                case LcTypeTestOptInfo type:
                {
                    var deref = LC_Ident.CreateIndirOfLocal(type.LclNum, 0, type.MethodTableIndir.Addr.Type);
                    var methodTable = LC_Ident.CreateClassHandle(type.ClsHnd);
                    context.EnsureObjDerefs(loopNum).Add(deref);
                    context.EnsureConditions(loopNum).Add(new LC_Condition(GT_EQ,
                        new LC_Expr(deref), new LC_Expr(methodTable)));
                    break;
                }
                case LcMethodAddrTestOptInfo method:
                {
                    var deref = LC_Ident.CreateIndirOfLocal(method.DelegateLclNum,
                        checked((uint)eeGetEEInfo().offsetOfDelegateFirstTarget), method.DelegateAddressIndir.Addr.Type);
                    LC_Ident address;
                    if (method.IsSlot)
                    {
                        address = LC_Ident.CreateIndirMethodAddrSlot(method.MethAddr
#if DEBUG
                            , method.TargetMethHnd
#endif
                        );
                    }
                    else
                    {
                        address = LC_Ident.CreateMethodAddr(method.MethAddr
#if DEBUG
                            , method.TargetMethHnd
#endif
                        );
                    }
                    context.EnsureObjDerefs(loopNum).Add(deref);
                    context.EnsureConditions(loopNum).Add(new LC_Condition(GT_EQ,
                        new LC_Expr(deref), new LC_Expr(address)));
                    break;
                }
                default:
                {
                    throw new FatalJitException("Unknown loop-cloning candidate.");
                }
            }
        }
        if (!checkIteration)
        {
            JITDUMP("Conditions: ");
#if DEBUG
            if (verbose)
            {
                context.PrintConditions(loopNum);
            }
#endif
            JITDUMP("\n");
            return true;
        }

        var iter = context.GetLoopIterInfo(loopNum)!;
        var test = iter.TestOper();
        if (test is not GT_LT and not GT_LE and not GT_GT and not GT_GE and not GT_NE)
        {
            return false;
        }
        var increasing = iter.IsIncreasingLoop();
        if (!increasing && !iter.IsDecreasingLoop())
        {
            return false;
        }

        var stride = Math.Abs((long)iter.IterConst());
        var largeStride = stride >= int.MaxValue - (CORINFO_Array_MaxLength - 1) + 1;
        var needsOverflowGuard = hasSpans || largeStride;
        ArrIndex? limitArray = null;
        if (iter.HasArrayLengthLimit)
        {
            limitArray = new ArrIndex();
            if (!optArrLenLimit(iter, limitArray))
            {
                JITDUMP("> ArrLen not matching\n");
                return false;
            }
            context.EnsureArrayDerefs(loopNum).Add(new LC_Array(LC_Array.ArrType.Jagged,
                limitArray, LC_Array.OperType.None));
        }

        if (stride > 1 && increasing && needsOverflowGuard)
        {
            var adjustForLE = test is GT_LE ? 1 : 0;
            var maxLimitBase = (long)int.MaxValue - stride + 1 - adjustForLE - iter.LimitOffset;
            if (iter.HasConstLimit)
            {
                assert(iter.LimitOffset == 0);
                if (iter.ConstLimit() > maxLimitBase)
                {
                    JITDUMP($"> Stride {stride}: const limit {iter.ConstLimit()} exceeds overflow bound {maxLimitBase}\n");
                    return false;
                }
            }
            else if (iter.HasInvariantLocalLimit)
            {
                if (maxLimitBase >= int.MaxValue)
                {
                    JITDUMP($"Stride>1 overflow guard trivially holds (offset {iter.LimitOffset})\n");
                }
                else if (maxLimitBase < 0)
                {
                    JITDUMP($"> Stride {stride}, offset {iter.LimitOffset}: overflow guard unsatisfiable\n");
                    return false;
                }
                else
                {
                    var limitLcl = iter.VarLimit();
                    if (lvaGetDesc(limitLcl).Type.ActualType is not TYP_INT)
                    {
                        JITDUMP($"> Stride {stride}: limit var V{limitLcl:D2} not TYP_INT-compatible\n");
                        return false;
                    }
                    context.EnsureConditions(loopNum).Add(new LC_Condition(GT_LE,
                        new LC_Expr(LC_Ident.CreateVar(limitLcl, iter.LimitBase().Type)),
                        new LC_Expr(LC_Ident.CreateConst((uint)maxLimitBase))));
                    JITDUMP($"Added stride>1 overflow guard: V{limitLcl:D2} <= {maxLimitBase}\n");
                }
            }
            else if (iter.HasArrayLengthLimit && largeStride)
            {
                if (maxLimitBase >= CORINFO_Array_MaxLength)
                {
                    JITDUMP($"Stride>1 overflow guard trivially holds for arr.Length (offset {iter.LimitOffset})\n");
                }
                else if (maxLimitBase < 0)
                {
                    JITDUMP($"> Stride {stride}, offset {iter.LimitOffset}: arr.Length overflow guard unsatisfiable\n");
                    return false;
                }
                else
                {
                    assert(limitArray is not null);
                    var arrLen = LC_Ident.CreateArrAccess(new LC_Array(LC_Array.ArrType.Jagged,
                        limitArray, LC_Array.OperType.ArrLen));
                    context.EnsureConditions(loopNum).Add(new LC_Condition(GT_LE,
                        new LC_Expr(arrLen), new LC_Expr(LC_Ident.CreateConst((uint)maxLimitBase))));
                    JITDUMP($"Added stride>1 arr.Length overflow guard: <= {maxLimitBase}\n");
                }
            }
        }

        if (iter.NeedsZeroTripGuard)
        {
            LC_Ident init;
            if (iter.HasConstInit)
            {
                if (iter.ConstInitValue < 0)
                {
                    JITDUMP($"> NeedsZeroTripGuard: init {iter.ConstInitValue} is invalid\n");
                    return false;
                }
                init = LC_Ident.CreateConst((uint)iter.ConstInitValue);
            }
            else
            {
                if (lvaGetDesc(iter.IterVar).Type.ActualType is not TYP_INT)
                {
                    JITDUMP($"> NeedsZeroTripGuard: iter var V{iter.IterVar:D2} not compatible with TYP_INT\n");
                    return false;
                }
                init = LC_Ident.CreateVar(iter.IterVar, iter.Iterator().Type);
            }

            LC_Ident limit;
            if (iter.HasConstLimit)
            {
                if (iter.ConstLimit() < 0)
                {
                    JITDUMP($"> NeedsZeroTripGuard: limit {iter.ConstLimit()} is invalid\n");
                    return false;
                }
                limit = LC_Ident.CreateConst((uint)iter.ConstLimit());
            }
            else if (iter.HasInvariantLocalLimit)
            {
                if (lvaGetDesc(iter.VarLimit()).Type.ActualType is not TYP_INT)
                {
                    JITDUMP($"> NeedsZeroTripGuard: limit var V{iter.VarLimit():D2} not compatible with TYP_INT\n");
                    return false;
                }
                limit = LC_Ident.CreateVar(iter.VarLimit(), iter.LimitBase().Type, iter.LimitOffset);
            }
            else if (iter.HasArrayLengthLimit)
            {
                assert(limitArray is not null);
                limit = LC_Ident.CreateArrAccess(new LC_Array(LC_Array.ArrType.Jagged,
                    limitArray, LC_Array.OperType.ArrLen), iter.LimitOffset);
            }
            else
            {
                JITDUMP("> NeedsZeroTripGuard: undetected limit\n");
                return false;
            }
            var zeroTripOp = test is GT_NE ? (increasing ? GT_LT : GT_GT) : test;
            context.EnsureConditions(loopNum).Add(new LC_Condition(zeroTripOp,
                new LC_Expr(init), new LC_Expr(limit), (iter.TestTree!.Flags & GTF_UNSIGNED) != 0));
            JITDUMP("Added zero-trip guard cloning condition\n");
        }

        LC_Ident bound = default;
        if (iter.HasConstInit)
        {
            if (iter.ConstInitValue < 0)
            {
                JITDUMP($"> Init {iter.ConstInitValue} is invalid\n");
                return false;
            }
            if (!increasing)
            {
                bound = LC_Ident.CreateConst((uint)iter.ConstInitValue);
            }
        }
        else
        {
            if (lvaGetDesc(iter.IterVar).Type.ActualType is not TYP_INT)
            {
                JITDUMP($"> Init var V{iter.IterVar:D2} not compatible with TYP_INT\n");
                return false;
            }
            var init = LC_Ident.CreateVar(iter.IterVar, iter.Iterator().Type);
            if (!increasing)
            {
                bound = init;
            }
            context.EnsureConditions(loopNum).Add(new LC_Condition(GT_GE,
                new LC_Expr(init), new LC_Expr(LC_Ident.CreateConst(0))));
        }

        if (iter.HasConstLimit)
        {
            if (iter.ConstLimit() < 0)
            {
                JITDUMP($"> limit {iter.ConstLimit()} is invalid\n");
                return false;
            }
            if (increasing)
            {
                bound = LC_Ident.CreateConst((uint)iter.ConstLimit());
            }
        }
        else if (iter.HasInvariantLocalLimit)
        {
            var limitLcl = iter.VarLimit();
            if (lvaGetDesc(limitLcl).Type.ActualType is not TYP_INT)
            {
                JITDUMP($"> Limit var V{limitLcl:D2} not compatible with TYP_INT\n");
                return false;
            }
            var limit = LC_Ident.CreateVar(limitLcl, iter.LimitBase().Type, iter.LimitOffset);
            if (increasing)
            {
                bound = limit;
            }
            context.EnsureConditions(loopNum).Add(new LC_Condition(GT_GE,
                new LC_Expr(limit), new LC_Expr(LC_Ident.CreateConst(0))));
        }
        else if (iter.HasArrayLengthLimit)
        {
            assert(limitArray is not null);
            var limit = LC_Ident.CreateArrAccess(new LC_Array(LC_Array.ArrType.Jagged,
                limitArray, LC_Array.OperType.ArrLen), iter.LimitOffset);
            if (increasing)
            {
                bound = limit;
            }
            if (iter.LimitOffset < 0 ||
                iter.LimitOffset > int.MaxValue - CORINFO_Array_MaxLength)
            {
                context.EnsureConditions(loopNum).Add(new LC_Condition(GT_GE,
                    new LC_Expr(limit), new LC_Expr(LC_Ident.CreateConst(0))));
            }
        }
        else
        {
            JITDUMP("> Undetected limit\n");
            return false;
        }

        var limitOp = test switch
        {
            GT_LT => GT_LE,
            GT_LE or GT_GE or GT_GT => GT_LT,
            GT_NE => increasing ? GT_LE : GT_LT,
            _ => throw new FatalJitException("Unrecognized loop-cloning exit comparison."),
        };
        foreach (var option in options)
        {
            switch (option)
            {
                case LcJaggedArrayOptInfo array:
                {
                    var access = new LC_Array(LC_Array.ArrType.Jagged, array.ArrIndex,
                        array.Dim, LC_Array.OperType.ArrLen);
                    context.EnsureConditions(loopNum).Add(new LC_Condition(limitOp,
                        new LC_Expr(bound), new LC_Expr(LC_Ident.CreateArrAccess(access))));
                    context.EnsureArrayDerefs(loopNum).Add(new LC_Array(LC_Array.ArrType.Jagged,
                        array.ArrIndex, array.Dim, LC_Array.OperType.None));
                    break;
                }
                case LcSpanOptInfo span:
                {
                    context.EnsureConditions(loopNum).Add(new LC_Condition(limitOp,
                        new LC_Expr(bound), new LC_Expr(LC_Ident.CreateSpanAccess(new LC_Span(span.SpanIndex)))));
                    break;
                }
                case LcMdArrayOptInfo md:
                {
                    var access = new LC_Array(LC_Array.ArrType.MdArray, md.GetArrIndexForDim(),
                        md.Dim, LC_Array.OperType.None);
                    context.EnsureConditions(loopNum).Add(new LC_Condition(limitOp,
                        new LC_Expr(bound), new LC_Expr(LC_Ident.CreateArrAccess(access))));
                    break;
                }
                case LcTypeTestOptInfo:
                case LcMethodAddrTestOptInfo:
                {
                    break;
                }
                default:
                {
                    throw new FatalJitException("Unknown loop-cloning candidate.");
                }
            }
        }

        if (test is GT_NE)
        {
            var neInit = iter.HasConstInit ? LC_Ident.CreateConst((uint)iter.ConstInitValue) :
                LC_Ident.CreateVar(iter.IterVar, iter.Iterator().Type);
            LC_Ident neLimit;
            if (iter.HasConstLimit)
            {
                neLimit = LC_Ident.CreateConst((uint)iter.ConstLimit());
            }
            else if (iter.HasInvariantLocalLimit)
            {
                neLimit = LC_Ident.CreateVar(iter.VarLimit(), iter.LimitBase().Type, iter.LimitOffset);
            }
            else
            {
                assert(limitArray is not null);
                neLimit = LC_Ident.CreateArrAccess(new LC_Array(LC_Array.ArrType.Jagged,
                    limitArray, LC_Array.OperType.ArrLen), iter.LimitOffset);
            }
            context.EnsureConditions(loopNum).Add(new LC_Condition(increasing ? GT_LE : GT_GE,
                new LC_Expr(neInit), new LC_Expr(neLimit)));
            JITDUMP("Added NE init-vs-limit cloning condition\n");
        }
        JITDUMP("Conditions: ");
#if DEBUG
        if (verbose)
        {
            context.PrintConditions(loopNum);
        }
#endif
        JITDUMP("\n");
        return true;
    }

    private bool optComputeDerefConditions(FlowGraphNaturalLoop loop, LoopCloneContext context)
    {
        var loopNum = loop.Index;
        var arrays = context.EnsureArrayDerefs(loopNum);
        var objects = context.EnsureObjDerefs(loopNum);
        var roots = new List<LC_ArrayDeref>();
        var maxRank = -1;
        foreach (var array in arrays)
        {
            var node = LC_ArrayDeref.Find(roots, array.ArrIndex.ArrLcl);
            if (node is null)
            {
                node = new LC_ArrayDeref(array, 0);
                roots.Add(node);
            }
            var rank = array.GetDimRank();
            for (var i = 0; i < rank; i++)
            {
                var next = node.Find(array.ArrIndex.IndLcls[i]);
                if (next is null)
                {
                    next = new LC_ArrayDeref(array, node.Level + 1);
                    node.EnsureChildren();
                    node.Children!.Add(next);
                }
                node = next;
            }
            maxRank = Math.Max(rank, maxRank);
        }
#if DEBUG
        if (verbose)
        {
            if (roots.Count > 0)
            {
                jitprintf("Array deref condition tree:\n");
                foreach (var root in roots)
                {
                    root.Print();
                    jitprintf("\n");
                }
            }
            else
            {
                jitprintf("No array deref conditions\n");
            }
        }
#endif
        if (arrays.Count > 0)
        {
            assert(maxRank != -1);
            var blocks = maxRank * 2 + 1;
            if (blocks > 3)
            {
                JITDUMP($"> Too many condition blocks ({blocks} > 3)\n");
                return false;
            }
            var levels = context.EnsureBlockConditions(loopNum, blocks);
            foreach (var root in roots)
            {
                root.DeriveLevelConditions(levels);
            }
        }
        if (objects.Count > 0)
        {
            var level = context.EnsureBlockConditions(loopNum, 1)[0];
            foreach (var deref in objects)
            {
                var local = LC_Ident.CreateVar(deref.LclNum, deref.LclType);
                level.Add(new LC_Condition(GT_NE, new LC_Expr(local),
                    new LC_Expr(LC_Ident.CreateNull(deref.LclType))));
            }
        }
        return true;
    }
}
