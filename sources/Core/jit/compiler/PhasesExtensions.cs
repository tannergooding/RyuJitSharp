// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_JIT_METHOD_PERF || DUMP_FLOWGRAPHS
#endif

using System;

namespace RyuJitSharp;

public static class PhasesExtensions
{
#if FEATURE_JIT_METHOD_PERF || DUMP_FLOWGRAPHS
    private static readonly string[] s_names = [
        "Pre-import",                                               // PHASE_PRE_IMPORT
        "Importation",                                              // PHASE_IMPORTATION
        "Indirect call transform",                                  // PHASE_INDXCALL
        "Expand patchpoints",                                       // PHASE_PATCHPOINTS
        "Post-import",                                              // PHASE_POST_IMPORT
        "Save contexts around async calls",                         // PHASE_ASYNC_SAVE_CONTEXTS
        "Profile instrumentation prep",                             // PHASE_IBCPREP
        "Profile instrumentation",                                  // PHASE_IBCINSTR
        "Profile incorporation",                                    // PHASE_INCPROFILE
        "Post-inline no-return cleanup",                            // PHASE_POST_INLINE_NORETURN
        "Resolve GDV Checks",                                       // PHASE_RESOLVE_GDVS
        "Morph - Init",                                             // PHASE_MORPH_INIT
        "Morph - Inlining",                                         // PHASE_MORPH_INLINE
        "Morph - Add internal blocks",                              // PHASE_MORPH_ADD_INTERNAL
        "Add Swift error returns",                                  // PHASE_SWIFT_ERROR_RET
        "Allocate Objects",                                         // PHASE_ALLOCATE_OBJECTS
        "Remove empty try",                                         // PHASE_EMPTY_TRY
        "Remove empty try/catch/fault",                             // PHASE_EMPTY_TRY_CATCH_FAULT
        "Remove empty finally",                                     // PHASE_EMPTY_FINALLY
        "Merge callfinally chains",                                 // PHASE_MERGE_FINALLY_CHAINS
        "Clone finally",                                            // PHASE_CLONE_FINALLY
        "Update finally target flags",                              // PHASE_UPDATE_FINALLY_FLAGS
        "Update flow graph early pass",                             // PHASE_EARLY_UPDATE_FLOW_GRAPH
        "DFS blocks and remove dead code 1",                        // PHASE_DFS_BLOCKS1
        "DFS blocks and remove dead code 2",                        // PHASE_DFS_BLOCKS2
        "DFS blocks and remove dead code 3",                        // PHASE_DFS_BLOCKS3
        "Local morph",                                              // PHASE_LOCAL_MORPH
        "Optimize mask conversions",                                // PHASE_OPTIMIZE_MASK_CONVERSIONS
        "Early liveness",                                           // PHASE_EARLY_LIVENESS
        "Physical promotion",                                       // PHASE_PHYSICAL_PROMOTION
        "Forward Substitution",                                     // PHASE_FWD_SUB
        "Identify candidates for implicit byref copy omission",     // PHASE_IMPBYREF_COPY_OMISSION
        "Morph - ByRefs",                                           // PHASE_MORPH_IMPBYREF
        "Morph - Promote Structs",                                  // PHASE_PROMOTE_STRUCTS
        "Morph - Global",                                           // PHASE_MORPH_GLOBAL
        "Post-Morph",                                               // PHASE_POST_MORPH
        "Morph - Finish",                                           // PHASE_MORPH_END
        "GS Cookie",                                                // PHASE_GS_COOKIE
        "Compute block weights",                                    // PHASE_COMPUTE_BLOCK_WEIGHTS
        "Create EH funclets",                                       // PHASE_CREATE_FUNCLETS
        "Head and tail merge",                                      // PHASE_HEAD_TAIL_MERGE
        "Early QMARK expansion",                                    // PHASE_EARLY_QMARK_EXPANSION
        "Merge throw blocks",                                       // PHASE_MERGE_THROWS
        "Invert loops",                                             // PHASE_INVERT_LOOPS
        "Post-morph head and tail merge",                           // PHASE_HEAD_TAIL_MERGE2
        "Optimize control flow",                                    // PHASE_OPTIMIZE_FLOW
        "Optimize pre-layout",                                      // PHASE_OPTIMIZE_PRE_LAYOUT
        "Optimize layout",                                          // PHASE_OPTIMIZE_LAYOUT
        "Optimize post-layout",                                     // PHASE_OPTIMIZE_POST_LAYOUT
        "Compute dominators",                                       // PHASE_COMPUTE_DOMINATORS
        "Canonicalize entry",                                       // PHASE_CANONICALIZE_ENTRY
        "Set block weights",                                        // PHASE_SET_BLOCK_WEIGHTS
        "Redundant zero Inits",                                     // PHASE_ZERO_INITS
        "Adjust throw edge likelihoods",                            // PHASE_ADJUST_THROW_LIKELIHOODS
        "Find loops",                                               // PHASE_FIND_LOOPS
        "Clone loops",                                              // PHASE_CLONE_LOOPS
        "Unroll loops",                                             // PHASE_UNROLL_LOOPS
        "Morph array ops",                                          // PHASE_MORPH_MDARR
        "Remove empty finally 2",                                   // PHASE_EMPTY_FINALLY_2
        "Remove empty try 2",                                       // PHASE_EMPTY_TRY_2
        "Remove empty try-catch-fault 2",                           // PHASE_EMPTY_TRY_CATCH_FAULT_2
        "Hoist loop code",                                          // PHASE_HOIST_LOOP_CODE
        "Mark local vars",                                          // PHASE_MARK_LOCAL_VARS
        "Optimize bools",                                           // PHASE_OPTIMIZE_BOOLS
        "Recognize Switch",                                         // PHASE_SWITCH_RECOGNITION
        "Find oper order",                                          // PHASE_FIND_OPER_ORDER
        "Set block order",                                          // PHASE_SET_BLOCK_ORDER
        "Build SSA representation",                                 // PHASE_BUILD_SSA
        "SSA: liveness",                                            // PHASE_BUILD_SSA_LIVENESS
        "SSA: DF",                                                  // PHASE_BUILD_SSA_DF
        "SSA: insert phis",                                         // PHASE_BUILD_SSA_INSERT_PHIS
        "SSA: rename",                                              // PHASE_BUILD_SSA_RENAME
        "Early Value Propagation",                                  // PHASE_EARLY_PROP
        "Optimize Induction Variables",                             // PHASE_OPTIMIZE_INDUCTION_VARIABLES
        "Do value numbering",                                       // PHASE_VALUE_NUMBER
        "Optimize index checks",                                    // PHASE_OPTIMIZE_INDEX_CHECKS
        "Optimize Valnum CSEs",                                     // PHASE_OPTIMIZE_VALNUM_CSES
        "VN based copy prop",                                       // PHASE_VN_COPY_PROP
        "VN based intrinsic expansion",                             // PHASE_VN_BASED_INTRINSIC_EXPAND
        "Redundant branch opts",                                    // PHASE_OPTIMIZE_BRANCHES
        "Coalesce bounds checks",                                   // PHASE_BOUNDS_CHECK_COALESCE
        "Assertion prop",                                           // PHASE_ASSERTION_PROP_MAIN
        "Clone blocks with range checks",                           // PHASE_RANGE_CHECK_CLONING
        "If conversion",                                            // PHASE_IF_CONVERSION
        "VN-based dead store removal",                              // PHASE_VN_BASED_DEAD_STORE_REMOVAL
        "Remove empty finally 3",                                   // PHASE_EMPTY_FINALLY_3
        "Remove empty try 3",                                       // PHASE_EMPTY_TRY_3
        "Remove empty try-catch-fault 3",                           // PHASE_EMPTY_TRY_CATCH_FAULT_3
        "Update flow graph opt pass",                               // PHASE_OPT_UPDATE_FLOW_GRAPH
        "Remove unreachable blocks",                                // PHASE_OPT_DFS_BLOCKS
        "Stress gtSplitTree",                                       // PHASE_STRESS_SPLIT_TREE
        "Expand runtime lookups",                                   // PHASE_EXPAND_RTLOOKUPS
        "Expand static init",                                       // PHASE_EXPAND_STATIC_INIT
        "Expand casts",                                             // PHASE_EXPAND_CASTS
        "Expand TLS access",                                        // PHASE_EXPAND_TLS
        "Expand stack array allocation",                            // PHASE_EXPAND_STACK_ARR
        "Insert GC Polls",                                          // PHASE_INSERT_GC_POLLS
        "Create throw helper blocks",                               // PHASE_CREATE_THROW_HELPERS
        "Determine first cold block",                               // PHASE_DETERMINE_FIRST_COLD_BLOCK
        "Rationalize IR",                                           // PHASE_RATIONALIZE
        "Repair profile post-morph",                                // PHASE_REPAIR_PROFILE_POST_MORPH
        "Repair profile pre-layout",                                // PHASE_REPAIR_PROFILE_PRE_LAYOUT
        "Wasm remove unreachable blocks",                           // PHASE_DFS_BLOCKS_WASM
        "Wasm eh control flow",                                     // PHASE_WASM_EH_FLOW
        "Wasm transform sccs",                                      // PHASE_WASM_TRANSFORM_SCCS
        "Wasm control flow",                                        // PHASE_WASM_CONTROL_FLOW
        "Wasm virtual IP",                                          // PHASE_WASM_VIRTUAL_IP
        "Transform async",                                          // PHASE_ASYNC
        "Local var liveness",                                       // PHASE_LCLVARLIVENESS
        "Local var liveness init",                                  // PHASE_LCLVARLIVENESS_INIT
        "Per block local var liveness",                             // PHASE_LCLVARLIVENESS_PERBLOCK
        "Global local var liveness",                                // PHASE_LCLVARLIVENESS_INTERBLOCK
        "Lowering decomposition",                                   // PHASE_LOWERING_DECOMP
        "Lowering nodeinfo",                                        // PHASE_LOWERING
        "Calculate stack level slots",                              // PHASE_STACK_LEVEL_SETTER
        "Linear scan register alloc",                               // PHASE_LINEAR_SCAN
        "LSRA build intervals",                                     // PHASE_LINEAR_SCAN_BUILD
        "LSRA allocate",                                            // PHASE_LINEAR_SCAN_ALLOC
        "LSRA resolve",                                             // PHASE_LINEAR_SCAN_RESOLVE
        "Place 'align' instructions",                               // PHASE_ALIGN_LOOPS
        "Generate code",                                            // PHASE_GENERATE_CODE
        "Emit code",                                                // PHASE_EMIT_CODE
        "Emit GC+EH tables",                                        // PHASE_EMIT_GCEH
        "Post-Emit",                                                // PHASE_POST_EMIT

#if MEASURE_CLRAPI_CALLS
        "CLR API calls",                                            // PHASE_CLR_API
#endif
    ];
#endif

#if FEATURE_JIT_METHOD_PERF
    public static ReadOnlySpan<bool> s_hasChildren => [
        false,      // PHASE_PRE_IMPORT
        false,      // PHASE_IMPORTATION
        false,      // PHASE_INDXCALL
        false,      // PHASE_PATCHPOINTS
        false,      // PHASE_POST_IMPORT
        false,      // PHASE_ASYNC_SAVE_CONTEXTS
        false,      // PHASE_IBCPREP
        false,      // PHASE_IBCINSTR
        false,      // PHASE_INCPROFILE
        false,      // PHASE_POST_INLINE_NORETURN
        false,      // PHASE_RESOLVE_GDVS
        false,      // PHASE_MORPH_INIT
        false,      // PHASE_MORPH_INLINE
        false,      // PHASE_MORPH_ADD_INTERNAL
        false,      // PHASE_SWIFT_ERROR_RET
        false,      // PHASE_ALLOCATE_OBJECTS
        false,      // PHASE_EMPTY_TRY
        false,      // PHASE_EMPTY_TRY_CATCH_FAULT
        false,      // PHASE_EMPTY_FINALLY
        false,      // PHASE_MERGE_FINALLY_CHAINS
        false,      // PHASE_CLONE_FINALLY
        false,      // PHASE_UPDATE_FINALLY_FLAGS
        false,      // PHASE_EARLY_UPDATE_FLOW_GRAPH
        false,      // PHASE_DFS_BLOCKS1
        false,      // PHASE_DFS_BLOCKS2
        false,      // PHASE_DFS_BLOCKS3
        false,      // PHASE_LOCAL_MORPH
        false,      // PHASE_OPTIMIZE_MASK_CONVERSIONS
        false,      // PHASE_EARLY_LIVENESS
        false,      // PHASE_PHYSICAL_PROMOTION
        false,      // PHASE_FWD_SUB
        false,      // PHASE_IMPBYREF_COPY_OMISSION
        false,      // PHASE_MORPH_IMPBYREF
        false,      // PHASE_PROMOTE_STRUCTS
        false,      // PHASE_MORPH_GLOBAL
        false,      // PHASE_POST_MORPH
        false,      // PHASE_MORPH_END
        false,      // PHASE_GS_COOKIE
        false,      // PHASE_COMPUTE_BLOCK_WEIGHTS
        false,      // PHASE_CREATE_FUNCLETS
        false,      // PHASE_HEAD_TAIL_MERGE
        false,      // PHASE_EARLY_QMARK_EXPANSION
        false,      // PHASE_MERGE_THROWS
        false,      // PHASE_INVERT_LOOPS
        false,      // PHASE_HEAD_TAIL_MERGE2
        false,      // PHASE_OPTIMIZE_FLOW
        false,      // PHASE_OPTIMIZE_PRE_LAYOUT
        false,      // PHASE_OPTIMIZE_LAYOUT
        false,      // PHASE_OPTIMIZE_POST_LAYOUT
        false,      // PHASE_COMPUTE_DOMINATORS
        false,      // PHASE_CANONICALIZE_ENTRY
        false,      // PHASE_SET_BLOCK_WEIGHTS
        false,      // PHASE_ZERO_INITS
        false,      // PHASE_ADJUST_THROW_LIKELIHOODS
        false,      // PHASE_FIND_LOOPS
        false,      // PHASE_CLONE_LOOPS
        false,      // PHASE_UNROLL_LOOPS
        false,      // PHASE_MORPH_MDARR
        false,      // PHASE_EMPTY_FINALLY_2
        false,      // PHASE_EMPTY_TRY_2
        false,      // PHASE_EMPTY_TRY_CATCH_FAULT_2
        false,      // PHASE_HOIST_LOOP_CODE
        false,      // PHASE_MARK_LOCAL_VARS
        false,      // PHASE_OPTIMIZE_BOOLS
        false,      // PHASE_SWITCH_RECOGNITION
        false,      // PHASE_FIND_OPER_ORDER
        false,      // PHASE_SET_BLOCK_ORDER
        true,       // PHASE_BUILD_SSA
        false,      // PHASE_BUILD_SSA_LIVENESS
        false,      // PHASE_BUILD_SSA_DF
        false,      // PHASE_BUILD_SSA_INSERT_PHIS
        false,      // PHASE_BUILD_SSA_RENAME
        false,      // PHASE_EARLY_PROP
        false,      // PHASE_OPTIMIZE_INDUCTION_VARIABLES
        false,      // PHASE_VALUE_NUMBER
        false,      // PHASE_OPTIMIZE_INDEX_CHECKS
        false,      // PHASE_OPTIMIZE_VALNUM_CSES
        false,      // PHASE_VN_COPY_PROP
        false,      // PHASE_VN_BASED_INTRINSIC_EXPAND
        false,      // PHASE_OPTIMIZE_BRANCHES
        false,      // PHASE_BOUNDS_CHECK_COALESCE
        false,      // PHASE_ASSERTION_PROP_MAIN
        false,      // PHASE_RANGE_CHECK_CLONING
        false,      // PHASE_IF_CONVERSION
        false,      // PHASE_VN_BASED_DEAD_STORE_REMOVAL
        false,      // PHASE_EMPTY_FINALLY_3
        false,      // PHASE_EMPTY_TRY_3
        false,      // PHASE_EMPTY_TRY_CATCH_FAULT_3
        false,      // PHASE_OPT_UPDATE_FLOW_GRAPH
        false,      // PHASE_OPT_DFS_BLOCKS
        false,      // PHASE_STRESS_SPLIT_TREE
        false,      // PHASE_EXPAND_RTLOOKUPS
        false,      // PHASE_EXPAND_STATIC_INIT
        false,      // PHASE_EXPAND_CASTS
        false,      // PHASE_EXPAND_TLS
        false,      // PHASE_EXPAND_STACK_ARR
        false,      // PHASE_INSERT_GC_POLLS
        false,      // PHASE_CREATE_THROW_HELPERS
        false,      // PHASE_DETERMINE_FIRST_COLD_BLOCK
        false,      // PHASE_RATIONALIZE
        false,      // PHASE_REPAIR_PROFILE_POST_MORPH
        false,      // PHASE_REPAIR_PROFILE_PRE_LAYOUT
        false,      // PHASE_DFS_BLOCKS_WASM
        false,      // PHASE_WASM_EH_FLOW
        false,      // PHASE_WASM_TRANSFORM_SCCS
        false,      // PHASE_WASM_CONTROL_FLOW
        false,      // PHASE_WASM_VIRTUAL_IP
        false,      // PHASE_ASYNC
        true,       // PHASE_LCLVARLIVENESS
        false,      // PHASE_LCLVARLIVENESS_INIT
        false,      // PHASE_LCLVARLIVENESS_PERBLOCK
        false,      // PHASE_LCLVARLIVENESS_INTERBLOCK
        false,      // PHASE_LOWERING_DECOMP
        false,      // PHASE_LOWERING
        false,      // PHASE_STACK_LEVEL_SETTER
        true,       // PHASE_LINEAR_SCAN
        false,      // PHASE_LINEAR_SCAN_BUILD
        false,      // PHASE_LINEAR_SCAN_ALLOC
        false,      // PHASE_LINEAR_SCAN_RESOLVE
        false,      // PHASE_ALIGN_LOOPS
        false,      // PHASE_GENERATE_CODE
        false,      // PHASE_EMIT_CODE
        false,      // PHASE_EMIT_GCEH
        false,      // PHASE_POST_EMIT
#if MEASURE_CLRAPI_CALLS
        false,      // PHASE_CLR_API
#endif
    ];

    public static ReadOnlySpan<Phases> s_parents => [
        (Phases)(-1),           // PHASE_PRE_IMPORT
        (Phases)(-1),           // PHASE_IMPORTATION
        (Phases)(-1),           // PHASE_INDXCALL
        (Phases)(-1),           // PHASE_PATCHPOINTS
        (Phases)(-1),           // PHASE_POST_IMPORT
        (Phases)(-1),           // PHASE_ASYNC_SAVE_CONTEXTS
        (Phases)(-1),           // PHASE_IBCPREP
        (Phases)(-1),           // PHASE_IBCINSTR
        (Phases)(-1),           // PHASE_INCPROFILE
        (Phases)(-1),           // PHASE_POST_INLINE_NORETURN
        (Phases)(-1),           // PHASE_RESOLVE_GDVS
        (Phases)(-1),           // PHASE_MORPH_INIT
        (Phases)(-1),           // PHASE_MORPH_INLINE
        (Phases)(-1),           // PHASE_MORPH_ADD_INTERNAL
        (Phases)(-1),           // PHASE_SWIFT_ERROR_RET
        (Phases)(-1),           // PHASE_ALLOCATE_OBJECTS
        (Phases)(-1),           // PHASE_EMPTY_TRY
        (Phases)(-1),           // PHASE_EMPTY_TRY_CATCH_FAULT
        (Phases)(-1),           // PHASE_EMPTY_FINALLY
        (Phases)(-1),           // PHASE_MERGE_FINALLY_CHAINS
        (Phases)(-1),           // PHASE_CLONE_FINALLY
        (Phases)(-1),           // PHASE_UPDATE_FINALLY_FLAGS
        (Phases)(-1),           // PHASE_EARLY_UPDATE_FLOW_GRAPH
        (Phases)(-1),           // PHASE_DFS_BLOCKS1
        (Phases)(-1),           // PHASE_DFS_BLOCKS2
        (Phases)(-1),           // PHASE_DFS_BLOCKS3
        (Phases)(-1),           // PHASE_LOCAL_MORPH
        (Phases)(-1),           // PHASE_OPTIMIZE_MASK_CONVERSIONS
        (Phases)(-1),           // PHASE_EARLY_LIVENESS
        (Phases)(-1),           // PHASE_PHYSICAL_PROMOTION
        (Phases)(-1),           // PHASE_FWD_SUB
        (Phases)(-1),           // PHASE_IMPBYREF_COPY_OMISSION
        (Phases)(-1),           // PHASE_MORPH_IMPBYREF
        (Phases)(-1),           // PHASE_PROMOTE_STRUCTS
        (Phases)(-1),           // PHASE_MORPH_GLOBAL
        (Phases)(-1),           // PHASE_POST_MORPH
        (Phases)(-1),           // PHASE_MORPH_END
        (Phases)(-1),           // PHASE_GS_COOKIE
        (Phases)(-1),           // PHASE_COMPUTE_BLOCK_WEIGHTS
        (Phases)(-1),           // PHASE_CREATE_FUNCLETS
        (Phases)(-1),           // PHASE_HEAD_TAIL_MERGE
        (Phases)(-1),           // PHASE_EARLY_QMARK_EXPANSION
        (Phases)(-1),           // PHASE_MERGE_THROWS
        (Phases)(-1),           // PHASE_INVERT_LOOPS
        (Phases)(-1),           // PHASE_HEAD_TAIL_MERGE2
        (Phases)(-1),           // PHASE_OPTIMIZE_FLOW
        (Phases)(-1),           // PHASE_OPTIMIZE_PRE_LAYOUT
        (Phases)(-1),           // PHASE_OPTIMIZE_LAYOUT
        (Phases)(-1),           // PHASE_OPTIMIZE_POST_LAYOUT
        (Phases)(-1),           // PHASE_COMPUTE_DOMINATORS
        (Phases)(-1),           // PHASE_CANONICALIZE_ENTRY
        (Phases)(-1),           // PHASE_SET_BLOCK_WEIGHTS
        (Phases)(-1),           // PHASE_ZERO_INITS
        (Phases)(-1),           // PHASE_ADJUST_THROW_LIKELIHOODS
        (Phases)(-1),           // PHASE_FIND_LOOPS
        (Phases)(-1),           // PHASE_CLONE_LOOPS
        (Phases)(-1),           // PHASE_UNROLL_LOOPS
        (Phases)(-1),           // PHASE_MORPH_MDARR
        (Phases)(-1),           // PHASE_EMPTY_FINALLY_2
        (Phases)(-1),           // PHASE_EMPTY_TRY_2
        (Phases)(-1),           // PHASE_EMPTY_TRY_CATCH_FAULT_2
        (Phases)(-1),           // PHASE_HOIST_LOOP_CODE
        (Phases)(-1),           // PHASE_MARK_LOCAL_VARS
        (Phases)(-1),           // PHASE_OPTIMIZE_BOOLS
        (Phases)(-1),           // PHASE_SWITCH_RECOGNITION
        (Phases)(-1),           // PHASE_FIND_OPER_ORDER
        (Phases)(-1),           // PHASE_SET_BLOCK_ORDER
        (Phases)(-1),           // PHASE_BUILD_SSA
        PHASE_BUILD_SSA,        // PHASE_BUILD_SSA_LIVENESS
        PHASE_BUILD_SSA,        // PHASE_BUILD_SSA_DF
        PHASE_BUILD_SSA,        // PHASE_BUILD_SSA_INSERT_PHIS
        PHASE_BUILD_SSA,        // PHASE_BUILD_SSA_RENAME
        (Phases)(-1),           // PHASE_EARLY_PROP
        (Phases)(-1),           // PHASE_OPTIMIZE_INDUCTION_VARIABLES
        (Phases)(-1),           // PHASE_VALUE_NUMBER
        (Phases)(-1),           // PHASE_OPTIMIZE_INDEX_CHECKS
        (Phases)(-1),           // PHASE_OPTIMIZE_VALNUM_CSES
        (Phases)(-1),           // PHASE_VN_COPY_PROP
        (Phases)(-1),           // PHASE_VN_BASED_INTRINSIC_EXPAND
        (Phases)(-1),           // PHASE_OPTIMIZE_BRANCHES
        (Phases)(-1),           // PHASE_BOUNDS_CHECK_COALESCE
        (Phases)(-1),           // PHASE_ASSERTION_PROP_MAIN
        (Phases)(-1),           // PHASE_RANGE_CHECK_CLONING
        (Phases)(-1),           // PHASE_IF_CONVERSION
        (Phases)(-1),           // PHASE_VN_BASED_DEAD_STORE_REMOVAL
        (Phases)(-1),           // PHASE_EMPTY_FINALLY_3
        (Phases)(-1),           // PHASE_EMPTY_TRY_3
        (Phases)(-1),           // PHASE_EMPTY_TRY_CATCH_FAULT_3
        (Phases)(-1),           // PHASE_OPT_UPDATE_FLOW_GRAPH
        (Phases)(-1),           // PHASE_OPT_DFS_BLOCKS
        (Phases)(-1),           // PHASE_STRESS_SPLIT_TREE
        (Phases)(-1),           // PHASE_EXPAND_RTLOOKUPS
        (Phases)(-1),           // PHASE_EXPAND_STATIC_INIT
        (Phases)(-1),           // PHASE_EXPAND_CASTS
        (Phases)(-1),           // PHASE_EXPAND_TLS
        (Phases)(-1),           // PHASE_EXPAND_STACK_ARR
        (Phases)(-1),           // PHASE_INSERT_GC_POLLS
        (Phases)(-1),           // PHASE_CREATE_THROW_HELPERS
        (Phases)(-1),           // PHASE_DETERMINE_FIRST_COLD_BLOCK
        (Phases)(-1),           // PHASE_RATIONALIZE
        (Phases)(-1),           // PHASE_REPAIR_PROFILE_POST_MORPH
        (Phases)(-1),           // PHASE_REPAIR_PROFILE_PRE_LAYOUT
        (Phases)(-1),           // PHASE_DFS_BLOCKS_WASM
        (Phases)(-1),           // PHASE_WASM_EH_FLOW
        (Phases)(-1),           // PHASE_WASM_TRANSFORM_SCCS
        (Phases)(-1),           // PHASE_WASM_CONTROL_FLOW
        (Phases)(-1),           // PHASE_WASM_VIRTUAL_IP
        (Phases)(-1),           // PHASE_ASYNC
        (Phases)(-1),           // PHASE_LCLVARLIVENESS
        PHASE_LCLVARLIVENESS,   // PHASE_LCLVARLIVENESS_INIT
        PHASE_LCLVARLIVENESS,   // PHASE_LCLVARLIVENESS_PERBLOCK
        PHASE_LCLVARLIVENESS,   // PHASE_LCLVARLIVENESS_INTERBLOCK
        (Phases)(-1),           // PHASE_LOWERING_DECOMP
        (Phases)(-1),           // PHASE_LOWERING
        (Phases)(-1),           // PHASE_STACK_LEVEL_SETTER
        (Phases)(-1),           // PHASE_LINEAR_SCAN
        PHASE_LINEAR_SCAN,      // PHASE_LINEAR_SCAN_BUILD
        PHASE_LINEAR_SCAN,      // PHASE_LINEAR_SCAN_ALLOC
        PHASE_LINEAR_SCAN,      // PHASE_LINEAR_SCAN_RESOLVE
        (Phases)(-1),           // PHASE_ALIGN_LOOPS
        (Phases)(-1),           // PHASE_GENERATE_CODE
        (Phases)(-1),           // PHASE_EMIT_CODE
        (Phases)(-1),           // PHASE_EMIT_GCEH
        (Phases)(-1),           // PHASE_POST_EMIT
#if MEASURE_CLRAPI_CALLS
        (Phases)(-1),           // PHASE_CLR_API
#endif
    ];

    public static ReadOnlySpan<bool> s_reportsIRSize => [
        false,      // PHASE_PRE_IMPORT
        true,       // PHASE_IMPORTATION
        true,       // PHASE_INDXCALL
        true,       // PHASE_PATCHPOINTS
        false,      // PHASE_POST_IMPORT
        false,      // PHASE_ASYNC_SAVE_CONTEXTS
        false,      // PHASE_IBCPREP
        false,      // PHASE_IBCINSTR
        false,      // PHASE_INCPROFILE
        false,      // PHASE_POST_INLINE_NORETURN
        false,      // PHASE_RESOLVE_GDVS
        false,      // PHASE_MORPH_INIT
        true,       // PHASE_MORPH_INLINE
        true,       // PHASE_MORPH_ADD_INTERNAL
        true,       // PHASE_SWIFT_ERROR_RET
        false,      // PHASE_ALLOCATE_OBJECTS
        false,      // PHASE_EMPTY_TRY
        false,      // PHASE_EMPTY_TRY_CATCH_FAULT
        false,      // PHASE_EMPTY_FINALLY
        false,      // PHASE_MERGE_FINALLY_CHAINS
        false,      // PHASE_CLONE_FINALLY
        false,      // PHASE_UPDATE_FINALLY_FLAGS
        false,      // PHASE_EARLY_UPDATE_FLOW_GRAPH
        false,      // PHASE_DFS_BLOCKS1
        false,      // PHASE_DFS_BLOCKS2
        false,      // PHASE_DFS_BLOCKS3
        false,      // PHASE_LOCAL_MORPH
        false,      // PHASE_OPTIMIZE_MASK_CONVERSIONS
        false,      // PHASE_EARLY_LIVENESS
        false,      // PHASE_PHYSICAL_PROMOTION
        false,      // PHASE_FWD_SUB
        false,      // PHASE_IMPBYREF_COPY_OMISSION
        false,      // PHASE_MORPH_IMPBYREF
        false,      // PHASE_PROMOTE_STRUCTS
        false,      // PHASE_MORPH_GLOBAL
        false,      // PHASE_POST_MORPH
        true,       // PHASE_MORPH_END
        false,      // PHASE_GS_COOKIE
        false,      // PHASE_COMPUTE_BLOCK_WEIGHTS
        false,      // PHASE_CREATE_FUNCLETS
        false,      // PHASE_HEAD_TAIL_MERGE
        false,      // PHASE_EARLY_QMARK_EXPANSION
        false,      // PHASE_MERGE_THROWS
        false,      // PHASE_INVERT_LOOPS
        false,      // PHASE_HEAD_TAIL_MERGE2
        false,      // PHASE_OPTIMIZE_FLOW
        false,      // PHASE_OPTIMIZE_PRE_LAYOUT
        false,      // PHASE_OPTIMIZE_LAYOUT
        false,      // PHASE_OPTIMIZE_POST_LAYOUT
        false,      // PHASE_COMPUTE_DOMINATORS
        false,      // PHASE_CANONICALIZE_ENTRY
        false,      // PHASE_SET_BLOCK_WEIGHTS
        false,      // PHASE_ZERO_INITS
        false,      // PHASE_ADJUST_THROW_LIKELIHOODS
        false,      // PHASE_FIND_LOOPS
        false,      // PHASE_CLONE_LOOPS
        false,      // PHASE_UNROLL_LOOPS
        false,      // PHASE_MORPH_MDARR
        false,      // PHASE_EMPTY_FINALLY_2
        false,      // PHASE_EMPTY_TRY_2
        false,      // PHASE_EMPTY_TRY_CATCH_FAULT_2
        false,      // PHASE_HOIST_LOOP_CODE
        false,      // PHASE_MARK_LOCAL_VARS
        false,      // PHASE_OPTIMIZE_BOOLS
        false,      // PHASE_SWITCH_RECOGNITION
        false,      // PHASE_FIND_OPER_ORDER
        true,       // PHASE_SET_BLOCK_ORDER
        false,      // PHASE_BUILD_SSA
        false,      // PHASE_BUILD_SSA_LIVENESS
        false,      // PHASE_BUILD_SSA_DF
        false,      // PHASE_BUILD_SSA_INSERT_PHIS
        false,      // PHASE_BUILD_SSA_RENAME
        false,      // PHASE_EARLY_PROP
        false,      // PHASE_OPTIMIZE_INDUCTION_VARIABLES
        false,      // PHASE_VALUE_NUMBER
        false,      // PHASE_OPTIMIZE_INDEX_CHECKS
        false,      // PHASE_OPTIMIZE_VALNUM_CSES
        false,      // PHASE_VN_COPY_PROP
        false,      // PHASE_VN_BASED_INTRINSIC_EXPAND
        false,      // PHASE_OPTIMIZE_BRANCHES
        false,      // PHASE_BOUNDS_CHECK_COALESCE
        false,      // PHASE_ASSERTION_PROP_MAIN
        false,      // PHASE_RANGE_CHECK_CLONING
        false,      // PHASE_IF_CONVERSION
        false,      // PHASE_VN_BASED_DEAD_STORE_REMOVAL
        false,      // PHASE_EMPTY_FINALLY_3
        false,      // PHASE_EMPTY_TRY_3
        false,      // PHASE_EMPTY_TRY_CATCH_FAULT_3
        false,      // PHASE_OPT_UPDATE_FLOW_GRAPH
        false,      // PHASE_OPT_DFS_BLOCKS
        false,      // PHASE_STRESS_SPLIT_TREE
        true,       // PHASE_EXPAND_RTLOOKUPS
        true,       // PHASE_EXPAND_STATIC_INIT
        true,       // PHASE_EXPAND_CASTS
        true,       // PHASE_EXPAND_TLS
        true,       // PHASE_EXPAND_STACK_ARR
        true,       // PHASE_INSERT_GC_POLLS
        true,       // PHASE_CREATE_THROW_HELPERS
        true,       // PHASE_DETERMINE_FIRST_COLD_BLOCK
        false,      // PHASE_RATIONALIZE
        false,      // PHASE_REPAIR_PROFILE_POST_MORPH
        false,      // PHASE_REPAIR_PROFILE_PRE_LAYOUT
        false,      // PHASE_DFS_BLOCKS_WASM
        false,      // PHASE_WASM_EH_FLOW
        false,      // PHASE_WASM_TRANSFORM_SCCS
        false,      // PHASE_WASM_CONTROL_FLOW
        false,      // PHASE_WASM_VIRTUAL_IP
        true,       // PHASE_ASYNC
        false,      // PHASE_LCLVARLIVENESS
        false,      // PHASE_LCLVARLIVENESS_INIT
        false,      // PHASE_LCLVARLIVENESS_PERBLOCK
        false,      // PHASE_LCLVARLIVENESS_INTERBLOCK
        false,      // PHASE_LOWERING_DECOMP
        true,       // PHASE_LOWERING
        false,      // PHASE_STACK_LEVEL_SETTER
        true,       // PHASE_LINEAR_SCAN
        false,      // PHASE_LINEAR_SCAN_BUILD
        false,      // PHASE_LINEAR_SCAN_ALLOC
        false,      // PHASE_LINEAR_SCAN_RESOLVE
        false,      // PHASE_ALIGN_LOOPS
        false,      // PHASE_GENERATE_CODE
        false,      // PHASE_EMIT_CODE
        false,      // PHASE_EMIT_GCEH
        false,      // PHASE_POST_EMIT
#if MEASURE_CLRAPI_CALLS
        false,      // PHASE_CLR_API
#endif
    ];
#endif

    extension(Phases phase)
    {
#if FEATURE_JIT_METHOD_PERF || DUMP_FLOWGRAPHS
        public string Name
        {
            get
            {
                assert(s_names.Length == (int)(PHASE_NUMBER_OF));
                return s_names[(int)(phase)];
            }
        }
#else
        public string Name => phase.ToString();
#endif

#if FEATURE_JIT_METHOD_PERF
        public bool HasChildren
        {
            get
            {
                assert(s_hasChildren.Length == (int)(PHASE_NUMBER_OF));
                return s_hasChildren[(int)(phase)];
            }
        }

        public Phases Parent
        {
            get
            {
                assert(s_parents.Length == (int)(PHASE_NUMBER_OF));
                return s_parents[(int)(phase)];
            }
        }

        public bool ReportsIRSize
        {
            get
            {
                assert(s_reportsIRSize.Length == (int)(PHASE_NUMBER_OF));
                return s_reportsIRSize[(int)(phase)];
            }
        }
#endif
    }
}
