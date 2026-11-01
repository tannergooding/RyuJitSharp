// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CompilerClassMetadataNameCacheTests
{
    private static int s_getClassNameFromMetadataCalls;

    [Test]
    public static void ClassMetadataNameCachePreservesNamespaceOutputShape()
    {
        s_getClassNameFromMetadataCalls = 0;

        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getClassNameFromMetadata = &GetClassNameFromMetadata;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };

        var firstCompiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        firstCompiler.info.compCompHnd = &jitInfo;

        var firstClass = (CORINFO_CLASS_STRUCT_*)0x101;
        var secondClass = (CORINFO_CLASS_STRUCT_*)0x102;
        byte* namespaceName = null;

        var firstName = Compiler.eeGetClassNameFromMetadataFromJitInfo(ref jitInfo, firstCompiler, firstClass, &namespaceName);
        Assert.That((nint)firstName, Is.EqualTo((nint)0x121));
        Assert.That((nint)namespaceName, Is.EqualTo((nint)0x111));

        namespaceName = null;
        var repeatedName = Compiler.eeGetClassNameFromMetadataFromJitInfo(ref jitInfo, firstCompiler, firstClass, &namespaceName);
        Assert.That((nint)repeatedName, Is.EqualTo((nint)0x121));
        Assert.That((nint)namespaceName, Is.EqualTo((nint)0x111));

        var nameWithoutNamespace = Compiler.eeGetClassNameFromMetadataFromJitInfo(ref jitInfo, firstCompiler, firstClass, null);
        Assert.That((nint)nameWithoutNamespace, Is.EqualTo((nint)0x121));

        _ = Compiler.eeGetClassNameFromMetadataFromJitInfo(ref jitInfo, firstCompiler, firstClass, null);
        _ = Compiler.eeGetClassNameFromMetadataFromJitInfo(ref jitInfo, firstCompiler, secondClass, &namespaceName);

        var secondCompiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        secondCompiler.info.compCompHnd = &jitInfo;
        _ = Compiler.eeGetClassNameFromMetadataFromJitInfo(ref jitInfo, secondCompiler, firstClass, &namespaceName);

        Assert.That(s_getClassNameFromMetadataCalls, Is.EqualTo(4));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte* GetClassNameFromMetadata(
        ICorJitInfo* _,
        CORINFO_CLASS_STRUCT_* classHandle,
        byte** namespaceName)
    {
        s_getClassNameFromMetadataCalls++;
        if (namespaceName != null)
        {
            *namespaceName = (byte*)((nint)classHandle + 0x10);
        }

        return (byte*)((nint)classHandle + 0x20);
    }
}
