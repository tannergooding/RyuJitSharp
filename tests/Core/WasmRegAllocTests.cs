// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_WASM
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class WasmRegAllocTests
{
    [Test]
    public static void ResolveReferencesSkipsUnusedTemporaryRegisterBanks()
    {
        WithCompiler(compiler => {
            var allocator = new WasmRegAlloc(compiler);

            Assert.That(allocator.DoRegisterAllocation(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.compRegAllocDone, Is.True);

            var locals = compiler.compFuncInfos[0].funWasmLocalDecls
                ?? throw new AssertionException("Wasm register allocation did not publish local declarations.");
            Assert.That(locals, Has.Count.EqualTo(1));
            Assert.That(locals[0].Type, Is.EqualTo(WasmValueType.I32));
            Assert.That(locals[0].Count, Is.EqualTo(1));
        });
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitTls.Compiler = compiler;
        try
        {
            JitFlags flags = default;
            compiler.opts.jitFlags = &flags;
            compiler.lvaRefCountState = RefCountState.RCS_NORMAL;
            compiler.opts.SetMinOpts(true);
            compiler.lvaTable = [new LclVarDsc { Type = TYP_I_IMPL }];
            compiler.lvaCount = 1;
            compiler.lvaWasmSpArg = 0;
            compiler.lvaWasmResumeIP = BAD_VAR_NUM;
            compiler.lvaTable[0].setLvRefCnt(1);
            compiler.fgFuncletsCreated = true;
            compiler.compFuncInfos = [new FuncInfoDsc { funKind = FuncKind.FUNC_ROOT }];
            compiler.compFuncInfoCount = 1;

            compiler.codeGen = new CodeGen(compiler);
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
#endif
