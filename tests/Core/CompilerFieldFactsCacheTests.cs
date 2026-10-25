// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.CorInfoType;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CompilerFieldFactsCacheTests
{
    private static int s_classFieldCountCalls;
    private static int s_fieldInClassCalls;
    private static int s_fieldOffsetCalls;
    private static int s_fieldTypeCalls;

    [Test]
    public static void FieldFactsCacheExactInputsAndPreserveOptionalClassOutput()
    {
        s_classFieldCountCalls = 0;
        s_fieldInClassCalls = 0;
        s_fieldOffsetCalls = 0;
        s_fieldTypeCalls = 0;

        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getClassNumInstanceFields = &GetClassNumInstanceFields;
        vtable.Base.Base.getFieldInClass = &GetFieldInClass;
        vtable.Base.Base.getFieldOffset = &GetFieldOffset;
        vtable.Base.Base.getFieldType = &GetFieldType;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };

        var firstCompiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        firstCompiler.info.compCompHnd = &jitInfo;

        var firstClass = (CORINFO_CLASS_STRUCT_*)0x101;
        var secondClass = (CORINFO_CLASS_STRUCT_*)0x102;
        Assert.That(firstCompiler.eeGetClassNumInstanceFields(firstClass), Is.EqualTo(2));
        Assert.That(firstCompiler.eeGetClassNumInstanceFields(firstClass), Is.EqualTo(2));
        Assert.That(firstCompiler.eeGetClassNumInstanceFields(secondClass), Is.EqualTo(1));

        var firstField = firstCompiler.eeGetFieldInClass(firstClass, 1);
        Assert.That((nint)firstField, Is.EqualTo((nint)0x301));
        Assert.That((nint)firstCompiler.eeGetFieldInClass(firstClass, 1), Is.EqualTo((nint)0x301));

        var secondField = firstCompiler.eeGetFieldInClass(firstClass, 0);
        Assert.That((nint)secondField, Is.EqualTo((nint)0x201));
        Assert.That((nint)firstCompiler.eeGetFieldInClass(secondClass, 0), Is.EqualTo((nint)0x202));

        Assert.That(firstCompiler.eeGetFieldOffset(firstField), Is.EqualTo(0x30));
        Assert.That(firstCompiler.eeGetFieldOffset(firstField), Is.EqualTo(0x30));
        Assert.That(firstCompiler.eeGetFieldOffset(secondField), Is.EqualTo(0x20));

        var memberParent = (CORINFO_CLASS_STRUCT_*)0x201;
        Assert.That(firstCompiler.eeGetFieldCorInfoType(firstField, memberParent: memberParent),
            Is.EqualTo(CORINFO_TYPE_VALUECLASS));
        Assert.That(firstCompiler.eeGetFieldCorInfoType(firstField, memberParent: memberParent),
            Is.EqualTo(CORINFO_TYPE_VALUECLASS));

        var structType = (CORINFO_CLASS_STRUCT_*)0xDEAD;
        Assert.That(firstCompiler.eeGetFieldCorInfoType(firstField, &structType, memberParent),
            Is.EqualTo(CORINFO_TYPE_VALUECLASS));
        Assert.That((nint)structType, Is.EqualTo((nint)0x101));
        structType = (CORINFO_CLASS_STRUCT_*)0xBEEF;
        Assert.That(firstCompiler.eeGetFieldCorInfoType(firstField, &structType, memberParent),
            Is.EqualTo(CORINFO_TYPE_VALUECLASS));
        Assert.That((nint)structType, Is.EqualTo((nint)0x101));

        var otherMemberParent = (CORINFO_CLASS_STRUCT_*)0x202;
        structType = (CORINFO_CLASS_STRUCT_*)0xDEAD;
        Assert.That(firstCompiler.eeGetFieldCorInfoType(firstField, &structType, otherMemberParent),
            Is.EqualTo(CORINFO_TYPE_VALUECLASS));
        Assert.That((nint)structType, Is.EqualTo((nint)0x102));

        structType = (CORINFO_CLASS_STRUCT_*)0xDEAD;
        Assert.That(firstCompiler.eeGetFieldCorInfoType(secondField, &structType), Is.EqualTo(CORINFO_TYPE_INT));
        Assert.That((nint)structType, Is.EqualTo((nint)0xDEAD));
        structType = (CORINFO_CLASS_STRUCT_*)0xBEEF;
        Assert.That(firstCompiler.eeGetFieldCorInfoType(secondField, &structType), Is.EqualTo(CORINFO_TYPE_INT));
        Assert.That((nint)structType, Is.EqualTo((nint)0xBEEF));

        var secondCompiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        secondCompiler.info.compCompHnd = &jitInfo;
        Assert.That(secondCompiler.eeGetClassNumInstanceFields(firstClass), Is.EqualTo(2));
        var secondCompilerField = secondCompiler.eeGetFieldInClass(firstClass, 1);
        Assert.That((nint)secondCompilerField, Is.EqualTo((nint)0x301));
        Assert.That(secondCompiler.eeGetFieldOffset(secondCompilerField), Is.EqualTo(0x30));

        Assert.Multiple(() => {
            Assert.That(s_classFieldCountCalls, Is.EqualTo(3));
            Assert.That(s_fieldInClassCalls, Is.EqualTo(4));
            Assert.That(s_fieldOffsetCalls, Is.EqualTo(3));
            Assert.That(s_fieldTypeCalls, Is.EqualTo(4));
        });
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetClassNumInstanceFields(ICorJitInfo* _, CORINFO_CLASS_STRUCT_* classHandle)
    {
        s_classFieldCountCalls++;
        return (nint)classHandle == 0x101 ? 2 : 1;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_FIELD_STRUCT_* GetFieldInClass(
        ICorJitInfo* _, CORINFO_CLASS_STRUCT_* classHandle, int fieldIndex)
    {
        s_fieldInClassCalls++;
        var fieldOffset = ((nint)fieldIndex + 1) * 0x100;
        return (CORINFO_FIELD_STRUCT_*)((nint)classHandle + fieldOffset);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetFieldOffset(ICorJitInfo* _, CORINFO_FIELD_STRUCT_* fieldHandle)
    {
        s_fieldOffsetCalls++;
        return (int)((nint)fieldHandle >> 4);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoType GetFieldType(
        ICorJitInfo* _, CORINFO_FIELD_STRUCT_* fieldHandle, CORINFO_CLASS_STRUCT_** structType,
        CORINFO_CLASS_STRUCT_* memberParent)
    {
        s_fieldTypeCalls++;
        if ((nint)fieldHandle != 0x301)
        {
            return CORINFO_TYPE_INT;
        }

        if (structType is not null)
        {
            *structType = (nint)memberParent == 0x202
                        ? (CORINFO_CLASS_STRUCT_*)0x102
                        : (CORINFO_CLASS_STRUCT_*)0x101;
        }

        return CORINFO_TYPE_VALUECLASS;
    }
}
