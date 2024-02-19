// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.CorInfoHelpFunc;
using System;

namespace RyuJitSharp;

/// <summary>Defines the set of helpers (accessed via <see cref="ICorDynamicInfo.getHelperFtn" />).</summary>
/// <remarks>
///   <para>These helpers can be called by native code which executes in the runtime.</para>
///   <para>Compilers can emit calls to these helpers.</para>
/// </remarks>
public enum CorInfoHelpFunc
{
    /// <summary>Invalid value.</summary>
    /// <remarks>This should never be used.</remarks>
    CORINFO_HELP_UNDEF,

    //
    // Arithmetic helpers
    //

    // For the ARM 32-bit integer divide uses a helper call :-(
    CORINFO_HELP_DIV,

    CORINFO_HELP_MOD,

    CORINFO_HELP_UDIV,

    CORINFO_HELP_UMOD,

    CORINFO_HELP_LLSH,

    CORINFO_HELP_LRSH,

    CORINFO_HELP_LRSZ,

    CORINFO_HELP_LMUL,

    CORINFO_HELP_LMUL_OVF,

    CORINFO_HELP_ULMUL_OVF,

    CORINFO_HELP_LDIV,

    CORINFO_HELP_LMOD,

    CORINFO_HELP_ULDIV,

    CORINFO_HELP_ULMOD,

    /// <summary>Convert a signed int64 to a double.</summary>
    CORINFO_HELP_LNG2DBL,

    /// <summary>Convert a unsigned int64 to a double.</summary>
    CORINFO_HELP_ULNG2DBL,

    CORINFO_HELP_DBL2INT,

    CORINFO_HELP_DBL2INT_OVF,

    CORINFO_HELP_DBL2LNG,

    CORINFO_HELP_DBL2LNG_OVF,

    CORINFO_HELP_DBL2UINT,

    CORINFO_HELP_DBL2UINT_OVF,

    CORINFO_HELP_DBL2ULNG,

    CORINFO_HELP_DBL2ULNG_OVF,

    CORINFO_HELP_FLTREM,

    CORINFO_HELP_DBLREM,

    CORINFO_HELP_FLTROUND,

    CORINFO_HELP_DBLROUND,

    //
    // Allocating a new object. Always use ICorClassInfo.getNewHelper() to decide
    // which is the right helper to use to allocate an object of a given type.
    //

    CORINFO_HELP_NEWFAST,

    /// <summary>Allocator for objects that *might* allocate them on a frozen segment.</summary>
    CORINFO_HELP_NEWFAST_MAYBEFROZEN,

    /// <summary>Allocator for small, non-finalizer, non-array object.</summary>
    CORINFO_HELP_NEWSFAST,

    /// <summary>Allocator for small, finalizable, non-array object.</summary>
    CORINFO_HELP_NEWSFAST_FINALIZE,

    /// <summary>Allocator for small, non-finalizer, non-array object, 8 byte aligned.</summary>
    CORINFO_HELP_NEWSFAST_ALIGN8,

    /// <summary>Allocator for small, value class, 8 byte aligned.</summary>
    CORINFO_HELP_NEWSFAST_ALIGN8_VC,

    /// <summary>Allocator for small, finalizable, non-array object, 8 byte aligned.</summary>
    CORINFO_HELP_NEWSFAST_ALIGN8_FINALIZE,

    /// <summary>Multi-dim array helper for arrays Rank != 1 (with or without lower bounds - dimensions passed in as unmanaged array).</summary>
    CORINFO_HELP_NEW_MDARR,

    /// <summary>Rare multi-dim array helper (Rank == 1).</summary>
    CORINFO_HELP_NEW_MDARR_RARE,

    /// <summary>Helper for any one dimensional array creation.</summary>
    CORINFO_HELP_NEWARR_1_DIRECT,

    /// <summary>Allocator for arrays that *might* allocate them on a frozen segment.</summary>
    CORINFO_HELP_NEWARR_1_MAYBEFROZEN,

    /// <summary>Optimized 1-D object arrays.</summary>
    CORINFO_HELP_NEWARR_1_OBJ,

    /// <summary>Optimized 1-D value class arrays.</summary>
    CORINFO_HELP_NEWARR_1_VC,

    /// <summary>Like VC, but aligns the array start.</summary>
    CORINFO_HELP_NEWARR_1_ALIGN8,

    /// <summary>Create a new string literal.</summary>
    CORINFO_HELP_STRCNS,

    //
    // Object model
    //

    /// <summary>Initialize class if not already initialized.</summary>
    CORINFO_HELP_INITCLASS,

    /// <summary>Initialize class for instantiated type.</summary>
    CORINFO_HELP_INITINSTCLASS,

    //
    // Use ICorClassInfo.getCastingHelper to determine the right helper to use
    //

    /// <summary>Optimized helper for interfaces.</summary>
    CORINFO_HELP_ISINSTANCEOFINTERFACE,

    /// <summary>Optimized helper for arrays.</summary>
    CORINFO_HELP_ISINSTANCEOFARRAY,

    /// <summary>Optimized helper for classes.</summary>
    CORINFO_HELP_ISINSTANCEOFCLASS,

    /// <summary>Slow helper for any type.</summary>
    CORINFO_HELP_ISINSTANCEOFANY,

    CORINFO_HELP_CHKCASTINTERFACE,

    CORINFO_HELP_CHKCASTARRAY,

    CORINFO_HELP_CHKCASTCLASS,

    CORINFO_HELP_CHKCASTANY,

    /// <summary>Optimized helper for classes.</summary>
    /// <remarks>Assumes that the trivial cases has been taken care of by the inlined check.</remarks>
    CORINFO_HELP_CHKCASTCLASS_SPECIAL, 

    CORINFO_HELP_ISINSTANCEOF_EXCEPTION,

    /// <summary>Fast box helper.</summary>
    /// <remarks>Only possible exception is <see cref="OutOfMemoryException" />.</remarks>
    CORINFO_HELP_BOX,

    /// <summary>special form of boxing for <see cref="Nullable{T}" />.</summary>
    CORINFO_HELP_BOX_NULLABLE,

    CORINFO_HELP_UNBOX,

    /// <summary>Special form of unboxing for <see cref="Nullable{T}" />.</summary>
    CORINFO_HELP_UNBOX_NULLABLE,

    /// <summary>Extract the byref from a <see cref="TypedReference" />, checking that it is the expected type.</summary>
    CORINFO_HELP_GETREFANY,

    /// <summary>Assign to element of object array with type-checking.</summary>
    CORINFO_HELP_ARRADDR_ST,

    /// <summary>Does a precise type comparison and returns address.</summary>
    CORINFO_HELP_LDELEMA_REF,

    //
    // Exceptions
    //

    /// <summary>Throw an exception object.</summary>
    CORINFO_HELP_THROW,

    /// <summary>Rethrow the currently active exception.</summary>
    CORINFO_HELP_RETHROW,

    /// <summary>For a user program to break to the debugger.</summary>
    CORINFO_HELP_USER_BREAKPOINT,

    /// <summary>Array bounds check failed.</summary>
    CORINFO_HELP_RNGCHKFAIL,

    /// <summary>Throw an <see cref="OverflowException" />.</summary>
    CORINFO_HELP_OVERFLOW,

    /// <summary>Throw a <see cref="DivideByZeroException" />.</summary>
    CORINFO_HELP_THROWDIVZERO,

    /// <summary>Throw a <see cref="NullReferenceException" />.</summary>
    CORINFO_HELP_THROWNULLREF,

    /// <summary>Throw a <c>VerificationException</c>.</summary>
    CORINFO_HELP_VERIFICATION,

    /// <summary>Kill the process avoiding any exceptions or stack and data dependencies (use for GuardStack unsafe buffer checks).</summary>
    CORINFO_HELP_FAIL_FAST,

    /// <summary>Throw an access exception due to a failed member/class access check.</summary>
    CORINFO_HELP_METHOD_ACCESS_EXCEPTION,

    CORINFO_HELP_FIELD_ACCESS_EXCEPTION,

    CORINFO_HELP_CLASS_ACCESS_EXCEPTION,

    /// <summary>Callback into the EE at the end of a catch block.</summary>
    CORINFO_HELP_ENDCATCH,

    //
    // Synchronization
    //

    CORINFO_HELP_MON_ENTER,

    CORINFO_HELP_MON_EXIT,

    CORINFO_HELP_MON_ENTER_STATIC,

    CORINFO_HELP_MON_EXIT_STATIC,

    /// <summary>Given a generics method handle, returns a class handle.</summary>
    CORINFO_HELP_GETCLASSFROMMETHODPARAM,

    /// <summary>Given a generics class handle, returns the sync monitor in its ManagedClassObject.</summary>
    CORINFO_HELP_GETSYNCFROMCLASSHANDLE,

    //
    // GC support
    //

    /// <summary>Call GC (force a GC).</summary>
    CORINFO_HELP_STOP_FOR_GC,

    /// <summary>Ask GC if it wants to collect.</summary>
    CORINFO_HELP_POLL_GC,

    /// <summary>Force a GC, but then update the jitted code to be a no-op call</summary>
    CORINFO_HELP_STRESS_GC,

    /// <summary>Confirm that ECX is a valid object pointer (debugging only).</summary>
    CORINFO_HELP_CHECK_OBJ,

    //
    // GC Write barrier support
    //

    /// <summary>Universal helpers with F_CALL_CONV calling convention.</summary>
    CORINFO_HELP_ASSIGN_REF,

    CORINFO_HELP_CHECKED_ASSIGN_REF,

    /// <summary>Do the store, and ensure that the target was not in the heap.</summary>
    CORINFO_HELP_ASSIGN_REF_ENSURE_NONHEAP,

    CORINFO_HELP_ASSIGN_BYREF,

    CORINFO_HELP_ASSIGN_STRUCT,

    //
    // Accessing fields
    //

    /// <summary>For COM object support (using COM get/set routines to update object) and EnC and cross-context support</summary>
    CORINFO_HELP_GETFIELD8,

    CORINFO_HELP_SETFIELD8,

    CORINFO_HELP_GETFIELD16,

    CORINFO_HELP_SETFIELD16,

    CORINFO_HELP_GETFIELD32,

    CORINFO_HELP_SETFIELD32,

    CORINFO_HELP_GETFIELD64,

    CORINFO_HELP_SETFIELD64,

    CORINFO_HELP_GETFIELDOBJ,

    CORINFO_HELP_SETFIELDOBJ,

    CORINFO_HELP_GETFIELDSTRUCT,

    CORINFO_HELP_SETFIELDSTRUCT,

    CORINFO_HELP_GETFIELDFLOAT,

    CORINFO_HELP_SETFIELDFLOAT,

    CORINFO_HELP_GETFIELDDOUBLE,

    CORINFO_HELP_SETFIELDDOUBLE,


    CORINFO_HELP_GETFIELDADDR,

    CORINFO_HELP_GETSTATICFIELDADDR,

    /// <summary>Helper for PE TLS fields.</summary>
    CORINFO_HELP_GETSTATICFIELDADDR_TLS,

    //
    // There are a variety of specialized helpers for accessing static fields. The JIT should use
    // ICorClassInfo.getSharedStaticsOrCCtorHelper to determine which helper to use
    //

    //
    // Helpers for regular statics
    //

    CORINFO_HELP_GETGENERICS_GCSTATIC_BASE,

    CORINFO_HELP_GETGENERICS_NONGCSTATIC_BASE,

    CORINFO_HELP_GETSHARED_GCSTATIC_BASE,

    CORINFO_HELP_GETSHARED_NONGCSTATIC_BASE,

    CORINFO_HELP_GETSHARED_GCSTATIC_BASE_NOCTOR,

    CORINFO_HELP_GETSHARED_NONGCSTATIC_BASE_NOCTOR,

    CORINFO_HELP_GETSHARED_GCSTATIC_BASE_DYNAMICCLASS,

    CORINFO_HELP_GETSHARED_NONGCSTATIC_BASE_DYNAMICCLASS,

    //
    // Helper to class initialize shared generic with dynamic class, but not get static field address
    //

    CORINFO_HELP_CLASSINIT_SHARED_DYNAMICCLASS,

    //
    // Helpers for thread statics
    //

    CORINFO_HELP_GETGENERICS_GCTHREADSTATIC_BASE,

    CORINFO_HELP_GETGENERICS_NONGCTHREADSTATIC_BASE,

    CORINFO_HELP_GETSHARED_GCTHREADSTATIC_BASE,

    CORINFO_HELP_GETSHARED_NONGCTHREADSTATIC_BASE,

    CORINFO_HELP_GETSHARED_GCTHREADSTATIC_BASE_NOCTOR,

    CORINFO_HELP_GETSHARED_NONGCTHREADSTATIC_BASE_NOCTOR,

    CORINFO_HELP_GETSHARED_GCTHREADSTATIC_BASE_DYNAMICCLASS,

    CORINFO_HELP_GETSHARED_NONGCTHREADSTATIC_BASE_DYNAMICCLASS,

    CORINFO_HELP_GETSHARED_GCTHREADSTATIC_BASE_NOCTOR_OPTIMIZED,

    CORINFO_HELP_GETSHARED_NONGCTHREADSTATIC_BASE_NOCTOR_OPTIMIZED,

    //
    // Debugger
    //

    /// <summary>Check if this is "JustMyCode" and needs to be stepped through.</summary>
    CORINFO_HELP_DBG_IS_JUST_MY_CODE,

    //
    // Profiling enter/leave probe addresses
    //

    /// <summary>Record the entry to a method (caller).</summary>
    CORINFO_HELP_PROF_FCN_ENTER,

    /// <summary>Record the completion of current method (caller).</summary>
    CORINFO_HELP_PROF_FCN_LEAVE,

    /// <summary>Record the completion of current method through tail call (caller).</summary>
    CORINFO_HELP_PROF_FCN_TAILCALL,

    //
    // Miscellaneous
    //

    /// <summary>Record the entry to a method for collecting Tuning data</summary>
    CORINFO_HELP_BBT_FCN_ENTER,

    /// <summary>Indirect p/invoke call.</summary>
    CORINFO_HELP_PINVOKE_CALLI,

    /// <summary>Perform a tail call.</summary>
    CORINFO_HELP_TAILCALL,

    CORINFO_HELP_GETCURRENTMANAGEDTHREADID,

    /// <summary>Initialize an inlined PInvoke Frame for the JIT-compiler.</summary>
    CORINFO_HELP_INIT_PINVOKE_FRAME,

    /// <summary>Init block of memory.</summary>
    CORINFO_HELP_MEMSET,

    /// <summary>Copy block of memory.</summary>
    CORINFO_HELP_MEMCPY,

    /// <summary>Determine a type/field/method handle at run-time.</summary>
    CORINFO_HELP_RUNTIMEHANDLE_METHOD,

    /// <summary>Determine a type/field/method handle at run-time, with IBC logging.</summary>
    CORINFO_HELP_RUNTIMEHANDLE_METHOD_LOG,

    /// <summary>Determine a type/field/method handle at run-time.</summary>
    CORINFO_HELP_RUNTIMEHANDLE_CLASS,

    /// <summary>Determine a type/field/method handle at run-time, with IBC logging.</summary>
    CORINFO_HELP_RUNTIMEHANDLE_CLASS_LOG,

    /// <summary>Convert from a TypeHandle (native structure pointer) to RuntimeType at run-time.</summary>
    CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPE,

    /// <summary>Convert from a TypeHandle (native structure pointer) to RuntimeType at run-time, the type may be null.</summary>
    CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPE_MAYBENULL,

    /// <summary>Convert from a MethodDesc (native structure pointer) to RuntimeMethodHandle at run-time.</summary>
    CORINFO_HELP_METHODDESC_TO_STUBRUNTIMEMETHOD,

    /// <summary>Convert from a FieldDesc (native structure pointer) to RuntimeFieldHandle at run-time.</summary>
    CORINFO_HELP_FIELDDESC_TO_STUBRUNTIMEFIELD,

    /// <summary>Convert from a TypeHandle (native structure pointer) to RuntimeTypeHandle at run-time.</summary>
    CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPEHANDLE,

    /// <summary>Convert from a TypeHandle (native structure pointer) to RuntimeTypeHandle at run-time, handle might point to a null type.</summary>
    CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPEHANDLE_MAYBENULL,

    /// <summary>Look up a virtual method at run-time.</summary>
    CORINFO_HELP_VIRTUAL_FUNC_PTR,

    //
    // Not a real helpers. Instead of taking handle arguments, these helpers point to a small stub that loads the handle argument and calls the static helper.
    //

    CORINFO_HELP_READYTORUN_NEW,

    CORINFO_HELP_READYTORUN_NEWARR_1,

    CORINFO_HELP_READYTORUN_ISINSTANCEOF,

    CORINFO_HELP_READYTORUN_CHKCAST,

    /// <summary>Static gc field access.</summary>
    CORINFO_HELP_READYTORUN_GCSTATIC_BASE,

    /// <summary>Static non gc field access.</summary>
    CORINFO_HELP_READYTORUN_NONGCSTATIC_BASE,

    CORINFO_HELP_READYTORUN_THREADSTATIC_BASE,

    CORINFO_HELP_READYTORUN_THREADSTATIC_BASE_NOCTOR,

    CORINFO_HELP_READYTORUN_NONGCTHREADSTATIC_BASE,

    CORINFO_HELP_READYTORUN_VIRTUAL_FUNC_PTR,

    CORINFO_HELP_READYTORUN_GENERIC_HANDLE,

    CORINFO_HELP_READYTORUN_DELEGATE_CTOR,

    CORINFO_HELP_READYTORUN_GENERIC_STATIC_BASE,

    // Not real JIT helper. Used in native images.
    CORINFO_HELP_EE_PERSONALITY_ROUTINE,

    // Not real JIT helper. Used in native images to detect filter funclets.
    CORINFO_HELP_EE_PERSONALITY_ROUTINE_FILTER_FUNCLET,

    //
    // ASSIGN_REF_EAX - CHECKED_ASSIGN_REF_EBP: NOGC_WRITE_BARRIERS JIT helper calls
    //
    // For unchecked versions EDX is required to point into GC heap.
    //
    // NOTE: these helpers are only used for x86.
    //

    /// <summary>EAX holds GC ptr, do a 'mov [EDX], EAX' and inform GC.</summary>
    CORINFO_HELP_ASSIGN_REF_EAX,

    /// <summary>EBX holds GC ptr, do a 'mov [EDX], EBX' and inform GC.</summary>
    CORINFO_HELP_ASSIGN_REF_EBX,

    /// <summary>ECX holds GC ptr, do a 'mov [EDX], ECX' and inform GC.</summary>
    CORINFO_HELP_ASSIGN_REF_ECX,

    /// <summary>ESI holds GC ptr, do a 'mov [EDX], ESI' and inform GC.</summary>
    CORINFO_HELP_ASSIGN_REF_ESI,

    /// <summary>EDI holds GC ptr, do a 'mov [EDX], EDI' and inform GC</summary>
    CORINFO_HELP_ASSIGN_REF_EDI,

    /// <summary>EBP holds GC ptr, do a 'mov [EDX], EBP' and inform GC</summary>
    CORINFO_HELP_ASSIGN_REF_EBP,

    //
    // These are the same as ASSIGN_REF above but also check if EDX points into heap.
    //

    CORINFO_HELP_CHECKED_ASSIGN_REF_EAX,

    CORINFO_HELP_CHECKED_ASSIGN_REF_EBX,

    CORINFO_HELP_CHECKED_ASSIGN_REF_ECX,

    CORINFO_HELP_CHECKED_ASSIGN_REF_ESI,

    CORINFO_HELP_CHECKED_ASSIGN_REF_EDI,

    CORINFO_HELP_CHECKED_ASSIGN_REF_EBP,

    /// <summary>Return the reference to a counter to decide to take cloned path in debug stress.</summary>
    CORINFO_HELP_LOOP_CLONE_CHOICE_ADDR,

    /// <summary>Print a message that a loop cloning optimization has occurred in debug mode.</summary>
    CORINFO_HELP_DEBUG_LOG_LOOP_CLONING,

    /// <summary>Throw <see cref="ArgumentException" />.</summary>
    CORINFO_HELP_THROW_ARGUMENTEXCEPTION,

    /// <summary>Throw <see cref="ArgumentOutOfRangeException" />.</summary>
    CORINFO_HELP_THROW_ARGUMENTOUTOFRANGEEXCEPTION,

    /// <summary>Throw <see cref="NotImplementedException" />.</summary>
    CORINFO_HELP_THROW_NOT_IMPLEMENTED,

    /// <summary>Throw <see cref="PlatformNotSupportedException" />.</summary>
    CORINFO_HELP_THROW_PLATFORM_NOT_SUPPORTED,

    /// <summary>Throw <c>TypeNotSupportedException</c>.</summary>
    CORINFO_HELP_THROW_TYPE_NOT_SUPPORTED,

    /// <summary>Throw <c>AmbiguousResolutionException</c> for failed static virtual method resolution.</summary>
    CORINFO_HELP_THROW_AMBIGUOUS_RESOLUTION_EXCEPTION,

    /// <summary>Throw <c>EntryPointNotFoundException</c> for failed static virtual method resolution.</summary>
    CORINFO_HELP_THROW_ENTRYPOINT_NOT_FOUND_EXCEPTION,

    /// <summary>Transition to preemptive mode before a P/Invoke, frame is the first argument.</summary>
    CORINFO_HELP_JIT_PINVOKE_BEGIN,

    /// <summary>Transition to cooperative mode after a P/Invoke, frame is the first argument.</summary>
    CORINFO_HELP_JIT_PINVOKE_END,

    /// <summary>Transition to cooperative mode in reverse P/Invoke prolog, frame is the first argument.</summary>
    CORINFO_HELP_JIT_REVERSE_PINVOKE_ENTER,

    /// <summary>Transition to cooperative mode and track transitions in reverse P/Invoke prolog.</summary>
    CORINFO_HELP_JIT_REVERSE_PINVOKE_ENTER_TRACK_TRANSITIONS,

    /// <summary>Transition to preemptive mode in reverse P/Invoke epilog, frame is the first argument.</summary>
    CORINFO_HELP_JIT_REVERSE_PINVOKE_EXIT,

    /// <summary>Transition to preemptive mode and track transitions in reverse P/Invoke prolog.</summary>
    CORINFO_HELP_JIT_REVERSE_PINVOKE_EXIT_TRACK_TRANSITIONS,

    /// <summary>Resolve a generic virtual method target from this pointer and runtime method handle.</summary>
    CORINFO_HELP_GVMLOOKUP_FOR_SLOT,

    /// <summary>Probes each page of the allocated stack frame.</summary>
    CORINFO_HELP_STACK_PROBE,

    /// <summary>Notify runtime that code has reached a patch point</summary>
    CORINFO_HELP_PATCHPOINT,

    /// <summary>Notify runtime that code has reached a part of the method that wasn't originally jitted.</summary>
    CORINFO_HELP_PARTIAL_COMPILATION_PATCHPOINT,

    /// <summary>Update 32-bit class profile for a call site.</summary>
    CORINFO_HELP_CLASSPROFILE32,

    /// <summary>Update 64-bit class profile for a call site.</summary>
    CORINFO_HELP_CLASSPROFILE64,

    /// <summary>Update 32-bit method profile for a delegate call site.</summary>
    CORINFO_HELP_DELEGATEPROFILE32,

    /// <summary>Update 64-bit method profile for a delegate call site.</summary>
    CORINFO_HELP_DELEGATEPROFILE64,

    /// <summary>Update 32-bit method profile for a vtable call site.</summary>
    CORINFO_HELP_VTABLEPROFILE32,

    /// <summary>Update 64-bit method profile for a vtable call site.</summary>
    CORINFO_HELP_VTABLEPROFILE64,

    /// <summary>Update 32-bit block or edge count profile.</summary>
    CORINFO_HELP_COUNTPROFILE32,

    /// <summary>Update 64-bit block or edge count profile.</summary>
    CORINFO_HELP_COUNTPROFILE64,

    /// <summary>Update 32-bit value profile.</summary>
    CORINFO_HELP_VALUEPROFILE32,

    /// <summary>Update 64-bit value profile.</summary>
    CORINFO_HELP_VALUEPROFILE64,

    /// <summary>CFG: Validate function pointer</summary>
    CORINFO_HELP_VALIDATE_INDIRECT_CALL,

    /// <summary>CFG: Validate and dispatch to pointer</summary>
    CORINFO_HELP_DISPATCH_INDIRECT_CALL,

    CORINFO_HELP_COUNT,
}
