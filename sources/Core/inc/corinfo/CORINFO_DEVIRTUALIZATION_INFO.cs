// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public unsafe struct CORINFO_DEVIRTUALIZATION_INFO
{
    //
    // [In] arguments of resolveVirtualMethod
    //

    public CORINFO_METHOD_HANDLE virtualMethod;

    public CORINFO_CLASS_HANDLE objClass;

    public CORINFO_CONTEXT_HANDLE context;

    public CORINFO_RESOLVED_TOKEN* pResolvedTokenVirtualMethod;

    //
    // [Out] results of resolveVirtualMethod.
    // - devirtualizedMethod is set to MethodDesc of devirt'ed method iff we were able to devirtualize.
    //      invariant is `resolveVirtualMethod(...) == (devirtualizedMethod != nullptr)`.
    // - requiresInstMethodTableArg is set to TRUE if the devirtualized method requires a type handle arg.
    // - exactContext is set to wrapped CORINFO_CLASS_HANDLE of devirt'ed method table.
    // - details on the computation done by the jit host
    // - If pResolvedTokenDevirtualizedMethod is not set to NULL and targeting an R2R image
    //   use it as the parameter to getCallInfo
    //

    public CORINFO_METHOD_HANDLE devirtualizedMethod;

    public bool requiresInstMethodTableArg;

    public CORINFO_CONTEXT_HANDLE exactContext;

    public CORINFO_DEVIRTUALIZATION_DETAIL detail;

    public CORINFO_RESOLVED_TOKEN resolvedTokenDevirtualizedMethod;

    public CORINFO_RESOLVED_TOKEN resolvedTokenDevirtualizedUnboxedMethod;
}
