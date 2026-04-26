// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace RyuJitSharp;

public sealed partial class Compiler
{
#if DEBUG
    public bool verbose;
#endif

#if DEBUG
    // Are we doing a fallback compile?
    // That is, have we executed a NO_WAY assert, and we are trying to compile again in a "safer", minopts mode?
    public bool jitFallbackCompile;
#endif

    /// <summary>The Compiler instance for the inlinee</summary>
    public Compiler? InlineeCompiler;

    public Options opts;

    public Info info;

    // the most recently active phase
    public Phases mostRecentlyActivePhase = PHASE_PRE_IMPORT;

#if FUNC_INFO_LOGGING
    // If a log file for per-function information is required, this is the filename to write it to.
    public static string? compJitFuncInfoFilename;

    // And this is the actual FILE* to write to.
    public static StreamWriter? compJitFuncInfoFile;
#endif

    public unsafe Compiler(CORINFO_METHOD_HANDLE methodHandle, COMP_HANDLE jitInfo, CORINFO_METHOD_INFO* methodInfo, InlineInfo? inlineInfo)
    {
        // TODO: Port constructor
    }

    public unsafe void eePrintMethod(StringBuilder stringBuilder, CORINFO_CLASS_HANDLE classHandle, CORINFO_METHOD_HANDLE methodHandle, CORINFO_SIG_INFO* sigInfo, bool includeAssembly, bool includeClass, bool includeClassInstantiation, bool includeMethodInstantiation, bool includeSignature, bool includeReturnType, bool includeThisSpecifier)
    {
        // TODO: Port eePrintMethod
    }

    public unsafe bool eeRunWithErrorTrap<TParam>(delegate* unmanaged[Cdecl]<TParam*, void> function, TParam* parameter)
        where TParam : unmanaged
    {
        return eeRunWithErrorTrapImp((errorTrapFunction)(function), (void*)(parameter));
    }

    public unsafe bool eeRunWithErrorTrapImp(errorTrapFunction function, void* parameter)
    {
        return info.compCompHnd->runWithErrorTrap(function, parameter);
    }

    public unsafe bool eeRunWithSPMIErrorTrap<TParam>(delegate* unmanaged[Cdecl]<TParam*, void> function, TParam* parameter)
        where TParam : unmanaged
    {
        return eeRunWithSPMIErrorTrapImp((errorTrapFunction)(function), (void*)(parameter));
    }

    public unsafe bool eeRunFunctorWithSPMIErrorTrap(Action function)
    {
        var functionHandle = new GCHandle<Action>(function);
        var succeeded = eeRunWithErrorTrapImp(&NativeShim, (void*)(GCHandle<Action>.ToIntPtr(functionHandle)));

        functionHandle.Dispose();
        return succeeded;

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static void NativeShim(void* parameter)
        {
            var functionHandle = GCHandle<Action>.FromIntPtr((nint)(parameter));
            functionHandle.Target();
        }
    }

    public unsafe bool eeRunWithSPMIErrorTrapImp(errorTrapFunction function, void* parameter)
    {
        return info.compCompHnd->runWithSPMIErrorTrap(function, parameter);
    }

    public string compGetTieringName(bool wantShortName = false)
    {
        // TODO: Port compGetTieringName
        return "";
    }

    /// <summary>One-time initialization.</summary>
    public static void compStartup() // 11638
    {
        // TODO: Port compStartup
    }

    /// <summary>One time finalization code.</summary>
    public static void compShutdown()
    {
        // TODO: Port compShutdown
    }

    public unsafe CorJitResult compCompileAfterInit(CORINFO_MODULE_HANDLE moduleHandle, out void* methodCodePtr, out uint methodCodeSize, in JitFlags compileFlags)
    {
        // TODO: Port compCompileAfterInit

        methodCodePtr = null;
        methodCodeSize = 0;

        return CORJIT_INTERNALERROR;
    }

    public unsafe void compFunctionTraceEnd(void* methodCodePtr, uint methodCodeSize, bool isNyi)
    {
        // TODO: Port compFunctionTraceEnd
    }

    /// <summary>Assumes called as part of process shutdown; does any compiler-specific work associated with that.</summary>
    public static unsafe void ProcessShutdownWork(ICorStaticInfo* staticInfo)
    {
    }

#if MEASURE_NOWAY
    public void RecordNowayAssert(ReadOnlySpan<char> filePath, uint line, ReadOnlySpan<char> message)
    {
        // TODO: Port RecordNowayAssert
    }
#endif // MEASURE_NOWAY

    // Should we actually fire the noway assert body and the exception handler?
    public bool compShouldThrowOnNoway()
    {
        // TODO: Port compShouldThrowOnNoway
        return true;
    }
}
