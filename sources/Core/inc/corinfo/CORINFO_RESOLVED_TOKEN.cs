// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public unsafe struct CORINFO_RESOLVED_TOKEN
{
    //
    // [In] arguments of resolveToken
    //

    /// <summary>Context for resolution of generic arguments.</summary>
    public CORINFO_CONTEXT_HANDLE tokenContext;

    public CORINFO_MODULE_HANDLE tokenScope;

    /// <summary>The source token.</summary>
    public mdToken token;

    public CorInfoTokenKind tokenType;

    //
    // [Out] arguments of resolveToken.
    // - Type handle is always non-NULL.
    // - At most one of method and field handles is non-NULL (according to the token type).
    // - Method handle is an instantiating stub only for generic methods. Type handle
    //   is required to provide the full context for methods in generic types.
    //

    public CORINFO_CLASS_HANDLE hClass;

    public CORINFO_METHOD_HANDLE hMethod;

    public CORINFO_FIELD_HANDLE hField;

    //
    // [Out] TypeSpec and MethodSpec signatures for generics. NULL otherwise.
    //
    public PCCOR_SIGNATURE pTypeSpec;

    public uint cbTypeSpec;

    public PCCOR_SIGNATURE pMethodSpec;

    public uint cbMethodSpec;
}
