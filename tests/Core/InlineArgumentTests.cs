// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class InlineArgumentTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void UnusedStructArgumentsPreservePossibleNullFault(bool nonFaulting)
    {
        WithCompiler((compiler, inlinee, info) => {
            compiler.lvaTable[0].Type = TYP_BYREF;
            var address = compiler.gtNewCommaNode(TYP_BYREF,
                compiler.gtNewStoreLclVarNode(3, compiler.gtNewIconNode(TYP_INT, 1)),
                compiler.gtNewLclvNode(TYP_BYREF, 0));
            var layout = new ClassLayout(8);
            var value = compiler.gtNewBlkIndir(address, layout);

            if (nonFaulting)
            {
                value.Flags |= GenTreeFlags.GTF_IND_NONFAULTING;
            }

            var argInfo = new InlArgInfo {
                arg = new CallArg(NewCallArg.CreateForStruct(value, TYP_STRUCT, layout)),
                argHasSideEff = true,
            };
            var after = info.iciStmt ?? throw new InvalidOperationException("Missing call statement.");
            var block = info.iciBlock ?? throw new InvalidOperationException("Missing caller block.");
            Statement? added = null;
            compiler.fgInsertInlineeArgument(ref argInfo, block, ref after, ref added, default);
            Assert.That(added, Is.SameAs(after));
            Assert.That(after.RootNode.Oper, Is.EqualTo(nonFaulting ? GT_COMMA : GT_NULLCHECK));
            Assert.That(after.RootNode.AsUnOp().Op1, Is.SameAs(address));
            Assert.That((after.RootNode.Flags & GenTreeFlags.GTF_EXCEPT) != 0, Is.EqualTo(!nonFaulting));
            Assert.That(after.RootNode.Flags & GenTreeFlags.GTF_ASG, Is.EqualTo(GenTreeFlags.GTF_ASG));
        });
    }

    [TestCase("statement")]
    [TestCase("root")]
    [TestCase("return")]
    [TestCase("shared")]
    [TestCase("unused")]
    [TestCase("more-uses")]
    [TestCase("address")]
    [TestCase("starg")]
    public static void SingleUseArgumentsReplaceInlineeEdgesOrRetainTheirTemp(string shape)
    {
        WithCompiler((compiler, inlinee, info) => {
            var target = compiler.gtNewLclvNode(TYP_INT, 0);
            var value = new GenTreeOp(GT_ADD, TYP_INT, compiler.gtNewIconNode(TYP_INT, 3), compiler.gtNewIconNode(TYP_INT, 5));
            var argInfo = new InlArgInfo {
                arg = new CallArg(NewCallArg.CreateForPrimitive(value)),
                argHasTmp = true,
                argIsUsed = true,
                argTmpNum = 0,
                argBashTmpNode = target,
                argHasLdargaOp = shape == "address",
                argHasStargOp = shape == "starg",
            };

            if (shape == "more-uses")
            {
                target.Flags |= GenTreeFlags.GTF_VAR_MOREUSES;
            }

            var hasStatementUse = shape is not "return" and not "unused";
            var hasReturnUse = shape is "return" or "shared";
            var body = new GenTreeUnOp(GT_NEG, TYP_INT, target);
            var inlineeBlock = new BasicBlock(null, null);
            inlinee.fgFirstBB = inlineeBlock;
            inlinee.fgLastBB = inlineeBlock;

            if (hasStatementUse)
            {
                compiler.fgInsertStmtAtEnd(inlineeBlock, new Statement(shape == "root" ? target : body, 2));
            }

            var call = info.iciCall ?? throw new InvalidOperationException("Missing call.");
            var retExpr = new GenTreeRetExpr(TYP_INT, call) { SubstExpr = hasReturnUse ? target : null };
            info.inlineCandidateInfo.retExpr = retExpr;
            var after = info.iciStmt ?? throw new InvalidOperationException("Missing call statement.");
            var block = info.iciBlock ?? throw new InvalidOperationException("Missing caller block.");
            Statement? added = null;
            compiler.fgInsertInlineeArgument(ref argInfo, block, ref after, ref added, default);

            var needsTemp = shape is "more-uses" or "address" or "starg";
            Assert.That(added is not null, Is.EqualTo(needsTemp));

            if (needsTemp)
            {
                Assert.That(after.RootNode.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
                Assert.That(after.RootNode.AsLclVarCommon().Data, Is.SameAs(value));
            }
            else
            {
                Assert.That(after, Is.SameAs(info.iciStmt));
                Assert.That(argInfo.argBashTmpNode, Is.SameAs(value));
            }

            if (hasStatementUse)
            {
                var stmt = inlineeBlock.FirstStmt ?? throw new InvalidOperationException("Missing inlinee statement.");
                Assert.That(shape == "root" ? stmt.RootNode : body.Op1, Is.SameAs(needsTemp ? target : value));
            }

            if (hasReturnUse)
            {
                Assert.That(retExpr.SubstExpr, Is.SameAs(value));
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void InlinePreludeNullCheckFollowsArgumentSetup(bool alreadyDereferenced)
    {
        WithCompiler((compiler, inlinee, info) => {
            var call = info.iciCall ?? throw new InvalidOperationException("Missing call.");
            call.Flags |= GenTreeFlags.GTF_CALL_NULLCHECK;
            var receiver = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewZeroConNode(TYP_REF)).WithWellKnownArg(WellKnownArg.ThisPointer));
            var argument = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewIconNode(TYP_INT, 5)));
            info.argCnt = 2;
            info.thisDereferencedFirst = alreadyDereferenced;
            info.inlArgInfo[0] = new InlArgInfo { arg = receiver, argIsInvariant = true };
            info.lclVarInfo[0].lclTypeInfo = TYP_REF;
            info.inlArgInfo[1] = TempArg(argument, 1);

            var last = compiler.fgInlinePrependStatements(info);
            var setup = info.iciStmt?.NextStmt ?? throw new InvalidOperationException("Missing argument setup.");
            Assert.That(setup.RootNode.Oper, Is.EqualTo(GT_STORE_LCL_VAR));

            if (alreadyDereferenced)
            {
                Assert.That(last, Is.SameAs(setup));
            }
            else
            {
                Assert.That(setup.NextStmt, Is.SameAs(last));
                Assert.That(last.RootNode.Oper, Is.EqualTo(GT_NULLCHECK));
                Assert.That(last.RootNode.AsUnOp().Op1.IsIntegralConst(0), Is.True);
            }

            Assert.That(last.NextStmt, Is.Null);
        });
    }

    [TestCase(WellKnownArg.AsyncAwaiter)]
    [TestCase(WellKnownArg.AsyncResumedUse)]
    [TestCase(WellKnownArg.AsyncResumedDef)]
    [TestCase(WellKnownArg.VarArgsCookie)]
    public static void InlinePreludeSkipsPseudoArgsAndPreservesSetupOrder(WellKnownArg pseudoArg)
    {
        WithCompiler((compiler, inlinee, info) => {
            var call = info.iciCall ?? throw new InvalidOperationException("Missing call.");
            var first = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewIconNode(TYP_INT, 1)));
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewIconNode(TYP_INT, 99)).WithWellKnownArg(pseudoArg));
            var context = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewIconNode(TYP_INT, 7)).WithWellKnownArg(WellKnownArg.InstParam));
            var second = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewIconNode(TYP_INT, 2)));
            info.argCnt = 2;
            info.inlArgInfo[0] = TempArg(first, 0);
            info.inlArgInfo[1] = TempArg(second, 1);
            info.inlInstParamArgInfo = [TempArg(context, 2)];

            var last = compiler.fgInlinePrependStatements(info);
            var current = info.iciStmt;
            ReadOnlySpan<int> argumentOrder = [0, 2, 1];

            foreach (var local in argumentOrder)
            {
                current = current?.NextStmt ?? throw new InvalidOperationException("Missing argument setup.");
                Assert.That(current.RootNode.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
                Assert.That(current.RootNode.AsLclVarCommon().LclNum, Is.EqualTo(local));
            }

            Assert.That(current, Is.SameAs(last));
            Assert.That(last.NextStmt, Is.Null);
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void InlinePreludeInitializesLocalsAtRequiredSites(bool callerInitializes, bool inLoop)
    {
        WithCompiler((compiler, inlinee, info) => {
            compiler.info.compInitMem = callerInitializes;
            inlinee.info.compMethodInfo->locals.numArgs = 1;
            inlinee.info.compMethodInfo->options = CorInfoOptions.CORINFO_OPT_INIT_LOCALS;
            info.lclVarInfo[0].lclTypeInfo = TYP_INT;
            info.lclTmpNum[0] = 3;
            var block = info.iciBlock ?? throw new InvalidOperationException("Missing caller block.");

            if (inLoop)
            {
                block.SetFlags(BasicBlockFlags.BBF_BACKWARD_JUMP);
            }

            var last = compiler.fgInlinePrependStatements(info);

            if (inLoop || !callerInitializes)
            {
                Assert.That(last.RootNode.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
                Assert.That(last.RootNode.AsLclVarCommon().LclNum, Is.EqualTo(3));
                Assert.That(last.RootNode.AsLclVarCommon().Data.IsIntegralConst(0), Is.True);
            }
            else
            {
                Assert.That(last, Is.SameAs(info.iciStmt));
            }
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    public static void InlineEpilogueClearsUsedGcLocalsExceptAtTailcalls(bool implicitTail, bool unusedRef)
    {
        WithCompiler((compiler, inlinee, info) => {
            inlinee.info.compMethodInfo->locals.numArgs = 3;
            info.lclVarInfo[0].lclTypeInfo = TYP_REF;
            info.lclVarInfo[1].lclTypeInfo = TYP_INT;
            info.lclVarInfo[2].lclTypeInfo = TYP_BYREF;
            info.lclTmpNum[0] = unusedRef ? Globals.BAD_VAR_NUM : 0;
            info.lclTmpNum[1] = 1;
            info.lclTmpNum[2] = 2;
            info.numberOfGcRefLocals = 2;
            compiler.lvaTable[0].Type = TYP_REF;
            compiler.lvaTable[2].Type = TYP_BYREF;
            var call = info.iciCall ?? throw new InvalidOperationException("Missing call.");

            if (implicitTail)
            {
                call._callMoreFlags |= GenTreeCallFlags.GTF_CALL_M_IMPLICIT_TAILCALL;
            }

            var block = info.iciBlock ?? throw new InvalidOperationException("Missing caller block.");
            compiler.fgInlineAppendStatements(info, block, info.iciStmt);
            var current = info.iciStmt;

            if (!implicitTail)
            {
                ReadOnlySpan<int> gcLocals = unusedRef ? [2] : [0, 2];

                foreach (var local in gcLocals)
                {
                    current = current?.NextStmt ?? throw new InvalidOperationException("Missing GC-local cleanup.");
                    Assert.That(current.RootNode.AsLclVarCommon().LclNum, Is.EqualTo(local));
                    Assert.That(current.RootNode.AsLclVarCommon().Data.IsIntegralConst(0), Is.True);
                }
            }

            Assert.That(current?.NextStmt, Is.Null);
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public static void DemotionClearsOnlyImplicitByRefPromotionAnnotations(int promotionState)
    {
        WithCompiler((compiler, inlinee, info) => {
            compiler.info.compArgsCount = 1;
            ref var arg = ref compiler.lvaTable[0];
            arg.Type = TYP_BYREF;
            arg.lvIsParam = true;
            arg.IsImplicitByRef = true;
            arg.lvPromoted = promotionState == 1;
            arg.lvFieldLclStart = promotionState == 0 ? 0 : 2;
            compiler.lvaTable[1].lvPromoted = true;
            compiler.lvaTable[1].lvFieldLclStart = 7;
            ref var temp = ref compiler.lvaTable[2];
            temp.Type = TYP_STRUCT;
            temp.lvPromoted = true;
            temp.lvFieldLclStart = 3;
            temp.lvFieldCnt = 2;
            temp.SetAddressExposed(true, default);

            for (var i = 3; i < 5; i++)
            {
                compiler.lvaTable[i].lvParentLcl = 0;
                compiler.lvaTable[i].SetAddressExposed(true, default);
            }

            compiler.fgMarkDemotedImplicitByRefArgs();

            Assert.That(arg.lvPromoted, Is.False);
            Assert.That(arg.lvFieldLclStart, Is.Zero);
            Assert.That(arg.IsImplicitByRef, Is.True);
            Assert.That(compiler.lvaTable[1].lvPromoted, Is.True);
            Assert.That(compiler.lvaTable[1].lvFieldLclStart, Is.EqualTo(7));
            Assert.That(temp.IsAddressExposed, Is.EqualTo(promotionState != 2));
            Assert.That(temp.lvPromoted, Is.True);

#if DEBUG
            Assert.That(temp.lvUnusedStruct, Is.EqualTo(promotionState == 2));
            Assert.That(temp.lvUndoneStructPromotion, Is.EqualTo(promotionState == 2));

#endif
            for (var i = 3; i < 5; i++)
            {
                Assert.That(compiler.lvaTable[i].lvParentLcl, Is.EqualTo(promotionState == 2 ? 2 : 0));
                Assert.That(compiler.lvaTable[i].IsAddressExposed, Is.EqualTo(promotionState != 2));
            }
        });
    }

    private static InlArgInfo TempArg(CallArg arg, int local)
        => new() { arg = arg, argTmpNum = local, argHasTmp = true, argIsUsed = true };

    private static void WithCompiler(Action<Compiler, Compiler, InlineInfo> action)
    {
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var inlinee = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.InlineeCompiler = inlinee;
        compiler.lvaTable = new LclVarDsc[8];
        compiler.lvaCount = compiler.lvaTable.Length;

        for (var i = 0; i < compiler.lvaCount; i++)
        {
            compiler.lvaTable[i].Type = TYP_INT;
        }

        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        CORINFO_METHOD_INFO methodInfo = default;
        inlinee.info.compMethodInfo = &methodInfo;
        JitTls.Compiler = compiler;

        try
        {
            var call = compiler.gtNewCallNode(TYP_VOID, gtCallTypes.CT_USER_FUNC, null);
            var stmt = new Statement(call, 1);
            var block = new BasicBlock(null, null);
            compiler.fgInsertStmtAtEnd(block, stmt);
            var info = new InlineInfo {
                InlinerCompiler = compiler,
                InlineRoot = compiler,
                inlineCandidateInfo = new InlineCandidateInfo(),
                iciBlock = block,
                iciStmt = stmt,
                iciCall = call,
            };
            inlinee.impInlineInfo = info;
            action(compiler, inlinee, info);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
