// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.InfoAccessType;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe partial class IndirectCallTransformationTests
{
    private static InfoAccessType s_lookupAccess;

#if DEBUG
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitPrintDevirtualizedMethods")]
    private static extern ref JitConfigValues.MethodSet PrintDevirtualizedMethods(ref JitConfigValues config);
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitGuardedDevirtualizationChainLikelihood")]
    private static extern ref int ChainLikelihood(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitGuardedDevirtualizationChainStatements")]
    private static extern ref int ChainStatements(ref JitConfigValues config);

    [TestCase(false, 0)]
    [TestCase(true, 0)]
    [TestCase(false, 1)]
    [TestCase(true, 1)]
    [TestCase(false, 2)]
    [TestCase(true, 2)]
    [TestCase(false, 3)]
    [TestCase(true, 3)]
    [TestCase(false, 4)]
    [TestCase(true, 4)]
    [TestCase(false, 5)]
    [TestCase(true, 5)]
    public static void GuardedCallsRepairReturnValuesAndCandidateRelationships(bool inlineable, int returnMode)
    {
        WithCompiler(compiler => {
            var block = compiler.fgFirstBB ?? throw new InvalidOperationException();
            block.setBBProfileWeight(100);
            var type = returnMode == 4 ? TYP_VOID : returnMode == 5 ? TYP_BYTE : TYP_INT;
            var call = CreateGdvCall(compiler, type, [80]);
            var info = call.GetGdvCandidateInfo(0);
            info.isInlineable = inlineable;
            var stmt = compiler.gtNewStmt(call, new DebugInfo(null, new ILLocation(23, 0)));
            compiler.fgInsertStmtAtEnd(block, stmt);
            GenTreeRetExpr? retExpr = null;
            var existingTemp = BAD_VAR_NUM;

            if (returnMode != 0)
            {
                retExpr = compiler.gtNewInlineCandidateReturnExpr(call, call.Type);
                info.retExpr = retExpr;
                GenTree use = retExpr;
                if (returnMode == 3)
                {
                    use = compiler.gtNewBinaryNode(GT_COMMA, TYP_INT, retExpr, compiler.gtNewIconNode(TYP_INT, 0));
                }

                compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(use));
            }

            if (returnMode == 2)
            {
                existingTemp = AddLocal(compiler, TYP_INT);
                compiler.lvaGetDesc(existingTemp).lvSingleDef = true;
                info.preexistingSpillTemp = existingTemp;
            }

            Assert.That(compiler.fgTransformIndirectCalls(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            var hot = block.FalseTarget;
            var cold = block.TrueTarget;
            Assert.That(hot.Target, Is.SameAs(cold.Target));
            Assert.That(block.FalseEdge.Likelihood, Is.EqualTo(0.8));
            Assert.That(block.TrueEdge.Likelihood, Is.EqualTo(0.2).Within(1e-12));
            Assert.That(hot.bbWeight, Is.EqualTo(80));
            Assert.That(cold.bbWeight, Is.EqualTo(20).Within(1e-12));
            Assert.That(compiler.Metrics.ClassGDV, Is.EqualTo(1));
            Assert.That(call.IsGuardedDevirtualizationCandidate, Is.False);
            Assert.That(call.IsInlineCandidate, Is.False);
            Assert.That(compiler.MethodHasGuardedDevirtualization, Is.True);

            var hotStatements = hot.Statements.ToArray();
            var receiverStore = hotStatements[0].RootNode.AsLclVar();
            Assert.That(compiler.lvaGetDesc(receiverStore.LclNum).lvClassIsExact, Is.True);
            var direct = GetCall(hotStatements[1]);
            Assert.That(direct.IsVirtual, Is.False);
            Assert.That(direct.Flags & GTF_CALL_NULLCHECK, Is.EqualTo(GTF_CALL_NULLCHECK));
            Assert.That(direct.IsInlineCandidate, Is.EqualTo(inlineable));
            Assert.That(direct.Args.ThisArg?.EarlyNode?.AsLclVar().LclNum, Is.EqualTo(receiverStore.LclNum));
            var fallbackStmt = cold.FirstStmt ?? throw new InvalidOperationException();
            Assert.That(GetCall(fallbackStmt), Is.SameAs(call));
            Assert.That(call.IsVirtual, Is.True);

            var needsReturnTemp = returnMode is 1 or 2 or 5;
            if (needsReturnTemp)
            {
                Assert.That(retExpr, Is.Not.Null);
                var replacement = retExpr?.SubstExpr ?? throw new InvalidOperationException();
                var temp = replacement.AsLclVar().LclNum;
                Assert.That(fallbackStmt.RootNode.AsLclVar().LclNum, Is.EqualTo(temp));
                Assert.That(compiler.lvaGetDesc(temp).Type, Is.EqualTo(returnMode == 5 ? TYP_BYTE : TYP_INT));
                if (returnMode == 2)
                {
                    Assert.That(temp, Is.EqualTo(existingTemp));
                    Assert.That(compiler.lvaGetDesc(temp).lvSingleDef, Is.False);
                }

                if (inlineable)
                {
                    Assert.That(info.preexistingSpillTemp, Is.EqualTo(temp));
                    Assert.That(hotStatements[2].RootNode.AsLclVar().LclNum, Is.EqualTo(temp));
                    Assert.That(hotStatements[2].RootNode.AsLclVar().Data, Is.SameAs(info.retExpr));
                }
                else
                {
                    Assert.That(hotStatements[1].RootNode.AsLclVar().LclNum, Is.EqualTo(temp));
                }
            }
            else if (retExpr is not null)
            {
                Assert.That(retExpr.SubstExpr?.Oper, Is.EqualTo(GT_NOP));
                Assert.That(fallbackStmt.RootNode, Is.SameAs(call));
            }

            if (inlineable && (retExpr is not null))
            {
                Assert.That(info.retExpr, Is.Not.SameAs(retExpr));
                Assert.That(info.retExpr?.InlineCandidate, Is.SameAs(direct));
            }
            else
            {
                Assert.That(info.retExpr, Is.SameAs(retExpr));
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void MultipleGuessesPreserveConditionalMassAndExactFallthrough(bool exact)
    {
        WithCompiler(compiler => {
            var block = compiler.fgFirstBB ?? throw new InvalidOperationException();
            block.setBBProfileWeight(100);
            var call = CreateGdvCall(compiler, TYP_INT, [50, 30, exact ? 20 : 10]);
            if (exact)
            {
                CallMoreFlags(call) |= GenTreeCallFlags.GTF_CALL_M_GUARDED_DEVIRT_EXACT;
            }

            var returnTemp = AddLocal(compiler, TYP_INT);
            compiler.lvaGetDesc(returnTemp).lvSingleDef = true;
            call.GetGdvCandidateInfo(1).preexistingSpillTemp = returnTemp;
            call.GetGdvCandidateInfo(1).isInlineable = true;
            call.GetGdvCandidateInfo(2).isInlineable = true;
            var retExpr = compiler.gtNewInlineCandidateReturnExpr(call, TYP_INT);
            for (byte i = 0; i < 3; i++)
            {
                call.GetGdvCandidateInfo(i).retExpr = retExpr;
            }

            compiler.ImpEnumeratorGdvLocalMap[call] = 0;
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(call));
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(retExpr));

            Assert.That(compiler.fgTransformIndirectCalls(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(retExpr.SubstExpr?.AsLclVar().LclNum, Is.EqualTo(returnTemp));
            Assert.That(compiler.lvaGetDesc(returnTemp).lvSingleDef, Is.False);
            var check = block;
            BasicBlock? lastHot = null;
            GenTreeCall? firstInlineable = null;
            double[] weights = [50, 30, exact ? 20 : 10];
            double[] conditional = [0.5, 0.6, exact ? 1.0 : 0.5];

            for (var i = 0; i < 3; i++)
            {
                var hot = (exact && (i == 2)) ? check.Target : check.FalseTarget;
                var edge = (exact && (i == 2)) ? check.TargetEdge : check.FalseEdge;
                Assert.That(edge.Likelihood, Is.EqualTo(conditional[i]).Within(1e-12));
                Assert.That(hot.bbWeight, Is.EqualTo(weights[i]).Within(1e-12));
                var direct = GetCall(hot.Statements.ElementAt(1));
                Assert.That(direct.IsInlineCandidate, Is.EqualTo(i != 0));
                if (i == 1)
                {
                    firstInlineable = direct;
                }

                lastHot = hot;
                if (i < 2)
                {
                    check = check.TrueTarget;
                }
            }

            Assert.That(firstInlineable, Is.Not.Null);
            Assert.That(compiler.ImpEnumeratorGdvLocalMap.ContainsKey(call), Is.False);
            Assert.That(compiler.ImpEnumeratorGdvLocalMap.Single().Key, Is.SameAs(firstInlineable));
            var fallback = lastHot?.Next ?? throw new InvalidOperationException();
            Assert.That(GetCall(fallback.FirstStmt ?? throw new InvalidOperationException()), Is.SameAs(call));
            Assert.That(fallback.bbWeight, Is.EqualTo(exact ? 0 : 10).Within(1e-12));
            Assert.That(fallback.bbRefs, Is.EqualTo(exact ? 0 : 1));
            Assert.That(check.Kind, Is.EqualTo(exact ? BBJ_ALWAYS : BBJ_COND));
            Assert.That(compiler.Metrics.ClassGDV, Is.EqualTo(exact ? 2 : 3));
            Assert.That(compiler.Metrics.NoInlineGDV, Is.EqualTo(1));
        });
    }

    [TestCase(false, IAT_VALUE)]
    [TestCase(false, IAT_PVALUE)]
    [TestCase(false, IAT_RELPVALUE)]
    [TestCase(true, IAT_VALUE)]
    [TestCase(true, IAT_PVALUE)]
    [TestCase(true, IAT_RELPVALUE)]
    public static void MethodAndDelegateGuessesUseNativeTargetLookups(bool isDelegate, InfoAccessType access)
    {
        WithCompiler(compiler => {
            s_lookupAccess = access;
            s_chunkOffset = 16;
            s_slotOffset = 24;
            s_relative = false;
            var block = compiler.fgFirstBB ?? throw new InvalidOperationException();
            var call = CreateGdvCall(compiler, TYP_VOID, [80], classGuard: false);
            if (isDelegate)
            {
                call.Flags &= ~GTF_CALL_VIRT_VTABLE;
                CallMoreFlags(call) |= GenTreeCallFlags.GTF_CALL_M_DELEGATE_INV;
            }

            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(call));
            Assert.That(compiler.fgTransformIndirectCalls(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.Metrics.MethodGDV, Is.EqualTo(1));
            var compare = (block.LastStmt ?? throw new InvalidOperationException()).RootNode.AsUnOp().Op1.AsOp();
            Assert.That(compare.Oper, Is.EqualTo(GT_NE));
            Assert.That(compare.Op1.Oper, Is.EqualTo(access == IAT_VALUE ? GT_CNS_INT : access == IAT_PVALUE ? GT_IND : GT_ADD));
            if (access == IAT_RELPVALUE)
            {
                var relative = compare.Op1.AsOp();
                Assert.That(relative.Op1.AsIndir().Addr, Is.Not.SameAs(relative.Op2));
                Assert.That(relative.Op1.AsIndir().Addr.AsIntCon().IconValue, Is.EqualTo(relative.Op2.AsIntCon().IconValue));
            }

            var hot = block.FalseTarget;
            var receiver = (hot.FirstStmt ?? throw new InvalidOperationException()).RootNode.AsLclVar().Data;
            if (isDelegate)
            {
                Assert.That(compare.Op2.AsIndir().Addr.AsOp().Op2.AsIntCon().IconValue, Is.EqualTo((nint)16));
                Assert.That(receiver.AsIndir().Addr.AsOp().Op2.AsIntCon().IconValue, Is.EqualTo((nint)8));
            }
            else
            {
                Assert.That(receiver.Oper, Is.EqualTo(GT_LCL_VAR));
            }

            var direct = GetCall(hot.Statements.ElementAt(1));
            Assert.That(direct.IsDelegateInvoke, Is.False);
            Assert.That(direct.IsVirtual, Is.False);
            Assert.That(GetCall(block.TrueTarget.FirstStmt ?? throw new InvalidOperationException()), Is.SameAs(call));
        });
    }

    [TestCase(false, 0)]
    [TestCase(true, 0)]
    [TestCase(true, 2)]
    public static void PhaseVisitsNewRemaindersAndClearsFatPointerMethodFlag(bool hasFlags, int calls)
    {
        WithCompiler(compiler => {
            var block = compiler.fgFirstBB ?? throw new InvalidOperationException();
            compiler.MethodHasFatPointer = hasFlags;
            if (hasFlags)
            {
                var pointer = AddLocal(compiler, TYP_I_IMPL);
                for (var i = 0; i < calls; i++)
                {
                    var call = CreateFatCall(compiler, compiler.gtNewLclvNode(TYP_I_IMPL, pointer), TYP_VOID);
                    compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(call));
                }
            }

            var result = compiler.fgTransformIndirectCalls();
            Assert.That(result, Is.EqualTo(calls != 0 ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING));
            Assert.That(compiler.MethodHasFatPointer, Is.False);
            Assert.That(compiler.fgBBcount, Is.EqualTo(1 + (calls * 4)));
            Assert.That(compiler.fgTransformIndirectCalls(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public static void ScoutingHonorsInlinePlaceholderAndLikelihoodBoundaries(int barrier)
    {
        WithCompiler(compiler => {
            ChainLikelihood(ref JitConfig) = 75;
            ChainStatements(ref JitConfig) = 1;
            var block = compiler.fgFirstBB ?? throw new InvalidOperationException();
            var first = CreateGdvCall(compiler, barrier == 2 ? TYP_INT : TYP_VOID, [barrier == 3 ? 70 : 80]);
            var unrelated = compiler.gtNewCallNode(TYP_INT, gtCallTypes.CT_USER_FUNC, (CORINFO_METHOD_STRUCT_*)0x3000);
            var unrelatedInfo = new InlineCandidateInfo { preexistingSpillTemp = BAD_VAR_NUM };
            unrelated.SingleInlineCandidateInfo = unrelatedInfo;
            if (barrier == 1)
            {
                compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(unrelated));
            }

            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(first));
            Statement between;
            if (barrier == 0)
            {
                between = compiler.gtNewStmt(unrelated);
            }
            else if (barrier is 1 or 2)
            {
                var owner = barrier == 1 ? unrelated : first;
                var placeholder = compiler.gtNewInlineCandidateReturnExpr(owner, TYP_INT);
                if (barrier == 1)
                {
                    unrelatedInfo.retExpr = placeholder;
                }
                else
                {
                    first.GetGdvCandidateInfo(0).retExpr = placeholder;
                }

                between = compiler.gtNewStmt(placeholder);
            }
            else
            {
                between = compiler.gtNewStmt(compiler.gtNewNothingNode());
            }

            compiler.fgInsertStmtAtEnd(block, between);
            var second = CreateGdvCall(compiler, TYP_VOID, [90]);
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(second));
            Assert.That(compiler.fgTransformIndirectCalls(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.Metrics.ChainedGDV, Is.EqualTo(barrier == 2 ? 1 : 0));
            Assert.That(between.RootNode.Oper, Is.EqualTo(barrier switch {
                0 => GT_CALL,
                1 => GT_RET_EXPR,
                2 => GT_LCL_VAR,
                _ => GT_NOP,
            }));
        });
    }

    [Test]
    public static void UnboxedTargetMismatchKeepsDirectCallWithoutConsumingInlineMapping()
    {
        WithCompiler(compiler => {
            var block = compiler.fgFirstBB ?? throw new InvalidOperationException();
            var call = CreateGdvCall(compiler, TYP_VOID, [80]);
            var info = call.GetGdvCandidateInfo(0);
            info.isInlineable = true;
            info.guardedMethodUnboxedResolvedToken.hMethod = (CORINFO_METHOD_STRUCT_*)0x9000;
            compiler.ImpEnumeratorGdvLocalMap[call] = 0;
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(call));

            Assert.That(compiler.fgTransformIndirectCalls(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            var direct = GetCall(block.FalseTarget.Statements.ElementAt(1));
            Assert.That(direct.IsVirtual, Is.False);
            Assert.That(direct.IsInlineCandidate, Is.False);
            Assert.That(compiler.ImpEnumeratorGdvLocalMap.Single().Key, Is.SameAs(call));
            Assert.That(compiler.Metrics.NoInlineGDV, Is.Zero);
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public static void ChainingCopiesOnlyPermittedStatementsAndRepairsProfile(int interveningStatements)
    {
        WithCompiler(compiler => {
            ChainLikelihood(ref JitConfig) = 75;
            ChainStatements(ref JitConfig) = 1;
            var block = compiler.fgFirstBB ?? throw new InvalidOperationException();
            block.setBBProfileWeight(100);
            var first = CreateGdvCall(compiler, TYP_VOID, [80]);
            first.GetGdvCandidateInfo(0).isInlineable = true;
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(first));
            var local = AddLocal(compiler, TYP_INT);
            for (var i = 0; i < interveningStatements; i++)
            {
                compiler.fgInsertStmtAtEnd(block,
                    compiler.gtNewStmt(compiler.gtNewStoreLclVarNode(local, compiler.gtNewIconNode(TYP_INT, i))));
            }

            var second = CreateGdvCall(compiler, TYP_VOID, [90]);
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(second));
            var chained = interveningStatements <= 1;
            Assert.That(compiler.fgTransformIndirectCalls(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.Metrics.ChainedGDV, Is.EqualTo(chained ? 1 : 0));
            var firstHot = block.FalseTarget;
            var firstCold = block.TrueTarget;
            var secondCheck = firstHot.Target;
            var secondHot = secondCheck.FalseTarget;
            var secondCold = secondCheck.TrueTarget;
            Assert.That(firstCold.Target, Is.SameAs(chained ? secondCold : secondCheck));
            Assert.That(secondCheck.bbWeight, Is.EqualTo(chained ? 80 : 100).Within(1e-12));
            Assert.That(secondHot.bbWeight, Is.EqualTo(chained ? 72 : 90).Within(1e-12));
            Assert.That(secondCold.bbWeight, Is.EqualTo(chained ? 28 : 10).Within(1e-12));

            if (chained && (interveningStatements == 1))
            {
                var hotCopy = firstHot.LastStmt ?? throw new InvalidOperationException();
                var coldCopy = firstCold.LastStmt ?? throw new InvalidOperationException();
                Assert.That(hotCopy.RootNode.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
                Assert.That(coldCopy.RootNode.AsLclVar().LclNum, Is.EqualTo(local));
                Assert.That(hotCopy.RootNode.AsLclVar().LclNum, Is.EqualTo(local));
                Assert.That(hotCopy.RootNode, Is.Not.SameAs(coldCopy.RootNode));
            }
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_callMoreFlags")]
    private static extern ref GenTreeCallFlags CallMoreFlags(GenTreeCall call);

    private static GenTreeCall CreateGdvCall(Compiler compiler, var_types type, int[] likelihoods, bool classGuard = true)
    {
        if (compiler.lvaCount == 0)
        {
            _ = AddLocal(compiler, TYP_REF);
        }

        var call = compiler.gtNewCallNode(type, gtCallTypes.CT_USER_FUNC, (CORINFO_METHOD_STRUCT_*)0x1000);
        call.Flags |= GTF_CALL_VIRT_VTABLE;
        _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewLclvNode(TYP_REF, 0))
            .WithWellKnownArg(WellKnownArg.ThisPointer));
        for (var i = 0; i < likelihoods.Length; i++)
        {
            var method = (CORINFO_METHOD_STRUCT_*)(0x2000 + (i * 16));
            var cls = (CORINFO_CLASS_STRUCT_*)(0x4000 + (i * 16));
            call.AddGdvCandidateInfo(compiler, new InlineCandidateInfo {
                guardedClassHandle = classGuard ? cls : null,
                guardedMethodHandle = method,
                exactContextHandle = (CORINFO_CONTEXT_STRUCT_*)((nint)cls | 1),
                likelihood = likelihoods[i],
                preexistingSpillTemp = BAD_VAR_NUM,
                originalMethodHandle = (CORINFO_METHOD_STRUCT_*)0x1000,
                guardedMethodResolvedToken = new CORINFO_RESOLVED_TOKEN { hMethod = method, hClass = cls },
            });
        }

        compiler.MethodHasGuardedDevirtualization = true;
        return call;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* GetMethodClass(ICorJitInfo* self, CORINFO_METHOD_STRUCT_* method)
        => (CORINFO_CLASS_STRUCT_*)(0x4000 + ((nint)method & 0xFF0));

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoFlag GetMethodAttribs(ICorJitInfo* self, CORINFO_METHOD_STRUCT_* method) => 0;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void GetMethodSig(ICorJitInfo* self, CORINFO_METHOD_STRUCT_* method, CORINFO_SIG_INFO* sig, CORINFO_CLASS_STRUCT_* parent)
    {
        *sig = new CORINFO_SIG_INFO { retType = CorInfoType.CORINFO_TYPE_INT };
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte IsValueClass(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* cls) => 0;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetExactClasses(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* cls, int count, CORINFO_CLASS_STRUCT_** exact)
    {
        *exact = cls;
        return 1;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* EmbedClassHandle(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* cls, void** indirection)
    {
        *indirection = null;
        return cls;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void GetFunctionEntryPoint(ICorJitInfo* self, CORINFO_METHOD_STRUCT_* method, CORINFO_CONST_LOOKUP* lookup, CORINFO_ACCESS_FLAGS flags)
    {
        *lookup = new CORINFO_CONST_LOOKUP { accessType = s_lookupAccess, addr = (void*)0x8000 };
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void GetFunctionFixedEntryPoint(ICorJitInfo* self, CORINFO_METHOD_STRUCT_* method, byte unsafePointer, CORINFO_CONST_LOOKUP* lookup)
    {
        *lookup = new CORINFO_CONST_LOOKUP { accessType = s_lookupAccess, addr = (void*)0x8000 };
    }
}
