// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static RyuJitSharp.CorInfoHelpFunc;

namespace RyuJitSharp.UnitTests;

internal sealed unsafe class Arm32HelperCallbacks : IDisposable
{
    private readonly ICorJitInfo.Vtbl<ICorJitInfo>* _vtbl;
    private readonly ICorJitInfo* _jitInfo;

    public static CorInfoHelpFunc Helper => s_helper;
    public static int Assertions => s_assertions;

    public Arm32HelperCallbacks(
        Compiler compiler, InfoAccessType accessType, CorInfoReloc relocation, void* address)
    {
        s_accessType = accessType;
        s_relocation = relocation;
        s_address = address;
        s_helper = CORINFO_HELP_UNDEF;
        s_assertions = 0;
        _vtbl = (ICorJitInfo.Vtbl<ICorJitInfo>*)NativeMemory.AllocZeroed(
            (nuint)sizeof(ICorJitInfo.Vtbl<ICorJitInfo>));
        _jitInfo = (ICorJitInfo*)NativeMemory.AllocZeroed((nuint)sizeof(ICorJitInfo));
        _vtbl->Base.getHelperFtn = &GetHelperFtn;
        _vtbl->getRelocTypeHint = &GetRelocTypeHint;
        _vtbl->doAssert = &RecordAssertion;
        _jitInfo->lpVtbl = _vtbl;
        compiler.info.compCompHnd = _jitInfo;
        compiler.info.compMatchedVM = true;
    }

    public void Dispose()
    {
        NativeMemory.Free(_jitInfo);
        NativeMemory.Free(_vtbl);
    }

    private static InfoAccessType s_accessType;
    private static CorInfoReloc s_relocation;
    private static void* s_address;
    private static CorInfoHelpFunc s_helper;
    private static int s_assertions;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void GetHelperFtn(
        ICorJitInfo* self, CorInfoHelpFunc helper, CORINFO_CONST_LOOKUP* lookup, CORINFO_METHOD_STRUCT_** method)
    {
        s_helper = helper;
        lookup->accessType = s_accessType;
        lookup->addr = s_address;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoReloc GetRelocTypeHint(ICorJitInfo* self, void* address)
        => s_relocation;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        s_assertions++;
        return 0;
    }
}
