// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.CorInfoClassId;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CompilerBuiltinClassCacheTests
{
    private static int s_callCount;
    private static int s_defaultComparerCallCount;
    private static int s_defaultEqualityComparerCallCount;
    private static int s_arrayHelperEnumeratorCallCount;

    [Test]
    public static void BuiltinClassHandlesAreCachedByIdAndCompiler()
    {
        s_callCount = 0;
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getBuiltinClass = &GetBuiltinClass;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };

        var firstCompiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        firstCompiler.info.compCompHnd = &jitInfo;
        Assert.That((nint)firstCompiler.eeGetBuiltinClass(CLASSID_STRING), Is.EqualTo((nint)0x1234));
        Assert.That((nint)firstCompiler.eeGetBuiltinClass(CLASSID_STRING), Is.EqualTo((nint)0x1234));
        Assert.That(firstCompiler.eeGetBuiltinClass(CLASSID_RUNTIME_TYPE) == null, Is.True);
        Assert.That(firstCompiler.eeGetBuiltinClass(CLASSID_RUNTIME_TYPE) == null, Is.True);
        Assert.That(s_callCount, Is.EqualTo(2));

        var secondCompiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        secondCompiler.info.compCompHnd = &jitInfo;
        Assert.That((nint)secondCompiler.eeGetBuiltinClass(CLASSID_STRING), Is.EqualTo((nint)0x1234));
        Assert.That(s_callCount, Is.EqualTo(3));
    }

    [Test]
    public static void ElementTypeClassHandlesAreCachedByElementAndQueryKind()
    {
        s_defaultComparerCallCount = 0;
        s_defaultEqualityComparerCallCount = 0;
        s_arrayHelperEnumeratorCallCount = 0;

        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getDefaultComparerClass = &GetDefaultComparerClass;
        vtable.Base.Base.getDefaultEqualityComparerClass = &GetDefaultEqualityComparerClass;
        vtable.Base.Base.getSZArrayHelperEnumeratorClass = &GetArrayHelperEnumeratorClass;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info.compCompHnd = &jitInfo;

        var firstElement = (CORINFO_CLASS_STRUCT_*)0x101;
        var secondElement = (CORINFO_CLASS_STRUCT_*)0x102;
        Assert.That((nint)compiler.eeGetDefaultComparerClass(firstElement), Is.EqualTo((nint)0x201));
        Assert.That((nint)compiler.eeGetDefaultComparerClass(firstElement), Is.EqualTo((nint)0x201));
        Assert.That(compiler.eeGetDefaultComparerClass(secondElement) == null, Is.True);
        Assert.That(compiler.eeGetDefaultComparerClass(secondElement) == null, Is.True);
        Assert.That((nint)compiler.eeGetDefaultEqualityComparerClass(firstElement), Is.EqualTo((nint)0x301));
        Assert.That((nint)compiler.eeGetDefaultEqualityComparerClass(firstElement), Is.EqualTo((nint)0x301));
        Assert.That((nint)compiler.eeGetArrayHelperEnumeratorClass(firstElement), Is.EqualTo((nint)0x401));
        Assert.That((nint)compiler.eeGetArrayHelperEnumeratorClass(firstElement), Is.EqualTo((nint)0x401));

        Assert.Multiple(() => {
            Assert.That(s_defaultComparerCallCount, Is.EqualTo(2));
            Assert.That(s_defaultEqualityComparerCallCount, Is.EqualTo(1));
            Assert.That(s_arrayHelperEnumeratorCallCount, Is.EqualTo(1));
        });
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* GetBuiltinClass(ICorJitInfo* _, CorInfoClassId classId)
    {
        s_callCount++;
        return classId is CLASSID_STRING ? (CORINFO_CLASS_STRUCT_*)0x1234 : null;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* GetDefaultComparerClass(
        ICorJitInfo* _, CORINFO_CLASS_STRUCT_* elementType)
    {
        s_defaultComparerCallCount++;
        return (nint)elementType is 0x101 ? (CORINFO_CLASS_STRUCT_*)0x201 : null;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* GetDefaultEqualityComparerClass(
        ICorJitInfo* _, CORINFO_CLASS_STRUCT_* elementType)
    {
        s_defaultEqualityComparerCallCount++;
        return (CORINFO_CLASS_STRUCT_*)((nint)elementType + 0x200);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* GetArrayHelperEnumeratorClass(
        ICorJitInfo* _, CORINFO_CLASS_STRUCT_* elementType)
    {
        s_arrayHelperEnumeratorCallCount++;
        return (CORINFO_CLASS_STRUCT_*)((nint)elementType + 0x300);
    }
}
