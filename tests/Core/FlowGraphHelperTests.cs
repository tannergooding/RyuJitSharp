// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class FlowGraphHelperTests
{
    [TestCase(1, 1)]
    [TestCase(1, 3)]
    [TestCase(3, 1)]
    public static void IRMeasurementCountsAllStatements(int blockCount, int statementsPerBlock)
    {
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags jitFlags = default;
        compiler.opts.jitFlags = &jitFlags;
        JitTls.Compiler = compiler;
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
#endif

        try
        {
            BasicBlock? last = null;
            for (var i = 0; i < blockCount; i++)
            {
                var block = BasicBlock.New(compiler, BBKinds.BBJ_RETURN);
                if (last is null)
                {
                    compiler.fgFirstBB = block;
                }
                else
                {
                    last.Next = block;
                }
                last = block;

                for (var j = 0; j < statementsPerBlock; j++)
                {
                    var statement = new Statement(compiler.gtNewNothingNode(), (i * statementsPerBlock) + j + 1);
                    compiler.fgInsertStmtAtEnd(block, statement);
                }
            }
            compiler.fgLastBB = last;

            Assert.That(compiler.fgMeasureIR(), Is.EqualTo(blockCount * statementsPerBlock));
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [TestCase(false, 0)]
    [TestCase(false, 8)]
    [TestCase(false, -8)]
    [TestCase(true, 0)]
    [TestCase(true, 8)]
    [TestCase(true, -8)]
    public static void StackAddressesRemainNonHeapAfterPeeling(bool isReturnBuffer, int offset)
    {
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags jitFlags = default;
        compiler.opts.jitFlags = &jitFlags;
        compiler.lvaTable = [new LclVarDsc { Type = var_types.TYP_INT }];
        compiler.lvaCount = 1;
        compiler.info.compRetBuffArg = isReturnBuffer ? 0 : -1;
        JitTls.Compiler = compiler;

        try
        {
            GenTree address = isReturnBuffer
                ? compiler.gtNewLclVarNode(var_types.TYP_BYREF, 0)
                : compiler.gtNewLclVarAddrNode(var_types.TYP_BYREF, 0);
            if (offset != 0)
            {
                address = compiler.gtNewBinaryNode(genTreeOps.GT_ADD, var_types.TYP_BYREF,
                    address, compiler.gtNewIconNode(Globals.TYP_I_IMPL, offset));
            }

            Assert.That(compiler.fgAddrCouldBeHeap(address), Is.False);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [TestCase(CorInfoHelpFunc.CORINFO_HELP_GETDYNAMIC_NONGCTHREADSTATIC_BASE_NOCTOR_OPTIMIZED2, false)]
    [TestCase(CorInfoHelpFunc.CORINFO_HELP_GETDYNAMIC_NONGCTHREADSTATIC_BASE_NOCTOR_OPTIMIZED2, true)]
    [TestCase(CorInfoHelpFunc.CORINFO_HELP_GETDYNAMIC_NONGCTHREADSTATIC_BASE_NOCTOR_OPTIMIZED2_NOJITOPT, false)]
    [TestCase(CorInfoHelpFunc.CORINFO_HELP_GETDYNAMIC_NONGCTHREADSTATIC_BASE_NOCTOR_OPTIMIZED2_NOJITOPT, true)]
    public static void PinnedStaticHelperResultTracksAsyncSuspensions(CorInfoHelpFunc helper, bool isAsync)
    {
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags jitFlags = default;
        compiler.opts.jitFlags = &jitFlags;
        if (isAsync)
        {
            jitFlags.Set(JitFlags.JIT_FLAG_ASYNC);
        }
        JitTls.Compiler = compiler;

        try
        {
            var call = compiler.fgGetStaticsCCtorHelper(null, helper, 7);
            var argument = call.Args.GetArgByIndex(0) ?? throw new InvalidOperationException("Missing static block index argument.");
            Assert.Multiple(() => {
                Assert.That(compiler.compIsAsync, Is.EqualTo(isAsync));
                Assert.That(call.Type, Is.EqualTo(isAsync ? var_types.TYP_BYREF : Globals.TYP_I_IMPL));
                Assert.That((call.Flags & GenTreeFlags.GTF_CALL_HOISTABLE) != 0, Is.True);
                Assert.That(argument.Node.AsIntCon().IconVal, Is.EqualTo((nint)7));
            });
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [TestCase(Compiler.AcdKeyDesignator.KD_TRY, (ushort)1, 1)]
    [TestCase(Compiler.AcdKeyDesignator.KD_HND, (ushort)2, 0x40000002)]
    [TestCase(Compiler.AcdKeyDesignator.KD_FLT, (ushort)3, int.MinValue + 3)]
    public static void ThrowHelperKeyDecodesRegion(Compiler.AcdKeyDesignator designator, ushort index, int data)
    {
        var key = new Compiler.AddCodeDscKey(new Compiler.AddCodeDsc {
            acdKind = SpecialCodeKind.SCK_RNGCHK_FAIL,
            acdKeyDsg = designator,
            acdTryIndex = index,
            acdHndIndex = index
        });

        Assert.Multiple(() => {
            Assert.That(key.Data, Is.EqualTo(data));
            Assert.That(key.Designator, Is.EqualTo(designator));
            Assert.That(key.RegionIndex, Is.EqualTo(index - 1));
            Assert.That(default(Compiler.AddCodeDscKey).Designator, Is.EqualTo(Compiler.AcdKeyDesignator.KD_NONE));
        });
    }
}
