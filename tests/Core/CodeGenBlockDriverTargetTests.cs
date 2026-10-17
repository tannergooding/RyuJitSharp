// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CodeGenBlockDriverTargetTests
{
    [Test]
    public static void DriverInitializesBeforeBlockEndEmission()
    {
        Assume.That(Configuration.TARGET_ARM64);

        WithDriver((compiler, codeGen, block) =>
        {
            codeGen.genCodeForBBlist();
            if (TestConfiguration.IsDebug)
            {
                Assert.That(compiler.fgSafeBasicBlockCreation, Is.False);
            }
            Assert.That(block.HasFlag(BBF_HAS_LABEL), Is.True);
            Assert.That(VarSetOps.IsEmpty(compiler, compiler.compCurLife), Is.True);
            if (TestConfiguration.IsDebug)
            {
                Assert.That(codeGen.IsGcTypeFixed, Is.True);
            }
        });
    }

    [Test]
    public static void DriverMarksLabelsForAnEmptyWasmFunction()
    {
        Assume.That(Configuration.TARGET_WASM);

        WithDriver((compiler, codeGen, block) =>
        {
            codeGen.genCodeForBBlist();
            if (TestConfiguration.IsDebug)
            {
                Assert.That(compiler.fgSafeBasicBlockCreation, Is.False);
            }
            Assert.That(block.HasFlag(BBF_HAS_LABEL), Is.True);
            Assert.That(VarSetOps.IsEmpty(compiler, compiler.compCurLife), Is.True);
        });
    }

    private static void WithDriver(Action<Compiler, CodeGen, BasicBlock> action)
    {
        using var tls = TestConfiguration.CreateJitTls();
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        CORINFO_METHOD_INFO methodInfo = default;
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.info.compMethodInfo = &methodInfo;
        compiler.opts.SetMinOpts(true);
        compiler.lvaCount = 1;
        compiler.lvaTrackedCount = 1;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        compiler.lvaTrackedToVarNum = [0];
        compiler.lvaTable = [new LclVarDsc { Type = TYP_INT, lvTracked = true, _varIndex = 0 }];
        compiler.lvaDoneFrameLayout = Compiler.INITIAL_FRAME_LAYOUT;

        var block = new BasicBlock(null, null)
        {
            bbLiveIn = VarSetOps.MakeEmpty(compiler),
            bbLiveOut = VarSetOps.MakeEmpty(compiler),
        };
        compiler.fgFirstBB = block;
        compiler.fgLastBB = block;
        compiler.fgBBcount = 1;
        compiler.fgBBNumMax = 1;
        if (Configuration.TARGET_WASM)
        {
            compiler.fgWasmIntervals = [];
        }
        TestConfiguration.EnableSafeBasicBlockCreation(compiler);
        compiler.compHndBBtab = [];
        compiler.fgFuncletsCreated = true;
        compiler.compFuncInfos = [new FuncInfoDsc { funKind = FuncKind.FUNC_ROOT }];
        compiler.compFuncInfoCount = 1;
        compiler.compCurLife = VarSetOps.MakeSingleton(compiler, 0);
        compiler.genIPmappings = [];
        compiler.genRichIPmappings = [];
        compiler.compRetTypeDesc.InitializeReturnType(compiler, TYP_VOID, null, compiler.info.compCallConv);
        JitTls.Compiler = compiler;

        JitConfigValues.MethodSet? previousEmitterTests = null;
        if (TestConfiguration.IsDebug)
        {
            previousEmitterTests = EmitterTests(ref JitConfig);
            EmitterTests(ref JitConfig) = new JitConfigValues.MethodSet(null, null);
        }
        try
        {
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            codeGen.Emitter.emitBegCG(compiler, default);
            codeGen.Emitter.Init();
            codeGen.Emitter.emitBegFN(true, false);
            if (Configuration.HAS_FIXED_REGISTER_SET)
            {
                compiler.lvaTable[0].RegNum = REG_ARM64_STK;
            }
            Allocator(compiler) = new LinearScan(compiler);
            codeGen.RegSet.rsClearRegsModified();
            action(compiler, codeGen, block);
        }
        finally
        {
            if (TestConfiguration.IsDebug)
            {
                EmitterTests(ref JitConfig) = previousEmitterTests!;
            }
            JitTls.Compiler = previous;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_regAlloc")]
    private static extern ref IRegAlloc? Allocator(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitEmitUnitTests")]
    private static extern ref JitConfigValues.MethodSet EmitterTests(ref JitConfigValues config);
}
