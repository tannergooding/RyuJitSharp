// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT,. See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c, .NET Foundation and Contributors. Licensed under the MIT License (MIT,.

namespace RyuJitSharp;

public partial class Compiler
{
    public enum compStressArea
    {
        STRESS_NONE,

        // "Variations" stress areas which we try to mix up with each other.               
        // These should not be exhaustively used as they might hide/trivialize other areas

        STRESS_REGS,

        STRESS_DBL_ALN,

        STRESS_LCL_FLDS,

        STRESS_UNROLL_LOOPS,

        STRESS_MAKE_CSE,

        STRESS_LEGACY_INLINE,

        STRESS_CLONE_EXPR,

        STRESS_FOLD,

        STRESS_MERGED_RETURNS,

        STRESS_BB_PROFILE,

        STRESS_OPT_BOOLS_GC,

        STRESS_OPT_BOOLS_COMPARE_CHAIN_COST,

        STRESS_REMORPH_TREES,

        STRESS_64RSLT_MUL,

        STRESS_DO_WHILE_LOOPS,

        STRESS_MIN_OPTS,

        // <summary>Will set GTF_REVERSE_OPS whenever we can</summary>
        STRESS_REVERSE_FLAG,

        // <summary>Will make the call as a tailcall whenever legal</summary>
        STRESS_TAILCALL,

        // <summary>Will spill catch arg</summary>
        STRESS_CATCH_ARG,

        STRESS_UNSAFE_BUFFER_CHECKS,

        STRESS_NULL_OBJECT_CHECK,

        STRESS_RANDOM_INLINE,

        STRESS_SWITCH_CMP_BR_EXPANSION,

        STRESS_GENERIC_VARN,

        /// <summary>Will generate profiler hooks for ELT callbacks</summary>
        STRESS_PROFILER_CALLBACKS,

        /// <summary>Change undoPromotion decisions for byrefs</summary>
        STRESS_BYREF_PROMOTION,

        /// <summary>Don't promote some structs that can be promoted</summary>
        STRESS_PROMOTE_FEWER_STRUCTS,

        /// <summary>Randomize the VN budget</summary>
        STRESS_VN_BUDGET,

        /// <summary>Select lower thresholds for "complex" SSA num encoding</summary>
        STRESS_SSA_INFO,

        /// <summary>Split all statements at a random tree</summary>
        STRESS_SPLIT_TREES_RANDOMLY,

        /// <summary>Remove all GT_COMMA nodes</summary>
        STRESS_SPLIT_TREES_REMOVE_COMMAS,

        /// <summary>Do not use old promotion</summary>
        STRESS_NO_OLD_PROMOTION,

        /// <summary>Use physical promotion</summary>
        STRESS_PHYSICAL_PROMOTION,

        STRESS_PHYSICAL_PROMOTION_COST,

        /// <summary>stress unwind info; e.g., create function fragments</summary>
        STRESS_UNWIND,

        /// <summary>stress JitOptRepeat</summary>
        STRESS_OPT_REPEAT,

        /// <summary>Stress initial register assigned to parameters</summary>
        STRESS_INITIAL_PARAM_REG,

        /// <summary>Make more loops downwards counted</summary>
        STRESS_DOWNWARDS_COUNTED_LOOPS,

        /// <summary>Enable strength reduction</summary>
        STRESS_STRENGTH_REDUCTION,

        /// <summary>Do more strength reduction</summary>
        STRESS_STRENGTH_REDUCTION_PROFITABILITY,

        // After COUNT_VARN, stress level 2 does all of these all the time

        STRESS_COUNT_VARN,

        // "Check" stress areas that can be exhaustively used if we don't care about performance at all                             

        /// <summary>Treat every method as AggressiveInlining</summary>
        STRESS_FORCE_INLINE,

        STRESS_EMITTER,

        STRESS_CHK_REIMPORT,

        STRESS_GENERIC_CHECK,

        STRESS_IF_CONVERSION_COST,

        STRESS_IF_CONVERSION_INNER_LOOPS,

        STRESS_POISON_IMPLICIT_BYREFS,

        STRESS_STORE_BLOCK_UNROLLING,

        STRESS_THREE_OPT_LAYOUT,

        STRESS_COUNT,
    }
}
