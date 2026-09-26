// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ObjectAllocatorPhaseTests
{
    [TestCase(0, false)]
    [TestCase(OMF_HAS_NEWARRAY, true)]
    public static void IneligibleMethodsLeaveAnalysisAndIRUntouched(int methodFlags, bool minOpts)
    {
        WithAllocator(methodFlags, minOpts, (compiler, allocator, block) =>
        {
            var store = compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_REF, 0));
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(store));
            compiler._dfsTree = compiler.fgComputeDfs();

            var status = InvokePhase(allocator);

            Assert.That(status, Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(Get<bool>(allocator, "_analysisDone"), Is.False);
            Assert.That(store.Data.Oper, Is.EqualTo(GT_CNS_INT));
            Assert.That(compiler._dfsTree, Is.Null);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void HeapMorphingRunsWithOrWithoutMinOpts(bool minOpts)
    {
        WithAllocator(OMF_HAS_NEWOBJ, minOpts, (compiler, allocator, block) =>
        {
            block.SetFlags(BBF_HAS_NEWOBJ);
            var allocation = new GenTreeAllocObj(TYP_REF,
                compiler.gtNewIconNode(TYP_I_IMPL, 123), CORINFO_HELP_NEWSFAST,
                true, (CORINFO_CLASS_STRUCT_*)123);
            var store = compiler.gtNewStoreLclVarNode(0, allocation);
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(store));
            compiler._dfsTree = compiler.fgComputeDfs();

            var status = InvokePhase(allocator);

            Assert.That(status, Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(Get<bool>(allocator, "_analysisDone"), Is.False);
            Assert.That(Get<int>(allocator, "_stackAllocationCount"), Is.Zero);
            Assert.That(store.Data.Oper, Is.EqualTo(GT_CALL));
            Assert.That(store.Data.AsCall().HelperNum, Is.EqualTo(CORINFO_HELP_NEWSFAST));
            Assert.That(store.Data, Is.Not.SameAs(allocation));
            Assert.That(compiler._dfsTree, Is.Null);
        });
    }

    [Test]
    public static void EnabledPhaseAnalyzesBeforeMorphingAndLeavesHeapOnlyLocalsUnretyped()
    {
        WithAllocator(OMF_HAS_NEWOBJ, minOpts: false, (compiler, allocator, block) =>
        {
            block.SetFlags(BBF_HAS_NEWOBJ);
            var allocation = new GenTreeAllocObj(TYP_REF,
                compiler.gtNewIconNode(TYP_I_IMPL, 123), CORINFO_HELP_NEWSFAST,
                true, (CORINFO_CLASS_STRUCT_*)123);
            var store = compiler.gtNewStoreLclVarNode(0, allocation);
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(store));
            compiler._dfsTree = compiler.fgComputeDfs();
            allocator.EnableObjectStackAllocation();

            var status = InvokePhase(allocator);

            Assert.That(status, Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(Get<bool>(allocator, "_analysisDone"), Is.True);
            Assert.That(Get<int>(allocator, "_stackAllocationCount"), Is.Zero);
            Assert.That(compiler.lvaGetDesc(0).Type, Is.EqualTo(TYP_REF));
            Assert.That(store.Data.Oper, Is.EqualTo(GT_CALL));
            Assert.That(store.Data.AsCall().HelperNum, Is.EqualTo(CORINFO_HELP_NEWSFAST));
            Assert.That(compiler._dfsTree, Is.Null);
        });
    }

#if DEBUG
    [Test]
    public static void MethodRangeExcludesStackAnalysisButStillMorphsHeapAllocations()
    {
        WithAllocator(OMF_HAS_NEWOBJ, minOpts: false, (compiler, allocator, block) =>
        {
            block.SetFlags(BBF_HAS_NEWOBJ);
            var allocation = new GenTreeAllocObj(TYP_REF,
                compiler.gtNewIconNode(TYP_I_IMPL, 123), CORINFO_HELP_NEWSFAST,
                true, (CORINFO_CLASS_STRUCT_*)123);
            var store = compiler.gtNewStoreLclVarNode(0, allocation);
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(store));
            allocator.EnableObjectStackAllocation();

            var field = typeof(ObjectAllocator).GetField("s_jitObjectStackAllocationRange",
                BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException("Missing method-range config.");
            var previous = field.GetValue(null);
            var excludedHash = unchecked((uint)compiler.info.compMethodHash() + 1);
            var bytes = System.Text.Encoding.ASCII.GetBytes($"{excludedHash:x8}\0");
            var range = new ConfigMethodRange();
            fixed (byte* value = bytes)
            {
                range.EnsureInit(value);
            }
            field.SetValue(null, range);

            try
            {
                var status = InvokePhase(allocator);

                Assert.That(status, Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
                Assert.That(Get<bool>(allocator, "_analysisDone"), Is.False);
                Assert.That(Get<bool>(allocator, "_isObjectStackAllocationEnabled"), Is.False);
                Assert.That(store.Data.Oper, Is.EqualTo(GT_CALL));
                Assert.That(store.Data.AsCall().HelperNum, Is.EqualTo(CORINFO_HELP_NEWSFAST));
            }
            finally
            {
                field.SetValue(null, previous);
            }
        });
    }
#endif

    private static PhaseStatus InvokePhase(ObjectAllocator allocator)
    {
        var phase = typeof(ObjectAllocator).GetMethod("DoPhase",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Missing object allocation phase.");

        try
        {
            return (PhaseStatus)(phase.Invoke(allocator, null)
                ?? throw new InvalidOperationException("Object allocation phase returned no status."));
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private static T Get<T>(ObjectAllocator allocator, string name)
    {
        var field = typeof(ObjectAllocator).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Missing {name}.");
        return (T)(field.GetValue(allocator) ?? throw new InvalidOperationException($"Uninitialized {name}."));
    }

    private static void WithAllocator(int methodFlags, bool minOpts,
        Action<Compiler, ObjectAllocator, BasicBlock> action)
    {
        var previousConfig = JitConfig;
        object config = previousConfig;
        var field = typeof(JitConfigValues).GetField("_jitObjectStackAllocationTrackFields",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Missing field-tracking config.");
        field.SetValue(config, 0);
        JitConfig = (JitConfigValues)config;

        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.isValueClass = &False;
        vtable.Base.Base.canAllocateOnStack = &False;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
#if DEBUG
        using var tls = new JitTls(&jitInfo);
#endif
        var previousCompiler = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info.compCompHnd = &jitInfo;
        compiler.info.compRetBuffArg = BAD_VAR_NUM;
#if DEBUG
        compiler.info.compFullName = nameof(ObjectAllocatorPhaseTests);
        compiler.fgSafeBasicBlockCreation = true;
#endif
        compiler.lvaTable = new LclVarDsc[1];
        compiler.lvaCount = 1;
        compiler.lvaTable[0].Type = TYP_REF;
        compiler.optMethodFlags = methodFlags;
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(minOpts);
        JitTls.Compiler = compiler;
        var block = BasicBlock.New(compiler, BBJ_RETURN);
        compiler.fgFirstBB = block;
        compiler.fgLastBB = block;

        try
        {
            action(compiler, new ObjectAllocator(compiler), block);
        }
        finally
        {
            JitTls.Compiler = previousCompiler;
            JitConfig = previousConfig;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte False(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* clsHnd) => 0;
}
