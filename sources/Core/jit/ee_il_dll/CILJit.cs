// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics;

namespace RyuJitSharp;

public unsafe partial struct CILJit : ICorJitCompiler.Interface
{
    internal ICorJitCompiler.Vtbl* lpVtbl;

    // The main JIT function for the 32 bit JIT.  See code:ICorJitCompiler#EEToJitInterface for more on the EE-JIT
    // interface. Things really don't get going inside the JIT until the code:Compiler::compCompile#Phases
    // method.  Usually that is where you want to go.
    public CorJitResult compileMethod(ICorJitInfo* comp, CORINFO_METHOD_INFO* info, uint flags, byte** nativeEntry, uint* nativeSizeOfCode)
    {
        Debug.Assert(flags == unchecked((uint)CORJIT_FLAGS.CorJitFlag.CORJIT_FLAG_CALL_GETJITFLAGS));
        Debug.Assert(info->ILCode != null);

        CORJIT_FLAGS corJitFlags;
        uint jitFlagsSize = comp->getJitFlags(&corJitFlags, (uint)sizeof(CORJIT_FLAGS));
        Debug.Assert(jitFlagsSize == (uint)sizeof(CORJIT_FLAGS));

        JitFlags jitFlags = new JitFlags();
        jitFlags.SetFromFlags(corJitFlags);

#if DEBUG
        // Initialize any necessary thread-local state
        using JitTls jitTls = new JitTls(comp);
#endif

        void* methodCodePtr = null;
        CorJitResult result = (CorJitResult)jitNativeCode(info->ftn, info->scope, comp, info, &methodCodePtr, nativeSizeOfCode, &jitFlags, null);

        if (result == CORJIT_OK)
        {
            *nativeEntry = (byte*)methodCodePtr;
        }
        return result;
    }

    public void ProcessShutdownWork(ICorStaticInfo* info)
    {
        jitShutdown(false);
        Compiler.ProcessShutdownWork(info);
    }

    public void getVersionIdentifier(Guid* versionIdentifier)
    {
        Debug.Assert(versionIdentifier != null);
        *versionIdentifier = JITEEVersionIdentifier;
    }

    public void setTargetOS(CORINFO_OS os)
    {
        if (TARGET_OS_RUNTIMEDETERMINED)
        {
            TargetOS.IsApplePlatform = os is CORINFO_APPLE;

            // This define is only set if ONLY the different unix variants are runtime determined
            if (!TARGET_UNIX_OS_RUNTIMEDETERMINED)
            {
                TargetOS.IsUnix = os is CORINFO_UNIX or CORINFO_APPLE;
                TargetOS.IsWindows = os is CORINFO_WINNT;
            }

            TargetOS.OSSettingConfigured = true;
        }
    }
}
