// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.CorInfoGCType;
using static RyuJitSharp.CorInfoHFAElemType;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm64ReturnTypeTests
{
    [TestCase(9, TYPE_GC_NONE, TYPE_GC_NONE, TYP_I_IMPL, TYP_I_IMPL)]
    [TestCase(9, TYPE_GC_REF, TYPE_GC_NONE, TYP_REF, TYP_I_IMPL)]
    [TestCase(9, TYPE_GC_BYREF, TYPE_GC_NONE, TYP_BYREF, TYP_I_IMPL)]
    [TestCase(16, TYPE_GC_NONE, TYPE_GC_REF, TYP_I_IMPL, TYP_REF)]
    [TestCase(16, TYPE_GC_BYREF, TYPE_GC_NONE, TYP_BYREF, TYP_I_IMPL)]
    [TestCase(16, TYPE_GC_NONE, TYPE_GC_BYREF, TYP_I_IMPL, TYP_BYREF)]
    [TestCase(16, TYPE_GC_REF, TYPE_GC_BYREF, TYP_REF, TYP_BYREF)]
    [TestCase(16, TYPE_GC_BYREF, TYPE_GC_REF, TYP_BYREF, TYP_REF)]
    [TestCase(16, TYPE_GC_REF, TYPE_GC_REF, TYP_REF, TYP_REF)]
    [TestCase(16, TYPE_GC_BYREF, TYPE_GC_BYREF, TYP_BYREF, TYP_BYREF)]
    public static void TwoRegisterReturnsMapBothTypedGcLayoutSlots(
        int size, CorInfoGCType first, CorInfoGCType second, var_types firstType, var_types secondType)
    {
        WithCompiler(size, first, second, (compiler, handle, metadata) => {
            var descriptor = new ReturnTypeDesc();
            descriptor.InitializeStructReturnType(compiler, handle, CorInfoCallConvExtension.Managed);

            Assert.Multiple(() => {
                Assert.That(descriptor.ReturnRegCount, Is.EqualTo(2));
                Assert.That(descriptor.IsMultiRegRetType, Is.True);
                Assert.That(descriptor.GetReturnRegType(0), Is.EqualTo(firstType));
                Assert.That(descriptor.GetReturnRegType(1), Is.EqualTo(secondType));
                Assert.That(descriptor.GetAbiReturnReg(0, CorInfoCallConvExtension.Managed), Is.EqualTo(REG_R0));
                Assert.That(descriptor.GetAbiReturnReg(1, CorInfoCallConvExtension.Managed), Is.EqualTo(REG_R1));
                Assert.That(metadata.GcLayoutCalls, Is.EqualTo(1));
                Assert.That(metadata.GcLayoutWasInitialized, Is.True);
                Assert.That(metadata.SizeCalls, Is.EqualTo(1));
            });
        });
    }

    [Test]
    public static void ResetClearsBothReturnSlotsBeforeReinitialization()
    {
        WithCompiler(16, TYPE_GC_REF, TYPE_GC_BYREF, (compiler, handle, metadata) => {
            var descriptor = new ReturnTypeDesc();
            descriptor.InitializeStructReturnType(compiler, handle, CorInfoCallConvExtension.Managed);
            Assert.That(descriptor.GetReturnRegType(0), Is.EqualTo(TYP_REF));
            Assert.That(descriptor.GetReturnRegType(1), Is.EqualTo(TYP_BYREF));

            descriptor.Reset();
            metadata.First = TYPE_GC_NONE;
            metadata.Second = TYPE_GC_NONE;
            descriptor.InitializeStructReturnType(compiler, handle, CorInfoCallConvExtension.Managed);

            Assert.That(descriptor.ReturnRegCount, Is.EqualTo(2));
            Assert.That(descriptor.GetReturnRegType(0), Is.EqualTo(TYP_I_IMPL));
            Assert.That(descriptor.GetReturnRegType(1), Is.EqualTo(TYP_I_IMPL));
            Assert.That(metadata.GcLayoutCalls, Is.EqualTo(2));
        });
    }

    private sealed class Metadata
    {
        public int Size;
        public CorInfoGCType First;
        public CorInfoGCType Second;
        public int SizeCalls;
        public int GcLayoutCalls;
        public bool GcLayoutWasInitialized = true;
    }

    private delegate void ReturnAction(Compiler compiler, CORINFO_CLASS_STRUCT_* handle, Metadata metadata);

    private static void WithCompiler(int size, CorInfoGCType first, CorInfoGCType second, ReturnAction action)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getClassSize = &GetClassSize;
        vtable.Base.Base.getClassGClayout = &GetClassGcLayout;
        vtable.Base.Base.getClassAttribs = &GetClassAttribs;
        vtable.Base.Base.getHFAType = &GetHfaType;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
        JitFlags flags = default;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info.compCompHnd = &jitInfo;
        compiler.opts.jitFlags = &flags;
        var metadata = new Metadata {
            Size = size,
            First = first,
            Second = second,
        };
        var metadataHandle = GCHandle.Alloc(metadata);

#if DEBUG
        using var tls = new JitTls(&jitInfo);
#endif
        var previous = JitTls.Compiler;
        JitTls.Compiler = compiler;

        try
        {
            action(compiler, (CORINFO_CLASS_STRUCT_*)GCHandle.ToIntPtr(metadataHandle), metadata);
        }
        finally
        {
            metadataHandle.Free();
            JitTls.Compiler = previous;
        }
    }

    private static Metadata GetMetadata(CORINFO_CLASS_STRUCT_* handle)
    {
        return (Metadata)(GCHandle.FromIntPtr((nint)handle).Target
            ?? throw new InvalidOperationException("Missing struct return metadata"));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetClassSize(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* handle)
    {
        var metadata = GetMetadata(handle);
        metadata.SizeCalls++;
        return metadata.Size;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetClassGcLayout(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* handle, CorInfoGCType* layout)
    {
        var metadata = GetMetadata(handle);
        metadata.GcLayoutCalls++;
        metadata.GcLayoutWasInitialized &= (layout[0] == TYPE_GC_NONE) && (layout[1] == TYPE_GC_NONE);
        layout[0] = metadata.First;
        layout[1] = metadata.Second;

        return (metadata.First is TYPE_GC_NONE ? 0 : 1) + (metadata.Second is TYPE_GC_NONE ? 0 : 1);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoFlag GetClassAttribs(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* handle) => 0;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoHFAElemType GetHfaType(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* handle)
        => CORINFO_HFA_ELEM_NONE;
}
#endif
