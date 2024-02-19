// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public unsafe partial struct CILJit
{
    internal static readonly ICorJitCompiler.Vtbl* s_vtbl = InitVtbl();

    private static ICorJitCompiler.Vtbl* InitVtbl()
    {
        ICorJitCompiler.Vtbl* lpVtbl = (ICorJitCompiler.Vtbl*)RuntimeHelpers.AllocateTypeAssociatedMemory(typeof(CILJit), sizeof(ICorJitCompiler.Vtbl));

        lpVtbl->compileMethod = &compileMethod;
        lpVtbl->ProcessShutdownWork = &ProcessShutdownWork;
        lpVtbl->getVersionIdentifier = &getVersionIdentifier;
        lpVtbl->setTargetOS = &setTargetOS;

        return lpVtbl;
    }


    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorJitResult compileMethod(ICorJitCompiler* pSelf, ICorJitInfo* comp, CORINFO_METHOD_INFO* info, uint flags, byte** nativeEntry, uint* nativeSizeOfCode) => ((CILJit*)pSelf)->compileMethod(comp, info, flags, nativeEntry, nativeSizeOfCode);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void ProcessShutdownWork(ICorJitCompiler* pSelf, ICorStaticInfo* info) => ((CILJit*)pSelf)->ProcessShutdownWork(info);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void getVersionIdentifier(ICorJitCompiler* pSelf, Guid* versionIdentifier) => ((CILJit*)pSelf)->getVersionIdentifier(versionIdentifier);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void setTargetOS(ICorJitCompiler* pSelf, CORINFO_OS os) => ((CILJit*)pSelf)->setTargetOS(os);
}
