// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.CorInfoType;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class EETypePrintingTests
{
    private static int s_rank;
    private static CorInfoType s_childType;
    private static bool s_jagged;
    private static int s_arrayNameCalls;
    private static int s_arrayInstantiationCalls;
    private static byte[]? s_classNameUtf8;

    [TestCase(0, CORINFO_TYPE_CLASS, false, false, "Example.Box`1")]
    [TestCase(0, CORINFO_TYPE_CLASS, true, false, "Example.Box`1[int]")]
    [TestCase(1, CORINFO_TYPE_INT, true, false, "int[]")]
    [TestCase(2, CORINFO_TYPE_INT, true, false, "int[,]")]
    [TestCase(3, CORINFO_TYPE_INT, true, false, "int[,,]")]
    [TestCase(2, CORINFO_TYPE_INT, false, false, "int[,]")]
    [TestCase(1, CORINFO_TYPE_CLASS, false, false, "Example.Box`1[]")]
    [TestCase(1, CORINFO_TYPE_CLASS, true, false, "Example.Box`1[int][]")]
    [TestCase(3, CORINFO_TYPE_VALUECLASS, false, false, "Example.Box`1[,,]")]
    [TestCase(3, CORINFO_TYPE_VALUECLASS, true, false, "Example.Box`1[int][,,]")]
    [TestCase(2, CORINFO_TYPE_CLASS, false, true, "int[][,]")]
    [TestCase(2, CORINFO_TYPE_CLASS, true, true, "int[][,]")]
    public static void ArrayNamesStopAfterTheRankSuffix(
        int rank, CorInfoType childType, bool includeInstantiation, bool jagged, string expected)
    {
        s_rank = rank;
        s_childType = childType;
        s_jagged = jagged;
        s_arrayNameCalls = 0;
        s_arrayInstantiationCalls = 0;
        s_classNameUtf8 = null;

        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getArrayRank = &GetArrayRank;
        vtable.Base.Base.getChildType = &GetChildType;
        vtable.Base.Base.printClassName = &PrintClassName;
        vtable.Base.Base.getTypeInstantiationArgument = &GetTypeArgument;
        vtable.Base.Base.asCorInfoType = &AsCorInfoType;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info.compCompHnd = &jitInfo;
        var builder = new StringBuilder("prefix:");

        var result = compiler.eePrintType(builder, (CORINFO_CLASS_STRUCT_*)1, includeInstantiation);

        Assert.Multiple(() => {
            Assert.That(result, Is.SameAs(builder));
            Assert.That(result.ToString(), Is.EqualTo($"prefix:{expected}"));
            Assert.That(s_arrayNameCalls, Is.Zero);
            Assert.That(s_arrayInstantiationCalls, Is.Zero);
        });
    }

    [Test]
    public static void LongTypeNamesGrowTheMappedBuilder()
    {
        s_rank = 0;
        s_childType = CORINFO_TYPE_INT;
        s_jagged = false;
        var className = new string('\u00e9', 150);
        s_classNameUtf8 = Encoding.UTF8.GetBytes(className);

        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getArrayRank = &GetArrayRank;
        vtable.Base.Base.getChildType = &GetChildType;
        vtable.Base.Base.printClassName = &PrintClassName;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info.compCompHnd = &jitInfo;
        var builder = new StringBuilder(128).Append("prefix:");

        var result = compiler.eePrintType(builder, (CORINFO_CLASS_STRUCT_*)1, includeInstantiation: false);

        Assert.Multiple(() => {
            Assert.That(result, Is.SameAs(builder));
            Assert.That(result.ToString(), Is.EqualTo($"prefix:{className}"));
        });

        s_classNameUtf8 = null;
    }

    [Test]
    public static void LargeArrayNamesGrowTheMappedBuilder()
    {
        const int rank = 32;
        s_rank = rank;
        s_childType = CORINFO_TYPE_INT;
        s_jagged = false;
        s_classNameUtf8 = null;

        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getArrayRank = &GetArrayRank;
        vtable.Base.Base.getChildType = &GetChildType;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info.compCompHnd = &jitInfo;
        var builder = new StringBuilder(8).Append("prefix:");

        var result = compiler.eePrintType(builder, (CORINFO_CLASS_STRUCT_*)1, includeInstantiation: false);

        Assert.Multiple(() => {
            Assert.That(result, Is.SameAs(builder));
            Assert.That(result.ToString(), Is.EqualTo($"prefix:int[{new string(',', rank - 1)}]"));
        });
    }

    private static int Rank(CORINFO_CLASS_STRUCT_* type) => (nint)type switch
    {
        1 => s_rank,
        2 when s_jagged => 1,
        _ => 0,
    };

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetArrayRank(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type) => Rank(type);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoType GetChildType(
        ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type, CORINFO_CLASS_STRUCT_** child)
    {
        *child = (nint)type == 1 ? (CORINFO_CLASS_STRUCT_*)2 : null;
        return (nint)type == 1 ? s_childType : CORINFO_TYPE_INT;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoType AsCorInfoType(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type)
        => (nint)type == 3 ? CORINFO_TYPE_INT : CORINFO_TYPE_CLASS;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* GetTypeArgument(
        ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type, int index)
    {
        if (Rank(type) > 0)
        {
            s_arrayInstantiationCalls++;
            return null;
        }

        return index == 0 ? (CORINFO_CLASS_STRUCT_*)3 : null;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static nint PrintClassName(
        ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type, byte* buffer, nint length, nint* required)
    {
        var isArray = Rank(type) > 0;

        if (isArray)
        {
            s_arrayNameCalls++;
        }

        ReadOnlySpan<byte> name;

        if (isArray)
        {
            name = "(dynamicClass)"u8;
        }
        else if (s_classNameUtf8 is not null)
        {
            name = s_classNameUtf8;
        }
        else
        {
            name = "Example.Box`1"u8;
        }

        *required = name.Length + 1;
        var written = (int)Math.Min(Math.Max(length - 1, 0), name.Length);
        name[..written].CopyTo(new Span<byte>(buffer, written));

        if (length > 0)
        {
            buffer[written] = 0;
        }

        return written;
    }
}
