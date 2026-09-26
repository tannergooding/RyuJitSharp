// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
#if DEBUG
using System.IO;
using System.Text;
#endif
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.LsraGlobals;
using static RyuJitSharp.bbCatchType;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LinearScanMinimalCandidatesTests
{
    [Test]
    public static void MinimalCandidatePreparationLeavesEveryLocalOnTheStack()
    {
        WithCompiler(compiler => {
            compiler.lvaTable = [
                new() { Type = TYP_INT, RegNum = REG_RAX, lvLRACandidate = true },
                new() { Type = TYP_REF, RegNum = REG_RCX, lvLRACandidate = true },
                new() { Type = TYP_SIMD16, RegNum = REG_XMM0, lvLRACandidate = true },
            ];
            compiler.lvaCount = compiler.lvaTable.Length;
            var allocator = new LinearScan(compiler) {
                localVarIntervals = [new Interval(TYP_INT, SRBM_RAX)],
            };

            IdentifyCandidatesMinimal(allocator);

            Assert.That(allocator.localVarIntervals, Is.Null);
            foreach (var local in compiler.lvaTable)
            {
                Assert.That(local.RegNum, Is.EqualTo(REG_STK));
                Assert.That(local.lvLRACandidate, Is.False);
            }
        });
    }

    [Test]
    public static void EmptyMinimalCandidatePreparationReturnsWithoutChangingMappings()
    {
        WithCompiler(compiler => {
            compiler.lvaTable = [];
            compiler.lvaCount = 0;
            var existingMappings = new Interval?[1];
            var allocator = new LinearScan(compiler) {
                localVarIntervals = existingMappings,
            };

            IdentifyCandidatesMinimal(allocator);

            Assert.That(allocator.localVarIntervals, Is.SameAs(existingMappings));
        });
    }

    [Test]
    public static void MinimalCandidatePreparationBuildsExceptionAndFinallyVariableSets()
    {
        WithCompiler((compiler, allocator) => {
            compiler.lvaTable = [
                new() { Type = TYP_INT, _varIndex = 0, lvTracked = true, lvLRACandidate = true },
                new() {
                    Type = TYP_REF,
                    _varIndex = 1,
                    lvTracked = true,
                    lvLRACandidate = true,
                    lvMustInit = true,
                },
            ];
            compiler.lvaCount = 2;
            compiler.lvaTrackedCount = 2;
            compiler.lvaTrackedCountInSizeTUnits = 1;
            compiler.compHndBBtabCount = 1;
            compiler.fgBBVarSetsInited = true;
            LiveInOutOfHandler(ref compiler.lvaTable[0], true);
            LiveInOutOfHandler(ref compiler.lvaTable[1], true);

            var handler = BasicBlock.New(compiler, BBJ_ALWAYS);
            handler.CatchType = BBCT_FINALLY;
            var finallyReturn = BasicBlock.New(compiler, BBJ_EHFINALLYRET);
            handler.Next = finallyReturn;
            finallyReturn.Prev = handler;
            compiler.fgFirstBB = handler;
            compiler.fgLastBB = finallyReturn;
            compiler.fgBBNumMax = finallyReturn.bbNum;
            handler.bbLiveIn = new nint[1];
            handler.bbLiveOut = new nint[1];
            finallyReturn.bbLiveIn = new nint[1];
            finallyReturn.bbLiveOut = new nint[1];
            AddTrackedVar(handler.bbLiveIn, 0);
            AddTrackedVar(finallyReturn.bbLiveOut, 1);

#if DEBUG
            compiler.verbose = true;
            var output = Capture(() => IdentifyCandidatesMinimal(allocator));
#else
            IdentifyCandidatesMinimal(allocator);
#endif

            Assert.That(ContainsTrackedVar(ExceptVars(allocator), 0), Is.True);
            Assert.That(ContainsTrackedVar(ExceptVars(allocator), 1), Is.True);
            Assert.That(ContainsTrackedVar(FinallyVars(allocator), 0), Is.False);
            Assert.That(ContainsTrackedVar(FinallyVars(allocator), 1), Is.True);
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_STK));
            Assert.That(compiler.lvaTable[1].RegNum, Is.EqualTo(REG_STK));
#if DEBUG
            Assert.That(output, Is.EqualTo(
                $"EH Vars: {{V00 V01}}{Environment.NewLine}" +
                $"Finally Vars: {{V01}}{Environment.NewLine}{Environment.NewLine}{Environment.NewLine}" +
                $"FP callee save candidate vars: None{Environment.NewLine}{Environment.NewLine}" +
                $"floatVarCount = 0; hasLoops = false, singleExit = true{Environment.NewLine}"));
#endif
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "identifyCandidatesMinimal")]
    private static extern void IdentifyCandidatesMinimal(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_exceptVars")]
    private static extern ref nint[] ExceptVars(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_finallyVars")]
    private static extern ref nint[] FinallyVars(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set__lvLiveInOutOfHandler")]
    private static extern void LiveInOutOfHandler(ref LclVarDsc local, bool value);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllInt")]
    private static extern ref regMask CompilerAllIntRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllFloat")]
    private static extern ref regMask CompilerAllFloatRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllMask")]
    private static extern ref regMask CompilerAllMaskRegs(Compiler compiler);

    internal static void WithCompiler(Action<Compiler> action, bool minOpts = true)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(minOpts);
        CompilerAllIntRegs(compiler) = SRBM_ALLINT_INIT;
        CompilerAllFloatRegs(compiler) = SRBM_ALLFLOAT_INIT;
        CompilerAllMaskRegs(compiler) = SRBM_ALLMASK_INIT;
        JitTls.Compiler = compiler;
        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
        codeGen.CopyRegisterInfo();
        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    private static void WithCompiler(Action<Compiler, LinearScan> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
#endif
        CompilerAllIntRegs(compiler) = SRBM_ALLINT_INIT;
        CompilerAllFloatRegs(compiler) = SRBM_ALLFLOAT_INIT;
        CompilerAllMaskRegs(compiler) = SRBM_ALLMASK_INIT;
        JitTls.Compiler = compiler;
        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
        codeGen.CopyRegisterInfo();
        try
        {
            action(compiler, new LinearScan(compiler));
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

#if DEBUG
    private static string Capture(Action action)
    {
        using var stream = new MemoryStream();
        using var writer = new JitTextWriter(stream, leaveOpen: true);
        var previous = s_jitstdout;
        try
        {
            s_jitstdout = writer;
            action();
            writer.Flush();
        }
        finally
        {
            s_jitstdout = previous;
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
#endif

    private static void AddTrackedVar(nint[] set, int varIndex)
    {
        var wordBits = IntPtr.Size * 8;
        set[varIndex / wordBits] |= (nint)(1L << (varIndex % wordBits));
    }

    private static bool ContainsTrackedVar(nint[] set, int varIndex)
    {
        var wordBits = IntPtr.Size * 8;
        return (set[varIndex / wordBits] & (nint)(1L << (varIndex % wordBits))) != 0;
    }
}
