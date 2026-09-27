// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeCallFlags;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.gtCallTypes;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ProfileInstrumentationTests
{
    [TestCase(NI_System_Runtime_CompilerServices_RuntimeHelpers_IsRuntimeAsync)]
    [TestCase(NI_System_Runtime_CompilerServices_RuntimeHelpers_SetNextCallAsyncContinuation)]
    [TestCase(NI_System_Runtime_CompilerServices_AsyncHelpers_AsyncSuspend)]
    [TestCase(NI_System_Runtime_CompilerServices_AsyncHelpers_AsyncCallContinuation)]
    [TestCase(NI_System_Runtime_CompilerServices_AsyncHelpers_TailAwait)]
    [TestCase(NI_System_Runtime_CompilerServices_StaticsHelpers_VolatileReadAsByref)]
    [TestCase(NI_System_Threading_Volatile_ReadBarrier)]
    public static void MinimalProfilingExcludesCompilerPrimitives(NamedIntrinsic intrinsic)
    {
        Assert.That(Compiler.ShouldInstrumentMinimalIntrinsic(intrinsic, null), Is.False);
    }

    [TestCase("Vector", false)]
    [TestCase("Vector`1", false)]
    [TestCase("Vector128", true)]
    [TestCase("Vector2", true)]
    public static void NumericsPolicyDistinguishesGenericSimdFromManagedVectors(string className, bool expected)
    {
        Assert.That(Compiler.ShouldInstrumentMinimalIntrinsic(NI_System_Numerics_Intrinsic, className),
            Is.EqualTo(expected));
        Assert.That(Compiler.ShouldInstrumentMinimalIntrinsic(NI_System_SpanHelpers_Memmove, className),
            Is.True);
    }

    [TestCase(false, false, 4, 4, GT_STOREIND)]
    [TestCase(true, false, 4, 4, GT_XADD)]
    [TestCase(false, true, 4, 4, GT_CALL)]
    [TestCase(true, true, 4, 2, GT_COMMA)]
    public static void CounterSchemaAndIncrementRespectModeAndPadding(
        bool interlocked, bool scalable, int padding, int expectedSlots, genTreeOps expectedOperation)
    {
        var previous = JitConfig;
        JitConfig = default;
        Interlocked(ref JitConfig) = interlocked ? 1 : 0;
        Scalable(ref JitConfig) = scalable ? 1 : 0;
        Padding(ref JitConfig) = padding;
        try
        {
            FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
                var block = BasicBlock.New(compiler, BBJ_RETURN);
                block.SetFlags(BBF_IMPORTED);
                block.bbCodeOffs = 42;
                block.bbCountSchemaIndex = -1;
                var instrumentor = new BlockCountInstrumentor(compiler);
                var schema = new List<ICorJitInfo.PgoInstrumentationSchema>();
                instrumentor.BuildSchemaElements(block, schema);

                Assert.That(schema, Has.Count.EqualTo(1));
                Assert.That(schema[0].Count, Is.EqualTo(expectedSlots));
                Assert.That(schema[0].InstrumentationKind,
                    Is.EqualTo(ICorJitInfo.PgoInstrumentationKind.BasicBlockIntCount));
                Assert.That(schema[0].ILOffset, Is.EqualTo(42));
                Assert.That(block.bbCountSchemaIndex, Is.Zero);
                Assert.That(instrumentor.SchemaCount, Is.EqualTo(1));

                var buffer = stackalloc byte[16];
                var address = (byte*)Unsafe.AsPointer(ref buffer[0]);
                var increment = BlockCountInstrumentor.CreateCounterIncrement(compiler, address, TYP_INT);
                Assert.That(increment.Oper, Is.EqualTo(expectedOperation));
                if (interlocked && scalable)
                {
                    var scalableCall = increment.AsOp().Op2.AsCall();
                    Assert.That(scalableCall.Args.GetArgByIndex(0)!.Node.AsIntCon().IconVal,
                        Is.EqualTo((nint)(address + 4)));
                }
            });
        }
        finally
        {
            JitConfig = previous;
        }
    }

    [Test]
    public static void EdgeSchemaUsesSourceBlockProbeOrderAndPseudoReturnEdge()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var entry = BasicBlock.New(compiler, BBJ_COND);
            var branch = BasicBlock.New(compiler, BBJ_ALWAYS);
            var exit = BasicBlock.New(compiler, BBJ_RETURN);
            entry.Next = branch;
            branch.Prev = entry;
            branch.Next = exit;
            exit.Prev = branch;
            compiler.fgFirstBB = entry;
            compiler.fgLastBB = exit;
            entry.bbCodeOffs = 0;
            branch.bbCodeOffs = 5;
            exit.bbCodeOffs = 10;
            entry.SetFlags(BBF_IMPORTED);
            branch.SetFlags(BBF_IMPORTED);
            exit.SetFlags(BBF_IMPORTED);
            entry.SetCond(new FlowEdge(entry, exit, null), new FlowEdge(entry, branch, null));
            branch.SetKindAndTargetEdge(BBJ_ALWAYS, new FlowEdge(branch, exit, null));

            var instrumentor = new EfficientEdgeCountInstrumentor(compiler, minimal: false);
            instrumentor.Prepare(preImport: true);
            var schema = new List<ICorJitInfo.PgoInstrumentationSchema>();
            foreach (var block in compiler.Blocks)
            {
                instrumentor.BuildSchemaElements(block, schema);
            }

            Assert.That(instrumentor.SchemaCount, Is.EqualTo(2));
            Assert.That(schema, Has.Count.EqualTo(2));
            Assert.That(schema[0].ILOffset, Is.EqualTo(5));
            Assert.That(schema[0].Other, Is.EqualTo(10));
            Assert.That(schema[1].ILOffset, Is.EqualTo(10));
            Assert.That(schema[1].Other, Is.Zero);
            Assert.That(schema[0].InstrumentationKind,
                Is.EqualTo(ICorJitInfo.PgoInstrumentationKind.EdgeIntCount));
        });
    }

    [Test]
    public static void TailcallReturnCountIsPlacedInTheLivePredecessor()
    {
        var previous = JitConfig;
        JitConfig = default;
        try
        {
            FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
                compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_BBINSTR);
                compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_BBOPT);
                compiler.optMethodFlags |= OMF_HAS_TAILCALL_SUCCESSOR;
                var pred = BasicBlock.New(compiler, BBJ_ALWAYS);
                var ret = BasicBlock.New(compiler, BBJ_RETURN);
                pred.Next = ret;
                ret.Prev = pred;
                compiler.fgFirstBB = pred;
                compiler.fgLastBB = ret;
                pred.SetFlags(BBF_IMPORTED);
                ret.SetFlags(BBF_IMPORTED | BBF_TAILCALL_SUCCESSOR);
                pred.bbCodeOffs = 0;
                ret.bbCodeOffs = 5;
                var edge = compiler.fgAddRefPred(ret, pred);
                pred.TargetEdge = edge;

                var instrumentor = new BlockCountInstrumentor(compiler);
                instrumentor.Prepare(preImport: false);
                var schema = new List<ICorJitInfo.PgoInstrumentationSchema>();
                foreach (var block in compiler.Blocks)
                {
                    instrumentor.BuildSchemaElements(block, schema);
                }

                var buffer = stackalloc byte[16];
                instrumentor.Instrument(ret, schema, (byte*)Unsafe.AsPointer(ref buffer[0]));
                Assert.That(ret.FirstStmt, Is.Null);
                Assert.That(pred.FirstStmt?.RootNode.Oper, Is.EqualTo(GT_STOREIND));
                Assert.That(instrumentor.InstrCount, Is.EqualTo(1));
            });
        }
        finally
        {
            JitConfig = previous;
        }
    }

#if DEBUG
    [TestCase(false, 4294967295.0, 4294967295UL)]
    [TestCase(false, 4294967296.0, 0UL)]
    [TestCase(false, 4294967297.0, 1UL)]
    [TestCase(false, 9223372036854779904.0, 0UL)]
    [TestCase(true, 9223372036854775808.0, 9223372036854775808UL)]
    [TestCase(true, 9223372036854779904.0, 9223372036854779904UL)]
    [TestCase(true, 18446744073709551616.0, 9223372036854775808UL)]
    public static void SynthesizedBlockCountsUseNativeIntegerConversion(bool wide, double weight, ulong expected)
    {
        WithSynthesizedCountConfig(() =>
            FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
                var block = BasicBlock.New(compiler, BBJ_RETURN);
                block.bbWeight = weight;
                block.bbCountSchemaIndex = 0;
                var kind = wide ? ICorJitInfo.PgoInstrumentationKind.BasicBlockLongCount
                    : ICorJitInfo.PgoInstrumentationKind.BasicBlockIntCount;
                var schema = new List<ICorJitInfo.PgoInstrumentationSchema> {
                    new() { InstrumentationKind = kind, ILOffset = block.bbCodeOffs },
                };
                var buffer = stackalloc byte[16];
                var address = (byte*)Unsafe.AsPointer(ref buffer[0]);
                new BlockCountInstrumentor(compiler).Instrument(block, schema, address);
                Assert.That(wide ? *(ulong*)address : *(uint*)address, Is.EqualTo(expected));
            }));
    }

    [TestCase(false, 4294967295.0, 4294967295UL)]
    [TestCase(false, 4294967296.0, 0UL)]
    [TestCase(false, 4294967297.0, 1UL)]
    [TestCase(false, 9223372036854779904.0, 0UL)]
    [TestCase(true, 9223372036854775808.0, 9223372036854775808UL)]
    [TestCase(true, 9223372036854779904.0, 9223372036854779904UL)]
    [TestCase(true, 18446744073709551616.0, 9223372036854775808UL)]
    public static void SynthesizedEdgeCountsUseNativeIntegerConversion(bool wide, double weight, ulong expected)
    {
        WithSynthesizedCountConfig(() =>
            FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
                var entry = BasicBlock.New(compiler, BBJ_COND);
                var source = BasicBlock.New(compiler, BBJ_ALWAYS);
                var target = BasicBlock.New(compiler, BBJ_RETURN);
                entry.Next = source;
                source.Prev = entry;
                source.Next = target;
                target.Prev = source;
                compiler.fgFirstBB = entry;
                compiler.fgLastBB = target;
                entry.SetFlags(BBF_IMPORTED);
                source.SetFlags(BBF_IMPORTED);
                target.SetFlags(BBF_IMPORTED);
                entry.bbCodeOffs = 0;
                source.bbCodeOffs = 5;
                target.bbCodeOffs = 10;
                source.bbWeight = weight;
                entry.SetCond(new FlowEdge(entry, target, null), new FlowEdge(entry, source, null));
                var edge = compiler.fgAddRefPred(target, source);
                source.SetKindAndTargetEdge(BBJ_ALWAYS, edge);
                edge.Likelihood = 1.0;
                var instrumentor = new EfficientEdgeCountInstrumentor(compiler, minimal: false);
                instrumentor.Prepare(preImport: true);
                compiler.opts.compCollect64BitCounts = wide;
                var schema = new List<ICorJitInfo.PgoInstrumentationSchema>();
                foreach (var block in compiler.Blocks)
                {
                    instrumentor.BuildSchemaElements(block, schema);
                }

                Assert.That(schema[0].ILOffset, Is.EqualTo(5));
                var buffer = stackalloc byte[16];
                var address = (byte*)Unsafe.AsPointer(ref buffer[0]);
                instrumentor.Instrument(source, schema, address);
                Assert.That(wide ? *(ulong*)address : *(uint*)address, Is.EqualTo(expected));
            }));
    }

    private static void WithSynthesizedCountConfig(Action test)
    {
        var previous = JitConfig;
        JitConfig = default;
        PropagateSynthesizedCounts(ref JitConfig) = 1;
        try
        {
            test();
        }
        finally
        {
            JitConfig = previous;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitPropagateSynthesizedCountsToProfileData")]
    private static extern ref int PropagateSynthesizedCounts(ref JitConfigValues config);
#endif

    [Test]
    public static void InterfaceCallHistogramPreservesPairOrderAndObjectEvaluation()
    {
        var previous = JitConfig;
        JitConfig = default;
        ClassProfiling(ref JitConfig) = 1;
        try
        {
            FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
                compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_BBINSTR);
                var block = BasicBlock.New(compiler, BBJ_RETURN);
                compiler.fgFirstBB = block;
                compiler.fgLastBB = block;
                block.SetFlags(BBF_IMPORTED | BBF_HAS_HISTOGRAM_PROFILE);
                var call = compiler.gtNewCallNode(TYP_REF, CT_USER_FUNC, (CORINFO_METHOD_STRUCT_*)0x10);
                call.Flags |= GTF_CALL_VIRT_STUB;
                call._handleHistogramProfileCandidateInfo = new() { ilOffset = 17, probeIndex = 0 };
                var instance = compiler.gtNewLclvNode(TYP_REF, 0);
                _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(instance)
                    .WithWellKnownArg(WellKnownArg.ThisPointer));
                compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(call));

                var instrumentor = new HandleHistogramProbeInstrumentor(compiler);
                var schema = new List<ICorJitInfo.PgoInstrumentationSchema>();
                instrumentor.BuildSchemaElements(block, schema);
                Assert.That(schema, Has.Count.EqualTo(2));
                Assert.That(schema[0].InstrumentationKind,
                    Is.EqualTo(ICorJitInfo.PgoInstrumentationKind.HandleHistogramIntCount));
                Assert.That(schema[1].InstrumentationKind,
                    Is.EqualTo(ICorJitInfo.PgoInstrumentationKind.HandleHistogramTypes));
                Assert.That(schema[0].Other, Is.EqualTo(ICorJitInfo.HandleHistogram32.CLASS_FLAG |
                    ICorJitInfo.HandleHistogram32.INTERFACE_FLAG));
                Assert.That(schema[1].Count, Is.EqualTo(ICorJitInfo.HandleHistogram32.SIZE));
                Assert.That(schema[0].ILOffset, Is.EqualTo(17));

                var buffer = stackalloc byte[512];
                var address = (byte*)Unsafe.AsPointer(ref buffer[0]);
                var count = schema[0];
                count.Offset = 0;
                schema[0] = count;
                var table = schema[1];
                table.Offset = 8;
                schema[1] = table;
                instrumentor.Instrument(block, schema, address);
                Assert.That(instrumentor.InstrCount, Is.EqualTo(1));
                Assert.That(call.Args.ThisArg!.EarlyNode!.Oper, Is.EqualTo(GT_COMMA));
                var store = call.Args.ThisArg.EarlyNode.AsOp().Op1.AsLclVar();
                Assert.That(store.Data, Is.SameAs(instance));
            });
        }
        finally
        {
            JitConfig = previous;
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void MethodHistogramsRetainTypeBeforeMethodAndDelegateFlags(bool isDelegate)
    {
        var previous = JitConfig;
        JitConfig = default;
        ClassProfiling(ref JitConfig) = 1;
        VTableProfiling(ref JitConfig) = 1;
        DelegateProfiling(ref JitConfig) = 1;
        try
        {
            FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
                ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
                vtable.Base.embedMethodHandle = &EmbedProfileMethodHandle;
                ICorJitInfo runtime = new() { lpVtbl = &vtable };
                compiler.info.compCompHnd = &runtime;
                compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_BBINSTR);
                var block = BasicBlock.New(compiler, BBJ_RETURN);
                block.SetFlags(BBF_IMPORTED | BBF_HAS_HISTOGRAM_PROFILE);
                compiler.fgFirstBB = block;
                compiler.fgLastBB = block;
                var call = compiler.gtNewCallNode(TYP_REF, CT_USER_FUNC, (CORINFO_METHOD_STRUCT_*)0x10);
                if (isDelegate)
                {
                    call._callMoreFlags |= GTF_CALL_M_DELEGATE_INV;
                }
                else
                {
                    call.Flags |= GTF_CALL_VIRT_VTABLE;
                }

                call._handleHistogramProfileCandidateInfo = new() { ilOffset = 23 };
                _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewLclvNode(TYP_REF, 0))
                    .WithWellKnownArg(WellKnownArg.ThisPointer));
                compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(call));
                var instrumentor = new HandleHistogramProbeInstrumentor(compiler);
                var schema = new List<ICorJitInfo.PgoInstrumentationSchema>();
                instrumentor.BuildSchemaElements(block, schema);
                Assert.That(schema, Has.Count.EqualTo(isDelegate ? 2 : 4));
                var methodIndex = isDelegate ? 0 : 2;
                if (!isDelegate)
                {
                    Assert.That(schema[1].InstrumentationKind,
                        Is.EqualTo(ICorJitInfo.PgoInstrumentationKind.HandleHistogramTypes));
                }

                Assert.That(schema[methodIndex + 1].InstrumentationKind,
                    Is.EqualTo(ICorJitInfo.PgoInstrumentationKind.HandleHistogramMethods));
                Assert.That(schema[methodIndex].Other,
                    Is.EqualTo(isDelegate ? ICorJitInfo.HandleHistogram32.DELEGATE_FLAG : 0));

                var buffer = stackalloc byte[1024];
                var address = (byte*)Unsafe.AsPointer(ref buffer[0]);
                for (var i = 0; i < schema.Count; i++)
                {
                    var entry = schema[i];
                    entry.Offset = i switch { 0 => 0, 1 => 8, 2 => 264, _ => 272 };
                    schema[i] = entry;
                }

                instrumentor.Instrument(block, schema, address);
                Assert.That(instrumentor.InstrCount, Is.EqualTo(1));
                var probeSequence = call.Args.ThisArg!.EarlyNode!.AsOp().Op2.AsOp().Op1;
                Assert.That(probeSequence.Oper, Is.EqualTo(isDelegate ? GT_CALL : GT_COMMA));
                if (!isDelegate)
                {
                    Assert.That(probeSequence.AsOp().Op1.AsCall().HelperNum,
                        Is.EqualTo(CorInfoHelpFunc.CORINFO_HELP_CLASSPROFILE32));
                    probeSequence = probeSequence.AsOp().Op2;
                }

                Assert.That(probeSequence.AsCall().HelperNum,
                    Is.EqualTo(isDelegate
                        ? CorInfoHelpFunc.CORINFO_HELP_DELEGATEPROFILE32
                        : CorInfoHelpFunc.CORINFO_HELP_VTABLEPROFILE32));
            });
        }
        finally
        {
            JitConfig = previous;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_METHOD_STRUCT_* EmbedProfileMethodHandle(
        ICorJitInfo* self, CORINFO_METHOD_STRUCT_* method, void** indirection)
    {
        *indirection = null;
        return method;
    }

    [Test]
    public static void ValueHistogramProfilesLengthOnceAndPreservesTheArgument()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            fixed (byte* className = "SpanHelpers\0"u8)
            fixed (byte* methodName = "Memmove\0"u8)
            fixed (byte* namespaceName = "System\0"u8)
            {
                var metadata = new ProfileMethodMetadata { Class = className, Method = methodName, Namespace = namespaceName };
                ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
                vtable.Base.Base.getMethodNameFromMetadata = &GetProfileMethodName;
                ICorJitInfo runtime = new() { lpVtbl = &vtable };
                compiler.info.compCompHnd = &runtime;
                var block = BasicBlock.New(compiler, BBJ_RETURN);
                compiler.fgFirstBB = block;
                compiler.fgLastBB = block;
                block.SetFlags(BBF_IMPORTED | BBF_HAS_VALUE_PROFILE);
                var call = compiler.gtNewCallNode(TYP_VOID, CT_USER_FUNC, (CORINFO_METHOD_STRUCT_*)&metadata);
                call._callMoreFlags |= GTF_CALL_M_SPECIAL_INTRINSIC;
                call._handleHistogramProfileCandidateInfo = new() { ilOffset = 19 };
                _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewLclvNode(TYP_BYREF, 0)));
                _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewLclvNode(TYP_BYREF, 1)));
                var length = compiler.gtNewIconNode(TYP_I_IMPL, 7);
                _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(length));
                compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(call));

                var instrumentor = new ValueInstrumentor(compiler);
                var schema = new List<ICorJitInfo.PgoInstrumentationSchema>();
                instrumentor.BuildSchemaElements(block, schema);
                Assert.That(schema, Has.Count.EqualTo(2));
                Assert.That(schema[0].InstrumentationKind,
                    Is.EqualTo(ICorJitInfo.PgoInstrumentationKind.ValueHistogramIntCount));
                Assert.That(schema[1].InstrumentationKind,
                    Is.EqualTo(ICorJitInfo.PgoInstrumentationKind.ValueHistogram));
                Assert.That(schema[1].Count, Is.EqualTo(ICorJitInfo.HandleHistogram32.SIZE));
                Assert.That(schema[0].ILOffset, Is.EqualTo(19));

                var buffer = stackalloc byte[512];
                var address = (byte*)Unsafe.AsPointer(ref buffer[0]);
                instrumentor.Instrument(block, schema, address);
                Assert.That(instrumentor.InstrCount, Is.EqualTo(1));
                var replacement = call.Args.GetUserArgByIndex(2)!.EarlyNode!;
                Assert.That(replacement.Oper, Is.EqualTo(GT_COMMA));
                Assert.That(replacement.AsOp().Op1.AsCall().HelperNum,
                    Is.EqualTo(CorInfoHelpFunc.CORINFO_HELP_VALUEPROFILE32));
                Assert.That(replacement.AsOp().Op2.Oper, Is.EqualTo(GT_LCL_VAR));
                var helperLength = replacement.AsOp().Op1.AsCall().Args.GetArgByIndex(0)!.Node;
                Assert.That(helperLength.AsOp().Op1.AsLclVar().Data, Is.SameAs(length));
            }
        });
    }

    private struct ProfileMethodMetadata
    {
        public byte* Class;
        public byte* Method;
        public byte* Namespace;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte* GetProfileMethodName(ICorJitInfo* self, CORINFO_METHOD_STRUCT_* method,
        byte** className, byte** namespaceName, byte** enclosingClasses, nint maxEnclosingClasses)
    {
        var metadata = (ProfileMethodMetadata*)method;
        *className = metadata->Class;
        *namespaceName = metadata->Namespace;
        return metadata->Method;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitInterlockedProfiling")]
    private static extern ref int Interlocked(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitScalableProfiling")]
    private static extern ref int Scalable(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitCounterPadding")]
    private static extern ref int Padding(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitClassProfiling")]
    private static extern ref int ClassProfiling(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitVTableProfiling")]
    private static extern ref int VTableProfiling(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitDelegateProfiling")]
    private static extern ref int DelegateProfiling(ref JitConfigValues config);
}
