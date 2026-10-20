// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class MethodNameFormattingTests
{
    [Test]
    public static void OpenGenericSignaturePrintsUndefinedArgumentAndReturnTypes()
    {
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info = new Compiler.Info();

        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getArgNext = &GetArgNext;
        vtable.Base.Base.getArgType = &GetArgType;
        vtable.Base.Base.getArrayRank = &GetArrayRank;
        vtable.Base.Base.getMethodSig = &GetMethodSig;
        vtable.Base.Base.getMethodClass = &GetMethodClass;
        vtable.Base.Base.getTypeInstantiationArgument = &GetTypeInstantiationArgument;
        vtable.Base.Base.printClassName = &PrintClassName;
        vtable.Base.Base.printMethodName = &PrintMethodName;
        vtable.Base.Base.runWithSPMIErrorTrap = &RunWithSPMIErrorTrap;
#if DEBUG
        vtable.doAssert = &RecordAssertion;
#endif
        var jitInfo = new ICorJitInfo { lpVtbl = &vtable };
        compiler.info.compCompHnd = &jitInfo;
#if DEBUG
        using var tls = new JitTls(&jitInfo);
#endif
        JitTls.Compiler = compiler;

        try
        {
            var result = compiler.eeGetMethodFullName((CORINFO_METHOD_STRUCT_*)2);

            Assert.That(result, Is.EqualTo("OpenClass:OpenGeneric(<UNDEF>):<UNDEF>"));
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_ARG_LIST_STRUCT_* GetArgNext(
        ICorJitInfo* jitInfo, CORINFO_ARG_LIST_STRUCT_* argument)
        => default;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetArrayRank(ICorJitInfo* jitInfo, CORINFO_CLASS_STRUCT_* classHandle)
        => 0;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void GetMethodSig(
        ICorJitInfo* jitInfo, CORINFO_METHOD_STRUCT_* method,
        CORINFO_SIG_INFO* signature, CORINFO_CLASS_STRUCT_* memberParent)
    {
        *signature = new CORINFO_SIG_INFO
        {
            args = (CORINFO_ARG_LIST_STRUCT_*)4,
            numArgs = 1,
            retType = CorInfoType.CORINFO_TYPE_UNDEF,
        };
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* GetMethodClass(
        ICorJitInfo* jitInfo, CORINFO_METHOD_STRUCT_* method)
        => (CORINFO_CLASS_STRUCT_*)1;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* GetTypeInstantiationArgument(
        ICorJitInfo* jitInfo, CORINFO_CLASS_STRUCT_* classHandle, int index)
        => default;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoTypeWithMod GetArgType(
        ICorJitInfo* jitInfo, CORINFO_SIG_INFO* signature,
        CORINFO_ARG_LIST_STRUCT_* argument, CORINFO_CLASS_STRUCT_** valueClass)
    {
        *valueClass = null;
        return (CorInfoTypeWithMod)CorInfoType.CORINFO_TYPE_UNDEF;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static nint PrintClassName(
        ICorJitInfo* jitInfo, CORINFO_CLASS_STRUCT_* classHandle,
        byte* buffer, nint bufferSize, nint* requiredBufferSize)
    {
        var name = "OpenClass"u8;
        name.CopyTo(new Span<byte>(buffer, name.Length));
        buffer[name.Length] = 0;
        *requiredBufferSize = name.Length + 1;
        return name.Length;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static nint PrintMethodName(
        ICorJitInfo* jitInfo, CORINFO_METHOD_STRUCT_* method,
        byte* buffer, nint bufferSize, nint* requiredBufferSize)
    {
        var name = "OpenGeneric"u8;
        name.CopyTo(new Span<byte>(buffer, name.Length));
        buffer[name.Length] = 0;
        *requiredBufferSize = name.Length + 1;
        return name.Length;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte RunWithSPMIErrorTrap(ICorJitInfo* jitInfo,
        delegate* unmanaged[Cdecl]<void*, void> function, void* parameter)
    {
        function(parameter);
        return 1;
    }

#if DEBUG
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* jitInfo, byte* file, int line, byte* expression)
        => 0;
#endif
}
