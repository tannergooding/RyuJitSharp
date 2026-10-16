// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CSELiveAcrossCallCostTests
{
    private static bool SupportsTarget =>
        Configuration.FEATURE_SIMD && (Configuration.TARGET_AMD64 || Configuration.TARGET_ARM64);

    private static IEnumerable<TestCaseData> CallCrossingCostCases()
    {
        if (!SupportsTarget)
        {
            yield return new TestCaseData(TYP_INT, true);
            yield break;
        }

        yield return new TestCaseData(TYP_INT, true);
        if (Configuration.FEATURE_MASKED_HW_INTRINSICS)
        {
            yield return new TestCaseData(TYP_MASK, Configuration.TARGET_ARM64);
        }

        if (Configuration.TARGET_AMD64 && Configuration.UNIX_AMD64_ABI)
        {
            yield return new TestCaseData(TYP_FLOAT, false);
            yield return new TestCaseData(TYP_SIMD8, false);
            yield return new TestCaseData(TYP_SIMD16, false);
        }
        else if (Configuration.TARGET_AMD64)
        {
            yield return new TestCaseData(TYP_FLOAT, true);
            yield return new TestCaseData(TYP_SIMD8, true);
            yield return new TestCaseData(TYP_SIMD16, true);
        }
        else
        {
            yield return new TestCaseData(TYP_FLOAT, true);
            yield return new TestCaseData(TYP_SIMD8, true);
            yield return new TestCaseData(TYP_SIMD16, false);
        }

        if (Configuration.TARGET_AMD64)
        {
            yield return new TestCaseData(TYP_SIMD32, false);
            yield return new TestCaseData(TYP_SIMD64, false);
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "aggressiveRefCnt")]
    private static extern ref double AggressiveCutoff(CSE_Heuristic heuristic);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "enregCountInt")]
    private static extern ref int IntegerRegisterUse(CSE_Heuristic heuristic);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "enregCountFlt")]
    private static extern ref int FloatingRegisterUse(CSE_Heuristic heuristic);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "enregCountMsk")]
    private static extern ref int MaskRegisterUse(CSE_Heuristic heuristic);

    [TestCaseSource(nameof(CallCrossingCostCases))]
    public static void CallCrossingChangesProfitabilityAtTheSpillCostBoundary(
        var_types expressionType, bool keepsValueInCalleeSavedRegister)
    {
        if (!SupportsTarget)
        {
            Assert.Ignore("CSE call-crossing costs require SIMD on AMD64 or ARM64.");
        }

        WithCompiler(compiler => {
            compiler.lvaTable = [new LclVarDsc { Type = expressionType }];
            compiler.lvaCount = 1;
            var heuristic = new CSE_Heuristic(compiler);
            heuristic.Initialize();
            AggressiveCutoff(heuristic) = 1;
            // Exclude the independent caller-save pressure surcharge from the spill-cost boundary.
            IntegerRegisterUse(heuristic) = 20;
            FloatingRegisterUse(heuristic) = 20;
            MaskRegisterUse(heuristic) = 20;

            Assert.Multiple(() => {
                Assert.That(Promotes(compiler, heuristic, expressionType, 3, liveAcrossCall: false),
                    Is.True, "without a call, def and use each cost one");
                Assert.That(Promotes(compiler, heuristic, expressionType, 2, liveAcrossCall: true),
                    Is.EqualTo(keepsValueInCalleeSavedRegister), "two is the no-spill break-even cost");
                Assert.That(Promotes(compiler, heuristic, expressionType, 3, liveAcrossCall: true),
                    Is.EqualTo(keepsValueInCalleeSavedRegister), "three falls between the two cost boundaries");
                Assert.That(Promotes(compiler, heuristic, expressionType, 4, liveAcrossCall: true),
                    Is.True, "four is the spill break-even cost");
            });
        });
    }

    private static bool Promotes(Compiler compiler, CSE_Heuristic heuristic, var_types expressionType,
        byte expressionCost, bool liveAcrossCall)
    {
        var tree = compiler.gtNewLclvNode(expressionType, 0);
        tree.SetCosts(expressionCost, 1);
        var block = BasicBlock.New(compiler, BBJ_RETURN);
        var descriptor = new CSEdsc(tree, compiler.gtNewStmt(tree), block) {
            csdIndex = 1,
            csdDefCount = 1,
            csdUseCount = 1,
            csdDefWtCnt = 1,
            csdUseWtCnt = 1,
            csdLiveAcrossCall = liveAcrossCall,
        };
        var candidate = new CSE_Candidate(heuristic, descriptor);
        candidate.InitializeCounts();
        return heuristic.PromotionCheck(candidate);
    }

    internal static void WithCompiler(Action<Compiler> action)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
        using var tls = TestConfiguration.CreateJitTls(&jitInfo);
        var previousCompiler = JitTls.Compiler;
        var previousConfig = JitConfig;
        JitConfig = new JitConfigValues();
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info.compCompHnd = &jitInfo;
        compiler.compHndBBtab = [];
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.opts.compCodeOpt = Compiler.BLENDED_CODE;
        if (TestConfiguration.IsDebug)
        {
            compiler.lvaRefCountState = RefCountState.RCS_NORMAL;
            TestConfiguration.EnableSafeBasicBlockCreation(compiler);
            TestConfiguration.EnableSafeFlowEdgeCreation(compiler);
        }
        TestConfiguration.SetCompilerFullName(compiler, nameof(CSELiveAcrossCallCostTests));
        JitTls.Compiler = compiler;
        TestConfiguration.SetJitTlsLogCompiler(compiler);
        try
        {
            action(compiler);
        }
        finally
        {
            JitConfig = previousConfig;
            JitTls.Compiler = previousCompiler;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression) => 0;
}
