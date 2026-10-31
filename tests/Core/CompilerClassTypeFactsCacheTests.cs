// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.CorInfoFlag;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CompilerClassTypeFactsCacheTests
{
    private static int s_classAttribsCalls;
    private static int s_valueClassCalls;

    [Test]
    public static void ClassAttributesAndValueClassQueriesCacheByHandleAndCompiler()
    {
        s_classAttribsCalls = 0;
        s_valueClassCalls = 0;

        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getClassAttribs = &GetClassAttribs;
        vtable.Base.Base.isByRefLike = &IsByRefLike;
        vtable.Base.Base.isSharedInst = &IsSharedInst;
        vtable.Base.Base.isValueClass = &IsValueClass;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };

        var firstCompiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        firstCompiler.info.compCompHnd = &jitInfo;

        var firstClass = (CORINFO_CLASS_STRUCT_*)0x101;
        var secondClass = (CORINFO_CLASS_STRUCT_*)0x102;
        var firstClassAttribs = CORINFO_FLG_BYREF_LIKE | CORINFO_FLG_SHAREDINST;

        Assert.That(firstCompiler.eeGetClassAttribs(firstClass), Is.EqualTo(firstClassAttribs));
        Assert.That(firstCompiler.eeGetClassAttribs(firstClass), Is.EqualTo(firstClassAttribs));
        Assert.That(firstCompiler.eeIsByRefLike(firstClass), Is.True);
        Assert.That(firstCompiler.eeIsSharedInst(firstClass), Is.True);
        Assert.That(firstCompiler.eeGetClassAttribs(secondClass), Is.EqualTo(CORINFO_FLG_FINAL));

        var secondCompiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        secondCompiler.info.compCompHnd = &jitInfo;
        Assert.That(secondCompiler.eeGetClassAttribs(firstClass), Is.EqualTo(firstClassAttribs));

        Assert.That(firstCompiler.eeIsValueClass(firstClass), Is.True);
        Assert.That(firstCompiler.eeIsValueClass(firstClass), Is.True);
        Assert.That(firstCompiler.eeIsValueClass(secondClass), Is.False);
        Assert.That(secondCompiler.eeIsValueClass(firstClass), Is.True);

        Assert.Multiple(() => {
            Assert.That(s_classAttribsCalls, Is.EqualTo(3));
            Assert.That(s_valueClassCalls, Is.EqualTo(3));
        });
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoFlag GetClassAttribs(ICorJitInfo* _, CORINFO_CLASS_STRUCT_* classHandle)
    {
        s_classAttribsCalls++;
        return (nint)classHandle == 0x101
             ? CORINFO_FLG_BYREF_LIKE | CORINFO_FLG_SHAREDINST
             : CORINFO_FLG_FINAL;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte IsByRefLike(ICorJitInfo* _, CORINFO_CLASS_STRUCT_* classHandle)
        => (byte)((nint)classHandle == 0x101 ? 1 : 0);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte IsSharedInst(ICorJitInfo* _, CORINFO_CLASS_STRUCT_* classHandle)
        => (byte)((nint)classHandle == 0x101 ? 1 : 0);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte IsValueClass(ICorJitInfo* _, CORINFO_CLASS_STRUCT_* classHandle)
    {
        s_valueClassCalls++;
        return (nint)classHandle == 0x101 ? (byte)1 : (byte)0;
    }
}
