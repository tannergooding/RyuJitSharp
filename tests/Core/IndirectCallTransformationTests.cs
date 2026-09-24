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
internal static unsafe partial class IndirectCallTransformationTests
{
    private static int s_chunkOffset;
    private static int s_slotOffset;
    private static bool s_relative;

    [TestCase(false)]
    [TestCase(true)]
    public static void OwningLinkIdentifiesTheFirstSharedOperand(bool reversed)
    {
        WithCompiler(compiler => {
            var shared = compiler.gtNewIconNode(TYP_INT, 1);
            var parent = compiler.gtNewBinaryNode(GT_ADD, TYP_INT, shared, shared);
            if (reversed)
            {
                parent.Flags |= GTF_REVERSE_OPS;
            }

            var stmt = compiler.gtNewStmt(parent);
            var link = compiler.gtFindLink(stmt, shared);
            Assert.That(link.nodeToFind, Is.SameAs(shared));
            Assert.That(link.parent, Is.SameAs(parent));
            Assert.That(Unsafe.AreSame(ref link.result, ref parent.Op1Ref), Is.True);
            var replacement = compiler.gtNewIconNode(TYP_INT, 2);
            link.result = replacement;
            Assert.That(parent.Op1, Is.SameAs(replacement));
            Assert.That(parent.Op2, Is.SameAs(shared));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void OwningLinkDistinguishesRootFromMissingNode(bool present)
    {
        WithCompiler(compiler => {
            var node = compiler.gtNewIconNode(TYP_INT, 1);
            var stmt = compiler.gtNewStmt(present ? node : compiler.gtNewIconNode(TYP_INT, 2));
            var link = compiler.gtFindLink(stmt, node);
            Assert.That(link.parent, Is.Null);
            Assert.That(Unsafe.IsNullRef(ref link.result), Is.EqualTo(!present));

            if (present)
            {
                var replacement = compiler.gtNewIconNode(TYP_INT, 3);
                link.result = replacement;
                Assert.That(stmt.RootNode, Is.SameAs(replacement));
            }
        });
    }

    [Test]
    public static void OwningCallLinksRetainDistinctArgumentAndControlSlots()
    {
        WithCompiler(compiler => {
            var shared = compiler.gtNewIconNode(TYP_I_IMPL, 0x1000);
            var call = CreateFatCall(compiler, shared, TYP_VOID);
            var arg = call.Args.PushBack(NewCallArg.CreateForPrimitive(shared));
            var stmt = compiler.gtNewStmt(call);
            var argumentLink = compiler.gtFindLink(stmt, shared);
            Assert.That(argumentLink.parent, Is.SameAs(call));
            Assert.That(Unsafe.AreSame(ref argumentLink.result, ref arg.EarlyNodeRef), Is.True);
            var replacement = compiler.gtNewIconNode(TYP_I_IMPL, 0x2000);
            argumentLink.result = replacement;

            var controlLink = compiler.gtFindLink(stmt, shared);
            Assert.That(Unsafe.AreSame(ref controlLink.result, ref call.ControlExprRef), Is.True);
            controlLink.result = compiler.gtNewIconNode(TYP_I_IMPL, 0x3000);
            Assert.That(argumentLink.result, Is.SameAs(replacement));
            Assert.That(arg.EarlyNode, Is.SameAs(replacement));
        });
    }

    [TestCase(0, false)]
    [TestCase(1, false)]
    [TestCase(2, false)]
    [TestCase(0, true)]
    public static void CandidateCloningPreservesInfoWithoutRetargetingPlaceholders(int guardedCandidates, bool noReturn)
    {
        WithCompiler(compiler => {
            var call = compiler.gtNewCallNode(TYP_INT, gtCallTypes.CT_USER_FUNC, (CORINFO_METHOD_STRUCT_*)0x1000);
            call.IsNoReturn = noReturn;
            call.RegNum = regNumber.REG_RAX;
            var argument = compiler.gtNewBinaryNode(GT_ADD, TYP_INT,
                compiler.gtNewIconNode(TYP_INT, 1), compiler.gtNewIconNode(TYP_INT, 2));
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(argument));
            var info = new InlineCandidateInfo { preexistingSpillTemp = BAD_VAR_NUM };

            if (guardedCandidates == 0)
            {
                call.SingleInlineCandidateInfo = info;
            }
            else
            {
                call.AddGdvCandidateInfo(compiler, info);
                if (guardedCandidates == 2)
                {
                    call.AddGdvCandidateInfo(compiler, new InlineCandidateInfo { preexistingSpillTemp = BAD_VAR_NUM });
                }
            }

            var retExpr = compiler.gtNewInlineCandidateReturnExpr(call, TYP_INT);
            info.retExpr = retExpr;
            var beforeNoReturnCount = compiler.optNoReturnCallCount;
            var copy = compiler.gtCloneCandidateCall(call);

            Assert.That(copy, Is.Not.SameAs(call));
            Assert.That(copy.Flags, Is.EqualTo(call.Flags));
            Assert.That(copy.RegNum, Is.EqualTo(call.RegNum));
            Assert.That(copy.IsNoReturn, Is.EqualTo(noReturn));
            Assert.That(compiler.optNoReturnCallCount, Is.EqualTo(beforeNoReturnCount + (noReturn ? 1 : 0)));
            Assert.That(copy.InlineCandidatesCount, Is.EqualTo(Math.Max(guardedCandidates, 1)));
            Assert.That(copy.IsGuardedDevirtualizationCandidate, Is.EqualTo(guardedCandidates != 0));
            Assert.That(copy.Args.Head, Is.Not.SameAs(call.Args.Head));
            var clonedArgument = (copy.Args.Head?.EarlyNode ?? throw new InvalidOperationException()).AsOp();
            Assert.That(clonedArgument, Is.Not.SameAs(argument));
            Assert.That(clonedArgument.Op1, Is.Not.SameAs(argument.Op1));
            Assert.That(clonedArgument.Op2, Is.Not.SameAs(argument.Op2));
            Assert.That(info.retExpr, Is.SameAs(retExpr));
            Assert.That(retExpr.InlineCandidate, Is.SameAs(call));

            if (guardedCandidates == 0)
            {
                Assert.That(copy.SingleInlineCandidateInfo, Is.SameAs(info));
            }
            else
            {
                for (byte i = 0; i < guardedCandidates; i++)
                {
                    Assert.That(copy.GetGdvCandidateInfo(i), Is.SameAs(call.GetGdvCandidateInfo(i)));
                }
            }

            copy.ClearInlineInfo();
            Assert.That(call.InlineCandidatesCount, Is.EqualTo(Math.Max(guardedCandidates, 1)));
            var ordinaryClone = compiler.gtCloneExpr(copy) ?? throw new InvalidOperationException();
            Assert.That(ordinaryClone.AsCall().Args.Head, Is.Not.SameAs(copy.Args.Head));
            Assert.That(ordinaryClone.AsCall().IsGuardedDevirtualizationCandidate, Is.False);
        });
    }

    [TestCase(-1, 24, false, false)]
    [TestCase(16, 24, false, false)]
    [TestCase(16, 24, true, false)]
    [TestCase(-1, 24, false, true)]
    [TestCase(16, 24, false, true)]
    [TestCase(16, 24, true, true)]
    [TestCase(int.MaxValue - 7, 32, true, false)]
    [TestCase(-16, 32, true, false)]
    public static void VirtualTargetsPreserveChunkRelativeAndExceptionSemantics(int chunkOffset, int slotOffset,
        bool relative, bool globalMorph)
    {
        WithCompiler(compiler => {
            s_chunkOffset = chunkOffset;
            s_slotOffset = slotOffset;
            s_relative = relative;
            compiler.fgGlobalMorph = globalMorph;
            var local = AddLocal(compiler, TYP_REF);
            var receiver = compiler.gtNewLclvNode(TYP_REF, local);
            var call = compiler.gtNewCallNode(TYP_VOID, gtCallTypes.CT_USER_FUNC, (CORINFO_METHOD_STRUCT_*)0x1000);
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(receiver).WithWellKnownArg(WellKnownArg.ThisPointer));

            var result = compiler.fgExpandVirtualVtableCallTarget(call);
            GenTreeIndir methodTable;
            GenTreeIndir target;

            if (relative)
            {
                Assert.That(result.Oper, Is.EqualTo(GT_COMMA));
                var outer = result.AsOp();
                var firstStore = outer.Op1.AsLclVar();
                methodTable = firstStore.Data.AsIndir();
                var inner = outer.Op2.AsOp();
                Assert.That(inner.Oper, Is.EqualTo(GT_COMMA));
                var secondStore = inner.Op1.AsLclVar();
                var address = secondStore.Data.AsOp();
                Assert.That(address.Oper, Is.EqualTo(GT_ADD));
                Assert.That(address.Op1.AsOp().Op1.AsLclVar().LclNum, Is.EqualTo(firstStore.LclNum));
                Assert.That(address.Op1.AsOp().Op2.AsIntCon().IconValue,
                    Is.EqualTo(unchecked((nint)(uint)(chunkOffset + slotOffset))));
                var chunk = address.Op2.AsIndir();
                Assert.That(chunk.Flags & (GTF_IND_NONFAULTING | GTF_IND_INVARIANT),
                    Is.EqualTo(GTF_IND_NONFAULTING | GTF_IND_INVARIANT));
                Assert.That(chunk.Addr.AsOp().Op1.AsLclVar().LclNum, Is.EqualTo(firstStore.LclNum));
                Assert.That(chunk.Addr.AsOp().Op2.AsIntCon().IconValue, Is.EqualTo(unchecked((nint)(uint)chunkOffset)));
                var finalAdd = inner.Op2.AsOp();
                Assert.That(finalAdd.Oper, Is.EqualTo(GT_ADD));
                target = finalAdd.Op1.AsIndir();
                Assert.That(target.Addr.AsLclVar().LclNum, Is.EqualTo(secondStore.LclNum));
                Assert.That(finalAdd.Op2.AsLclVar().LclNum, Is.EqualTo(secondStore.LclNum));
                Assert.That(compiler.lvaCount, Is.EqualTo(3));
            }
            else
            {
                target = result.AsIndir();
                var offset = target.Addr.AsOp();
                Assert.That(offset.Oper, Is.EqualTo(GT_ADD));
                Assert.That(offset.Op2.AsIntCon().IconValue, Is.EqualTo((nint)slotOffset));
                methodTable = offset.Op1.AsIndir();

                if (chunkOffset != CORINFO_VIRTUALCALL_NO_CHUNK)
                {
                    Assert.That(methodTable.Flags & (GTF_IND_NONFAULTING | GTF_IND_INVARIANT),
                        Is.EqualTo(GTF_IND_NONFAULTING | GTF_IND_INVARIANT));
                    var chunk = methodTable.Addr.AsOp();
                    Assert.That(chunk.Oper, Is.EqualTo(GT_ADD));
                    Assert.That(chunk.Op2.AsIntCon().IconValue, Is.EqualTo((nint)chunkOffset));
                    methodTable = chunk.Op1.AsIndir();
                }

                Assert.That(compiler.lvaCount, Is.EqualTo(1));
            }

            Assert.That(target.Flags & (GTF_IND_NONFAULTING | GTF_IND_INVARIANT), Is.EqualTo(GTF_IND_NONFAULTING));
            Assert.That(methodTable.Flags & GTF_EXCEPT, Is.EqualTo(globalMorph ? 0 : GTF_EXCEPT));
            Assert.That(methodTable.Addr, Is.Not.SameAs(receiver));
            Assert.That(methodTable.Addr.AsLclVar().LclNum, Is.EqualTo(local));
            Assert.That(call.Args.ThisArg?.Node, Is.SameAs(receiver));
        });
    }

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
            var hiddenLoad = (hidden.EarlyNode ?? throw new InvalidOperationException()).AsIndir();
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
            Assert.That(readArg.EarlyNode?.AsLclVar().LclNum, Is.EqualTo(local + 1));
            Assert.That(effectArg.EarlyNode?.AsLclVar().LclNum, Is.EqualTo(local + 2));
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
        vtable.Base.Base.getMethodVTableOffset = &GetMethodVTableOffset;
        vtable.Base.Base.getMethodClass = &GetMethodClass;
        vtable.Base.Base.getMethodAttribs = &GetMethodAttribs;
        vtable.Base.Base.getMethodSig = &GetMethodSig;
        vtable.Base.Base.isValueClass = &IsValueClass;
        vtable.Base.Base.getExactClasses = &GetExactClasses;
        vtable.Base.embedClassHandle = &EmbedClassHandle;
        vtable.Base.getFunctionEntryPoint = &GetFunctionEntryPoint;
        // The EE uses a one-byte bool; unmanaged managed callbacks require its
        // blittable byte representation.
        vtable.Base.getFunctionFixedEntryPoint =
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_METHOD_STRUCT_*, bool, CORINFO_CONST_LOOKUP*, void>)
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_METHOD_STRUCT_*, byte, CORINFO_CONST_LOOKUP*, void>)&GetFunctionFixedEntryPoint;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.compHndBBtab = [];
        compiler.fgPredsComputed = true;
        compiler.lvaTable = [];
        compiler.info.compIsStatic = true;
        compiler.info.compCompHnd = &jitInfo;
        CORINFO_METHOD_INFO methodInfo = default;
        compiler.info.compMethodInfo = &methodInfo;
        compiler.eeInfoInitialized = true;
        compiler.eeInfo.offsetOfDelegateFirstTarget = 16;
        compiler.eeInfo.offsetOfDelegateInstance = 8;
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
        using var jitTls = new JitTls(&jitInfo);
#endif
        var previousCompiler = JitTls.Compiler;
        var previousConfig = JitConfig;
        JitTls.Compiler = compiler;
        JitConfig = new JitConfigValues();
        ChainLikelihood(ref JitConfig) = 101;
        ChainStatements(ref JitConfig) = 1;
#if DEBUG
        PrintDevirtualizedMethods(ref JitConfig) = new JitConfigValues.MethodSet(null, null);
#endif

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
            JitConfig = previousConfig;
            JitTls.Compiler = previousCompiler;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte UnavailableClassName(ICorJitInfo* self, delegate* unmanaged[Cdecl]<void*, void> callback, void* state)
    {
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void GetMethodVTableOffset(ICorJitInfo* self, CORINFO_METHOD_STRUCT_* method, int* chunk, int* slot, bool* relative)
    {
        *chunk = s_chunkOffset;
        *slot = s_slotOffset;
        *relative = s_relative;
    }
}
