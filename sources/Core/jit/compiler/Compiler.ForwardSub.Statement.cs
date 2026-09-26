// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, forwardsub.cpp.

using System.Numerics;
using System.Runtime.CompilerServices;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public partial class Compiler
{
    private bool fgForwardSubStatement(Statement stmt)
    {
        var defNode = stmt.RootNode;
        if (defNode.Oper is not GT_STORE_LCL_VAR)
        {
            return false;
        }

#if DEBUG
        JITDUMP($"    [{defNode.TreeId:D6}]: ");
#endif
        var number = defNode.AsLclVarCommon().LclNum;
        ref var descriptor = ref lvaGetDesc(number);
        if (descriptor.lvPinned)
        {
            JITDUMP(" pinned local\n");
            return false;
        }
        if (descriptor.IsAddressExposed)
        {
            JITDUMP(" not store (unaliased single-use lcl)\n");
            return false;
        }
        if (lvaIsImplicitByRefLocal(number))
        {
            JITDUMP(" implicit by-ref local\n");
            return false;
        }

        var fwdSubNode = defNode.AsLclVarCommon().Data;
        if (fwdSubNode.Oper is GT_CATCH_ARG or GT_LCLHEAP or GT_ASYNC_CONTINUATION)
        {
            JITDUMP($" tree to sub is {fwdSubNode.Oper.Name}\n");
            return false;
        }
        if (gtTreeContainsAsyncCall(fwdSubNode))
        {
            JITDUMP(" tree has an async call\n");
            return false;
        }
        if ((fwdSubNode.Flags & GTF_ASG) != 0)
        {
            JITDUMP(" tree to sub has effects\n");
            return false;
        }
        if (defNode.Type.ActualType != fwdSubNode.Type.ActualType)
        {
            JITDUMP(" mismatched types (store)\n");
            return false;
        }

        var nextStmt = stmt.NextStmt
            ?? throw new FatalJitException("A forward-substitution candidate needs a following statement.");
        var isCheapAddressTree = fgIsCheapReorderableAddressTree(fwdSubNode);
        var visitor = new ForwardSubVisitor(this, number);
        var found = false;
        var multiUse = false;
        foreach (var local in nextStmt.LocalsTreeList)
        {
            if ((local.Oper is GT_LCL_VAR) && (local.LclNum == number))
            {
                if (visitor.IsLastUse(local.AsLclVar()))
                {
                    found = true;
                    break;
                }
                if (isCheapAddressTree)
                {
                    multiUse = true;
                    continue;
                }
            }

            if (visitor.IsUse(local))
            {
                JITDUMP(" next stmt has non-last use\n");
                return false;
            }
        }
        if (!found)
        {
            JITDUMP(" no next stmt use\n");
            return false;
        }

        const uint nodeLimit = 16;
        if (gtComplexityExceeds(fwdSubNode, nodeLimit, static _ => 1u))
        {
            JITDUMP($" tree to sub has more than {nodeLimit} nodes\n");
            return false;
        }

        gtUpdateStmtSideEffects(nextStmt);
        gtUpdateStmtSideEffects(stmt);
        _ = visitor.WalkTree(ref nextStmt.RootNodeRef, null);
        if (visitor.Node is not GenTree useNode)
        {
            JITDUMP(" no next stmt use\n");
            return false;
        }

#if DEBUG
        JITDUMP($" [{useNode.TreeId:D6}] is last use of [{defNode.TreeId:D6}] (V{number:D2}) ");
#endif
        var nextRoot = nextStmt.RootNode;
        if (fwdSubNode.Oper is GT_QMARK)
        {
            if ((visitor.ParentNode != nextRoot) || (nextRoot.Oper is not GT_STORE_LCL_VAR))
            {
                JITDUMP(" can't fwd sub qmark as use is not top level STORE_LCL_VAR\n");
                return false;
            }
            if (descriptor.lvNormalizeOnStore)
            {
                JITDUMP($" can't fwd sub qmark as V{number:D2} is normalize on store\n");
                return false;
            }

            var destination = nextRoot.AsLclVarCommon().LclNum;
            if (lvaGetDesc(destination).lvNormalizeOnStore)
            {
                JITDUMP($" can't fwd sub qmark as V{destination:D2} is normalize on store\n");
                return false;
            }
        }

        const uint nextTreeLimit = 200;
        if ((visitor.Complexity > nextTreeLimit) &&
            gtComplexityExceeds(fwdSubNode, 1, static _ => 1u))
        {
            JITDUMP($" next stmt tree is too large ({visitor.Complexity})\n");
            return false;
        }
        if (useNode.Type.ActualType != fwdSubNode.Type.ActualType)
        {
            JITDUMP(" mismatched types (substitution)\n");
            return false;
        }

        var precedingEffects = visitor.Flags;
        if (((precedingEffects & GTF_ASG) != 0) &&
            fgForwardSubHasStoreInterference(stmt, nextStmt, useNode))
        {
            JITDUMP(" cannot reorder with potential interfering store\n");
            return false;
        }
        if (((fwdSubNode.Flags & GTF_CALL) != 0) &&
            ((precedingEffects & GTF_ALL_EFFECT) != 0))
        {
            JITDUMP(" cannot reorder call with any side effect\n");
            return false;
        }
        if (((fwdSubNode.Flags & GTF_GLOB_REF) != 0) &&
            ((precedingEffects & GTF_PERSISTENT_SIDE_EFFECTS) != 0))
        {
            JITDUMP(" cannot reorder global reference with persistent side effects\n");
            return false;
        }
        if (((fwdSubNode.Flags & GTF_ORDER_SIDEEFF) != 0) &&
            ((precedingEffects & (GTF_GLOB_REF | GTF_ORDER_SIDEEFF)) != 0))
        {
            JITDUMP(" cannot reorder ordering side effect with global reference/ordering side effect\n");
            return false;
        }
        if ((fwdSubNode.Flags & GTF_EXCEPT) != 0)
        {
            if ((precedingEffects & GTF_PERSISTENT_SIDE_EFFECTS) != 0)
            {
                JITDUMP(" cannot reorder exception with persistent side effect\n");
                return false;
            }
            if ((precedingEffects & GTF_EXCEPT) != 0)
            {
                assert(visitor.Exceptions is not ExceptionSetFlags.None);
                if ((BitOperations.PopCount((uint)visitor.Exceptions) > 1) ||
                    ((visitor.Exceptions & ExceptionSetFlags.UnknownException) != 0))
                {
                    JITDUMP(" cannot reorder different/unknown thrown exceptions\n");
                    return false;
                }
                var sourceExceptions = gtCollectExceptions(fwdSubNode);
                assert(sourceExceptions is not ExceptionSetFlags.None);
                if (sourceExceptions != visitor.Exceptions)
                {
                    JITDUMP(" cannot reorder different thrown exceptions\n");
                    return false;
                }
            }
        }

        if ((precedingEffects & GTF_PERSISTENT_SIDE_EFFECTS) != 0)
        {
            var effects = new ForwardSubEffectsVisitor(this);
            _ = effects.WalkTree(ref fwdSubNode, null);
            if ((effects.Flags & GTF_GLOB_REF) != 0)
            {
                JITDUMP(" potentially interacting effects (AX locals)\n");
                return false;
            }
        }

        if (fwdSubNode.Oper is GT_LCL_VAR)
        {
            var sourceNumber = fwdSubNode.AsLclVarCommon().LclNum;
            ref var sourceDesc = ref lvaGetDesc(sourceNumber);
            if (!varTypeIsStruct(sourceDesc.Type) && sourceDesc.IsAddressExposed)
            {
                JITDUMP($" V{sourceNumber:D2} is address exposed\n");
                return false;
            }
        }
        if (visitor.IsCallArg && (useNode.Type is TYP_STRUCT) &&
            (fwdSubNode.Oper is not GT_BLK and not GT_LCL_VAR and not GT_LCL_FLD))
        {
            JITDUMP(" use is a struct arg; fwd sub node is not BLK/LCL_VAR/LCL_FLD\n");
            return false;
        }
        if (fwdSubNode.Oper.IsCall && descriptor.CanBeReplacedWithItsField(this))
        {
            JITDUMP(" fwd sub local is 'CanBeReplacedWithItsField'\n");
            return false;
        }
        if (varTypeIsStruct(fwdSubNode.Type) && fwdSubNode.IsMultiRegNode)
        {
            if ((visitor.ParentNode is null) || (visitor.ParentNode.Oper is not GT_STORE_LCL_VAR))
            {
                JITDUMP(" multi-reg struct node, parent not STORE_LCL_VAR\n");
                return false;
            }
            var destination = visitor.ParentNode.AsLclVarCommon().LclNum;
            JITDUMP($" [marking V{destination:D2} as multi-reg-dest]");
            lvaGetDesc(destination).IsMultiRegDest = true;
        }
        if ((fwdSubNode.Oper is GT_LCL_VAR) && varTypeIsSimd(fwdSubNode.Type))
        {
            ref var sourceDesc = ref lvaGetDesc(fwdSubNode.AsLclVarCommon().LclNum);
            if (sourceDesc.lvPromoted && !sourceDesc.lvDoNotEnregister)
            {
                JITDUMP(" promoted SIMD lcl var\n");
                return false;
            }
        }

        if (compMethodReturnsMultiRegRetType &&
            visitor.ParentNode?.Oper is GT_RETURN or GT_SWIFT_ERROR_RET)
        {
#if TARGET_X86
            if (fwdSubNode.Type is TYP_LONG)
            {
                JITDUMP(" TYP_LONG fwd sub node, target is x86\n");
                return false;
            }
#endif
            if (fwdSubNode.Oper is not GT_LCL_VAR)
            {
#if TARGET_ARM
                if (fwdSubNode.Type is not TYP_LONG)
#endif
                {
                    JITDUMP(" parent is multi-reg struct return, fwd sub node is not lcl var\n");
                    return false;
                }
            }
            else if (varTypeIsStruct(fwdSubNode.Type))
            {
                var source = fwdSubNode.AsLclVar();
                var sourceNumber = source.LclNum;
                if (lvaIsImplicitByRefLocal(sourceNumber))
                {
                    JITDUMP(" parent is multi-reg return; fwd sub node is implicit byref\n");
                    return false;
                }
                JITDUMP($" [marking V{sourceNumber:D2} as multi-reg-ret]");
                lvaGetDesc(sourceNumber).lvIsMultiRegRet = true;
                source.Flags |= GTF_DONT_CSE;
            }
        }

        if (varTypeIsSmall(descriptor.Type) && fgCastNeeded(fwdSubNode, descriptor.Type))
        {
            JITDUMP(" [adding cast for small-typed local]");
            fwdSubNode = gtNewCastNode(TYP_INT, fwdSubNode, false, descriptor.Type);
        }

        if (multiUse)
        {
            if (!fgForwardSubMultiUse(nextStmt, number, fwdSubNode))
            {
                JITDUMP(" multi-use sub failed (count out of range or indirect-call context)\n");
                return false;
            }
#if DEBUG
            JITDUMP($" -- multi-use fwd subbing [{fwdSubNode.TreeId:D6}]; new next stmt is\n");
#endif
            DISPSTMT(nextStmt);
            return true;
        }

        var link = gtFindLink(nextStmt, useNode);
        if (Unsafe.IsNullRef(ref link.result))
        {
            throw new FatalJitException("The last forward-substitution use lost its owner.");
        }
        var useLocal = link.result.AsLclVarCommon();
        link.result = fwdSubNode;

        assert(defNode.Next is null);
        var first = stmt.TreeListBegin?.AsLclVarCommon()
            ?? throw new FatalJitException("A forwarded store must have a locals list.");
        fgForwardSubSpliceLocals(nextStmt, useLocal,
            first == defNode ? null : first,
            first == defNode ? null : defNode.Prev?.AsLclVarCommon());

        if ((fwdSubNode.Flags & GTF_ALL_EFFECT) != 0)
        {
            gtUpdateStmtSideEffects(nextStmt);
        }
#if DEBUG
        JITDUMP($" -- fwd subbing [{fwdSubNode.TreeId:D6}]; new next stmt is\n");
#endif
        DISPSTMT(nextStmt);
        return true;
    }
}
