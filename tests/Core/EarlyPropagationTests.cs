// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class EarlyPropagationTests
{
    [Test]
    public static void EarlyPropagationSkipsMethodsWithoutArraysOrNullChecks()
    {
        WithCompiler(compiler =>
            Assert.That(compiler.optEarlyProp(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING)));
    }

    [TestCase(0, true)]
    [TestCase(5, true)]
    [TestCase(6, false)]
    public static void ArrayLengthLookupFollowsCopiesOnlyWithinNativeRecursionLimit(int copies, bool found)
    {
        WithCompiler(compiler =>
        {
            var allocation = NewArrayAllocation(compiler, compiler.gtNewIconNode(TYP_INT, 7));
            for (var local = 0; local <= copies; local++)
            {
                GenTree value = local == 0 ? allocation : NewSsaUse(compiler, local - 1);
                NewSsaDefinition(compiler, local, value);
            }

            Assert.That(GetLength(compiler, copies), found
                ? Is.SameAs(allocation.Args.GetUserArgByIndex(1)?.Node)
                : Is.Null);
            Assert.That(GetLength(compiler, copies, reserved: true), Is.Null);
        });
    }

    [TestCase(10, 0, true)]
    [TestCase(10, 9, true)]
    [TestCase(10, 10, false)]
    [TestCase(10, -1, false)]
    [TestCase(-1, 0, false)]
    [TestCase(int.MaxValue, 0, false)]
    public static void EarlyPropagationRemovesOnlyProvedConstantBoundsChecks(int length, int index, bool removed)
    {
        WithCompiler(compiler =>
        {
            NewSsaDefinition(compiler, 0, NewArrayAllocation(compiler, compiler.gtNewIconNode(TYP_INT, length)));
            var arrayLength = new GenTreeArrLen(TYP_INT, NewSsaUse(compiler, 0), OFFSETOF__CORINFO_Array__length);
            var check = new GenTreeBoundsChk(compiler.gtNewIconNode(TYP_INT, index), arrayLength,
                SpecialCodeKind.SCK_RNGCHK_FAIL);
            var statement = compiler.gtNewStmt(check);
            compiler.compCurBB = new BasicBlock(null, null) { bbNum = 1 };
            compiler.compCurStmt = statement;
            compiler.gtSetStmtInfo(statement);
            compiler.fgSetStmtSeq(statement);
            var map = new Dictionary<int, GenTree>();

            var replacement = Invoke(compiler, "optEarlyPropRewriteTree", arrayLength, map);

            Assert.That(replacement is not null, Is.EqualTo(removed));
            Assert.That(check.Oper, Is.EqualTo(removed ? GT_NOP : GT_BOUNDS_CHECK));
        });
    }

    [TestCase(0, 1, true)]
    [TestCase(0, 2, false)]
    [TestCase(0x10000, 1, false)]
    public static void NullCheckCandidateRequiresMatchingSsaAndSmallOffset(int offset, int useSsa, bool found)
    {
        WithCompiler(compiler =>
        {
            var checkedAddress = NewSsaUse(compiler, 0);
            var nullCheck = compiler.gtNewNullCheck(checkedAddress);
            var local = NewSsaUse(compiler, 0);
            local.SsaNum = useSsa;
            if (useSsa != SsaConfig.FIRST_SSA_NUM)
            {
                while (!compiler.lvaTable[0].IsValidSsaNum(useSsa))
                {
                    _ = compiler.lvaTable[0].lvPerSsaData.AllocSsaNum();
                }
                compiler.lvaTable[0].GetPerSsaData(useSsa) = new LclSsaVarDsc(new BasicBlock(null, null));
            }
            var address = compiler.gtNewBinaryNode(GT_ADD, TYP_BYREF,
                local, compiler.gtNewIconNode(TYP_I_IMPL, offset));
            var indir = new GenTreeIndir(GT_IND, TYP_INT, address);
            var map = new Dictionary<int, GenTree> { [0] = nullCheck };

            Assert.That(Invoke(compiler, "optFindNullCheckToFold", indir, map),
                found ? Is.SameAs(nullCheck) : Is.Null);
        });
    }

    [Test]
    public static void EarlyPropagationFoldsSameBlockNullCheckIntoLaterIndirection()
    {
        WithCompiler(compiler =>
        {
            compiler.optMethodFlags |= OMF_HAS_NULLCHECK;
            var block = new BasicBlock(null, null) { bbNum = 1 };
            compiler.fgFirstBB = block;
            compiler.fgLastBB = block;
            compiler.fgSsaPassesCompleted = 1;
            var ssa = compiler.lvaTable[0].lvPerSsaData.AllocSsaNum();
            var checkedAddress = NewSsaUse(compiler, 0);
            checkedAddress.SsaNum = ssa;
            var nullCheck = compiler.gtNewNullCheck(checkedAddress);
            var readAddress = NewSsaUse(compiler, 0);
            readAddress.SsaNum = ssa;
            var indir = new GenTreeIndir(GT_IND, TYP_INT, readAddress);
            indir.Flags |= GTF_EXCEPT | GTF_GLOB_REF;
            foreach (var tree in new GenTree[] { nullCheck, indir })
            {
                var stmt = compiler.gtNewStmt(tree);
                compiler.fgInsertStmtAtEnd(block, stmt);
                compiler.gtSetStmtInfo(stmt);
                compiler.fgSetStmtSeq(stmt);
            }

            Assert.That(compiler.optEarlyProp(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That((nullCheck.Flags & GTF_EXCEPT) == 0, Is.True);
            Assert.That((indir.Flags & GTF_EXCEPT) != 0, Is.True);
            Assert.That((indir.Flags & GTF_IND_NONFAULTING) == 0, Is.True);
        });
    }

    [TestCase(4, 8, true)]
    [TestCase(0x10000, 8, false)]
    public static void NullCheckCandidateFollowsSameBlockCommaDefinition(int firstOffset, int secondOffset, bool found)
    {
        WithCompiler(compiler =>
        {
            var block = compiler.compCurBB = new BasicBlock(null, null);
            var checkedAddress = NewSsaUse(compiler, 0);
            var nullCheck = compiler.gtNewNullCheck(checkedAddress);
            var addition = compiler.gtNewBinaryNode(GT_ADD, TYP_BYREF,
                NewSsaUse(compiler, 0), compiler.gtNewIconNode(TYP_I_IMPL, firstOffset));
            var comma = compiler.gtNewBinaryNode(GT_COMMA, TYP_BYREF, nullCheck, addition);
            NewSsaDefinition(compiler, 1, comma);
            compiler.lvaTable[1].GetPerSsaData(SsaConfig.FIRST_SSA_NUM).Block = block;
            var read = compiler.gtNewBinaryNode(GT_ADD, TYP_BYREF,
                NewSsaUse(compiler, 1), compiler.gtNewIconNode(TYP_I_IMPL, secondOffset));
            var indir = new GenTreeIndir(GT_IND, TYP_INT, read);

            Assert.That(Invoke(compiler, "optFindNullCheckToFold", indir, new Dictionary<int, GenTree>()),
                found ? Is.SameAs(nullCheck) : Is.Null);
        });
    }

    [TestCase(GTF_CALL, false, true)]
    [TestCase(GTF_CALL, true, false)]
    [TestCase(GTF_EXCEPT, false, true)]
    [TestCase(GTF_EXCEPT, true, false)]
    [TestCase(GTF_ASG | GTF_GLOB_REF, false, true)]
    [TestCase(GTF_ASG | GTF_GLOB_REF, true, false)]
    [TestCase(GTF_ASG, false, true)]
    public static void MovingNullCheckRespectsOwnEffectsAndStatementSummary(
        GenTreeFlags flags, bool summary, bool allowed)
    {
        WithCompiler(compiler =>
        {
            var tree = compiler.gtNewBinaryNode(GT_ADD, TYP_INT,
                compiler.gtNewIconNode(TYP_INT, 1), compiler.gtNewIconNode(TYP_INT, 2));
            tree.Flags |= flags;
            Assert.That(Invoke(compiler, "optCanMoveNullCheckPastTree", tree, false, summary),
                Is.EqualTo(allowed));
        });
    }

    private static GenTreeCall NewArrayAllocation(Compiler compiler, GenTree length)
    {
        compiler.optMethodFlags |= OMF_HAS_NEWARRAY;
        var call = compiler.gtNewCallNode(TYP_REF, gtCallTypes.CT_HELPER,
            Compiler.eeFindHelper(CORINFO_HELP_NEWARR_1_DIRECT));
        _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewIconNode(TYP_I_IMPL, 0)));
        _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(length));
        return call;
    }

    private static void NewSsaDefinition(Compiler compiler, int local, GenTree value)
    {
        ref var descriptor = ref compiler.lvaTable[local];
        descriptor.lvInSsa = true;
        var ssa = descriptor.lvPerSsaData.AllocSsaNum();
        var store = compiler.gtNewStoreLclVarNode(local, value);
        store.SsaNum = ssa;
        descriptor.GetPerSsaData(ssa) = new LclSsaVarDsc(new BasicBlock(null, null), store);
    }

    private static GenTreeLclVar NewSsaUse(Compiler compiler, int local)
    {
        var use = compiler.gtNewLclvNode(TYP_REF, local);
        use.SsaNum = SsaConfig.FIRST_SSA_NUM;
        return use;
    }

    private static GenTree? GetLength(Compiler compiler, int local, bool reserved = false)
    {
        var kind = typeof(Compiler).GetNestedType("OptPropKind", BindingFlags.NonPublic) ??
            throw new AssertionException("Missing propagation kind.");
        return (GenTree?)Invoke(compiler, "optPropGetValue", local,
            reserved ? SsaConfig.RESERVED_SSA_NUM : SsaConfig.FIRST_SSA_NUM, Enum.ToObject(kind, 1));
    }

    private static object? Invoke(Compiler compiler, string method, params object[] args)
    {
        var target = typeof(Compiler).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new AssertionException($"Missing {method}.");
        return target.Invoke(compiler, args);
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.lvaTable = new LclVarDsc[8];
        compiler.lvaCount = compiler.lvaTable.Length;
        (typeof(Compiler).GetField("compMaxUncheckedOffsetForNullObject",
            BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new AssertionException("Missing unchecked null-object offset limit.")).SetValue(compiler, 0x1000);
        for (var i = 0; i < compiler.lvaCount; i++)
        {
            compiler.lvaTable[i] = new LclVarDsc { Type = TYP_REF };
        }
        compiler.fgNodeThreading = NodeThreading.AllTrees;
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
