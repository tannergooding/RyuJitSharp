// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_JIT_METHOD_PERF || DUMP_FLOWGRAPHS
#endif

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
    }
}
