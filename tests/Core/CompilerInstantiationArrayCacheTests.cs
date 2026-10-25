// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.CorInfoType;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CompilerInstantiationArrayCacheTests
{
    private static int s_typeInstantiationArgumentCalls;
    private static int s_methodInstantiationArgumentCalls;
    private static int s_childTypeCalls;
    private static int s_arrayRankCalls;
    private static int s_sdArrayCalls;

    [Test]
    public static void InstantiationAndArrayFactsCacheExactInputsAndOutputs()
    {
        s_typeInstantiationArgumentCalls = 0;
        s_methodInstantiationArgumentCalls = 0;
        s_childTypeCalls = 0;
        s_arrayRankCalls = 0;
        s_sdArrayCalls = 0;

        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getTypeInstantiationArgument = &GetTypeInstantiationArgument;
        vtable.Base.Base.getMethodInstantiationArgument = &GetMethodInstantiationArgument;
        vtable.Base.Base.getChildType = &GetChildType;
        vtable.Base.Base.isSDArray = &IsSDArray;
        vtable.Base.Base.getArrayRank = &GetArrayRank;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };

        var firstCompiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        firstCompiler.info.compCompHnd = &jitInfo;

        var firstClass = (CORINFO_CLASS_STRUCT_*)0x101;
        var secondClass = (CORINFO_CLASS_STRUCT_*)0x102;
        var method = (CORINFO_METHOD_STRUCT_*)0x101;

        Assert.That((nint)firstCompiler.eeGetTypeInstantiationArgument(firstClass, 0), Is.EqualTo((nint)0x301));
        Assert.That((nint)firstCompiler.eeGetTypeInstantiationArgument(firstClass, 0), Is.EqualTo((nint)0x301));
        Assert.That(firstCompiler.eeGetTypeInstantiationArgument(firstClass, 1) == null, Is.True);
        Assert.That(firstCompiler.eeGetTypeInstantiationArgument(firstClass, 1) == null, Is.True);
        Assert.That((nint)firstCompiler.eeGetTypeInstantiationArgument(secondClass, 0), Is.EqualTo((nint)0x302));

        Assert.That((nint)firstCompiler.eeGetMethodInstantiationArgument(method, 0), Is.EqualTo((nint)0x401));
        Assert.That((nint)firstCompiler.eeGetMethodInstantiationArgument(method, 0), Is.EqualTo((nint)0x401));
        Assert.That((nint)firstCompiler.eeGetMethodInstantiationArgument(method, 1), Is.EqualTo((nint)0x402));

        var childClass = (CORINFO_CLASS_STRUCT_*)0xDEAD;
        Assert.That(firstCompiler.eeGetChildType(firstClass, &childClass), Is.EqualTo(CORINFO_TYPE_VALUECLASS));
        Assert.That((nint)childClass, Is.EqualTo((nint)0x501));
        childClass = (CORINFO_CLASS_STRUCT_*)0xBEEF;
        Assert.That(firstCompiler.eeGetChildType(firstClass, &childClass), Is.EqualTo(CORINFO_TYPE_VALUECLASS));
        Assert.That((nint)childClass, Is.EqualTo((nint)0x501));

        Assert.That(firstCompiler.eeGetChildType(firstClass, childClassHandle: null), Is.EqualTo(CORINFO_TYPE_VALUECLASS));
        Assert.That(firstCompiler.eeGetChildType(firstClass, childClassHandle: null), Is.EqualTo(CORINFO_TYPE_VALUECLASS));

        childClass = (CORINFO_CLASS_STRUCT_*)0xDEAD;
        Assert.That(firstCompiler.eeGetChildType(secondClass, &childClass), Is.EqualTo(CORINFO_TYPE_INT));
        Assert.That((nint)childClass, Is.EqualTo((nint)0xDEAD));
        childClass = (CORINFO_CLASS_STRUCT_*)0xBEEF;
        Assert.That(firstCompiler.eeGetChildType(secondClass, &childClass), Is.EqualTo(CORINFO_TYPE_INT));
        Assert.That((nint)childClass, Is.EqualTo((nint)0xBEEF));

        Assert.That(firstCompiler.eeGetArrayRank(firstClass), Is.EqualTo(1));
        Assert.That(firstCompiler.eeGetArrayRank(firstClass), Is.EqualTo(1));
        Assert.That(firstCompiler.eeGetArrayRank(secondClass), Is.EqualTo(2));
        Assert.That(firstCompiler.eeIsSDArray(firstClass), Is.True);
        Assert.That(firstCompiler.eeIsSDArray(firstClass), Is.True);
        Assert.That(firstCompiler.eeIsSDArray(secondClass), Is.False);

        var secondCompiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        secondCompiler.info.compCompHnd = &jitInfo;
        Assert.That((nint)secondCompiler.eeGetTypeInstantiationArgument(firstClass, 0), Is.EqualTo((nint)0x301));
        Assert.That((nint)secondCompiler.eeGetMethodInstantiationArgument(method, 0), Is.EqualTo((nint)0x401));
        childClass = (CORINFO_CLASS_STRUCT_*)0xDEAD;
        Assert.That(secondCompiler.eeGetChildType(firstClass, &childClass), Is.EqualTo(CORINFO_TYPE_VALUECLASS));
        Assert.That((nint)childClass, Is.EqualTo((nint)0x501));
        Assert.That(secondCompiler.eeGetArrayRank(firstClass), Is.EqualTo(1));
        Assert.That(secondCompiler.eeIsSDArray(firstClass), Is.True);

        Assert.Multiple(() => {
            Assert.That(s_typeInstantiationArgumentCalls, Is.EqualTo(4));
            Assert.That(s_methodInstantiationArgumentCalls, Is.EqualTo(3));
            Assert.That(s_childTypeCalls, Is.EqualTo(4));
            Assert.That(s_arrayRankCalls, Is.EqualTo(3));
            Assert.That(s_sdArrayCalls, Is.EqualTo(3));
        });
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* GetTypeInstantiationArgument(
        ICorJitInfo* _, CORINFO_CLASS_STRUCT_* classHandle, int index)
    {
        s_typeInstantiationArgumentCalls++;
        return ((nint)classHandle, index) switch {
            (0x101, 0) => (CORINFO_CLASS_STRUCT_*)0x301,
            (0x101, 1) => null,
            (0x102, 0) => (CORINFO_CLASS_STRUCT_*)0x302,
            _ => null,
        };
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* GetMethodInstantiationArgument(
        ICorJitInfo* _, CORINFO_METHOD_STRUCT_* methodHandle, int index)
    {
        s_methodInstantiationArgumentCalls++;
        return ((nint)methodHandle, index) switch {
            (0x101, 0) => (CORINFO_CLASS_STRUCT_*)0x401,
            (0x101, 1) => (CORINFO_CLASS_STRUCT_*)0x402,
            _ => null,
        };
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoType GetChildType(
        ICorJitInfo* _, CORINFO_CLASS_STRUCT_* classHandle, CORINFO_CLASS_STRUCT_** childClassHandle)
    {
        s_childTypeCalls++;
        if ((nint)classHandle == 0x101)
        {
            if (childClassHandle is not null)
            {
                *childClassHandle = (CORINFO_CLASS_STRUCT_*)0x501;
            }

            return CORINFO_TYPE_VALUECLASS;
        }

        return CORINFO_TYPE_INT;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte IsSDArray(ICorJitInfo* _, CORINFO_CLASS_STRUCT_* classHandle)
    {
        s_sdArrayCalls++;
        return (nint)classHandle == 0x101 ? (byte)1 : (byte)0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetArrayRank(ICorJitInfo* _, CORINFO_CLASS_STRUCT_* classHandle)
    {
        s_arrayRankCalls++;
        return (nint)classHandle == 0x101 ? 1 : 2;
    }
}
