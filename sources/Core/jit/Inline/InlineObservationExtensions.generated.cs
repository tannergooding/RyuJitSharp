// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public static partial class InlineObservationExtensions
{
    private static string GetDescription(InlineObservation observation) => observation switch
    {
        InlineObservation.CALLEE_UNUSED_INITIAL => "unused initial observation",
        InlineObservation.CALLEE_BAD_ARGUMENT_NUMBER => "invalid argument number",
        InlineObservation.CALLEE_BAD_LOCAL_NUMBER => "invalid local number",
        InlineObservation.CALLEE_COMPILATION_ERROR => "compilation error",
        InlineObservation.CALLEE_EXPLICIT_TAIL_PREFIX => "explicit tail prefix in callee",
        InlineObservation.CALLEE_HAS_EH => "has exception handling",
        InlineObservation.CALLEE_HAS_ENDFILTER => "has endfilter",
        InlineObservation.CALLEE_HAS_ENDFINALLY => "has endfinally",
        InlineObservation.CALLEE_HAS_LEAVE => "has leave",
        InlineObservation.CALLEE_HAS_MANAGED_VARARGS => "managed varargs",
        InlineObservation.CALLEE_HAS_NATIVE_VARARGS => "native varargs",
        InlineObservation.CALLEE_HAS_NO_BODY => "has no body",
        InlineObservation.CALLEE_HAS_NULL_FOR_LDELEM => "has null pointer for ldelem",
        InlineObservation.CALLEE_HAS_UNMANAGED_CALLCONV => "has unmanaged calling convention",
        InlineObservation.CALLEE_IS_ARRAY_METHOD => "is array method",
        InlineObservation.CALLEE_IS_JIT_NOINLINE => "noinline per JitNoinline",
        InlineObservation.CALLEE_IS_NOINLINE => "noinline per IL/cached result",
        InlineObservation.CALLEE_IS_SYNCHRONIZED => "is synchronized",
        InlineObservation.CALLEE_IS_VM_NOINLINE => "noinline per VM",
        InlineObservation.CALLSITE_LACKS_RETURN => "no return expression",
        InlineObservation.CALLEE_LDFLD_NEEDS_HELPER => "ldfld needs helper",
        InlineObservation.CALLEE_LOCALLOC_TOO_LARGE => "localloc size too large",
        InlineObservation.CALLEE_LOG_REPLAY_REJECT => "rejected by log replay",
        InlineObservation.CALLEE_MARKED_AS_SKIPPED => "skipped by complus request",
        InlineObservation.CALLEE_MAXSTACK_TOO_BIG => "maxstack too big",
        InlineObservation.CALLEE_NO_METHOD_INFO => "cannot get method info",
        InlineObservation.CALLEE_NOT_PROFITABLE_INLINE => "unprofitable inline",
        InlineObservation.CALLEE_RANDOM_REJECT => "random reject",
        InlineObservation.CALLEE_STACK_CRAWL_MARK => "uses stack crawl mark",
        InlineObservation.CALLEE_STFLD_NEEDS_HELPER => "stfld needs helper",
        InlineObservation.CALLEE_TOO_MANY_ARGUMENTS => "too many arguments",
        InlineObservation.CALLEE_TOO_MANY_LOCALS => "too many locals",
        InlineObservation.CALLEE_AWAIT => "has await",
        InlineObservation.CALLEE_AWAIT_IN_TRY => "has await inside try region",
        InlineObservation.CALLEE_ASYNC_SUSPEND => "has async suspend",
        InlineObservation.CALLEE_LDFLD_STATIC_VALUECLASS => "ldsfld of value class",
        InlineObservation.CALLEE_TOO_MANY_BASIC_BLOCKS => "too many basic blocks",
        InlineObservation.CALLEE_TOO_MUCH_IL => "too many il bytes",
        InlineObservation.CALLEE_ARG_FEEDS_CONSTANT_TEST => "argument feeds constant test",
        InlineObservation.CALLEE_ARG_FEEDS_TEST => "argument feeds test",
        InlineObservation.CALLEE_ARG_FEEDS_CAST => "argument feeds castclass or isinst",
        InlineObservation.CALLEE_ARG_FEEDS_RANGE_CHECK => "argument feeds range check",
        InlineObservation.CALLEE_ARG_FEEDS_ISCONST => "argument feeds IsKnownConstant",
        InlineObservation.CALLEE_CONST_ARG_FEEDS_ISCONST => "const argument feeds IsKnownConstant",
        InlineObservation.CALLEE_ARG_STRUCT => "arg is a struct passed by value",
        InlineObservation.CALLEE_RETURNS_STRUCT => "returns a struct by value",
        InlineObservation.CALLEE_ARG_STRUCT_FIELD_ACCESS => "ldfld/stfld over arg (struct)",
        InlineObservation.CALLEE_BINARY_EXRP_WITH_CNS => "'X op CNS' pattern",
        InlineObservation.CALLEE_BEGIN_OPCODE_SCAN => "prepare to look at opcodes",
        InlineObservation.CALLEE_BELOW_ALWAYS_INLINE_SIZE => "below ALWAYS_INLINE size",
        InlineObservation.CALLEE_CLASS_PROMOTABLE => "promotable value class",
        InlineObservation.CALLEE_CLASS_VALUETYPE => "value class",
        InlineObservation.CALLEE_FOLDABLE_BOX => "foldable box/unbox operation",
        InlineObservation.CALLEE_INTRINSIC => "call marked as intrinsic",
        InlineObservation.CALLEE_BACKWARD_JUMP => "backward jump",
        InlineObservation.CALLEE_THROW_BLOCK => "throw block",
        InlineObservation.CALLEE_DOES_NOT_RETURN => "does not return",
        InlineObservation.CALLEE_END_OPCODE_SCAN => "done looking at opcodes",
        InlineObservation.CALLEE_HAS_GC_STRUCT => "has gc field in struct local",
        InlineObservation.CALLEE_HAS_LOCALLOC => "has localloc",
        InlineObservation.CALLEE_HAS_PINNED_LOCALS => "has pinned locals",
        InlineObservation.CALLEE_HAS_SIMD => "has SIMD arg, local, or ret",
        InlineObservation.CALLEE_HAS_SWITCH => "has switch",
        InlineObservation.CALLEE_IL_CODE_SIZE => "number of bytes of IL",
        InlineObservation.CALLEE_IS_CLASS_CTOR => "class constructor",
        InlineObservation.CALLEE_IS_DISCRETIONARY_INLINE => "can inline, check heuristics",
        InlineObservation.CALLEE_IS_FORCE_INLINE => "aggressive inline attribute",
        InlineObservation.CALLEE_IS_INSTANCE_CTOR => "instance constructor",
        InlineObservation.CALLEE_IS_PROFITABLE_INLINE => "profitable inline",
        InlineObservation.CALLEE_IS_SIZE_DECREASING_INLINE => "size decreasing inline",
        InlineObservation.CALLEE_IS_INTRINSIC_TYPE => "callee class marked as Intrinsic",
        InlineObservation.CALLEE_IS_ASYNC => "is an async call",
        InlineObservation.CALLEE_LOG_REPLAY_ACCEPT => "accepted by log replay",
        InlineObservation.CALLEE_LOOKS_LIKE_WRAPPER => "thin wrapper around a call",
        InlineObservation.CALLEE_MAXSTACK => "maxstack",
        InlineObservation.CALLEE_MAY_RETURN_SMALL_ARRAY => "may return a small new array",
        InlineObservation.CALLEE_OPCODE => "next opcode in IL stream",
        InlineObservation.CALLEE_OPCODE_NORMED => "next opcode in IL stream",
        InlineObservation.CALLEE_NUMBER_OF_ARGUMENTS => "number of arguments",
        InlineObservation.CALLEE_NUMBER_OF_BASIC_BLOCKS => "number of basic blocks",
        InlineObservation.CALLEE_NUMBER_OF_LOCALS => "number of locals",
        InlineObservation.CALLEE_RANDOM_ACCEPT => "random accept",
        InlineObservation.CALLEE_UNBOX_ARG => "callee unboxes arg",
        InlineObservation.CALLEE_UNSUPPORTED_OPCODE => "unsupported opcode",
        InlineObservation.CALLER_DEBUG_CODEGEN => "debug codegen",
        InlineObservation.CALLER_IS_JIT_NOINLINE => "noinline per JitNoInlineRange",
        InlineObservation.CALLER_USES_NEXT_CALL_RET_ADDR => "uses NextCallReturnAddress intrinsic",
        InlineObservation.CALLER_ASYNC_USED_CONTINUATION => "uses AsyncCallContinuation intrinsic",
        InlineObservation.CALLER_HAS_NEWARRAY => "has newarray",
        InlineObservation.CALLER_HAS_NEWOBJ => "has newobj",
        InlineObservation.CALLSITE_ARG_HAS_NULL_THIS => "this pointer argument is null",
        InlineObservation.CALLSITE_ARG_NO_BASH_TO_INT => "argument can't bash to int",
        InlineObservation.CALLSITE_ARG_NO_BASH_TO_REF => "argument can't bash to ref",
        InlineObservation.CALLSITE_ARG_TYPES_INCOMPATIBLE => "argument types incompatible",
        InlineObservation.CALLSITE_AWAIT_IN_NON_ASYNC_ROOT => "has await, root method is not async",
        InlineObservation.CALLSITE_AWAIT_IN_TRY_REGION => "within try region, has await",
        InlineObservation.CALLSITE_CANT_CLASS_INIT => "can't class init",
        InlineObservation.CALLSITE_COMPILATION_ERROR => "compilation error",
        InlineObservation.CALLSITE_COMPILATION_FAILURE => "failed to compile",
        InlineObservation.CALLSITE_EXPLICIT_TAIL_PREFIX => "explicit tail prefix",
        InlineObservation.CALLSITE_EH_TABLE_FULL => "callee has eh, eh table is full",
        InlineObservation.CALLSITE_GENERIC_DICTIONARY_LOOKUP => "runtime dictionary lookup",
        InlineObservation.CALLSITE_HAS_COMPLEX_HANDLE => "complex handle access",
        InlineObservation.CALLSITE_IMPLICIT_REC_TAIL_CALL => "implicit recursive tail call",
        InlineObservation.CALLSITE_IS_CALL_TO_HELPER => "target is helper",
        InlineObservation.CALLSITE_IS_NOT_DIRECT => "target not direct",
        InlineObservation.CALLSITE_IS_NOT_DIRECT_MANAGED => "target not direct managed",
        InlineObservation.CALLSITE_IS_RECURSIVE => "recursive",
        InlineObservation.CALLSITE_IS_TOO_DEEP => "too deep",
        InlineObservation.CALLSITE_IS_VIRTUAL => "virtual",
        InlineObservation.CALLSITE_IS_VM_NOINLINE => "noinline per VM",
        InlineObservation.CALLSITE_IS_WITHIN_CATCH => "within catch region",
        InlineObservation.CALLSITE_IS_WITHIN_FILTER => "within filter region",
        InlineObservation.CALLSITE_LDARGA_NOT_LOCAL_VAR => "ldarga not on local var",
        InlineObservation.CALLSITE_LDFLD_NEEDS_HELPER => "ldfld needs helper",
        InlineObservation.CALLSITE_LDVIRTFN_ON_NON_VIRTUAL => "ldvirtfn on non-virtual",
        InlineObservation.CALLSITE_LOCALLOC_IN_LOOP => "within loop, has localloc",
        InlineObservation.CALLSITE_LOCALLOC_SIZE_UNKNOWN => "localloc size unknown",
        InlineObservation.CALLSITE_LOG_REPLAY_REJECT => "rejected by log replay",
        InlineObservation.CALLSITE_NOT_CANDIDATE => "not inline candidate",
        InlineObservation.CALLSITE_NOT_PROFITABLE_INLINE => "unprofitable inline",
        InlineObservation.CALLSITE_OVER_BUDGET => "inline exceeds budget",
        InlineObservation.CALLSITE_OVER_INLINE_LIMIT => "limited by JitInlineLimit",
        InlineObservation.CALLSITE_PIN_IN_TRY_REGION => "within try region, pinned",
        InlineObservation.CALLSITE_RANDOM_REJECT => "random reject",
        InlineObservation.CALLSITE_RETURN_TYPE_MISMATCH => "return type mismatch",
        InlineObservation.CALLSITE_STFLD_NEEDS_HELPER => "stfld needs helper",
        InlineObservation.CALLSITE_TOO_MANY_LOCALS => "too many locals",
        InlineObservation.CALLSITE_PINVOKE_EH => "PInvoke call site with EH",
        InlineObservation.CALLSITE_RARE_GC_STRUCT => "rarely called, has gc struct",
        InlineObservation.CALLSITE_NONGENERIC_CALLS_GENERIC => "callee is generic and caller is not",
        InlineObservation.CALLSITE_ARG_EXACT_CLS => "arg is of an exact class",
        InlineObservation.CALLSITE_ARG_EXACT_CLS_SIG_IS_NOT => "arg is more concrete than in sig.",
        InlineObservation.CALLSITE_ARG_CONST => "arg is a constant",
        InlineObservation.CALLSITE_ARG_BOXED => "arg is boxed at call site",
        InlineObservation.CALLSITE_ASYNC_STRESS_INDEX => "async inlining stress index",
        InlineObservation.CALLSITE_FOLDABLE_INTRINSIC => "foldable intrinsic",
        InlineObservation.CALLSITE_FOLDABLE_EXPR => "foldable binary expression",
        InlineObservation.CALLSITE_FOLDABLE_EXPR_UN => "foldable unary expression",
        InlineObservation.CALLSITE_FOLDABLE_BRANCH => "foldable branch",
        InlineObservation.CALLSITE_FOLDABLE_SWITCH => "foldable switch",
        InlineObservation.CALLSITE_UNROLLABLE_MEMOP => "unrollable memmove/memcmp",
        InlineObservation.CALLSITE_DIV_BY_CNS => "dividy by const",
        InlineObservation.CALLSITE_CONSTANT_ARG_FEEDS_TEST => "constant argument feeds test",
        InlineObservation.CALLSITE_DEPTH => "depth",
        InlineObservation.CALLSITE_FREQUENCY => "rough call site frequency",
        InlineObservation.CALLSITE_HAS_PROFILE_WEIGHTS => "profile weights are available",
        InlineObservation.CALLSITE_INSIDE_THROW_BLOCK => "inside throw block",
        InlineObservation.CALLSITE_IN_LOOP => "call site is in a loop",
        InlineObservation.CALLSITE_IN_TRY_REGION => "call site is in a try region",
        InlineObservation.CALLSITE_IN_NORETURN_REGION => "call site is in a no-return region",
        InlineObservation.CALLSITE_IS_PROFITABLE_INLINE => "profitable inline",
        InlineObservation.CALLSITE_IS_SAME_THIS => "same this as root caller",
        InlineObservation.CALLSITE_IS_SIZE_DECREASING_INLINE => "size decreasing inline",
        InlineObservation.CALLSITE_LOG_REPLAY_ACCEPT => "accepted by log replay",
        InlineObservation.CALLSITE_PROFILE_FREQUENCY => "frequency from profile data",
        InlineObservation.CALLSITE_RANDOM_ACCEPT => "random accept",
        InlineObservation.CALLSITE_WEIGHT => "frequency from block weight",
        InlineObservation.CALLSITE_UNBOX_EXACT_ARG => "unbox of arg with exact class",
        InlineObservation.CALLEE_UNUSED_FINAL => "unused final observation",
        _ => throw new FatalJitException("Invalid inline observation."),
    };

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
        InlineImpact.FATAL, // CALLSITE_LACKS_RETURN
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
        InlineImpact.FATAL, // CALLEE_AWAIT_IN_TRY
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
        InlineImpact.INFORMATION, // CALLEE_IS_ASYNC
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
        InlineImpact.FATAL, // CALLSITE_AWAIT_IN_NON_ASYNC_ROOT
        InlineImpact.FATAL, // CALLSITE_AWAIT_IN_TRY_REGION
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
        InlineImpact.INFORMATION, // CALLSITE_ASYNC_STRESS_INDEX
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
        InlineTarget.CALLSITE, // CALLSITE_LACKS_RETURN
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
        InlineTarget.CALLEE, // CALLEE_AWAIT_IN_TRY
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
        InlineTarget.CALLEE, // CALLEE_IS_ASYNC
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
        InlineTarget.CALLSITE, // CALLSITE_AWAIT_IN_NON_ASYNC_ROOT
        InlineTarget.CALLSITE, // CALLSITE_AWAIT_IN_TRY_REGION
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
        InlineTarget.CALLSITE, // CALLSITE_ASYNC_STRESS_INDEX
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