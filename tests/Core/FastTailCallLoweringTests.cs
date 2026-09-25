// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if FEATURE_FASTTAILCALL && TARGET_AMD64
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.GenTreeCallFlags;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class FastTailCallLoweringTests
{
    [Test]
    public static void CallsWithoutStackArgumentsDoNotEnterNonGcRegion()
    {
        WithCompiler((compiler, block, lowering) => {
            var call = FastCall();
            block.InsertAtEnd(call);

            LowerFastTailCall(lowering, call);

            Assert.That(block.FirstNode, Is.SameAs(call));
            Assert.That(block.LastNode, Is.SameAs(call));
        });
    }

    [TestCase(false, true)]
    [TestCase(true, false)]
    public static void StackArgumentsBeginNonGcRegionAfterGcSafePoint(
        bool blockIsGcSafe, bool insertGcStarvationNoOp)
    {
        WithCompiler((compiler, block, lowering) => {
            if (blockIsGcSafe)
            {
                block.SetFlags(BBF_GC_SAFE_POINT);
            }

            var call = FastCall();
            var value = compiler.gtNewIconNode(TYP_INT, 7);
            var put = AddStackArgument(call, value, 32, 8);
            block.InsertAtEnd(value);
            block.InsertAtEnd(put);
            block.InsertAtEnd(call);

            LowerFastTailCall(lowering, call);

            var start = put.Prev;
            Assert.That(start?.Oper, Is.EqualTo(GT_START_NONGC));
            Assert.That(start?.Prev?.Oper is GT_NO_OP, Is.EqualTo(insertGcStarvationNoOp));
            Assert.That(start?.Prev, Is.SameAs(insertGcStarvationNoOp ? block.FirstNode?.Next : value));
            Assert.That(call.Prev, Is.SameAs(put));
        });
    }

    [Test]
    public static void NonGcRegionBeginsAtEarliestPutargInLirNotArgumentListOrder()
    {
        WithCompiler((compiler, block, lowering) => {
            block.SetFlags(BBF_GC_SAFE_POINT);
            var call = FastCall();
            var firstValue = compiler.gtNewIconNode(TYP_INT, 1);
            var firstPut = AddStackArgument(call, firstValue, 32, 8);
            var secondValue = compiler.gtNewIconNode(TYP_INT, 2);
            var secondPut = AddStackArgument(call, secondValue, 40, 8);
            block.InsertAtEnd(secondValue);
            block.InsertAtEnd(secondPut);
            block.InsertAtEnd(firstValue);
            block.InsertAtEnd(firstPut);
            block.InsertAtEnd(call);

            LowerFastTailCall(lowering, call);

            Assert.That(secondPut.Prev?.Oper, Is.EqualTo(GT_START_NONGC));
            Assert.That(firstPut.Prev, Is.SameAs(firstValue));
            Assert.That(block.FirstNode, Is.SameAs(secondValue));
        });
    }

    [Test]
    public static void OverwrittenStackParameterIsCopiedBeforeArgumentSetup()
    {
        WithCompiler((compiler, block, lowering) => {
            compiler.info.compArgsCount = 1;
            compiler.lvaCount = 1;
            compiler.lvaTable = [new LclVarDsc(), new LclVarDsc(), new LclVarDsc()];
            compiler.lvaTable[0].Type = TYP_INT;
            compiler.lvaTable[0].lvIsParam = true;
            compiler.lvaParameterPassingInfo = [
                AbiPassingInformation.FromSegment(compiler, false, AbiPassingSegment.OnStack(32, 0, 8)),
            ];

            var call = FastCall();
            var firstValue = compiler.gtNewIconNode(TYP_INT, 10);
            var firstPut = AddStackArgument(call, firstValue, 32, 8);
            var laterUse = compiler.gtNewLclvNode(TYP_INT, 0);
            var secondPut = AddStackArgument(call, laterUse, 40, 8);
            block.InsertAtEnd(firstValue);
            block.InsertAtEnd(firstPut);
            block.InsertAtEnd(laterUse);
            block.InsertAtEnd(secondPut);
            block.InsertAtEnd(call);

            LowerFastTailCall(lowering, call);

            Assert.That(compiler.lvaCount, Is.EqualTo(2));
            Assert.That(laterUse.LclNum, Is.EqualTo(1));
            var savedValue = block.FirstNode!.AsLclVar();
            var store = savedValue.Next!.AsLclVarCommon();
            Assert.That(savedValue.LclNum, Is.Zero);
            Assert.That(store.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
            Assert.That(store.LclNum, Is.EqualTo(1));
            Assert.That(store.Next, Is.SameAs(firstValue));
            Assert.That(firstPut.Prev!.Oper, Is.EqualTo(GT_START_NONGC));
            Assert.That(secondPut.Op1, Is.SameAs(laterUse));
        });
    }

    [Test]
    public static void ScalarInPlaceStackCopyNeedsNoDefensiveTemp()
    {
        WithCompiler((compiler, block, lowering) => {
            compiler.info.compArgsCount = 1;
            compiler.lvaCount = 1;
            compiler.lvaTable[0].Type = TYP_INT;
            compiler.lvaTable[0].lvIsParam = true;
            compiler.lvaParameterPassingInfo = [
                AbiPassingInformation.FromSegment(compiler, false, AbiPassingSegment.OnStack(32, 0, 8)),
            ];

            var call = FastCall();
            var value = compiler.gtNewLclvNode(TYP_INT, 0);
            var put = AddStackArgument(call, value, 32, 8);
            block.InsertAtEnd(value);
            block.InsertAtEnd(put);
            block.InsertAtEnd(call);

            LowerFastTailCall(lowering, call);

            Assert.That(compiler.lvaCount, Is.EqualTo(1));
            Assert.That(value.LclNum, Is.Zero);
            Assert.That(block.FirstNode, Is.SameAs(value));
            Assert.That(put.Prev?.Oper, Is.EqualTo(GT_START_NONGC));
        });
    }

    [Test]
    public static void FirstOperandDescendsIntoContainedFieldList()
    {
        WithCompiler((compiler, block, lowering) => {
            var first = compiler.gtNewIconNode(TYP_INT, 1);
            var second = compiler.gtNewIconNode(TYP_INT, 2);
            var fields = new GenTreeFieldList();
            fields.AddFieldLIR(compiler, first, 0, TYP_INT);
            fields.AddFieldLIR(compiler, second, 4, TYP_INT);
            fields.IsContained = true;
            var put = new GenTreePutArgStk(TYP_VOID, fields, null, 32, 8, true);
            block.InsertAtEnd(first);
            block.InsertAtEnd(second);
            block.InsertAtEnd(fields);
            block.InsertAtEnd(put);

            Assert.That(FirstOperand(lowering, put), Is.SameAs(first));
        });
    }

    [Test]
    public static void OverwrittenStructParameterRetainsItsLayoutInTemporary()
    {
        WithCompiler((compiler, block, lowering) => {
            var layout = new ClassLayout(16);
            compiler.info.compArgsCount = 1;
            compiler.lvaCount = 1;
            compiler.lvaTable = [new LclVarDsc(), new LclVarDsc(), new LclVarDsc()];
            compiler.lvaTable[0].Type = TYP_STRUCT;
            compiler.lvaTable[0].Layout = layout;
            compiler.lvaTable[0].lvIsParam = true;
            compiler.lvaParameterPassingInfo = [
                AbiPassingInformation.FromSegment(compiler, false, AbiPassingSegment.OnStack(32, 0, 16)),
            ];

            var call = FastCall();
            var first = compiler.gtNewLclvNode(TYP_STRUCT, 0);
            var firstPut = new GenTreePutArgStk(TYP_VOID, first, call, 32, 16, true);
            call.Args.PushBack(NewCallArg.CreateForStruct(first, TYP_STRUCT, layout)).EarlyNode = firstPut;
            var later = compiler.gtNewLclvNode(TYP_STRUCT, 0);
            var secondPut = new GenTreePutArgStk(TYP_VOID, later, call, 48, 16, true);
            call.Args.PushBack(NewCallArg.CreateForStruct(later, TYP_STRUCT, layout)).EarlyNode = secondPut;
            block.InsertAtEnd(first);
            block.InsertAtEnd(firstPut);
            block.InsertAtEnd(later);
            block.InsertAtEnd(secondPut);
            block.InsertAtEnd(call);

            LowerFastTailCall(lowering, call);

            Assert.That(compiler.lvaCount, Is.EqualTo(2));
            Assert.That(compiler.lvaTable[1].Type, Is.EqualTo(TYP_STRUCT));
            Assert.That(compiler.lvaTable[1].Layout, Is.SameAs(layout));
            Assert.That(first.LclNum, Is.Zero);
            Assert.That(later.LclNum, Is.EqualTo(1));
        });
    }

    [Test]
    public static void PromotedParameterFieldsAreRehomedAfterParentOverlap()
    {
        WithCompiler((compiler, block, lowering) => {
            var layout = new ClassLayout(16);
            compiler.info.compArgsCount = 1;
            compiler.lvaCount = 3;
            compiler.lvaTable = [new LclVarDsc(), new LclVarDsc(), new LclVarDsc(), new LclVarDsc()];
            compiler.lvaTable[0].Type = TYP_STRUCT;
            compiler.lvaTable[0].Layout = layout;
            compiler.lvaTable[0].lvIsParam = true;
            compiler.lvaTable[0].lvPromoted = true;
            compiler.lvaTable[0].lvFieldLclStart = 1;
            compiler.lvaTable[0].lvFieldCnt = 2;
            compiler.lvaTable[1].Type = TYP_INT;
            compiler.lvaTable[2].Type = TYP_INT;
            compiler.lvaParameterPassingInfo = [
                AbiPassingInformation.FromSegment(compiler, false, AbiPassingSegment.OnStack(32, 0, 16)),
            ];

            var call = FastCall();
            var parent = compiler.gtNewLclvNode(TYP_STRUCT, 0);
            var firstPut = new GenTreePutArgStk(TYP_VOID, parent, call, 32, 16, true);
            call.Args.PushBack(NewCallArg.CreateForStruct(parent, TYP_STRUCT, layout)).EarlyNode = firstPut;
            var field = compiler.gtNewLclvNode(TYP_INT, 1);
            var fieldPut = AddStackArgument(call, field, 48, 8);
            block.InsertAtEnd(parent);
            block.InsertAtEnd(firstPut);
            block.InsertAtEnd(field);
            block.InsertAtEnd(fieldPut);
            block.InsertAtEnd(call);

            LowerFastTailCall(lowering, call);

            Assert.That(compiler.lvaCount, Is.EqualTo(4));
            Assert.That(parent.LclNum, Is.Zero);
            Assert.That(field.LclNum, Is.EqualTo(3));
            Assert.That(compiler.lvaTable[3].Type, Is.EqualTo(TYP_INT));
        });
    }

#if PROFILING_SUPPORTED
    [TestCase(true)]
    [TestCase(false)]
    public static void ProfilerHookPrecedesNonGcRegionOrFirstRegisterArgument(bool hasStackArg)
    {
        WithCompiler((compiler, block, lowering) => {
            ProfilerHookNeeded(compiler) = true;
            var call = FastCall();
            GenTree placement;
            if (hasStackArg)
            {
                var value = compiler.gtNewIconNode(TYP_INT, 1);
                var put = AddStackArgument(call, value, 32, 8);
                block.InsertAtEnd(value);
                block.InsertAtEnd(put);
                placement = put;
            }
            else
            {
                var value = compiler.gtNewIconNode(TYP_INT, 2);
                var put = new GenTreeUnOp(GT_PUTARG_REG, TYP_INT, value);
                call.Args.PushBack(NewCallArg.CreateForPrimitive(value)).EarlyNode = put;
                block.InsertAtEnd(value);
                block.InsertAtEnd(put);
                placement = put;
            }
            block.InsertAtEnd(call);

            LowerFastTailCall(lowering, call);

            var hook = hasStackArg
                ? placement.Prev?.Prev
                : placement.Prev;
            Assert.That(hook?.Oper, Is.EqualTo(GT_PROF_HOOK));
            Assert.That(hook?.Next?.Oper, Is.EqualTo(hasStackArg ? GT_START_NONGC : GT_PUTARG_REG));
        });
    }

    [Test]
    public static void ArgumentlessProfilerTailCallHooksImmediatelyBeforeCall()
    {
        WithCompiler((compiler, block, lowering) => {
            ProfilerHookNeeded(compiler) = true;
            var call = FastCall();
            block.InsertAtEnd(call);

            LowerFastTailCall(lowering, call);

            Assert.That(block.FirstNode?.Oper, Is.EqualTo(GT_PROF_HOOK));
            Assert.That(block.FirstNode?.Next, Is.SameAs(call));
        });
    }
#endif

    private static GenTreeCall FastCall()
        => new(TYP_VOID) { _callMoreFlags = GTF_CALL_M_TAILCALL };

    private static GenTreePutArgStk AddStackArgument(GenTreeCall call, GenTree value, int offset, int size)
    {
        var put = new GenTreePutArgStk(TYP_VOID, value, call, offset, size, true);
        call.Args.PushBack(NewCallArg.CreateForPrimitive(value)).EarlyNode = put;
        return put;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? LoweringBlock(Lowering lowering);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerFastTailCall")]
    private static extern void LowerFastTailCall(Lowering lowering, GenTreeCall call);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "FirstOperand")]
    private static extern GenTree FirstOperand(Lowering lowering, GenTree node);

#if PROFILING_SUPPORTED
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "compProfilerHookNeeded")]
    private static extern ref bool ProfilerHookNeeded(Compiler compiler);
#endif

    private static void WithCompiler(Action<Compiler, BasicBlock, Lowering> action)
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
        compiler.lvaTable = [new LclVarDsc()];
        var block = new BasicBlock(null, null) { Kind = BBJ_RETURN };
        block.MakeLir(null, null);
        compiler.fgFirstBB = block;
        compiler.fgBBcount = 1;
        compiler.compCurBB = block;
        JitTls.Compiler = compiler;
        compiler.codeGen = new CodeGen(compiler);
        var lowering = new Lowering(compiler, new LinearScan(compiler));
        LoweringBlock(lowering) = block;
        try
        {
            action(compiler, block, lowering);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
#endif
