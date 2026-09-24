// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class ArrayLengthLoweringTests
{
    [Test]
    public static void MetadataBecomesIndirectionWithNativeOffsetsAndOwnership(
        [Values(GT_ARR_LENGTH, GT_MDARR_LENGTH, GT_MDARR_LOWER_BOUND)] genTreeOps oper,
        [Values(false, true)] bool nullArray,
        [Values(false, true)] bool unused)
    {
        WithCompiler(compiler => {
            GenTree array = nullArray
                ? compiler.gtNewIconNode(TYP_REF, 0)
                : compiler.gtNewLclvNode(TYP_REF, 0);
            GenTreeArrCommon metadata = oper is GT_ARR_LENGTH
                ? new GenTreeArrLen(TYP_INT, array, OFFSETOF__CORINFO_Array__length)
                : new GenTreeMDArr(oper, array, 2, 3);
            metadata.Flags |= GTF_EXCEPT | GTF_GLOB_REF | GTF_DONT_CSE;
            if (!nullArray)
            {
                metadata.Flags |= GTF_IND_NONFAULTING;
            }
            metadata.IsUnusedValue = unused;
            metadata._vnPair.SetBoth(123);
            var flags = metadata.Flags;
#if DEBUG
            var treeId = metadata.TreeId;
#endif
            var owner = unused ? null : compiler.gtNewStoreLclVarNode(1, metadata);
            var block = new BasicBlock(null, null);
            block.MakeLir(null, null);
            block.InsertAtEnd(array);
            block.InsertAtEnd(metadata);
            if (owner is not null)
            {
                block.InsertAtEnd(owner);
            }
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            LoweringBlock(lowering) = block;

            var next = LowerArrLength(lowering, metadata);

            var indirection = (owner is null ? block.LastNode : owner.Data) as GenTreeIndir
                ?? throw new AssertionException("Array metadata was not replaced with an indirection.");
            Assert.That(indirection.Oper, Is.EqualTo(GT_IND));
            Assert.That(indirection.Type, Is.EqualTo(TYP_INT));
            Assert.That(indirection.Flags, Is.EqualTo(flags & (GTF_COMMON_MASK | GTF_IND_NONFAULTING)));
            Assert.That(indirection._vnPair.Liberal, Is.EqualTo(ValueNumStore.NoVN));
            Assert.That(indirection._vnPair.Conservative, Is.EqualTo(ValueNumStore.NoVN));
#if DEBUG
            Assert.That(indirection.TreeId, Is.EqualTo(treeId));
#endif
            Assert.That(metadata.Prev is null && metadata.Next is null, Is.True);
            Assert.That(indirection.Next, Is.SameAs(owner));
            Assert.That(next, Is.SameAs(array.Next));
            if (nullArray)
            {
                Assert.That(next, Is.SameAs(indirection));
                Assert.That(indirection.Addr, Is.SameAs(array));
            }
            else
            {
                var offset = oper switch {
                    GT_ARR_LENGTH => OFFSETOF__CORINFO_Array__length,
                    GT_MDARR_LENGTH => OFFSETOF__CORINFO_Array__data + (2 * sizeof(int)),
                    _ => OFFSETOF__CORINFO_Array__data + (5 * sizeof(int)),
                };
                var constant = next ?? throw new AssertionException("The inserted offset was not returned.");
                Assert.That(constant.AsIntCon().IconValue, Is.EqualTo((nint)offset));
                Assert.That(constant.Type, Is.EqualTo(TYP_I_IMPL));
                var address = indirection.Addr.AsOp();
                Assert.That(address.Oper, Is.EqualTo(GT_ADD));
                Assert.That(address.Type, Is.EqualTo(TYP_BYREF));
                Assert.That(address.Op1, Is.SameAs(array));
                Assert.That(address.Op2, Is.SameAs(constant));
                Assert.That(constant.Next, Is.SameAs(address));
                Assert.That(address.Next, Is.SameAs(indirection));
            }
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? LoweringBlock(Lowering lowering);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerArrLength")]
    private static extern GenTree? LowerArrLength(Lowering lowering, GenTreeArrCommon node);

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.fgNodeThreading = NodeThreading.LIR;
        compiler.lvaTable = [new LclVarDsc(), new LclVarDsc()];
        compiler.lvaCount = 2;
        compiler.lvaTable[0].Type = TYP_REF;
        compiler.lvaTable[1].Type = TYP_INT;
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
