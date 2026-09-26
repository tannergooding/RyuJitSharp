// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.SpecialCodeKind;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;
using GenTreeStack = System.Collections.Generic.Stack<RyuJitSharp.GenTree>;
using fgWalkResult = RyuJitSharp.Compiler.fgWalkResult;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ArrayMorphingTests
{
    private static PhaseStatus MorphArrayOps(Compiler compiler) => compiler.fgMorphArrayOps();

    [TestCase(false, true)]
    [TestCase(true, false)]
    public static void PhaseAndBlockFlagsGateExpansion(bool methodFlag, bool blockFlag)
    {
        WithCompiler((compiler, block) => {
            if (methodFlag)
            {
                compiler.optMethodFlags |= OMF_HAS_MDARRAYREF;
            }
            if (blockFlag)
            {
                block.SetFlags(BBF_HAS_MDARRAYREF);
            }
            var element = NewElement(compiler, 2);
            var store = AddStatement(compiler, block, compiler.gtNewStoreLclVarNode(4, element));

            Assert.That(MorphArrayOps(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(store.RootNode.AsLclVarCommon().Data, Is.SameAs(element));
            Assert.That(compiler.lvaCount, Is.EqualTo(5));
        });
    }

    [TestCase(2)]
    [TestCase(3)]
    public static void ExpandsAllDimensionsWithSignedIndicesAndPointerSizedFinalOffset(int rank)
    {
        WithCompiler((compiler, block) => {
            compiler.optMethodFlags |= OMF_HAS_MDARRAYREF;
            block.SetFlags(BBF_HAS_MDARRAYREF);
            var element = NewElement(compiler, rank);
            var store = AddStatement(compiler, block, compiler.gtNewStoreLclVarNode(4, element));

            Assert.That(MorphArrayOps(compiler), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(store.RootNode.AsLclVarCommon().Data, Is.Not.SameAs(element));
            var nodes = Nodes(store.RootNode);
            Assert.That(nodes, Has.None.SameAs(element));
            Assert.That(nodes.FindAll(node => node.Oper is GT_BOUNDS_CHECK), Has.Count.EqualTo(rank));
            Assert.That(nodes.FindAll(node => node.Oper is GT_MDARR_LOWER_BOUND), Has.Count.EqualTo(rank));
            Assert.That(nodes.FindAll(node => node.Oper is GT_MDARR_LENGTH), Has.Count.EqualTo((2 * rank) - 1));
            var pointerCast = nodes.FindAll(node => node.Oper is GT_CAST && node.Type is TYP_I_IMPL);
            Assert.That(pointerCast, Has.Count.EqualTo(1));
            Assert.That(pointerCast[0].AsCast().CastOp.Type, Is.EqualTo(TYP_INT));
            Assert.That(pointerCast[0].Flags & GTF_UNSIGNED, Is.EqualTo(GTF_UNSIGNED));
            foreach (var node in nodes)
            {
                if (node.Oper is GT_MDARR_LOWER_BOUND or GT_MDARR_LENGTH)
                {
                    Assert.That(node.AsMDArr().Dim, Is.InRange(0, rank - 1));
                    Assert.That(node.AsMDArr().Rank, Is.EqualTo(rank));
                }
                if (node is GenTreeBoundsChk bounds)
                {
                    Assert.That(bounds.ThrowKind, Is.EqualTo(SCK_RNGCHK_FAIL));
                    Assert.That(bounds.Index.Type, Is.EqualTo(TYP_INT));
                    Assert.That(bounds.ArrayLength.Type, Is.EqualTo(TYP_INT));
                }
            }
            Assert.That(nodes.Exists(node => node.Oper is GT_ADD && node.Type is TYP_BYREF), Is.True);
            Assert.That(nodes.Exists(node => node.Oper is GT_MUL or GT_LSH
                && node.Type is TYP_I_IMPL), Is.True);
            Assert.That(nodes.Exists(node => node.Oper is GT_CNS_INT
                && node.Type is TYP_I_IMPL
                && node.AsIntCon().IconValue == Compiler.eeGetMDArrayDataOffset(rank)), Is.True);
            Assert.That(compiler.lvaCount, Is.EqualTo(5 + rank));
        });
    }

    [Test]
    public static void KeepsTempsDistinctWithinOneBlock()
    {
        WithCompiler((compiler, block) => {
            compiler.optMethodFlags |= OMF_HAS_MDARRAYREF;
            block.SetFlags(BBF_HAS_MDARRAYREF);
            var first = AddStatement(compiler, block,
                compiler.gtNewStoreLclVarNode(4, NewElement(compiler, 2)));
            var second = AddStatement(compiler, block,
                compiler.gtNewStoreLclVarNode(4, NewElement(compiler, 3)));

            Assert.That(MorphArrayOps(compiler), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.lvaCount, Is.EqualTo(10));
            Assert.That(Nodes(first.RootNode).Exists(node => node.Oper is GT_ARR_ELEM), Is.False);
            Assert.That(Nodes(second.RootNode).Exists(node => node.Oper is GT_ARR_ELEM), Is.False);
        });
    }

    [Test]
    public static void ReusesTempsOnlyAfterCompletingAFlaggedBlock()
    {
        WithCompiler((compiler, firstBlock) => {
            compiler.optMethodFlags |= OMF_HAS_MDARRAYREF;
            firstBlock.SetFlags(BBF_HAS_MDARRAYREF);
            var secondBlock = BasicBlock.New(compiler, BBJ_RETURN);
            secondBlock.SetFlags(BBF_HAS_MDARRAYREF);
            firstBlock.Next = secondBlock;
            secondBlock.Prev = firstBlock;
            compiler.fgLastBB = secondBlock;
            var first = AddStatement(compiler, firstBlock,
                compiler.gtNewStoreLclVarNode(4, NewElement(compiler, 2)));
            var second = AddStatement(compiler, secondBlock,
                compiler.gtNewStoreLclVarNode(4, NewElement(compiler, 3)));

            Assert.That(MorphArrayOps(compiler), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.lvaCount, Is.EqualTo(8));
            Assert.That(compiler.compCurBB, Is.SameAs(secondBlock));
            Assert.That(Nodes(first.RootNode).Exists(node => node.Oper is GT_ARR_ELEM), Is.False);
            Assert.That(Nodes(second.RootNode).Exists(node => node.Oper is GT_ARR_ELEM), Is.False);
        });
    }

    [Test]
    public static void CopiesEffectfulIndicesBeforeAnyArrayMetadataAccess()
    {
        WithCompiler((compiler, block) => {
            compiler.optMethodFlags |= OMF_HAS_MDARRAYREF;
            block.SetFlags(BBF_HAS_MDARRAYREF);
            compiler.lvaTable[3].Type = TYP_REF;
            var arrayStore = compiler.gtNewStoreLclVarNode(3, compiler.gtNewLclvNode(TYP_REF, 0));
            var array = compiler.gtNewCommaNode(TYP_REF, arrayStore,
                compiler.gtNewLclvNode(TYP_REF, 3));
            var firstStore = compiler.gtNewStoreLclVarNode(1, compiler.gtNewIconNode(TYP_INT, 5));
            var firstIndex = compiler.gtNewCommaNode(TYP_INT, firstStore,
                compiler.gtNewLclvNode(TYP_INT, 1));
            var earlierCheck = new GenTreeBoundsChk(
                compiler.gtNewLclvNode(TYP_INT, 2), compiler.gtNewIconNode(TYP_INT, 100),
                SCK_RNGCHK_FAIL);
            var secondIndex = compiler.gtNewCommaNode(TYP_INT, earlierCheck,
                compiler.gtNewLclvNode(TYP_INT, 2));
            var element = new GenTreeArrElem(TYP_BYREF, array, 8, [firstIndex, secondIndex]);
            var statement = AddStatement(compiler, block,
                compiler.gtNewStoreLclVarNode(4, element));

            Assert.That(MorphArrayOps(compiler), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            var nodes = Nodes(statement.RootNode);
            var firstArrayAccess = nodes.FindIndex(node => node.Oper is GT_MDARR_LOWER_BOUND);
            Assert.That(nodes.IndexOf(arrayStore), Is.LessThan(nodes.IndexOf(firstStore)));
            Assert.That(nodes.IndexOf(firstStore), Is.LessThan(nodes.IndexOf(earlierCheck)));
            Assert.That(nodes.IndexOf(earlierCheck), Is.LessThan(firstArrayAccess));
            Assert.That(nodes.FindAll(node => node.Oper is GT_BOUNDS_CHECK), Has.Count.EqualTo(3));
            Assert.That(nodes.Exists(node => node.Oper is GT_ARR_ELEM), Is.False);
            Assert.That(compiler.lvaCount, Is.EqualTo(10));
        });
    }

    [Test]
    public static void ElementScaleUsesUnsignedNativePointerWidth()
    {
        WithCompiler((compiler, block) => {
            compiler.optMethodFlags |= OMF_HAS_MDARRAYREF;
            block.SetFlags(BBF_HAS_MDARRAYREF);
            var indices = new GenTree[] {
                compiler.gtNewLclvNode(TYP_INT, 1),
                compiler.gtNewLclvNode(TYP_INT, 2),
            };
            var element = new GenTreeArrElem(TYP_BYREF,
                compiler.gtNewLclvNode(TYP_REF, 0), -2, indices);
            var statement = AddStatement(compiler, block,
                compiler.gtNewStoreLclVarNode(4, element));

            Assert.That(MorphArrayOps(compiler), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(Nodes(statement.RootNode).Exists(node => node.Oper is GT_CNS_INT
                && node.Type is TYP_I_IMPL
                && node.AsIntCon().IconValue == unchecked((nint)4294967294L)), Is.True);
        });
    }

    private static GenTreeArrElem NewElement(Compiler compiler, int rank)
    {
        var indices = new GenTree[rank];
        for (var i = 0; i < rank; i++)
        {
            indices[i] = compiler.gtNewLclvNode(TYP_INT, 1 + i);
        }
        return new GenTreeArrElem(TYP_BYREF, compiler.gtNewLclvNode(TYP_REF, 0), 4, indices);
    }

    private static Statement AddStatement(Compiler compiler, BasicBlock block, GenTree root)
    {
        var statement = compiler.fgNewStmtFromTree(root);
        compiler.fgInsertStmtAtEnd(block, statement);
        return statement;
    }

    private static List<GenTree> Nodes(GenTree tree)
    {
        List<GenTree> nodes = [];
        var visitor = new CollectVisitor(nodes);
        _ = visitor.WalkTree(ref tree, null);
        return nodes;
    }

    private struct CollectVisitor(List<GenTree> nodes) : IGenTreeVisitor<CollectVisitor>
    {
        private readonly GenTreeStack _ancestors = [];
        public static bool DoPostOrder => true;
        public static bool UseExecutionOrder => true;
        public readonly fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
            => Compiler.WALK_CONTINUE;
        public readonly fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user)
        {
            nodes.Add(use);
            return Compiler.WALK_CONTINUE;
        }
        public fgWalkResult WalkTree(ref GenTree use, GenTree? user)
            => IGenTreeVisitor<CollectVisitor>.WalkTree(ref this, ref use, user, _ancestors);
    }

    private static void WithCompiler(Action<Compiler, BasicBlock> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        CORINFO_METHOD_INFO methodInfo = default;
        compiler.info.compMethodInfo = &methodInfo;
        compiler.info.compIsStatic = true;
#if DEBUG
        compiler.info.compFullName = nameof(ArrayMorphingTests);
        compiler.fgSafeBasicBlockCreation = true;
#endif
        compiler.compHndBBtab = [];
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.lvaArg0Var = BAD_VAR_NUM;
        compiler.lvaCount = 5;
        compiler.lvaTable = new LclVarDsc[16];
        compiler.lvaTable[0].Type = TYP_REF;
        compiler.lvaTable[1].Type = TYP_INT;
        compiler.lvaTable[2].Type = TYP_INT;
        compiler.lvaTable[3].Type = TYP_INT;
        compiler.lvaTable[4].Type = TYP_BYREF;
        var block = BasicBlock.New(compiler, BBJ_RETURN);
        compiler.fgFirstBB = block;
        compiler.fgLastBB = block;
        JitTls.Compiler = compiler;
        try
        {
            action(compiler, block);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
