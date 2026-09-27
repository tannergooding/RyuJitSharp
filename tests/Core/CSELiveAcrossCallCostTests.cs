// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if FEATURE_SIMD && (TARGET_AMD64 || TARGET_ARM64)
using System;
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
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "aggressiveRefCnt")]
    private static extern ref double AggressiveCutoff(CSE_Heuristic heuristic);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "enregCountInt")]
    private static extern ref int IntegerRegisterUse(CSE_Heuristic heuristic);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "enregCountFlt")]
    private static extern ref int FloatingRegisterUse(CSE_Heuristic heuristic);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "enregCountMsk")]
    private static extern ref int MaskRegisterUse(CSE_Heuristic heuristic);

    [TestCase(TYP_INT, true)]
#if FEATURE_MASKED_HW_INTRINSICS
#if TARGET_ARM64
    [TestCase(TYP_MASK, true)]
#else
    [TestCase(TYP_MASK, false)]
#endif
#endif
#if TARGET_AMD64 && UNIX_AMD64_ABI
    [TestCase(TYP_FLOAT, false)]
    [TestCase(TYP_SIMD8, false)]
    [TestCase(TYP_SIMD16, false)]
#elif TARGET_AMD64
    [TestCase(TYP_FLOAT, true)]
    [TestCase(TYP_SIMD8, true)]
    [TestCase(TYP_SIMD16, true)]
#else
    [TestCase(TYP_FLOAT, true)]
    [TestCase(TYP_SIMD8, true)]
    [TestCase(TYP_SIMD16, false)]
#endif
#if TARGET_AMD64
    [TestCase(TYP_SIMD32, false)]
    [TestCase(TYP_SIMD64, false)]
#endif
    public static void CallCrossingChangesProfitabilityAtTheSpillCostBoundary(
        var_types expressionType, bool keepsValueInCalleeSavedRegister)
    {
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

    private static void WithCompiler(Action<Compiler> action)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
#if DEBUG
        using var tls = new JitTls(&jitInfo);
#endif
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
#if DEBUG
        compiler.info.compFullName = nameof(CSELiveAcrossCallCostTests);
        compiler.lvaRefCountState = RefCountState.RCS_NORMAL;
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
#endif
        JitTls.Compiler = compiler;
#if DEBUG
        JitTls.LogEnv.Compiler = compiler;
#endif
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
#endif
