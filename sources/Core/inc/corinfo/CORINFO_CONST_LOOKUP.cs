// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

//----------------------------------------------------------------------------
// Looking up handles and addresses.
//
// When the JIT requests a handle, the EE may direct the JIT that it must
// access the handle in a variety of ways.  These are packed as
//    CORINFO_CONST_LOOKUP
// or CORINFO_LOOKUP (contains either a CORINFO_CONST_LOOKUP or a CORINFO_RUNTIME_LOOKUP)
//
// Constant Lookups v. Runtime Lookups (i.e. when will Runtime Lookups be generated?)
// -----------------------------------------------------------------------------------
//
// CORINFO_LOOKUP_KIND is part of the result type of embedGenericHandle,
// getVirtualCallInfo and any other functions that may require a
// runtime lookup when compiling shared generic code.
//
// CORINFO_LOOKUP_KIND indicates whether a particular token in the instruction stream can be:
// (a) Mapped to a handle (type, field or method) at compile-time (!needsRuntimeLookup)
// (b) Must be looked up at run-time, and if so which runtime lookup technique should be used (see below)
//
// If the JIT or EE does not support code sharing for generic code, then
// all CORINFO_LOOKUP results will be "constant lookups", i.e.
// the needsRuntimeLookup of CORINFO_LOOKUP.lookupKind.needsRuntimeLookup
// will be false.
//
// Constant Lookups
// ----------------
//
// Constant Lookups are either:
//     IAT_VALUE: immediate (relocatable) values,
//     IAT_PVALUE: immediate values access via an indirection through an immediate (relocatable) address
//     IAT_RELPVALUE: immediate values access via a relative indirection through an immediate offset
//     IAT_PPVALUE: immediate values access via a double indirection through an immediate (relocatable) address
//
// Runtime Lookups
// ---------------
//
// CORINFO_LOOKUP_KIND is part of the result type of embedGenericHandle,
// getVirtualCallInfo and any other functions that may require a
// runtime lookup when compiling shared generic code.
//
// CORINFO_LOOKUP_KIND indicates whether a particular token in the instruction stream can be:
// (a) Mapped to a handle (type, field or method) at compile-time (!needsRuntimeLookup)
// (b) Must be looked up at run-time using the class dictionary
//     stored in the vtable of the this pointer (needsRuntimeLookup && THISOBJ)
// (c) Must be looked up at run-time using the method dictionary
//     stored in the method descriptor parameter passed to a generic
//     method (needsRuntimeLookup && METHODPARAM)
// (d) Must be looked up at run-time using the class dictionary stored
//     in the vtable parameter passed to a method in a generic
//     struct (needsRuntimeLookup && CLASSPARAM)
public unsafe struct CORINFO_CONST_LOOKUP
{
    // If the handle is obtained at compile-time, then this handle is the "exact" handle (class, method, or field)
    // Otherwise, it's a representative...
    // If accessType is
    //     IAT_VALUE     --> "handle" stores the real handle or "addr " stores the computed address
    //     IAT_PVALUE    --> "addr" stores a pointer to a location which will hold the real handle
    //     IAT_RELPVALUE --> "addr" stores a relative pointer to a location which will hold the real handle
    //     IAT_PPVALUE   --> "addr" stores a double indirection to a location which will hold the real handle

    public InfoAccessType accessType;

    private _Anonymous_e__Union _anonymous;

    [UnscopedRef]
    public ref CORINFO_GENERIC_HANDLE handle => ref _anonymous.handle;

    [UnscopedRef]
    public ref void* addr => ref _anonymous.addr;

    [StructLayout(LayoutKind.Explicit)]
    private struct _Anonymous_e__Union
    {
        [FieldOffset(0)]
        public CORINFO_GENERIC_HANDLE handle;

        [FieldOffset(0)]
        public void* addr;
    }
}
