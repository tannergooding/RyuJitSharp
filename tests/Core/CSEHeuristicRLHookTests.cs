// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if DEBUG
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CSEHeuristicRLHookTests
{
    private static readonly int[] s_expectedFeatures =
    [
        1, 1, 1, 1, 1, 1, 0, 0, 10, 5, 3, 2, 195, 85, 2, 4, 3, 2, 1,
    ];

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "GetFeatures")]
    private static extern void ExtractFeatures(CSE_HeuristicRLHook heuristic, CSEdsc descriptor, int[] features);

    [TestCase(TYP_INT, 1)]
    [TestCase(TYP_LONG, 2)]
    [TestCase(TYP_FLOAT, 3)]
    [TestCase(TYP_DOUBLE, 4)]
    [TestCase(TYP_STRUCT, 5)]
    [TestCase(TYP_SIMD16, 6)]
    public static void CandidateTypeCodesMatchNativeFeatureEncoding(var_types type, int expected)
    {
        WithCompiler(compiler =>
        {
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            tree.Type = type;
            tree.SetCosts(1, 1);
            var descriptor = new CSEdsc(tree, compiler.gtNewStmt(tree), block);
            var features = new int[19];

            ExtractFeatures(new CSE_HeuristicRLHook(compiler), descriptor, features);

            Assert.That(features[0], Is.EqualTo(expected));
        });
    }

    [Test]
    public static void FeatureOrderIncludesWeightedCountsBlockSpreadAndIntegerRegisterPressure()
    {
        WithCompiler(compiler =>
        {
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            compiler.lvaCount = 3;
            compiler.lvaTable =
            [
                new LclVarDsc { Type = TYP_INT },
                new LclVarDsc { Type = TYP_FLOAT },
                new LclVarDsc { Type = TYP_INT, lvDoNotEnregister = true },
            ];
            compiler.lvaTrackedCount = 3;
            compiler.lvaTrackedToVarNum = [0, 1, 2];
            foreach (ref var local in compiler.lvaTable.AsSpan())
            {
                local.setLvRefCnt(1);
            }

            var first = BasicBlock.New(compiler, BBJ_RETURN);
            var last = BasicBlock.New(compiler, BBJ_RETURN);
            compiler.fgBBcount = 3;
            first.bbPostorderNum = 1;
            last.bbPostorderNum = 3;
            var tree = compiler.gtNewIconNode(TYP_INT, 42);
            tree.SetCosts(10, 5);
            var descriptor = new CSEdsc(tree, compiler.gtNewStmt(tree), first)
            {
                csdIndex = 1,
                csdDefCount = 2,
                csdUseCount = 3,
                csdDefWtCnt = 85,
                csdUseWtCnt = 195,
                csdLiveAcrossCall = true,
                csdIsSharedConst = true,
                defExcSetPromise = store.VNForIntCon(1),
                numDistinctLocals = 2,
                numLocalOccurrences = 4,
            };
            var later = compiler.gtNewIconNode(TYP_INT, 43);
            later.Flags |= GTF_MAKE_CSE;
            descriptor.csdTreeList.tslNext = new treeStmtLst(later, compiler.gtNewStmt(later), last);
            var features = new int[19];

            ExtractFeatures(new CSE_HeuristicRLHook(compiler), descriptor, features);

            Assert.That(features, Is.EqualTo(s_expectedFeatures));
        });
    }

    private static void WithCompiler(Action<Compiler> action)
    {
        using var tls = new JitTls(null);
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.compHndBBtab = [];
        compiler.lvaRefCountState = RefCountState.RCS_NORMAL;
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
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
}
#endif
