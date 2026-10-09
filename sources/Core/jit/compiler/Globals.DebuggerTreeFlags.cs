// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static partial class Globals
{
#if DEBUG
    private static uint s_cTreeFlagsSequence;

    public static void cTreeFlags(Compiler comp, GenTree tree)
    {
        jitprintf($"===================================================================== *TreeFlags {unchecked(s_cTreeFlagsSequence++)}\n");

        if (tree.Flags is not 0)
        {
            jitprintf("flags=");

            // Node flags
            if ((tree._debugFlags & GTF_DEBUG_NODE_LARGE) is not 0)
            {
                jitprintf("[NODE_LARGE]");
            }
            if ((tree._debugFlags & GTF_DEBUG_NODE_SMALL) is not 0)
            {
                jitprintf("[NODE_SMALL]");
            }
            if ((tree.Flags & GTF_COLON_COND) is not 0)
            {
                jitprintf("[COLON_COND]");
            }

            // Operator flags
            var op = tree.Oper;
            switch (op)
            {
                case GT_LCL_VAR:
                case GT_LCL_FLD:
                case GT_LCL_ADDR:
                case GT_STORE_LCL_FLD:
                case GT_STORE_LCL_VAR:
                {
                    if ((tree.Flags & GTF_VAR_DEF) is not 0)
                    {
                        jitprintf("[VAR_DEF]");
                    }
                    if ((tree.Flags & GTF_VAR_USEASG) is not 0)
                    {
                        jitprintf("[VAR_USEASG]");
                    }
                    if ((tree.Flags & GTF_VAR_MOREUSES) is not 0)
                    {
                        jitprintf("[VAR_MOREUSES]");
                    }
                    if (!comp.lvaGetDesc(tree.AsLclVarCommon().LclNum).lvPromoted)
                    {
                        if ((tree.Flags & GTF_VAR_DEATH) is not 0)
                        {
                            jitprintf("[VAR_DEATH]");
                        }
                    }
                    else
                    {
                        if ((tree.Flags & GTF_VAR_FIELD_DEATH0) is not 0)
                        {
                            jitprintf("[VAR_FIELD_DEATH0]");
                        }
                    }
                    if ((tree.Flags & GTF_VAR_FIELD_DEATH1) is not 0)
                    {
                        jitprintf("[VAR_FIELD_DEATH1]");
                    }
                    if ((tree.Flags & GTF_VAR_FIELD_DEATH2) is not 0)
                    {
                        jitprintf("[VAR_FIELD_DEATH2]");
                    }
                    if ((tree.Flags & GTF_VAR_FIELD_DEATH3) is not 0)
                    {
                        jitprintf("[VAR_FIELD_DEATH3]");
                    }
                    if ((tree.Flags & GTF_VAR_EXPLICIT_INIT) is not 0)
                    {
                        jitprintf("[VAR_EXPLICIT_INIT]");
                    }
                    break;
                }
                case GT_NO_OP:
                {
                    break;
                }
                case GT_INDEX_ADDR:
                {
                    if ((tree.Flags & GTF_INX_RNGCHK) is not 0)
                    {
                        jitprintf("[INX_RNGCHK]");
                    }
                    break;
                }
                case GT_IND:
                case GT_STOREIND:
                {
                    if ((tree.Flags & GTF_IND_VOLATILE) is not 0)
                    {
                        jitprintf("[IND_VOLATILE]");
                    }
                    if ((tree.Flags & GTF_IND_TGT_NOT_HEAP) is not 0)
                    {
                        jitprintf("[IND_TGT_NOT_HEAP]");
                    }
                    if ((tree.Flags & GTF_IND_TGT_HEAP) is not 0)
                    {
                        jitprintf("[IND_TGT_HEAP]");
                    }
                    if ((tree.Flags & GTF_IND_REQ_ADDR_IN_REG) is not 0)
                    {
                        jitprintf("[IND_REQ_ADDR_IN_REG]");
                    }
                    if ((tree.Flags & GTF_IND_UNALIGNED) is not 0)
                    {
                        jitprintf("[IND_UNALIGNED]");
                    }
                    if ((tree.Flags & GTF_IND_INVARIANT) is not 0)
                    {
                        jitprintf("[IND_INVARIANT]");
                    }
                    if ((tree.Flags & GTF_IND_NONNULL) is not 0)
                    {
                        jitprintf("[IND_NONNULL]");
                    }
                    if ((tree.Flags & GTF_IND_INITCLASS) is not 0)
                    {
                        jitprintf("[IND_INITCLASS]");
                    }
                    break;
                }
                case GT_MUL:
#if !TARGET_64BIT
                case GT_MUL_LONG:
#endif
                {
                    if ((tree.Flags & GTF_MUL_64RSLT) is not 0)
                    {
                        jitprintf("[64RSLT]");
                    }
                    if ((tree.Flags & GTF_ADDRMODE_NO_CSE) is not 0)
                    {
                        jitprintf("[ADDRMODE_NO_CSE]");
                    }
                    break;
                }
                case GT_ADD:
                {
                    if ((tree.Flags & GTF_ADDRMODE_NO_CSE) is not 0)
                    {
                        jitprintf("[ADDRMODE_NO_CSE]");
                    }
                    break;
                }
                case GT_LSH:
                {
                    if ((tree.Flags & GTF_ADDRMODE_NO_CSE) is not 0)
                    {
                        jitprintf("[ADDRMODE_NO_CSE]");
                    }
                    break;
                }
                case GT_MOD:
                case GT_UMOD:
                {
                    break;
                }
                case GT_EQ:
                case GT_NE:
                case GT_LT:
                case GT_LE:
                case GT_GT:
                case GT_GE:
                {
                    if ((tree.Flags & GTF_RELOP_NAN_UN) is not 0)
                    {
                        jitprintf("[RELOP_NAN_UN]");
                    }
                    if ((tree.Flags & GTF_RELOP_JMP_USED) is not 0)
                    {
                        jitprintf("[RELOP_JMP_USED]");
                    }
                    break;
                }
                case GT_BOX:
                {
                    if ((tree.Flags & GTF_BOX_CLONED) is not 0)
                    {
                        jitprintf("[BOX_CLONED]");
                    }
                    break;
                }
                case GT_ARR_ADDR:
                {
                    if ((tree.Flags & GTF_ARR_ADDR_NONNULL) is not 0)
                    {
                        jitprintf("[ARR_ADDR_NONNULL]");
                    }
                    break;
                }
                case GT_CNS_INT:
                {
                    var handleKind = tree.Flags & GTF_ICON_HDL_MASK;
                    if (handleKind is not 0)
                    {
                        jitprintf($"[{GenTree.gtGetHandleKindString(handleKind)}]");
                    }
                    break;
                }
                case GT_BLK:
                case GT_STORE_BLK:
                {
                    if ((tree.Flags & GTF_IND_VOLATILE) is not 0)
                    {
                        jitprintf("[IND_VOLATILE]");
                    }
                    if ((tree.Flags & GTF_IND_UNALIGNED) is not 0)
                    {
                        jitprintf("[IND_UNALIGNED]");
                    }
                    break;
                }
                case GT_CALL:
                {
                    if ((tree.Flags & GTF_CALL_UNMANAGED) is not 0)
                    {
                        jitprintf("[CALL_UNMANAGED]");
                    }
                    if ((tree.Flags & GTF_CALL_INLINE_CANDIDATE) is not 0)
                    {
                        jitprintf("[CALL_INLINE_CANDIDATE]");
                    }
                    if (!tree.AsCall().IsVirtual)
                    {
                        jitprintf("[CALL_NONVIRT]");
                    }
                    if (tree.AsCall().IsVirtualVtable)
                    {
                        jitprintf("[CALL_VIRT_VTABLE]");
                    }
                    if (tree.AsCall().IsVirtualStub)
                    {
                        jitprintf("[CALL_VIRT_STUB]");
                    }
                    if ((tree.Flags & GTF_CALL_NULLCHECK) is not 0)
                    {
                        jitprintf("[CALL_NULLCHECK]");
                    }
                    if ((tree.Flags & GTF_CALL_POP_ARGS) is not 0)
                    {
                        jitprintf("[CALL_POP_ARGS]");
                    }
                    if ((tree.Flags & GTF_CALL_HOISTABLE) is not 0)
                    {
                        jitprintf("[CALL_HOISTABLE]");
                    }

                    // More flags associated with calls.
                    var call = tree.AsCall();
                    if ((call._callMoreFlags & GTF_CALL_M_EXPLICIT_TAILCALL) is not 0)
                    {
                        jitprintf("[CALL_M_EXPLICIT_TAILCALL]");
                    }
                    if ((call._callMoreFlags & GTF_CALL_M_TAILCALL) is not 0)
                    {
                        jitprintf("[CALL_M_TAILCALL]");
                    }
                    if ((call._callMoreFlags & GTF_CALL_M_RETBUFFARG) is not 0)
                    {
                        jitprintf("[CALL_M_RETBUFFARG]");
                    }
                    if ((call._callMoreFlags & GTF_CALL_M_DELEGATE_INV) is not 0)
                    {
                        jitprintf("[CALL_M_DELEGATE_INV]");
                    }
                    if ((call._callMoreFlags & GTF_CALL_M_NOGCCHECK) is not 0)
                    {
                        jitprintf("[CALL_M_NOGCCHECK]");
                    }
                    if (call.IsSpecialIntrinsic())
                    {
                        jitprintf("[CALL_M_SPECIAL_INTRINSIC]");
                    }
                    if (call.IsVirtualStub)
                    {
                        if ((call._callMoreFlags & GTF_CALL_M_VIRTSTUB_REL_INDIRECT) is not 0)
                        {
                            jitprintf("[CALL_M_VIRTSTUB_REL_INDIRECT]");
                        }
                    }
                    else if (!call.IsVirtual)
                    {
                        if ((call._callMoreFlags & GTF_CALL_M_NONVIRT_SAME_THIS) is not 0)
                        {
                            jitprintf("[CALL_M_NONVIRT_SAME_THIS]");
                        }
                    }
                    if ((call._callMoreFlags & GTF_CALL_M_FRAME_VAR_DEATH) is not 0)
                    {
                        jitprintf("[CALL_M_FRAME_VAR_DEATH]");
                    }
                    if ((call._callMoreFlags & GTF_CALL_M_TAILCALL_VIA_JIT_HELPER) is not 0)
                    {
                        jitprintf("[CALL_M_TAILCALL_VIA_JIT_HELPER]");
                    }
#if FEATURE_TAILCALL_OPT
                    if ((call._callMoreFlags & GTF_CALL_M_IMPLICIT_TAILCALL) is not 0)
                    {
                        jitprintf("[CALL_M_IMPLICIT_TAILCALL]");
                    }
#endif
                    if ((call._callMoreFlags & GTF_CALL_M_PINVOKE) is not 0)
                    {
                        jitprintf("[CALL_M_PINVOKE]");
                    }
                    if (call.IsFatPointerCandidate)
                    {
                        jitprintf("[CALL_FAT_POINTER_CANDIDATE]");
                    }
                    if (call.IsGuarded)
                    {
                        jitprintf("[CALL_GUARDED]");
                    }
                    if ((call._callDebugFlags & GTF_CALL_MD_STRESS_TAILCALL) is not 0)
                    {
                        jitprintf("[CALL_MD_STRESS_TAILCALL]");
                    }
                    if ((call._callDebugFlags & GTF_CALL_MD_DEVIRTUALIZED) is not 0)
                    {
                        jitprintf("[CALL_MD_DEVIRTUALIZED]");
                    }
                    if ((call._callDebugFlags & GTF_CALL_MD_GUARDED) is not 0)
                    {
                        jitprintf("[CALL_MD_GUARDED]");
                    }
                    if ((call._callDebugFlags & GTF_CALL_MD_UNBOXED) is not 0)
                    {
                        jitprintf("[CALL_MD_UNBOXED]");
                    }
                    break;
                }
                default:
                {
                    var flags = tree.Flags & ~(GTF_COMMON_MASK | GTF_OVERFLOW);
                    if (flags is not 0)
                    {
                        jitprintf($"[{unchecked((uint)flags):X8}]");
                    }
                    break;
                }
            }

            // Common flags
            if ((tree.Flags & GTF_ASG) is not 0)
            {
                jitprintf("[ASG]");
            }
            if ((tree.Flags & GTF_CALL) is not 0)
            {
                jitprintf("[CALL]");
            }
            switch (op)
            {
                case GT_MUL:
                case GT_CAST:
                case GT_ADD:
                case GT_SUB:
                {
                    if ((tree.Flags & GTF_OVERFLOW) is not 0)
                    {
                        jitprintf("[OVERFLOW]");
                    }
                    break;
                }
                default:
                {
                    break;
                }
            }
            if ((tree.Flags & GTF_EXCEPT) is not 0)
            {
                jitprintf("[EXCEPT]");
            }
            if ((tree.Flags & GTF_GLOB_REF) is not 0)
            {
                jitprintf("[GLOB_REF]");
            }
            if ((tree.Flags & GTF_ORDER_SIDEEFF) is not 0)
            {
                jitprintf("[ORDER_SIDEEFF]");
            }
            if ((tree.Flags & GTF_REVERSE_OPS) is not 0)
            {
                if (op != GT_LCL_VAR)
                {
                    jitprintf("[REVERSE_OPS]");
                }
            }
            if ((tree.Flags & GTF_SPILLED) is not 0)
            {
                jitprintf("[SPILLED_OPER]");
            }
            if ((tree.Flags & GTF_SET_FLAGS) is not 0)
            {
                jitprintf("[SET_FLAGS]");
            }
            if ((tree.Flags & GTF_IND_NONFAULTING) is not 0)
            {
                if (tree.Oper.IsIndirOrArrMetaData)
                {
                    jitprintf("[IND_NONFAULTING]");
                }
            }
            if ((tree.Flags & GTF_MAKE_CSE) is not 0)
            {
                jitprintf("[MAKE_CSE]");
            }
            if ((tree.Flags & GTF_DONT_CSE) is not 0)
            {
                jitprintf("[DONT_CSE]");
            }
            if ((tree.Flags & GTF_UNSIGNED) is not 0)
            {
                jitprintf("[SMALL_UNSIGNED]");
            }
            if ((tree.Flags & GTF_SPILL) is not 0)
            {
                jitprintf("[SPILL]");
            }
            if ((tree.Flags & GTF_REUSE_REG_VAL) is not 0)
            {
                if (op == GT_CNS_INT)
                {
                    jitprintf("[REUSE_REG_VAL]");
                }
            }
        }
    }
#endif
}
