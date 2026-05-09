// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

/// <summary>Declares the typeInfo class, which represents the type of an entity on the stack.</summary>
public readonly struct typeInfo
{
    private readonly var_types _type;

    private readonly unsafe void* _anonymous;

    // Valid, but not always available, for TYP_REFs.
    private unsafe CORINFO_CLASS_HANDLE _cls => (CORINFO_CLASS_HANDLE)(_anonymous);

    // Valid only for function pointers.
    private unsafe methodPointerInfo* _methodPointerInfo => (methodPointerInfo*)(_anonymous);

    public unsafe typeInfo() : this(TYP_UNDEF)
    {
    }

    public unsafe typeInfo(var_types type)
    {
        _type = type;
        _anonymous = NO_CLASS_HANDLE;
    }

    public unsafe typeInfo(CORINFO_CLASS_HANDLE cls)
    {
        _type = TYP_REF;
        _anonymous = cls;
    }

    public unsafe typeInfo(methodPointerInfo* methodPointerInfo)
    {
        assert(methodPointerInfo is not null);
        assert(methodPointerInfo->m_token.hMethod is not null);

        _type = TYP_I_IMPL;
        _anonymous = methodPointerInfo;
    }

    public unsafe CORINFO_CLASS_HANDLE GetClassHandleForObjRef()
    {
        assert((_type == TYP_REF) || (_type == TYP_UNDEF));
        return (CORINFO_CLASS_HANDLE)(_anonymous);
    }

    public unsafe CORINFO_METHOD_HANDLE GetMethod()
    {
        assert(IsMethod());
        return _methodPointerInfo->m_token.hMethod;
    }

    public unsafe methodPointerInfo* GetMethodPointerInfo()
    {
        assert(IsMethod());
        return _methodPointerInfo;
    }

    public new var_types GetType()
    {
        return _type;
    }

    public bool IsType(var_types type)
    {
        return _type == type;
    }

    // Returns whether this is a method desc
    public unsafe bool IsMethod()
    {
        return IsType(TYP_I_IMPL) && (_methodPointerInfo is not null);
    }
}
