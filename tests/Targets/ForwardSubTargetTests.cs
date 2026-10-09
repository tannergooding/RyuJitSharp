// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if !TARGET_64BIT
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ForwardSubTargetTests
{
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "fgForwardSub")]
    private static extern PhaseStatus ForwardSub(Compiler compiler);

    [TestCase(TYP_BYTE, false)]
    [TestCase(TYP_UBYTE, false)]
    [TestCase(TYP_SHORT, false)]
    [TestCase(TYP_USHORT, false)]
    [TestCase(TYP_INT, true)]
    [TestCase(TYP_UINT, true)]
    public static void MultiUseNativeIntCopyPreservesSmallLocalNormalization(
        var_types type, bool substitutes)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var previousConfig = JitConfig;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        CORINFO_METHOD_INFO methodInfo = default;
        compiler.info.compMethodInfo = &methodInfo;
        compiler.info.compIsStatic = true;
        compiler.info.compRetBuffArg = BAD_VAR_NUM;
#if DEBUG
        compiler.info.compFullName = nameof(ForwardSubTargetTests);
        compiler.fgSafeBasicBlockCreation = true;
#endif
        compiler.compHndBBtab = [];
        compiler.lvaArg0Var = BAD_VAR_NUM;
        compiler.lvaCount = 3;
        compiler.lvaTable = new LclVarDsc[compiler.lvaCount];
        compiler.lvaRefCountState = RefCountState.RCS_NORMAL;
        compiler.fgNodeThreading = NodeThreading.AllLocals;
        compiler.fgDidEarlyLiveness = true;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        JitConfig = new JitConfigValues();
        JitTls.Compiler = compiler;
        try
        {
            compiler.compRetTypeDesc = new ReturnTypeDesc();
            compiler.compRetTypeDesc.InitializeReturnType(compiler, TYP_VOID, null, CorInfoCallConvExtension.Managed);
            for (var i = 0; i < compiler.lvaCount; i++)
            {
                compiler.lvaTable[i].Type = TYP_INT;
                compiler.lvaTable[i].setLvRefCnt(3);
                compiler.lvaTable[i].setLvRefCntWtd(3);
            }
            compiler.lvaTable[0].Type = type;
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            compiler.fgFirstBB = block;
            compiler.fgLastBB = block;
            var expression = compiler.gtNewLclvNode(TYP_INT, 1);
            var definition = compiler.fgNewStmtFromTree(compiler.gtNewStoreLclVarNode(0, expression));
            compiler.fgInsertStmtAtEnd(block, definition);
            var first = compiler.gtNewLclvNode(TYP_INT, 0);
            var last = compiler.gtNewLclvNode(TYP_INT, 0);
            last.Flags |= GTF_VAR_DEATH;
            var sum = compiler.gtNewBinaryNode(GT_ADD, TYP_INT, first, last);
            var consumer = compiler.fgNewStmtFromTree(compiler.gtNewStoreLclVarNode(2, sum));
            compiler.fgInsertStmtAtEnd(block, consumer);

            Assert.That(ForwardSub(compiler), Is.EqualTo(substitutes
                ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING));
            if (substitutes)
            {
                Assert.That(block.FirstStmt, Is.SameAs(consumer));
                Assert.That(sum.Op1.Oper, Is.EqualTo(GT_LCL_VAR));
                Assert.That(sum.Op1.AsLclVar().LclNum, Is.EqualTo(1));
                Assert.That(sum.Op1, Is.Not.SameAs(expression));
                Assert.That(sum.Op2, Is.SameAs(expression));
            }
            else
            {
                Assert.That(block.FirstStmt, Is.SameAs(definition));
                Assert.That(definition.NextStmt, Is.SameAs(consumer));
                Assert.That(sum.Op1, Is.SameAs(first));
                Assert.That(sum.Op2, Is.SameAs(last));
            }
        }
        finally
        {
            JitTls.Compiler = previous;
            JitConfig = previousConfig;
        }
    }
}
#endif
