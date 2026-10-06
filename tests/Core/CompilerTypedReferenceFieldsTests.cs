// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static unsafe class CompilerTypedReferenceFieldsTests
{
    [Test]
    public static void TypedReferenceFieldLookupsCacheTheBuiltinClass()
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getBuiltinClass = &GetBuiltinClass;
        vtable.Base.Base.getFieldInClass = &GetFieldInClass;

        var state = new TypedReferenceState
        {
            Runtime = new ICorJitInfo { lpVtbl = &vtable },
        };

        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info.compCompHnd = &state.Runtime;

        var dataField = compiler.GetRefanyDataField();
        var typeField = compiler.GetRefanyTypeField();

        Assert.That((nuint)dataField, Is.EqualTo((nuint)1));
        Assert.That((nuint)typeField, Is.EqualTo((nuint)2));
        Assert.That(state.BuiltinClassRequests, Is.EqualTo(1));
        Assert.That(state.RequestedClass, Is.EqualTo(CorInfoClassId.CLASSID_TYPED_BYREF));
        Assert.That(state.FieldRequests, Is.EqualTo(2));
        Assert.That((nuint)state.FirstFieldClass, Is.EqualTo((nuint)state.SecondFieldClass));
        Assert.That(state.FirstFieldIndex, Is.EqualTo(0));
        Assert.That(state.SecondFieldIndex, Is.EqualTo(1));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* GetBuiltinClass(ICorJitInfo* runtime, CorInfoClassId classId)
    {
        var state = (TypedReferenceState*)runtime;
        state->BuiltinClassRequests++;
        state->RequestedClass = classId;
        return (CORINFO_CLASS_STRUCT_*)0x1234;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_FIELD_STRUCT_* GetFieldInClass(
        ICorJitInfo* runtime, CORINFO_CLASS_STRUCT_* classHandle, int index)
    {
        var state = (TypedReferenceState*)runtime;
        state->FieldRequests++;
        if (index == 0)
        {
            state->FirstFieldClass = classHandle;
            state->FirstFieldIndex = index;
        }
        else
        {
            state->SecondFieldClass = classHandle;
            state->SecondFieldIndex = index;
        }

        return (CORINFO_FIELD_STRUCT_*)(nuint)(index + 1);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TypedReferenceState
    {
        public ICorJitInfo Runtime;
        public int BuiltinClassRequests;
        public CorInfoClassId RequestedClass;
        public int FieldRequests;
        public CORINFO_CLASS_STRUCT_* FirstFieldClass;
        public int FirstFieldIndex;
        public CORINFO_CLASS_STRUCT_* SecondFieldClass;
        public int SecondFieldIndex;
    }
}
