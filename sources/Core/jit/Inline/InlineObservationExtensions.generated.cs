// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public static partial class InlineObservationExtensions
{
    private static readonly string[] s_descriptions = [
        "unused initial observation", // CALLEE_UNUSED_INITIAL
        "invalid argument number", // CALLEE_BAD_ARGUMENT_NUMBER
        "invalid local number", // CALLEE_BAD_LOCAL_NUMBER
        "compilation error", // CALLEE_COMPILATION_ERROR
        "explicit tail prefix in callee", // CALLEE_EXPLICIT_TAIL_PREFIX
        "has exception handling", // CALLEE_HAS_EH
        "has endfilter", // CALLEE_HAS_ENDFILTER
        "has endfinally", // CALLEE_HAS_ENDFINALLY
        "has leave", // CALLEE_HAS_LEAVE
        "managed varargs", // CALLEE_HAS_MANAGED_VARARGS
        "native varargs", // CALLEE_HAS_NATIVE_VARARGS
        "has no body", // CALLEE_HAS_NO_BODY
        "has null pointer for ldelem", // CALLEE_HAS_NULL_FOR_LDELEM
        "has unmanaged calling convention", // CALLEE_HAS_UNMANAGED_CALLCONV
        "is array method", // CALLEE_IS_ARRAY_METHOD
        "noinline per JitNoinline", // CALLEE_IS_JIT_NOINLINE
        "noinline per IL/cached result", // CALLEE_IS_NOINLINE
        "is synchronized", // CALLEE_IS_SYNCHRONIZED
        "noinline per VM", // CALLEE_IS_VM_NOINLINE
        "no return opcode", // CALLEE_LACKS_RETURN
        "ldfld needs helper", // CALLEE_LDFLD_NEEDS_HELPER
        "localloc size too large", // CALLEE_LOCALLOC_TOO_LARGE
        "rejected by log replay", // CALLEE_LOG_REPLAY_REJECT
        "skipped by complus request", // CALLEE_MARKED_AS_SKIPPED
        "maxstack too big", // CALLEE_MAXSTACK_TOO_BIG
        "cannot get method info", // CALLEE_NO_METHOD_INFO
        "unprofitable inline", // CALLEE_NOT_PROFITABLE_INLINE
        "random reject", // CALLEE_RANDOM_REJECT
        "uses stack crawl mark", // CALLEE_STACK_CRAWL_MARK
        "stfld needs helper", // CALLEE_STFLD_NEEDS_HELPER
        "too many arguments", // CALLEE_TOO_MANY_ARGUMENTS
        "too many locals", // CALLEE_TOO_MANY_LOCALS
        "has await", // CALLEE_AWAIT
        "has async suspend", // CALLEE_ASYNC_SUSPEND
        "ldsfld of value class", // CALLEE_LDFLD_STATIC_VALUECLASS
        "too many basic blocks", // CALLEE_TOO_MANY_BASIC_BLOCKS
        "too many il bytes", // CALLEE_TOO_MUCH_IL
        "argument feeds constant test", // CALLEE_ARG_FEEDS_CONSTANT_TEST
        "argument feeds test", // CALLEE_ARG_FEEDS_TEST
        "argument feeds castclass or isinst", // CALLEE_ARG_FEEDS_CAST
        "argument feeds range check", // CALLEE_ARG_FEEDS_RANGE_CHECK
        "argument feeds IsKnownConstant", // CALLEE_ARG_FEEDS_ISCONST
        "const argument feeds IsKnownConstant", // CALLEE_CONST_ARG_FEEDS_ISCONST
        "arg is a struct passed by value", // CALLEE_ARG_STRUCT
        "returns a struct by value", // CALLEE_RETURNS_STRUCT
        "ldfld/stfld over arg (struct)", // CALLEE_ARG_STRUCT_FIELD_ACCESS
        "'X op CNS' pattern", // CALLEE_BINARY_EXRP_WITH_CNS
        "prepare to look at opcodes", // CALLEE_BEGIN_OPCODE_SCAN
        "below ALWAYS_INLINE size", // CALLEE_BELOW_ALWAYS_INLINE_SIZE
        "promotable value class", // CALLEE_CLASS_PROMOTABLE
        "value class", // CALLEE_CLASS_VALUETYPE
        "foldable box/unbox operation", // CALLEE_FOLDABLE_BOX
        "call marked as intrinsic", // CALLEE_INTRINSIC
        "backward jump", // CALLEE_BACKWARD_JUMP
        "throw block", // CALLEE_THROW_BLOCK
        "does not return", // CALLEE_DOES_NOT_RETURN
        "done looking at opcodes", // CALLEE_END_OPCODE_SCAN
        "has gc field in struct local", // CALLEE_HAS_GC_STRUCT
        "has localloc", // CALLEE_HAS_LOCALLOC
        "has pinned locals", // CALLEE_HAS_PINNED_LOCALS
        "has SIMD arg, local, or ret", // CALLEE_HAS_SIMD
        "has switch", // CALLEE_HAS_SWITCH
        "number of bytes of IL", // CALLEE_IL_CODE_SIZE
        "class constructor", // CALLEE_IS_CLASS_CTOR
        "can inline, check heuristics", // CALLEE_IS_DISCRETIONARY_INLINE
        "aggressive inline attribute", // CALLEE_IS_FORCE_INLINE
        "instance constructor", // CALLEE_IS_INSTANCE_CTOR
        "profitable inline", // CALLEE_IS_PROFITABLE_INLINE
        "size decreasing inline", // CALLEE_IS_SIZE_DECREASING_INLINE
        "callee class marked as Intrinsic", // CALLEE_IS_INTRINSIC_TYPE
        "accepted by log replay", // CALLEE_LOG_REPLAY_ACCEPT
        "thin wrapper around a call", // CALLEE_LOOKS_LIKE_WRAPPER
        "maxstack", // CALLEE_MAXSTACK
        "may return a small new array", // CALLEE_MAY_RETURN_SMALL_ARRAY
        "next opcode in IL stream", // CALLEE_OPCODE
        "next opcode in IL stream", // CALLEE_OPCODE_NORMED
        "number of arguments", // CALLEE_NUMBER_OF_ARGUMENTS
        "number of basic blocks", // CALLEE_NUMBER_OF_BASIC_BLOCKS
        "number of locals", // CALLEE_NUMBER_OF_LOCALS
        "random accept", // CALLEE_RANDOM_ACCEPT
        "callee unboxes arg", // CALLEE_UNBOX_ARG
        "unsupported opcode", // CALLEE_UNSUPPORTED_OPCODE
        "debug codegen", // CALLER_DEBUG_CODEGEN
        "noinline per JitNoInlineRange", // CALLER_IS_JIT_NOINLINE
        "uses NextCallReturnAddress intrinsic", // CALLER_USES_NEXT_CALL_RET_ADDR
        "uses AsyncCallContinuation intrinsic", // CALLER_ASYNC_USED_CONTINUATION
        "has newarray", // CALLER_HAS_NEWARRAY
        "has newobj", // CALLER_HAS_NEWOBJ
        "this pointer argument is null", // CALLSITE_ARG_HAS_NULL_THIS
        "argument can't bash to int", // CALLSITE_ARG_NO_BASH_TO_INT
        "argument can't bash to ref", // CALLSITE_ARG_NO_BASH_TO_REF
        "argument types incompatible", // CALLSITE_ARG_TYPES_INCOMPATIBLE
        "can't class init", // CALLSITE_CANT_CLASS_INIT
        "compilation error", // CALLSITE_COMPILATION_ERROR
        "failed to compile", // CALLSITE_COMPILATION_FAILURE
        "explicit tail prefix", // CALLSITE_EXPLICIT_TAIL_PREFIX
        "callee has eh, eh table is full", // CALLSITE_EH_TABLE_FULL
        "runtime dictionary lookup", // CALLSITE_GENERIC_DICTIONARY_LOOKUP
        "complex handle access", // CALLSITE_HAS_COMPLEX_HANDLE
        "implicit recursive tail call", // CALLSITE_IMPLICIT_REC_TAIL_CALL
        "target is helper", // CALLSITE_IS_CALL_TO_HELPER
        "target not direct", // CALLSITE_IS_NOT_DIRECT
        "target not direct managed", // CALLSITE_IS_NOT_DIRECT_MANAGED
        "recursive", // CALLSITE_IS_RECURSIVE
        "too deep", // CALLSITE_IS_TOO_DEEP
        "virtual", // CALLSITE_IS_VIRTUAL
        "noinline per VM", // CALLSITE_IS_VM_NOINLINE
        "within catch region", // CALLSITE_IS_WITHIN_CATCH
        "within filter region", // CALLSITE_IS_WITHIN_FILTER
        "ldarga not on local var", // CALLSITE_LDARGA_NOT_LOCAL_VAR
        "ldfld needs helper", // CALLSITE_LDFLD_NEEDS_HELPER
        "ldvirtfn on non-virtual", // CALLSITE_LDVIRTFN_ON_NON_VIRTUAL
        "within loop, has localloc", // CALLSITE_LOCALLOC_IN_LOOP
        "localloc size unknown", // CALLSITE_LOCALLOC_SIZE_UNKNOWN
        "rejected by log replay", // CALLSITE_LOG_REPLAY_REJECT
        "not inline candidate", // CALLSITE_NOT_CANDIDATE
        "unprofitable inline", // CALLSITE_NOT_PROFITABLE_INLINE
        "inline exceeds budget", // CALLSITE_OVER_BUDGET
        "limited by JitInlineLimit", // CALLSITE_OVER_INLINE_LIMIT
        "within try region, pinned", // CALLSITE_PIN_IN_TRY_REGION
        "random reject", // CALLSITE_RANDOM_REJECT
        "return type mismatch", // CALLSITE_RETURN_TYPE_MISMATCH
        "stfld needs helper", // CALLSITE_STFLD_NEEDS_HELPER
        "too many locals", // CALLSITE_TOO_MANY_LOCALS
        "PInvoke call site with EH", // CALLSITE_PINVOKE_EH
        "rarely called, has gc struct", // CALLSITE_RARE_GC_STRUCT
        "callee is generic and caller is not", // CALLSITE_NONGENERIC_CALLS_GENERIC
        "arg is of an exact class", // CALLSITE_ARG_EXACT_CLS
        "arg is more concrete than in sig.", // CALLSITE_ARG_EXACT_CLS_SIG_IS_NOT
        "arg is a constant", // CALLSITE_ARG_CONST
        "arg is boxed at call site", // CALLSITE_ARG_BOXED
        "foldable intrinsic", // CALLSITE_FOLDABLE_INTRINSIC
        "foldable binary expression", // CALLSITE_FOLDABLE_EXPR
        "foldable unary expression", // CALLSITE_FOLDABLE_EXPR_UN
        "foldable branch", // CALLSITE_FOLDABLE_BRANCH
        "foldable switch", // CALLSITE_FOLDABLE_SWITCH
        "unrollable memmove/memcmp", // CALLSITE_UNROLLABLE_MEMOP
        "dividy by const", // CALLSITE_DIV_BY_CNS
        "constant argument feeds test", // CALLSITE_CONSTANT_ARG_FEEDS_TEST
        "depth", // CALLSITE_DEPTH
        "rough call site frequency", // CALLSITE_FREQUENCY
        "profile weights are available", // CALLSITE_HAS_PROFILE_WEIGHTS
        "inside throw block", // CALLSITE_INSIDE_THROW_BLOCK
        "call site is in a loop", // CALLSITE_IN_LOOP
        "call site is in a try region", // CALLSITE_IN_TRY_REGION
        "call site is in a no-return region", // CALLSITE_IN_NORETURN_REGION
        "profitable inline", // CALLSITE_IS_PROFITABLE_INLINE
        "same this as root caller", // CALLSITE_IS_SAME_THIS
        "size decreasing inline", // CALLSITE_IS_SIZE_DECREASING_INLINE
        "accepted by log replay", // CALLSITE_LOG_REPLAY_ACCEPT
        "frequency from profile data", // CALLSITE_PROFILE_FREQUENCY
        "random accept", // CALLSITE_RANDOM_ACCEPT
        "frequency from block weight", // CALLSITE_WEIGHT
        "unbox of arg with exact class", // CALLSITE_UNBOX_EXACT_ARG
        "unused final observation", // CALLEE_UNUSED_FINAL
    ];

    private static ReadOnlySpan<InlineImpact> s_impacts => [
        InlineImpact.FATAL, // CALLEE_UNUSED_INITIAL
        InlineImpact.FATAL, // CALLEE_BAD_ARGUMENT_NUMBER
        InlineImpact.FATAL, // CALLEE_BAD_LOCAL_NUMBER
        InlineImpact.FATAL, // CALLEE_COMPILATION_ERROR
        InlineImpact.FATAL, // CALLEE_EXPLICIT_TAIL_PREFIX
        InlineImpact.FATAL, // CALLEE_HAS_EH
        InlineImpact.FATAL, // CALLEE_HAS_ENDFILTER
        InlineImpact.FATAL, // CALLEE_HAS_ENDFINALLY
        InlineImpact.FATAL, // CALLEE_HAS_LEAVE
        InlineImpact.FATAL, // CALLEE_HAS_MANAGED_VARARGS
        InlineImpact.FATAL, // CALLEE_HAS_NATIVE_VARARGS
        InlineImpact.FATAL, // CALLEE_HAS_NO_BODY
        InlineImpact.FATAL, // CALLEE_HAS_NULL_FOR_LDELEM
        InlineImpact.FATAL, // CALLEE_HAS_UNMANAGED_CALLCONV
        InlineImpact.FATAL, // CALLEE_IS_ARRAY_METHOD
        InlineImpact.FATAL, // CALLEE_IS_JIT_NOINLINE
        InlineImpact.FATAL, // CALLEE_IS_NOINLINE
        InlineImpact.FATAL, // CALLEE_IS_SYNCHRONIZED
        InlineImpact.FATAL, // CALLEE_IS_VM_NOINLINE
        InlineImpact.FATAL, // CALLEE_LACKS_RETURN
        InlineImpact.FATAL, // CALLEE_LDFLD_NEEDS_HELPER
        InlineImpact.FATAL, // CALLEE_LOCALLOC_TOO_LARGE
        InlineImpact.FATAL, // CALLEE_LOG_REPLAY_REJECT
        InlineImpact.FATAL, // CALLEE_MARKED_AS_SKIPPED
        InlineImpact.FATAL, // CALLEE_MAXSTACK_TOO_BIG
        InlineImpact.FATAL, // CALLEE_NO_METHOD_INFO
        InlineImpact.FATAL, // CALLEE_NOT_PROFITABLE_INLINE
        InlineImpact.FATAL, // CALLEE_RANDOM_REJECT
        InlineImpact.FATAL, // CALLEE_STACK_CRAWL_MARK
        InlineImpact.FATAL, // CALLEE_STFLD_NEEDS_HELPER
        InlineImpact.FATAL, // CALLEE_TOO_MANY_ARGUMENTS
        InlineImpact.FATAL, // CALLEE_TOO_MANY_LOCALS
        InlineImpact.FATAL, // CALLEE_AWAIT
        InlineImpact.FATAL, // CALLEE_ASYNC_SUSPEND
        InlineImpact.PERFORMANCE, // CALLEE_LDFLD_STATIC_VALUECLASS
        InlineImpact.PERFORMANCE, // CALLEE_TOO_MANY_BASIC_BLOCKS
        InlineImpact.PERFORMANCE, // CALLEE_TOO_MUCH_IL
        InlineImpact.INFORMATION, // CALLEE_ARG_FEEDS_CONSTANT_TEST
        InlineImpact.INFORMATION, // CALLEE_ARG_FEEDS_TEST
        InlineImpact.INFORMATION, // CALLEE_ARG_FEEDS_CAST
        InlineImpact.INFORMATION, // CALLEE_ARG_FEEDS_RANGE_CHECK
        InlineImpact.INFORMATION, // CALLEE_ARG_FEEDS_ISCONST
        InlineImpact.INFORMATION, // CALLEE_CONST_ARG_FEEDS_ISCONST
        InlineImpact.INFORMATION, // CALLEE_ARG_STRUCT
        InlineImpact.INFORMATION, // CALLEE_RETURNS_STRUCT
        InlineImpact.INFORMATION, // CALLEE_ARG_STRUCT_FIELD_ACCESS
        InlineImpact.INFORMATION, // CALLEE_BINARY_EXRP_WITH_CNS
        InlineImpact.INFORMATION, // CALLEE_BEGIN_OPCODE_SCAN
        InlineImpact.INFORMATION, // CALLEE_BELOW_ALWAYS_INLINE_SIZE
        InlineImpact.INFORMATION, // CALLEE_CLASS_PROMOTABLE
        InlineImpact.INFORMATION, // CALLEE_CLASS_VALUETYPE
        InlineImpact.INFORMATION, // CALLEE_FOLDABLE_BOX
        InlineImpact.INFORMATION, // CALLEE_INTRINSIC
        InlineImpact.INFORMATION, // CALLEE_BACKWARD_JUMP
        InlineImpact.INFORMATION, // CALLEE_THROW_BLOCK
        InlineImpact.INFORMATION, // CALLEE_DOES_NOT_RETURN
        InlineImpact.INFORMATION, // CALLEE_END_OPCODE_SCAN
        InlineImpact.INFORMATION, // CALLEE_HAS_GC_STRUCT
        InlineImpact.INFORMATION, // CALLEE_HAS_LOCALLOC
        InlineImpact.INFORMATION, // CALLEE_HAS_PINNED_LOCALS
        InlineImpact.INFORMATION, // CALLEE_HAS_SIMD
        InlineImpact.INFORMATION, // CALLEE_HAS_SWITCH
        InlineImpact.INFORMATION, // CALLEE_IL_CODE_SIZE
        InlineImpact.INFORMATION, // CALLEE_IS_CLASS_CTOR
        InlineImpact.INFORMATION, // CALLEE_IS_DISCRETIONARY_INLINE
        InlineImpact.INFORMATION, // CALLEE_IS_FORCE_INLINE
        InlineImpact.INFORMATION, // CALLEE_IS_INSTANCE_CTOR
        InlineImpact.INFORMATION, // CALLEE_IS_PROFITABLE_INLINE
        InlineImpact.INFORMATION, // CALLEE_IS_SIZE_DECREASING_INLINE
        InlineImpact.INFORMATION, // CALLEE_IS_INTRINSIC_TYPE
        InlineImpact.INFORMATION, // CALLEE_LOG_REPLAY_ACCEPT
        InlineImpact.INFORMATION, // CALLEE_LOOKS_LIKE_WRAPPER
        InlineImpact.INFORMATION, // CALLEE_MAXSTACK
        InlineImpact.INFORMATION, // CALLEE_MAY_RETURN_SMALL_ARRAY
        InlineImpact.INFORMATION, // CALLEE_OPCODE
        InlineImpact.INFORMATION, // CALLEE_OPCODE_NORMED
        InlineImpact.INFORMATION, // CALLEE_NUMBER_OF_ARGUMENTS
        InlineImpact.INFORMATION, // CALLEE_NUMBER_OF_BASIC_BLOCKS
        InlineImpact.INFORMATION, // CALLEE_NUMBER_OF_LOCALS
        InlineImpact.INFORMATION, // CALLEE_RANDOM_ACCEPT
        InlineImpact.INFORMATION, // CALLEE_UNBOX_ARG
        InlineImpact.INFORMATION, // CALLEE_UNSUPPORTED_OPCODE
        InlineImpact.FATAL, // CALLER_DEBUG_CODEGEN
        InlineImpact.FATAL, // CALLER_IS_JIT_NOINLINE
        InlineImpact.FATAL, // CALLER_USES_NEXT_CALL_RET_ADDR
        InlineImpact.FATAL, // CALLER_ASYNC_USED_CONTINUATION
        InlineImpact.INFORMATION, // CALLER_HAS_NEWARRAY
        InlineImpact.INFORMATION, // CALLER_HAS_NEWOBJ
        InlineImpact.FATAL, // CALLSITE_ARG_HAS_NULL_THIS
        InlineImpact.FATAL, // CALLSITE_ARG_NO_BASH_TO_INT
        InlineImpact.FATAL, // CALLSITE_ARG_NO_BASH_TO_REF
        InlineImpact.FATAL, // CALLSITE_ARG_TYPES_INCOMPATIBLE
        InlineImpact.FATAL, // CALLSITE_CANT_CLASS_INIT
        InlineImpact.FATAL, // CALLSITE_COMPILATION_ERROR
        InlineImpact.FATAL, // CALLSITE_COMPILATION_FAILURE
        InlineImpact.FATAL, // CALLSITE_EXPLICIT_TAIL_PREFIX
        InlineImpact.FATAL, // CALLSITE_EH_TABLE_FULL
        InlineImpact.FATAL, // CALLSITE_GENERIC_DICTIONARY_LOOKUP
        InlineImpact.FATAL, // CALLSITE_HAS_COMPLEX_HANDLE
        InlineImpact.FATAL, // CALLSITE_IMPLICIT_REC_TAIL_CALL
        InlineImpact.FATAL, // CALLSITE_IS_CALL_TO_HELPER
        InlineImpact.FATAL, // CALLSITE_IS_NOT_DIRECT
        InlineImpact.FATAL, // CALLSITE_IS_NOT_DIRECT_MANAGED
        InlineImpact.FATAL, // CALLSITE_IS_RECURSIVE
        InlineImpact.FATAL, // CALLSITE_IS_TOO_DEEP
        InlineImpact.FATAL, // CALLSITE_IS_VIRTUAL
        InlineImpact.FATAL, // CALLSITE_IS_VM_NOINLINE
        InlineImpact.FATAL, // CALLSITE_IS_WITHIN_CATCH
        InlineImpact.FATAL, // CALLSITE_IS_WITHIN_FILTER
        InlineImpact.FATAL, // CALLSITE_LDARGA_NOT_LOCAL_VAR
        InlineImpact.FATAL, // CALLSITE_LDFLD_NEEDS_HELPER
        InlineImpact.FATAL, // CALLSITE_LDVIRTFN_ON_NON_VIRTUAL
        InlineImpact.FATAL, // CALLSITE_LOCALLOC_IN_LOOP
        InlineImpact.FATAL, // CALLSITE_LOCALLOC_SIZE_UNKNOWN
        InlineImpact.FATAL, // CALLSITE_LOG_REPLAY_REJECT
        InlineImpact.FATAL, // CALLSITE_NOT_CANDIDATE
        InlineImpact.FATAL, // CALLSITE_NOT_PROFITABLE_INLINE
        InlineImpact.FATAL, // CALLSITE_OVER_BUDGET
        InlineImpact.FATAL, // CALLSITE_OVER_INLINE_LIMIT
        InlineImpact.FATAL, // CALLSITE_PIN_IN_TRY_REGION
        InlineImpact.FATAL, // CALLSITE_RANDOM_REJECT
        InlineImpact.FATAL, // CALLSITE_RETURN_TYPE_MISMATCH
        InlineImpact.FATAL, // CALLSITE_STFLD_NEEDS_HELPER
        InlineImpact.FATAL, // CALLSITE_TOO_MANY_LOCALS
        InlineImpact.FATAL, // CALLSITE_PINVOKE_EH
        InlineImpact.INFORMATION, // CALLSITE_RARE_GC_STRUCT
        InlineImpact.INFORMATION, // CALLSITE_NONGENERIC_CALLS_GENERIC
        InlineImpact.INFORMATION, // CALLSITE_ARG_EXACT_CLS
        InlineImpact.INFORMATION, // CALLSITE_ARG_EXACT_CLS_SIG_IS_NOT
        InlineImpact.INFORMATION, // CALLSITE_ARG_CONST
        InlineImpact.INFORMATION, // CALLSITE_ARG_BOXED
        InlineImpact.INFORMATION, // CALLSITE_FOLDABLE_INTRINSIC
        InlineImpact.INFORMATION, // CALLSITE_FOLDABLE_EXPR
        InlineImpact.INFORMATION, // CALLSITE_FOLDABLE_EXPR_UN
        InlineImpact.INFORMATION, // CALLSITE_FOLDABLE_BRANCH
        InlineImpact.INFORMATION, // CALLSITE_FOLDABLE_SWITCH
        InlineImpact.INFORMATION, // CALLSITE_UNROLLABLE_MEMOP
        InlineImpact.INFORMATION, // CALLSITE_DIV_BY_CNS
        InlineImpact.INFORMATION, // CALLSITE_CONSTANT_ARG_FEEDS_TEST
        InlineImpact.INFORMATION, // CALLSITE_DEPTH
        InlineImpact.INFORMATION, // CALLSITE_FREQUENCY
        InlineImpact.INFORMATION, // CALLSITE_HAS_PROFILE_WEIGHTS
        InlineImpact.INFORMATION, // CALLSITE_INSIDE_THROW_BLOCK
        InlineImpact.INFORMATION, // CALLSITE_IN_LOOP
        InlineImpact.INFORMATION, // CALLSITE_IN_TRY_REGION
        InlineImpact.INFORMATION, // CALLSITE_IN_NORETURN_REGION
        InlineImpact.INFORMATION, // CALLSITE_IS_PROFITABLE_INLINE
        InlineImpact.INFORMATION, // CALLSITE_IS_SAME_THIS
        InlineImpact.INFORMATION, // CALLSITE_IS_SIZE_DECREASING_INLINE
        InlineImpact.INFORMATION, // CALLSITE_LOG_REPLAY_ACCEPT
        InlineImpact.INFORMATION, // CALLSITE_PROFILE_FREQUENCY
        InlineImpact.INFORMATION, // CALLSITE_RANDOM_ACCEPT
        InlineImpact.INFORMATION, // CALLSITE_WEIGHT
        InlineImpact.INFORMATION, // CALLSITE_UNBOX_EXACT_ARG
        InlineImpact.FATAL, // CALLEE_UNUSED_FINAL
    ];

    private static ReadOnlySpan<InlineTarget> s_targets => [
        InlineTarget.CALLEE, // CALLEE_UNUSED_INITIAL
        InlineTarget.CALLEE, // CALLEE_BAD_ARGUMENT_NUMBER
        InlineTarget.CALLEE, // CALLEE_BAD_LOCAL_NUMBER
        InlineTarget.CALLEE, // CALLEE_COMPILATION_ERROR
        InlineTarget.CALLEE, // CALLEE_EXPLICIT_TAIL_PREFIX
        InlineTarget.CALLEE, // CALLEE_HAS_EH
        InlineTarget.CALLEE, // CALLEE_HAS_ENDFILTER
        InlineTarget.CALLEE, // CALLEE_HAS_ENDFINALLY
        InlineTarget.CALLEE, // CALLEE_HAS_LEAVE
        InlineTarget.CALLEE, // CALLEE_HAS_MANAGED_VARARGS
        InlineTarget.CALLEE, // CALLEE_HAS_NATIVE_VARARGS
        InlineTarget.CALLEE, // CALLEE_HAS_NO_BODY
        InlineTarget.CALLEE, // CALLEE_HAS_NULL_FOR_LDELEM
        InlineTarget.CALLEE, // CALLEE_HAS_UNMANAGED_CALLCONV
        InlineTarget.CALLEE, // CALLEE_IS_ARRAY_METHOD
        InlineTarget.CALLEE, // CALLEE_IS_JIT_NOINLINE
        InlineTarget.CALLEE, // CALLEE_IS_NOINLINE
        InlineTarget.CALLEE, // CALLEE_IS_SYNCHRONIZED
        InlineTarget.CALLEE, // CALLEE_IS_VM_NOINLINE
        InlineTarget.CALLEE, // CALLEE_LACKS_RETURN
        InlineTarget.CALLEE, // CALLEE_LDFLD_NEEDS_HELPER
        InlineTarget.CALLEE, // CALLEE_LOCALLOC_TOO_LARGE
        InlineTarget.CALLEE, // CALLEE_LOG_REPLAY_REJECT
        InlineTarget.CALLEE, // CALLEE_MARKED_AS_SKIPPED
        InlineTarget.CALLEE, // CALLEE_MAXSTACK_TOO_BIG
        InlineTarget.CALLEE, // CALLEE_NO_METHOD_INFO
        InlineTarget.CALLEE, // CALLEE_NOT_PROFITABLE_INLINE
        InlineTarget.CALLEE, // CALLEE_RANDOM_REJECT
        InlineTarget.CALLEE, // CALLEE_STACK_CRAWL_MARK
        InlineTarget.CALLEE, // CALLEE_STFLD_NEEDS_HELPER
        InlineTarget.CALLEE, // CALLEE_TOO_MANY_ARGUMENTS
        InlineTarget.CALLEE, // CALLEE_TOO_MANY_LOCALS
        InlineTarget.CALLEE, // CALLEE_AWAIT
        InlineTarget.CALLEE, // CALLEE_ASYNC_SUSPEND
        InlineTarget.CALLEE, // CALLEE_LDFLD_STATIC_VALUECLASS
        InlineTarget.CALLEE, // CALLEE_TOO_MANY_BASIC_BLOCKS
        InlineTarget.CALLEE, // CALLEE_TOO_MUCH_IL
        InlineTarget.CALLEE, // CALLEE_ARG_FEEDS_CONSTANT_TEST
        InlineTarget.CALLEE, // CALLEE_ARG_FEEDS_TEST
        InlineTarget.CALLEE, // CALLEE_ARG_FEEDS_CAST
        InlineTarget.CALLEE, // CALLEE_ARG_FEEDS_RANGE_CHECK
        InlineTarget.CALLEE, // CALLEE_ARG_FEEDS_ISCONST
        InlineTarget.CALLEE, // CALLEE_CONST_ARG_FEEDS_ISCONST
        InlineTarget.CALLEE, // CALLEE_ARG_STRUCT
        InlineTarget.CALLEE, // CALLEE_RETURNS_STRUCT
        InlineTarget.CALLEE, // CALLEE_ARG_STRUCT_FIELD_ACCESS
        InlineTarget.CALLEE, // CALLEE_BINARY_EXRP_WITH_CNS
        InlineTarget.CALLEE, // CALLEE_BEGIN_OPCODE_SCAN
        InlineTarget.CALLEE, // CALLEE_BELOW_ALWAYS_INLINE_SIZE
        InlineTarget.CALLEE, // CALLEE_CLASS_PROMOTABLE
        InlineTarget.CALLEE, // CALLEE_CLASS_VALUETYPE
        InlineTarget.CALLEE, // CALLEE_FOLDABLE_BOX
        InlineTarget.CALLEE, // CALLEE_INTRINSIC
        InlineTarget.CALLEE, // CALLEE_BACKWARD_JUMP
        InlineTarget.CALLEE, // CALLEE_THROW_BLOCK
        InlineTarget.CALLEE, // CALLEE_DOES_NOT_RETURN
        InlineTarget.CALLEE, // CALLEE_END_OPCODE_SCAN
        InlineTarget.CALLEE, // CALLEE_HAS_GC_STRUCT
        InlineTarget.CALLEE, // CALLEE_HAS_LOCALLOC
        InlineTarget.CALLEE, // CALLEE_HAS_PINNED_LOCALS
        InlineTarget.CALLEE, // CALLEE_HAS_SIMD
        InlineTarget.CALLEE, // CALLEE_HAS_SWITCH
        InlineTarget.CALLEE, // CALLEE_IL_CODE_SIZE
        InlineTarget.CALLEE, // CALLEE_IS_CLASS_CTOR
        InlineTarget.CALLEE, // CALLEE_IS_DISCRETIONARY_INLINE
        InlineTarget.CALLEE, // CALLEE_IS_FORCE_INLINE
        InlineTarget.CALLEE, // CALLEE_IS_INSTANCE_CTOR
        InlineTarget.CALLEE, // CALLEE_IS_PROFITABLE_INLINE
        InlineTarget.CALLEE, // CALLEE_IS_SIZE_DECREASING_INLINE
        InlineTarget.CALLEE, // CALLEE_IS_INTRINSIC_TYPE
        InlineTarget.CALLEE, // CALLEE_LOG_REPLAY_ACCEPT
        InlineTarget.CALLEE, // CALLEE_LOOKS_LIKE_WRAPPER
        InlineTarget.CALLEE, // CALLEE_MAXSTACK
        InlineTarget.CALLEE, // CALLEE_MAY_RETURN_SMALL_ARRAY
        InlineTarget.CALLEE, // CALLEE_OPCODE
        InlineTarget.CALLEE, // CALLEE_OPCODE_NORMED
        InlineTarget.CALLEE, // CALLEE_NUMBER_OF_ARGUMENTS
        InlineTarget.CALLEE, // CALLEE_NUMBER_OF_BASIC_BLOCKS
        InlineTarget.CALLEE, // CALLEE_NUMBER_OF_LOCALS
        InlineTarget.CALLEE, // CALLEE_RANDOM_ACCEPT
        InlineTarget.CALLEE, // CALLEE_UNBOX_ARG
        InlineTarget.CALLEE, // CALLEE_UNSUPPORTED_OPCODE
        InlineTarget.CALLER, // CALLER_DEBUG_CODEGEN
        InlineTarget.CALLER, // CALLER_IS_JIT_NOINLINE
        InlineTarget.CALLER, // CALLER_USES_NEXT_CALL_RET_ADDR
        InlineTarget.CALLER, // CALLER_ASYNC_USED_CONTINUATION
        InlineTarget.CALLER, // CALLER_HAS_NEWARRAY
        InlineTarget.CALLER, // CALLER_HAS_NEWOBJ
        InlineTarget.CALLSITE, // CALLSITE_ARG_HAS_NULL_THIS
        InlineTarget.CALLSITE, // CALLSITE_ARG_NO_BASH_TO_INT
        InlineTarget.CALLSITE, // CALLSITE_ARG_NO_BASH_TO_REF
        InlineTarget.CALLSITE, // CALLSITE_ARG_TYPES_INCOMPATIBLE
        InlineTarget.CALLSITE, // CALLSITE_CANT_CLASS_INIT
        InlineTarget.CALLSITE, // CALLSITE_COMPILATION_ERROR
        InlineTarget.CALLSITE, // CALLSITE_COMPILATION_FAILURE
        InlineTarget.CALLSITE, // CALLSITE_EXPLICIT_TAIL_PREFIX
        InlineTarget.CALLSITE, // CALLSITE_EH_TABLE_FULL
        InlineTarget.CALLSITE, // CALLSITE_GENERIC_DICTIONARY_LOOKUP
        InlineTarget.CALLSITE, // CALLSITE_HAS_COMPLEX_HANDLE
        InlineTarget.CALLSITE, // CALLSITE_IMPLICIT_REC_TAIL_CALL
        InlineTarget.CALLSITE, // CALLSITE_IS_CALL_TO_HELPER
        InlineTarget.CALLSITE, // CALLSITE_IS_NOT_DIRECT
        InlineTarget.CALLSITE, // CALLSITE_IS_NOT_DIRECT_MANAGED
        InlineTarget.CALLSITE, // CALLSITE_IS_RECURSIVE
        InlineTarget.CALLSITE, // CALLSITE_IS_TOO_DEEP
        InlineTarget.CALLSITE, // CALLSITE_IS_VIRTUAL
        InlineTarget.CALLSITE, // CALLSITE_IS_VM_NOINLINE
        InlineTarget.CALLSITE, // CALLSITE_IS_WITHIN_CATCH
        InlineTarget.CALLSITE, // CALLSITE_IS_WITHIN_FILTER
        InlineTarget.CALLSITE, // CALLSITE_LDARGA_NOT_LOCAL_VAR
        InlineTarget.CALLSITE, // CALLSITE_LDFLD_NEEDS_HELPER
        InlineTarget.CALLSITE, // CALLSITE_LDVIRTFN_ON_NON_VIRTUAL
        InlineTarget.CALLSITE, // CALLSITE_LOCALLOC_IN_LOOP
        InlineTarget.CALLSITE, // CALLSITE_LOCALLOC_SIZE_UNKNOWN
        InlineTarget.CALLSITE, // CALLSITE_LOG_REPLAY_REJECT
        InlineTarget.CALLSITE, // CALLSITE_NOT_CANDIDATE
        InlineTarget.CALLSITE, // CALLSITE_NOT_PROFITABLE_INLINE
        InlineTarget.CALLSITE, // CALLSITE_OVER_BUDGET
        InlineTarget.CALLSITE, // CALLSITE_OVER_INLINE_LIMIT
        InlineTarget.CALLSITE, // CALLSITE_PIN_IN_TRY_REGION
        InlineTarget.CALLSITE, // CALLSITE_RANDOM_REJECT
        InlineTarget.CALLSITE, // CALLSITE_RETURN_TYPE_MISMATCH
        InlineTarget.CALLSITE, // CALLSITE_STFLD_NEEDS_HELPER
        InlineTarget.CALLSITE, // CALLSITE_TOO_MANY_LOCALS
        InlineTarget.CALLSITE, // CALLSITE_PINVOKE_EH
        InlineTarget.CALLSITE, // CALLSITE_RARE_GC_STRUCT
        InlineTarget.CALLSITE, // CALLSITE_NONGENERIC_CALLS_GENERIC
        InlineTarget.CALLSITE, // CALLSITE_ARG_EXACT_CLS
        InlineTarget.CALLSITE, // CALLSITE_ARG_EXACT_CLS_SIG_IS_NOT
        InlineTarget.CALLSITE, // CALLSITE_ARG_CONST
        InlineTarget.CALLSITE, // CALLSITE_ARG_BOXED
        InlineTarget.CALLSITE, // CALLSITE_FOLDABLE_INTRINSIC
        InlineTarget.CALLSITE, // CALLSITE_FOLDABLE_EXPR
        InlineTarget.CALLSITE, // CALLSITE_FOLDABLE_EXPR_UN
        InlineTarget.CALLSITE, // CALLSITE_FOLDABLE_BRANCH
        InlineTarget.CALLSITE, // CALLSITE_FOLDABLE_SWITCH
        InlineTarget.CALLSITE, // CALLSITE_UNROLLABLE_MEMOP
        InlineTarget.CALLSITE, // CALLSITE_DIV_BY_CNS
        InlineTarget.CALLSITE, // CALLSITE_CONSTANT_ARG_FEEDS_TEST
        InlineTarget.CALLSITE, // CALLSITE_DEPTH
        InlineTarget.CALLSITE, // CALLSITE_FREQUENCY
        InlineTarget.CALLSITE, // CALLSITE_HAS_PROFILE_WEIGHTS
        InlineTarget.CALLSITE, // CALLSITE_INSIDE_THROW_BLOCK
        InlineTarget.CALLSITE, // CALLSITE_IN_LOOP
        InlineTarget.CALLSITE, // CALLSITE_IN_TRY_REGION
        InlineTarget.CALLSITE, // CALLSITE_IN_NORETURN_REGION
        InlineTarget.CALLSITE, // CALLSITE_IS_PROFITABLE_INLINE
        InlineTarget.CALLSITE, // CALLSITE_IS_SAME_THIS
        InlineTarget.CALLSITE, // CALLSITE_IS_SIZE_DECREASING_INLINE
        InlineTarget.CALLSITE, // CALLSITE_LOG_REPLAY_ACCEPT
        InlineTarget.CALLSITE, // CALLSITE_PROFILE_FREQUENCY
        InlineTarget.CALLSITE, // CALLSITE_RANDOM_ACCEPT
        InlineTarget.CALLSITE, // CALLSITE_WEIGHT
        InlineTarget.CALLSITE, // CALLSITE_UNBOX_EXACT_ARG
        InlineTarget.CALLEE, // CALLEE_UNUSED_FINAL
    ];
}