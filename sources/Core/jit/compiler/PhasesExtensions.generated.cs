// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public static partial class PhasesExtensions
{
    private static string GetName(Phases phase) => phase switch
    {
        Phases.PHASE_PRE_IMPORT => "Pre-import",
        Phases.PHASE_IMPORTATION => "Importation",
        Phases.PHASE_INDXCALL => "Indirect call transform",
        Phases.PHASE_PATCHPOINTS => "Expand patchpoints",
        Phases.PHASE_POST_IMPORT => "Post-import",
        Phases.PHASE_ASYNC_SAVE_CONTEXTS => "Save contexts around async calls",
        Phases.PHASE_IBCPREP => "Profile instrumentation prep",
        Phases.PHASE_IBCINSTR => "Profile instrumentation",
        Phases.PHASE_INCPROFILE => "Profile incorporation",
        Phases.PHASE_POST_INLINE_NORETURN => "Post-inline no-return cleanup",
        Phases.PHASE_RESOLVE_GDVS => "Resolve GDV Checks",
        Phases.PHASE_MORPH_INIT => "Morph - Init",
        Phases.PHASE_MORPH_INLINE => "Morph - Inlining",
        Phases.PHASE_MORPH_ADD_INTERNAL => "Morph - Add internal blocks",
        Phases.PHASE_SWIFT_ERROR_RET => "Add Swift error returns",
        Phases.PHASE_ALLOCATE_OBJECTS => "Allocate Objects",
        Phases.PHASE_EMPTY_TRY => "Remove empty try",
        Phases.PHASE_EMPTY_TRY_CATCH_FAULT => "Remove empty try/catch/fault",
        Phases.PHASE_EMPTY_FINALLY => "Remove empty finally",
        Phases.PHASE_MERGE_FINALLY_CHAINS => "Merge callfinally chains",
        Phases.PHASE_CLONE_FINALLY => "Clone finally",
        Phases.PHASE_UPDATE_FINALLY_FLAGS => "Update finally target flags",
        Phases.PHASE_EARLY_UPDATE_FLOW_GRAPH => "Update flow graph early pass",
        Phases.PHASE_DFS_BLOCKS1 => "DFS blocks and remove dead code 1",
        Phases.PHASE_DFS_BLOCKS2 => "DFS blocks and remove dead code 2",
        Phases.PHASE_DFS_BLOCKS3 => "DFS blocks and remove dead code 3",
        Phases.PHASE_LOCAL_MORPH => "Local morph",
        Phases.PHASE_OPTIMIZE_MASK_CONVERSIONS => "Optimize mask conversions",
        Phases.PHASE_EARLY_LIVENESS => "Early liveness",
        Phases.PHASE_PHYSICAL_PROMOTION => "Physical promotion",
        Phases.PHASE_UNPIN_LOCALS => "Unpin non-movable locals",
        Phases.PHASE_FWD_SUB => "Forward Substitution",
        Phases.PHASE_IMPBYREF_COPY_OMISSION => "Identify candidates for implicit byref copy omission",
        Phases.PHASE_MORPH_IMPBYREF => "Morph - ByRefs",
        Phases.PHASE_PROMOTE_STRUCTS => "Morph - Promote Structs",
        Phases.PHASE_MORPH_GLOBAL => "Morph - Global",
        Phases.PHASE_POST_MORPH => "Post-Morph",
        Phases.PHASE_MORPH_END => "Morph - Finish",
        Phases.PHASE_GS_COOKIE => "GS Cookie",
        Phases.PHASE_COMPUTE_BLOCK_WEIGHTS => "Compute block weights",
        Phases.PHASE_CREATE_FUNCLETS => "Create EH funclets",
        Phases.PHASE_HEAD_TAIL_MERGE => "Head and tail merge",
        Phases.PHASE_EARLY_QMARK_EXPANSION => "Early QMARK expansion",
        Phases.PHASE_INVERT_LOOPS => "Invert loops",
        Phases.PHASE_HEAD_TAIL_MERGE2 => "Post-morph head and tail merge",
        Phases.PHASE_OPTIMIZE_FLOW => "Optimize control flow",
        Phases.PHASE_OPTIMIZE_PRE_LAYOUT => "Optimize pre-layout",
        Phases.PHASE_OPTIMIZE_LAYOUT => "Optimize layout",
        Phases.PHASE_OPTIMIZE_POST_LAYOUT => "Optimize post-layout",
        Phases.PHASE_COMPUTE_DOMINATORS => "Compute dominators",
        Phases.PHASE_CANONICALIZE_ENTRY => "Canonicalize entry",
        Phases.PHASE_SET_BLOCK_WEIGHTS => "Set block weights",
        Phases.PHASE_ZERO_INITS => "Redundant zero Inits",
        Phases.PHASE_ADJUST_THROW_LIKELIHOODS => "Adjust throw edge likelihoods",
        Phases.PHASE_FIND_LOOPS => "Find loops",
        Phases.PHASE_CLONE_LOOPS => "Clone loops",
        Phases.PHASE_UNROLL_LOOPS => "Unroll loops",
        Phases.PHASE_MORPH_MDARR => "Morph array ops",
        Phases.PHASE_EMPTY_FINALLY_2 => "Remove empty finally 2",
        Phases.PHASE_EMPTY_TRY_2 => "Remove empty try 2",
        Phases.PHASE_EMPTY_TRY_CATCH_FAULT_2 => "Remove empty try-catch-fault 2",
        Phases.PHASE_HOIST_LOOP_CODE => "Hoist loop code",
        Phases.PHASE_MARK_LOCAL_VARS => "Mark local vars",
        Phases.PHASE_OPTIMIZE_BOOLS => "Optimize bools",
        Phases.PHASE_SWITCH_RECOGNITION => "Recognize Switch",
        Phases.PHASE_FIND_OPER_ORDER => "Find oper order",
        Phases.PHASE_SET_BLOCK_ORDER => "Set block order",
        Phases.PHASE_BUILD_SSA => "Build SSA representation",
        Phases.PHASE_BUILD_SSA_LIVENESS => "SSA: liveness",
        Phases.PHASE_BUILD_SSA_DF => "SSA: DF",
        Phases.PHASE_BUILD_SSA_INSERT_PHIS => "SSA: insert phis",
        Phases.PHASE_BUILD_SSA_RENAME => "SSA: rename",
        Phases.PHASE_EARLY_PROP => "Early Value Propagation",
        Phases.PHASE_OPTIMIZE_INDUCTION_VARIABLES => "Optimize Induction Variables",
        Phases.PHASE_VALUE_NUMBER => "Do value numbering",
        Phases.PHASE_OPTIMIZE_INDEX_CHECKS => "Optimize index checks",
        Phases.PHASE_OPTIMIZE_VALNUM_CSES => "Optimize Valnum CSEs",
        Phases.PHASE_VN_COPY_PROP => "VN based copy prop",
        Phases.PHASE_VN_BASED_INTRINSIC_EXPAND => "VN based intrinsic expansion",
        Phases.PHASE_OPTIMIZE_BRANCHES => "Redundant branch opts",
        Phases.PHASE_BOUNDS_CHECK_COALESCE => "Coalesce bounds checks",
        Phases.PHASE_ASSERTION_PROP_MAIN => "Assertion prop",
        Phases.PHASE_RANGE_CHECK_CLONING => "Clone blocks with range checks",
        Phases.PHASE_IF_CONVERSION => "If conversion",
        Phases.PHASE_VN_BASED_DEAD_STORE_REMOVAL => "VN-based dead store removal",
        Phases.PHASE_EMPTY_FINALLY_3 => "Remove empty finally 3",
        Phases.PHASE_EMPTY_TRY_3 => "Remove empty try 3",
        Phases.PHASE_EMPTY_TRY_CATCH_FAULT_3 => "Remove empty try-catch-fault 3",
        Phases.PHASE_REMOVE_UNREACHABLE_TRY => "Remove unreachable try",
        Phases.PHASE_OPT_UPDATE_FLOW_GRAPH => "Update flow graph opt pass",
        Phases.PHASE_OPT_DFS_BLOCKS => "Remove unreachable blocks",
        Phases.PHASE_STRESS_SPLIT_TREE => "Stress gtSplitTree",
        Phases.PHASE_EXPAND_RTLOOKUPS => "Expand runtime lookups",
        Phases.PHASE_EXPAND_STATIC_INIT => "Expand static init",
        Phases.PHASE_EXPAND_CASTS => "Expand casts",
        Phases.PHASE_EXPAND_TLS => "Expand TLS access",
        Phases.PHASE_EXPAND_STACK_ARR => "Expand stack array allocation",
        Phases.PHASE_INSERT_GC_POLLS => "Insert GC Polls",
        Phases.PHASE_CREATE_THROW_HELPERS => "Create throw helper blocks",
        Phases.PHASE_DETERMINE_FIRST_COLD_BLOCK => "Determine first cold block",
        Phases.PHASE_RATIONALIZE => "Rationalize IR",
        Phases.PHASE_REPAIR_PROFILE_POST_MORPH => "Repair profile post-morph",
        Phases.PHASE_REPAIR_PROFILE_PRE_LAYOUT => "Repair profile pre-layout",
        Phases.PHASE_DFS_BLOCKS_WASM => "Wasm remove unreachable blocks",
        Phases.PHASE_WASM_EH_FLOW => "Wasm eh control flow",
        Phases.PHASE_WASM_REPAIR_TRY_ENTRIES => "Wasm repair try entries",
        Phases.PHASE_WASM_TRANSFORM_SCCS => "Wasm transform sccs",
        Phases.PHASE_WASM_CONTROL_FLOW => "Wasm control flow",
        Phases.PHASE_WASM_IDENTIFY_GCSAFE_LOCALS => "Wasm identify GC safe locals",
        Phases.PHASE_WASM_SPILL_REFS => "Wasm spill refs",
        Phases.PHASE_WASM_VIRTUAL_IP => "Wasm virtual IP",
        Phases.PHASE_ASYNC => "Transform async",
        Phases.PHASE_LCLVARLIVENESS => "Local var liveness",
        Phases.PHASE_LCLVARLIVENESS_INIT => "Local var liveness init",
        Phases.PHASE_LCLVARLIVENESS_PERBLOCK => "Per block local var liveness",
        Phases.PHASE_LCLVARLIVENESS_INTERBLOCK => "Global local var liveness",
        Phases.PHASE_LOWERING_DECOMP => "Lowering decomposition",
        Phases.PHASE_LOWERING => "Lowering nodeinfo",
        Phases.PHASE_STACK_LEVEL_SETTER => "Calculate stack level slots",
        Phases.PHASE_LINEAR_SCAN => "Linear scan register alloc",
        Phases.PHASE_LINEAR_SCAN_BUILD => "LSRA build intervals",
        Phases.PHASE_LINEAR_SCAN_ALLOC => "LSRA allocate",
        Phases.PHASE_LINEAR_SCAN_RESOLVE => "LSRA resolve",
        Phases.PHASE_ALIGN_LOOPS => "Place 'align' instructions",
        Phases.PHASE_GENERATE_CODE => "Generate code",
        Phases.PHASE_EMIT_CODE => "Emit code",
        Phases.PHASE_EMIT_GCEH => "Emit GC+EH tables",
        Phases.PHASE_POST_EMIT => "Post-Emit",
        Phases.PHASE_CLR_API => "CLR API calls",
        _ => throw new FatalJitException("Invalid compiler phase."),
    };

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