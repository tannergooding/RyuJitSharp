// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

/// <summary>Result of calling <c>embedGenericHandle</c>.</summary>
public struct CORINFO_LOOKUP
{
    public CORINFO_LOOKUP_KIND lookupKind;

    private _Anonymous_e__Union _anonymous;

    /// <summary>If kind.needsRuntimeLookup then this indicates how to do the lookup</summary>
    [UnscopedRef]
    public ref CORINFO_RUNTIME_LOOKUP runtimeLookup => ref _anonymous.runtimeLookup;

    // If the handle is obtained at compile-time, then this handle is the "exact" handle (class, method, or field)
    // Otherwise, it's a representative...  If accessType is
    //     IAT_VALUE --> "handle" stores the real handle or "addr " stores the computed address
    //     IAT_PVALUE --> "addr" stores a pointer to a location which will hold the real handle
    //     IAT_RELPVALUE --> "addr" stores a relative pointer to a location which will hold the real handle
    //     IAT_PPVALUE --> "addr" stores a double indirection to a location which will hold the real handle
    [UnscopedRef]
    public ref CORINFO_CONST_LOOKUP constLookup => ref _anonymous.constLookup;

    [StructLayout(LayoutKind.Explicit)]
    private struct _Anonymous_e__Union
    {
        [FieldOffset(0)]
        public CORINFO_RUNTIME_LOOKUP runtimeLookup;

        [FieldOffset(0)]
        public CORINFO_CONST_LOOKUP constLookup;
    }
}
