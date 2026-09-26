// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class OptimizedStoreLoweringTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void ScalarizedStructFieldStoreRetainsLirAndTraversalCursor(bool withSuccessor)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        CORINFO_METHOD_INFO methodInfo = default;
        compiler.info.compMethodInfo = &methodInfo;
        compiler.info.compRetBuffArg = Globals.BAD_VAR_NUM;
        compiler.info.compTypeCtxtArg = Globals.BAD_VAR_NUM;
        compiler.lvaAsyncContinuationArg = Globals.BAD_VAR_NUM;
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.lvaTable = new LclVarDsc[2];
        compiler.lvaCount = 2;
        var layout = new ClassLayout(8);
        compiler.lvaTable[0].Type = TYP_STRUCT;
        compiler.lvaTable[0].Layout = layout;
        compiler.lvaTable[0].lvDoNotEnregister = true;
        compiler.lvaTable[1].Type = TYP_STRUCT;
        compiler.lvaTable[1].Layout = layout;
        JitTls.Compiler = compiler;
        compiler.codeGen = new CodeGen(compiler);

        try
        {
            var source = new GenTreeLclVar(TYP_STRUCT, 1);
            var original = new GenTreeLclFld(TYP_STRUCT, 0, 0, source, layout);
            var block = new BasicBlock(null, null);
            block.MakeLir(null, null);
            block.InsertAtEnd(source);
            block.InsertAtEnd(original);
            var successor = withSuccessor ? new GenTree(GT_NOP, TYP_VOID) : null;
            if (successor is not null)
            {
                block.InsertAtEnd(successor);
            }

            var lowering = new Lowering(compiler, new LinearScan(compiler));
            LoweringBlock(lowering) = block;
#if DEBUG
            compiler.verbose = true;
            _ = CodeGenLifeTransitionTests.Capture(() =>
                Assert.That(LowerNode(lowering, original), Is.SameAs(successor)));
#else
            Assert.That(LowerNode(lowering, original), Is.SameAs(successor));
#endif
            var lowered = successor?.Prev ?? block.LastNode;
            Assert.That(lowered, Is.TypeOf<GenTreeStoreInd>());
            Assert.That(lowered!.Oper, Is.EqualTo(GT_STOREIND));
            Assert.That(lowered.AsStoreInd().Data, Is.SameAs(source));
            Assert.That(lowered.AsStoreInd().Addr.Oper, Is.EqualTo(GT_LCL_ADDR));
#if DEBUG
            Assert.That(lowered.TreeId, Is.EqualTo(original.TreeId));
#endif
            Assert.That(original.Prev, Is.Null);
            Assert.That(original.Next, Is.Null);
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? LoweringBlock(Lowering lowering);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerNode")]
    private static extern GenTree? LowerNode(Lowering lowering, GenTree node);
}
