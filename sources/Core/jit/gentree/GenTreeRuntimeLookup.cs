// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;

namespace RyuJitSharp;

public sealed class GenTreeRuntimeLookup : GenTreeUnOp
{
    private unsafe CORINFO_GENERIC_HANDLE gtHnd;
    private CorInfoGenericHandleType gtHndType;

    internal unsafe GenTreeRuntimeLookup(GenTree tree, CORINFO_GENERIC_HANDLE hnd, CorInfoGenericHandleType hndTyp)
        : base(GT_RUNTIMELOOKUP, tree.Type, tree)
    {
        assert(hnd is not null);
        gtHnd = hnd;
        gtHndType = hndTyp;
    }

    public unsafe CORINFO_CLASS_HANDLE ClassHandle
    {
        get
        {
            assert(Debugger.IsAttached || IsClassHandle);
            return (CORINFO_CLASS_HANDLE)(gtHnd);
        }
    }

    public unsafe CORINFO_FIELD_HANDLE FieldHandle
    {
        get
        {
            assert(Debugger.IsAttached || IsFieldHandle);
            return (CORINFO_FIELD_HANDLE)(gtHnd);
        }
    }

    public unsafe CORINFO_GENERIC_HANDLE Handle => gtHnd;

    public CorInfoGenericHandleType HandleType => gtHndType;

    public bool IsClassHandle => gtHndType is CORINFO_HANDLETYPE_CLASS;

    public bool IsFieldHandle => gtHndType is CORINFO_HANDLETYPE_FIELD;

    public bool IsMethodHandle => gtHndType is CORINFO_HANDLETYPE_METHOD;

    public GenTree Lookup => Op1;

    public unsafe CORINFO_METHOD_HANDLE MethodHandle
    {
        get
        {
            assert(Debugger.IsAttached || IsMethodHandle);
            return (CORINFO_METHOD_HANDLE)(gtHnd);
        }
    }
}
