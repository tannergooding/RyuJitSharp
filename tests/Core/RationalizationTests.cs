// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
            EnableIsa(compiler, ManagedInstructionSet.InstructionSet_AVX);
            EnableIsa(compiler, ManagedInstructionSet.InstructionSet_AVX2);
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

    [Test]
    public static void IdentityUserShuffleRemovesTheShuffleAndItsIndexVector()
    {
        Assume.That(Configuration.TARGET_XARCH);

        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = TYP_SIMD16;
            compiler.lvaTable[1].Type = TYP_SIMD16;
            var block = BasicBlock.New(compiler, BBKinds.BBJ_RETURN);
            compiler.fgFirstBB = compiler.fgLastBB = block;

            var source = compiler.gtNewLclvNode(TYP_SIMD16, 1);
            var indices = new GenTreeVecCon(TYP_SIMD16);
            for (var index = 0; index < TYP_SIMD16.Size; index++)
            {
                indices.SimdVal.u8[index] = (byte)index;
            }

            var shuffle = compiler.gtNewSimdHWIntrinsicNode(
                TYP_SIMD16, NI_Vector_Shuffle, TYP_UBYTE, TYP_SIMD16.Size, source, indices);
            shuffle.MethodHandle = (CORINFO_METHOD_STRUCT_*)1;
            var store = compiler.gtNewStoreLclVarNode(0, shuffle);
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(store));
            _ = compiler.fgSetBlockOrder();

            Rationalize(compiler);

            Assert.That(new List<GenTree>(block), Is.EqualTo(new GenTree[] { source, store }));
            Assert.That(store.Data, Is.SameAs(source));
            Assert.That(block.FirstStmt, Is.Null);
        }, withMethodSignature: true);
    }

    private static void EnableIsa(Compiler compiler, ManagedInstructionSet isa)
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

    private static void WithCompiler(Action<Compiler> action, bool withMethodSignature = false)
    {
        using var tls = TestConfiguration.CreateJitTls();
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
        compiler.SetMinOpts(true);
        TestConfiguration.EnableSafeBasicBlockCreation(compiler);
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        ICorJitInfo jitInfo = default;
        if (withMethodSignature)
        {
            vtable.Base.Base.getMethodSig = &GetMethodSignature;
            jitInfo.lpVtbl = &vtable;
            compiler.info.compCompHnd = &jitInfo;
        }
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

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void GetMethodSignature(
        ICorJitInfo* self, CORINFO_METHOD_STRUCT_* method, CORINFO_SIG_INFO* signature, CORINFO_CLASS_STRUCT_* parent)
    {
        *signature = new CORINFO_SIG_INFO { retType = CorInfoType.CORINFO_TYPE_VOID };
    }
}
