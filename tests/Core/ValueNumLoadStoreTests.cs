// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.MemoryKind;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ValueNumLoadStoreTests
{
    [Test]
    public static void PhysicalStoreAndLoadRecoverSubrange()
    {
        ValueNumMemoryAccessTests.WithStore((_, store) =>
        {
            var original = store.VNForExpr(null, TYP_STRUCT);
            var value = store.VNForIntCon(314);
            var size = new ValueSize(8);
            var partial = store.VNForStore(original, size, 4, new ValueSize(4), value);
            Assert.That(partial, Is.Not.EqualTo(ValueNumStore.NoVN));
            Assert.That(store.VNForLoad(ValueNumKind.VNK_Liberal, partial, size, TYP_INT, 4, new ValueSize(4)),
                Is.EqualTo(value));
            Assert.That(store.VNForLoad(ValueNumKind.VNK_Liberal, partial, size, TYP_STRUCT, 0, size),
                Is.EqualTo(partial));
            Assert.That(ValueNumStore.LoadStoreIsEntire(size, 0, size), Is.True);
            Assert.That(ValueNumStore.LoadStoreIsEntire(size, 1, size), Is.False);
            Assert.That(ValueNumStore.LoadStoreIsEntire(size, 0, ValueSize.Unknown), Is.False);
        });
    }

    [TestCase(4294967296L)]
    [TestCase(-4294967296L)]
    [TestCase(long.MinValue)]
    public static void WholeLoadUsesTruncatedUnsignedOffset(long offset)
    {
        ValueNumMemoryAccessTests.WithStore((_, store) =>
        {
            var value = store.VNForIntCon(41);
            var size = new ValueSize(4);

            Assert.That(store.VNForLoad(ValueNumKind.VNK_Liberal, value, size, TYP_INT,
                unchecked((nint)offset), size), Is.EqualTo(value));
        });
    }

    [TestCase(-1)]
    [TestCase(6)]
    public static void OutOfBoundsAccessesDoNotInventPhysicalStores(int offset)
    {
        ValueNumMemoryAccessTests.WithStore((_, store) =>
        {
            var original = store.VNForExpr(null, TYP_STRUCT);
            var size = new ValueSize(8);
            var data = store.VNForIntCon(5);
            Assert.That(store.VNForStore(original, size, offset, new ValueSize(4), data),
                Is.EqualTo(ValueNumStore.NoVN));
            var first = store.VNForLoad(ValueNumKind.VNK_Liberal, original, size, TYP_INT,
                offset, new ValueSize(4));
            var second = store.VNForLoad(ValueNumKind.VNK_Liberal, original, size, TYP_INT,
                offset, new ValueSize(4));
            Assert.That(first, Is.Not.EqualTo(second));
        });
    }

    [Test]
    public static void UnknownSizeRejectsPartialStoreAndMakesLoadUnique()
    {
        ValueNumMemoryAccessTests.WithStore((_, store) =>
        {
            var original = store.VNForExpr(null, TYP_STRUCT);
            var value = store.VNForIntCon(7);
            Assert.That(store.VNForStore(original, ValueSize.Unknown, 1, new ValueSize(4), value),
                Is.EqualTo(ValueNumStore.NoVN));
            var first = store.VNForLoad(ValueNumKind.VNK_Liberal, original, ValueSize.Unknown,
                TYP_INT, 1, new ValueSize(4));
            Assert.That(first, Is.Not.EqualTo(store.VNForLoad(ValueNumKind.VNK_Liberal, original,
                ValueSize.Unknown, TYP_INT, 1, new ValueSize(4))));
        });
    }

    [Test]
    public static void WholeLoadBitcastsAndPairsRetainConservativeInputs()
    {
        ValueNumMemoryAccessTests.WithStore((_, store) =>
        {
            var size = new ValueSize(8);
            var liberal = store.VNForLongCon(42);
            var conservative = store.VNForLongCon(43);
            var pair = store.VNPairForLoad(new(liberal, conservative), size, TYP_DOUBLE, 0, size);
            Assert.That(pair.Liberal, Is.EqualTo(store.VNForBitCast(liberal, TYP_DOUBLE, size)));
            Assert.That(pair.Conservative, Is.EqualTo(store.VNForBitCast(conservative, TYP_DOUBLE, size)));
            Assert.That(pair.Liberal, Is.Not.EqualTo(pair.Conservative));

            var original = store.VNForExpr(null, TYP_STRUCT);
            var equalValue = new ValueNumPair(liberal, liberal);
            var newPair = store.VNPairForStore(new(original, original), size, 4,
                new ValueSize(4), equalValue);
            Assert.That(newPair.BothEqual(), Is.True);
        });
    }

    [Test]
    public static void FieldSelectorUsesFieldTypeAndHandle()
    {
        ValueNumMemoryAccessTests.WithStore((compiler, store) =>
        {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.Base.getFieldType = &GetFieldType;
            ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
            compiler.info.compCompHnd = &jitInfo;
            var handle = (CORINFO_FIELD_STRUCT_*)0x1000;
            var selector = store.VNForFieldSelector(handle, out var fieldType, out var size);
            Assert.That(selector, Is.EqualTo(store.VNForHandle((nint)handle, GTF_ICON_FIELD_HDL)));
            Assert.That(fieldType, Is.EqualTo(TYP_INT));
            Assert.That(size, Is.EqualTo(new ValueSize(4)));
        });
    }

    [TestCase(0, true)]
    [TestCase(2, false)]
    public static void SimpleStaticStoreAndLoadRespectFieldBounds(int offset, bool inBounds)
    {
        ValueNumMemoryAccessTests.WithStore((compiler, store) =>
        {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.Base.getFieldType = &GetFieldType;
            vtable.Base.Base.isFieldStatic = &IsFieldStatic;
            ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
            compiler.info.compCompHnd = &jitInfo;
            var field = new FieldSeq((CORINFO_FIELD_STRUCT_*)0x1000, 0, FieldSeq.FieldKind.SimpleStatic);
            ArgumentNullException.ThrowIfNull(compiler.compCurBB);
            compiler.compCurBB.bbMemoryDef = (1 << (int)GcHeap) | (1 << (int)ByrefExposed);
            var original = store.VNForExpr(null, TYP_HEAP);
            compiler.fgCurMemoryVN[(int)GcHeap] = original;
            var value = store.VNForIntCon(25);
            var tree = compiler.gtNewIconNode(TYP_INT, 0);

            compiler.fgValueNumberFieldStore(tree, null, field, offset, new ValueSize(4), value);

            var newHeap = compiler.fgCurMemoryVN[(int)GcHeap];
            Assert.That(newHeap, Is.Not.EqualTo(original));
            if (inBounds)
            {
                var load = new GenTreeIndir(genTreeOps.GT_IND, TYP_INT,
                    compiler.gtNewIconNode(TYP_BYREF, 0));
                compiler.fgValueNumberFieldLoad(load, null, field, 0);
                Assert.That(load._vnPair.Liberal, Is.EqualTo(value));
                Assert.That(load._vnPair.Conservative, Is.Not.EqualTo(value));
            }
            else
            {
                Assert.That(store.TypeOfVN(newHeap), Is.EqualTo(TYP_HEAP));
            }
        });
    }

    [Test]
    public static void FieldAddressDistinguishesSharedKnownAndByrefBases()
    {
        ValueNumMemoryAccessTests.WithStore((compiler, store) =>
        {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.Base.isFieldStatic = &IsFieldStatic;
            vtable.Base.Base.getFieldClass = &GetFieldClass;
            vtable.Base.Base.isValueClass = &IsValueClass;
            ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
            compiler.info.compCompHnd = &jitInfo;
            var handle = (CORINFO_FIELD_STRUCT_*)0x1000;
            var shared = new FieldSeq(handle, 16, FieldSeq.FieldKind.SharedStatic);
            var simple = new FieldSeq(handle, 8, FieldSeq.FieldKind.SimpleStatic);
            var known = new FieldSeq(handle, 0x1000, FieldSeq.FieldKind.SimpleStaticKnownAddress);
            var instance = new FieldSeq((CORINFO_FIELD_STRUCT_*)0x2000, 12, FieldSeq.FieldKind.Instance);
            var baseAddress = compiler.gtNewIconNode(TYP_I_IMPL, 0x2000);

            var sharedAddress = compiler.gtNewBinaryNode(genTreeOps.GT_ADD, TYP_I_IMPL, baseAddress,
                compiler.gtNewIconNode(20, shared));
            Assert.That(sharedAddress.IsFieldAddr(compiler, out var baseNode, out var sequence, out var offset),
                Is.True);
            Assert.That(baseNode, Is.SameAs(baseAddress));
            Assert.That(sequence, Is.SameAs(shared));
            Assert.That(offset, Is.EqualTo((nint)4));

            var simpleAddress = compiler.gtNewBinaryNode(genTreeOps.GT_ADD, TYP_I_IMPL, baseAddress,
                compiler.gtNewIconNode(12, simple));
            Assert.That(simpleAddress.IsFieldAddr(compiler, out baseNode, out sequence, out offset), Is.True);
            Assert.That(baseNode, Is.Null);
            Assert.That(sequence, Is.SameAs(simple));
            Assert.That(offset, Is.EqualTo((nint)4));

            var knownAddress = compiler.gtNewIconNode(0x1010, known);
            knownAddress.Flags |= GTF_ICON_STATIC_HDL;
            Assert.That(knownAddress.IsFieldAddr(compiler, out baseNode, out sequence, out offset), Is.True);
            Assert.That(baseNode, Is.Null);
            Assert.That(sequence, Is.SameAs(known));
            Assert.That(offset, Is.EqualTo((nint)16));

            var invalid = compiler.gtNewBinaryNode(genTreeOps.GT_ADD, TYP_I_IMPL, baseAddress,
                compiler.gtNewIconNode(0x1010, known));
            Assert.That(invalid.IsFieldAddr(compiler, out _, out _, out _), Is.False);
            var byref = compiler.gtNewIconNode(TYP_BYREF, 0);
            var byrefAddress = compiler.gtNewBinaryNode(genTreeOps.GT_ADD, TYP_BYREF, byref,
                compiler.gtNewIconNode(12, instance));
            Assert.That(byrefAddress.IsFieldAddr(compiler, out _, out _, out _), Is.False);

            var reference = compiler.gtNewIconNode(TYP_REF, 0);
            var instanceAddress = compiler.gtNewBinaryNode(genTreeOps.GT_ADD, TYP_BYREF, reference,
                compiler.gtNewIconNode(16, instance));
            Assert.That(instanceAddress.IsFieldAddr(compiler, out baseNode, out sequence, out offset), Is.True);
            Assert.That(baseNode, Is.SameAs(reference));
            Assert.That(sequence, Is.SameAs(instance));
            Assert.That(offset, Is.EqualTo((nint)4));
        });
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoType GetFieldType(ICorJitInfo* info, CORINFO_FIELD_STRUCT_* field,
        CORINFO_CLASS_STRUCT_** structType, CORINFO_CLASS_STRUCT_* memberParent)
        => CorInfoType.CORINFO_TYPE_INT;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte IsFieldStatic(ICorJitInfo* info, CORINFO_FIELD_STRUCT_* field)
        => (byte)((nint)field == 0x1000 ? 1 : 0);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* GetFieldClass(ICorJitInfo* info, CORINFO_FIELD_STRUCT_* field)
        => (CORINFO_CLASS_STRUCT_*)0x3000;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte IsValueClass(ICorJitInfo* info, CORINFO_CLASS_STRUCT_* cls)
        => 0;
}
