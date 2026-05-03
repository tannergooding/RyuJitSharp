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

    /// <summary>Get the assembly name of a type.</summary>
    /// <param name="clsHnd">the handle of the class</param>
    /// <returns>The name string.</returns>
    /// <remarks>If missing information (in SPMI), then return a placeholder string.</remarks>
    public unsafe string eeGetClassAssemblyName(CORINFO_CLASS_HANDLE clsHnd)
    {
        var assemblyName = "<unknown assembly>";

        var success = eeRunFunctorWithSpmiErrorTrap(() => {
            var pClassAssemblyNameUtf8 = info.compCompHnd->getClassAssemblyName(clsHnd);
            var classAssemblyNameUtf8 = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(pClassAssemblyNameUtf8);
            assemblyName = Encoding.UTF8.GetString(classAssemblyNameUtf8);
        });

        return (assemblyName is not null) ? assemblyName : "<no assembly>";
    }

    /// <summary>Get the name (including namespace and instantiation) of a type.</summary>
    /// <param name="classHandle">the handle of the class</param>
    /// <param name="stringBuilder">Pre-existing string builder to use or <c>null</c> to create an internal one</param>
    /// <returns>The name string.</returns>
    /// <remarks>If missing information (in SPMI), then return a placeholder string.</remarks>
    public unsafe string eeGetClassName(CORINFO_CLASS_HANDLE classHandle, StringBuilder? stringBuilder = null)
    {
        stringBuilder ??= new StringBuilder();

        var success = eeRunFunctorWithSpmiErrorTrap(() =>
            eePrintType(
                stringBuilder,
                classHandle,
                includeInstantiation: true
            )
        );

        if (!success)
        {
            _ = stringBuilder.Clear();
            _ = stringBuilder.Append("<unknown class>");
        }
        return stringBuilder.ToString();
    }

    public unsafe ref CORINFO_EE_INFO eeGetEEInfo()
    {
        if (!eeInfoInitialized)
        {
            fixed (CORINFO_EE_INFO* pEEInfo = &eeInfo)
            {
                info.compCompHnd->getEEInfo(pEEInfo);
            }
            eeInfoInitialized = true;
        }
        return ref eeInfo;
    }

    public unsafe CorInfoHelpFunc eeGetHelperNum(CORINFO_METHOD_HANDLE method)
    {
        if (((nuint)(method) & 1) == 0)
        {
            // Helpers are marked by the fact that they are odd numbers
            return CORINFO_HELP_UNDEF;
        }
        return unchecked((CorInfoHelpFunc)((nuint)(method) >> 2));
    }

    /// <summary>Get a string describing a method.</summary>
    /// <param name="methodHandle">the method handle</param>
    /// <param name="includeReturnType">Whether to include the return type in the string</param>
    /// <param name="includeThisSpecifier">Whether to include a specifier for whether this is an instance method.</param>
    /// <param name="stringBuilder">Pre-existing string builder to use or <c>null</c> to create an internal one</param>
    /// <returns>The string</returns>
    public unsafe string eeGetMethodFullName(CORINFO_METHOD_HANDLE methodHandle, bool includeReturnType = true, bool includeThisSpecifier = true, StringBuilder? stringBuilder = null)
    {
        var helper = eeGetHelperNum(methodHandle);

        if (helper != CORINFO_HELP_UNDEF)
        {
            return helper.ToString();
        }

        stringBuilder ??= new StringBuilder();
        var classHandle = NO_CLASS_HANDLE;

        var success = eeRunFunctorWithSpmiErrorTrap(() => {
            classHandle = info.compCompHnd->getMethodClass(methodHandle);
            eeGetMethodSig(methodHandle, out var sigInfo);
            eePrintMethod(
                stringBuilder,
                classHandle,
                methodHandle,
                &sigInfo,
                includeAssembly: false,
                includeClass: true,
                includeClassInstantiation: true,
                includeMethodInstantiation: true,
                includeSignature: true,
                includeReturnType,
                includeThisSpecifier
            );
        });

        if (success)
        {
            return stringBuilder.ToString();
        }

        // Try without signature
        _ = stringBuilder.Clear();
        return eeGetMethodName(methodHandle, stringBuilder);
    }

    /// <summary>Get the name of a method.</summary>
    /// <param name="methodHandle">the method handle</param>
    /// <param name="stringBuilder">Pre-existing string builder to use or <c>null</c> to create an internal one</param>
    /// <returns>The string</returns>
    /// <remarks>See <see cref="eeGetMethodFullName" /> for documentation about the buffer.</remarks>
    public unsafe string eeGetMethodName(CORINFO_METHOD_HANDLE methodHandle, StringBuilder? stringBuilder = null)
    {
        stringBuilder ??= new StringBuilder();

        var success = eeRunFunctorWithSpmiErrorTrap(() =>
            eePrintMethod(
                stringBuilder,
                NO_CLASS_HANDLE,
                methodHandle,
                sigInfo: null,
                includeAssembly: false,
                includeClass: false,
                includeClassInstantiation: false,
                includeMethodInstantiation: false,
                includeSignature: false,
                includeReturnType: false,
                includeThisSpecifier: false
            )
        );

        if (!success)
        {
            _ = stringBuilder.Clear();
            _ = stringBuilder.Append("<unknown method>");
        }
        return stringBuilder.ToString();
    }

    public unsafe void eeGetMethodSig(CORINFO_METHOD_HANDLE methHnd, out CORINFO_SIG_INFO returnSigInfo, CORINFO_CLASS_HANDLE owner = null)
    {
        fixed (CORINFO_SIG_INFO* pReturnSigInfo = &returnSigInfo)
        {
            info.compCompHnd->getMethodSig(methHnd, pReturnSigInfo, owner);
        }
        assert(!varTypeIsComposite(returnSigInfo.retType.VarType) || (returnSigInfo.retTypeClass is not null));
    }

    public unsafe bool eeIsByrefLike(CORINFO_CLASS_HANDLE classHandle)
        => (info.compCompHnd->getClassAttribs(classHandle) & CORINFO_FLG_BYREF_LIKE) != 0;

    public unsafe bool eeIsFieldStatic(CORINFO_FIELD_HANDLE fieldHandle)
        => info.compCompHnd->isFieldStatic(fieldHandle);

    public unsafe void eePrintMethod(StringBuilder stringBuilder, CORINFO_CLASS_HANDLE classHandle, CORINFO_METHOD_HANDLE methodHandle, CORINFO_SIG_INFO* sigInfo, bool includeAssembly, bool includeClass, bool includeClassInstantiation, bool includeMethodInstantiation, bool includeSignature, bool includeReturnType, bool includeThisSpecifier)
    {
        // TODO: Port eePrintMethod
    }

    public unsafe void eePrintType(StringBuilder stringBuilder, CORINFO_CLASS_HANDLE clsHnd, bool includeInstantiation)
    {
        // TODO: Port eePrintType
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

    public unsafe bool eeRunFunctorWithSpmiErrorTrap(Action function)
    {
        var functionHandle = new GCHandle<Action>(function);
        var succeeded = info.compCompHnd->runWithErrorTrap(&NativeShim, (void*)(GCHandle<Action>.ToIntPtr(functionHandle)));

        functionHandle.Dispose();
        return succeeded;

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static void NativeShim(void* parameter)
        {
            var functionHandle = GCHandle<Action>.FromIntPtr(unchecked((nint)(parameter)));
            functionHandle.Target();
        }
    }
}
