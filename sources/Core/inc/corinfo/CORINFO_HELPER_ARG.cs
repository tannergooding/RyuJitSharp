// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public unsafe struct CORINFO_HELPER_ARG
{
    private _Anonymous_e__Union _anonymous;

    [UnscopedRef]
    public ref CORINFO_FIELD_HANDLE fieldHandle => ref _anonymous.fieldHandle;

    [UnscopedRef]
    public ref CORINFO_METHOD_HANDLE methodHandle => ref _anonymous.methodHandle;

    [UnscopedRef]
    public ref CORINFO_CLASS_HANDLE classHandle => ref _anonymous.classHandle;

    [UnscopedRef]
    public ref CORINFO_MODULE_HANDLE moduleHandle => ref _anonymous.moduleHandle;

    [UnscopedRef]
    public ref nuint constant => ref _anonymous.constant;

    public CorInfoAccessAllowedHelperArgType argType;

    public void Set(CORINFO_METHOD_HANDLE handle)
    {
        argType = CORINFO_HELPER_ARG_TYPE_Method;
        methodHandle = handle;
    }

    public void Set(CORINFO_FIELD_HANDLE handle)
    {
        argType = CORINFO_HELPER_ARG_TYPE_Field;
        fieldHandle = handle;
    }

    public void Set(CORINFO_CLASS_HANDLE handle)
    {
        argType = CORINFO_HELPER_ARG_TYPE_Class;
        classHandle = handle;
    }

    public void Set(nuint value)
    {
        argType = CORINFO_HELPER_ARG_TYPE_Const;
        constant = value;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct _Anonymous_e__Union
    {
        [FieldOffset(0)]
        public CORINFO_FIELD_HANDLE fieldHandle;

        [FieldOffset(0)]
        public CORINFO_METHOD_HANDLE methodHandle;

        [FieldOffset(0)]
        public CORINFO_CLASS_HANDLE classHandle;

        [FieldOffset(0)]
        public CORINFO_MODULE_HANDLE moduleHandle;

        [FieldOffset(0)]
        public nuint constant;
    }
}
