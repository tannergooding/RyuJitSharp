// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class RationalizationTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void RationalizationPreservesExecutionOrderAndRemovesHirOwners(bool reverse)
    {
        WithCompiler(compiler => {
            var block = BasicBlock.New(compiler, BBKinds.BBJ_RETURN);
            compiler.fgFirstBB = compiler.fgLastBB = block;
            var discarded = compiler.gtNewLclvNode(TYP_INT, 1);
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(discarded));
            var storedValue = compiler.gtNewIconNode(TYP_INT, 3);
            var store = compiler.gtNewStoreLclVarNode(0, storedValue);
            var left = compiler.gtNewIconNode(TYP_INT, 4);
            var right = compiler.gtNewIconNode(TYP_INT, 5);
            var sum = compiler.gtNewBinaryNode(GT_ADD, TYP_INT, left, right);
            sum.IsReverseOp = reverse;
            var comma = compiler.gtNewCommaNode(TYP_INT, store, sum);
            var root = compiler.gtNewUnaryNode(GT_RETURN, TYP_INT, comma);
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(root));
            _ = compiler.fgSetBlockOrder();

            Rationalize(compiler);

            GenTree[] expected = [storedValue, store, reverse ? right : left, reverse ? left : right, sum, root];
            Assert.That(new List<GenTree>(block), Is.EqualTo(expected));
            Assert.That(block.FirstStmt, Is.Null);
            Assert.That(block.IsLIR, Is.True);
            Assert.That(compiler.compRationalIRForm, Is.True);
            Assert.That(root.Op1, Is.SameAs(sum));
            Assert.That(sum.IsReverseOp, Is.False);
            for (var index = 0; index < expected.Length; index++)
            {
                Assert.That(expected[index].Prev, Is.SameAs(index == 0 ? null : expected[index - 1]));
                Assert.That(expected[index].Next, Is.SameAs(index + 1 == expected.Length ? null : expected[index + 1]));
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void UnusedCommaDeletesPureRangesButPreservesStores(bool hasStore)
    {
        WithCompiler(compiler => {
            var block = BasicBlock.New(compiler, BBKinds.BBJ_RETURN);
            compiler.fgFirstBB = compiler.fgLastBB = block;
            var value = compiler.gtNewIconNode(TYP_INT, 7);
            GenTree left = hasStore ? compiler.gtNewStoreLclVarNode(0, value) : value;
            var comma = compiler.gtNewCommaNode(TYP_INT, left, compiler.gtNewIconNode(TYP_INT, 9));
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(comma));
            _ = compiler.fgSetBlockOrder();

            Rationalize(compiler);

            GenTree[] expected = hasStore ? [value, left] : [];
            Assert.That(new List<GenTree>(block), Is.EqualTo(expected));
            Assert.That(block.FirstStmt, Is.Null);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ConstantShuffleSelectsOneOrBothSourceLanes(bool mixedLanes)
    {
        WithCompiler(compiler => {
            EnableIsa(compiler, CORINFO_InstructionSet.InstructionSet_AVX);
            EnableIsa(compiler, CORINFO_InstructionSet.InstructionSet_AVX2);
            var values = compiler.gtNewVconNode(TYP_SIMD32);
            var indices = compiler.gtNewVconNode(TYP_SIMD32);
            for (var index = 0; index < 32; index++)
            {
                values.SimdVal.u8[index] = (byte)index;
                indices.SimdVal.u8[index] = (byte)(index ^ ((!mixedLanes || ((index & 1) != 0)) ? 16 : 0));
            }

            var result = compiler.gtNewSimdShuffleNode(TYP_SIMD32, values, indices, TYP_UBYTE, 32, false).AsHWIntrinsic();
            Assert.That(result.HWIntrinsicId, Is.EqualTo(mixedLanes ? NI_AVX2_BlendVariable : NI_AVX2_Permute2x128));
            if (mixedLanes)
            {
                var mask = result.GetOp(3).AsVecCon();
                for (var index = 0; index < 32; index++)
                {
                    Assert.That(mask.SimdVal.u8[index], Is.EqualTo((index & 1) != 0 ? 255 : 0));
                }
            }
            else
            {
                Assert.That(result.GetOp(3).AsIntCon().IconValue, Is.EqualTo((nint)1));
            }
        });
    }

    private static void EnableIsa(Compiler compiler, CORINFO_InstructionSet isa)
    {
        compiler.opts.compSupportsISA.AddInstructionSet(isa);
        compiler.opts.compSupportsISAReported.AddInstructionSet(isa);
        compiler.opts.compSupportsISAExactly.AddInstructionSet(isa);
    }
    private static void Rationalize(Compiler compiler)
    {
        compiler.mostRecentlyActivePhase = Phases.PHASE_RATIONALIZE;
        var method = typeof(Rationalizer).GetMethod("DoPhase", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Rationalization phase entry point not found.");
        Assert.That(method.Invoke(new Rationalizer(compiler), null), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var previousConfig = JitConfig;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.compHndBBtab = [];
        compiler.lvaTable = new LclVarDsc[2];
        compiler.lvaCount = 2;
        compiler.lvaTable[0].Type = TYP_INT;
        compiler.lvaTable[1].Type = TYP_INT;
        compiler.fgNodeThreading = NodeThreading.AllTrees;
        compiler.codeGen = new CodeGen(compiler);
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
#endif
        JitConfig = new JitConfigValues();
        JitTls.Compiler = compiler;
        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
            JitConfig = previousConfig;
        }
    }
}
