// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.GenTreeCallFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class TailCallMorphTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void RecursiveArgumentsAreSavedBeforeParametersAreOverwritten(bool late)
    {
        WithCompiler((compiler, entry, block) => {
            var call = NewRecursiveCall(compiler);
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewIconNode(TYP_I_IMPL, 99))
                .WithWellKnownArg(WellKnownArg.R2RIndirectionCell));
            var first = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewLclvNode(TYP_INT, 1)));
            var second = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewLclvNode(TYP_INT, 0)));
            Assert.That(Compiler.fgGetArgParameterLclNum(call, first), Is.Zero);
            Assert.That(Compiler.fgGetArgParameterLclNum(call, second), Is.EqualTo(1));
            if (late)
            {
                first.LateNode = first.EarlyNode;
                first.EarlyNode = null;
                second.LateNode = second.EarlyNode;
                second.EarlyNode = null;
                LateHead(ref call.Args) = second;
                second.LateNext = first;
            }

            var debugInfo = new DebugInfo(null, new ILLocation(17, 0));
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(call, debugInfo));
            entry.setBBProfileWeight(10);
            block.setBBProfileWeight(3);
            compiler.fgPgoConsistent = true;
            compiler.fgMorphRecursiveFastTailCallIntoLoop(block, call);

            var statements = block.Statements.ToArray();
            var stores = statements.Select(statement => statement.RootNode.AsLclVar()).ToArray();
            Assert.That(stores.Select(store => store.LclNum), Is.EqualTo(late ? (int[])[2, 3, 1, 0] : [2, 3, 0, 1]));
            Assert.That(stores[0].Data.AsLclVar().LclNum, Is.EqualTo(late ? 0 : 1));
            Assert.That(stores[1].Data.AsLclVar().LclNum, Is.EqualTo(late ? 1 : 0));
            Assert.That(stores[2].Data.AsLclVar().LclNum, Is.EqualTo(2));
            Assert.That(stores[3].Data.AsLclVar().LclNum, Is.EqualTo(3));
            Assert.That(statements.All(statement => statement.DebugInfo.Equals(debugInfo)), Is.True);
            Assert.That(compiler.lvaCount, Is.EqualTo(4));
            Assert.That(compiler.lvaTable[0].lvIsParam && compiler.lvaTable[1].lvIsParam, Is.True);
            Assert.That(block.Kind, Is.EqualTo(BBJ_ALWAYS));
            Assert.That(block.Target, Is.SameAs(entry));
            Assert.That(entry.bbRefs, Is.EqualTo(2));
            Assert.That(entry.bbWeight, Is.EqualTo(13));
            Assert.That(compiler.fgPgoConsistent, Is.False);
            Assert.That(block.HasFlag(BBF_HAS_JMP), Is.False);
        });
    }

    [Test]
    public static void EarlySetupPrecedesLateParameterAssignments()
    {
        WithCompiler((compiler, entry, block) => {
            compiler.lvaTable = [
                new LclVarDsc { Type = TYP_INT, lvIsParam = true },
                new LclVarDsc { Type = TYP_INT, lvIsParam = true },
                new LclVarDsc { Type = TYP_INT },
            ];
            compiler.lvaCount = 3;
            var call = NewRecursiveCall(compiler);
            var first = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewLclvNode(TYP_INT, 1)));
            var setup = compiler.gtNewStoreLclVarNode(2, first.Node);
            first.EarlyNode = setup;
            first.LateNode = compiler.gtNewLclvNode(TYP_INT, 2);
            var second = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewLclvNode(TYP_INT, 0)));
            second.LateNode = second.EarlyNode;
            second.EarlyNode = null;
            LateHead(ref call.Args) = first;
            first.LateNext = second;
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(call));
            compiler.fgMorphRecursiveFastTailCallIntoLoop(block, call);

            var stores = block.Statements.Select(statement => statement.RootNode.AsLclVar()).ToArray();
            Assert.That(stores.Select(store => store.LclNum), Is.EqualTo((int[])[2, 3, 0, 1]));
            Assert.That(stores[0], Is.SameAs(setup));
            Assert.That(stores.Select(store => store.Data.AsLclVar().LclNum), Is.EqualTo((int[])[1, 0, 2, 3]));
            Assert.That(compiler.lvaCount, Is.EqualTo(4));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void UnchangedParametersAndIndependentValuesAvoidExtraTemps(bool useLocal)
    {
        WithCompiler((compiler, entry, block) => {
            compiler.lvaTable = [
                new LclVarDsc { Type = TYP_INT, lvIsParam = true },
                new LclVarDsc { Type = TYP_INT, lvIsParam = true },
                new LclVarDsc { Type = TYP_INT },
            ];
            compiler.lvaCount = 3;
            var call = NewRecursiveCall(compiler);
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewLclvNode(TYP_INT, 0)));
            GenTree value = useLocal ? compiler.gtNewLclvNode(TYP_INT, 2) : compiler.gtNewIconNode(TYP_INT, 42);
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(value));
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(call));
            compiler.fgMorphRecursiveFastTailCallIntoLoop(block, call);

            var statements = block.Statements.ToArray();
            Assert.That(statements.Length, Is.EqualTo(1));
            Assert.That(statements[0].RootNode.AsLclVar().LclNum, Is.EqualTo(1));
            Assert.That(statements[0].RootNode.AsLclVar().Data, Is.SameAs(value));
            Assert.That(compiler.lvaCount, Is.EqualTo(3));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void LoopReentryRestoresWritableThisAndRequiredInitialization(bool initMem)
    {
        WithCompiler((compiler, entry, block) => {
            var gcLayout = new ClassLayout(16) { GCPtrCount = 1 };
            gcLayout._inlineGCPtrs[0] = CorInfoGCType.TYPE_GC_REF;
            compiler.lvaTable = [
                new LclVarDsc { Type = TYP_REF, lvIsParam = true },
                new LclVarDsc { Type = TYP_INT, lvIsParam = true },
                new LclVarDsc { Type = TYP_INT },
                new LclVarDsc { Type = TYP_REF },
                new LclVarDsc { Type = TYP_STRUCT, Layout = gcLayout },
                new LclVarDsc { Type = TYP_STRUCT, Layout = new ClassLayout(16) },
                new LclVarDsc { Type = TYP_STRUCT, Layout = gcLayout },
                new LclVarDsc { Type = TYP_INT, lvSuppressedZeroInit = true },
            ];
            compiler.lvaCount = compiler.lvaTable.Length;
            compiler.info.compIsStatic = false;
            compiler.info.compThisArg = 0;
            compiler.info.compLocalsCount = 3;
            compiler.info.compInitMem = initMem;
            compiler.compSuppressedZeroInit = true;
            compiler.lvaArg0Var = 3;
            compiler.lvaOutgoingArgSpaceVar = 6;
            var call = NewRecursiveCall(compiler);
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewLclvNode(TYP_REF, 0)));
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewLclvNode(TYP_INT, 1)));
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(call));
            compiler.fgMorphRecursiveFastTailCallIntoLoop(block, call);

            var stores = block.Statements.Select(statement => statement.RootNode.AsLclVar()).ToArray();
            Assert.That(stores.Select(store => store.LclNum), Is.EqualTo(initMem ? (int[])[3, 2, 4, 7] : [3, 7]));
            Assert.That(stores[0].Data.AsLclVar().LclNum, Is.Zero);
            Assert.That(stores[^1].Data.AsIntCon().IconValue, Is.EqualTo((nint)0));
            if (initMem)
            {
                Assert.That(stores[1].Data.AsIntCon().IconValue, Is.EqualTo((nint)0));
                Assert.That(stores[2].IsInitBlkOp, Is.True);
                Assert.That(stores[2].Type, Is.EqualTo(TYP_STRUCT));
            }
        });
    }

    private static GenTreeCall NewRecursiveCall(Compiler compiler)
    {
        var call = compiler.gtNewCallNode(TYP_VOID, gtCallTypes.CT_USER_FUNC, null);
        call._callMoreFlags |= GTF_CALL_M_TAILCALL | GTF_CALL_M_TAILCALL_TO_LOOP;
        return call;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_lateHead")]
    private static extern ref CallArg? LateHead(ref CallArgs arguments);

    private static void WithCompiler(Action<Compiler, BasicBlock, BasicBlock> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.info.compIsStatic = true;
        compiler.info.compArgsCount = 2;
        compiler.info.compLocalsCount = 2;
        compiler.lvaTable = [
            new LclVarDsc { Type = TYP_INT, lvIsParam = true },
            new LclVarDsc { Type = TYP_INT, lvIsParam = true },
        ];
        compiler.lvaCount = 2;
        compiler.lvaOutgoingArgSpaceVar = BAD_VAR_NUM;
        compiler.MethodHasRecursiveTailCall = true;
        compiler.fgGlobalMorph = true;
        compiler.fgPredsComputed = true;
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
#endif
        JitTls.Compiler = compiler;
        try
        {
            var scratch = BasicBlock.New(compiler, BBJ_ALWAYS);
            scratch.SetFlags(BBF_INTERNAL);
            var entry = BasicBlock.New(compiler, BBJ_RETURN);
            entry.bbRefs = 0;
            scratch.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(entry, scratch));
            compiler.fgFirstBB = scratch;
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            block.SetFlags(BBF_HAS_JMP);
            action(compiler, entry, block);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
