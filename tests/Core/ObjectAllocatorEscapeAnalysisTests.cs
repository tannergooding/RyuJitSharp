// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;
using BitVecOps = RyuJitSharp.BitSetOps<RyuJitSharp.BitVecTraits, RyuJitSharp.BitVecTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ObjectAllocatorEscapeAnalysisTests
{
    [TestCase(false, false)]
    [TestCase(true, true)]
    public static void PseudoRejectionRestartsEscapeClosure(bool hasCloneInfo, bool independentlyEscapes)
    {
        WithAllocator((compiler, allocator) => {
            PrepareWithPseudo(compiler, allocator);
            var pseudo = Get<int>(allocator, "_firstPseudoIndex");
            Call(allocator, "AddConnGraphEdgeIndex", pseudo, 1);
            Call(allocator, "AddConnGraphEdgeIndex", 1, 2);
            if (hasCloneInfo)
            {
                _ = AddCloneInfo(allocator, pseudo);
            }

            if (independentlyEscapes)
            {
                Call(allocator, "MarkIndexAsEscaping", 1);
            }

            var traits = Get<BitVecTraits>(allocator, "_bitVecTraits");
            var escaping = Get<nint[]>(allocator, "_escapingPointers");
            Call(allocator, "ComputeEscapingNodes", traits, escaping);

            Assert.That(BitVecOps.IsMember(traits, escaping, pseudo), Is.True);
            Assert.That(BitVecOps.IsMember(traits, escaping, 1), Is.True);
            Assert.That(BitVecOps.IsMember(traits, escaping, 2), Is.True);
            Assert.That(Get<int>(allocator, "_regionsToClone"), Is.Zero);
            Assert.That(compiler.Metrics.EnumeratorGDVProvisionalNoEscape, Is.Zero);
        });
    }

    [Test]
    public static void AllocationTempEscapesIndependentlyBeforeViabilityChecks()
    {
        WithAllocator((compiler, allocator) => {
            PrepareWithPseudo(compiler, allocator);
            var pseudo = Get<int>(allocator, "_firstPseudoIndex");
            Call(allocator, "AddConnGraphEdgeIndex", pseudo, 0);
            var clone = AddCloneInfo(allocator, pseudo);
            List<int> allocTemps = [1];
            Set(clone, "AllocTemps", allocTemps);
            Call(allocator, "MarkIndexAsEscaping", 1);

            var traits = Get<BitVecTraits>(allocator, "_bitVecTraits");
            var escaping = Get<nint[]>(allocator, "_escapingPointers");
            Call(allocator, "ComputeEscapingNodes", traits, escaping);

            Assert.That(BitVecOps.IsMember(traits, escaping, pseudo), Is.True);
            Assert.That(BitVecOps.IsMember(traits, escaping, 0), Is.True);
            Assert.That(compiler.Metrics.EnumeratorGDVProvisionalNoEscape, Is.Zero);
        });
    }

    [Test]
    public static void EmptyPseudoAdjacencyRejectsCloningWithoutNewLocalEscapes()
    {
        WithAllocator((compiler, allocator) => {
            PrepareWithPseudo(compiler, allocator);
            var pseudo = Get<int>(allocator, "_firstPseudoIndex");
            _ = AddCloneInfo(allocator, pseudo);
            var traits = Get<BitVecTraits>(allocator, "_bitVecTraits");
            var escaping = Get<nint[]>(allocator, "_escapingPointers");
            Call(allocator, "ComputeEscapingNodes", traits, escaping);

            Assert.That(BitVecOps.IsMember(traits, escaping, pseudo), Is.True);
            Assert.That(compiler.Metrics.EnumeratorGDVProvisionalNoEscape, Is.Zero);
        });
    }

    [Test]
    public static void EarlierPseudoRejectionIsVisibleToLaterCloneDecisions()
    {
        WithAllocator((compiler, allocator) => {
            PrepareWithPseudo(compiler, allocator, pseudoCount: 2);
            var first = Get<int>(allocator, "_firstPseudoIndex");
            var second = first + 1;
            Call(allocator, "AddConnGraphEdgeIndex", first, 0);
            Call(allocator, "AddConnGraphEdgeIndex", second, first);
            _ = AddCloneInfo(allocator, second);

            var traits = Get<BitVecTraits>(allocator, "_bitVecTraits");
            var escaping = Get<nint[]>(allocator, "_escapingPointers");
            Call(allocator, "ComputeEscapingNodes", traits, escaping);

            Assert.That(BitVecOps.IsMember(traits, escaping, first), Is.True);
            Assert.That(BitVecOps.IsMember(traits, escaping, second), Is.True);
            Assert.That(BitVecOps.IsMember(traits, escaping, 0), Is.True);
            Assert.That(Get<int>(allocator, "_regionsToClone"), Is.Zero);
        });
    }

    [Test]
    public static void CloneViabilityRejectsUnprofiledAllocationBeforeGraphWalk()
    {
        WithAllocator((compiler, allocator) => {
            PrepareWithPseudo(compiler, allocator);
            var pseudo = Get<int>(allocator, "_firstPseudoIndex");
            var clone = AddCloneInfo(allocator, pseudo);
#if DEBUG
            compiler.fgSafeBasicBlockCreation = true;
#endif
            var block = BasicBlock.New(compiler, BBKinds.BBJ_ALWAYS);
            Set(clone, "AllocBlock", block);

            Assert.That(Call<bool>(allocator, "CanClone", clone), Is.False);
            Assert.That(Get<bool>(clone, "CheckedCanClone"), Is.True);
            Assert.That(Get<bool>(clone, "CanClone"), Is.False);
            Assert.That(Call<bool>(allocator, "CanClone", clone), Is.False);
            Assert.That(compiler.Metrics.EnumeratorGDVCanCloneToEnsureNoEscape, Is.Zero);
        });
    }

    [Test]
    public static void CloneViabilityRetainsRpoExtentProfileAndOverlapDecision()
    {
        WithAllocator((compiler, allocator) => {
            var alloc = compiler.fgFirstBB ?? throw new InvalidOperationException("Missing allocation block.");
            var def = compiler.fgLastBB ?? throw new InvalidOperationException("Missing definition block.");

            alloc.SetFlags(BasicBlockFlags.BBF_PROF_WEIGHT);
            def.SetFlags(BasicBlockFlags.BBF_PROF_WEIGHT);
            alloc.bbWeight = 1.0;
            def.bbWeight = 1.0;
            var defStmt = compiler.gtNewStmt(compiler.gtNewIconNode(TYP_INT, 1));
            compiler.fgInsertStmtAtEnd(def, defStmt);
            compiler._dfsTree = compiler.fgComputeDfs();
            compiler._domTree = FlowGraphDominatorTree.Build(compiler._dfsTree);

            var pseudo = Get<int>(allocator, "_firstPseudoIndex");
            var clone = AddCloneInfo(allocator, pseudo);
            Set(clone, "Local", 0);
            Set(clone, "AllocBlock", alloc);
            Set(clone, "AllocTree", compiler.gtNewIconNode(TYP_INT, 2));
            Set(clone, "AppearanceMap", CreateAppearanceMap(def, defStmt));

            Assert.That(Call<bool>(allocator, "CanClone", clone), Is.True);
            Assert.That(Get<bool>(clone, "CanClone"), Is.True);
            Assert.That(Get<double>(clone, "ProfileScale"), Is.EqualTo(1.0));
            var cloneBlocks = Get<List<BasicBlock>>(clone, "BlocksToClone");
            Assert.That(cloneBlocks, Has.Count.EqualTo(1));
            Assert.That(cloneBlocks[0], Is.SameAs(def));
            Assert.That(compiler.Metrics.EnumeratorGDVCanCloneToEnsureNoEscape, Is.EqualTo(1));
            Assert.That(Call<bool>(allocator, "CanClone", clone), Is.True);
            Assert.That(compiler.Metrics.EnumeratorGDVCanCloneToEnsureNoEscape, Is.EqualTo(1));

            var other = AddCloneInfo(allocator, pseudo + 1);
            Set(other, "WillClone", true);
            Set(other, "Blocks", Get<nint[]>(clone, "Blocks"));
            Assert.That(Call<bool>(allocator, "CloneOverlaps", clone), Is.True);

            var traits = new BitVecTraits(compiler, compiler.compBasicBlockID);
            Set(other, "Blocks", BitVecOps.MakeEmpty(traits));
            Assert.That(Call<bool>(allocator, "CloneOverlaps", clone), Is.False);
        }, withGraph: true);
    }

    [Test]
    public static void CloneViabilityIncludesTryAndHandlerWithoutMutatingEh()
    {
        WithAllocator((compiler, allocator) => {
            var alloc = compiler.fgFirstBB ?? throw new InvalidOperationException("Missing allocation block.");
            var def = alloc.Next ?? throw new InvalidOperationException("Missing definition block.");
            var tryEntry = def.Next ?? throw new InvalidOperationException("Missing try entry.");
            var handler = tryEntry.Next ?? throw new InvalidOperationException("Missing handler.");

            alloc.SetFlags(BasicBlockFlags.BBF_PROF_WEIGHT);
            def.SetFlags(BasicBlockFlags.BBF_PROF_WEIGHT);
            alloc.bbWeight = 1.0;
            def.bbWeight = 1.0;
            tryEntry.TryIndex = 0;
            handler.HndIndex = 0;
            compiler.compHndBBtab =
            [
                new EHblkDsc
                {
                    ebdTryBeg = tryEntry,
                    ebdTryLast = tryEntry,
                    ebdHndBeg = handler,
                    ebdHndLast = handler,
                    ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                    ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                },
            ];
            compiler.compHndBBtabCount = 1;
            var defStmt = compiler.gtNewStmt(compiler.gtNewIconNode(TYP_INT, 1));
            var useStmt = compiler.gtNewStmt(compiler.gtNewLconNode(2));
            compiler.fgInsertStmtAtEnd(def, defStmt);
            compiler.fgInsertStmtAtEnd(tryEntry, useStmt);
            compiler._dfsTree = compiler.fgComputeDfs();
            compiler._domTree = FlowGraphDominatorTree.Build(compiler._dfsTree);

            var pseudo = Get<int>(allocator, "_firstPseudoIndex");
            var clone = AddCloneInfo(allocator, pseudo);
            Set(clone, "Local", 0);
            Set(clone, "AllocBlock", alloc);
            Set(clone, "AllocTree", compiler.gtNewIconNode(TYP_INT, 3));
            Set(clone, "AppearanceMap", CreateAppearanceMap(def, defStmt, tryEntry, useStmt));

            Assert.That(Call<bool>(allocator, "CanClone", clone), Is.True);
            var blocks = Get<List<BasicBlock>>(clone, "BlocksToClone");
            Assert.That(blocks, Does.Contain(def));
            Assert.That(blocks, Does.Contain(tryEntry));
            Assert.That(blocks, Does.Contain(handler));
            Assert.That(compiler.compHndBBtabCount, Is.EqualTo(1));
            Assert.That(compiler.compHndBBtab, Has.Length.EqualTo(1));
        }, withEhRegion: true);
    }

    [TestCase(0, false)]
    [TestCase(1, true)]
    public static void CloneProfitabilityCountsTreesAcrossBlocks(int sizeLimit, bool accepted)
    {
        WithAllocator((compiler, allocator) => {
            PrepareWithPseudo(compiler, allocator);
            var pseudo = Get<int>(allocator, "_firstPseudoIndex");
            var clone = AddCloneInfo(allocator, pseudo);
#if DEBUG
            compiler.fgSafeBasicBlockCreation = true;
#endif
            var block = BasicBlock.New(compiler, BBKinds.BBJ_RETURN);
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(compiler.gtNewIconNode(TYP_INT, 1)));
            Set(clone, "BlocksToClone", new System.Collections.Generic.List<BasicBlock> { block });

            var previousConfig = JitConfig;
            object config = previousConfig;
            SetConfig(ref config, "_jitCloneLoopsSizeLimit", sizeLimit);
            JitConfig = (JitConfigValues)config;

            try
            {
                Assert.That(Call<bool>(allocator, "ShouldClone", clone), Is.EqualTo(accepted));
            }
            finally
            {
                JitConfig = previousConfig;
            }
        });
    }

    private static void PrepareWithPseudo(Compiler compiler, ObjectAllocator allocator, int pseudoCount = 1)
    {
        for (var index = 0; index < pseudoCount; index++)
        {
            compiler.ImpEnumeratorGdvLocalMap.Add(compiler.gtNewIconNode(TYP_INT, index + 1), index);
        }

        Call(allocator, "PrepareAnalysis");
        var traits = Get<BitVecTraits>(allocator, "_bitVecTraits");
        var count = Get<int>(allocator, "_bvCount");
        var adjacency = new nint[count][];
        for (var index = 0; index < count; index++)
        {
            adjacency[index] = BitVecOps.MakeEmpty(traits);
        }

        Set(allocator, "_connGraphAdjacencyMatrix", adjacency);
        Set(allocator, "_escapingPointers", BitVecOps.MakeEmpty(traits));
        Set(allocator, "_numPseudos", pseudoCount);
    }

    private static object AddCloneInfo(ObjectAllocator allocator, int pseudo)
    {
        var type = typeof(ObjectAllocator).GetNestedType("CloneInfo", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing clone analysis state.");
        var clone = Activator.CreateInstance(type, nonPublic: true)
            ?? throw new InvalidOperationException("Could not construct clone analysis state.");
        Set(clone, "PseudoIndex", pseudo);
        var map = (IDictionary)Get<object>(allocator, "_cloneMap");
        map.Add(pseudo, clone);
        return clone;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2072",
        Justification = "The private collection types are concrete fields of ObjectAllocator and retained for this test.")]
    private static object CreateAppearanceMap(BasicBlock block, Statement statement,
        BasicBlock? useBlock = null, Statement? useStatement = null)
    {
        var owner = typeof(ObjectAllocator);
        var appearanceType = owner.GetNestedType("EnumeratorVarAppearance", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing enumerator appearances.");
        var variableType = owner.GetNestedType("EnumeratorVar", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing enumerator variable state.");
        var constructor = appearanceType.GetConstructor(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
            [typeof(BasicBlock), typeof(Statement), typeof(int), typeof(bool)], null)
            ?? throw new InvalidOperationException("Missing enumerator appearance constructor.");
        var appearance = constructor.Invoke([block, statement, 0, true]);
        var variable = Activator.CreateInstance(variableType, nonPublic: true)
            ?? throw new InvalidOperationException("Could not construct enumerator variable.");
        var appearancesField = variableType.GetField("Appearances")
            ?? throw new InvalidOperationException("Missing enumerator appearance collection.");
        var appearances = (IList)(Activator.CreateInstance(appearancesField.FieldType)
            ?? throw new InvalidOperationException("Could not construct appearance list."));
        _ = appearances.Add(appearance);
        if (useBlock is not null && useStatement is not null)
        {
            _ = appearances.Add(constructor.Invoke([useBlock, useStatement, 0, false]));
        }

        Set(variable, "Def", appearance);
        Set(variable, "Appearances", appearances);

        var mapType = typeof(ObjectAllocator).GetNestedType("CloneInfo", BindingFlags.NonPublic)?
            .GetField("AppearanceMap")?.FieldType
            ?? throw new InvalidOperationException("Missing enumerator appearance map.");
        var map = (IDictionary)(Activator.CreateInstance(mapType)
            ?? throw new InvalidOperationException("Could not construct appearance map."));
        map.Add(0, variable);
        return map;
    }

    private static void WithAllocator(Action<Compiler, ObjectAllocator> action, bool withGraph = false, bool withEhRegion = false)
    {
        var previousConfig = JitConfig;
        object config = previousConfig;
        SetConfig(ref config, "_jitObjectStackAllocationConditionalEscape", 1);
        JitConfig = (JitConfigValues)config;

#if DEBUG
        ICorJitInfo jitInfo = default;
        using var tls = new JitTls(&jitInfo);
#endif
        var previousCompiler = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
#if DEBUG
        compiler.info.compFullName = "ObjectAllocatorEscapeAnalysisTests";
#endif
        compiler.lvaTable = new LclVarDsc[3];
        compiler.lvaCount = 3;
        compiler.lvaTable[0].Type = TYP_REF;
        compiler.lvaTable[1].Type = TYP_REF;
        compiler.lvaTable[2].Type = TYP_REF;
        compiler.compHndBBtab = [];
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        JitTls.Compiler = compiler;

        try
        {
#if DEBUG
            compiler.fgSafeBasicBlockCreation = true;
#endif
            if (withGraph || withEhRegion)
            {
                var alloc = BasicBlock.New(compiler, BBKinds.BBJ_RETURN);
                var def = BasicBlock.New(compiler, BBKinds.BBJ_RETURN);
                alloc.Next = def;
                compiler.fgFirstBB = alloc;
                compiler.fgLastBB = def;
                var edge = new FlowEdge(alloc, def, def.bbPreds);
                def.bbPreds = edge;
                alloc.SetKindAndTargetEdge(BBKinds.BBJ_ALWAYS, edge);
                edge.Likelihood = 1.0;

                if (withEhRegion)
                {
                    var tryEntry = BasicBlock.New(compiler, BBKinds.BBJ_RETURN);
                    var handler = BasicBlock.New(compiler, BBKinds.BBJ_RETURN);
                    def.Next = tryEntry;
                    tryEntry.Next = handler;
                    compiler.fgLastBB = handler;
                    var tryEdge = new FlowEdge(def, tryEntry, tryEntry.bbPreds);
                    tryEntry.bbPreds = tryEdge;
                    def.SetKindAndTargetEdge(BBKinds.BBJ_ALWAYS, tryEdge);
                    tryEdge.Likelihood = 1.0;
                }
            }

            action(compiler, new ObjectAllocator(compiler));
        }
        finally
        {
            JitTls.Compiler = previousCompiler;
            JitConfig = previousConfig;
        }
    }

    private static void SetConfig(ref object config, string name, int value)
    {
        var field = typeof(JitConfigValues).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Missing {name}");
        field.SetValue(config, value);
    }

    private static T Get<T>(object target, string name)
    {
        var field = GetTargetType(target).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException($"Missing {name}");
        return (T)(field.GetValue(target) ?? throw new InvalidOperationException($"Uninitialized {name}"));
    }

    private static void Set(object target, string name, object value)
    {
        var field = GetTargetType(target).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException($"Missing {name}");
        field.SetValue(target, value);
    }

    [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields)]
    private static Type GetTargetType(object target)
    {
        if (target is ObjectAllocator)
        {
            return typeof(ObjectAllocator);
        }

        return typeof(ObjectAllocator).GetNestedType(target.GetType().Name, BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Unexpected clone-analysis state type.");
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
