// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace RyuJitSharp;

public partial class Compiler
{
    public CORINFO_EE_INFO eeInfo;

    public bool eeInfoInitialized;

    public uint eeBoundariesCount;

    /// <summary>Boundaries to report to the EE</summary>
    public unsafe ICorDebugInfo.OffsetMapping* eeBoundaries;

    public uint eeVarsCount;

    public VarResultInfo? eeVars;

    public unsafe bool eeIsByrefLike(CORINFO_CLASS_HANDLE classHandle) => ((CorInfoFlag)(info.compCompHnd->getClassAttribs(classHandle)) & CORINFO_FLG_BYREF_LIKE) != 0;

    public unsafe bool eeIsFieldStatic(CORINFO_FIELD_HANDLE fieldHandle) => info.compCompHnd->isFieldStatic(fieldHandle);

    public unsafe void eePrintMethod(StringBuilder stringBuilder, CORINFO_CLASS_HANDLE classHandle, CORINFO_METHOD_HANDLE methodHandle, CORINFO_SIG_INFO* sigInfo, bool includeAssembly, bool includeClass, bool includeClassInstantiation, bool includeMethodInstantiation, bool includeSignature, bool includeReturnType, bool includeThisSpecifier)
    {
        // TODO: Port eePrintMethod
    }

    public unsafe bool eeRunWithErrorTrap<TParam>(delegate* unmanaged[Cdecl]<TParam*, void> function, TParam* parameter)
        where TParam : unmanaged
    {
        return info.compCompHnd->runWithErrorTrap((errorTrapFunction)(function), parameter);
    }

    public unsafe bool eeRunWithSpmiErrorTrap<TParam>(delegate* unmanaged[Cdecl]<TParam*, void> function, TParam* parameter)
        where TParam : unmanaged
    {
        return info.compCompHnd->runWithSPMIErrorTrap((errorTrapFunction)(function), parameter);
    }

    public unsafe bool eeRunFunctorWithSPMIErrorTrap(Action function)
    {
        var functionHandle = new GCHandle<Action>(function);
        var succeeded = info.compCompHnd->runWithErrorTrap(&NativeShim, (void*)(GCHandle<Action>.ToIntPtr(functionHandle)));

        functionHandle.Dispose();
        return succeeded;

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static void NativeShim(void* parameter)
        {
            var functionHandle = GCHandle<Action>.FromIntPtr((nint)(parameter));
            functionHandle.Target();
        }
    }
}
