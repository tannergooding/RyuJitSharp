// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public unsafe partial struct CILJit : ICorJitCompiler.Interface
{
    private ICorJitCompiler.Vtbl* lpVtbl;

    // The main JIT function for the 32 bit JIT.  See code:ICorJitCompiler#EEToJitInterface for more on the EE-JIT
    // interface. Things really don't get going inside the JIT until the code:Compiler.compCompile#Phases
    // method.  Usually that is where you want to go.
    public readonly CorJitResult compileMethod(ICorJitInfo* jitInfo, CORINFO_METHOD_INFO* methodInfo, int flags, byte** nativeEntry, int* nativeSizeOfCode)
    {
        assert(flags is (int)(CORJIT_FLAGS.CORJIT_FLAG_CALL_GETJITFLAGS));
        assert(methodInfo->ILCode is not null);

        CORJIT_FLAGS corJitFlags;
        var jitFlagsSize = jitInfo->getJitFlags(&corJitFlags, sizeof(CORJIT_FLAGS));
        assert(jitFlagsSize == sizeof(CORJIT_FLAGS));

        JitFlags jitFlags = new JitFlags();
        jitFlags.SetFromFlags(corJitFlags);

        // Initialize any necessary thread-local state
        using var jitTls = JitTls.CreateScope(jitInfo);

        var result = jitNativeCode(methodInfo->ftn, methodInfo->scope, jitInfo, methodInfo, out var methodCodePtr, out *nativeSizeOfCode, &jitFlags, inlineInfo: null);

        if (result == CORJIT_OK)
        {
            *nativeEntry = (byte*)methodCodePtr;
        }

        return result;
    }

    public readonly void ProcessShutdownWork(ICorStaticInfo* staticInfo)
    {
        jitShutdown(false);
        Compiler.ProcessShutdownWork(staticInfo);
    }

    public readonly void getVersionIdentifier(Guid* versionIdentifier)
    {
        assert(versionIdentifier is not null);
        *versionIdentifier = JITEEVersionIdentifier;
    }

    public readonly void setTargetOS(CORINFO_OS os)
    {
        TargetOS.SetTargetOS(
            os is CORINFO_WINNT,
            os is CORINFO_UNIX or CORINFO_APPLE,
            os is CORINFO_APPLE);
    }
}
