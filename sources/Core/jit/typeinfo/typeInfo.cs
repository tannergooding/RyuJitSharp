// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace RyuJitSharp;

/// <summary>Declares the typeInfo class, which represents the type of an entity on the stack.</summary>
public readonly struct typeInfo
{
    private readonly var_types _type;

    // Valid, but not always available, for TYP_REFs.
    private readonly unsafe CORINFO_CLASS_HANDLE _cls;

    // Valid only for function pointers.
    private readonly methodPointerInfo? _methodPointerInfo;

    public unsafe typeInfo() : this(TYP_UNDEF)
    {
    }

    public unsafe typeInfo(var_types type)
    {
        _type = type;
    }

    public unsafe typeInfo(CORINFO_CLASS_HANDLE cls)
    {
        _type = TYP_REF;
        _cls = cls;
    }

    public unsafe typeInfo(methodPointerInfo methodPointerInfo)
    {
        assert(methodPointerInfo is not null);
        assert(methodPointerInfo._token.hMethod is not null);

        _type = TYP_I_IMPL;
        _methodPointerInfo = methodPointerInfo;
    }

    public unsafe CORINFO_CLASS_HANDLE ClassHandleForObjRef
    {
        get
        {
            assert(Debugger.IsAttached || (_type is TYP_REF or TYP_UNDEF));
            return _cls;
        }
    }

    [MemberNotNullWhen(true, nameof(_methodPointerInfo), nameof(MethodPointerInfo))]
    public unsafe bool IsMethod => (_type is TYP_I_IMPL) && (_methodPointerInfo is not null);

    public unsafe CORINFO_METHOD_HANDLE Method
    {
        get
        {
            if (IsMethod)
            {
                return _methodPointerInfo._token.hMethod;
            }
            return NO_METHOD_HANDLE;
        }
    }

    public methodPointerInfo? MethodPointerInfo => _methodPointerInfo;

    public var_types Type => _type;
}
