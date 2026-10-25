// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.CorInfoCallConv;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CompilerDiagnosticNameCacheTests
{
    private static int s_classNameCalls;
    private static int s_fieldNameCalls;
    private static int s_methodNameCalls;
    private static int s_arrayRankCalls;
    private static int s_typeInstantiationArgumentCalls;
    private static int s_fieldClassCalls;
    private static int s_methodClassCalls;
    private static int s_methodSigCalls;
    private static bool s_failNextErrorTrap;

    [Test]
    public static void SuccessfulDiagnosticNamesCacheByHandleAndFormattingFlags()
    {
        s_classNameCalls = 0;
        s_fieldNameCalls = 0;
        s_methodNameCalls = 0;
        s_arrayRankCalls = 0;
        s_typeInstantiationArgumentCalls = 0;
        s_fieldClassCalls = 0;
        s_methodClassCalls = 0;
        s_methodSigCalls = 0;
        s_failNextErrorTrap = true;

        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.printClassName = &PrintClassName;
        vtable.Base.Base.printFieldName = &PrintFieldName;
        vtable.Base.Base.printMethodName = &PrintMethodName;
        vtable.Base.Base.getArrayRank = &GetArrayRank;
        vtable.Base.Base.getTypeInstantiationArgument = &GetTypeInstantiationArgument;
        vtable.Base.Base.getFieldClass = &GetFieldClass;
        vtable.Base.Base.getMethodClass = &GetMethodClass;
        vtable.Base.Base.getMethodSig = &GetMethodSig;
        vtable.Base.Base.runWithSPMIErrorTrap = &RunWithSPMIErrorTrap;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info.compCompHnd = &jitInfo;

        var classHandle = (CORINFO_CLASS_STRUCT_*)0x100;
        var fieldHandle = (CORINFO_FIELD_STRUCT_*)0x200;
        var methodHandle = (CORINFO_METHOD_STRUCT_*)0x300;

        Assert.That(compiler.eeGetClassName(classHandle), Is.EqualTo("<unknown class>"));
        Assert.That(compiler.eeGetClassName(classHandle), Is.EqualTo("TestClass"));
        Assert.That(compiler.eeGetClassName(classHandle), Is.EqualTo("TestClass"));
        Assert.That(compiler.eeGetShortClassName(classHandle), Is.EqualTo("TestClass"));
        Assert.That(compiler.eeGetShortClassName(classHandle), Is.EqualTo("TestClass"));

        Assert.That(compiler.eeGetFieldName(fieldHandle, includeType: false), Is.EqualTo("TestField"));
        Assert.That(compiler.eeGetFieldName(fieldHandle, includeType: false), Is.EqualTo("TestField"));
        Assert.That(compiler.eeGetFieldName(fieldHandle, includeType: true), Is.EqualTo("TestClass:TestField"));
        Assert.That(compiler.eeGetFieldName(fieldHandle, includeType: true), Is.EqualTo("TestClass:TestField"));

        Assert.That(compiler.eeGetMethodName(methodHandle), Is.EqualTo("TestMethod"));
        Assert.That(compiler.eeGetMethodName(methodHandle), Is.EqualTo("TestMethod"));
        Assert.That(compiler.eeGetMethodFullName(methodHandle), Is.EqualTo("TestClass:TestMethod():int:this"));
        Assert.That(compiler.eeGetMethodFullName(methodHandle), Is.EqualTo("TestClass:TestMethod():int:this"));
        Assert.That(compiler.eeGetMethodFullName(methodHandle, includeReturnType: false),
            Is.EqualTo("TestClass:TestMethod():this"));
        Assert.That(compiler.eeGetMethodFullName(methodHandle, includeReturnType: false),
            Is.EqualTo("TestClass:TestMethod():this"));
        Assert.That(compiler.eeGetMethodFullName(methodHandle, includeThisSpecifier: false),
            Is.EqualTo("TestClass:TestMethod():int"));
        Assert.That(compiler.eeGetMethodFullName(methodHandle, includeThisSpecifier: false),
            Is.EqualTo("TestClass:TestMethod():int"));
        Assert.That(compiler.eeGetMethodFullName(
            methodHandle, includeReturnType: false, includeThisSpecifier: false),
            Is.EqualTo("TestClass:TestMethod()"));
        Assert.That(compiler.eeGetMethodFullName(
            methodHandle, includeReturnType: false, includeThisSpecifier: false),
            Is.EqualTo("TestClass:TestMethod()"));

        Assert.Multiple(() => {
            Assert.That(s_classNameCalls, Is.EqualTo(8));
            Assert.That(s_fieldNameCalls, Is.EqualTo(2));
            Assert.That(s_methodNameCalls, Is.EqualTo(5));
            Assert.That(s_arrayRankCalls, Is.EqualTo(8));
            Assert.That(s_typeInstantiationArgumentCalls, Is.EqualTo(7));
            Assert.That(s_fieldClassCalls, Is.EqualTo(1));
            Assert.That(s_methodClassCalls, Is.EqualTo(4));
            Assert.That(s_methodSigCalls, Is.EqualTo(4));
        });

        var boundedCompiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        boundedCompiler.info.compCompHnd = &jitInfo;
        var callsBeforeBoundedNames = s_classNameCalls;
        for (var index = 0; index < 65; index++)
        {
            _ = boundedCompiler.eeGetClassName((CORINFO_CLASS_STRUCT_*)(nint)(0x1000 + index));
        }

        _ = boundedCompiler.eeGetClassName((CORINFO_CLASS_STRUCT_*)0x1000);
        _ = boundedCompiler.eeGetClassName((CORINFO_CLASS_STRUCT_*)0x1040);
        _ = boundedCompiler.eeGetClassName((CORINFO_CLASS_STRUCT_*)0x1040);

        Assert.That(s_classNameCalls - callsBeforeBoundedNames, Is.EqualTo(67));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static nint PrintClassName(
        ICorJitInfo* _, CORINFO_CLASS_STRUCT_* classHandle,
        byte* buffer, nint bufferSize, nint* requiredBufferSize)
    {
        s_classNameCalls++;
        return PrintUtf8("TestClass"u8, buffer, bufferSize, requiredBufferSize);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static nint PrintFieldName(
        ICorJitInfo* _, CORINFO_FIELD_STRUCT_* fieldHandle,
        byte* buffer, nint bufferSize, nint* requiredBufferSize)
    {
        s_fieldNameCalls++;
        return PrintUtf8("TestField"u8, buffer, bufferSize, requiredBufferSize);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static nint PrintMethodName(
        ICorJitInfo* _, CORINFO_METHOD_STRUCT_* methodHandle,
        byte* buffer, nint bufferSize, nint* requiredBufferSize)
    {
        s_methodNameCalls++;
        return PrintUtf8("TestMethod"u8, buffer, bufferSize, requiredBufferSize);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetArrayRank(ICorJitInfo* _, CORINFO_CLASS_STRUCT_* classHandle)
    {
        s_arrayRankCalls++;
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* GetTypeInstantiationArgument(
        ICorJitInfo* _, CORINFO_CLASS_STRUCT_* classHandle, int index)
    {
        s_typeInstantiationArgumentCalls++;
        return null;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* GetFieldClass(
        ICorJitInfo* _, CORINFO_FIELD_STRUCT_* fieldHandle)
    {
        s_fieldClassCalls++;
        return (CORINFO_CLASS_STRUCT_*)0x100;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* GetMethodClass(
        ICorJitInfo* _, CORINFO_METHOD_STRUCT_* methodHandle)
    {
        s_methodClassCalls++;
        return (CORINFO_CLASS_STRUCT_*)0x100;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void GetMethodSig(
        ICorJitInfo* _, CORINFO_METHOD_STRUCT_* methodHandle,
        CORINFO_SIG_INFO* signature, CORINFO_CLASS_STRUCT_* memberParent)
    {
        s_methodSigCalls++;
        *signature = new CORINFO_SIG_INFO {
            callConv = CORINFO_CALLCONV_HASTHIS,
            retType = CorInfoType.CORINFO_TYPE_INT,
        };
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte RunWithSPMIErrorTrap(
        ICorJitInfo* _, delegate* unmanaged[Cdecl]<void*, void> function, void* parameter)
    {
        function(parameter);
        if (s_failNextErrorTrap)
        {
            s_failNextErrorTrap = false;
            return 0;
        }

        return 1;
    }

    private static int PrintUtf8(ReadOnlySpan<byte> name, byte* buffer, nint bufferSize, nint* requiredBufferSize)
    {
        var requiredSize = name.Length + 1;
        if (bufferSize < requiredSize)
        {
            *requiredBufferSize = requiredSize;
            return 0;
        }

        name.CopyTo(new Span<byte>(buffer, name.Length));
        buffer[name.Length] = 0;
        *requiredBufferSize = requiredSize;
        return name.Length;
    }
}
