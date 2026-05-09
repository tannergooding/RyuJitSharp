// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.GenTreeCallFlags;
using System;

namespace RyuJitSharp;

/// <summary>a bitmask of flags for GenTreeCall stored in gtCallMoreFlags.</summary>
[Flags]
public enum GenTreeCallFlags
{
    GTF_CALL_M_EMPTY = 0,

    /// <summary>the ABI dictates that this call needs a ret buffer</summary>
    GTF_CALL_M_RETBUFFARG = 1 << 0,

    /// <summary>Does this call have a local ret buffer that we are optimizing?</summary>
    GTF_CALL_M_RETBUFFARG_LCLOPT = 1 << 1,

    /// <summary>call to Delegate.Invoke</summary>
    GTF_CALL_M_DELEGATE_INV = 1 << 2,

    /// <summary>not a call for computing full interruptability and therefore no GC check is required.</summary>
    GTF_CALL_M_NOGCCHECK = 1 << 3,

    /// <summary>function that could be optimized as an intrinsic in special cases. Used to optimize fast way out in morphing</summary>
    GTF_CALL_M_SPECIAL_INTRINSIC = 1 << 4,

    /// <summary>the virtstub is indirected through a relative address (only for GTF_CALL_VIRT_STUB)</summary>
    GTF_CALL_M_VIRTSTUB_REL_INDIRECT = 1 << 5,

    /// <summary>callee "this" pointer is equal to caller this pointer (only for GTF_CALL_NONVIRT)</summary>
    GTF_CALL_M_NONVIRT_SAME_THIS = 1 << 5,

    /// <summary>the compLvFrameListRoot variable dies here (last use)</summary>
    GTF_CALL_M_FRAME_VAR_DEATH = 1 << 6,

    /// <summary>the call is a tailcall</summary>
    GTF_CALL_M_TAILCALL = 1 << 7,

    /// <summary>the call is "tail" prefixed and importer has performed tail call checks</summary>
    GTF_CALL_M_EXPLICIT_TAILCALL = 1 << 8,

    /// <summary>call is a tail call dispatched via tail call JIT helper.</summary>
    GTF_CALL_M_TAILCALL_VIA_JIT_HELPER = 1 << 9,

    /// <summary>call is an opportunistic tail call and importer has performed tail call checks</summary>
    GTF_CALL_M_IMPLICIT_TAILCALL = 1 << 10,

    /// <summary>call is a fast recursive tail call that can be converted into a loop</summary>
    GTF_CALL_M_TAILCALL_TO_LOOP = 1 << 11,

    /// <summary>call is a pinvoke.  This mirrors VM flag CORINFO_FLG_PINVOKE. A call marked as Pinvoke is not necessarily a GT_CALL_UNMANAGED.</summary>
    /// <remarks>For e.g. an IL Stub dynamically generated for a PInvoke declaration is flagged as a Pinvoke but not as an unmanaged call. See impCheckForPInvokeCall() to know when these flags are set.</remarks>
    GTF_CALL_M_PINVOKE = 1 << 12,

    /// <summary>call does not return</summary>
    GTF_CALL_M_DOES_NOT_RETURN = 1 << 13,

    /// <summary>call is in wrapper delegate</summary>
    GTF_CALL_M_WRAPPER_DELEGATE_INV = 1 << 14,

    /// <summary>NativeAOT managed calli needs transformation, that checks special bit in calli address. If it is set, then it is necessary to restore real function address and load hidden argument as the first argument for calli.</summary>
    /// <remarks>It is NativeAOT replacement for instantiating stubs, because executable code cannot be generated at runtime.</remarks>
    GTF_CALL_M_FAT_POINTER_CHECK = 1 << 15,

    /// <summary>this helper call can be removed if it is part of a comma and the comma result is unused.</summary>
    GTF_CALL_M_HELPER_SPECIAL_DCE = 1 << 16,

    /// <summary>this call is a candidate for guarded devirtualization</summary>
    GTF_CALL_M_GUARDED_DEVIRT = 1 << 17,

    /// <summary>this call is a candidate for guarded devirtualization without a fallback</summary>
    GTF_CALL_M_GUARDED_DEVIRT_EXACT = 1 << 18,

    /// <summary>this call is a candidate for chained guarded devirtualization</summary>
    GTF_CALL_M_GUARDED_DEVIRT_CHAIN = 1 << 19,

    /// <summary>this is a call to an allocator with side effects</summary>
    GTF_CALL_M_ALLOC_SIDE_EFFECTS = 1 << 20,

    /// <summary>suppress the GC transition (i.e. during a pinvoke) but a separate GC safe point is required.</summary>
    GTF_CALL_M_SUPPRESS_GC_TRANSITION = 1 << 21,

    /// <summary>this call is a runtime async method call and thus a suspension point</summary>
    GTF_CALL_M_ASYNC = 1 << 22,

    /// <summary>the Virtual Call target address is expanded and placed in gtControlExpr in Morph rather than in Lower</summary>
    GTF_CALL_M_EXPANDED_EARLY = 1 << 23,

    /// <summary>ldvirtftn on an interface type</summary>
    GTF_CALL_M_LDVIRTFTN_INTERFACE = 1 << 24,

    /// <summary>this cast (helper call) can be expanded if it's profitable. To be removed.</summary>
    GTF_CALL_M_CAST_CAN_BE_EXPANDED = 1 << 25,

    /// <summary>if we expand this specific cast we don't need to check the input object for null</summary>
    /// <remarks>NOTE: if needed, this flag can be removed, and we can introduce new _NONNUL cast helpers</remarks>
    GTF_CALL_M_CAST_OBJ_NONNULL = 1 << 26,

    /// <summary>this call is a new array helper for a stack allocated array.</summary>
    GTF_CALL_M_STACK_ARRAY = 1 << 27,
}
