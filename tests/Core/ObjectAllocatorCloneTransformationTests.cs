// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;
using BitVecOps = RyuJitSharp.BitSetOps<RyuJitSharp.BitVecTraits, RyuJitSharp.BitVecTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ObjectAllocatorCloneTransformationTests
{
    private static readonly List<string> s_assertions = [];
#if DEBUG
    private static bool s_captureCloneRoot;
    private static GenTree? s_rootBeforeRewrite;
    private static int s_rootTreeIdBeforeRewrite;
#endif

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public static void CloningRedirectsAllocationAndRewritesOnlyFastPath(bool emptyStatic, bool selfCopy)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        s_assertions.Clear();
#if DEBUG
        s_captureCloneRoot = selfCopy;
        s_rootBeforeRewrite = null;
        s_rootTreeIdBeforeRewrite = BAD_VAR_NUM;
#endif
        vtable.doAssert = &RecordAssertion;
        vtable.Base.Base.getArrayRank = &GetArrayRank;
        vtable.Base.Base.getTypeInstantiationArgument = &GetTypeArgument;
        vtable.Base.Base.printClassName = &PrintClassName;
        vtable.Base.Base.runWithSPMIErrorTrap =
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, delegate* unmanaged[Cdecl]<void*, void>, void*, byte>)&RunWithErrorTrap;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
#if DEBUG
        using var tls = new JitTls(&jitInfo);
#endif
        var previousCompiler = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
#if DEBUG
        compiler.info.compFullName = nameof(ObjectAllocatorCloneTransformationTests);
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
#endif
        compiler.info.compCompHnd = &jitInfo;
        compiler.info.compRetBuffArg = BAD_VAR_NUM;
        compiler.lvaTable = new LclVarDsc[2];
        compiler.lvaCount = 1;
        compiler.lvaTable[0].Type = TYP_REF;
        compiler.lvaTrackedToVarNum = new int[2];
        compiler.fgPgoConsistent = true;
        compiler.fgPredsComputed = true;
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        JitTls.Compiler = compiler;

        try
        {
            var alloc = BasicBlock.New(compiler, BBKinds.BBJ_ALWAYS);
            compiler.fgFirstBB = alloc;
            compiler.fgLastBB = alloc;
            var def = compiler.fgNewBBafter(BBKinds.BBJ_RETURN, alloc, extendRegion: false);
            def.clearTryIndex();
            def.clearHndIndex();
            var edge = compiler.fgAddRefPred(def, alloc);
            alloc.SetKindAndTargetEdge(BBKinds.BBJ_ALWAYS, edge);
            alloc.bbWeight = 2.0;
            def.bbWeight = 2.0;
            GenTree value = selfCopy
                ? compiler.gtNewLclvNode(TYP_REF, 0) : compiler.gtNewIconNode(TYP_REF, 0);
            var store = compiler.gtNewStoreLclVarNode(0, value);
            var statement = compiler.gtNewStmt(store);
            compiler.fgInsertStmtAtEnd(def, statement);

            var allocator = new ObjectAllocator(compiler);
            var traits = new BitVecTraits(compiler, 4);
            Set(allocator, "_bitVecTraits", traits);
            Set(allocator, "_connGraphAdjacencyMatrix", new nint[4][]);
            Set(allocator, "_nextLocalIndex", 1);
            Set(allocator, "_firstPseudoIndex", 2);
            Set(allocator, "_maxPseudos", 1);
            Set(allocator, "_regionsToClone", 1);
            var type = typeof(ObjectAllocator).GetNestedType("CloneInfo", BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Missing clone information.");
            var info = Activator.CreateInstance(type) ?? throw new InvalidOperationException("Missing clone information.");
            var variableType = typeof(ObjectAllocator).GetNestedType("EnumeratorVar", BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Missing enumerator variable.");
            var variable = Activator.CreateInstance(variableType)
                ?? throw new InvalidOperationException("Missing enumerator variable.");
            var appearanceType = typeof(ObjectAllocator).GetNestedType("EnumeratorVarAppearance", BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Missing appearance type.");
            var appearance = Activator.CreateInstance(appearanceType, def, statement, 0, true)
                ?? throw new InvalidOperationException("Missing enumerator appearance.");
            var appearances = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(appearanceType))!;
            appearances.Add(appearance);
            Set(variable, "Appearances", appearances);
            var variables = (IDictionary)Activator.CreateInstance(
                typeof(Dictionary<,>).MakeGenericType(typeof(int), variableType))!;
            variables.Add(0, variable);
            Set(info, "AppearanceMap", variables);
            Set(info, "BlocksToClone", new List<BasicBlock> { def });
            Set(info, "AllocBlock", alloc);
            var allocTree = compiler.gtNewIconNode(TYP_REF, 1);
            if (emptyStatic)
            {
                allocTree.Flags |= GTF_ALLOCOBJ_EMPTY_STATIC;
            }
            Set(info, "AllocTree", allocTree);
            Set(info, "Type", System.Reflection.Pointer.Box((void*)1, typeof(CORINFO_CLASS_STRUCT_*)));
            Set(info, "ProfileScale", 1.5);
            Set(info, "CanClone", true);
            Set(info, "WillClone", true);

            var map = (IDictionary)Get(allocator, "_cloneMap");
            map.Add(2, info);
            Call(allocator, "CloneAndSpecialize");

            var cloned = alloc.Target;
            Assert.That(cloned, Is.Not.SameAs(def));
            Assert.That(cloned.Kind, Is.EqualTo(BBKinds.BBJ_RETURN));
            Assert.That(cloned.FirstStmt?.RootNode.Oper,
                Is.EqualTo(selfCopy ? GT_NOP : GT_STORE_LCL_VAR));
            if (selfCopy)
            {
#if DEBUG
                Assert.That(s_rootBeforeRewrite, Is.Not.Null);
                Assert.That(cloned.FirstStmt?.RootNode, Is.SameAs(s_rootBeforeRewrite));
                Assert.That(cloned.FirstStmt?.RootNode.TreeId, Is.EqualTo(s_rootTreeIdBeforeRewrite));
#endif
            }
            else
            {
                Assert.That(cloned.FirstStmt?.RootNode.AsLclVar().LclNum, Is.EqualTo(1));
            }
            Assert.That(def.FirstStmt?.RootNode.AsLclVar().LclNum, Is.Zero);
            Assert.That(compiler.lvaCount, Is.EqualTo(2));
            Assert.That(compiler.lvaGetDesc(1).lvClassIsExact, Is.True);
            Assert.That(compiler.lvaGetDesc(1).lvTracked, Is.True);
            Assert.That(compiler.lvaTrackedToVarNum[1], Is.EqualTo(1));
            Assert.That(compiler.fgPgoConsistent, Is.EqualTo(emptyStatic));
            Assert.That(def.bbWeight, Is.EqualTo(0.0));
            Assert.That(cloned.bbWeight, Is.EqualTo(3.0));
            Assert.That(BitVecOps.IsEmpty(traits, ((nint[][])Get(allocator, "_connGraphAdjacencyMatrix"))[1]), Is.True);
            Assert.That(s_assertions, Is.Empty);
        }
        finally
        {
#if DEBUG
            s_captureCloneRoot = false;
            s_rootBeforeRewrite = null;
#endif
            JitTls.Compiler = previousCompiler;
        }
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void GuardSpecializationRetainsCorrectBranch(bool fastPath, bool inequality)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
#if DEBUG
        using var tls = new JitTls(&jitInfo);
#endif
        s_assertions.Clear();
        var previousCompiler = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
#if DEBUG
        compiler.info.compFullName = nameof(ObjectAllocatorCloneTransformationTests);
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
#endif
        compiler.info.compCompHnd = &jitInfo;
        compiler.lvaTable = new LclVarDsc[1];
        compiler.lvaCount = 1;
        compiler.lvaTable[0].Type = TYP_REF;
        compiler.fgPredsComputed = true;
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        JitTls.Compiler = compiler;

        try
        {
            var guard = BasicBlock.New(compiler, BBKinds.BBJ_COND);
            compiler.fgFirstBB = guard;
            compiler.fgLastBB = guard;
            var trueTarget = compiler.fgNewBBafter(BBKinds.BBJ_RETURN, guard, extendRegion: false);
            var falseTarget = compiler.fgNewBBafter(BBKinds.BBJ_RETURN, trueTarget, extendRegion: false);
            trueTarget.clearTryIndex();
            trueTarget.clearHndIndex();
            falseTarget.clearTryIndex();
            falseTarget.clearHndIndex();
            var trueEdge = compiler.fgAddRefPred(trueTarget, guard);
            var falseEdge = compiler.fgAddRefPred(falseTarget, guard);
            trueEdge.Likelihood = 0.4;
            falseEdge.Likelihood = 0.6;
            guard.bbWeight = 10.0;
            guard.SetCond(trueEdge, falseEdge);
            var address = compiler.gtNewLclvNode(TYP_REF, 0);
            var load = new GenTreeIndir(GT_IND, TYP_I_IMPL, address);
            var handle = compiler.gtNewIconHandleNode(1, GTF_ICON_CLASS_HDL);
            var relop = compiler.gtNewBinaryNode(inequality ? GT_NE : GT_EQ, TYP_INT, load, handle);
            compiler.fgInsertStmtAtEnd(guard,
                compiler.gtNewStmt(new GenTreeUnOp(GT_JTRUE, TYP_VOID, relop)));

            var allocator = new ObjectAllocator(compiler);
            var member = typeof(ObjectAllocator).GetMethod("SpecializeGuard",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Missing guard specialization.");
            try
            {
                _ = member.Invoke(allocator, [guard, fastPath]);
            }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }

            var keepTrue = fastPath == !inequality;
            Assert.That(guard.Kind, Is.EqualTo(BBKinds.BBJ_ALWAYS));
            Assert.That(guard.Target, Is.SameAs(keepTrue ? trueTarget : falseTarget));
            Assert.That(guard.LastStmt?.RootNode, Is.SameAs(relop));
            Assert.That(guard.TargetEdge.Likelihood, Is.EqualTo(1.0));
            Assert.That(s_assertions, Is.Empty);
        }
        finally
        {
            JitTls.Compiler = previousCompiler;
        }
    }

    private static void Set(object instance, string field, object value)
    {
        var member = GetTargetType(instance).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            ?? throw new InvalidOperationException($"Missing {field}.");
        member.SetValue(instance, value);
    }

    private static object Get(object instance, string field)
    {
        var member = GetTargetType(instance).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Missing {field}.");
        return member.GetValue(instance) ?? throw new InvalidOperationException($"Missing {field} value.");
    }

    private static void Call(object instance, string method)
    {
        var member = typeof(ObjectAllocator).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null, types: Type.EmptyTypes, modifiers: null)
            ?? throw new InvalidOperationException($"Missing {method}.");
        try
        {
            _ = member.Invoke(instance, null);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields)]
    private static Type GetTargetType(object instance)
    {
        if (instance is ObjectAllocator)
        {
            return typeof(ObjectAllocator);
        }
        return typeof(ObjectAllocator).GetNestedType(instance.GetType().Name, BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Unexpected clone transformation state type.");
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        s_assertions.Add(Marshal.PtrToStringUTF8((nint)expression) ?? "Unknown assertion");
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetArrayRank(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type)
    {
#if DEBUG
        // Class-name formatting runs after block cloning, before the local-rewrite visitor.
        if (s_captureCloneRoot)
        {
            s_rootBeforeRewrite = JitTls.Compiler?.fgFirstBB?.Next?.FirstStmt?.RootNode;
            s_rootTreeIdBeforeRewrite = s_rootBeforeRewrite?.TreeId ?? BAD_VAR_NUM;
        }
#endif
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* GetTypeArgument(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type, int index) => null;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static nint PrintClassName(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type, byte* buffer, nint length, nint* required)
    {
        var name = "Enumerator"u8;
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
    private static byte RunWithErrorTrap(ICorJitInfo* self, delegate* unmanaged[Cdecl]<void*, void> callback, void* state)
    {
        callback(state);
        return 1;
    }
}
