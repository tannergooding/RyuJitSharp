// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;
using BitVecOps = RyuJitSharp.BitSetOps<RyuJitSharp.BitVecTraits, RyuJitSharp.BitVecTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ObjectAllocatorUseRewritingTests
{
    [Test]
    public static void MappedObjectUseBecomesStackAddressAndBoxWrapperIsRemoved()
    {
        WithAllocator([TYP_REF, TYP_STRUCT], (compiler, allocator, block) =>
        {
            var layout = compiler.typGetBlkLayout(16);
            compiler.lvaSetStruct(1, layout, unsafeValueClsCheck: false);
            Track(compiler, allocator, 0, definitely: true);
            StackObjectMap(allocator)[0] = 1;
            var original = compiler.gtNewLclvNode(TYP_REF, 0);
            var box = new GenTreeBox(TYP_REF, original,
                compiler.gtNewStmt(compiler.gtNewIconNode(TYP_INT, 0)),
                compiler.gtNewStmt(compiler.gtNewIconNode(TYP_INT, 0)));
            var statement = compiler.gtNewStmt(box);
            compiler.fgInsertStmtAtEnd(block, statement);

            allocator.RewriteUses();

            Assert.That(compiler.lvaGetDesc(0).Type, Is.EqualTo(TYP_I_IMPL));
            Assert.That(statement.RootNode.Oper, Is.EqualTo(GT_LCL_ADDR));
            Assert.That(statement.RootNode.Type, Is.EqualTo(TYP_I_IMPL));
            Assert.That(statement.RootNode.AsLclVarCommon().LclNum, Is.EqualTo(1));
            Assert.That(statement.RootNode, Is.Not.SameAs(original));
            Assert.That(box.BoxOp, Is.SameAs(statement.RootNode));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void PossiblyStackPointingComparisonRetypesLocalAndNull(bool definitely)
    {
        WithAllocator([TYP_REF], (compiler, allocator, block) =>
        {
            Track(compiler, allocator, 0, definitely);
            var reference = compiler.gtNewLclvNode(TYP_REF, 0);
            var nullValue = compiler.gtNewIconNode(TYP_REF, 0);
            var comparison = compiler.gtNewBinaryNode(GT_EQ, TYP_INT, reference, nullValue);
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(comparison));

            allocator.RewriteUses();

            var expectedType = definitely ? TYP_I_IMPL : TYP_BYREF;
            Assert.That(compiler.lvaGetDesc(0).Type, Is.EqualTo(expectedType));
            Assert.That(reference.Type, Is.EqualTo(expectedType));
            Assert.That(nullValue.Type, Is.EqualTo(expectedType));
            Assert.That(comparison.Type, Is.EqualTo(TYP_INT));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void StructFieldLayoutAndLoadFollowStackPointerCertainty(bool definitely)
    {
        WithAllocator([TYP_STRUCT], (compiler, allocator, block) =>
        {
            var builder = new ClassLayoutBuilder(compiler, 16);
            builder.SetGCPtrType(1, TYP_REF);
            var layout = compiler.typGetCustomLayout(builder);
            compiler.lvaSetStruct(0, layout, unsafeValueClsCheck: false);
            SetFieldTracking(allocator);
            Track(compiler, allocator, 0, definitely);
            var address = compiler.gtNewLclAddrNode(TYP_BYREF, 0, 0);
            var load = new GenTreeBlk(TYP_STRUCT, address, layout);
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(load));

            allocator.RewriteUses();

            var rewrittenLayout = compiler.lvaGetDesc(0).Layout!;
            Assert.That(rewrittenLayout.Size, Is.EqualTo(layout.Size));
            Assert.That(rewrittenLayout.GCPtrCount, Is.EqualTo(definitely ? 0 : 1));
            Assert.That(rewrittenLayout.GetGCPtrType(1),
                Is.EqualTo(definitely ? TYP_LONG : TYP_BYREF));
            Assert.That(load.Layout, Is.SameAs(rewrittenLayout));
            Assert.That(address.Type, Is.EqualTo(TYP_BYREF));
        });
    }

    [Test]
    public static void DefiniteStackAddressDisablesHeapTargetWriteBarrier()
    {
        WithAllocator([TYP_REF, TYP_STRUCT], (compiler, allocator, block) =>
        {
            compiler.lvaSetStruct(1, compiler.typGetBlkLayout(16), unsafeValueClsCheck: false);
            Track(compiler, allocator, 0, definitely: true);
            StackObjectMap(allocator)[0] = 1;
            var address = compiler.gtNewLclvNode(TYP_REF, 0);
            var store = compiler.gtNewStoreIndNode(TYP_REF, address, compiler.gtNewIconNode(TYP_REF, 0));
            store.Flags |= GTF_IND_TGT_HEAP;
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(store));

            allocator.RewriteUses();

            Assert.That(store.Addr.Oper, Is.EqualTo(GT_LCL_ADDR));
            Assert.That(store.Flags & GTF_IND_TGT_HEAP, Is.EqualTo(GTF_EMPTY));
            Assert.That(store.Flags & GTF_IND_TGT_NOT_HEAP, Is.EqualTo(GTF_IND_TGT_NOT_HEAP));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void StoreValueFollowsStackPointerCertainty(bool definitely)
    {
        WithAllocator([TYP_REF], (compiler, allocator, block) =>
        {
            Track(compiler, allocator, 0, definitely);
            var value = compiler.gtNewLclvNode(TYP_REF, 0);
            var address = compiler.gtNewIconNode(TYP_I_IMPL, 0);
            var store = compiler.gtNewStoreIndNode(TYP_REF, address, value);
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(store));

            allocator.RewriteUses();

            Assert.That(store.Data, Is.SameAs(value));
            Assert.That(store.Type, Is.EqualTo(definitely ? TYP_I_IMPL : TYP_BYREF));
            Assert.That(address.Type, Is.EqualTo(TYP_I_IMPL));
        });
    }

    private static Dictionary<int, int> StackObjectMap(ObjectAllocator allocator)
    {
        return (Dictionary<int, int>)(GetField(allocator, "_heapLocalToStackObjLocalMap")
            ?? throw new InvalidOperationException("Missing stack-object map."));
    }

    private static void SetFieldTracking(ObjectAllocator allocator)
        => SetField(allocator, "_trackFields", true);

    private static void Track(Compiler compiler, ObjectAllocator allocator, int local, bool definitely)
    {
        ref var descriptor = ref compiler.lvaGetDesc(local);
        descriptor.lvTracked = true;
        descriptor._varIndex = checked((ushort)local);
        var traits = new BitVecTraits(compiler, compiler.lvaCount);
        var possible = GetField(allocator, "_possiblyStackPointingPointers") is nint[] existingPossible &&
            !BitVecOps.MaybeUninit(existingPossible) ? existingPossible : BitVecOps.MakeEmpty(traits);
        var definite = GetField(allocator, "_definitelyStackPointingPointers") is nint[] existingDefinite &&
            !BitVecOps.MaybeUninit(existingDefinite) ? existingDefinite : BitVecOps.MakeEmpty(traits);
        BitVecOps.AddElemD(traits, possible, local);
        if (definitely)
        {
            BitVecOps.AddElemD(traits, definite, local);
        }
        SetField(allocator, "_possiblyStackPointingPointers", possible);
        SetField(allocator, "_definitelyStackPointingPointers", definite);
        SetField(allocator, "_bvCount", compiler.lvaCount);
        SetField(allocator, "_bitVecTraits", traits);
        SetField(allocator, "_analysisDone", true);
    }

    private static object? GetField(ObjectAllocator allocator, string name)
        => typeof(ObjectAllocator).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)?
            .GetValue(allocator);

    private static void SetField(ObjectAllocator allocator, string name, object value)
    {
        var field = typeof(ObjectAllocator).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Missing {name}.");
        field.SetValue(allocator, value);
    }

    private static void WithAllocator(var_types[] types, Action<Compiler, ObjectAllocator, BasicBlock> action)
    {
#if DEBUG
        ICorJitInfo jitInfo = default;
        using var tls = new JitTls(&jitInfo);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.info.compFullName = nameof(ObjectAllocatorUseRewritingTests);
#endif
        compiler.lvaTable = new LclVarDsc[types.Length];
        compiler.lvaCount = types.Length;
        for (var index = 0; index < types.Length; index++)
        {
            compiler.lvaTable[index].Type = types[index];
        }
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        JitTls.Compiler = compiler;
        var block = BasicBlock.New(compiler, BBJ_RETURN);
        block.SetFlags(BBF_HAS_NEWOBJ);
        compiler.fgFirstBB = block;
        compiler.fgLastBB = block;

        try
        {
            action(compiler, new ObjectAllocator(compiler), block);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
