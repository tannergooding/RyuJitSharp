// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;
using BitVecOps = RyuJitSharp.BitSetOps<RyuJitSharp.BitVecTraits, RyuJitSharp.BitVecTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ObjectAllocatorAllocationAnalysisTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void LocalCopyModelsAliasBeforeEscapeAndUseClosure(bool returnsCopy)
    {
        WithAllocator([TYP_REF, TYP_REF], (compiler, allocator, block) => {
            compiler.lvaTable[1].lvIsParam = true;
            var store = compiler.gtNewStoreLclVarNode(0, compiler.gtNewLclvNode(TYP_REF, 1));
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(store));

            var read = compiler.gtNewLclvNode(TYP_REF, 0);
            GenTree consumer = returnsCopy
                ? new GenTreeUnOp(GT_RETURN, TYP_REF, read)
                : new GenTreeIndir(GT_NULLCHECK, TYP_VOID, read);
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(consumer));

            Call(allocator, "DoAnalysis");

            var traits = Get<BitVecTraits>(allocator, "_bitVecTraits");
            var adjacency = Get<nint[][]>(allocator, "_connGraphAdjacencyMatrix");
            var unknown = Get<int>(allocator, "_unknownSourceIndex");
            Assert.That(BitVecOps.IsMember(traits, adjacency[0], 1), Is.True);
            Assert.That(BitVecOps.IsMember(traits, adjacency[1], unknown), Is.True);
            Assert.That(Call<bool>(allocator, "CanLclVarEscape", 0), Is.EqualTo(returnsCopy));
            Assert.That(Call<bool>(allocator, "CanLclVarEscape", 1), Is.EqualTo(returnsCopy));
            Assert.That(Call<bool>(allocator, "IsLclVarUsed", 0), Is.EqualTo(returnsCopy));
            Assert.That(Call<bool>(allocator, "IsLclVarUsed", 1), Is.EqualTo(returnsCopy));
            Assert.That(Get<bool>(allocator, "_analysisDone"), Is.True);
        });
    }

    [Test]
    public static void UnmodelledStoreConnectsToUnknownOnlyForNonNullValues()
    {
        WithAllocator([TYP_REF, TYP_REF], (compiler, allocator, block) => {
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(
                compiler.gtNewStoreLclVarNode(0, new GenTree(GT_CATCH_ARG, TYP_REF))));
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(
                compiler.gtNewStoreLclVarNode(1, compiler.gtNewIconNode(TYP_REF, 0))));

            Call(allocator, "DoAnalysis");

            var traits = Get<BitVecTraits>(allocator, "_bitVecTraits");
            var adjacency = Get<nint[][]>(allocator, "_connGraphAdjacencyMatrix");
            var unknown = Get<int>(allocator, "_unknownSourceIndex");
            Assert.That(BitVecOps.IsMember(traits, adjacency[0], unknown), Is.True);
            Assert.That(BitVecOps.IsEmpty(traits, adjacency[1]), Is.True);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void StructFieldStoreConnectsValueOrUnknownSource(bool modeledValue)
    {
        WithAllocator([TYP_STRUCT, TYP_REF], (compiler, allocator, block) => {
            var address = compiler.gtNewLclAddrNode(TYP_BYREF, 0, 0);
            var value = modeledValue
                ? compiler.gtNewLclvNode(TYP_REF, 1) : new GenTree(GT_CATCH_ARG, TYP_REF);
            var store = compiler.gtNewStoreIndNode(TYP_REF, address, value);
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(store));

            Call(allocator, "DoAnalysis");

            var traits = Get<BitVecTraits>(allocator, "_bitVecTraits");
            var adjacency = Get<nint[][]>(allocator, "_connGraphAdjacencyMatrix");
            var source = modeledValue ? 1 : Get<int>(allocator, "_unknownSourceIndex");
            Assert.That(BitVecOps.IsMember(traits, adjacency[0], source), Is.True);
            Assert.That(BitVecOps.IsEmpty(traits, adjacency[1]), Is.True);
            Assert.That(Call<bool>(allocator, "CanLclVarEscape", 0), Is.False);
        }, trackFields: true);
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void NullComparisonIsTrivialButOtherComparisonIsUsed(bool compareWithNull)
    {
        WithAllocator([TYP_REF], (compiler, allocator, block) => {
            var read = compiler.gtNewLclvNode(TYP_REF, 0);
            var other = compiler.gtNewIconNode(TYP_REF, compareWithNull ? 0 : 1);
            var comparison = compiler.gtNewBinaryNode(GT_EQ, TYP_INT, read, other);
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(comparison));

            Call(allocator, "DoAnalysis");

            Assert.That(Call<bool>(allocator, "CanLclVarEscape", 0), Is.False);
            Assert.That(Call<bool>(allocator, "IsLclVarUsed", 0), Is.EqualTo(!compareWithNull));
        });
    }

    private static void WithAllocator(var_types[] types, Action<Compiler, ObjectAllocator, BasicBlock> action,
        bool trackFields = false)
    {
        var previousConfig = JitConfig;
        object config = previousConfig;
        var field = typeof(JitConfigValues).GetField("_jitObjectStackAllocationConditionalEscape",
            BindingFlags.NonPublic | BindingFlags.Instance) ?? throw new InvalidOperationException("Missing config field.");
        field.SetValue(config, 0);
        field = typeof(JitConfigValues).GetField("_jitObjectStackAllocationTrackFields",
            BindingFlags.NonPublic | BindingFlags.Instance) ?? throw new InvalidOperationException("Missing field config.");
        field.SetValue(config, trackFields ? 1 : 0);
        JitConfig = (JitConfigValues)config;

#if DEBUG
        ICorJitInfo jitInfo = default;
        using var tls = new JitTls(&jitInfo);
#endif
        var previousCompiler = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
#if DEBUG
        compiler.info.compFullName = nameof(ObjectAllocatorAllocationAnalysisTests);
        compiler.fgSafeBasicBlockCreation = true;
#endif
        compiler.info.compRetBuffArg = BAD_VAR_NUM;
        compiler.lvaTable = new LclVarDsc[types.Length];
        compiler.lvaCount = types.Length;
        for (var index = 0; index < types.Length; index++)
        {
            compiler.lvaTable[index].Type = types[index];
        }

        var block = BasicBlock.New(compiler, BBKinds.BBJ_RETURN);
        compiler.fgFirstBB = block;
        compiler.fgLastBB = block;
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        JitTls.Compiler = compiler;

        try
        {
            var allocator = new ObjectAllocator(compiler);
            allocator.EnableObjectStackAllocation();
            compiler._dfsTree = compiler.fgComputeDfs();
            action(compiler, allocator, block);
        }
        finally
        {
            JitTls.Compiler = previousCompiler;
            JitConfig = previousConfig;
        }
    }

    private static T Get<T>(ObjectAllocator allocator, string fieldName)
    {
        var field = typeof(ObjectAllocator).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Missing {fieldName}");
        return (T)(field.GetValue(allocator) ?? throw new InvalidOperationException($"Uninitialized {fieldName}"));
    }

    private static void Call(ObjectAllocator allocator, string method, params object[] args)
    {
        _ = Invoke(allocator, method, args);
    }

    private static T Call<T>(ObjectAllocator allocator, string method, params object[] args)
    {
        return (T)(Invoke(allocator, method, args) ?? throw new InvalidOperationException($"No result from {method}"));
    }

    private static object? Invoke(ObjectAllocator allocator, string method, object[] args)
    {
        var member = typeof(ObjectAllocator).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Missing {method}");

        try
        {
            return member.Invoke(allocator, args);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }
}
