// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CompilerIntrinsicTypeCacheTests
{
    private static int s_intrinsicTypeCalls;

    [Test]
    public static void IntrinsicTypeCacheRetainsLastResultPerCompiler()
    {
        s_intrinsicTypeCalls = 0;

        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.isIntrinsicType = &IsIntrinsicType;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };

        var firstCompiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        firstCompiler.info.compCompHnd = &jitInfo;

        var intrinsicClass = (CORINFO_CLASS_STRUCT_*)0x101;
        var regularClass = (CORINFO_CLASS_STRUCT_*)0x102;

        Assert.That(firstCompiler.eeIsIntrinsicType(intrinsicClass), Is.True);
        Assert.That(firstCompiler.eeIsIntrinsicType(intrinsicClass), Is.True);
        Assert.That(firstCompiler.eeIsIntrinsicType(regularClass), Is.False);
        Assert.That(firstCompiler.eeIsIntrinsicType(regularClass), Is.False);
        Assert.That(firstCompiler.eeIsIntrinsicType(intrinsicClass), Is.True);

        var secondCompiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        secondCompiler.info.compCompHnd = &jitInfo;
        Assert.That(secondCompiler.eeIsIntrinsicType(intrinsicClass), Is.True);

        Assert.That(s_intrinsicTypeCalls, Is.EqualTo(4));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte IsIntrinsicType(ICorJitInfo* _, CORINFO_CLASS_STRUCT_* classHandle)
    {
        s_intrinsicTypeCalls++;
        return ((nint)classHandle == 0x101) ? (byte)1 : (byte)0;
    }
}
