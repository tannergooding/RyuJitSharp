// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public unsafe struct CORINFO_CALL_INFO
{
    /// <summary>Target method handle.</summary>
    public CORINFO_METHOD_HANDLE hMethod;

    /// <summary>Flags for the target method.</summary>
    public uint methodFlags;

    /// <summary>Flags for <see cref="CORINFO_RESOLVED_TOKEN.hClass" />.</summary>
    public uint classFlags;

    public CORINFO_SIG_INFO sig;

    // If set to:
    //  - CORINFO_ACCESS_ALLOWED - The access is allowed.
    //  - CORINFO_ACCESS_ILLEGAL - This access cannot be allowed (i.e. it is public calling private).  The
    //      JIT may either insert the callsiteCalloutHelper into the code (as per a verification error) or
    //      call throwExceptionFromHelper on the callsiteCalloutHelper.  In this case callsiteCalloutHelper
    //      is guaranteed not to return.
    public CorInfoIsAccessAllowedResult accessAllowed;

    public CORINFO_HELPER_DESC callsiteCalloutHelper;

    // See above section on constraintCalls to understand when these are set to unusual values.
    public CORINFO_THIS_TRANSFORM thisTransform;

    public CORINFO_CALL_KIND kind;

    public bool nullInstanceCheck;

    /// <summary>Context for inlining and hidden arg.</summary>
    public CORINFO_CONTEXT_HANDLE contextHandle;

    /// <summary>Set if contextHandle is approx handle. Runtime lookup is required to get the exact handle.</summary>
    public bool exactContextNeedsRuntimeLookup;

    private _Anonymous_e__Union _anonymous;

    /// <summary>If kind.CORINFO_VIRTUALCALL_STUB then stubLookup will be set.</summary>
    [UnscopedRef]
    public ref CORINFO_LOOKUP stubLookup => ref _anonymous.stubLookup;

    /// <summary>If kind.CORINFO_CALL_CODE_POINTER then entryPointLookup will be set.</summary>
    [UnscopedRef]
    public ref CORINFO_LOOKUP codePointerLookup => ref _anonymous.codePointerLookup;

    public CORINFO_CONST_LOOKUP instParamLookup;

    public bool wrapperDelegateInvoke;

    [StructLayout(LayoutKind.Explicit)]
    private struct _Anonymous_e__Union
    {
        [FieldOffset(0)]
        public CORINFO_LOOKUP stubLookup;

        [FieldOffset(0)]
        public CORINFO_LOOKUP codePointerLookup;
    }
}
