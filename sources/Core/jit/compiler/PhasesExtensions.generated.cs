// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public static partial class PhasesExtensions
{
    private static string GetName(Phases phase)
    {
        switch (phase)
        {
            case Phases.PHASE_PRE_IMPORT:
            {
                return "Pre-import";
            }
            case Phases.PHASE_IMPORTATION:
            {
                return "Importation";
            }
            case Phases.PHASE_INDXCALL:
            {
                return "Indirect call transform";
            }
            case Phases.PHASE_PATCHPOINTS:
            {
                return "Expand patchpoints";
            }
            case Phases.PHASE_POST_IMPORT:
            {
                return "Post-import";
            }
            case Phases.PHASE_ASYNC_SAVE_CONTEXTS:
            {
                return "Save contexts around async calls";
            }
            case Phases.PHASE_IBCPREP:
            {
                return "Profile instrumentation prep";
            }
            case Phases.PHASE_IBCINSTR:
            {
                return "Profile instrumentation";
            }
            case Phases.PHASE_INCPROFILE:
            {
                return "Profile incorporation";
            }
            case Phases.PHASE_POST_INLINE_NORETURN:
            {
                return "Post-inline no-return cleanup";
            }
            case Phases.PHASE_RESOLVE_GDVS:
            {
                return "Resolve GDV Checks";
            }
            case Phases.PHASE_MORPH_INIT:
            {
                return "Morph - Init";
            }
            case Phases.PHASE_MORPH_INLINE:
            {
                return "Morph - Inlining";
            }
            case Phases.PHASE_MORPH_ADD_INTERNAL:
            {
                return "Morph - Add internal blocks";
            }
            case Phases.PHASE_SWIFT_ERROR_RET:
            {
                return "Add Swift error returns";
            }
            case Phases.PHASE_ALLOCATE_OBJECTS:
            {
                return "Allocate Objects";
            }
            case Phases.PHASE_EMPTY_TRY:
            {
                return "Remove empty try";
            }
            case Phases.PHASE_EMPTY_TRY_CATCH_FAULT:
            {
                return "Remove empty try/catch/fault";
            }
            case Phases.PHASE_EMPTY_FINALLY:
            {
                return "Remove empty finally";
            }
            case Phases.PHASE_MERGE_FINALLY_CHAINS:
            {
                return "Merge callfinally chains";
            }
            case Phases.PHASE_CLONE_FINALLY:
            {
                return "Clone finally";
            }
            case Phases.PHASE_UPDATE_FINALLY_FLAGS:
            {
                return "Update finally target flags";
            }
            case Phases.PHASE_EARLY_UPDATE_FLOW_GRAPH:
            {
                return "Update flow graph early pass";
            }
            case Phases.PHASE_DFS_BLOCKS1:
            {
                return "DFS blocks and remove dead code 1";
            }
            case Phases.PHASE_DFS_BLOCKS2:
            {
                return "DFS blocks and remove dead code 2";
            }
            case Phases.PHASE_DFS_BLOCKS3:
            {
                return "DFS blocks and remove dead code 3";
            }
            case Phases.PHASE_LOCAL_MORPH:
            {
                return "Local morph";
            }
            case Phases.PHASE_OPTIMIZE_MASK_CONVERSIONS:
            {
                return "Optimize mask conversions";
            }
            case Phases.PHASE_EARLY_LIVENESS:
            {
                return "Early liveness";
            }
            case Phases.PHASE_PHYSICAL_PROMOTION:
            {
                return "Physical promotion";
            }
            case Phases.PHASE_UNPIN_LOCALS:
            {
                return "Unpin non-movable locals";
            }
            case Phases.PHASE_FWD_SUB:
            {
                return "Forward Substitution";
            }
            case Phases.PHASE_IMPBYREF_COPY_OMISSION:
            {
                return "Identify candidates for implicit byref copy omission";
            }
            case Phases.PHASE_MORPH_IMPBYREF:
            {
                return "Morph - ByRefs";
            }
            case Phases.PHASE_PROMOTE_STRUCTS:
            {
                return "Morph - Promote Structs";
            }
            case Phases.PHASE_MORPH_GLOBAL:
            {
                return "Morph - Global";
            }
            case Phases.PHASE_POST_MORPH:
            {
                return "Post-Morph";
            }
            case Phases.PHASE_MORPH_END:
            {
                return "Morph - Finish";
            }
            case Phases.PHASE_GS_COOKIE:
            {
                return "GS Cookie";
            }
            case Phases.PHASE_COMPUTE_BLOCK_WEIGHTS:
            {
                return "Compute block weights";
            }
            case Phases.PHASE_CREATE_FUNCLETS:
            {
                return "Create EH funclets";
            }
            case Phases.PHASE_HEAD_TAIL_MERGE:
            {
                return "Head and tail merge";
            }
            case Phases.PHASE_EARLY_QMARK_EXPANSION:
            {
                return "Early QMARK expansion";
            }
            case Phases.PHASE_INVERT_LOOPS:
            {
                return "Invert loops";
            }
            case Phases.PHASE_HEAD_TAIL_MERGE2:
            {
                return "Post-morph head and tail merge";
            }
            case Phases.PHASE_OPTIMIZE_FLOW:
            {
                return "Optimize control flow";
            }
            case Phases.PHASE_OPTIMIZE_PRE_LAYOUT:
            {
                return "Optimize pre-layout";
            }
            case Phases.PHASE_OPTIMIZE_LAYOUT:
            {
                return "Optimize layout";
            }
            case Phases.PHASE_OPTIMIZE_POST_LAYOUT:
            {
                return "Optimize post-layout";
            }
            case Phases.PHASE_COMPUTE_DOMINATORS:
            {
                return "Compute dominators";
            }
            case Phases.PHASE_CANONICALIZE_ENTRY:
            {
                return "Canonicalize entry";
            }
            case Phases.PHASE_SET_BLOCK_WEIGHTS:
            {
                return "Set block weights";
            }
            case Phases.PHASE_ZERO_INITS:
            {
                return "Redundant zero Inits";
            }
            case Phases.PHASE_ADJUST_THROW_LIKELIHOODS:
            {
                return "Adjust throw edge likelihoods";
            }
            case Phases.PHASE_FIND_LOOPS:
            {
                return "Find loops";
            }
            case Phases.PHASE_CLONE_LOOPS:
            {
                return "Clone loops";
            }
            case Phases.PHASE_UNROLL_LOOPS:
            {
                return "Unroll loops";
            }
            case Phases.PHASE_MORPH_MDARR:
            {
                return "Morph array ops";
            }
            case Phases.PHASE_EMPTY_FINALLY_2:
            {
                return "Remove empty finally 2";
            }
            case Phases.PHASE_EMPTY_TRY_2:
            {
                return "Remove empty try 2";
            }
            case Phases.PHASE_EMPTY_TRY_CATCH_FAULT_2:
            {
                return "Remove empty try-catch-fault 2";
            }
            case Phases.PHASE_HOIST_LOOP_CODE:
            {
                return "Hoist loop code";
            }
            case Phases.PHASE_MARK_LOCAL_VARS:
            {
                return "Mark local vars";
            }
            case Phases.PHASE_OPTIMIZE_BOOLS:
            {
                return "Optimize bools";
            }
            case Phases.PHASE_SWITCH_RECOGNITION:
            {
                return "Recognize Switch";
            }
            case Phases.PHASE_FIND_OPER_ORDER:
            {
                return "Find oper order";
            }
            case Phases.PHASE_SET_BLOCK_ORDER:
            {
                return "Set block order";
            }
            case Phases.PHASE_BUILD_SSA:
            {
                return "Build SSA representation";
            }
            case Phases.PHASE_BUILD_SSA_LIVENESS:
            {
                return "SSA: liveness";
            }
            case Phases.PHASE_BUILD_SSA_DF:
            {
                return "SSA: DF";
            }
            case Phases.PHASE_BUILD_SSA_INSERT_PHIS:
            {
                return "SSA: insert phis";
            }
            case Phases.PHASE_BUILD_SSA_RENAME:
            {
                return "SSA: rename";
            }
            case Phases.PHASE_EARLY_PROP:
            {
                return "Early Value Propagation";
            }
            case Phases.PHASE_OPTIMIZE_INDUCTION_VARIABLES:
            {
                return "Optimize Induction Variables";
            }
            case Phases.PHASE_VALUE_NUMBER:
            {
                return "Do value numbering";
            }
            case Phases.PHASE_OPTIMIZE_INDEX_CHECKS:
            {
                return "Optimize index checks";
            }
            case Phases.PHASE_OPTIMIZE_VALNUM_CSES:
            {
                return "Optimize Valnum CSEs";
            }
            case Phases.PHASE_VN_COPY_PROP:
            {
                return "VN based copy prop";
            }
            case Phases.PHASE_VN_BASED_INTRINSIC_EXPAND:
            {
                return "VN based intrinsic expansion";
            }
            case Phases.PHASE_OPTIMIZE_BRANCHES:
            {
                return "Redundant branch opts";
            }
            case Phases.PHASE_BOUNDS_CHECK_COALESCE:
            {
                return "Coalesce bounds checks";
            }
            case Phases.PHASE_ASSERTION_PROP_MAIN:
            {
                return "Assertion prop";
            }
            case Phases.PHASE_RANGE_CHECK_CLONING:
            {
                return "Clone blocks with range checks";
            }
            case Phases.PHASE_IF_CONVERSION:
            {
                return "If conversion";
            }
            case Phases.PHASE_VN_BASED_DEAD_STORE_REMOVAL:
            {
                return "VN-based dead store removal";
            }
            case Phases.PHASE_EMPTY_FINALLY_3:
            {
                return "Remove empty finally 3";
            }
            case Phases.PHASE_EMPTY_TRY_3:
            {
                return "Remove empty try 3";
            }
            case Phases.PHASE_EMPTY_TRY_CATCH_FAULT_3:
            {
                return "Remove empty try-catch-fault 3";
            }
            case Phases.PHASE_REMOVE_UNREACHABLE_TRY:
            {
                return "Remove unreachable try";
            }
            case Phases.PHASE_OPT_UPDATE_FLOW_GRAPH:
            {
                return "Update flow graph opt pass";
            }
            case Phases.PHASE_OPT_DFS_BLOCKS:
            {
                return "Remove unreachable blocks";
            }
            case Phases.PHASE_STRESS_SPLIT_TREE:
            {
                return "Stress gtSplitTree";
            }
            case Phases.PHASE_EXPAND_RTLOOKUPS:
            {
                return "Expand runtime lookups";
            }
            case Phases.PHASE_EXPAND_STATIC_INIT:
            {
                return "Expand static init";
            }
            case Phases.PHASE_EXPAND_CASTS:
            {
                return "Expand casts";
            }
            case Phases.PHASE_EXPAND_TLS:
            {
                return "Expand TLS access";
            }
            case Phases.PHASE_EXPAND_STACK_ARR:
            {
                return "Expand stack array allocation";
            }
            case Phases.PHASE_INSERT_GC_POLLS:
            {
                return "Insert GC Polls";
            }
            case Phases.PHASE_CREATE_THROW_HELPERS:
            {
                return "Create throw helper blocks";
            }
            case Phases.PHASE_DETERMINE_FIRST_COLD_BLOCK:
            {
                return "Determine first cold block";
            }
            case Phases.PHASE_RATIONALIZE:
            {
                return "Rationalize IR";
            }
            case Phases.PHASE_REPAIR_PROFILE_POST_MORPH:
            {
                return "Repair profile post-morph";
            }
            case Phases.PHASE_REPAIR_PROFILE_PRE_LAYOUT:
            {
                return "Repair profile pre-layout";
            }
            case Phases.PHASE_DFS_BLOCKS_WASM:
            {
                return "Wasm remove unreachable blocks";
            }
            case Phases.PHASE_WASM_EH_FLOW:
            {
                return "Wasm eh control flow";
            }
            case Phases.PHASE_WASM_REPAIR_TRY_ENTRIES:
            {
                return "Wasm repair try entries";
            }
            case Phases.PHASE_WASM_TRANSFORM_SCCS:
            {
                return "Wasm transform sccs";
            }
            case Phases.PHASE_WASM_CONTROL_FLOW:
            {
                return "Wasm control flow";
            }
            case Phases.PHASE_WASM_IDENTIFY_GCSAFE_LOCALS:
            {
                return "Wasm identify GC safe locals";
            }
            case Phases.PHASE_WASM_SPILL_REFS:
            {
                return "Wasm spill refs";
            }
            case Phases.PHASE_WASM_VIRTUAL_IP:
            {
                return "Wasm virtual IP";
            }
            case Phases.PHASE_WASM_PROF_INSTRUMENT:
            {
                return "Wasm profiler instrumentation";
            }
            case Phases.PHASE_ASYNC:
            {
                return "Transform async";
            }
            case Phases.PHASE_LCLVARLIVENESS:
            {
                return "Local var liveness";
            }
            case Phases.PHASE_LCLVARLIVENESS_INIT:
            {
                return "Local var liveness init";
            }
            case Phases.PHASE_LCLVARLIVENESS_PERBLOCK:
            {
                return "Per block local var liveness";
            }
            case Phases.PHASE_LCLVARLIVENESS_INTERBLOCK:
            {
                return "Global local var liveness";
            }
            case Phases.PHASE_LOWERING_DECOMP:
            {
                return "Lowering decomposition";
            }
            case Phases.PHASE_LOWERING:
            {
                return "Lowering nodeinfo";
            }
            case Phases.PHASE_STACK_LEVEL_SETTER:
            {
                return "Calculate stack level slots";
            }
            case Phases.PHASE_LINEAR_SCAN:
            {
                return "Linear scan register alloc";
            }
            case Phases.PHASE_LINEAR_SCAN_BUILD:
            {
                return "LSRA build intervals";
            }
            case Phases.PHASE_LINEAR_SCAN_ALLOC:
            {
                return "LSRA allocate";
            }
            case Phases.PHASE_LINEAR_SCAN_RESOLVE:
            {
                return "LSRA resolve";
            }
            case Phases.PHASE_ALIGN_LOOPS:
            {
                return "Place 'align' instructions";
            }
            case Phases.PHASE_GENERATE_CODE:
            {
                return "Generate code";
            }
            case Phases.PHASE_EMIT_CODE:
            {
                return "Emit code";
            }
            case Phases.PHASE_EMIT_GCEH:
            {
                return "Emit GC+EH tables";
            }
            case Phases.PHASE_POST_EMIT:
            {
                return "Post-Emit";
            }
            case Phases.PHASE_CLR_API:
            {
                return "CLR API calls";
            }
            default:
            {
                FatalJitException.ThrowFatalJitException(
                    CORJIT_INTERNALERROR,
                    FatalJitErrorCode.InvalidCompilerPhase
                );
                return default!;
            }
        }
    }

    private static ReadOnlySpan<bool> s_hasChildren => [
        false, // PHASE_PRE_IMPORT
        false, // PHASE_IMPORTATION
        false, // PHASE_INDXCALL
        false, // PHASE_PATCHPOINTS
        false, // PHASE_POST_IMPORT
        false, // PHASE_ASYNC_SAVE_CONTEXTS
        false, // PHASE_IBCPREP
        false, // PHASE_IBCINSTR
        false, // PHASE_INCPROFILE
        false, // PHASE_POST_INLINE_NORETURN
        false, // PHASE_RESOLVE_GDVS
        false, // PHASE_MORPH_INIT
        false, // PHASE_MORPH_INLINE
        false, // PHASE_MORPH_ADD_INTERNAL
        false, // PHASE_SWIFT_ERROR_RET
        false, // PHASE_ALLOCATE_OBJECTS
        false, // PHASE_EMPTY_TRY
        false, // PHASE_EMPTY_TRY_CATCH_FAULT
        false, // PHASE_EMPTY_FINALLY
        false, // PHASE_MERGE_FINALLY_CHAINS
        false, // PHASE_CLONE_FINALLY
        false, // PHASE_UPDATE_FINALLY_FLAGS
        false, // PHASE_EARLY_UPDATE_FLOW_GRAPH
        false, // PHASE_DFS_BLOCKS1
        false, // PHASE_DFS_BLOCKS2
        false, // PHASE_DFS_BLOCKS3
        false, // PHASE_LOCAL_MORPH
        false, // PHASE_OPTIMIZE_MASK_CONVERSIONS
        false, // PHASE_EARLY_LIVENESS
        false, // PHASE_PHYSICAL_PROMOTION
        false, // PHASE_UNPIN_LOCALS
        false, // PHASE_FWD_SUB
        false, // PHASE_IMPBYREF_COPY_OMISSION
        false, // PHASE_MORPH_IMPBYREF
        false, // PHASE_PROMOTE_STRUCTS
        false, // PHASE_MORPH_GLOBAL
        false, // PHASE_POST_MORPH
        false, // PHASE_MORPH_END
        false, // PHASE_GS_COOKIE
        false, // PHASE_COMPUTE_BLOCK_WEIGHTS
        false, // PHASE_CREATE_FUNCLETS
        false, // PHASE_HEAD_TAIL_MERGE
        false, // PHASE_EARLY_QMARK_EXPANSION
        false, // PHASE_INVERT_LOOPS
        false, // PHASE_HEAD_TAIL_MERGE2
        false, // PHASE_OPTIMIZE_FLOW
        false, // PHASE_OPTIMIZE_PRE_LAYOUT
        false, // PHASE_OPTIMIZE_LAYOUT
        false, // PHASE_OPTIMIZE_POST_LAYOUT
        false, // PHASE_COMPUTE_DOMINATORS
        false, // PHASE_CANONICALIZE_ENTRY
        false, // PHASE_SET_BLOCK_WEIGHTS
        false, // PHASE_ZERO_INITS
        false, // PHASE_ADJUST_THROW_LIKELIHOODS
        false, // PHASE_FIND_LOOPS
        false, // PHASE_CLONE_LOOPS
        false, // PHASE_UNROLL_LOOPS
        false, // PHASE_MORPH_MDARR
        false, // PHASE_EMPTY_FINALLY_2
        false, // PHASE_EMPTY_TRY_2
        false, // PHASE_EMPTY_TRY_CATCH_FAULT_2
        false, // PHASE_HOIST_LOOP_CODE
        false, // PHASE_MARK_LOCAL_VARS
        false, // PHASE_OPTIMIZE_BOOLS
        false, // PHASE_SWITCH_RECOGNITION
        false, // PHASE_FIND_OPER_ORDER
        false, // PHASE_SET_BLOCK_ORDER
        true, // PHASE_BUILD_SSA
        false, // PHASE_BUILD_SSA_LIVENESS
        false, // PHASE_BUILD_SSA_DF
        false, // PHASE_BUILD_SSA_INSERT_PHIS
        false, // PHASE_BUILD_SSA_RENAME
        false, // PHASE_EARLY_PROP
        false, // PHASE_OPTIMIZE_INDUCTION_VARIABLES
        false, // PHASE_VALUE_NUMBER
        false, // PHASE_OPTIMIZE_INDEX_CHECKS
        false, // PHASE_OPTIMIZE_VALNUM_CSES
        false, // PHASE_VN_COPY_PROP
        false, // PHASE_VN_BASED_INTRINSIC_EXPAND
        false, // PHASE_OPTIMIZE_BRANCHES
        false, // PHASE_BOUNDS_CHECK_COALESCE
        false, // PHASE_ASSERTION_PROP_MAIN
        false, // PHASE_RANGE_CHECK_CLONING
        false, // PHASE_IF_CONVERSION
        false, // PHASE_VN_BASED_DEAD_STORE_REMOVAL
        false, // PHASE_EMPTY_FINALLY_3
        false, // PHASE_EMPTY_TRY_3
        false, // PHASE_EMPTY_TRY_CATCH_FAULT_3
        false, // PHASE_REMOVE_UNREACHABLE_TRY
        false, // PHASE_OPT_UPDATE_FLOW_GRAPH
        false, // PHASE_OPT_DFS_BLOCKS
        false, // PHASE_STRESS_SPLIT_TREE
        false, // PHASE_EXPAND_RTLOOKUPS
        false, // PHASE_EXPAND_STATIC_INIT
        false, // PHASE_EXPAND_CASTS
        false, // PHASE_EXPAND_TLS
        false, // PHASE_EXPAND_STACK_ARR
        false, // PHASE_INSERT_GC_POLLS
        false, // PHASE_CREATE_THROW_HELPERS
        false, // PHASE_DETERMINE_FIRST_COLD_BLOCK
        false, // PHASE_RATIONALIZE
        false, // PHASE_REPAIR_PROFILE_POST_MORPH
        false, // PHASE_REPAIR_PROFILE_PRE_LAYOUT
        false, // PHASE_DFS_BLOCKS_WASM
        false, // PHASE_WASM_EH_FLOW
        false, // PHASE_WASM_REPAIR_TRY_ENTRIES
        false, // PHASE_WASM_TRANSFORM_SCCS
        false, // PHASE_WASM_CONTROL_FLOW
        false, // PHASE_WASM_IDENTIFY_GCSAFE_LOCALS
        false, // PHASE_WASM_SPILL_REFS
        false, // PHASE_WASM_VIRTUAL_IP
        false, // PHASE_WASM_PROF_INSTRUMENT
        false, // PHASE_ASYNC
        true, // PHASE_LCLVARLIVENESS
        false, // PHASE_LCLVARLIVENESS_INIT
        false, // PHASE_LCLVARLIVENESS_PERBLOCK
        false, // PHASE_LCLVARLIVENESS_INTERBLOCK
        false, // PHASE_LOWERING_DECOMP
        false, // PHASE_LOWERING
        false, // PHASE_STACK_LEVEL_SETTER
        true, // PHASE_LINEAR_SCAN
        false, // PHASE_LINEAR_SCAN_BUILD
        false, // PHASE_LINEAR_SCAN_ALLOC
        false, // PHASE_LINEAR_SCAN_RESOLVE
        false, // PHASE_ALIGN_LOOPS
        false, // PHASE_GENERATE_CODE
        false, // PHASE_EMIT_CODE
        false, // PHASE_EMIT_GCEH
        false, // PHASE_POST_EMIT
        false, // PHASE_CLR_API
    ];

    private static ReadOnlySpan<Phases> s_parents => [
        (Phases)(-1), // PHASE_PRE_IMPORT
        (Phases)(-1), // PHASE_IMPORTATION
        (Phases)(-1), // PHASE_INDXCALL
        (Phases)(-1), // PHASE_PATCHPOINTS
        (Phases)(-1), // PHASE_POST_IMPORT
        (Phases)(-1), // PHASE_ASYNC_SAVE_CONTEXTS
        (Phases)(-1), // PHASE_IBCPREP
        (Phases)(-1), // PHASE_IBCINSTR
        (Phases)(-1), // PHASE_INCPROFILE
        (Phases)(-1), // PHASE_POST_INLINE_NORETURN
        (Phases)(-1), // PHASE_RESOLVE_GDVS
        (Phases)(-1), // PHASE_MORPH_INIT
        (Phases)(-1), // PHASE_MORPH_INLINE
        (Phases)(-1), // PHASE_MORPH_ADD_INTERNAL
        (Phases)(-1), // PHASE_SWIFT_ERROR_RET
        (Phases)(-1), // PHASE_ALLOCATE_OBJECTS
        (Phases)(-1), // PHASE_EMPTY_TRY
        (Phases)(-1), // PHASE_EMPTY_TRY_CATCH_FAULT
        (Phases)(-1), // PHASE_EMPTY_FINALLY
        (Phases)(-1), // PHASE_MERGE_FINALLY_CHAINS
        (Phases)(-1), // PHASE_CLONE_FINALLY
        (Phases)(-1), // PHASE_UPDATE_FINALLY_FLAGS
        (Phases)(-1), // PHASE_EARLY_UPDATE_FLOW_GRAPH
        (Phases)(-1), // PHASE_DFS_BLOCKS1
        (Phases)(-1), // PHASE_DFS_BLOCKS2
        (Phases)(-1), // PHASE_DFS_BLOCKS3
        (Phases)(-1), // PHASE_LOCAL_MORPH
        (Phases)(-1), // PHASE_OPTIMIZE_MASK_CONVERSIONS
        (Phases)(-1), // PHASE_EARLY_LIVENESS
        (Phases)(-1), // PHASE_PHYSICAL_PROMOTION
        (Phases)(-1), // PHASE_UNPIN_LOCALS
        (Phases)(-1), // PHASE_FWD_SUB
        (Phases)(-1), // PHASE_IMPBYREF_COPY_OMISSION
        (Phases)(-1), // PHASE_MORPH_IMPBYREF
        (Phases)(-1), // PHASE_PROMOTE_STRUCTS
        (Phases)(-1), // PHASE_MORPH_GLOBAL
        (Phases)(-1), // PHASE_POST_MORPH
        (Phases)(-1), // PHASE_MORPH_END
        (Phases)(-1), // PHASE_GS_COOKIE
        (Phases)(-1), // PHASE_COMPUTE_BLOCK_WEIGHTS
        (Phases)(-1), // PHASE_CREATE_FUNCLETS
        (Phases)(-1), // PHASE_HEAD_TAIL_MERGE
        (Phases)(-1), // PHASE_EARLY_QMARK_EXPANSION
        (Phases)(-1), // PHASE_INVERT_LOOPS
        (Phases)(-1), // PHASE_HEAD_TAIL_MERGE2
        (Phases)(-1), // PHASE_OPTIMIZE_FLOW
        (Phases)(-1), // PHASE_OPTIMIZE_PRE_LAYOUT
        (Phases)(-1), // PHASE_OPTIMIZE_LAYOUT
        (Phases)(-1), // PHASE_OPTIMIZE_POST_LAYOUT
        (Phases)(-1), // PHASE_COMPUTE_DOMINATORS
        (Phases)(-1), // PHASE_CANONICALIZE_ENTRY
        (Phases)(-1), // PHASE_SET_BLOCK_WEIGHTS
        (Phases)(-1), // PHASE_ZERO_INITS
        (Phases)(-1), // PHASE_ADJUST_THROW_LIKELIHOODS
        (Phases)(-1), // PHASE_FIND_LOOPS
        (Phases)(-1), // PHASE_CLONE_LOOPS
        (Phases)(-1), // PHASE_UNROLL_LOOPS
        (Phases)(-1), // PHASE_MORPH_MDARR
        (Phases)(-1), // PHASE_EMPTY_FINALLY_2
        (Phases)(-1), // PHASE_EMPTY_TRY_2
        (Phases)(-1), // PHASE_EMPTY_TRY_CATCH_FAULT_2
        (Phases)(-1), // PHASE_HOIST_LOOP_CODE
        (Phases)(-1), // PHASE_MARK_LOCAL_VARS
        (Phases)(-1), // PHASE_OPTIMIZE_BOOLS
        (Phases)(-1), // PHASE_SWITCH_RECOGNITION
        (Phases)(-1), // PHASE_FIND_OPER_ORDER
        (Phases)(-1), // PHASE_SET_BLOCK_ORDER
        (Phases)(-1), // PHASE_BUILD_SSA
        PHASE_BUILD_SSA, // PHASE_BUILD_SSA_LIVENESS
        PHASE_BUILD_SSA, // PHASE_BUILD_SSA_DF
        PHASE_BUILD_SSA, // PHASE_BUILD_SSA_INSERT_PHIS
        PHASE_BUILD_SSA, // PHASE_BUILD_SSA_RENAME
        (Phases)(-1), // PHASE_EARLY_PROP
        (Phases)(-1), // PHASE_OPTIMIZE_INDUCTION_VARIABLES
        (Phases)(-1), // PHASE_VALUE_NUMBER
        (Phases)(-1), // PHASE_OPTIMIZE_INDEX_CHECKS
        (Phases)(-1), // PHASE_OPTIMIZE_VALNUM_CSES
        (Phases)(-1), // PHASE_VN_COPY_PROP
        (Phases)(-1), // PHASE_VN_BASED_INTRINSIC_EXPAND
        (Phases)(-1), // PHASE_OPTIMIZE_BRANCHES
        (Phases)(-1), // PHASE_BOUNDS_CHECK_COALESCE
        (Phases)(-1), // PHASE_ASSERTION_PROP_MAIN
        (Phases)(-1), // PHASE_RANGE_CHECK_CLONING
        (Phases)(-1), // PHASE_IF_CONVERSION
        (Phases)(-1), // PHASE_VN_BASED_DEAD_STORE_REMOVAL
        (Phases)(-1), // PHASE_EMPTY_FINALLY_3
        (Phases)(-1), // PHASE_EMPTY_TRY_3
        (Phases)(-1), // PHASE_EMPTY_TRY_CATCH_FAULT_3
        (Phases)(-1), // PHASE_REMOVE_UNREACHABLE_TRY
        (Phases)(-1), // PHASE_OPT_UPDATE_FLOW_GRAPH
        (Phases)(-1), // PHASE_OPT_DFS_BLOCKS
        (Phases)(-1), // PHASE_STRESS_SPLIT_TREE
        (Phases)(-1), // PHASE_EXPAND_RTLOOKUPS
        (Phases)(-1), // PHASE_EXPAND_STATIC_INIT
        (Phases)(-1), // PHASE_EXPAND_CASTS
        (Phases)(-1), // PHASE_EXPAND_TLS
        (Phases)(-1), // PHASE_EXPAND_STACK_ARR
        (Phases)(-1), // PHASE_INSERT_GC_POLLS
        (Phases)(-1), // PHASE_CREATE_THROW_HELPERS
        (Phases)(-1), // PHASE_DETERMINE_FIRST_COLD_BLOCK
        (Phases)(-1), // PHASE_RATIONALIZE
        (Phases)(-1), // PHASE_REPAIR_PROFILE_POST_MORPH
        (Phases)(-1), // PHASE_REPAIR_PROFILE_PRE_LAYOUT
        (Phases)(-1), // PHASE_DFS_BLOCKS_WASM
        (Phases)(-1), // PHASE_WASM_EH_FLOW
        (Phases)(-1), // PHASE_WASM_REPAIR_TRY_ENTRIES
        (Phases)(-1), // PHASE_WASM_TRANSFORM_SCCS
        (Phases)(-1), // PHASE_WASM_CONTROL_FLOW
        (Phases)(-1), // PHASE_WASM_IDENTIFY_GCSAFE_LOCALS
        (Phases)(-1), // PHASE_WASM_SPILL_REFS
        (Phases)(-1), // PHASE_WASM_VIRTUAL_IP
        (Phases)(-1), // PHASE_WASM_PROF_INSTRUMENT
        (Phases)(-1), // PHASE_ASYNC
        (Phases)(-1), // PHASE_LCLVARLIVENESS
        PHASE_LCLVARLIVENESS, // PHASE_LCLVARLIVENESS_INIT
        PHASE_LCLVARLIVENESS, // PHASE_LCLVARLIVENESS_PERBLOCK
        PHASE_LCLVARLIVENESS, // PHASE_LCLVARLIVENESS_INTERBLOCK
        (Phases)(-1), // PHASE_LOWERING_DECOMP
        (Phases)(-1), // PHASE_LOWERING
        (Phases)(-1), // PHASE_STACK_LEVEL_SETTER
        (Phases)(-1), // PHASE_LINEAR_SCAN
        PHASE_LINEAR_SCAN, // PHASE_LINEAR_SCAN_BUILD
        PHASE_LINEAR_SCAN, // PHASE_LINEAR_SCAN_ALLOC
        PHASE_LINEAR_SCAN, // PHASE_LINEAR_SCAN_RESOLVE
        (Phases)(-1), // PHASE_ALIGN_LOOPS
        (Phases)(-1), // PHASE_GENERATE_CODE
        (Phases)(-1), // PHASE_EMIT_CODE
        (Phases)(-1), // PHASE_EMIT_GCEH
        (Phases)(-1), // PHASE_POST_EMIT
        (Phases)(-1), // PHASE_CLR_API
    ];

    private static ReadOnlySpan<bool> s_reportsIRSize => [
        false, // PHASE_PRE_IMPORT
        true, // PHASE_IMPORTATION
        true, // PHASE_INDXCALL
        true, // PHASE_PATCHPOINTS
        false, // PHASE_POST_IMPORT
        false, // PHASE_ASYNC_SAVE_CONTEXTS
        false, // PHASE_IBCPREP
        false, // PHASE_IBCINSTR
        false, // PHASE_INCPROFILE
        false, // PHASE_POST_INLINE_NORETURN
        false, // PHASE_RESOLVE_GDVS
        false, // PHASE_MORPH_INIT
        true, // PHASE_MORPH_INLINE
        true, // PHASE_MORPH_ADD_INTERNAL
        true, // PHASE_SWIFT_ERROR_RET
        false, // PHASE_ALLOCATE_OBJECTS
        false, // PHASE_EMPTY_TRY
        false, // PHASE_EMPTY_TRY_CATCH_FAULT
        false, // PHASE_EMPTY_FINALLY
        false, // PHASE_MERGE_FINALLY_CHAINS
        false, // PHASE_CLONE_FINALLY
        false, // PHASE_UPDATE_FINALLY_FLAGS
        false, // PHASE_EARLY_UPDATE_FLOW_GRAPH
        false, // PHASE_DFS_BLOCKS1
        false, // PHASE_DFS_BLOCKS2
        false, // PHASE_DFS_BLOCKS3
        false, // PHASE_LOCAL_MORPH
        false, // PHASE_OPTIMIZE_MASK_CONVERSIONS
        false, // PHASE_EARLY_LIVENESS
        false, // PHASE_PHYSICAL_PROMOTION
        false, // PHASE_UNPIN_LOCALS
        false, // PHASE_FWD_SUB
        false, // PHASE_IMPBYREF_COPY_OMISSION
        false, // PHASE_MORPH_IMPBYREF
        false, // PHASE_PROMOTE_STRUCTS
        false, // PHASE_MORPH_GLOBAL
        false, // PHASE_POST_MORPH
        true, // PHASE_MORPH_END
        false, // PHASE_GS_COOKIE
        false, // PHASE_COMPUTE_BLOCK_WEIGHTS
        false, // PHASE_CREATE_FUNCLETS
        false, // PHASE_HEAD_TAIL_MERGE
        false, // PHASE_EARLY_QMARK_EXPANSION
        false, // PHASE_INVERT_LOOPS
        false, // PHASE_HEAD_TAIL_MERGE2
        false, // PHASE_OPTIMIZE_FLOW
        false, // PHASE_OPTIMIZE_PRE_LAYOUT
        false, // PHASE_OPTIMIZE_LAYOUT
        false, // PHASE_OPTIMIZE_POST_LAYOUT
        false, // PHASE_COMPUTE_DOMINATORS
        false, // PHASE_CANONICALIZE_ENTRY
        false, // PHASE_SET_BLOCK_WEIGHTS
        false, // PHASE_ZERO_INITS
        false, // PHASE_ADJUST_THROW_LIKELIHOODS
        false, // PHASE_FIND_LOOPS
        false, // PHASE_CLONE_LOOPS
        false, // PHASE_UNROLL_LOOPS
        false, // PHASE_MORPH_MDARR
        false, // PHASE_EMPTY_FINALLY_2
        false, // PHASE_EMPTY_TRY_2
        false, // PHASE_EMPTY_TRY_CATCH_FAULT_2
        false, // PHASE_HOIST_LOOP_CODE
        false, // PHASE_MARK_LOCAL_VARS
        false, // PHASE_OPTIMIZE_BOOLS
        false, // PHASE_SWITCH_RECOGNITION
        false, // PHASE_FIND_OPER_ORDER
        true, // PHASE_SET_BLOCK_ORDER
        false, // PHASE_BUILD_SSA
        false, // PHASE_BUILD_SSA_LIVENESS
        false, // PHASE_BUILD_SSA_DF
        false, // PHASE_BUILD_SSA_INSERT_PHIS
        false, // PHASE_BUILD_SSA_RENAME
        false, // PHASE_EARLY_PROP
        false, // PHASE_OPTIMIZE_INDUCTION_VARIABLES
        false, // PHASE_VALUE_NUMBER
        false, // PHASE_OPTIMIZE_INDEX_CHECKS
        false, // PHASE_OPTIMIZE_VALNUM_CSES
        false, // PHASE_VN_COPY_PROP
        false, // PHASE_VN_BASED_INTRINSIC_EXPAND
        false, // PHASE_OPTIMIZE_BRANCHES
        false, // PHASE_BOUNDS_CHECK_COALESCE
        false, // PHASE_ASSERTION_PROP_MAIN
        false, // PHASE_RANGE_CHECK_CLONING
        false, // PHASE_IF_CONVERSION
        false, // PHASE_VN_BASED_DEAD_STORE_REMOVAL
        false, // PHASE_EMPTY_FINALLY_3
        false, // PHASE_EMPTY_TRY_3
        false, // PHASE_EMPTY_TRY_CATCH_FAULT_3
        false, // PHASE_REMOVE_UNREACHABLE_TRY
        false, // PHASE_OPT_UPDATE_FLOW_GRAPH
        false, // PHASE_OPT_DFS_BLOCKS
        false, // PHASE_STRESS_SPLIT_TREE
        true, // PHASE_EXPAND_RTLOOKUPS
        true, // PHASE_EXPAND_STATIC_INIT
        true, // PHASE_EXPAND_CASTS
        true, // PHASE_EXPAND_TLS
        true, // PHASE_EXPAND_STACK_ARR
        true, // PHASE_INSERT_GC_POLLS
        true, // PHASE_CREATE_THROW_HELPERS
        true, // PHASE_DETERMINE_FIRST_COLD_BLOCK
        false, // PHASE_RATIONALIZE
        false, // PHASE_REPAIR_PROFILE_POST_MORPH
        false, // PHASE_REPAIR_PROFILE_PRE_LAYOUT
        false, // PHASE_DFS_BLOCKS_WASM
        false, // PHASE_WASM_EH_FLOW
        false, // PHASE_WASM_REPAIR_TRY_ENTRIES
        false, // PHASE_WASM_TRANSFORM_SCCS
        false, // PHASE_WASM_CONTROL_FLOW
        false, // PHASE_WASM_IDENTIFY_GCSAFE_LOCALS
        false, // PHASE_WASM_SPILL_REFS
        false, // PHASE_WASM_VIRTUAL_IP
        false, // PHASE_WASM_PROF_INSTRUMENT
        true, // PHASE_ASYNC
        false, // PHASE_LCLVARLIVENESS
        false, // PHASE_LCLVARLIVENESS_INIT
        false, // PHASE_LCLVARLIVENESS_PERBLOCK
        false, // PHASE_LCLVARLIVENESS_INTERBLOCK
        false, // PHASE_LOWERING_DECOMP
        true, // PHASE_LOWERING
        false, // PHASE_STACK_LEVEL_SETTER
        true, // PHASE_LINEAR_SCAN
        false, // PHASE_LINEAR_SCAN_BUILD
        false, // PHASE_LINEAR_SCAN_ALLOC
        false, // PHASE_LINEAR_SCAN_RESOLVE
        false, // PHASE_ALIGN_LOOPS
        false, // PHASE_GENERATE_CODE
        false, // PHASE_EMIT_CODE
        false, // PHASE_EMIT_GCEH
        false, // PHASE_POST_EMIT
        false, // PHASE_CLR_API
    ];
}