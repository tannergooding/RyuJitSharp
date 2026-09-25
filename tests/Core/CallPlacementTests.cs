// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CallPlacementTests
{
    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public static void PlacementMovesOnlyInvariantValuesAndPreservesFieldOrder(bool computedValue, bool fieldList)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        JitTls.Compiler = compiler;
        compiler.codeGen = new CodeGen(compiler);
        try
        {
            var block = new BasicBlock(null, null);
            block.MakeLir(null, null);
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            LoweringBlock(lowering) = block;
            var call = new GenTreeCall(var_types.TYP_VOID);
            var constant = compiler.gtNewIconNode(var_types.TYP_INT, 1);
            GenTree value = computedValue
                ? new GenTreeUnOp(genTreeOps.GT_NEG, var_types.TYP_INT, constant)
                : constant;
            var firstArg = new GenTreeUnOp(genTreeOps.GT_PUTARG_REG, var_types.TYP_INT, value);
            var secondValue = compiler.gtNewIconNode(var_types.TYP_INT, 2);
            var secondArg = new GenTreeUnOp(genTreeOps.GT_PUTARG_REG, var_types.TYP_INT, secondValue);
            var marker = compiler.gtNewNothingNode();
            var fields = new GenTreeFieldList();
            if (fieldList)
            {
                fields.AddFieldLIR(compiler, firstArg, 0, var_types.TYP_INT);
                fields.AddFieldLIR(compiler, secondArg, 4, var_types.TYP_INT);
                call.Args.PushBack(NewCallArg.CreateForPrimitive(value)).EarlyNode = fields;
            }
            else
            {
                call.Args.PushBack(NewCallArg.CreateForPrimitive(value)).EarlyNode = firstArg;
                var late = call.Args.PushBack(NewCallArg.CreateForPrimitive(secondValue));
                late.EarlyNode = null;
                late.LateNode = secondArg;
                call.Args.PushLateBack(late);
            }

            block.InsertAtEnd(constant);
            if (computedValue)
            {
                block.InsertAtEnd(value);
            }
            block.InsertAtEnd(firstArg);
            block.InsertAtEnd(secondValue);
            block.InsertAtEnd(secondArg);
            if (fieldList)
            {
                block.InsertAtEnd(fields);
            }
            block.InsertAtEnd(marker);
            block.InsertAtEnd(call);

            MovePutArgNodesUpToCall(lowering, call);

            Assert.That(firstArg.Prev, computedValue ? Is.SameAs(marker) : Is.SameAs(value));
            Assert.That(firstArg.Next, Is.SameAs(secondValue));
            Assert.That(secondValue.Next, Is.SameAs(secondArg));
            Assert.That(secondArg.Next, fieldList ? Is.SameAs(fields) : Is.SameAs(call));
            Assert.That(call.Prev, fieldList ? Is.SameAs(fields) : Is.SameAs(secondArg));
            if (computedValue)
            {
                Assert.That(value.Next, Is.SameAs(marker));
            }
            else
            {
                Assert.That(block.FirstNode, Is.SameAs(marker));
            }
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

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "MovePutArgNodesUpToCall")]
    private static extern void MovePutArgNodesUpToCall(Lowering lowering, GenTreeCall call);
}
