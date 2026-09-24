// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.GenTreeCallFlags;
using static RyuJitSharp.GenTreeFlags;
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

    [TestCase(TYP_VOID)]
    [TestCase(TYP_BYTE)]
    public static void DispatcherPreservesReturnStorageAndArgumentOrder(var_types returnType)
    {
        WithCompiler((compiler, entry, block) => {
            var call = compiler.gtNewCallNode(returnType.ActualType, gtCallTypes.CT_USER_FUNC, null);
            call._returnType = returnType;
            MorphStatement(compiler) = compiler.gtNewStmt(call);
            var result = compiler.fgCreateCallDispatcherAndGetResult(call,
                (CORINFO_METHOD_STRUCT_*)0x1234, (CORINFO_METHOD_STRUCT_*)0x5678);
            var dispatcher = returnType is TYP_VOID ? result.AsCall() : result.AsOp().Op1.AsCall();
            var arguments = dispatcher.Args.Args.ToArray();

            Assert.That((nuint)dispatcher._callMethHnd, Is.EqualTo((nuint)0x5678));
            Assert.That(arguments.Length, Is.EqualTo(3));
            Assert.That(arguments[0].Node.AsLclVarCommon().LclNum, Is.EqualTo(compiler.lvaRetAddrVar));
            Assert.That((nuint)arguments[1].Node.AsFptrVal().FptrMethod, Is.EqualTo((nuint)0x1234));
            Assert.That(compiler.lvaGetDesc(compiler.lvaRetAddrVar).Type, Is.EqualTo(TYP_I_IMPL));
            Assert.That(compiler.lvaGetDesc(compiler.lvaRetAddrVar).IsAddressExposed, Is.True);
            if (returnType is TYP_VOID)
            {
                Assert.That(arguments[2].Node.AsIntCon().IconValue, Is.EqualTo((nint)0));
            }
            else
            {
                var returnLocal = arguments[2].Node.AsLclVarCommon().LclNum;
                Assert.That(compiler.lvaGetDesc(returnLocal).Type, Is.EqualTo(TYP_BYTE));
                Assert.That(compiler.lvaGetDesc(returnLocal).IsAddressExposed, Is.True);
                Assert.That(result.AsOp().Op2.AsLclVar().LclNum, Is.EqualTo(returnLocal));
                Assert.That(result.Type, Is.EqualTo(TYP_INT));
            }

            var previousReturnAddress = compiler.lvaRetAddrVar;
            _ = compiler.fgCreateCallDispatcherAndGetResult(call, null, null);
            Assert.That(compiler.lvaRetAddrVar, Is.EqualTo(previousReturnAddress));
        });
    }

    [Test]
    public static void DispatcherTransfersIncomingReturnBufferWithoutAllocatingResultStorage()
    {
        WithCompiler((compiler, entry, block) => {
            compiler.info.compRetBuffArg = 0;
            compiler.lvaTable[0].Type = TYP_BYREF;
            var call = compiler.gtNewCallNode(TYP_BYREF, gtCallTypes.CT_USER_FUNC, null);
            var buffer = compiler.gtNewLclvNode(TYP_BYREF, 0);
            var bufferArgument = call.Args.PushBack(NewCallArg.CreateForPrimitive(buffer).WithWellKnownArg(WellKnownArg.RetBuffer));
            MorphStatement(compiler) = compiler.gtNewStmt(call);
            var result = compiler.fgCreateCallDispatcherAndGetResult(call, null, null);
            var arguments = result.AsOp().Op1.AsCall().Args.Args.ToArray();

            Assert.That(arguments[2].Node, Is.SameAs(buffer));
            Assert.That(result.AsOp().Op2, Is.Not.SameAs(buffer));
            Assert.That(result.AsOp().Op2.AsLclVar().LclNum, Is.Zero);
            Assert.That(compiler.lvaCount, Is.EqualTo(3));
            Assert.That(call.Args.HasRetBuffer, Is.True);
            call.Args.Remove(bufferArgument);
            Assert.That(call.Args.HasRetBuffer, Is.False);
            Assert.That(call.Args.IsEmpty, Is.True);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void JitHelperMovesReceiverAndBuildsStackSuffix(bool sideEffects)
    {
        WithCompiler((compiler, entry, block) => {
            compiler.lvaParameterStackSize = 3 * REGSIZE_BYTES;
            compiler.lvaTable[0].Type = TYP_REF;
            var call = compiler.gtNewCallNode(TYP_VOID, gtCallTypes.CT_USER_FUNC, null);
            call.Flags |= GTF_CALL_NULLCHECK | GTF_CALL_POP_ARGS;
            GenTree receiver = sideEffects
                ? compiler.gtNewCallNode(TYP_REF, gtCallTypes.CT_USER_FUNC, null)
                : compiler.gtNewLclvNode(TYP_REF, 0);
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(receiver).WithWellKnownArg(WellKnownArg.ThisPointer));
            var userArgument = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewIconNode(TYP_INT, 42)));
            compiler.fgMorphTailCallViaJitHelper(call);

            var arguments = call.Args.Args.ToArray();
            Assert.That(arguments.Length, Is.EqualTo(6));
            Assert.That(arguments[1], Is.SameAs(userArgument));
            Assert.That(arguments[2..].Select(argument => argument.WellKnownArg),
                Is.All.EqualTo(WellKnownArg.X86TailCallSpecialArg));
            Assert.That(arguments[2..].Select(argument => argument.Node.AsIntCon().IconValue),
                Is.EqualTo((nint[])[3, 9, 8, 7]));
            Assert.That(call.Args.HasThisPointer, Is.False);
            Assert.That(call.Args.IsVarArgs, Is.True);
            Assert.That(call.NeedsNullCheck, Is.False);
            Assert.That(call.Flags & GTF_CALL_POP_ARGS, Is.EqualTo(GTF_EMPTY));
            var movedReceiver = arguments[0].Node.AsOp();
            if (sideEffects)
            {
                var setup = movedReceiver.Op1.AsOp();
                Assert.That(setup.Op1.AsLclVar().Data, Is.SameAs(receiver));
                Assert.That(setup.Op2.Oper, Is.EqualTo(GT_NULLCHECK));
                Assert.That(movedReceiver.Op2.AsLclVar().LclNum, Is.EqualTo(2));
                Assert.That(compiler.lvaCount, Is.EqualTo(3));
            }
            else
            {
                Assert.That(movedReceiver.Op1.Oper, Is.EqualTo(GT_NULLCHECK));
                Assert.That(movedReceiver.Op2.AsLclVar().LclNum, Is.Zero);
                Assert.That(compiler.lvaCount, Is.EqualTo(2));
            }
        });
    }

    [Test]
    public static void WindowsX64DoesNotSelectX86JitHelper()
    {
        WithCompiler((compiler, entry, block) =>
            Assert.That(compiler.fgCanTailCallViaJitHelper(NewRecursiveCall(compiler)), Is.False));
    }

#if DEBUG
    private static int s_assertionCount;

    [Test]
    public static void TailCallValidationRejectsAnUnrelatedReturnValue()
    {
        WithCompiler((compiler, entry, block) => {
            var call = compiler.gtNewCallNode(TYP_INT, gtCallTypes.CT_USER_FUNC, null);
            var first = compiler.gtNewStmt(compiler.gtNewStoreLclVarNode(0, call));
            compiler.fgInsertStmtAtEnd(block, first);
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(compiler.gtNewUnaryNode(GT_RETURN, TYP_INT,
                compiler.gtNewLclvNode(TYP_INT, 1))));
            compiler.compCurBB = block;
            compiler.compCurStmt = first;

            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.doAssert = &RecordAssertion;
            ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
            using var tls = new JitTls(&jitInfo);
            s_assertionCount = 0;
            compiler.fgValidateIRForTailCall(call);
            Assert.That(s_assertionCount, Is.EqualTo(2));
        });
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* jitInfo, byte* file, int line, byte* expression)
    {
        s_assertionCount++;
        return 0;
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void TailCallValidationFollowsForwardedResultsAcrossBlocks(bool normalize)
    {
        WithCompiler((compiler, entry, block) => {
            var call = compiler.gtNewCallNode(TYP_INT, gtCallTypes.CT_USER_FUNC, null);
            call._returnType = TYP_UBYTE;
            GenTree value = normalize ? compiler.gtNewCastNode(TYP_INT, call, false, TYP_UBYTE) : call;
            var first = compiler.gtNewStmt(compiler.gtNewStoreLclVarNode(0, value));
            compiler.fgInsertStmtAtEnd(entry, first);
            entry.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(block, entry));
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(compiler.gtNewStoreLclVarNode(1,
                compiler.gtNewLclvNode(TYP_INT, 0))));
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(compiler.gtNewBinaryNode(GT_COMMA, TYP_VOID,
                compiler.gtNewNothingNode(), compiler.gtNewNothingNode())));
            var returnStatement = compiler.gtNewStmt(compiler.gtNewUnaryNode(GT_RETURN, TYP_INT,
                compiler.gtNewLclvNode(TYP_INT, 1)));
            compiler.fgInsertStmtAtEnd(block, returnStatement);
            compiler.compCurBB = entry;
            compiler.compCurStmt = first;
            compiler.fgValidateIRForTailCall(call);

            Assert.That(entry.FirstStmt, Is.SameAs(first));
            Assert.That(block.LastStmt, Is.SameAs(returnStatement));
            Assert.That(first.RootNode.AsLclVar().Data, Is.SameAs(value));
        });
    }
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "fgMorphStmt")]
    private static extern ref Statement? MorphStatement(Compiler compiler);

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
        compiler.lvaRetAddrVar = BAD_VAR_NUM;
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
