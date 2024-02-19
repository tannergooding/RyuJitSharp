// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.CORINFO_CALL_KIND;

namespace RyuJitSharp;

//----------------------------------------------------------------------------
// getCallInfo and CORINFO_CALL_INFO: The EE instructs the JIT about how to make a call
//
// callKind
// --------
//
// CORINFO_CALL :
//   Indicates that the JIT can use getFunctionEntryPoint to make a call,
//   i.e. there is nothing abnormal about the call.  The JITs know what to do if they get this.
//   Except in the case of constraint calls (see below), [targetMethodHandle] will hold
//   the CORINFO_METHOD_HANDLE that a call to findMethod would
//   have returned.
//   This flag may be combined with nullInstanceCheck=TRUE for uses of callvirt on methods that can
//   be resolved at compile-time (non-virtual, final or sealed).
//
// CORINFO_CALL_CODE_POINTER (shared generic code only) :
//   Indicates that the JIT should do an indirect call to the entrypoint given by address, which may be specified
//   as a runtime lookup by CORINFO_CALL_INFO::codePointerLookup.
//   [targetMethodHandle] will not hold a valid value.
//   This flag may be combined with nullInstanceCheck=TRUE for uses of callvirt on methods whose target method can
//   be resolved at compile-time but whose instantiation can be resolved only through runtime lookup.
//
// CORINFO_VIRTUALCALL_STUB (interface calls) :
//   Indicates that the EE supports "stub dispatch" and request the JIT to make a
//   "stub dispatch" call (an indirect call through CORINFO_CALL_INFO::stubLookup,
//   similar to CORINFO_CALL_CODE_POINTER).
//   "Stub dispatch" is a specialized calling sequence (that may require use of NOPs)
//   which allow the runtime to determine the call-site after the call has been dispatched.
//   If the call is too complex for the JIT (e.g. because
//   fetching the dispatch stub requires a runtime lookup, i.e. lookupKind.needsRuntimeLookup
//   is set) then the JIT is allowed to implement the call as if it were CORINFO_VIRTUALCALL_LDVIRTFTN
//   [targetMethodHandle] will hold the CORINFO_METHOD_HANDLE that a call to findMethod would
//   have returned.
//   This flag is always accompanied by nullInstanceCheck=TRUE.
//
// CORINFO_VIRTUALCALL_LDVIRTFTN (virtual generic methods) :
//   Indicates that the EE provides no way to implement the call directly and
//   that the JIT should use a LDVIRTFTN sequence (as implemented by CORINFO_HELP_VIRTUAL_FUNC_PTR)
//   followed by an indirect call.
//   [targetMethodHandle] will hold the CORINFO_METHOD_HANDLE that a call to findMethod would
//   have returned.
//   This flag is always accompanied by nullInstanceCheck=TRUE though typically the null check will
//   be implicit in the access through the instance pointer.
//
//  CORINFO_VIRTUALCALL_VTABLE (regular virtual methods) :
//   Indicates that the EE supports vtable dispatch and that the JIT should use getVTableOffset etc.
//   to implement the call.
//   [targetMethodHandle] will hold the CORINFO_METHOD_HANDLE that a call to findMethod would
//   have returned.
//   This flag is always accompanied by nullInstanceCheck=TRUE though typically the null check will
//   be implicit in the access through the instance pointer.
//
// thisTransform and constraint calls
// ----------------------------------
//
// For everything besides "constrained." calls "thisTransform" is set to
// CORINFO_NO_THIS_TRANSFORM.
//
// For "constrained." calls the EE attempts to resolve the call at compile
// time to a more specific method, or (shared generic code only) to a runtime lookup
// for a code pointer for the more specific method.
//
// In order to permit this, the "this" pointer supplied for a "constrained." call
// is a byref to an arbitrary type (see the IL spec). The "thisTransform" field
// will indicate how the JIT must transform the "this" pointer in order
// to be able to call the resolved method:
//
//  CORINFO_NO_THIS_TRANSFORM --> Leave it as a byref to an unboxed value type
//  CORINFO_BOX_THIS          --> Box it to produce an object
//  CORINFO_DEREF_THIS        --> Deref the byref to get an object reference
//
// In addition, the "kind" field will be set as follows for constraint calls:
//
//    CORINFO_CALL              --> the call was resolved at compile time, and
//                                  can be compiled like a normal call.
//    CORINFO_CALL_CODE_POINTER --> the call was resolved, but the target address will be
//                                  computed at runtime.  Only returned for shared generic code.
//    CORINFO_VIRTUALCALL_STUB,
//    CORINFO_VIRTUALCALL_LDVIRTFTN,
//    CORINFO_VIRTUALCALL_VTABLE   --> usual values indicating that a virtual call must be made
public enum CORINFO_CALL_KIND
{
    CORINFO_CALL,

    CORINFO_CALL_CODE_POINTER,

    CORINFO_VIRTUALCALL_STUB,

    CORINFO_VIRTUALCALL_LDVIRTFTN,

    CORINFO_VIRTUALCALL_VTABLE,
}
