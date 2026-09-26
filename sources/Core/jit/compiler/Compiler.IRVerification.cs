// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
using System.Numerics;
using static RyuJitSharp.GenTreeDebugFlags;

namespace RyuJitSharp;

public partial class Compiler
{
    public void fgDebugCheckLinks()
    {
        if ((fgBBcount > 10000) && (expensiveDebugCheckLevel < 1))
        {
            return;
        }

        fgDebugCheckBlockLinks();

        foreach (var block in Blocks)
        {
            if (block.IsLIR)
            {
                _ = block.CheckLir(this, (activePhaseChecks & PhaseChecks.CHECK_LIR_UNUSED_VALUES) != 0);
            }
            else
            {
                fgDebugCheckStmtsList(block);
            }
        }

        fgDebugCheckSsa();
    }

    public void fgDebugCheckStmtsList(BasicBlock block)
    {
        foreach (var stmt in block.Statements)
        {
            noway_assert(stmt.PrevStmt is not null);

            if (stmt == block.FirstStmt)
            {
                noway_assert(stmt.PrevStmt.NextStmt is null);
            }
            else
            {
                noway_assert(stmt.PrevStmt.NextStmt == stmt);
            }

            if (stmt.NextStmt is not null)
            {
                noway_assert(stmt.NextStmt.PrevStmt == stmt);
            }
            else
            {
                noway_assert(block.LastStmt == stmt);
            }

            var tree = stmt.RootNode;
            noway_assert(tree is not null);
            fgDebugCheckFlagsAndTypes(tree, block);

            if (block.Kind is not BBJ_RETURN)
            {
                assert(tree.Oper is not GT_RETURN, "GT_RETURN node found in a block that isn't BBJ_RETURN");
            }
            else
            {
                assert((tree.Oper is not GT_RETURN) || (stmt.NextStmt is null),
                    "GT_RETURN node found that is not the last statement in the block");
            }

            if (fgNodeThreading is NodeThreading.AllTrees or NodeThreading.LIR)
            {
                fgDebugCheckNodeLinks(block, stmt);
            }
        }
    }

    public void fgDebugCheckFlagsAndTypes(GenTree tree, BasicBlock block)
    {
        fgDebugCheckType(tree);

        var actualFlags = tree.Flags & GTF_ALL_EFFECT;
        var expectedFlags = GTF_EMPTY;

        if (tree.MayThrow(this))
        {
            expectedFlags |= GTF_EXCEPT;
        }

        if (tree.RequiresAsgFlag)
        {
            expectedFlags |= GTF_ASG;
        }

        if (tree.RequiresCallFlag(this))
        {
            expectedFlags |= GTF_CALL;
        }

        if ((tree.Flags & GTF_REVERSE_OPS) != 0)
        {
            assert(fgOperSupportsReverseOpEvalOrder(tree));
        }

        var op1 = tree.Oper.IsSimple ? tree.AsUnOp().Op1 : null;

        switch (tree.Oper)
        {
            case GT_STORE_LCL_VAR:
            case GT_STORE_LCL_FLD:
            {
                assert((tree.Flags & GTF_VAR_DEF) != 0);

                if (!fgImplicitByRefLclFldsStale || (tree.Oper is not GT_STORE_LCL_FLD) ||
                    (lvaGetDesc(tree.AsLclFld().LclNum).Type is not TYP_BYREF) ||
                    !lvaIsImplicitByRefLocal(tree.AsLclFld().LclNum))
                {
                    assert(((tree.Flags & GTF_VAR_USEASG) != 0) ==
                        ((tree.Oper is GT_STORE_LCL_FLD) && tree.AsLclFld().IsPartial(this)));
                }
                break;
            }

            case GT_CATCH_ARG:
            case GT_ASYNC_CONTINUATION:
            {
                expectedFlags |= GTF_ORDER_SIDEEFF;
                break;
            }

            case GT_MEMORYBARRIER:
            {
                expectedFlags |= GTF_GLOB_REF | GTF_ASG;
                break;
            }

            case GT_QMARK:
            {
                if (op1 is not GenTree condition)
                {
                    NO_WAY("GT_QMARK has no condition");
                    break;
                }

                assert(((activePhaseChecks & PhaseChecks.CHECK_IR_RELAXED) != 0) || !condition.CanCse);
                assert(condition.Oper.IsCompare || condition.IsIntegralConst(0) || condition.IsIntegralConst(1));
                break;
            }

            case GT_RET_EXPR:
            {
                expectedFlags |= GTF_CALL;
                break;
            }

            case GT_IND:
            {
                assert(((tree.Flags & GTF_IND_TGT_NOT_HEAP) == 0) || ((tree.Flags & GTF_IND_TGT_HEAP) == 0));
                break;
            }

            case GT_CALL:
            {
                var call = tree.AsCall();

                if (!fgGlobalMorphDone && call.CanTailCall && gtIsRecursiveCall(call))
                {
                    assert((optMethodFlags & OMF_HAS_RECURSIVE_TAILCALL) != 0);
                    assert(block.HasFlag(BBF_RECURSIVE_TAILCALL));
                }
                break;
            }

            case GT_CMPXCHG:
            {
                expectedFlags |= GTF_GLOB_REF | GTF_ASG;
                break;
            }

#if FEATURE_HW_INTRINSICS
            case GT_HWINTRINSIC:
            {
                var intrinsic = tree.AsHWIntrinsic();
                var intrinsicId = intrinsic.HWIntrinsicId;

                if (intrinsic.IsMemoryLoad())
                {
                    assert(tree.MayThrow(this));
                    expectedFlags |= GTF_GLOB_REF;
                }
                else if (intrinsic.IsMemoryStore(out _))
                {
                    assert(tree.RequiresAsgFlag);
                    assert(tree.MayThrow(this));
                    expectedFlags |= GTF_GLOB_REF;
                }
                else if (HWIntrinsicInfo.HasSpecialSideEffect(intrinsicId))
                {
                    switch (intrinsicId)
                    {
#if TARGET_XARCH
                        case NI_X86Base_LoadFence:
                        case NI_X86Base_MemoryFence:
                        case NI_X86Base_StoreFence:
                        case NI_X86Serialize_Serialize:
                        {
                            assert(tree.RequiresAsgFlag);
                            expectedFlags |= GTF_GLOB_REF;
                            break;
                        }

                        case NI_X86Base_Pause:
                        case NI_X86Base_Prefetch0:
                        case NI_X86Base_Prefetch1:
                        case NI_X86Base_Prefetch2:
                        case NI_X86Base_PrefetchNonTemporal:
                        {
                            assert(tree.RequiresCallFlag(this));
                            expectedFlags |= GTF_GLOB_REF;
                            break;
                        }

                        case NI_Vector_op_Division:
                        {
                            assert(intrinsic.SimdSize is 16 or 32);
                            break;
                        }
#endif

#if TARGET_ARM64
                        case NI_ArmBase_Yield:
                        case NI_Sve_GatherPrefetch16Bit:
                        case NI_Sve_GatherPrefetch32Bit:
                        case NI_Sve_GatherPrefetch64Bit:
                        case NI_Sve_GatherPrefetch8Bit:
                        case NI_Sve_Prefetch16Bit:
                        case NI_Sve_Prefetch32Bit:
                        case NI_Sve_Prefetch64Bit:
                        case NI_Sve_Prefetch8Bit:
                        case NI_Sve_GetFfrByte:
                        case NI_Sve_GetFfrDouble:
                        case NI_Sve_GetFfrInt16:
                        case NI_Sve_GetFfrInt32:
                        case NI_Sve_GetFfrInt64:
                        case NI_Sve_GetFfrSByte:
                        case NI_Sve_GetFfrSingle:
                        case NI_Sve_GetFfrUInt16:
                        case NI_Sve_GetFfrUInt32:
                        case NI_Sve_GetFfrUInt64:
                        case NI_Sve_SetFfr:
                        {
                            assert(tree.RequiresCallFlag(this));
                            expectedFlags |= GTF_GLOB_REF;
                            break;
                        }
#endif
                        default:
                        {
                            assert(false, "Unhandled HWIntrinsic with special side effect");
                            break;
                        }
                    }
                }
                break;
            }
#endif
            default:
            {
                break;
            }
        }

        _ = tree.VisitOperands(operand => {
            fgDebugCheckFlagsAndTypes(operand, block);
            expectedFlags |= operand.Flags & GTF_ALL_EFFECT;
            return GenTree.VisitResult.Continue;
        });

        fgDebugCheckFlagsHelper(tree, actualFlags, expectedFlags);
    }

    public void fgDebugCheckType(GenTree node)
    {
        if (node.Type is TYP_ULONG or TYP_UINT)
        {
            gtDispTree(node);
            assert(false, "TYP_ULONG and TYP_UINT are not legal in IR");
        }

        switch (node.Oper)
        {
            case GT_NOP:
            case GT_JTRUE:
            case GT_BOUNDS_CHECK:
            {
                if (node.Type is not TYP_VOID)
                {
                    gtDispTree(node);
                    assert(false, "The tree is expected to be of TYP_VOID type");
                }
                break;
            }
        }

        if (varTypeIsSmall(node.Type))
        {
            if (node.Oper is GT_COMMA)
            {
                return;
            }

            if (node.Oper.IsIndir || (node.Oper is GT_PHI or GT_PHI_ARG) ||
                node.IsPhiDefn || node.Oper.IsAnyLocal)
            {
                return;
            }

            gtDispTree(node);
            assert(false, "Unexpected small type in IR");
        }
    }

    private bool fgOperSupportsReverseOpEvalOrder(GenTree tree)
    {
#if TARGET_WASM
        return false;
#else
        if (tree.Oper.IsBinary)
        {
            var op = tree.AsOp();

            if ((op.Op1 is null) || (op.Op2 is null) || (tree.Oper is GT_COMMA or GT_BOUNDS_CHECK))
            {
                return false;
            }

            return (tree.Oper is not GT_INTRINSIC) ||
                !IsIntrinsicImplementedByUserCall(tree.AsIntrinsic().IntrinsicName);
        }

#if FEATURE_SIMD || FEATURE_HW_INTRINSICS
        if (tree.Oper.IsMultiOp)
        {
            var multiOp = tree.AsMultiOp();
            return (multiOp.Operands.Length == 2) && !multiOp.IsUserCall;
        }
#endif
        return false;
#endif
    }

    private static void fgDebugCheckDispFlags(GenTree tree, GenTreeFlags dispFlags)
    {
        if (tree.Oper is GT_IND)
        {
            jitprintf($"{(((dispFlags & GTF_IND_INVARIANT) != 0) ? '#' : '-')}");
            jitprintf($"{(((dispFlags & GTF_IND_NONFAULTING) != 0) ? 'n' : '-')}");
            jitprintf($"{(((dispFlags & GTF_IND_NONNULL) != 0) ? '@' : '-')}");
        }

        _ = GenTree.gtDispFlags(dispFlags, GTF_DEBUG_NONE);
    }

    public void fgDebugCheckFlagsHelper(GenTree tree, GenTreeFlags actualFlags, GenTreeFlags expectedFlags)
    {
        if ((expectedFlags & ~actualFlags) != 0)
        {
            PrintFlags("Missing", expectedFlags & ~actualFlags);
            noway_assert(false, "Missing flags on tree");
            PrintFlags("Missing", expectedFlags & ~actualFlags);
        }
        else if ((actualFlags & ~expectedFlags) != 0)
        {
            var flagsToCheck = ~GTF_GLOB_REF;

            if (tree.SupportsOrderingSideEffect())
            {
                flagsToCheck &= ~GTF_ORDER_SIDEEFF;
            }

            if ((tree.Oper is GT_IND or GT_STOREIND) &&
                tree.AsIndir().Addr.IsIconHandle() &&
                tree.AsIndir().Addr.AsIntCon().IsIconHandle(GTF_ICON_FTN_ADDR))
            {
                flagsToCheck &= ~GTF_IND_INVARIANT;
            }

            var extraFlags = actualFlags & ~expectedFlags & flagsToCheck;

            if (extraFlags != 0)
            {
                var isRelaxed = (activePhaseChecks & PhaseChecks.CHECK_IR_RELAXED) != 0;

                if (!isRelaxed || verbose)
                {
                    PrintFlags("Extra", extraFlags);
                }

                if (isRelaxed)
                {
                    Metrics.IRExtraFlags += BitOperations.PopCount((uint)extraFlags);
                    return;
                }

                noway_assert(false, "Extra flags on tree");
                PrintFlags("Extra", extraFlags);
            }
        }

        void PrintFlags(string kind, GenTreeFlags flags)
        {
            jitprintf($"{kind} flags on tree [{tree.TreeId:D6}]: ");
            fgDebugCheckDispFlags(tree, flags);
            jitprintf("\n");
            gtDispTree(tree);
        }
    }
}
#endif
