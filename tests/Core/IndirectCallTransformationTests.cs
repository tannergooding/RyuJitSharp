// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class IndirectCallTransformationTests
{
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void FatPointersPreserveCallsControlFlowAndEh(bool returnsValue, bool inTry)
    {
        WithCompiler(compiler => {
            var block = compiler.fgFirstBB ?? throw new InvalidOperationException();
            block.SetFlags(BBF_IMPORTED | BBF_DONT_REMOVE | BBF_BACKWARD_JUMP);
            block.setBBProfileWeight(100);
            var pointerLocal = AddLocal(compiler, TYP_I_IMPL);
            var call = CreateFatCall(compiler, compiler.gtNewLclvNode(TYP_I_IMPL, pointerLocal), returnsValue ? TYP_INT : TYP_VOID);
            var argument = compiler.gtNewIconNode(TYP_INT, 42);
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(argument));
            GenTree root = call;
            var resultLocal = BAD_VAR_NUM;

            if (returnsValue)
            {
                resultLocal = AddLocal(compiler, TYP_INT);
                root = compiler.gtNewStoreLclVarNode(resultLocal, call);
            }

            var debugInfo = new DebugInfo(null, new ILLocation(17, 0));
            var before = compiler.gtNewStmt(compiler.gtNewNothingNode());
            var stmt = compiler.gtNewStmt(root, debugInfo);
            var after = compiler.gtNewStmt(compiler.gtNewNothingNode());
            compiler.fgInsertStmtAtEnd(block, before);
            compiler.fgInsertStmtAtEnd(block, stmt);
            compiler.fgInsertStmtAtEnd(block, after);

            if (inTry)
            {
                var handler = compiler.fgNewBBafter(BBJ_THROW, block, true);
                handler.HndIndex = 0;
                block.TryIndex = 0;
                compiler.compHndBBtab = [
                    new EHblkDsc { ebdTryBeg = block, ebdTryLast = block, ebdHndBeg = handler, ebdHndLast = handler }
                ];
                compiler.compHndBBtabCount = 1;
            }

            new IndirectCallTransformer.FatPointerCallTransformer(compiler, block, stmt).Run();

            var check = block.Target;
            var thin = check.FalseTarget;
            var fat = check.TrueTarget;
            var remainder = thin.Target;
            Assert.That(block.Statements.ToArray(), Is.EqualTo((Statement[])[before]));
            Assert.That(remainder.Statements.ToArray(), Is.EqualTo((Statement[])[after]));
            Assert.That(remainder.Kind, Is.EqualTo(BBJ_RETURN));
            Assert.That(remainder.bbRefs, Is.EqualTo(2));
            Assert.That(check.bbRefs, Is.EqualTo(1));
            Assert.That(thin.bbRefs, Is.EqualTo(1));
            Assert.That(fat.bbRefs, Is.EqualTo(1));
            Assert.That(check.Next, Is.SameAs(thin));
            Assert.That(thin.Next, Is.SameAs(fat));
            Assert.That(fat.Next, Is.SameAs(remainder));
            Assert.That(fat.Target, Is.SameAs(remainder));
            Assert.That(check.TrueEdge.Likelihood, Is.EqualTo(0.5));
            Assert.That(check.FalseEdge.Likelihood, Is.EqualTo(0.5));
            Assert.That(thin.bbWeight, Is.EqualTo(80));
            Assert.That(fat.bbWeight, Is.EqualTo(20));
            Assert.That(check.bbWeight, Is.EqualTo(100));
            Assert.That(remainder.bbWeight, Is.EqualTo(100));
            Assert.That(compiler.lvaCount, Is.EqualTo(returnsValue ? 2 : 1));
            Assert.That(call.IsFatPointerCandidate, Is.False);

            foreach (var added in new[] { check, thin, fat, remainder })
            {
                Assert.That(added.HasFlag(BBF_DONT_REMOVE), Is.False);
                Assert.That(added.HasFlag(BBF_IMPORTED), Is.True);
                Assert.That(added.HasFlag(BBF_BACKWARD_JUMP), Is.True);
                if (inTry)
                {
                    Assert.That(added.TryIndex, Is.EqualTo(0));
                }
            }

            if (inTry)
            {
                Assert.That(compiler.compHndBBtab[0].ebdTryLast, Is.SameAs(remainder));
            }

            var branch = (check.FirstStmt ?? throw new InvalidOperationException()).RootNode;
            Assert.That(branch.Oper, Is.EqualTo(GT_JTRUE));
            var compare = branch.AsUnOp().Op1.AsOp();
            Assert.That(compare.Oper, Is.EqualTo(GT_NE));
            Assert.That(compare.Op2.AsIntCon().IconValue, Is.EqualTo((nint)0));
            var mask = compare.Op1.AsOp();
            Assert.That(mask.Oper, Is.EqualTo(GT_AND));
            Assert.That(mask.Op1.AsLclVar().LclNum, Is.EqualTo(pointerLocal));
            Assert.That(mask.Op2.AsIntCon().IconValue, Is.EqualTo((nint)2));

            var thinStmt = thin.FirstStmt ?? throw new InvalidOperationException();
            var fatStmt = fat.FirstStmt ?? throw new InvalidOperationException();
            Assert.That(thinStmt.DebugInfo, Is.EqualTo(debugInfo));
            Assert.That(fatStmt.DebugInfo, Is.EqualTo(debugInfo));
            var thinCall = GetCall(thinStmt);
            var fatCall = GetCall(fatStmt);
            Assert.That(thinCall, Is.Not.SameAs(call));
            Assert.That(fatCall, Is.Not.SameAs(call));
            Assert.That(fatCall, Is.Not.SameAs(thinCall));
            Assert.That(thinCall.IsFatPointerCandidate, Is.False);
            Assert.That(fatCall.IsFatPointerCandidate, Is.False);
            Assert.That(thinCall.ControlExpr, Is.Not.SameAs(call.ControlExpr));
            Assert.That(thinCall.Args.Head?.EarlyNode, Is.Not.SameAs(argument));
            Assert.That(thinCall.Args.FindWellKnownArg(WellKnownArg.InstParam), Is.Null);
            Assert.That(thinCall.ControlExpr?.AsLclVar().LclNum, Is.EqualTo(pointerLocal));

            var target = (fatCall.ControlExpr ?? throw new InvalidOperationException()).AsIndir();
            AssertTupleLoad(target, pointerLocal);
            var hidden = fatCall.Args.FindWellKnownArg(WellKnownArg.InstParam) ?? throw new InvalidOperationException();
            Assert.That(hidden, Is.SameAs(fatCall.Args.Head));
            var hiddenLoad = hidden.EarlyNode.AsIndir();
            Assert.That(hiddenLoad.Flags & (GTF_IND_NONFAULTING | GTF_IND_INVARIANT),
                Is.EqualTo(GTF_IND_NONFAULTING | GTF_IND_INVARIANT));
            var offset = hiddenLoad.Addr.AsOp();
            Assert.That(offset.Oper, Is.EqualTo(GT_ADD));
            Assert.That(offset.Op2.AsIntCon().IconValue, Is.EqualTo((nint)TYP_I_IMPL.Size));
            Assert.That(offset.Op1, Is.Not.SameAs(target.Addr));
            Assert.That(offset.Op1.AsOp().Op1.AsLclVar().LclNum, Is.EqualTo(pointerLocal));
            Assert.That(offset.Op1.AsOp().Op2.AsIntCon().IconValue, Is.EqualTo((nint)2));

            if (returnsValue)
            {
                Assert.That(thinStmt.RootNode.AsLclVar().LclNum, Is.EqualTo(resultLocal));
                Assert.That(fatStmt.RootNode.AsLclVar().LclNum, Is.EqualTo(resultLocal));
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void HiddenContextFollowsThisAndAnyReturnBuffer(bool hasReturnBuffer)
    {
        WithCompiler(compiler => {
            var block = compiler.fgFirstBB ?? throw new InvalidOperationException();
            var call = CreateFatCall(compiler, compiler.gtNewIconNode(TYP_I_IMPL, 0x1002), TYP_VOID);
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewIconNode(TYP_REF, 0))
                .WithWellKnownArg(WellKnownArg.ThisPointer));

            if (hasReturnBuffer)
            {
                _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewIconNode(TYP_BYREF, 0x2000))
                    .WithWellKnownArg(WellKnownArg.RetBuffer));
            }

            var stmt = compiler.gtNewStmt(call);
            compiler.fgInsertStmtAtEnd(block, stmt);
            new IndirectCallTransformer.FatPointerCallTransformer(compiler, block, stmt).Run();

            var fatCall = GetCall(block.Target.TrueTarget.FirstStmt ?? throw new InvalidOperationException());
            var head = fatCall.Args.Head ?? throw new InvalidOperationException();
            Assert.That(head.WellKnownArg, Is.EqualTo(WellKnownArg.ThisPointer));
            var lastBeforeContext = hasReturnBuffer ? head.Next : head;
            Assert.That(lastBeforeContext?.WellKnownArg,
                Is.EqualTo(hasReturnBuffer ? WellKnownArg.RetBuffer : WellKnownArg.ThisPointer));
            Assert.That(lastBeforeContext?.Next?.WellKnownArg, Is.EqualTo(WellKnownArg.InstParam));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void GenericVirtualLookupSpillsEffectsAndAliasedReadsOnce(bool referenceArgument)
    {
        WithCompiler(compiler => {
            var block = compiler.fgFirstBB ?? throw new InvalidOperationException();
            var type = referenceArgument ? TYP_REF : TYP_INT;
            var local = AddLocal(compiler, type);
            compiler.lvaGetDesc(local).SetAddressExposed(true, default);
            if (referenceArgument)
            {
                compiler.lvaGetDesc(local).lvClassHnd = (CORINFO_CLASS_STRUCT_*)0x1234;
                compiler.lvaGetDesc(local).lvClassIsExact = true;
            }

            var lookup = compiler.gtNewHelperCallNode(TYP_I_IMPL, CORINFO_HELP_VIRTUAL_FUNC_PTR);
            var call = CreateFatCall(compiler, lookup, TYP_VOID);
            var invariant = compiler.gtNewIconNode(TYP_INT, 7);
            var read = compiler.gtNewLclvNode(type, local);
            var effect = compiler.gtNewHelperCallNode(TYP_INT, CORINFO_HELP_GETCURRENTMANAGEDTHREADID);
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(invariant));
            var readArg = call.Args.PushBack(NewCallArg.CreateForPrimitive(read));
            var effectArg = call.Args.PushBack(NewCallArg.CreateForPrimitive(effect));
            var debugInfo = new DebugInfo(null, new ILLocation(23, 0));
            var stmt = compiler.gtNewStmt(call, debugInfo);
            compiler.fgInsertStmtAtEnd(block, stmt);

            new IndirectCallTransformer.FatPointerCallTransformer(compiler, block, stmt).Run();

            var spills = block.Statements.ToArray();
            Assert.That(spills, Has.Length.EqualTo(3));
            Assert.That(spills.Select(s => s.RootNode.AsLclVar().Data).ToArray(),
                Is.EqualTo(new GenTree[] { read, effect, lookup }));
            Assert.That(spills.All(s => s.DebugInfo.Equals(debugInfo)), Is.True);
            Assert.That(call.Args.Head?.EarlyNode, Is.SameAs(invariant));
            Assert.That(readArg.EarlyNode.AsLclVar().LclNum, Is.EqualTo(local + 1));
            Assert.That(effectArg.EarlyNode.AsLclVar().LclNum, Is.EqualTo(local + 2));
            Assert.That(call.ControlExpr?.AsLclVar().LclNum, Is.EqualTo(local + 3));
            Assert.That(compiler.lvaCount, Is.EqualTo(4));
            if (referenceArgument)
            {
                Assert.That((nint)compiler.lvaGetDesc(local + 1).lvClassHnd, Is.EqualTo((nint)0x1234));
                Assert.That(compiler.lvaGetDesc(local + 1).lvClassIsExact, Is.True);
            }

            var thin = GetCall(block.Target.FalseTarget.FirstStmt ?? throw new InvalidOperationException());
            var fat = GetCall(block.Target.TrueTarget.FirstStmt ?? throw new InvalidOperationException());
            Assert.That(thin.ControlExpr?.AsLclVar().LclNum, Is.EqualTo(local + 3));
            AssertTupleLoad((fat.ControlExpr ?? throw new InvalidOperationException()).AsIndir(), local + 3);
        });
    }

    [TestCase(TYP_BYTE, false)]
    [TestCase(TYP_BYTE, true)]
    [TestCase(TYP_USHORT, false)]
    [TestCase(TYP_USHORT, true)]
    public static void InferredLocalLoadsPreserveNativeNormalization(var_types type, bool isParameter)
    {
        WithCompiler(compiler => {
            var local = AddLocal(compiler, type);
            compiler.lvaGetDesc(local).lvIsParam = isParameter;
            var load = compiler.gtNewLclVarNode(TYP_UNDEF, local);
            Assert.That(load.Type, Is.EqualTo(isParameter ? type : TYP_INT));
            Assert.That(compiler.gtNewLclVarNode(TYP_INT, local).Type, Is.EqualTo(TYP_INT));
        });
    }

    private static void AssertTupleLoad(GenTreeIndir load, int local)
    {
        Assert.That(load.Flags & (GTF_IND_NONFAULTING | GTF_IND_INVARIANT),
            Is.EqualTo(GTF_IND_NONFAULTING | GTF_IND_INVARIANT));
        var subtract = load.Addr.AsOp();
        Assert.That(subtract.Oper, Is.EqualTo(GT_SUB));
        Assert.That(subtract.Op1.AsLclVar().LclNum, Is.EqualTo(local));
        Assert.That(subtract.Op2.AsIntCon().IconValue, Is.EqualTo((nint)2));
    }

    private static GenTreeCall GetCall(Statement stmt)
    {
        var root = stmt.RootNode;
        return (root.Oper is GT_STORE_LCL_VAR ? root.AsLclVar().Data : root).AsCall();
    }

    private static GenTreeCall CreateFatCall(Compiler compiler, GenTree address, var_types type)
    {
        var call = compiler.gtNewCallNode(type, gtCallTypes.CT_INDIRECT, null);
        call.ControlExpr = address;
        call.IsFatPointerCandidate = true;
        return call;
    }

    private static int AddLocal(Compiler compiler, var_types type)
    {
        var local = compiler.lvaGrabTemp(true, "indirect call test local");
        compiler.lvaGetDesc(local).Type = type;
        return local;
    }

    private static void WithCompiler(Action<Compiler> action)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.runWithSPMIErrorTrap = &UnavailableClassName;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.compHndBBtab = [];
        compiler.fgPredsComputed = true;
        compiler.lvaTable = [];
        compiler.info.compIsStatic = true;
        compiler.info.compCompHnd = &jitInfo;
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
        using var jitTls = new JitTls(&jitInfo);
#endif
        var previousCompiler = JitTls.Compiler;
        JitTls.Compiler = compiler;

        try
        {
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            block.bbRefs = 0;
            compiler.fgFirstBB = block;
            compiler.fgLastBB = block;
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previousCompiler;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte UnavailableClassName(ICorJitInfo* self, delegate* unmanaged[Cdecl]<void*, void> callback, void* state)
    {
        return 0;
    }
}
