// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class SimdTypeTests
{
    [TestCase("Plane", 16)]
    [TestCase("Quaternion", 16)]
    [TestCase("Vector", 0)]
    [TestCase("Vector2", 8)]
    [TestCase("Vector3", 12)]
    [TestCase("Vector4", 16)]
    [TestCase("Vector`1", 16)]
    [TestCase("Vector2Extra", 0)]
    [TestCase("Vector`", 0)]
    [TestCase("Vector`2", 0)]
    [TestCase("Vector5", 0)]
    [TestCase("", 0)]
    public static void NumericsNamesUseManagedStringBoundaries(string name, int expectedSize)
    {
        var (size, usesSimd) = Classify(name, expectedSize, matchedVM: true);
        Assert.Multiple(() => {
            Assert.That(size, Is.EqualTo(expectedSize));
            Assert.That(usesSimd, Is.EqualTo(expectedSize != 0));
        });
    }

    [TestCase(16, 16)]
    [TestCase(32, 0)]
    [TestCase(64, 0)]
    public static void CrossTargetVectorSizeMustMatchVM(int vmSize, int expectedSize)
    {
        var (size, usesSimd) = Classify("Vector`1", vmSize, matchedVM: false);
        Assert.Multiple(() => {
            Assert.That(size, Is.EqualTo(expectedSize));
            Assert.That(usesSimd, Is.EqualTo(expectedSize != 0));
        });
    }

    private static (int Size, bool UsesSimd) Classify(string name, int vmSize, bool matchedVM)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        // Native bool is one byte; reverse P/Invoke signatures require blittable types.
        vtable.Base.Base.isIntrinsicType = (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_STRUCT_*, bool>)
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_STRUCT_*, byte>)&IsIntrinsicType;
        vtable.Base.Base.getClassNameFromMetadata = &GetClassName;
        vtable.Base.Base.getClassSize = &GetClassSize;
        vtable.Base.Base.getTypeInstantiationArgument = &GetTypeArgument;
        vtable.Base.Base.getTypeForPrimitiveNumericClass = &GetNumericType;
        vtable.Base.notifyInstructionSetUsage = (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_InstructionSet, bool, bool>)
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_InstructionSet, byte, byte>)&NotifyInstructionSetUsage;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
        JitFlags flags = default;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info = new Compiler.Info { compCompHnd = &jitInfo, compMatchedVM = matchedVM };
        compiler.opts.jitFlags = &flags;

#if DEBUG
        using var jitTls = new JitTls(&jitInfo);
#endif
        var previous = JitTls.Compiler;
        JitTls.Compiler = compiler;

        try
        {
            var utf8Name = Encoding.UTF8.GetBytes(name + '\0');
            fixed (byte* className = utf8Name)
            fixed (byte* namespaceName = "System.Numerics\0"u8)
            {
                var info = new ClassInfo { Name = className, Namespace = namespaceName, Size = vmSize };
                var size = compiler.GetSimdTypeSizeInBytes((CORINFO_CLASS_STRUCT_*)&info);
                return (size, compiler._usesSimdTypes);
            }
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    private struct ClassInfo
    {
        public byte* Name;
        public byte* Namespace;
        public int Size;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte IsIntrinsicType(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type) => 1;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte* GetClassName(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type, byte** namespaceName)
    {
        var info = (ClassInfo*)type;
        *namespaceName = info->Namespace;
        return info->Name;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetClassSize(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type) => ((ClassInfo*)type)->Size;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* GetTypeArgument(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type, int index) => type;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoType GetNumericType(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type) => CorInfoType.CORINFO_TYPE_FLOAT;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte NotifyInstructionSetUsage(ICorJitInfo* self, CORINFO_InstructionSet isa, byte supported) => supported;
}
