// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.GenTreeFlags;
using System;

namespace RyuJitSharp;

/// <summary>a bitmask of flags for GenTree stored in gtFlags</summary>
[Flags]
public enum GenTreeFlags
{
    GTF_EMPTY = 0,

    //---------------------------------------------------------------------
    //  The first set of flags can be used with a large set of nodes, and
    //  thus they must all have distinct values. That is, one can test any
    //  expression node for one of these flags.
    //---------------------------------------------------------------------

    /// <summary>sub-expression contains a store</summary>
    GTF_ASG = 1 << 0,

    /// <summary>sub-expression contains a func. call</summary>
    GTF_CALL = 1 << 1,

    /// <summary>sub-expression might throw an exception</summary>
    GTF_EXCEPT = 1 << 2,

    /// <summary>sub-expression uses global variable(s)</summary>
    GTF_GLOB_REF = 1 << 3,

    /// <summary>sub-expression has a re-ordering side effect</summary>
    GTF_ORDER_SIDEEFF = 1 << 4,

    // If you set these flags, make sure that code:gtExtractSideEffList knows how to find the tree,
    // otherwise the C# (run csc /o-) code:
    //     var v = side_eff_operation
    // with no use of `v` will drop your tree on the floor.

    GTF_PERSISTENT_SIDE_EFFECTS = GTF_ASG | GTF_CALL,
    GTF_SIDE_EFFECT = GTF_PERSISTENT_SIDE_EFFECTS | GTF_EXCEPT,
    GTF_GLOB_EFFECT = GTF_SIDE_EFFECT | GTF_GLOB_REF,
    GTF_ALL_EFFECT = GTF_GLOB_EFFECT | GTF_ORDER_SIDEEFF,

    /// <summary>operand op2 should be evaluated before op1 (normally, op1 is evaluated first and op2 is evaluated second)</summary>
    GTF_REVERSE_OPS = 1 << 5,

    /// <summary>This node is contained (executed as part of its parent)</summary>
    GTF_CONTAINED = 1 << 6,

    /// <summary>the value has been spilled</summary>
    GTF_SPILLED = 1 << 7,

    /// <summary>tree node is in memory at the point of use</summary>
    GTF_NOREG_AT_USE = 1 << 8,

    /// <summary>Requires that codegen for this node set the flags. Use gtSetFlags() to check this flag.</summary>
    GTF_SET_FLAGS = 1 << 9,

#if TARGET_XARCH
    /// <summary>This small-typed tree produces a value with undefined upper bits.</summary>
    /// <remarks>Used on x86/x64 as a lowering optimization and tells the codegen to use instructions like "mov al, [addr]" instead of "movzx/movsx", when the user node doesn't need the upper bits.</remarks>
    GTF_DONT_EXTEND = 1 << 10,
#endif

    /// <summary>Hoisted expression: try hard to make this into CSE (see optPerformHoistExpr)</summary>
    GTF_MAKE_CSE = 1 << 11,

    /// <summary>Don't bother CSE'ing this expr</summary>
    GTF_DONT_CSE = 1 << 12,

    /// <summary>This node is conditionally executed (part of ? :)</summary>
    GTF_COLON_COND = 1 << 13,

    GTF_NODE_MASK = GTF_COLON_COND,

    /// <summary>
    ///   <para>With GT_CAST:   the source operand is an unsigned type</para>
    ///   <para>With operators: the specified node is an unsigned operator</para>
    /// </summary>
    GTF_UNSIGNED = 1 << 15,

    /// <summary>Needs to be spilled here</summary>
    GTF_SPILL = 1 << 17,

    /// <summary>Mask of all the flags above</summary>
    GTF_COMMON_MASK = (1 << 18) - 1,

    /// <summary>This is set by the register allocator on nodes whose value already exists in the register assigned to this node, so the code generator does not have to generate code to produce the value. It is currently used only on constant nodes.</summary>
    /// <remarks>It CANNOT be set on var (GT_LCL*) nodes, or on indir (GT_IND or GT_STOREIND) nodes, since it is not needed for lclVars and is highly unlikely to be useful for indir nodes.</remarks>
    GTF_REUSE_REG_VAL = 1 << 23,

    //---------------------------------------------------------------------
    //  The following flags can be used only with a small set of nodes, and
    //  thus their values need not be distinct (other than within the set
    //  that goes with a particular node/nodes, of course). That is, one can
    //  only test for one of these flags if the 'gtOper' value is tested as
    //  well to make sure it's the right operator for the particular flag.
    //---------------------------------------------------------------------

    /// <summary>GT_STORE_LCL_VAR/GT_STORE_LCL_FLD/GT_LCL_ADDR -- this is a definition</summary>
    GTF_VAR_DEF = 1 << 31,

    /// <summary>GT_STORE_LCL_FLD/GT_STORE_LCL_FLD/GT_LCL_ADDR -- this is a partial definition, a use of the previous definition is implied.</summary>
    /// <remarks>A partial definition usually occurs when a struct field is assigned to (s.f = ...) or when a scalar typed variable is assigned to via a narrow store (*((byte*)&amp;i) = ...).</remarks>
    GTF_VAR_USEASG = 1 << 30,

    // Last-use bits. Also used by GenTreeCopyOrReload.
    // Note that a node marked GTF_VAR_MULTIREG can only be a pure definition of all the fields, or a pure use of all the fields,
    // so we don't need the equivalent of GTF_VAR_USEASG.

    /// <summary>The last-use bit for the first field of a promoted local.</summary>
    GTF_VAR_FIELD_DEATH0 = 1 << 26,

    /// <summary>The last-use bit for the second field of a promoted local.</summary>
    GTF_VAR_FIELD_DEATH1 = 1 << 27,

    /// <summary>The last-use bit for the third field of a promoted local.</summary>
    GTF_VAR_FIELD_DEATH2 = 1 << 28,

    /// <summary>The last-use bit for the fourth field of a promoted local.</summary>
    GTF_VAR_FIELD_DEATH3 = 1 << 29,

    GTF_VAR_DEATH_MASK = GTF_VAR_FIELD_DEATH0 | GTF_VAR_FIELD_DEATH1 | GTF_VAR_FIELD_DEATH2 | GTF_VAR_FIELD_DEATH3,

    /// <summary>The last-use bit for a tracked local.</summary>
    GTF_VAR_DEATH = GTF_VAR_FIELD_DEATH0,

    /// <summary>This is a struct or (on 32-bit platforms) long variable that is used or defined to/from a multireg source or destination (e.g. a call arg or return, or an op that returns its result in multiple registers such as a long multiply).</summary>
    /// <remarks>Set by (and thus only valid after) lowering.</remarks>
    GTF_VAR_MULTIREG = 1 << 25,

    GTF_LIVENESS_MASK = GTF_VAR_DEF | GTF_VAR_USEASG | GTF_VAR_DEATH_MASK,

    /// <summary>GT_LCL_VAR -- this node has additional uses, for example due to cloning</summary>
    GTF_VAR_MOREUSES = 1 << 23, 

    /// <summary>GT_LCL_VAR -- this node is part of a runtime lookup</summary>
    GTF_VAR_CONTEXT = 1 << 22,

    /// <summary>GT_LCL_VAR -- this node is an "explicit init" store. Valid until rationalization.</summary>
    GTF_VAR_EXPLICIT_INIT = 1 << 21, 

    // For additional flags for GT_CALL node see GTF_CALL_M_*

    /// <summary>GT_CALL -- direct call to unmanaged code</summary>
    GTF_CALL_UNMANAGED = 1 << 31,

    /// <summary>GT_CALL -- this call has been marked as an inline candidate</summary>
    GTF_CALL_INLINE_CANDIDATE = 1 << 30,

    /// <summary>GT_CALL -- mask of the below call kinds</summary>
    GTF_CALL_VIRT_KIND_MASK = (1 << 29) | (1 << 28),

    /// <summary>GT_CALL -- a non virtual call</summary>
    GTF_CALL_NONVIRT = 0x00000000,

    /// <summary>GT_CALL -- a stub-dispatch virtual call</summary>
    GTF_CALL_VIRT_STUB = 1 << 28,

    /// <summary>GT_CALL -- a  vtable-based virtual call</summary>
    GTF_CALL_VIRT_VTABLE = 1 << 29,

    /// <summary>GT_CALL -- must check instance pointer for null</summary>
    GTF_CALL_NULLCHECK = 1 << 27,

    /// <summary>GT_CALL -- caller pop arguments?</summary>
    GTF_CALL_POP_ARGS = 1 << 26,

    /// <summary>GT_CALL -- call is hoistable</summary>
    GTF_CALL_HOISTABLE = 1 << 25,

    /// <summary>GT_CALL -- call is tls_get_addr</summary>
    GTF_TLS_GET_ADDR = 1 << 24,

    /// <summary>GT_MEMORYBARRIER -- Load barrier</summary>
    GTF_MEMORYBARRIER_LOAD = 1 << 30,

    /// <summary>GT_MEMORYBARRIER -- Store barrier</summary>
    GTF_MEMORYBARRIER_STORE = 1 << 31,

    /// <summary>GT_FIELD_ADDR -- field address is a Windows x86 TLS reference</summary>
    GTF_FLD_TLS = 1 << 31,

    /// <summary>GT_FIELD_ADDR -- used to preserve previous behavior</summary>
    GTF_FLD_DEREFERENCED = 1 << 30,

    /// <summary>GT_FIELD_ADDR -- consuming indir must perform the implicit null check.</summary>
    GTF_FLD_TGT_NONFAULTING = 1 << 29,

    /// <summary>GT_INDEX_ADDR -- this array address should be range-checked</summary>
    GTF_INX_RNGCHK = 1 << 31,

    /// <summary>GT_INDEX_ADDR -- this array address is not null</summary>
    GTF_INX_ADDR_NONNULL = 1 << 30,

    /// <summary>GT_IND -- the target is not GC-tracked, such as an object on the nongc heap</summary>
    GTF_IND_TGT_NOT_HEAP = 1 << 31,

    /// <summary>OperIsIndir() -- the load or store must use volatile semantics (this is a nop on X86)</summary>
    GTF_IND_VOLATILE = 1 << 30,

    /// <summary>OperIsIndir() -- An indir that cannot fault.</summary>
    GTF_IND_NONFAULTING = 1 << 29,

    /// <summary>GT_IND -- the target is on the heap</summary>
    GTF_IND_TGT_HEAP = 1 << 28,

    /// <summary>GT_IND -- requires its addr operand to be evaluated into a register.</summary>
    /// <remarks>This flag is useful in cases where it is required to generate register indirect addressing mode. One such case is virtual stub calls on xarch.</remarks>
    GTF_IND_REQ_ADDR_IN_REG = 1 << 27,

    /// <summary>OperIsIndir() -- the load or store is unaligned (we assume worst case alignment of 1 byte)</summary>
    GTF_IND_UNALIGNED = 1 << 26,

    /// <summary>GT_IND -- the target is invariant (an AOT indirection)</summary>
    GTF_IND_INVARIANT = 1 << 25,

    /// <summary>GT_IND -- the indirection never returns null (zero)</summary>
    GTF_IND_NONNULL = 1 << 24,

    /// <summary>OperIsIndir() -- the indirection requires preceding static cctor</summary>
    GTF_IND_INITCLASS = 1 << 21,

    /// <summary>GT_IND -- this memory access does not need to be atomic</summary>
    GTF_IND_ALLOW_NON_ATOMIC = 1 << 20,

    /// <summary>Represents flags that an indirection based on another indirection must preserve</summary>
    GTF_IND_MUST_PRESERVE_FLAGS = GTF_IND_VOLATILE | GTF_IND_UNALIGNED | GTF_IND_INITCLASS,

    /// <summary>Represents flags that an indirection based on another indirection can and must preserve</summary>
    GTF_IND_COPYABLE_FLAGS = GTF_IND_MUST_PRESERVE_FLAGS | GTF_IND_NONFAULTING,

    GTF_IND_FLAGS = GTF_IND_COPYABLE_FLAGS | GTF_IND_NONNULL | GTF_IND_TGT_NOT_HEAP | GTF_IND_TGT_HEAP | GTF_IND_INVARIANT | GTF_IND_ALLOW_NON_ATOMIC,

    /// <summary>// GT_ADD/GT_MUL/GT_LSH/GT_CAST -- Do not CSE this node only, forms complex addressing mode</summary>
    GTF_ADDRMODE_NO_CSE = 1 << 31,

    /// <summary>GT_MUL     -- produce 64-bit result</summary>
    GTF_MUL_64RSLT = 1 << 30,

    /// <summary>GT_&lt;relop&gt; -- Is branch taken if ops are NaN?</summary>
    GTF_RELOP_NAN_UN = 1 << 31,

    /// <summary>GT_&lt;relop&gt; -- result of compare used for jump or ?: with explicit "loop test" in the header block.</summary>
    GTF_RELOP_JMP_USED = 1 << 30,

    /// <summary>GT_RETURN -- This is a return generated during epilog merging.</summary>
    GTF_RET_MERGED = 1 << 31,

    /// <summary>GT_BOX -- this box and its operand has been cloned, cannot assume it to be single-use anymore</summary>
    GTF_BOX_CLONED = 1 << 30,

    /// <summary>GT_BOX -- "box" is on a value type</summary>
    GTF_BOX_VALUE = 1 << 31,

    /// <summary>GT_QMARK -- early expansion of the QMARK node is required</summary>
    GTF_QMARK_EARLY_EXPAND = 1 << 24,

    /// <summary>GT_ARR_ADDR -- this array's address is not null</summary>
    GTF_ARR_ADDR_NONNULL = 1 << 31,

    /// <summary>// 0xFF000000, bits used by the handle types.</summary>
    GTF_ICON_HDL_MASK = -1 << HANDLE_KIND_INDEX_SHIFT,

    // /// <summary>GT_CNS_INT -- GTF_REUSE_REG_VAL, defined above</summary>
    // GTF_ICON_REUSE_REG_VAL = 1 << 23,

    /// <summary>GT_CNS_INT -- constant is Vector&lt;T&gt;.Count</summary>
    GTF_ICON_SIMD_COUNT = 1 << 21,

    /// <summary>Supported for: GT_ADD, GT_SUB, GT_MUL and GT_CAST.</summary>
    /// <remarks>Requires an overflow check. Use gtOverflow(Ex)() to check this flag.</remarks>
    GTF_OVERFLOW = 1 << 28,

    /// <summary>GT_DIV, GT_MOD -- Div or mod definitely does not divide-by-zero.</summary>
    GTF_DIV_MOD_NO_BY_ZERO = 1 << 29,

    /// <summary>GT_DIV, GT_MOD -- Div or mod definitely does not overflow.</summary>
    GTF_DIV_MOD_NO_OVERFLOW = 1 << 30,

    /// <summary>GT_BOUNDS_CHECK -- have proven this check is always in-bounds</summary>
    GTF_CHK_INDEX_INBND = 1 << 31,

    /// <summary>GT_ARR_LENGTH  -- An array length operation that cannot fault. Same as GT_IND_NONFAULTING.</summary>
    GTF_ARRLEN_NONFAULTING = 1 << 29,

    /// <summary>GT_MDARR_LENGTH -- An MD array length operation that cannot fault. Same as GT_IND_NONFAULTING.</summary>
    GTF_MDARRLEN_NONFAULTING = 1 << 29,

    /// <summary>GT_MDARR_LOWER_BOUND -- An MD array lower bound operation that cannot fault. Same as GT_IND_NONFAULTING.</summary>
    GTF_MDARRLOWERBOUND_NONFAULTING = 1 << 29,

    /// <summary>GT_ALLOCOBJ -- allocation site is part of an empty static pattern</summary>
    GTF_ALLOCOBJ_EMPTY_STATIC = 1 << 31,

#if FEATURE_HW_INTRINSICS
    /// <summary>GT_HWINTRINSIC -- node is used as an operand to an embedded mask</summary>
    GTF_HW_EM_OP = 1 << 28,

    /// <summary>GT_HWINTRINSIC -- node is implemented via a user call</summary>
    GTF_HW_USER_CALL = 1 << 29,
#endif
}
