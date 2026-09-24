// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.CorInfoGCType;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class StructReturnClassificationTests
{
    [TestCase(1, TYPE_GC_NONE, TYP_UBYTE)]
    [TestCase(2, TYPE_GC_NONE, TYP_USHORT)]
    [TestCase(3, TYPE_GC_NONE, TYP_UNKNOWN)]
    [TestCase(4, TYPE_GC_NONE, TYP_INT)]
    [TestCase(5, TYPE_GC_NONE, TYP_UNKNOWN)]
    [TestCase(6, TYPE_GC_NONE, TYP_UNKNOWN)]
    [TestCase(7, TYPE_GC_NONE, TYP_UNKNOWN)]
    [TestCase(8, TYPE_GC_NONE, TYP_I_IMPL)]
    [TestCase(8, TYPE_GC_REF, TYP_REF)]
    [TestCase(8, TYPE_GC_BYREF, TYP_BYREF)]
    [TestCase(9, TYPE_GC_NONE, TYP_UNKNOWN)]
    [TestCase(16, TYPE_GC_NONE, TYP_UNKNOWN)]
    public static void ManagedReturnsUseExactSizeAndPointerGcClassification(
        int size, CorInfoGCType gcType, var_types expectedType)
    {
        WithCompiler(size, gcType, false, "Example", "Value", (compiler, handle, metadata) => {
            var type = compiler.GetReturnTypeForStruct(handle, CorInfoCallConvExtension.Managed, out var kind);

            Assert.That(type, Is.EqualTo(expectedType));
            Assert.That(kind, Is.EqualTo(expectedType is TYP_UNKNOWN ? Compiler.SPK_ByReference : Compiler.SPK_PrimitiveType));
            Query[] expectedQueries = size == 8 ? [Query.ClassSize, Query.GcLayout] : [Query.ClassSize];
            Assert.That(metadata.Queries, Is.EqualTo(expectedQueries));
            Assert.That(compiler.compFloatingPointUsed, Is.False);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void SuppliedSizeAvoidsTheSizeQueryWithoutChangingTheClassification(bool suppliedSize)
    {
        WithCompiler(8, TYPE_GC_BYREF, false, "Example", "Value", (compiler, handle, metadata) => {
            var type = compiler.GetReturnTypeForStruct(handle, CorInfoCallConvExtension.C,
                out var kind, suppliedSize ? 8 : 0);

            Assert.That(type, Is.EqualTo(TYP_BYREF));
            Assert.That(kind, Is.EqualTo(Compiler.SPK_PrimitiveType));
            Query[] expectedQueries = suppliedSize ? [Query.GcLayout] : [Query.ClassSize, Query.GcLayout];
            Assert.That(metadata.Queries, Is.EqualTo(expectedQueries));

            metadata.Queries.Clear();
            Assert.That(compiler.GetReturnTypeForStruct(handle, CorInfoCallConvExtension.C, 8), Is.EqualTo(type));
            Assert.That(metadata.Queries, Is.EqualTo((Query[])[Query.GcLayout]));
        });
    }

    [TestCase(CorInfoCallConvExtension.Thiscall)]
    [TestCase(CorInfoCallConvExtension.CMemberFunction)]
    [TestCase(CorInfoCallConvExtension.StdcallMemberFunction)]
    [TestCase(CorInfoCallConvExtension.FastcallMemberFunction)]
    public static void OrdinaryNativeMemberReturnsUseARetBufferBeforeQueryingGcLayout(CorInfoCallConvExtension callConv)
    {
        WithCompiler(8, TYPE_GC_REF, false, "Example", "Value", (compiler, handle, metadata) => {
            var type = compiler.GetReturnTypeForStruct(handle, callConv, out var kind);

            Assert.That(type, Is.EqualTo(TYP_UNKNOWN));
            Assert.That(kind, Is.EqualTo(Compiler.SPK_ByReference));
            Assert.That(metadata.Queries, Is.EqualTo((Query[])[Query.ClassSize, Query.Intrinsic]));
        });
    }

    [TestCase("System.Runtime.InteropServices", "CLong", true, true)]
    [TestCase("System.Runtime.InteropServices", "CULong", true, true)]
    [TestCase("System.Runtime.InteropServices", "NFloat", true, true)]
    [TestCase("System.Runtime.InteropServices", "CLong", false, false)]
    [TestCase("System.Runtime.InteropServices", "Other", true, false)]
    [TestCase("Other.Namespace", "CLong", true, false)]
    [TestCase("System.Runtime.InteropServices.Extra", "NFloat", true, false)]
    public static void NativePrimitiveMemberReturnsRequireIntrinsicStatusAndExactMetadataIdentity(
        string namespaceName, string name, bool intrinsic, bool primitive)
    {
        WithCompiler(8, TYPE_GC_NONE, intrinsic, namespaceName, name, (compiler, handle, metadata) => {
            var type = compiler.GetReturnTypeForStruct(handle, CorInfoCallConvExtension.CMemberFunction, out var kind);

            Assert.That(type, Is.EqualTo(primitive ? TYP_I_IMPL : TYP_UNKNOWN));
            Assert.That(kind, Is.EqualTo(primitive ? Compiler.SPK_PrimitiveType : Compiler.SPK_ByReference));
            Query[] expectedQueries = !intrinsic
                ? [Query.ClassSize, Query.Intrinsic]
                : primitive
                    ? [Query.ClassSize, Query.Intrinsic, Query.MetadataName, Query.GcLayout]
                    : [Query.ClassSize, Query.Intrinsic, Query.MetadataName];
            Assert.That(metadata.Queries, Is.EqualTo(expectedQueries));
            Assert.That(compiler.compFloatingPointUsed, Is.False);
        });
    }

    [TestCase(8, TYPE_GC_REF, TYP_REF, 1)]
    [TestCase(8, TYPE_GC_BYREF, TYP_BYREF, 1)]
    [TestCase(3, TYPE_GC_NONE, TYP_UNKNOWN, 0)]
    [TestCase(16, TYPE_GC_NONE, TYP_UNKNOWN, 0)]
    public static void ReturnDescriptorsReuseClassificationAndDoNotQueryTheSizeTwice(
        int size, CorInfoGCType gcType, var_types expectedType, int registerCount)
    {
        WithCompiler(size, gcType, false, "Example", "Value", (compiler, handle, metadata) => {
            var descriptor = new ReturnTypeDesc();
            descriptor.InitializeStructReturnType(compiler, handle, CorInfoCallConvExtension.Managed);

            Assert.That(descriptor.ReturnRegCount, Is.EqualTo(registerCount));
            if (registerCount != 0)
            {
                Assert.That(descriptor.GetReturnRegType(0), Is.EqualTo(expectedType));
                Assert.That(descriptor.GetAbiReturnReg(0, CorInfoCallConvExtension.Managed), Is.EqualTo(regNumber.REG_RAX));
            }
            Query[] expectedQueries = size == 8 ? [Query.ClassSize, Query.GcLayout] : [Query.ClassSize];
            Assert.That(metadata.Queries, Is.EqualTo(expectedQueries));
        });
    }

    private enum Query
    {
        ClassSize,
        Intrinsic,
        MetadataName,
        GcLayout,
    }

    private sealed class Metadata
    {
        public int Size;
        public CorInfoGCType GcType;
        public bool Intrinsic;
        public byte* NamespaceName;
        public byte* Name;
        public readonly List<Query> Queries = [];
    }

    private delegate void ClassificationAction(Compiler compiler, CORINFO_CLASS_STRUCT_* handle, Metadata metadata);

    private static void WithCompiler(int size, CorInfoGCType gcType, bool intrinsic,
        string namespaceName, string name, ClassificationAction action)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getClassSize = &GetClassSize;
        vtable.Base.Base.getClassGClayout = &GetClassGcLayout;
        vtable.Base.Base.isIntrinsicType = &IsIntrinsicType;
        vtable.Base.Base.getClassNameFromMetadata = &GetClassNameFromMetadata;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
        JitFlags flags = default;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info.compCompHnd = &jitInfo;
        compiler.opts.jitFlags = &flags;

#if DEBUG
        using var tls = new JitTls(&jitInfo);
#endif
        var previous = JitTls.Compiler;
        JitTls.Compiler = compiler;
        try
        {
            fixed (byte* nameBytes = Encoding.UTF8.GetBytes(name + '\0'))
            fixed (byte* namespaceBytes = Encoding.UTF8.GetBytes(namespaceName + '\0'))
            {
                var metadata = new Metadata {
                    Size = size,
                    GcType = gcType,
                    Intrinsic = intrinsic,
                    Name = nameBytes,
                    NamespaceName = namespaceBytes,
                };
                var metadataHandle = GCHandle.Alloc(metadata);
                try
                {
                    action(compiler, (CORINFO_CLASS_STRUCT_*)GCHandle.ToIntPtr(metadataHandle), metadata);
                }
                finally
                {
                    metadataHandle.Free();
                }
            }
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    private static Metadata GetMetadata(CORINFO_CLASS_STRUCT_* handle)
    {
        var metadata = GCHandle.FromIntPtr((nint)handle).Target as Metadata;
        assert(metadata is not null);

        return metadata;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetClassSize(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* handle)
    {
        var metadata = GetMetadata(handle);
        metadata.Queries.Add(Query.ClassSize);

        return metadata.Size;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetClassGcLayout(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* handle, CorInfoGCType* layout)
    {
        var metadata = GetMetadata(handle);
        metadata.Queries.Add(Query.GcLayout);
        *layout = metadata.GcType;

        return metadata.GcType is TYPE_GC_NONE ? 0 : 1;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte IsIntrinsicType(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* handle)
    {
        var metadata = GetMetadata(handle);
        metadata.Queries.Add(Query.Intrinsic);

        return metadata.Intrinsic ? (byte)1 : (byte)0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte* GetClassNameFromMetadata(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* handle, byte** namespaceName)
    {
        var metadata = GetMetadata(handle);
        metadata.Queries.Add(Query.MetadataName);
        *namespaceName = metadata.NamespaceName;

        return metadata.Name;
    }
}
