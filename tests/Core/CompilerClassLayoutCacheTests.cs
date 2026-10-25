// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CompilerClassLayoutCacheTests
{
    private static int s_classSizeCalls;
    private static int s_heapClassSizeCalls;
    private static int s_alignmentCalls;

    [Test]
    public static void ClassLayoutQueriesCacheByHandleKindAndAlignmentHint()
    {
        s_classSizeCalls = 0;
        s_heapClassSizeCalls = 0;
        s_alignmentCalls = 0;

        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getClassSize = &GetClassSize;
        vtable.Base.Base.getHeapClassSize = &GetHeapClassSize;
        vtable.Base.Base.getClassAlignmentRequirement =
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_STRUCT_*, bool, int>)
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_STRUCT_*, byte, int>)&GetClassAlignmentRequirement;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info.compCompHnd = &jitInfo;

        var firstClass = (CORINFO_CLASS_STRUCT_*)0x100;
        var secondClass = (CORINFO_CLASS_STRUCT_*)0x200;

        Assert.That(compiler.eeGetClassSize(firstClass), Is.EqualTo(16));
        Assert.That(compiler.eeGetClassSize(firstClass), Is.EqualTo(16));
        Assert.That(compiler.eeGetClassSize(secondClass), Is.EqualTo(24));
        Assert.That(compiler.eeGetHeapClassSize(firstClass), Is.EqualTo(32));
        Assert.That(compiler.eeGetHeapClassSize(firstClass), Is.EqualTo(32));
        Assert.That(compiler.eeGetClassAlignmentRequirement(firstClass), Is.EqualTo(4));
        Assert.That(compiler.eeGetClassAlignmentRequirement(firstClass), Is.EqualTo(4));
        Assert.That(compiler.eeGetClassAlignmentRequirement(firstClass, fDoubleAlignHint: true), Is.EqualTo(8));
        Assert.That(compiler.eeGetClassAlignmentRequirement(firstClass, fDoubleAlignHint: true), Is.EqualTo(8));
        Assert.That(compiler.eeGetClassAlignmentRequirement(secondClass), Is.EqualTo(2));

        var layout = new ClassLayout(firstClass, true, 16, TYP_STRUCT, "Struct", "Struct");
        Assert.That(layout.GetAlignmentRequirement(compiler), Is.EqualTo(4));

        var secondCompiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        secondCompiler.info.compCompHnd = &jitInfo;
        Assert.That(secondCompiler.eeGetClassSize(firstClass), Is.EqualTo(16));

        Assert.Multiple(() => {
            Assert.That(s_classSizeCalls, Is.EqualTo(3));
            Assert.That(s_heapClassSizeCalls, Is.EqualTo(1));
            Assert.That(s_alignmentCalls, Is.EqualTo(3));
        });
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetClassSize(ICorJitInfo* _, CORINFO_CLASS_STRUCT_* classHandle)
    {
        s_classSizeCalls++;
        return (nint)classHandle == 0x100 ? 16 : 24;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetHeapClassSize(ICorJitInfo* _, CORINFO_CLASS_STRUCT_* classHandle)
    {
        s_heapClassSizeCalls++;
        return (nint)classHandle == 0x100 ? 32 : 40;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetClassAlignmentRequirement(
        ICorJitInfo* _, CORINFO_CLASS_STRUCT_* classHandle, byte doubleAlignHint)
    {
        s_alignmentCalls++;
        return (doubleAlignHint != 0) ? 8 : ((nint)classHandle == 0x100 ? 4 : 2);
    }
}
