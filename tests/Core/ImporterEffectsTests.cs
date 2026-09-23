// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ImporterEffectsTests
{
    [TestCase(genTreeOps.GT_ADD, var_types.TYP_INT, false)]
    [TestCase(genTreeOps.GT_ADD, var_types.TYP_BYREF, true)]
    [TestCase(genTreeOps.GT_DIV, var_types.TYP_INT, true)]
    [TestCase(genTreeOps.GT_UDIV, var_types.TYP_INT, true)]
    [TestCase(genTreeOps.GT_MOD, var_types.TYP_INT, true)]
    [TestCase(genTreeOps.GT_UMOD, var_types.TYP_INT, true)]
    public static void OperationEffectsRetainOnlySupportedOrdering(genTreeOps oper, var_types type, bool supported)
    {
        WithCompiler(compiler => {
            var tree = compiler.gtNewBinaryNode(oper, type,
                compiler.gtNewIconNode(var_types.TYP_INT, 4), compiler.gtNewIconNode(var_types.TYP_INT, 2));
            tree.Flags |= GenTreeFlags.GTF_ORDER_SIDEEFF;
            Assert.That(tree.SupportsOrderingSideEffect(), Is.EqualTo(supported));

            compiler.gtUpdateNodeSideEffects(tree);

            Assert.That((tree.Flags & GenTreeFlags.GTF_ORDER_SIDEEFF) != 0, Is.EqualTo(supported));
        });
    }

    [Test]
    public static void OperandEffectsAreRestoredAfterResettingCallOperationEffects()
    {
        WithCompiler(compiler => {
            var argument = compiler.gtNewIndir(var_types.TYP_INT, compiler.gtNewIconNode(Globals.TYP_I_IMPL, 8),
                GenTreeFlags.GTF_IND_VOLATILE);
            var call = compiler.gtNewCallNode(var_types.TYP_VOID, gtCallTypes.CT_USER_FUNC, null);
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(argument));
            call.Flags |= GenTreeFlags.GTF_ORDER_SIDEEFF;

            compiler.gtUpdateNodeOperSideEffects(call);
            Assert.That((call.Flags & GenTreeFlags.GTF_ORDER_SIDEEFF) != 0, Is.False);

            compiler.gtUpdateNodeSideEffects(call);
            Assert.That((call.Flags & GenTreeFlags.GTF_ORDER_SIDEEFF) != 0, Is.True);
        });
    }

    [TestCase(gtCallTypes.CT_USER_FUNC, CorInfoHelpFunc.CORINFO_HELP_UNDEF, true)]
    [TestCase(gtCallTypes.CT_INDIRECT, CorInfoHelpFunc.CORINFO_HELP_UNDEF, true)]
    [TestCase(gtCallTypes.CT_HELPER, CorInfoHelpFunc.CORINFO_HELP_DIV, true)]
    [TestCase(gtCallTypes.CT_HELPER, CorInfoHelpFunc.CORINFO_HELP_GETCURRENTMANAGEDTHREADID, false)]
    public static void CallCreationIncludesItsOwnExceptions(gtCallTypes kind, CorInfoHelpFunc helper, bool mayThrow)
    {
        WithCompiler(compiler => {
            var handle = kind == gtCallTypes.CT_HELPER ? Compiler.eeFindHelper(helper) : null;
            var call = compiler.gtNewCallNode(var_types.TYP_INT, kind, handle);
            Assert.That((call.Flags & GenTreeFlags.GTF_EXCEPT) != 0, Is.EqualTo(mayThrow));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void IndirectCallsPreserveVolatilePointerEvaluation(bool isVolatile)
    {
        WithCompiler(compiler => {
            var address = compiler.gtNewIconNode(Globals.TYP_I_IMPL, 8);
            var pointer = compiler.gtNewIndir(Globals.TYP_I_IMPL, address,
                isVolatile ? GenTreeFlags.GTF_IND_VOLATILE : 0);
            Assert.That((pointer.Flags & GenTreeFlags.GTF_ORDER_SIDEEFF) != 0, Is.EqualTo(isVolatile));
            compiler.info.compMaxStack = 1;
            compiler.stackState.esStack = [new() { val = pointer }];
            compiler.stackState.esStackDepth = 1;
            var signature = new CORINFO_SIG_INFO {
                callConv = CorInfoCallConv.CORINFO_CALLCONV_DEFAULT,
                retType = CorInfoType.CORINFO_TYPE_VOID,
            };

            var call = compiler.impImportIndirectCall(signature);

            Assert.Multiple(() => {
                Assert.That(call.ControlExpr, Is.SameAs(pointer));
                Assert.That((call.Flags & GenTreeFlags.GTF_ORDER_SIDEEFF) != 0, Is.EqualTo(isVolatile));
                Assert.That((call.Flags & GenTreeFlags.GTF_EXCEPT) != 0, Is.True);
                Assert.That(compiler.stackState.esStackDepth, Is.Zero);
            });
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void OrderingEffectsSpillEarlierGlobalReads(bool localStore)
    {
        WithCompiler(compiler => {
            var read = compiler.gtNewIndir(var_types.TYP_INT, compiler.gtNewIconNode(Globals.TYP_I_IMPL, 8));
            compiler.stackState.esStack = [new() { val = read }];
            compiler.stackState.esStackDepth = 1;
            var effect = new GenTree(genTreeOps.GT_CATCH_ARG, var_types.TYP_REF) { Flags = GenTreeFlags.GTF_ORDER_SIDEEFF };
            if (localStore)
            {
                effect = compiler.gtNewStoreLclVarNode(0, effect);
            }
            var statement = new Statement(effect, 1);

            compiler.impAppendStmt(statement, Compiler.CHECK_SPILL_ALL);

            Assert.Multiple(() => {
                Assert.That(compiler.stackState.esStack[0].val.Oper, Is.EqualTo(genTreeOps.GT_LCL_VAR));
                Assert.That(statement.PrevStmt, Is.Not.Null);
                Assert.That(statement.PrevStmt?.RootNode.Oper, Is.EqualTo(genTreeOps.GT_STORE_LCL_VAR));
            });
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void LocalFieldAnnotationPreservesStoredGlobalReads(bool store, bool globalRead)
    {
        WithCompiler(compiler => {
            var address = new GenTreeFieldAddr(var_types.TYP_BYREF,
                compiler.gtNewLclVarAddrNode(var_types.TYP_BYREF, 1), null, 0);
            GenTree value = globalRead
                ? compiler.gtNewIndir(var_types.TYP_INT, compiler.gtNewIconNode(Globals.TYP_I_IMPL, 8))
                : compiler.gtNewIconNode(var_types.TYP_INT, 1);
            var indir = store
                ? compiler.gtNewStoreIndNode(var_types.TYP_INT, address, value)
                : compiler.gtNewIndir(var_types.TYP_INT, address);

            compiler.impAnnotateFieldIndir(indir);

            Assert.Multiple(() => {
                Assert.That((indir.Flags & GenTreeFlags.GTF_GLOB_REF) != 0, Is.EqualTo(store && globalRead));
                Assert.That((address.Flags & GenTreeFlags.GTF_FLD_DEREFERENCED) != 0, Is.True);
            });
        });
    }

    [Test]
    public static void InlineNullCheckCannotMovePastEHVisibleStore(
        [Values(0, 1, 2, 3)] int region,
        [Values("tree", "args", "statement", "stack")] string location)
    {
        WithCompiler(compiler => {
            var callSite = new BasicBlock(null, null);
            var handler = new BasicBlock(null, null);
            if (region == 1)
            {
                callSite.TryIndex = 0;
            }
            else if (region >= 2)
            {
                callSite.HndIndex = 0;
                callSite.Next = handler;
                compiler.compHndBBtab = [new EHblkDsc {
                    ebdHandlerType = region == 2 ? EHHandlerType.EH_HANDLER_FILTER : EHHandlerType.EH_HANDLER_CATCH,
                    ebdFilter = callSite,
                    ebdHndBeg = handler,
                    ebdHndLast = handler,
                }];
                compiler.compHndBBtabCount = 1;
            }

            compiler.impInlineInfo = new InlineInfo { InlineRoot = compiler, InlinerCompiler = compiler, iciBlock = callSite };
            compiler.opts.compFlags = Globals.CLFLG_INLINING;
            compiler.compCurBB = compiler.fgFirstBB = new BasicBlock(null, null);
            var store = compiler.gtNewCommaNode(var_types.TYP_INT,
                compiler.gtNewStoreLclVarNode(1, compiler.gtNewIconNode(var_types.TYP_INT, 1)),
                compiler.gtNewIconNode(var_types.TYP_INT, 0));
            GenTree? additionalTree = null;
            CallArgs args = default;
            switch (location)
            {
                case "tree":
                    additionalTree = store;
                    break;
                case "args":
                    _ = args.PushBack(NewCallArg.CreateForPrimitive(store));
                    break;
                case "statement":
                    compiler.impAppendStmt(new Statement(store, 1));
                    break;
                case "stack":
                    compiler.stackState.esStack = [new() { val = store }];
                    compiler.stackState.esStackDepth = 1;
                    break;
            }

            InlArgInfo[] arguments = [new() { argTmpNum = 0 }];
            var result = compiler.impInlineIsGuaranteedThisDerefBeforeAnySideEffects(
                additionalTree, args, compiler.gtNewLclVarNode(var_types.TYP_REF, 0), arguments);
            Assert.That(result, Is.EqualTo(region is 0 or 3));
        });
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.info = new Compiler.Info();
        compiler.lvaTable = [new LclVarDsc { Type = var_types.TYP_REF }, new LclVarDsc { Type = var_types.TYP_INT }];
        compiler.lvaCount = 2;
        compiler.stackState.esStack = [];
        compiler.compCurBB = new BasicBlock(null, null);
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
