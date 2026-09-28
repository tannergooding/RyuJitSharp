// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
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
        var (size, usesSimd, _, _) = Classify(name, expectedSize, matchedVM: true);
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
        var (size, usesSimd, _, _) = Classify("Vector`1", vmSize, matchedVM: false);
        Assert.Multiple(() => {
            Assert.That(size, Is.EqualTo(expectedSize));
            Assert.That(usesSimd, Is.EqualTo(expectedSize != 0));
        });
    }

    [TestCase("Vector2", 8, var_types.TYP_SIMD8, var_types.TYP_FLOAT)]
    [TestCase("Vector3", 12, var_types.TYP_SIMD12, var_types.TYP_FLOAT)]
    [TestCase("Vector4", 16, var_types.TYP_SIMD16, var_types.TYP_FLOAT)]
    [TestCase("Vector`1", 16, var_types.TYP_SIMD16, var_types.TYP_FLOAT)]
    [TestCase("Vector5", 16, var_types.TYP_STRUCT, var_types.TYP_UNDEF)]
    public static void NormalizationPreservesSimdBaseType(string name, int size, var_types expectedType, var_types expectedBaseType)
    {
        var (_, _, type, baseType) = Classify(name, size, matchedVM: true, normalize: true);
        Assert.Multiple(() => {
            Assert.That(type, Is.EqualTo(expectedType));
            Assert.That(baseType, Is.EqualTo(expectedBaseType));
        });
    }

    [TestCase(16U, var_types.TYP_BYTE, 16)]
    [TestCase(16U, var_types.TYP_SHORT, 8)]
    [TestCase(16U, var_types.TYP_INT, 4)]
    [TestCase(16U, var_types.TYP_LONG, 2)]
    [TestCase(12U, var_types.TYP_FLOAT, 3)]
    [TestCase(64U, var_types.TYP_DOUBLE, 8)]
    [TestCase(0U, var_types.TYP_FLOAT, 0)]
    [TestCase(17U, var_types.TYP_SHORT, 8)]
    [TestCase(0x80000000U, var_types.TYP_INT, 536870912)]
    [TestCase(uint.MaxValue, var_types.TYP_BYTE, -1)]
    [TestCase(uint.MaxValue, var_types.TYP_FLOAT, 1073741823)]
    public static void VectorLengthPreservesUnsignedDivision(uint size, var_types baseType, int expected)
    {
        Assert.That(Compiler.getSIMDVectorLength(size, baseType), Is.EqualTo(expected));
    }

    [TestCase("Plane", 16, CorInfoType.CORINFO_TYPE_FLOAT, 4)]
    [TestCase("Quaternion", 16, CorInfoType.CORINFO_TYPE_FLOAT, 4)]
    [TestCase("Vector2", 8, CorInfoType.CORINFO_TYPE_FLOAT, 2)]
    [TestCase("Vector3", 12, CorInfoType.CORINFO_TYPE_FLOAT, 3)]
    [TestCase("Vector4", 16, CorInfoType.CORINFO_TYPE_FLOAT, 4)]
    [TestCase("Vector`1", 16, CorInfoType.CORINFO_TYPE_BYTE, 16)]
    [TestCase("Vector`1", 16, CorInfoType.CORINFO_TYPE_UBYTE, 16)]
    [TestCase("Vector`1", 16, CorInfoType.CORINFO_TYPE_SHORT, 8)]
    [TestCase("Vector`1", 16, CorInfoType.CORINFO_TYPE_USHORT, 8)]
    [TestCase("Vector`1", 16, CorInfoType.CORINFO_TYPE_INT, 4)]
    [TestCase("Vector`1", 16, CorInfoType.CORINFO_TYPE_UINT, 4)]
    [TestCase("Vector`1", 16, CorInfoType.CORINFO_TYPE_LONG, 2)]
    [TestCase("Vector`1", 16, CorInfoType.CORINFO_TYPE_ULONG, 2)]
    [TestCase("Vector`1", 16, CorInfoType.CORINFO_TYPE_FLOAT, 4)]
    [TestCase("Vector`1", 16, CorInfoType.CORINFO_TYPE_DOUBLE, 2)]
    public static void TypeHandleLengthPreservesRecognitionAndSimdUse(
        string name, int size, CorInfoType elementType, int expected)
    {
        var (length, usesSimd, _, _) = Classify(name, size, matchedVM: true, getLength: true, elementType: elementType);
        Assert.That(length, Is.EqualTo(expected));
        Assert.That(usesSimd, Is.True);
    }

    [Test]
    public static void UnknownTypeHandleLengthDoesNotBecomeZero()
    {
        _ = Assert.Throws<DivideByZeroException>(() => Classify("Vector5", 16, matchedVM: true, getLength: true));
    }

    private static (int Value, bool UsesSimd, var_types Type, var_types BaseType) Classify(
        string name, int vmSize, bool matchedVM, bool normalize = false, bool getLength = false,
        CorInfoType elementType = CorInfoType.CORINFO_TYPE_FLOAT)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        // Native bool is one byte; reverse P/Invoke signatures require blittable types.
        vtable.Base.Base.isIntrinsicType = (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_STRUCT_*, byte>)&IsIntrinsicType;
        vtable.Base.Base.getClassNameFromMetadata = &GetClassName;
        vtable.Base.Base.getClassSize = &GetClassSize;
        vtable.Base.Base.getClassAttribs = &GetClassAttribs;
        vtable.Base.Base.getTypeInstantiationArgument = &GetTypeArgument;
        vtable.Base.Base.getTypeForPrimitiveNumericClass = &GetNumericType;
        vtable.Base.notifyInstructionSetUsage = (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_InstructionSet, bool, bool, byte>)
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_InstructionSet, byte, byte, byte>)&NotifyInstructionSetUsage;
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
                var info = new ClassInfo { Name = className, Namespace = namespaceName, Size = vmSize, ElementType = elementType };
                var value = getLength
                    ? GetVectorLength(compiler, (CORINFO_CLASS_STRUCT_*)&info)
                    : compiler.GetSimdTypeSizeInBytes((CORINFO_CLASS_STRUCT_*)&info);
                var baseType = var_types.TYP_UNDEF;
                var type = normalize ? compiler.impNormStructType((CORINFO_CLASS_STRUCT_*)&info, out baseType) : var_types.TYP_UNDEF;
                return (value, compiler._usesSimdTypes, type, baseType);
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
        public CorInfoType ElementType;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "getSIMDVectorLength")]
    private static extern int GetVectorLength(Compiler compiler, CORINFO_CLASS_STRUCT_* type);

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
    private static CorInfoFlag GetClassAttribs(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type)
        => CorInfoFlag.CORINFO_FLG_VALUECLASS | CorInfoFlag.CORINFO_FLG_INTRINSIC_TYPE;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* GetTypeArgument(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type, int index) => type;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoType GetNumericType(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type) => ((ClassInfo*)type)->ElementType;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte NotifyInstructionSetUsage(ICorJitInfo* self, CORINFO_InstructionSet isa, byte supported, byte preserveNegativeDependency) => supported;
}
