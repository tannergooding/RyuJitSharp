// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
//
// Based on the RyuJIT compiler from dotnet/runtime, helperexpansion.cpp.

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.CorInfoFlag;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.GenTreeCallFlags;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class LateCastExpansionTests
{
    private static bool s_targetExact;
    private static bool s_candidateExact;
    private static int s_exactCount;
    private static CorInfoFlag s_attributes;
    private static TypeCompareState s_castResult;

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitGuardedDevirtualizationMaxTypeChecks")]
    private static extern ref int MaxTypeChecks(ref JitConfigValues config);

    [TestCase(CORINFO_HELP_CHKCASTCLASS, true)]
    [TestCase(CORINFO_HELP_CHKCASTARRAY, true)]
    [TestCase(CORINFO_HELP_ISINSTANCEOFCLASS, false)]
    [TestCase(CORINFO_HELP_ISINSTANCEOFARRAY, false)]
    public static void ExactTargetsPreserveNullAndSelectThrowOrNullFallback(CorInfoHelpFunc helper, bool throws)
    {
        WithCompiler((compiler, body) => {
            s_targetExact = true;
            var call = NewCall(compiler, helper);
            var original = Append(compiler, body, compiler.gtNewStoreLclVarNode(1, call));

            Assert.That(compiler.fgLateCastExpansion(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            var guards = compiler.Blocks.Where(block => block.Kind is BBJ_COND).ToArray();
            Assert.That(guards, Has.Length.EqualTo(2));
            var nullcheck = guards[0];
            var typecheck = guards[1];
            Assert.That(nullcheck.TrueTarget.FirstStmt, Is.SameAs(original));
            Assert.That(nullcheck.FalseTarget, Is.SameAs(typecheck));
            Assert.That(nullcheck.TrueEdge.Likelihood, Is.EqualTo(0.5));
            Assert.That(typecheck.TrueEdge.Likelihood, Is.EqualTo(throws ? 1 : 0.5));
            Assert.That(typecheck.TrueTarget.Target, Is.SameAs(nullcheck.TrueTarget));
            Assert.That(typecheck.FalseTarget.Kind, Is.EqualTo(throws ? BBJ_THROW : BBJ_ALWAYS));
            Assert.That(call.IsNoReturn, Is.EqualTo(throws));
            Assert.That(original.TreeList.Contains(call), Is.False);
            Assert.That(BasicBlock.sameEHRegion(nullcheck, typecheck.FalseTarget), Is.True);
            Assert.That(typecheck.FalseTarget.bbWeight, Is.EqualTo(throws ? 0 : 25));
            var methodTable = typecheck.LastStmt!.RootNode.AsUnOp().Op1.AsOp().Op1.AsIndir();
            Assert.That(methodTable.CostEx, Is.EqualTo(IND_COST_EX + methodTable.Addr.CostEx));
            Assert.That(methodTable.CostSz, Is.EqualTo(2 + methodTable.Addr.CostSz));
            Assert.That(compiler.fgLateCastExpansion(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
        });
    }

    [TestCase(false, 1)]
    [TestCase(false, 2)]
    [TestCase(false, 3)]
    [TestCase(true, 1)]
    [TestCase(true, 2)]
    [TestCase(true, 3)]
    public static void ExactClassSetsConditionWeightsAndKeepNativeRounding(bool castClass, int count)
    {
        WithCompiler((compiler, body) => {
            s_exactCount = count;
            var helper = castClass ? CORINFO_HELP_CHKCASTINTERFACE : CORINFO_HELP_ISINSTANCEOFINTERFACE;
            _ = Append(compiler, body, compiler.gtNewStoreLclVarNode(1, NewCall(compiler, helper)));
            Assert.That(compiler.fgLateCastExpansion(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            var checks = compiler.Blocks.Where(block => block.Kind is BBJ_COND).Skip(1).ToArray();
            Assert.That(checks, Has.Length.EqualTo(count));
            var probability = (double)(100 / (castClass ? count : count + 1)) / 100;
            double sum = 0;
            for (var i = 0; i < count; i++)
            {
                Assert.That(checks[i].bbWeight, Is.EqualTo(50 * (1 - sum)).Within(1e-10));
                Assert.That(checks[i].TrueEdge.Likelihood, Is.EqualTo(probability / (1 - sum)).Within(1e-12));
                Assert.That(checks[i].TrueTarget, Is.SameAs(checks[0].TrueTarget));
                sum += probability;
            }

            Assert.That(checks[^1].FalseTarget.bbWeight, Is.EqualTo(50 * (1 - sum)).Within(1e-10));
            Assert.That(compiler.fgPgoConsistent, Is.EqualTo(!castClass || count != 3));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void UnknownTargetUsesSharedHandleLocalAndSpecializedFallback(bool nonNull)
    {
        WithCompiler((compiler, body) => {
            var call = NewCall(compiler, CORINFO_HELP_CHKCASTCLASS, unknownTarget: true);
            if (nonNull)
            {
                call._callMoreFlags |= GTF_CALL_M_CAST_OBJ_NONNULL;
            }

            var original = Append(compiler, body, compiler.gtNewStoreLclVarNode(1, call));
            Assert.That(compiler.fgLateCastExpansion(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(call.HelperNum, Is.EqualTo(CORINFO_HELP_CHKCASTCLASS_SPECIAL));
            var checks = compiler.Blocks.Where(block => block.Kind is BBJ_COND).ToArray();
            Assert.That(checks, Has.Length.EqualTo(nonNull ? 1 : 2));
            var typecheck = checks[^1];
            var clsStore = typecheck.FirstStmt!.RootNode.AsLclVar();
            Assert.That(clsStore.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
            Assert.That(clsStore.Data.AsLclVar().LclNum, Is.EqualTo(2));
            Assert.That(call.Args.GetUserArgByIndex(0)!.Node.AsLclVar().LclNum, Is.EqualTo(clsStore.LclNum));
            Assert.That(typecheck.FalseTarget.LastStmt!.TreeList.Contains(call), Is.True);
            Assert.That(original.TreeList.Contains(call), Is.False);
            Assert.That(typecheck.bbWeight, Is.EqualTo(nonNull ? 100 : 50));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ExactIncompatibleInputSkipsTypeChecksAndRepairsProfile(bool nonNull)
    {
        WithCompiler((compiler, body) => {
            compiler.lvaTable[0].lvClassHnd = (CORINFO_CLASS_STRUCT_*)0x3000;
            compiler.lvaTable[0].lvClassIsExact = true;
            s_castResult = TypeCompareState.MustNot;
            var call = NewCall(compiler, CORINFO_HELP_CHKCASTCLASS);
            if (nonNull)
            {
                call._callMoreFlags |= GTF_CALL_M_CAST_OBJ_NONNULL;
            }

            _ = Append(compiler, body, compiler.gtNewStoreLclVarNode(1, call));
            Assert.That(compiler.fgLateCastExpansion(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(call.IsNoReturn, Is.True);
            Assert.That(compiler.Blocks.Count(block => block.Kind is BBJ_COND), Is.EqualTo(nonNull ? 0 : 1));
            Assert.That(compiler.Blocks.Single(block => block.Kind is BBJ_THROW).bbWeight, Is.EqualTo(nonNull ? 100 : 50));
            Assert.That(compiler.fgPgoConsistent, Is.False);
        });
    }

    [TestCase(CORINFO_HELP_CHKCASTCLASS, TypeCompareState.Must, true, true)]
    [TestCase(CORINFO_HELP_CHKCASTCLASS, TypeCompareState.Must, false, true)]
    [TestCase(CORINFO_HELP_ISINSTANCEOFINTERFACE, TypeCompareState.MustNot, false, true)]
    [TestCase(CORINFO_HELP_CHKCASTINTERFACE, TypeCompareState.MustNot, false, false)]
    [TestCase(CORINFO_HELP_ISINSTANCEOFINTERFACE, TypeCompareState.May, false, false)]
    public static void ProfileCandidateControlsFastResultAndFallback(
        CorInfoHelpFunc helper, TypeCompareState relation, bool sameTarget, bool expands)
    {
        WithCompiler((compiler, body) => {
            s_castResult = relation;
            var call = NewCall(compiler, helper);
            ICorJitInfo.PgoInstrumentationSchema schema = new() {
                InstrumentationKind = ICorJitInfo.PgoInstrumentationKind.GetLikelyClass,
                Count = 1,
                ILOffset = 0,
                Other = 80,
            };
            nint handle = sameTarget ? 0x1000 : 0x2000;
            call._inlineContext!.PgoInfo = new PgoInfo { PgoSchema = &schema, PgoSchemaCount = 1, PgoData = (byte*)&handle };
            _ = Append(compiler, body, compiler.gtNewStoreLclVarNode(1, call));

            Assert.That(compiler.fgLateCastExpansion(), Is.EqualTo(expands
                ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING));
            if (expands)
            {
                var check = compiler.Blocks.Where(block => block.Kind is BBJ_COND).Last();
                Assert.That(check.TrueEdge.Likelihood, Is.EqualTo(0.8));
                Assert.That(call.HelperNum, Is.EqualTo(sameTarget ? CORINFO_HELP_CHKCASTCLASS_SPECIAL : helper));
                Assert.That(check.TrueTarget.FirstStmt!.RootNode.Oper, Is.EqualTo(relation is TypeCompareState.MustNot
                    ? GT_STORE_LCL_VAR : GT_NOP));
            }
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    public static void IneligibleInputsLeaveOriginalGraphIntact(int mode)
    {
        WithCompiler((compiler, body) => {
            s_targetExact = mode < 4;
            s_exactCount = mode == 5 ? 1 : 0;
            s_candidateExact = mode != 5;
            var helper = mode >= 4 ? CORINFO_HELP_ISINSTANCEOFINTERFACE : CORINFO_HELP_CHKCASTCLASS;
            var call = NewCall(compiler, helper);
            var original = Append(compiler, body, compiler.gtNewStoreLclVarNode(1, call));
            compiler.MethodHasExpandableCasts = mode != 0;
            if (mode == 1)
            {
                call._callMoreFlags &= ~GTF_CALL_M_CAST_CAN_BE_EXPANDED;
            }
            if (mode == 2)
            {
                body.setBBProfileWeight(0);
            }

            Assert.That(compiler.fgLateCastExpansion(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(body.FirstStmt, Is.SameAs(original));
            Assert.That(compiler.Blocks.Count(), Is.EqualTo(2));
            Assert.That(original.TreeList.Contains(call), Is.True);
        }, minOpts: mode == 3);
    }

    private static GenTreeCall NewCall(Compiler compiler, CorInfoHelpFunc helper, bool unknownTarget = false)
    {
        var cls = unknownTarget ? compiler.gtNewLclvNode(TYP_I_IMPL, 2)
            : compiler.gtNewIconEmbClsHndNode((CORINFO_CLASS_STRUCT_*)0x1000);
        var call = compiler.gtNewHelperCallNode(TYP_REF, helper, cls, compiler.gtNewLclvNode(TYP_REF, 0));
        call._callMoreFlags |= GTF_CALL_M_CAST_CAN_BE_EXPANDED;
        call._inlineContext = (InlineContext)RuntimeHelpers.GetUninitializedObject(typeof(InlineContext));
        return call;
    }

    private static Statement Append(Compiler compiler, BasicBlock block, GenTree tree)
    {
        var stmt = compiler.gtNewStmt(tree);
        compiler.fgInsertStmtAtEnd(block, stmt);
        compiler.gtSetStmtInfo(stmt);
        compiler.fgSetStmtSeq(stmt);
        return stmt;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte IsExactType(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* cls)
        => (cls == (CORINFO_CLASS_STRUCT_*)0x1000 ? s_targetExact : s_candidateExact) ? (byte)1 : (byte)0;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetExactClasses(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* cls, int maximum, CORINFO_CLASS_STRUCT_** classes)
    {
        for (var i = 0; i < s_exactCount; i++)
        {
            classes[i] = (CORINFO_CLASS_STRUCT_*)(0x2000 + (0x1000 * i));
        }
        return s_exactCount;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* EmbedClass(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* cls, void** indirect)
    {
        *indirect = null;
        return cls;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoFlag GetAttributes(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* cls) => s_attributes;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static TypeCompareState CompareTypes(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* from, CORINFO_CLASS_STRUCT_* to)
        => s_castResult;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte UnavailableClassName(ICorJitInfo* self, delegate* unmanaged[Cdecl]<void*, void> callback, void* state)
        => 0;

    private static void WithCompiler(Action<Compiler, BasicBlock> action, bool minOpts = false)
    {
        s_targetExact = false;
        s_candidateExact = true;
        s_exactCount = 0;
        s_attributes = 0;
        s_castResult = TypeCompareState.Must;
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.isExactType = &IsExactType;
        vtable.Base.Base.getExactClasses = &GetExactClasses;
        vtable.Base.Base.getClassAttribs = &GetAttributes;
        vtable.Base.Base.compareTypesForCast = &CompareTypes;
        vtable.Base.Base.runWithSPMIErrorTrap = &UnavailableClassName;
        vtable.Base.embedClassHandle = &EmbedClass;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
#if DEBUG
        using var tls = new JitTls(&jitInfo);
#endif
        var previous = JitTls.Compiler;
        var previousConfig = JitConfig;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
#endif
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(minOpts);
        compiler.compHndBBtab = [];
        compiler.info.compCompHnd = &jitInfo;
        CORINFO_METHOD_INFO methodInfo = default;
        compiler.info.compMethodInfo = &methodInfo;
        compiler.eeInfoInitialized = true;
        compiler.lvaTable = new LclVarDsc[3];
        compiler.lvaCount = 3;
        compiler.lvaTable[0].Type = TYP_REF;
        compiler.lvaTable[1].Type = TYP_REF;
        compiler.lvaTable[2].Type = TYP_I_IMPL;
        compiler.fgNodeThreading = NodeThreading.AllTrees;
        compiler.codeGen = new CodeGen(compiler);
        compiler.fgPgoConsistent = true;
        compiler.MethodHasExpandableCasts = true;
        var entry = BasicBlock.New(compiler, BBJ_ALWAYS);
        var body = BasicBlock.New(compiler, BBJ_RETURN);
        body.RemoveFlags(BBF_INTERNAL);
        body.SetFlags(BBF_IMPORTED);
        entry.bbRefs = 1;
        entry.Next = body;
        body.Prev = entry;
        compiler.fgFirstBB = entry;
        compiler.fgLastBB = body;
        compiler.fgPredsComputed = true;
        JitTls.Compiler = compiler;
        JitConfig = new JitConfigValues();
        MaxTypeChecks(ref JitConfig) = 3;
        entry.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(body, entry));
        body.setBBProfileWeight(100);
        try
        {
            action(compiler, body);
        }
        finally
        {
            JitConfig = previousConfig;
            JitTls.Compiler = previous;
        }
    }
}
