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
using static RyuJitSharp.CorInfoType;
using static RyuJitSharp.Globals;
using static RyuJitSharp.GenTreeCallFlags;
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

    [Test]
    public static void EnabledPhaseStackAllocatesReferenceClassFromEELayout()
    {
        WithAllocator(OMF_HAS_NEWOBJ, minOpts: false, (compiler, allocator, block) =>
        {
            block.SetFlags(BBF_HAS_NEWOBJ);
            allocator.EnableObjectStackAllocation();

            var classHandle = (CORINFO_CLASS_STRUCT_*)123;
            var allocation = new GenTreeAllocObj(TYP_REF, compiler.gtNewIconNode(TYP_I_IMPL, 123),
                CORINFO_HELP_NEWSFAST, true, classHandle);
            var store = compiler.gtNewStoreLclVarNode(0, allocation);
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(store));

            var comparison = compiler.gtNewBinaryNode(GT_EQ, TYP_INT,
                compiler.gtNewLclvNode(TYP_REF, 0), compiler.gtNewIconNode(TYP_REF, 1));
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(comparison));
            compiler._dfsTree = compiler.fgComputeDfs();

            var status = InvokePhase(allocator);

            Assert.That(status, Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(Get<int>(allocator, "_stackAllocationCount"), Is.EqualTo(1));
            Assert.That(store.Oper, Is.EqualTo(GT_NOP));
            Assert.That(compiler.lvaGetDesc(1).lvStackAllocatedObject, Is.True);
            var layout = compiler.lvaGetDesc(1).Layout
                ?? throw new InvalidOperationException("Stack-allocated class layout was not recorded.");
            Assert.That(layout.Size, Is.EqualTo(24u));
            Assert.That(layout.ClassHandle == classHandle, Is.True);
            Assert.That(compiler._dfsTree, Is.Null);
        }, canAllocateOnStack: true);
    }

    [Test]
    public static void EnabledPhaseStackAllocatesBoxedValueClassFromEELayout()
    {
        WithAllocator(OMF_HAS_NEWOBJ, minOpts: false, (compiler, allocator, block) =>
        {
            block.SetFlags(BBF_HAS_NEWOBJ);
            allocator.EnableObjectStackAllocation();

            var classHandle = (CORINFO_CLASS_STRUCT_*)124;
            var allocation = new GenTreeAllocObj(TYP_REF, compiler.gtNewIconNode(TYP_I_IMPL, 124),
                CORINFO_HELP_BOX, true, classHandle);
            var store = compiler.gtNewStoreLclVarNode(0, allocation);
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(store));

            var comparison = compiler.gtNewBinaryNode(GT_EQ, TYP_INT,
                compiler.gtNewLclvNode(TYP_REF, 0), compiler.gtNewIconNode(TYP_REF, 1));
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(comparison));
            compiler._dfsTree = compiler.fgComputeDfs();

            var status = InvokePhase(allocator);

            Assert.That(status, Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(Get<int>(allocator, "_stackAllocationCount"), Is.EqualTo(1));
            Assert.That(store.Oper, Is.EqualTo(GT_NOP));
            Assert.That(compiler.lvaGetDesc(1).lvStackAllocatedObject, Is.True);
            var layout = compiler.lvaGetDesc(1).Layout
                ?? throw new InvalidOperationException("Boxed value-class layout was not recorded.");
            Assert.That(layout.Size, Is.EqualTo(16u));
            Assert.That(layout.IsCustomLayout, Is.True);
            Assert.That(compiler._dfsTree, Is.Null);
        }, enableBoxedValueClass: true);
    }

    [Test]
    public static void EnabledPhaseStackAllocatesConstantLengthArrayFromEELayout()
    {
        WithAllocator(OMF_HAS_NEWARRAY, minOpts: false, (compiler, allocator, block) =>
        {
            block.SetFlags(BBF_HAS_NEWARR);
            allocator.EnableObjectStackAllocation();

            var arrayHandle = (CORINFO_CLASS_STRUCT_*)789;
            var call = compiler.gtNewHelperCallNode(TYP_REF, CORINFO_HELP_NEWARR_1_DIRECT,
                compiler.gtNewIconNode(TYP_I_IMPL, 789), compiler.gtNewIconNode(TYP_INT, 3));
            call._compileTimeHelperArgumentHandle = (CORINFO_GENERIC_STRUCT_*)arrayHandle;
            var store = compiler.gtNewStoreLclVarNode(0, call);
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(store));

            var comparison = compiler.gtNewBinaryNode(GT_EQ, TYP_INT,
                compiler.gtNewLclvNode(TYP_REF, 0), compiler.gtNewIconNode(TYP_REF, 1));
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(comparison));
            compiler._dfsTree = compiler.fgComputeDfs();

            var status = InvokePhase(allocator);

            Assert.That(status, Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(Get<int>(allocator, "_stackAllocationCount"), Is.EqualTo(1));
            Assert.That(compiler.lvaGetDesc(1).lvStackAllocatedObject, Is.True);
            Assert.That(compiler.lvaGetDesc(1).Layout, Is.Not.Null);
            Assert.That(compiler.MethodHasStackAllocatedArray, Is.True);
            Assert.That(call._callMoreFlags & GTF_CALL_M_STACK_ARRAY, Is.Not.Zero);
            Assert.That(call.Type, Is.EqualTo(TYP_I_IMPL));
            Assert.That(compiler._dfsTree, Is.Null);
        }, enableArray: true);
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
        Action<Compiler, ObjectAllocator, BasicBlock> action, bool canAllocateOnStack = false,
        bool enableBoxedValueClass = false, bool enableArray = false)
    {
        var previousConfig = JitConfig;
        object config = previousConfig;
        var field = typeof(JitConfigValues).GetField("_jitObjectStackAllocationTrackFields",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Missing field-tracking config.");
        field.SetValue(config, 0);
        field = typeof(JitConfigValues).GetField("_jitObjectStackAllocationRefClass",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Missing reference-class allocation config.");
        field.SetValue(config, canAllocateOnStack ? 1 : 0);
        field = typeof(JitConfigValues).GetField("_jitObjectStackAllocationBoxedValueClass",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Missing boxed-value-class allocation config.");
        field.SetValue(config, enableBoxedValueClass ? 1 : 0);
        field = typeof(JitConfigValues).GetField("_jitObjectStackAllocationArray",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Missing array allocation config.");
        field.SetValue(config, enableArray ? 1 : 0);
        JitConfig = (JitConfigValues)config;

        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.isValueClass = &IsValueClass;
        vtable.Base.Base.getClassSize = &GetClassSize;
        vtable.Base.Base.canAllocateOnStack = canAllocateOnStack ? &True : &False;
        vtable.Base.Base.getTypeForBox = &GetTypeForBox;
        vtable.Base.Base.getHeapClassSize = &GetHeapClassSize;
        vtable.Base.Base.getClassGClayout = &GetClassGClayout;
        vtable.Base.Base.getClassAttribs = &GetClassAttribs;
        vtable.Base.Base.getTypeLayout = &GetTypeLayout;
        vtable.Base.Base.printClassName = &PrintClassName;
        vtable.Base.Base.getArrayRank = &GetArrayRank;
        vtable.Base.Base.getChildType = &GetChildType;
        vtable.Base.Base.getTypeInstantiationArgument = &GetTypeInstantiationArgument;
        vtable.Base.Base.isIntrinsicType = &IsIntrinsicType;
        vtable.Base.Base.runWithSPMIErrorTrap =
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, delegate* unmanaged[Cdecl]<void*, void>, void*, byte>)&RunWithErrorTrap;
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
        compiler.lvaTable = new LclVarDsc[8];
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

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte True(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* clsHnd) => 1;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte IsValueClass(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* clsHnd)
        => (nint)clsHnd is 124 or 456 ? (byte)1 : (byte)0;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetClassSize(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* clsHnd) => 8;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* GetTypeForBox(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* clsHnd)
        => (nint)clsHnd is 124 ? (CORINFO_CLASS_STRUCT_*)456 : null;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetHeapClassSize(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* clsHnd) => 24;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetClassGClayout(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* clsHnd,
        CorInfoGCType* gcPtrs) => 0;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoFlag GetClassAttribs(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* clsHnd) => 0;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static GetTypeLayoutResult GetTypeLayout(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* clsHnd,
        CORINFO_TYPE_LAYOUT_NODE* nodes, nint* count) => GetTypeLayoutResult.Failure;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetArrayRank(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* clsHnd)
        => (nint)clsHnd is 789 ? 1 : 0;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoType GetChildType(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* clsHnd,
        CORINFO_CLASS_STRUCT_** childClass)
    {
        *childClass = null;
        return (nint)clsHnd is 789 ? CORINFO_TYPE_INT : CORINFO_TYPE_UNDEF;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* GetTypeInstantiationArgument(ICorJitInfo* self,
        CORINFO_CLASS_STRUCT_* clsHnd, int index) => null;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte IsIntrinsicType(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* clsHnd) => 0;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static nint PrintClassName(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* clsHnd, byte* buffer,
        nint length, nint* required)
    {
        var name = "TestClass"u8;
        *required = name.Length + 1;
        var written = (int)Math.Min(Math.Max(length - 1, 0), name.Length);
        name[..written].CopyTo(new Span<byte>(buffer, written));
        if (length > 0)
        {
            buffer[written] = 0;
        }
        return written;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte RunWithErrorTrap(ICorJitInfo* self, delegate* unmanaged[Cdecl]<void*, void> callback,
        void* state)
    {
        callback(state);
        return 1;
    }
}
