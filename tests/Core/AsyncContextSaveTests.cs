// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class AsyncContextSaveTests
{
    private static int s_metadataQueries;
    private static int s_methodQueries;

    [TestCase(TYP_VOID, false, false, false)]
    [TestCase(TYP_INT, false, false, false)]
    [TestCase(TYP_BYREF, false, false, false)]
    [TestCase(TYP_VOID, false, true, false)]
    [TestCase(TYP_INT, false, true, true)]
    [TestCase(TYP_INT, true, false, false)]
    [TestCase(TYP_VOID, true, true, true)]
    public static void SavesContextsAndMergesReturns(var_types returnType, bool osr, bool initMem, bool loop)
    {
        WithCompiler(compiler => {
            compiler.info.compRetType = returnType == TYP_BYREF ? TYP_STRUCT : returnType;
            compiler.info.compRetBuffArg = returnType == TYP_BYREF ? 0 : BAD_VAR_NUM;
            compiler.info.compInitMem = initMem;
            if (osr)
            {
                compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_OSR);
            }
            var entry = NewBlock(compiler, BBJ_COND, 0);
            var left = NewBlock(compiler, BBJ_RETURN, 10);
            var right = NewBlock(compiler, BBJ_RETURN, 20);
            entry.Next = left;
            left.Next = right;
            compiler.fgFirstBB = entry;
            compiler.fgLastBB = right;
            compiler.compCurBB = right;
            compiler.fgReturnCount = 2;
            entry.SetCond(compiler.fgAddRefPred(left, entry), compiler.fgAddRefPred(right, entry));
            entry.TrueEdge.Likelihood = 0.3;
            entry.FalseEdge.Likelihood = 0.7;
            left.setBBProfileWeight(3);
            right.setBBProfileWeight(7);
            if (loop)
            {
                entry.SetFlags(BBF_BACKWARD_JUMP);
            }
            var call = compiler.gtNewCallNode(TYP_VOID, gtCallTypes.CT_USER_FUNC, (CORINFO_METHOD_STRUCT_*)3);
            call.SetIsAsync(default);
            compiler.fgInsertStmtAtEnd(entry, compiler.gtNewStmt(call));
            compiler.fgInsertStmtAtEnd(entry, compiler.gtNewStmt(new GenTreeUnOp(GT_JTRUE, TYP_VOID,
                new GenTreeOp(GT_EQ, TYP_INT, compiler.gtNewIconNode(TYP_INT, 1), compiler.gtNewIconNode(TYP_INT, 0)))));
            var leftReturn = AddReturn(compiler, left, returnType, 11);
            var rightReturn = AddReturn(compiler, right, returnType, 22);

            Assert.That(compiler.SaveAsyncContexts(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));

            var descriptor = compiler.compHndBBtab[0];
            var fault = descriptor.ebdHndBeg;
            var merged = compiler.fgLastBB ?? throw new InvalidOperationException("Missing merged return.");
            Assert.Multiple(() => {
                Assert.That(compiler.compHndBBtabCount, Is.EqualTo(1));
                Assert.That(descriptor.HasFaultHandler, Is.True);
                Assert.That(descriptor.ebdID, Is.EqualTo(7));
                Assert.That(compiler.compEHID, Is.EqualTo(8));
                Assert.That(compiler.ehIsAsyncContextRestore(descriptor.ebdID), Is.True);
                Assert.That(compiler.ehIsInsideNonAsyncContextRestoreRegion(left), Is.False);
                Assert.That(descriptor.ebdTryBeg, Is.SameAs(entry.Next));
                Assert.That(descriptor.ebdTryLast, Is.SameAs(right));
                Assert.That(descriptor.ebdHndLast, Is.SameAs(fault));
                Assert.That(descriptor.ebdTryBegOffs, Is.EqualTo(0));
                Assert.That(descriptor.ebdTryEndOffs, Is.EqualTo(30));
                Assert.That(descriptor.ebdEnclosingTryIndex, Is.EqualTo(EHblkDsc.NO_ENCLOSING_INDEX));
                Assert.That(entry.hasTryIndex, Is.False);
                Assert.That(left.TryIndex, Is.Zero);
                Assert.That(right.TryIndex, Is.Zero);
                Assert.That(fault.Kind, Is.EqualTo(BBJ_EHFAULTRET));
                Assert.That(fault.bbRefs, Is.EqualTo(1));
                Assert.That(fault.bbWeight, Is.Zero);
                Assert.That(fault.HndIndex, Is.Zero);
                Assert.That(fault.hasTryIndex, Is.False);
                Assert.That(merged.Kind, Is.EqualTo(BBJ_RETURN));
                Assert.That(merged.hasTryIndex, Is.False);
                Assert.That(merged.hasHndIndex, Is.False);
                Assert.That(merged.bbRefs, Is.EqualTo(2));
                Assert.That(merged.bbWeight, Is.EqualTo(10));
                Assert.That(left.Target, Is.SameAs(merged));
                Assert.That(right.Target, Is.SameAs(merged));
                Assert.That(compiler.fgReturnCount, Is.EqualTo(1));
                Assert.That(compiler.compAsyncBodyMaySuspend, Is.True);
                Assert.That(leftReturn.RootNode.Oper, Is.EqualTo(GT_NOP));
                Assert.That(rightReturn.RootNode.Oper, Is.EqualTo(GT_NOP));
                Assert.That(s_metadataQueries, Is.EqualTo(1));
                Assert.That(s_methodQueries, Is.EqualTo(osr ? 1 : 2));
            });

            var initialize = !osr && (loop || !initMem);
            var entryStatements = Statements(entry);
            Assert.That(entryStatements.Count, Is.EqualTo(osr ? 0 : initialize ? 2 : 1));
            if (!osr)
            {
                var capture = entryStatements[^1].RootNode.AsCall();
                Assert.That((nint)capture._callMethHnd, Is.EqualTo((nint)1));
                Assert.That(ArgumentLocals(capture), Is.EqualTo([
                    compiler.lvaAsyncThreadObjectVar, compiler.lvaAsyncExecutionContextVar, compiler.lvaAsyncSynchronizationContextVar
                ]));
                if (initialize)
                {
                    var store = entryStatements[0].RootNode.AsLclVar();
                    Assert.That(store.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
                    Assert.That(store.LclNum, Is.EqualTo(compiler.lvaResumedIndicator));
                    Assert.That(store.Data.AsIntCon().IconValue, Is.EqualTo((nint)0));
                }
            }

            foreach (var local in new[] {
                compiler.lvaResumedIndicator, compiler.lvaAsyncThreadObjectVar,
                compiler.lvaAsyncExecutionContextVar, compiler.lvaAsyncSynchronizationContextVar
            })
            {
                Assert.That(compiler.lvaTable[local].lvOnlyUsedOnSynchronousPath, Is.True);
                Assert.That(compiler.lvaTable[local].lvIsOSRLocal, Is.EqualTo(osr));
                Assert.That(compiler.lvaTable[local].Type, Is.EqualTo(local == compiler.lvaResumedIndicator ? TYP_I_IMPL : TYP_REF));
                Assert.That(compiler.lvaTable[local].lvHasLdAddrOp, Is.EqualTo(!osr || local == compiler.lvaResumedIndicator));
            }
            AssertRestore(compiler, Statements(fault)[0].RootNode.AsCall());
            AssertRestore(compiler, Statements(merged)[0].RootNode.AsCall());
            var args = new List<WellKnownArg>();
            foreach (var arg in call.Args.Args)
            {
                args.Add(arg.WellKnownArg);
            }
            Assert.That(args, Is.EqualTo([
                WellKnownArg.AsyncResumedDef, WellKnownArg.AsyncResumedUse,
                WellKnownArg.AsyncExecutionContext, WellKnownArg.AsyncSynchronizationContext
            ]));
            var finalReturn = Statements(merged)[^1].RootNode.AsUnOp();
            Assert.That(finalReturn.Oper, Is.EqualTo(GT_RETURN));
            Assert.That(finalReturn.Type, Is.EqualTo(returnType));
            if (returnType != TYP_VOID)
            {
                var local = finalReturn.Op1.AsLclVar().LclNum;
                Assert.That(Statements(left)[^1].RootNode.AsLclVar().LclNum, Is.EqualTo(local));
                Assert.That(Statements(right)[^1].RootNode.AsLclVar().LclNum, Is.EqualTo(local));
                Assert.That(compiler.lvaTable[local].Type, Is.EqualTo(returnType));
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void DoesNotCreateAReturnForNonReturningBodies(bool osr)
    {
        WithCompiler(compiler => {
            if (osr)
            {
                compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_OSR);
            }
            var entry = NewBlock(compiler, BBJ_ALWAYS, 0);
            entry.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(entry, entry));
            compiler.fgFirstBB = entry;
            compiler.fgLastBB = entry;

            Assert.That(compiler.SaveAsyncContexts(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.fgReturnCount, Is.Zero);
            Assert.That(compiler.fgLastBB?.Kind, Is.EqualTo(BBJ_EHFAULTRET));
            Assert.That(compiler.compAsyncBodyMaySuspend, Is.False);
            Assert.That(compiler.lvaCount, Is.EqualTo(5));
            Assert.That(s_methodQueries, Is.EqualTo(osr ? 0 : 1));
        });
    }

    [Test]
    public static void LeavesMethodsWithoutSaveContextsUnchanged()
    {
        WithCompiler(compiler => {
            compiler.info.compMethodInfo->options = 0;
            Assert.That(compiler.SaveAsyncContexts(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(compiler.lvaCount, Is.EqualTo(1));
            Assert.That(compiler.compHndBBtabCount, Is.Zero);
            Assert.That(compiler.compEHID, Is.EqualTo(7));
            Assert.That(s_metadataQueries, Is.Zero);
            Assert.That(s_methodQueries, Is.Zero);
        });
    }

    [Test]
    public static void EnclosesExistingClausesWithoutReplacingTheirIDsOrInnermostIndices()
    {
        WithCompiler(compiler => {
            var entry = NewBlock(compiler, BBJ_ALWAYS, 0);
            var body = NewBlock(compiler, BBJ_THROW, 10);
            var handler = NewBlock(compiler, BBJ_THROW, 20);
            entry.Next = body;
            body.Next = handler;
            entry.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(body, entry));
            compiler.fgFirstBB = entry;
            compiler.fgLastBB = handler;
            body.TryIndex = 0;
            handler.HndIndex = 0;
            compiler.compHndBBtab = [new EHblkDsc {
                ebdID = 3, ebdTryBeg = body, ebdTryLast = body, ebdHndBeg = handler, ebdHndLast = handler,
                ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX, ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX
            }];
            compiler.compHndBBtabCount = 1;

            Assert.That(compiler.SaveAsyncContexts(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.compHndBBtabCount, Is.EqualTo(2));
            Assert.That(compiler.compHndBBtab[0].ebdID, Is.EqualTo(3));
            Assert.That(compiler.compHndBBtab[0].ebdEnclosingTryIndex, Is.EqualTo(1));
            Assert.That(body.TryIndex, Is.Zero);
            Assert.That(handler.TryIndex, Is.EqualTo(1));
            Assert.That(handler.HndIndex, Is.Zero);
            Assert.That(compiler.ehIsAsyncContextRestore(3), Is.False);
            Assert.That(compiler.ehIsInsideNonAsyncContextRestoreRegion(body), Is.True);
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void BeginningInsertionPreservesPhiAndCatchPrefixes(bool hasBody, bool insertPhi)
    {
        WithCompiler(compiler => {
            var block = NewBlock(compiler, BBJ_RETURN, 0);
            var phi = compiler.gtNewStmt(compiler.gtNewStoreLclVarNode(0, new GenTreePhi(TYP_BYREF)));
            var catchStore = compiler.gtNewStmt(compiler.gtNewStoreLclVarNode(0, new GenTree(GT_CATCH_ARG, TYP_REF)));
            compiler.fgInsertStmtAtBeg(block, phi);
            compiler.fgInsertStmtAtEnd(block, catchStore);
            var body = compiler.gtNewStmt(compiler.gtNewIconNode(TYP_INT, 1));
            if (hasBody)
            {
                compiler.fgInsertStmtAtEnd(block, body);
            }
            var inserted = compiler.gtNewStmt(insertPhi
                ? compiler.gtNewStoreLclVarNode(0, new GenTreePhi(TYP_BYREF))
                : compiler.gtNewIconNode(TYP_INT, 2));

            compiler.fgInsertStmtAtBeg(block, inserted);
            var expected = insertPhi ? new List<Statement> { inserted, phi, catchStore } : [phi, catchStore, inserted];
            if (hasBody)
            {
                expected.Add(body);
            }
            Assert.That(Statements(block), Is.EqualTo(expected));
            Assert.That(block.FirstStmt?.PrevStmt, Is.SameAs(expected[^1]));
            Assert.That(expected[^1].NextStmt, Is.Null);
            for (var i = 1; i < expected.Count; i++)
            {
                Assert.That(expected[i].PrevStmt, Is.SameAs(expected[i - 1]));
            }
        });
    }

    private static Statement AddReturn(Compiler compiler, BasicBlock block, var_types type, int value)
    {
        GenTree? operand = type == TYP_VOID ? null : type == TYP_BYREF
            ? compiler.gtNewLclVarNode(TYP_BYREF, 0) : compiler.gtNewIconNode(type, value);
        var stmt = compiler.gtNewStmt(new GenTreeUnOp(GT_RETURN, type, operand));
        compiler.fgInsertStmtAtEnd(block, stmt);
        return stmt;
    }

    private static void AssertRestore(Compiler compiler, GenTreeCall call)
    {
        Assert.That((nint)call._callMethHnd, Is.EqualTo((nint)2));
        Assert.That(ArgumentLocals(call), Is.EqualTo([
            compiler.lvaResumedIndicator, compiler.lvaAsyncThreadObjectVar,
            compiler.lvaAsyncExecutionContextVar, compiler.lvaAsyncSynchronizationContextVar
        ]));
        var resumed = call.Args.GetArgByIndex(0) ?? throw new InvalidOperationException("Missing resumed argument.");
        Assert.That(resumed.Node.Type, Is.EqualTo(TYP_INT));
    }

    private static List<int> ArgumentLocals(GenTreeCall call)
    {
        var result = new List<int>();
        foreach (var arg in call.Args.Args)
        {
            result.Add(arg.Node.AsLclVarCommon().LclNum);
        }
        return result;
    }

    private static List<Statement> Statements(BasicBlock block)
    {
        var result = new List<Statement>();
        foreach (var stmt in block.Statements)
        {
            result.Add(stmt);
        }
        return result;
    }

    private static BasicBlock NewBlock(Compiler compiler, BBKinds kind, int offset)
    {
        var block = BasicBlock.New(compiler, kind);
        block.bbCodeOffs = offset;
        block.bbCodeOffsEnd = offset + 10;
        return block;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void GetAsyncInfo(ICorJitInfo* jitInfo, CORINFO_ASYNC_INFO* result)
    {
        s_metadataQueries++;
        *result = default;
        result->captureContextsMethHnd = (CORINFO_METHOD_STRUCT_*)1;
        result->restoreContextsMethHnd = (CORINFO_METHOD_STRUCT_*)2;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoFlag GetMethodFlags(ICorJitInfo* jitInfo, CORINFO_METHOD_STRUCT_* method)
    {
        s_methodQueries++;
        return CorInfoFlag.CORINFO_FLG_STATIC;
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
        compiler.opts.SetMinOpts(false);
        CORINFO_METHOD_INFO methodInfo = default;
        methodInfo.options = CorInfoOptions.CORINFO_ASYNC_SAVE_CONTEXTS;
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getAsyncInfo = &GetAsyncInfo;
        vtable.Base.Base.getMethodAttribs = &GetMethodFlags;
        var jitInfo = new ICorJitInfo { lpVtbl = &vtable };
        compiler.info = new Compiler.Info { compCompHnd = &jitInfo, compMethodInfo = &methodInfo, compRetBuffArg = BAD_VAR_NUM, compRetType = TYP_VOID };
        compiler.lvaTable = [new LclVarDsc { Type = TYP_BYREF }];
        compiler.lvaCount = 1;
        compiler.compHndBBtab = [];
        compiler.compEHID = 7;
        compiler.fgPredsComputed = true;
        compiler.compInlineContext = (InlineContext)RuntimeHelpers.GetUninitializedObject(typeof(InlineContext));
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
#endif
        JitTls.Compiler = compiler;
        s_metadataQueries = 0;
        s_methodQueries = 0;
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
