// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class GdvResolutionTests
{
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void ExactGuardResolutionPreservesEffectsAndRepairsLeafProfiles(bool inequality, bool swapped)
    {
        WithGuard(inequality, swapped, false, (compiler, block, statement, relop) => {
            var retained = inequality ? block.FalseEdge : block.TrueEdge;
            var removed = inequality ? block.TrueEdge : block.FalseEdge;
            Assert.That(compiler.fgResolveGDVs(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(block.Kind, Is.EqualTo(BBJ_ALWAYS));
            Assert.That(block.TargetEdge, Is.SameAs(retained));
            Assert.That(retained.Likelihood, Is.EqualTo(1));
            Assert.That(retained.DestinationBlock.bbWeight, Is.EqualTo(100));
            Assert.That(removed.DestinationBlock.bbWeight, Is.Zero);
            Assert.That(removed.DestinationBlock.bbPreds, Is.Null);
            Assert.That(statement.RootNode, Is.SameAs(relop));
            Assert.That(relop.Flags & GTF_EXCEPT, Is.Not.Zero);
            Assert.That(compiler.fgPgoConsistent, Is.True);
            Assert.That(compiler.fgResolveGDVs(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
        });
    }

    [TestCase("minopts")]
    [TestCase("no-gdv")]
    [TestCase("no-updated-types")]
    [TestCase("inexact")]
    [TestCase("multiple-definitions")]
    [TestCase("different-class")]
    public static void IneligibleMethodOrLocalPreservesTheGuard(string reason)
    {
        WithGuard(false, false, false, (compiler, block, statement, _) => {
            switch (reason)
            {
                case "minopts":
                {
                    break;
                }

                case "no-gdv":
                {
                    compiler.MethodHasGuardedDevirtualization = false;
                    break;
                }

                case "no-updated-types":
                {
                    compiler.hasUpdatedTypeLocals = false;
                    break;
                }

                case "inexact":
                {
                    compiler.lvaTable[0].lvClassIsExact = false;
                    break;
                }

                case "multiple-definitions":
                {
                    compiler.lvaTable[0].lvSingleDef = false;
                    break;
                }

                case "different-class":
                {
                    compiler.lvaTable[0].lvClassHnd = (CORINFO_CLASS_STRUCT_*)0x5678;
                    break;
                }

                default:
                {
                    throw new ArgumentException("Unknown eligibility condition.", nameof(reason));
                }
            }

            var root = statement.RootNode;
            Assert.That(compiler.fgResolveGDVs(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(block.Kind, Is.EqualTo(BBJ_COND));
            Assert.That(statement.RootNode, Is.SameAs(root));
        }, minopts: reason == "minopts");
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void SharedSuccessorLosesOnlyOneReference(bool inequality)
    {
        WithGuard(inequality, false, true, (compiler, block, statement, relop) => {
            var edge = block.TrueEdge;
            Assert.That(edge.DupCount, Is.EqualTo(2));
            Assert.That(compiler.fgResolveGDVs(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(block.TargetEdge, Is.SameAs(edge));
            Assert.That(edge.DupCount, Is.EqualTo(1));
            Assert.That(edge.DestinationBlock.CountOfInEdges, Is.EqualTo(1));
            Assert.That(edge.DestinationBlock.bbWeight, Is.EqualTo(100));
            Assert.That(statement.RootNode, Is.SameAs(relop));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void MissingProfileDataUsesTheExistingRepairPolicy(bool sourceProfile)
    {
        WithGuard(false, false, false, (compiler, block, _, _) => {
            block.TrueEdge.DestinationBlock.RemoveFlags(BBF_PROF_WEIGHT);
            if (!sourceProfile)
            {
                block.RemoveFlags(BBF_PROF_WEIGHT);
            }
            Assert.That(compiler.fgResolveGDVs(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.fgPgoConsistent, Is.EqualTo(!sourceProfile));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void UnrecognizedConditionIsNotResolved(bool removeStatement)
    {
        WithGuard(false, false, false, (compiler, block, statement, relop) => {
            if (removeStatement)
            {
                compiler.fgRemoveStmt(block, statement);
            }
            else
            {
                relop.AsOp().Op2 = compiler.gtNewIconNode(TYP_I_IMPL, 0x1234);
            }
            Assert.That(compiler.fgResolveGDVs(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(block.Kind, Is.EqualTo(BBJ_COND));
        });
    }

    private static void WithGuard(bool inequality, bool swapped, bool sharedTarget,
        Action<Compiler, BasicBlock, Statement, GenTree> action, bool minopts = false)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            compiler.MethodHasGuardedDevirtualization = true;
            compiler.hasUpdatedTypeLocals = true;
            compiler.lvaTable[0].Type = TYP_REF;
            compiler.lvaTable[0].lvClassHnd = (CORINFO_CLASS_STRUCT_*)0x1234;
            compiler.lvaTable[0].lvClassIsExact = true;
            compiler.lvaTable[0].lvSingleDef = true;
            var block = BasicBlock.New(compiler, BBJ_COND);
            var left = BasicBlock.New(compiler, BBJ_RETURN);
            var right = BasicBlock.New(compiler, BBJ_RETURN);
            left.bbRefs = 0;
            right.bbRefs = 0;
            block.Next = left;
            left.Next = right;
            compiler.fgFirstBB = block;
            compiler.fgLastBB = right;
            var trueEdge = compiler.fgAddRefPred(left, block);
            var falseEdge = compiler.fgAddRefPred(sharedTarget ? left : right, block);
            trueEdge.Likelihood = sharedTarget ? 1 : 0.4;
            falseEdge.Likelihood = sharedTarget ? 1 : 0.6;
            block.SetCond(trueEdge, falseEdge);
            block.setBBProfileWeight(100);
            left.setBBProfileWeight(sharedTarget ? 100 : 40);
            right.setBBProfileWeight(60);

            var load = new GenTreeIndir(GT_IND, TYP_I_IMPL, compiler.gtNewLclvNode(TYP_REF, 0)) { Flags = GTF_EXCEPT };
            var handle = compiler.gtNewIconHandleNode(0xABCD, GTF_ICON_CLASS_HDL);
            handle.CompileTimeHandle = 0x1234;
            var relop = compiler.gtNewBinaryNode(inequality ? GT_NE : GT_EQ, TYP_INT,
                swapped ? handle : load, swapped ? load : handle);
            var statement = compiler.gtNewStmt(new GenTreeUnOp(GT_JTRUE, TYP_VOID, relop));
            compiler.fgInsertStmtAtEnd(block, statement);
            action(compiler, block, statement, relop);
        }, minopts);
    }
}
