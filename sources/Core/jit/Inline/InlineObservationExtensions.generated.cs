// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public static partial class InlineObservationExtensions
{
    private static string GetDescription(InlineObservation observation)
    {
        switch (observation)
        {
            case InlineObservation.CALLEE_UNUSED_INITIAL:
            {
                return "unused initial observation";
            }
            case InlineObservation.CALLEE_BAD_ARGUMENT_NUMBER:
            {
                return "invalid argument number";
            }
            case InlineObservation.CALLEE_BAD_LOCAL_NUMBER:
            {
                return "invalid local number";
            }
            case InlineObservation.CALLEE_COMPILATION_ERROR:
            {
                return "compilation error";
            }
            case InlineObservation.CALLEE_EXPLICIT_TAIL_PREFIX:
            {
                return "explicit tail prefix in callee";
            }
            case InlineObservation.CALLEE_HAS_EH:
            {
                return "has exception handling";
            }
            case InlineObservation.CALLEE_HAS_ENDFILTER:
            {
                return "has endfilter";
            }
            case InlineObservation.CALLEE_HAS_ENDFINALLY:
            {
                return "has endfinally";
            }
            case InlineObservation.CALLEE_HAS_LEAVE:
            {
                return "has leave";
            }
            case InlineObservation.CALLEE_HAS_MANAGED_VARARGS:
            {
                return "managed varargs";
            }
            case InlineObservation.CALLEE_HAS_NATIVE_VARARGS:
            {
                return "native varargs";
            }
            case InlineObservation.CALLEE_HAS_NO_BODY:
            {
                return "has no body";
            }
            case InlineObservation.CALLEE_HAS_NULL_FOR_LDELEM:
            {
                return "has null pointer for ldelem";
            }
            case InlineObservation.CALLEE_HAS_UNMANAGED_CALLCONV:
            {
                return "has unmanaged calling convention";
            }
            case InlineObservation.CALLEE_IS_ARRAY_METHOD:
            {
                return "is array method";
            }
            case InlineObservation.CALLEE_IS_JIT_NOINLINE:
            {
                return "noinline per JitNoinline";
            }
            case InlineObservation.CALLEE_IS_NOINLINE:
            {
                return "noinline per IL/cached result";
            }
            case InlineObservation.CALLEE_IS_SYNCHRONIZED:
            {
                return "is synchronized";
            }
            case InlineObservation.CALLEE_IS_VM_NOINLINE:
            {
                return "noinline per VM";
            }
            case InlineObservation.CALLSITE_LACKS_RETURN:
            {
                return "no return expression";
            }
            case InlineObservation.CALLEE_LDFLD_NEEDS_HELPER:
            {
                return "ldfld needs helper";
            }
            case InlineObservation.CALLEE_LOCALLOC_TOO_LARGE:
            {
                return "localloc size too large";
            }
            case InlineObservation.CALLEE_LOG_REPLAY_REJECT:
            {
                return "rejected by log replay";
            }
            case InlineObservation.CALLEE_MARKED_AS_SKIPPED:
            {
                return "skipped by complus request";
            }
            case InlineObservation.CALLEE_MAXSTACK_TOO_BIG:
            {
                return "maxstack too big";
            }
            case InlineObservation.CALLEE_NO_METHOD_INFO:
            {
                return "cannot get method info";
            }
            case InlineObservation.CALLEE_NOT_PROFITABLE_INLINE:
            {
                return "unprofitable inline";
            }
            case InlineObservation.CALLEE_RANDOM_REJECT:
            {
                return "random reject";
            }
            case InlineObservation.CALLEE_STACK_CRAWL_MARK:
            {
                return "uses stack crawl mark";
            }
            case InlineObservation.CALLEE_STFLD_NEEDS_HELPER:
            {
                return "stfld needs helper";
            }
            case InlineObservation.CALLEE_TOO_MANY_ARGUMENTS:
            {
                return "too many arguments";
            }
            case InlineObservation.CALLEE_TOO_MANY_LOCALS:
            {
                return "too many locals";
            }
            case InlineObservation.CALLEE_AWAIT:
            {
                return "has await";
            }
            case InlineObservation.CALLEE_AWAIT_IN_TRY:
            {
                return "has await inside try region";
            }
            case InlineObservation.CALLEE_ASYNC_SUSPEND:
            {
                return "has async suspend";
            }
            case InlineObservation.CALLEE_LDFLD_STATIC_VALUECLASS:
            {
                return "ldsfld of value class";
            }
            case InlineObservation.CALLEE_TOO_MANY_BASIC_BLOCKS:
            {
                return "too many basic blocks";
            }
            case InlineObservation.CALLEE_TOO_MUCH_IL:
            {
                return "too many il bytes";
            }
            case InlineObservation.CALLEE_ARG_FEEDS_CONSTANT_TEST:
            {
                return "argument feeds constant test";
            }
            case InlineObservation.CALLEE_ARG_FEEDS_TEST:
            {
                return "argument feeds test";
            }
            case InlineObservation.CALLEE_ARG_FEEDS_CAST:
            {
                return "argument feeds castclass or isinst";
            }
            case InlineObservation.CALLEE_ARG_FEEDS_RANGE_CHECK:
            {
                return "argument feeds range check";
            }
            case InlineObservation.CALLEE_ARG_FEEDS_ISCONST:
            {
                return "argument feeds IsKnownConstant";
            }
            case InlineObservation.CALLEE_CONST_ARG_FEEDS_ISCONST:
            {
                return "const argument feeds IsKnownConstant";
            }
            case InlineObservation.CALLEE_ARG_STRUCT:
            {
                return "arg is a struct passed by value";
            }
            case InlineObservation.CALLEE_RETURNS_STRUCT:
            {
                return "returns a struct by value";
            }
            case InlineObservation.CALLEE_ARG_STRUCT_FIELD_ACCESS:
            {
                return "ldfld/stfld over arg (struct)";
            }
            case InlineObservation.CALLEE_BINARY_EXRP_WITH_CNS:
            {
                return "'X op CNS' pattern";
            }
            case InlineObservation.CALLEE_BEGIN_OPCODE_SCAN:
            {
                return "prepare to look at opcodes";
            }
            case InlineObservation.CALLEE_BELOW_ALWAYS_INLINE_SIZE:
            {
                return "below ALWAYS_INLINE size";
            }
            case InlineObservation.CALLEE_CLASS_PROMOTABLE:
            {
                return "promotable value class";
            }
            case InlineObservation.CALLEE_CLASS_VALUETYPE:
            {
                return "value class";
            }
            case InlineObservation.CALLEE_FOLDABLE_BOX:
            {
                return "foldable box/unbox operation";
            }
            case InlineObservation.CALLEE_INTRINSIC:
            {
                return "call marked as intrinsic";
            }
            case InlineObservation.CALLEE_BACKWARD_JUMP:
            {
                return "backward jump";
            }
            case InlineObservation.CALLEE_THROW_BLOCK:
            {
                return "throw block";
            }
            case InlineObservation.CALLEE_DOES_NOT_RETURN:
            {
                return "does not return";
            }
            case InlineObservation.CALLEE_END_OPCODE_SCAN:
            {
                return "done looking at opcodes";
            }
            case InlineObservation.CALLEE_HAS_GC_STRUCT:
            {
                return "has gc field in struct local";
            }
            case InlineObservation.CALLEE_HAS_LOCALLOC:
            {
                return "has localloc";
            }
            case InlineObservation.CALLEE_HAS_PINNED_LOCALS:
            {
                return "has pinned locals";
            }
            case InlineObservation.CALLEE_HAS_SIMD:
            {
                return "has SIMD arg, local, or ret";
            }
            case InlineObservation.CALLEE_HAS_SWITCH:
            {
                return "has switch";
            }
            case InlineObservation.CALLEE_IL_CODE_SIZE:
            {
                return "number of bytes of IL";
            }
            case InlineObservation.CALLEE_IS_CLASS_CTOR:
            {
                return "class constructor";
            }
            case InlineObservation.CALLEE_IS_DISCRETIONARY_INLINE:
            {
                return "can inline, check heuristics";
            }
            case InlineObservation.CALLEE_IS_FORCE_INLINE:
            {
                return "aggressive inline attribute";
            }
            case InlineObservation.CALLEE_IS_INSTANCE_CTOR:
            {
                return "instance constructor";
            }
            case InlineObservation.CALLEE_IS_PROFITABLE_INLINE:
            {
                return "profitable inline";
            }
            case InlineObservation.CALLEE_IS_SIZE_DECREASING_INLINE:
            {
                return "size decreasing inline";
            }
            case InlineObservation.CALLEE_IS_INTRINSIC_TYPE:
            {
                return "callee class marked as Intrinsic";
            }
            case InlineObservation.CALLEE_IS_ASYNC:
            {
                return "is an async call";
            }
            case InlineObservation.CALLEE_LOG_REPLAY_ACCEPT:
            {
                return "accepted by log replay";
            }
            case InlineObservation.CALLEE_LOOKS_LIKE_WRAPPER:
            {
                return "thin wrapper around a call";
            }
            case InlineObservation.CALLEE_MAXSTACK:
            {
                return "maxstack";
            }
            case InlineObservation.CALLEE_MAY_RETURN_SMALL_ARRAY:
            {
                return "may return a small new array";
            }
            case InlineObservation.CALLEE_OPCODE:
            {
                return "next opcode in IL stream";
            }
            case InlineObservation.CALLEE_OPCODE_NORMED:
            {
                return "next opcode in IL stream";
            }
            case InlineObservation.CALLEE_NUMBER_OF_ARGUMENTS:
            {
                return "number of arguments";
            }
            case InlineObservation.CALLEE_NUMBER_OF_BASIC_BLOCKS:
            {
                return "number of basic blocks";
            }
            case InlineObservation.CALLEE_NUMBER_OF_LOCALS:
            {
                return "number of locals";
            }
            case InlineObservation.CALLEE_RANDOM_ACCEPT:
            {
                return "random accept";
            }
            case InlineObservation.CALLEE_UNBOX_ARG:
            {
                return "callee unboxes arg";
            }
            case InlineObservation.CALLEE_UNSUPPORTED_OPCODE:
            {
                return "unsupported opcode";
            }
            case InlineObservation.CALLER_DEBUG_CODEGEN:
            {
                return "debug codegen";
            }
            case InlineObservation.CALLER_IS_JIT_NOINLINE:
            {
                return "noinline per JitNoInlineRange";
            }
            case InlineObservation.CALLER_USES_NEXT_CALL_RET_ADDR:
            {
                return "uses NextCallReturnAddress intrinsic";
            }
            case InlineObservation.CALLER_ASYNC_USED_CONTINUATION:
            {
                return "uses AsyncCallContinuation intrinsic";
            }
            case InlineObservation.CALLER_HAS_NEWARRAY:
            {
                return "has newarray";
            }
            case InlineObservation.CALLER_HAS_NEWOBJ:
            {
                return "has newobj";
            }
            case InlineObservation.CALLSITE_ARG_HAS_NULL_THIS:
            {
                return "this pointer argument is null";
            }
            case InlineObservation.CALLSITE_ARG_NO_BASH_TO_INT:
            {
                return "argument can't bash to int";
            }
            case InlineObservation.CALLSITE_ARG_NO_BASH_TO_REF:
            {
                return "argument can't bash to ref";
            }
            case InlineObservation.CALLSITE_ARG_TYPES_INCOMPATIBLE:
            {
                return "argument types incompatible";
            }
            case InlineObservation.CALLSITE_AWAIT_IN_NON_ASYNC_ROOT:
            {
                return "has await, root method is not async";
            }
            case InlineObservation.CALLSITE_AWAIT_IN_TRY_REGION:
            {
                return "within try region, has await";
            }
            case InlineObservation.CALLSITE_CANT_CLASS_INIT:
            {
                return "can't class init";
            }
            case InlineObservation.CALLSITE_COMPILATION_ERROR:
            {
                return "compilation error";
            }
            case InlineObservation.CALLSITE_COMPILATION_FAILURE:
            {
                return "failed to compile";
            }
            case InlineObservation.CALLSITE_EXPLICIT_TAIL_PREFIX:
            {
                return "explicit tail prefix";
            }
            case InlineObservation.CALLSITE_EH_TABLE_FULL:
            {
                return "callee has eh, eh table is full";
            }
            case InlineObservation.CALLSITE_GENERIC_DICTIONARY_LOOKUP:
            {
                return "runtime dictionary lookup";
            }
            case InlineObservation.CALLSITE_HAS_COMPLEX_HANDLE:
            {
                return "complex handle access";
            }
            case InlineObservation.CALLSITE_IMPLICIT_REC_TAIL_CALL:
            {
                return "implicit recursive tail call";
            }
            case InlineObservation.CALLSITE_IS_CALL_TO_HELPER:
            {
                return "target is helper";
            }
            case InlineObservation.CALLSITE_IS_NOT_DIRECT:
            {
                return "target not direct";
            }
            case InlineObservation.CALLSITE_IS_NOT_DIRECT_MANAGED:
            {
                return "target not direct managed";
            }
            case InlineObservation.CALLSITE_IS_RECURSIVE:
            {
                return "recursive";
            }
            case InlineObservation.CALLSITE_IS_TOO_DEEP:
            {
                return "too deep";
            }
            case InlineObservation.CALLSITE_IS_VIRTUAL:
            {
                return "virtual";
            }
            case InlineObservation.CALLSITE_IS_VM_NOINLINE:
            {
                return "noinline per VM";
            }
            case InlineObservation.CALLSITE_IS_WITHIN_CATCH:
            {
                return "within catch region";
            }
            case InlineObservation.CALLSITE_IS_WITHIN_FILTER:
            {
                return "within filter region";
            }
            case InlineObservation.CALLSITE_LDARGA_NOT_LOCAL_VAR:
            {
                return "ldarga not on local var";
            }
            case InlineObservation.CALLSITE_LDFLD_NEEDS_HELPER:
            {
                return "ldfld needs helper";
            }
            case InlineObservation.CALLSITE_LDVIRTFN_ON_NON_VIRTUAL:
            {
                return "ldvirtfn on non-virtual";
            }
            case InlineObservation.CALLSITE_LOCALLOC_IN_LOOP:
            {
                return "within loop, has localloc";
            }
            case InlineObservation.CALLSITE_LOCALLOC_SIZE_UNKNOWN:
            {
                return "localloc size unknown";
            }
            case InlineObservation.CALLSITE_LOG_REPLAY_REJECT:
            {
                return "rejected by log replay";
            }
            case InlineObservation.CALLSITE_NOT_CANDIDATE:
            {
                return "not inline candidate";
            }
            case InlineObservation.CALLSITE_NOT_PROFITABLE_INLINE:
            {
                return "unprofitable inline";
            }
            case InlineObservation.CALLSITE_OVER_BUDGET:
            {
                return "inline exceeds budget";
            }
            case InlineObservation.CALLSITE_OVER_INLINE_LIMIT:
            {
                return "limited by JitInlineLimit";
            }
            case InlineObservation.CALLSITE_PIN_IN_TRY_REGION:
            {
                return "within try region, pinned";
            }
            case InlineObservation.CALLSITE_RANDOM_REJECT:
            {
                return "random reject";
            }
            case InlineObservation.CALLSITE_RETURN_TYPE_MISMATCH:
            {
                return "return type mismatch";
            }
            case InlineObservation.CALLSITE_STFLD_NEEDS_HELPER:
            {
                return "stfld needs helper";
            }
            case InlineObservation.CALLSITE_TOO_MANY_LOCALS:
            {
                return "too many locals";
            }
            case InlineObservation.CALLSITE_PINVOKE_EH:
            {
                return "PInvoke call site with EH";
            }
            case InlineObservation.CALLSITE_RARE_GC_STRUCT:
            {
                return "rarely called, has gc struct";
            }
            case InlineObservation.CALLSITE_NONGENERIC_CALLS_GENERIC:
            {
                return "callee is generic and caller is not";
            }
            case InlineObservation.CALLSITE_ARG_EXACT_CLS:
            {
                return "arg is of an exact class";
            }
            case InlineObservation.CALLSITE_ARG_EXACT_CLS_SIG_IS_NOT:
            {
                return "arg is more concrete than in sig.";
            }
            case InlineObservation.CALLSITE_ARG_CONST:
            {
                return "arg is a constant";
            }
            case InlineObservation.CALLSITE_ARG_BOXED:
            {
                return "arg is boxed at call site";
            }
            case InlineObservation.CALLSITE_ASYNC_STRESS_INDEX:
            {
                return "async inlining stress index";
            }
            case InlineObservation.CALLSITE_FOLDABLE_INTRINSIC:
            {
                return "foldable intrinsic";
            }
            case InlineObservation.CALLSITE_FOLDABLE_EXPR:
            {
                return "foldable binary expression";
            }
            case InlineObservation.CALLSITE_FOLDABLE_EXPR_UN:
            {
                return "foldable unary expression";
            }
            case InlineObservation.CALLSITE_FOLDABLE_BRANCH:
            {
                return "foldable branch";
            }
            case InlineObservation.CALLSITE_FOLDABLE_SWITCH:
            {
                return "foldable switch";
            }
            case InlineObservation.CALLSITE_UNROLLABLE_MEMOP:
            {
                return "unrollable memmove/memcmp";
            }
            case InlineObservation.CALLSITE_DIV_BY_CNS:
            {
                return "dividy by const";
            }
            case InlineObservation.CALLSITE_CONSTANT_ARG_FEEDS_TEST:
            {
                return "constant argument feeds test";
            }
            case InlineObservation.CALLSITE_DEPTH:
            {
                return "depth";
            }
            case InlineObservation.CALLSITE_FREQUENCY:
            {
                return "rough call site frequency";
            }
            case InlineObservation.CALLSITE_HAS_PROFILE_WEIGHTS:
            {
                return "profile weights are available";
            }
            case InlineObservation.CALLSITE_INSIDE_THROW_BLOCK:
            {
                return "inside throw block";
            }
            case InlineObservation.CALLSITE_IN_LOOP:
            {
                return "call site is in a loop";
            }
            case InlineObservation.CALLSITE_IN_TRY_REGION:
            {
                return "call site is in a try region";
            }
            case InlineObservation.CALLSITE_IN_NORETURN_REGION:
            {
                return "call site is in a no-return region";
            }
            case InlineObservation.CALLSITE_IS_PROFITABLE_INLINE:
            {
                return "profitable inline";
            }
            case InlineObservation.CALLSITE_IS_SAME_THIS:
            {
                return "same this as root caller";
            }
            case InlineObservation.CALLSITE_IS_SIZE_DECREASING_INLINE:
            {
                return "size decreasing inline";
            }
            case InlineObservation.CALLSITE_LOG_REPLAY_ACCEPT:
            {
                return "accepted by log replay";
            }
            case InlineObservation.CALLSITE_PROFILE_FREQUENCY:
            {
                return "frequency from profile data";
            }
            case InlineObservation.CALLSITE_RANDOM_ACCEPT:
            {
                return "random accept";
            }
            case InlineObservation.CALLSITE_WEIGHT:
            {
                return "frequency from block weight";
            }
            case InlineObservation.CALLSITE_UNBOX_EXACT_ARG:
            {
                return "unbox of arg with exact class";
            }
            case InlineObservation.CALLEE_UNUSED_FINAL:
            {
                return "unused final observation";
            }
            default:
            {
                FatalJitException.ThrowFatalJitException(
                    CORJIT_INTERNALERROR,
                    FatalJitErrorCode.DirectFailureMessage240
                );
                return default!;
            }
        }
    }

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