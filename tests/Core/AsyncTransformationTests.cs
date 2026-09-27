// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class AsyncTransformationTests
{
    private static int s_continuationSize;
    private static bool[] s_gcSlots = [];
    private static readonly bool[] s_expectedGcSlots = [false, true, true, true, false, true, false, true, true, true];

    private static object NewTransformation(Compiler compiler)
    {
        var type = typeof(Compiler).GetNestedType("AsyncTransformation", BindingFlags.NonPublic) ??
            throw new AssertionException("Missing async transformation");
        return Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, [compiler], null) ?? throw new AssertionException("Could not create async transformation");
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.compHndBBtab = [];
        compiler.fgPredsComputed = true;
        compiler.lvaTable = new LclVarDsc[16];
        compiler.lvaCount = 1;
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
#endif
        JitTls.Compiler = compiler;
        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [Test]
    public static void RemoveFinalizedAsyncArgumentUnlinksBothListsWithoutResettingAbi()
    {
        WithCompiler(compiler => {
            var call = new GenTreeCall(TYP_VOID);
            var after = call.Args.PushFront(NewCallArg.CreateForPrimitive(new GenTreeIntCon(TYP_INT, 3)));
            var removed = call.Args.PushFront(NewCallArg.CreateForPrimitive(new GenTreeIntCon(TYP_INT, 2))
                .WithWellKnownArg(WellKnownArg.AsyncResumedUse));
            var before = call.Args.PushFront(NewCallArg.CreateForPrimitive(new GenTreeIntCon(TYP_INT, 1)));
            call.Args.ArgsComplete(compiler, call);
            call.Args.PushLateBack(before);
            call.Args.PushLateBack(removed);
            call.Args.PushLateBack(after);
            after.AbiInfo = new AbiPassingInformation(2);

            Assert.That(call.Args.AreArgsComplete, Is.True);
            call.Args.RemoveUnsafe(removed);
            Assert.Multiple(() => {
                Assert.That(call.Args.Head, Is.SameAs(before));
                Assert.That(before.Next, Is.SameAs(after));
                Assert.That(call.Args.LateHead, Is.SameAs(before));
                Assert.That(before.LateNext, Is.SameAs(after));
                Assert.That(call.Args.FindWellKnownArg(WellKnownArg.AsyncResumedUse), Is.Null);
                Assert.That(after.AbiInfo.NumSegments, Is.EqualTo(2));
                Assert.That(call.Args.AreArgsComplete, Is.True);
            });

            call.Args.RemoveUnsafe(before);
            call.Args.RemoveUnsafe(after);
            Assert.That(call.Args.IsEmpty, Is.True);
            Assert.That(call.Args.LateHead, Is.Null);
        });
    }

    [Test]
    public static void ResolveOffsetsInRemainderAfterBlockSplitPreservesNodeIdentity()
    {
        WithCompiler(compiler => {
            var initial = BasicBlock.New(compiler, BBJ_RETURN);
            initial.SetFlags(BasicBlockFlags.BBF_IS_LIR);
            compiler.fgFirstBB = initial;
            compiler.fgLastBB = initial;
            var marker = new GenTreeIntCon(TYP_INT, 1);
            var offset = new GenTreeVal(GT_CONTINUATION_MEMBER_OFFSET, TYP_I_IMPL, 0);
            var addend = new GenTreeIntCon(TYP_I_IMPL, 7);
            var use = new GenTreeOp(GT_ADD, TYP_I_IMPL, offset, addend);
            initial.InsertAtEnd(marker);
            initial.InsertAtEnd(offset);
            initial.InsertAtEnd(addend);
            initial.InsertAtEnd(use);
#if DEBUG
            var originalId = offset.TreeId;
#endif
            var remainder = compiler.fgSplitBlockAfterNode(initial, marker);
            var layout = new AsyncContinuationLayout();
            layout.ContinuationMemberOffsets.Add(16);
            var transformation = NewTransformation(compiler);
            var type = typeof(Compiler).GetNestedType("AsyncTransformation", BindingFlags.NonPublic) ??
                throw new AssertionException("Missing async transformation");
            var resolve = type.GetMethod("ResolveContinuationMemberOffsets",
                BindingFlags.Instance | BindingFlags.NonPublic) ??
                throw new AssertionException("Missing symbolic-offset resolution");
            resolve.Invoke(transformation, [layout, 1]);

            Assert.That(initial.FirstLIRNode, Is.SameAs(marker));
            Assert.That(remainder.FirstLIRNode, Is.TypeOf<GenTreeIntCon>());
            var resolved = remainder.FirstLIRNode as GenTreeIntCon ??
                throw new AssertionException("Missing resolved offset");
            Assert.That(use.Op1, Is.SameAs(resolved));
            Assert.That(resolved.IconVal, Is.EqualTo((nint)(
                OFFSETOF__CORINFO_Continuation__data - SIZEOF__CORINFO_Object + 16)));
#if DEBUG
            Assert.That(resolved.TreeId, Is.EqualTo(originalId));
#endif
        });
    }

    [Test]
    public static void ContinuationOffsetHelpersDefaultToNonfaultingMemoryAccess()
    {
        WithCompiler(compiler => {
            var transformation = NewTransformation(compiler);
            var type = typeof(Compiler).GetNestedType("AsyncTransformation", BindingFlags.NonPublic) ??
                throw new AssertionException("Missing async transformation");
            var load = type.GetMethod("LoadFromOffset", BindingFlags.Instance | BindingFlags.NonPublic) ??
                throw new AssertionException("Missing continuation load helper");
            var store = type.GetMethod("StoreAtOffset", BindingFlags.Instance | BindingFlags.NonPublic) ??
                throw new AssertionException("Missing continuation store helper");
            var address = new GenTreeIntCon(TYP_I_IMPL, 1);
            var loaded = (GenTreeIndir)(load.Invoke(transformation,
                [address, 8, TYP_INT, Type.Missing]) ??
                throw new AssertionException("Continuation load failed"));
            var stored = (GenTreeStoreInd)(store.Invoke(transformation,
                [new GenTreeIntCon(TYP_I_IMPL, 1), 8, new GenTreeIntCon(TYP_INT, 3),
                    TYP_INT, Type.Missing]) ??
                throw new AssertionException("Continuation store failed"));
            Assert.Multiple(() => {
                Assert.That((loaded.Flags & GenTreeFlags.GTF_IND_NONFAULTING) != 0, Is.True);
                Assert.That((stored.Flags & GenTreeFlags.GTF_IND_NONFAULTING) != 0, Is.True);
            });
        });
    }

    [Test]
    public static void PlaceholderReplacementPreservesExistingLirMetadata([Values] bool sequence)
    {
        WithCompiler(compiler => {
            compiler.fgNodeThreading = NodeThreading.LIR;
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            block.SetFlags(BasicBlockFlags.BBF_IS_LIR);
            var placeholder = compiler.gtNewIconNode(TYP_INT, 0);
            var result = compiler.gtNewUnaryNode(GT_RETURN, TYP_VOID, placeholder);
            block.InsertAtEnd(placeholder);
            block.InsertAtEnd(result);
            var value = compiler.gtNewIconNode(TYP_INT, 42);
            value.SetCosts(9, 7);
#if DEBUG
            value._seqNum = 17;
#endif
            var transformation = NewTransformation(compiler);
            var type = typeof(Compiler).GetNestedType("AsyncTransformation", BindingFlags.NonPublic) ??
                throw new AssertionException("Missing async transformation");
            var replace = type.GetMethod("ReplaceAsyncPlaceholder",
                BindingFlags.Instance | BindingFlags.NonPublic) ??
                throw new AssertionException("Missing async placeholder replacement");
            replace.Invoke(transformation, [block, placeholder, value, sequence]);

            Assert.That(result.Op1, Is.SameAs(value));
            Assert.That(block.FirstLIRNode, Is.SameAs(value));
            Assert.That(value.Next, Is.SameAs(result));
            Assert.That(placeholder.Prev, Is.Null);
            Assert.That(placeholder.Next, Is.Null);
            if (sequence)
            {
                var expected = compiler.gtNewIconNode(TYP_INT, 42);
                _ = compiler.gtSetEvalOrder(expected);
                Assert.That(value.CostEx, Is.EqualTo(expected.CostEx));
                Assert.That(value.CostSz, Is.EqualTo(expected.CostSz));
            }
            else
            {
                Assert.That(value.CostEx, Is.EqualTo(9));
                Assert.That(value.CostSz, Is.EqualTo(7));
            }
#if DEBUG
            Assert.That(value._seqNum, Is.EqualTo(sequence ? 1 : 17));
#endif
        });
    }

    [Test]
    public static void ContextArgumentExtractionPreservesNativeSpillPolicy([Values] bool allowInvariant)
    {
        WithCompiler(compiler => {
            compiler.fgNodeThreading = NodeThreading.LIR;
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            block.SetFlags(BasicBlockFlags.BBF_IS_LIR);
            var value = compiler.gtNewNull();
            var call = new GenTreeCall(TYP_VOID);
            var argument = call.Args.PushFront(NewCallArg.CreateForPrimitive(value)
                .WithWellKnownArg(WellKnownArg.AsyncExecutionContext));
            call.Args.ArgsComplete(compiler, call);
            block.InsertAtEnd(value);
            block.InsertAtEnd(call);
            var transformation = NewTransformation(compiler);
            var type = typeof(Compiler).GetNestedType("AsyncTransformation", BindingFlags.NonPublic) ??
                throw new AssertionException("Missing async transformation");
            var take = type.GetMethod("TakeAsyncArgument",
                BindingFlags.Instance | BindingFlags.NonPublic) ??
                throw new AssertionException("Missing async argument extraction");
            var extracted = (GenTree)(take.Invoke(transformation, [block, call, argument, allowInvariant]) ??
                throw new AssertionException("Async argument extraction failed"));

            Assert.That(call.Args.IsEmpty, Is.True);
            Assert.That(call.Args.AreArgsComplete, Is.True);
            Assert.That(extracted.Prev, Is.Null);
            Assert.That(extracted.Next, Is.Null);
            Assert.That(compiler.lvaCount, Is.EqualTo(allowInvariant ? 1 : 2));
            if (allowInvariant)
            {
                Assert.That(extracted, Is.SameAs(value));
                Assert.That(block.FirstLIRNode, Is.SameAs(call));
            }
            else
            {
                Assert.That(extracted.Oper, Is.EqualTo(GT_LCL_VAR));
                Assert.That(extracted.Type, Is.EqualTo(TYP_REF));
                var store = call.Prev as GenTreeLclVar ??
                    throw new AssertionException("Missing context spill");
                Assert.That(store.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
                Assert.That(store.Data, Is.SameAs(value));
                Assert.That(store.LclNum, Is.EqualTo(extracted.AsLclVar().LclNum));
            }
        });
    }

    [Test]
    public static void TailAwaitRunCreatesSuspensionAndSplitsContinuationPath()
    {
        WithCompiler(compiler => {
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_ASYNC);
            var entry = BasicBlock.New(compiler, BBJ_RETURN);
            entry.SetFlags(BasicBlockFlags.BBF_IS_LIR);
            entry.bbCodeOffs = 0;
            entry.bbCodeOffsEnd = 10;
            compiler.fgFirstBB = entry;
            compiler.fgLastBB = entry;
            compiler.compCurBB = entry;

            var call = compiler.gtNewCallNode(TYP_VOID, gtCallTypes.CT_USER_FUNC,
                (CORINFO_METHOD_STRUCT_*)1);
            call.SetIsAsync(new AsyncCallInfo {
                IsTailAwait = true,
                AlwaysSuspends = true,
                CallAsyncDebugInfo = new DebugInfo(null, new ILLocation(0, ICorDebugInfo.SOURCE_TYPE_INVALID)),
            });
            entry.InsertAtEnd(call);

            var result = compiler.TransformAsync();

            Assert.That(result, Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(entry.Kind, Is.EqualTo(BBJ_ALWAYS));
            Assert.That(entry.Target.FirstLIRNode?.Oper, Is.EqualTo(GT_LCL_VAR));
            Assert.That(compiler.Blocks, Has.Some.Matches<BasicBlock>(
                block => block.Kind is BBJ_RETURN && block.LastNode?.Oper is GT_RETURN_SUSPEND));
        });
    }

    [TestCase(0, 4, 2, 1 << 4)]
    [TestCase(16, 4, 2, 3 << 4)]
    [TestCase(24, 8, 3, 4 << 8)]
    [TestCase(32, 11, 21, 5 << 11)]
    public static void ContinuationFlagIndicesEncodeOneBasedPointerSlots(
        int offset, int firstBit, int bitCount, int expected)
    {
        var transformation = typeof(Compiler).GetNestedType(
            "AsyncTransformation", BindingFlags.NonPublic) ??
            throw new AssertionException("Missing async transformation");
        var encode = transformation.GetMethod("EncodeContinuationIndex",
            BindingFlags.Static | BindingFlags.NonPublic) ??
            throw new AssertionException("Missing continuation flag encoding");
        var actual = encode.Invoke(null, [offset, firstBit, bitCount]);
        Assert.That(actual, Is.EqualTo(expected));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* GetContinuationType(ICorJitInfo* _, nint size, bool* refs, nint count)
    {
        s_continuationSize = checked((int)size);
        s_gcSlots = new bool[checked((int)count)];

        for (var i = 0; i < s_gcSlots.Length; i++)
        {
            s_gcSlots[i] = refs[i];
        }

        return (CORINFO_CLASS_STRUCT_*)1;
    }

    [Test]
    public static void DefaultValueAnalysisDistinguishesNegativeFloatingZero()
    {
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previousCompiler = JitTls.Compiler;
        JitTls.Compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));

        try
        {
            var positiveZero = new GenTreeDblCon(var_types.TYP_DOUBLE, 0.0);
            var negativeZero = new GenTreeDblCon(var_types.TYP_DOUBLE, BitConverter.Int64BitsToDouble(long.MinValue));
            var integerZero = new GenTreeIntCon(var_types.TYP_INT, 0);

            Assert.Multiple(() => {
                Assert.That(Compiler.IsAsyncDefaultValue(positiveZero), Is.True);
                Assert.That(Compiler.IsAsyncDefaultValue(negativeZero), Is.False);
                Assert.That(Compiler.IsAsyncDefaultValue(integerZero), Is.True);
            });
        }
        finally
        {
            JitTls.Compiler = previousCompiler;
        }
    }

    [Test]
    public static void ContinuationLayoutKeepsReturnAlignmentAndWholeLiveInlineFrame()
    {
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var previousCompiler = JitTls.Compiler;
        JitTls.Compiler = compiler;

        try
        {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.getContinuationType = &GetContinuationType;
            ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
            compiler.info.compCompHnd = &jitInfo;

            var builder = new AsyncContinuationLayoutBuilder(compiler) {
                NeedsOSRAddress = true,
                NeedsExecutionContext = true,
                NeedsContinuationContext = true,
                NeedsException = true,
                NeedsKeepAlive = true,
            };
            builder.AddReturn(new AsyncReturnTypeInfo(var_types.TYP_INT, null));
            builder.AddReturn(new AsyncReturnTypeInfo(var_types.TYP_REF, null));

            var frameExecution = compiler.GetContinuationMemberIndex(ContinuationMember.InlineFrameExecutionContext(2));
            var frameContext = compiler.GetContinuationMemberIndex(ContinuationMember.InlineFrameContinuationContext(2));
            var frameFlags = compiler.GetContinuationMemberIndex(ContinuationMember.InlineFrameFlags(2));
            var liveMember = new GenTreeVal(genTreeOps.GT_CONTINUATION_MEMBER_OFFSET, var_types.TYP_LONG, frameFlags);

            var layout = builder.Create([liveMember]);

            Assert.Multiple(() => {
                Assert.That(layout.Size, Is.EqualTo(80));
                Assert.That(s_continuationSize, Is.EqualTo(layout.Size));
                Assert.That(layout.OSRAddressOffset, Is.EqualTo(0));
                Assert.That(layout.ExecutionContextOffset, Is.EqualTo(8));
                Assert.That(layout.ContinuationContextOffset, Is.EqualTo(16));
                Assert.That(layout.ExceptionOffset, Is.EqualTo(24));
                Assert.That(layout.Returns[0].Offset, Is.EqualTo(32));
                Assert.That(layout.Returns[1].Offset, Is.EqualTo(40));
                Assert.That(layout.ContinuationMemberOffsets[frameFlags], Is.EqualTo(48));
                Assert.That(layout.ContinuationMemberOffsets[frameExecution], Is.EqualTo(56));
                Assert.That(layout.ContinuationMemberOffsets[frameContext], Is.EqualTo(64));
                Assert.That(layout.KeepAliveOffset, Is.EqualTo(72));
                Assert.That(s_gcSlots, Is.EqualTo(s_expectedGcSlots));
            });
        }
        finally
        {
            JitTls.Compiler = previousCompiler;
        }
    }

    [Test]
    public static void SharedLayoutUnionsLocalsInNumberOrderAndDeduplicatesReturnTypes()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var first = new AsyncContinuationLayoutBuilder(compiler) {
            NeedsOSRAddress = true,
            NeedsKeepAlive = true,
        };
        first.AddLocal(1);
        first.AddLocal(5);
        first.AddReturn(new AsyncReturnTypeInfo(var_types.TYP_REF, null));
        first.AddReturn(new AsyncReturnTypeInfo(var_types.TYP_INT, null));
        first.AddReturn(new AsyncReturnTypeInfo(var_types.TYP_REF, null));

        var second = new AsyncContinuationLayoutBuilder(compiler) {
            NeedsException = true,
            NeedsExecutionContext = true,
        };
        second.AddLocal(2);
        second.AddLocal(5);
        second.AddReturn(new AsyncReturnTypeInfo(var_types.TYP_INT, null));
        second.AddReturn(new AsyncReturnTypeInfo(var_types.TYP_LONG, null));

        var shared = AsyncContinuationLayoutBuilder.CreateSharedLayout(compiler, [first, second]);

        Assert.Multiple(() => {
            Assert.That(shared.Locals.Count, Is.EqualTo(3));
            Assert.That(shared.Locals[0], Is.EqualTo(1));
            Assert.That(shared.Locals[1], Is.EqualTo(2));
            Assert.That(shared.Locals[2], Is.EqualTo(5));
            Assert.That(shared.Returns.Count, Is.EqualTo(3));
            Assert.That(shared.Returns[0], Is.EqualTo(new AsyncReturnTypeInfo(var_types.TYP_REF, null)));
            Assert.That(shared.Returns[1], Is.EqualTo(new AsyncReturnTypeInfo(var_types.TYP_INT, null)));
            Assert.That(shared.Returns[2], Is.EqualTo(new AsyncReturnTypeInfo(var_types.TYP_LONG, null)));
            Assert.That(shared.NeedsOSRAddress, Is.True);
            Assert.That(shared.NeedsException, Is.True);
            Assert.That(shared.NeedsKeepAlive, Is.True);
            Assert.That(shared.NeedsExecutionContext, Is.True);
            Assert.That(shared.NeedsContinuationContext, Is.False);
            Assert.That(shared.ContainsLocal(2), Is.True);
            Assert.That(shared.ContainsLocal(3), Is.False);
        });
    }

    [Test]
    public static void LayoutEqualityIncludesOrderedReturnTypesAndCompatibleStructs()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var first = new AsyncContinuationLayoutBuilder(compiler);
        var second = new AsyncContinuationLayoutBuilder(compiler);
        first.AddLocal(1);
        second.AddLocal(1);

        var firstStruct = new ClassLayout(16);
        first.AddReturn(new AsyncReturnTypeInfo(var_types.TYP_STRUCT, firstStruct));
        second.AddReturn(new AsyncReturnTypeInfo(var_types.TYP_STRUCT, firstStruct));
        Assert.That(AsyncContinuationLayoutBuilder.Equals(first, second), Is.True);

        var distinctCustomLayout = new ClassLayout(16);
        var third = new AsyncContinuationLayoutBuilder(compiler);
        third.AddLocal(1);
        third.AddReturn(new AsyncReturnTypeInfo(var_types.TYP_STRUCT, distinctCustomLayout));
        Assert.That(AsyncContinuationLayoutBuilder.Equals(first, third), Is.False);

        second.NeedsContinuationContext = true;
        Assert.That(AsyncContinuationLayoutBuilder.Equals(first, second), Is.False);
        second.NeedsContinuationContext = false;

        second.AddReturn(new AsyncReturnTypeInfo(var_types.TYP_INT, null));
        Assert.That(AsyncContinuationLayoutBuilder.Equals(first, second), Is.False);
    }
}
