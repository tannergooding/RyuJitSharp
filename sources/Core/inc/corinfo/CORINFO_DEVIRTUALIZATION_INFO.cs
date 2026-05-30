// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public struct CORINFO_DEVIRTUALIZATION_INFO
{
    //
    // [In] arguments of resolveVirtualMethod
    //

    public unsafe CORINFO_METHOD_HANDLE virtualMethod;

    public unsafe CORINFO_CLASS_HANDLE objClass;

    public unsafe CORINFO_CONTEXT_HANDLE context;

    public unsafe CORINFO_RESOLVED_TOKEN* pResolvedTokenVirtualMethod;

    //
    // [Out] results of resolveVirtualMethod.
    // - devirtualizedMethod is set to MethodDesc of devirt'ed method iff we were able to devirtualize.
    //      invariant is `resolveVirtualMethod(...) == (devirtualizedMethod is not null)`.
    // - requiresInstMethodTableArg is set to TRUE if the devirtualized method requires a type handle arg.
    // - tokenLookupContext is set to the wrapped context handle to use for token lookups after devirtualization.
    // - details on the computation done by the jit host
    // - If pResolvedTokenDevirtualizedMethod is not set to null and targeting an R2R image
    //   use it as the parameter to getCallInfo
    // - instParamLookup contains all the information necessary to pass the instantiation parameter for
    //   the devirtualized method.

    public unsafe CORINFO_METHOD_HANDLE devirtualizedMethod;

    public unsafe CORINFO_CONTEXT_HANDLE tokenLookupContext;

    public CORINFO_DEVIRTUALIZATION_DETAIL detail;

    public CORINFO_RESOLVED_TOKEN resolvedTokenDevirtualizedMethod;

    public CORINFO_RESOLVED_TOKEN resolvedTokenDevirtualizedUnboxedMethod;

    public CORINFO_LOOKUP instParamLookup;
}
