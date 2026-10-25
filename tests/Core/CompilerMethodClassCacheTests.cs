// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CompilerMethodClassCacheTests
{
    private static int s_methodClassCalls;

    [Test]
    public static void MethodClassesCacheByHandleAndCompilerAndReuseRootFacts()
    {
        s_methodClassCalls = 0;

        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getMethodClass = &GetMethodClass;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };

        var firstCompiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        firstCompiler.info.compCompHnd = &jitInfo;
        firstCompiler.info.compMethodHnd = (CORINFO_METHOD_STRUCT_*)0x100;
        firstCompiler.info.compClassHnd = (CORINFO_CLASS_STRUCT_*)0x500;

        Assert.That((nint)firstCompiler.eeGetMethodClass(firstCompiler.info.compMethodHnd), Is.EqualTo((nint)0x500));

        var firstMethod = (CORINFO_METHOD_STRUCT_*)0x101;
        var secondMethod = (CORINFO_METHOD_STRUCT_*)0x102;
        var missingMethod = (CORINFO_METHOD_STRUCT_*)0x103;
        Assert.That((nint)firstCompiler.eeGetMethodClass(firstMethod), Is.EqualTo((nint)0x301));
        Assert.That((nint)firstCompiler.eeGetMethodClass(firstMethod), Is.EqualTo((nint)0x301));
        Assert.That((nint)firstCompiler.eeGetMethodClass(secondMethod), Is.EqualTo((nint)0x302));
        Assert.That(firstCompiler.eeGetMethodClass(missingMethod) == null, Is.True);
        Assert.That(firstCompiler.eeGetMethodClass(missingMethod) == null, Is.True);

        var secondCompiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        secondCompiler.info.compCompHnd = &jitInfo;
        secondCompiler.info.compMethodHnd = (CORINFO_METHOD_STRUCT_*)0x100;
        secondCompiler.info.compClassHnd = (CORINFO_CLASS_STRUCT_*)0x600;
        Assert.That((nint)secondCompiler.eeGetMethodClass(secondCompiler.info.compMethodHnd), Is.EqualTo((nint)0x600));
        Assert.That((nint)secondCompiler.eeGetMethodClass(firstMethod), Is.EqualTo((nint)0x301));

        Assert.That(s_methodClassCalls, Is.EqualTo(4));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* GetMethodClass(
        ICorJitInfo* _, CORINFO_METHOD_STRUCT_* methodHandle)
    {
        s_methodClassCalls++;
        return (nint)methodHandle switch {
            0x101 => (CORINFO_CLASS_STRUCT_*)0x301,
            0x102 => (CORINFO_CLASS_STRUCT_*)0x302,
            _ => null,
        };
    }
}
