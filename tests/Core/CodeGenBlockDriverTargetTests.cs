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
#if TARGET_ARM64
    [Test]
    public static void DriverInitializesBeforeTheUnsupportedBlockEndBoundary()
    {
        WithDriver((compiler, codeGen, block) =>
        {
            var failure = Assert.Throws<FatalJitException>(codeGen.genCodeForBBlist) ??
                throw new AssertionException("Missing block-end dependency failure.");

            Assert.That(failure.Message, Is.EqualTo("Block-end generation requires Windows AMD64."));
#if DEBUG
            Assert.That(compiler.fgSafeBasicBlockCreation, Is.False);
#endif
            Assert.That(block.HasFlag(BBF_HAS_LABEL), Is.True);
            Assert.That(VarSetOps.IsEmpty(compiler, compiler.compCurLife), Is.True);
#if DEBUG
            Assert.That(codeGen.IsGcTypeFixed, Is.True);
#endif
        });
    }
#endif

#if TARGET_WASM
    [Test]
    public static void DriverMarksFuncletEntriesBeforeTheUnsupportedStackInitializationBoundary()
    {
        WithDriver((compiler, codeGen, block) =>
        {
            var failure = Assert.Throws<FatalJitException>(codeGen.genCodeForBBlist) ??
                throw new AssertionException("Missing Wasm control-flow stack dependency failure.");

            Assert.That(failure.Message, Is.EqualTo("Wasm control-flow stack initialization is not ported."));
#if DEBUG
            Assert.That(compiler.fgSafeBasicBlockCreation, Is.False);
#endif
            Assert.That(block.HasFlag(BBF_HAS_LABEL), Is.True);
            Assert.That(VarSetOps.IsEmpty(compiler, compiler.compCurLife), Is.True);
        });
    }
#endif

    private static void WithDriver(Action<Compiler, CodeGen, BasicBlock> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
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
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
#endif
        compiler.compHndBBtab = [];
        compiler.fgFuncletsCreated = true;
        compiler.compFuncInfos = [new FuncInfoDsc { funKind = FuncKind.FUNC_ROOT }];
        compiler.compFuncInfoCount = 1;
        compiler.compCurLife = VarSetOps.MakeSingleton(compiler, 0);
        compiler.genIPmappings = [];
        compiler.genRichIPmappings = [];
        compiler.compRetTypeDesc.InitializeReturnType(compiler, TYP_VOID, null, compiler.info.compCallConv);
        JitTls.Compiler = compiler;

#if DEBUG
        var previousEmitterTests = EmitterTests(ref JitConfig);
        EmitterTests(ref JitConfig) = new JitConfigValues.MethodSet(null, null);
#endif
        try
        {
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            codeGen.Emitter.emitBegCG(compiler, default);
            codeGen.Emitter.Init();
            codeGen.Emitter.emitBegFN(true
#if DEBUG
                , false
#endif
                );
#if HAS_FIXED_REGISTER_SET
            compiler.lvaTable[0].RegNum = REG_STK;
            Allocator(compiler) = new LinearScan(compiler);
#endif
            codeGen.RegSet.rsClearRegsModified();
            action(compiler, codeGen, block);
        }
        finally
        {
#if DEBUG
            EmitterTests(ref JitConfig) = previousEmitterTests;
#endif
            JitTls.Compiler = previous;
        }
    }

#if HAS_FIXED_REGISTER_SET
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_regAlloc")]
    private static extern ref IRegAlloc? Allocator(Compiler compiler);
#endif

#if DEBUG
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitEmitUnitTests")]
    private static extern ref JitConfigValues.MethodSet EmitterTests(ref JitConfigValues config);
#endif
}
