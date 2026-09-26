// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ObjectAllocatorStackAllocationTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void ObjectStackRewriteCreatesStructHomeAndInitializesMethodTable(bool hasGCPointer)
    {
        WithAllocator((compiler, allocator, block) =>
        {
            var builder = new ClassLayoutBuilder(compiler, 16);
            if (hasGCPointer)
            {
                builder.SetGCPtrType(1, TYP_REF);
            }

            var layout = compiler.typGetCustomLayout(builder);
            compiler.info.compInitMem = hasGCPointer;
            var handle = compiler.gtNewIconNode(TYP_I_IMPL, 123);
            var allocation = new GenTreeAllocObj(TYP_REF, handle, CORINFO_HELP_NEWSFAST, true,
                (CORINFO_CLASS_STRUCT_*)123);
            var store = compiler.gtNewStoreLclVarNode(0, allocation);
            var statement = compiler.gtNewStmt(store);
            compiler.fgInsertStmtAtEnd(block, statement);
            SetAnalysisDone(allocator);

            var local = allocator.MorphAllocObjNodeIntoStackAlloc(allocation, layout, block, statement);

            Assert.That(local, Is.EqualTo(1));
            Assert.That(compiler.lvaGetDesc(local).lvStackAllocatedObject, Is.True);
            Assert.That(compiler.lvaGetDesc(local).Layout, Is.SameAs(layout));
            Assert.That(block.LastStmt, Is.SameAs(statement));
            Assert.That(block.FirstStmt, Is.Not.SameAs(statement));
            Assert.That(block.FirstStmt!.RootNode.Oper, Is.EqualTo(hasGCPointer ? GT_STORE_LCL_FLD : GT_STORE_LCL_VAR));
            var methodTable = hasGCPointer ? block.FirstStmt : block.FirstStmt.NextStmt!;
            Assert.That(methodTable.RootNode.Oper, Is.EqualTo(GT_STORE_LCL_FLD));
            Assert.That(methodTable.RootNode.AsLclFld().LclNum, Is.EqualTo(local));
            Assert.That(methodTable.RootNode.AsLclFld().Data, Is.SameAs(handle));
            Assert.That(methodTable.NextStmt, Is.SameAs(statement));
            Assert.That(compiler.lvaGetDesc(local).lvSuppressedZeroInit, Is.EqualTo(hasGCPointer));
            Assert.That(compiler.compSuppressedZeroInit, Is.EqualTo(hasGCPointer));
        });
    }

    [Test]
    public static void CloneSiteExclusionDistinguishesNewBlocksFromOriginalAllocationBlock()
    {
        WithAllocator((compiler, allocator, original) =>
        {
            var cloned = BasicBlock.New(compiler, BBJ_RETURN);
            var method = typeof(ObjectAllocator).GetMethod("BlockIsCloneOrWasCloned",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("Missing clone-site exclusion.");

            Assert.That(method.Invoke(allocator, [original]), Is.False);
            Assert.That(method.Invoke(allocator, [cloned]), Is.True);
        });
    }

    private static void SetAnalysisDone(ObjectAllocator allocator)
    {
        var field = typeof(ObjectAllocator).GetField("_analysisDone",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Missing allocation analysis state.");
        field.SetValue(allocator, true);
    }

    private static void WithAllocator(Action<Compiler, ObjectAllocator, BasicBlock> action)
    {
#if DEBUG
        ICorJitInfo jitInfo = default;
        using var tls = new JitTls(&jitInfo);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.info.compFullName = nameof(ObjectAllocatorStackAllocationTests);
#endif
        compiler.lvaTable = new LclVarDsc[2];
        compiler.lvaCount = 1;
        compiler.lvaTable[0].Type = TYP_REF;
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
