// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.BasicBlockFlags;
using System;

namespace RyuJitSharp;

[Flags]
public enum BasicBlockFlags : long
{
    BBF_EMPTY = 0,

    /// <summary>Set if the basic block contains LIR (as opposed to HIR)</summary>
    BBF_IS_LIR = 1L << 0,

    /// <summary>BB marked  during optimizations</summary>
    BBF_MARKED = 1L << 1,

    /// <summary>BB has been removed from bb-list</summary>
    BBF_REMOVED = 1L << 2,

    /// <summary>BB should not be removed during flow graph optimizations</summary>
    BBF_DONT_REMOVE = 1L << 3,

    /// <summary>BB byte-code has been imported</summary>
    BBF_IMPORTED = 1L << 4,

    /// <summary>BB has been added by the compiler</summary>
    BBF_INTERNAL = 1L << 5,

    /// <summary>BB may need a GC poll because it uses the slow tail call helper</summary>
    BBF_NEEDS_GCPOLL = 1L << 6,

    /// <summary>First block of a cloned finally region</summary>
    BBF_CLONED_FINALLY_BEGIN = 1L << 7,

    /// <summary>Last block of a cloned finally region</summary>
    BBF_CLONED_FINALLY_END = 1L << 8,

    /// <summary>BB contains a call to a method with SuppressGCTransitionAttribute</summary>
    BBF_HAS_SUPPRESSGC_CALL = 1L << 9,

    /// <summary>BB needs a label</summary>
    BBF_HAS_LABEL = 1L << 10,

    /// <summary>Block is lexically the first block in a loop we intend to align.</summary>
    BBF_LOOP_ALIGN = 1L << 11,

    /// <summary>BB ends with 'align' instruction</summary>
    BBF_HAS_ALIGN = 1L << 12,

    /// <summary>BB executes a JMP instruction (instead of return)</summary>
    BBF_HAS_JMP = 1L << 13,

    /// <summary>BB has a GC safe point (e.g. a call)</summary>
    BBF_GC_SAFE_POINT = 1L << 14,

    /// <summary>Block has a multi-dimensional array reference</summary>
    BBF_HAS_MDARRAYREF = 1L << 15,

    /// <summary>BB contains 'new' of an object type.</summary>
    BBF_HAS_NEWOBJ = 1L << 16,

    /// <summary>BBJ_CALLFINALLY that will never return (and therefore, won't need a paired BBJ_CALLFINALLYRET); see isBBCallFinallyPair().</summary>
    BBF_RETLESS_CALL = 1L << 17,

    /// <summary>BB is cold</summary>
    BBF_COLD = 1L << 18,

    /// <summary>BB weight is computed from profile data</summary>
    BBF_PROF_WEIGHT = 1L << 19,

    /// <summary>A special BBJ_ALWAYS block, used by EH code generation. Keep the jump kind as BBJ_ALWAYS. Used on x86 for the final step block out of a finally.</summary>
    BBF_KEEP_BBJ_ALWAYS = 1L << 20,

    /// <summary>BB contains a call</summary>
    BBF_HAS_CALL = 1L << 21,

    /// <summary>Block is dominated by exceptional entry.</summary>
    BBF_DOMINATED_BY_EXCEPTIONAL_ENTRY = 1L << 22,

    /// <summary>BB is surrounded by a backward jump/switch arc</summary>
    BBF_BACKWARD_JUMP = 1L << 23,

    /// <summary>Block is a source of a backward jump</summary>
    BBF_BACKWARD_JUMP_SOURCE = 1L << 24,

    /// <summary>Block is a target of a backward jump</summary>
    BBF_BACKWARD_JUMP_TARGET = 1L << 25,

    /// <summary>Block is a patchpoint</summary>
    BBF_OSR_PATCHPOINT = 1L << 26,

    /// <summary>Block is a partial compilation patchpoint</summary>
    BBF_PARTIAL_COMPILATION_PATCHPOINT = 1L << 27,

    /// <summary>BB contains a call needing a histogram profile</summary>
    BBF_HAS_HISTOGRAM_PROFILE = 1L << 28,

    /// <summary>BB has pred that has potential tail call</summary>
    BBF_TAILCALL_SUCCESSOR = 1L << 29,

    /// <summary>Block has recursive tailcall that may turn into a loop</summary>
    BBF_RECURSIVE_TAILCALL = 1L << 30,

    /// <summary>Block should kill off any incoming CSE</summary>
    BBF_NO_CSE_IN = 1L << 31,

    /// <summary>Ok to add pred edge to this block, even when "safe" edge creation disabled</summary>
    BBF_CAN_ADD_PRED = 1L << 32,

    /// <summary>Block has a node that needs a value probing</summary>
    BBF_HAS_VALUE_PROFILE = 1L << 33,

    /// <summary>BB contains 'new' of an array type.</summary>
    BBF_HAS_NEWARR = 1L << 34,

    /// <summary>BB *likely* has a bounds check (after rangecheck phase).</summary>
    BBF_MAY_HAVE_BOUNDS_CHECKS = 1L << 35,

    /// <summary>Block is a resumption block in an async method</summary>
    BBF_ASYNC_RESUMPTION = 1L << 36,

    /// <summary>Block is a resumption from a catch</summary>
    BBF_CATCH_RESUMPTION = 1L << 37,

    /// <summary>Block is a call to a throw helper</summary>
    BBF_THROW_HELPER = 1L << 38,

    /// <summary>Flags to update when two blocks are compacted</summary>
    BBF_COMPACT_UPD = BBF_GC_SAFE_POINT | BBF_NEEDS_GCPOLL | BBF_HAS_JMP | BBF_BACKWARD_JUMP | BBF_HAS_NEWOBJ | BBF_HAS_NEWARR | BBF_HAS_MDARRAYREF | BBF_MAY_HAVE_BOUNDS_CHECKS,

    /// <summary>Flags a block should not have had before it is split.</summary>
    BBF_SPLIT_NONEXIST = BBF_RETLESS_CALL | BBF_COLD | BBF_THROW_HELPER,

    /// <summary>Flags lost by the top block when a block is split.</summary>
    /// <remarks>
    ///   <para>Note, this is a conservative guess.</para>
    ///   <para>For example, the top block might or might not have BBF_GC_SAFE_POINT, but we assume it does not have BBF_GC_SAFE_POINT any more.</para>
    /// </remarks>
    BBF_SPLIT_LOST = BBF_GC_SAFE_POINT | BBF_NEEDS_GCPOLL | BBF_HAS_JMP | BBF_KEEP_BBJ_ALWAYS | BBF_CLONED_FINALLY_END | BBF_RECURSIVE_TAILCALL,

    /// <summary>Flags gained by the bottom block when a block is split.</summary>
    /// <remarks>
    ///   <para>Note, this is a conservative guess.</para>
    ///   <para>For example, the bottom block might or might not have BBF_HAS_NEWARR, but we assume it has BBF_HAS_NEWARR.</para>
    /// </remarks>
    BBF_SPLIT_GAINED = BBF_DONT_REMOVE | BBF_HAS_JMP | BBF_BACKWARD_JUMP | BBF_PROF_WEIGHT | BBF_HAS_NEWARR | BBF_HAS_NEWOBJ | BBF_KEEP_BBJ_ALWAYS | BBF_CLONED_FINALLY_END | BBF_HAS_HISTOGRAM_PROFILE | BBF_HAS_VALUE_PROFILE | BBF_HAS_MDARRAYREF | BBF_NEEDS_GCPOLL | BBF_MAY_HAVE_BOUNDS_CHECKS | BBF_ASYNC_RESUMPTION,

    /// <summary>Flags that must be propagated to a new block if code is copied from a block to a new block.</summary>
    /// <remarks>
    ///   <para>These are flags that limit processing of a block if the code in question doesn't exist.</para>
    ///   <para>This is conservative; we might not have actually copied one of these type of tree nodes, but if we only copy a portion of the block's statements, we don't know (unless we actually pay close attention during the copy).</para>
    /// </remarks>
    BBF_COPY_PROPAGATE = BBF_HAS_NEWOBJ | BBF_HAS_NEWARR | BBF_HAS_MDARRAYREF | BBF_MAY_HAVE_BOUNDS_CHECKS,
}
