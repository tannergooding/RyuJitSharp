// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class BlockStoreLoweringTargetTests
{
    [Test]
    public static void UnaddressableLocalFieldBlockSourceUsesNonFaultingAddressLoads()
    {
        WithCompiler(compiler => {
            const int sourceOffset = 65_528;
            const int blockSize = 16;
            var blockLayoutBuilder = new ClassLayoutBuilder(compiler, blockSize);
            blockLayoutBuilder.SetGCPtrType(0, TYP_REF);
            var blockLayout = ClassLayout.Create(compiler, blockLayoutBuilder);
            var localLayoutBuilder = new ClassLayoutBuilder(compiler, sourceOffset + blockSize);
            localLayoutBuilder.SetGCPtrType(sourceOffset / TARGET_POINTER_SIZE, TYP_REF);
            var localLayout = ClassLayout.Create(compiler, localLayoutBuilder);
            compiler.lvaCount = 3;
            compiler.lvaSetStruct(2, localLayout, unsafeValueClsCheck: false);

            var lastByte = checked(sourceOffset + (int)blockLayout.Size - 1);
            Assert.That(compiler.IsValidLclAddr(2, lastByte), Is.False);

            var destination = compiler.gtNewLclvNode(TYP_BYREF, 0);
            var source = compiler.gtNewLclFldNode(TYP_STRUCT, 2, checked((ushort)sourceOffset), blockLayout);
            source.IsContained = true;
            var store = new GenTreeBlk(TYP_STRUCT, destination, source, blockLayout) {
                Flags = GTF_ASG,
            };
            var block = NewBlock(destination, source, store);
            compiler.compCurBB = block;
            compiler.fgNodeThreading = NodeThreading.LIR;
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            LoweringBlock(lowering) = block;

            _ = LowerNode(lowering, store);

            var addressStoreFound = false;
            var nonFaultingReferenceLoadFound = false;
            var referenceLoadUsesAddressTemp = false;
            for (var node = block.FirstNode; node is not null; node = node.Next)
            {
                if ((node.Oper is GT_STORE_LCL_VAR) &&
                    (node.AsLclVarCommon().Data.Oper is GT_LCL_ADDR))
                {
                    addressStoreFound = true;
                }

                if ((node.Oper is GT_IND) && (node.Type is TYP_REF))
                {
                    nonFaultingReferenceLoadFound |= (node.Flags & GTF_IND_NONFAULTING) is not 0;
                    referenceLoadUsesAddressTemp |= node.AsIndir().Addr.Oper is GT_LCL_VAR;
                }
            }

            Assert.That(addressStoreFound, Is.True);
            Assert.That(nonFaultingReferenceLoadFound, Is.True);
            Assert.That(referenceLoadUsesAddressTemp, Is.True);
            if (TestConfiguration.IsDebug)
            {
                Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
            }
        });
    }

    private static BasicBlock NewBlock(params ReadOnlySpan<GenTree> nodes)
    {
        var block = new BasicBlock(null, null);
        block.MakeLir(null, null);
        foreach (var node in nodes)
        {
            block.InsertAtEnd(node);
        }

        return block;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? LoweringBlock(Lowering lowering);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerNode")]
    private static extern GenTree? LowerNode(Lowering lowering, GenTree node);

    private static void WithCompiler(Action<Compiler> action)
    {
        using var tls = TestConfiguration.CreateJitTls();
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info.compRetBuffArg = BAD_VAR_NUM;
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.lvaTable = new LclVarDsc[4];
        compiler.lvaCount = 2;
        compiler.lvaTable[0].Type = TYP_BYREF;
        compiler.lvaTable[1].Type = TYP_BYREF;
        JitTls.Compiler = compiler;
        compiler.codeGen = new CodeGen(compiler);
        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
