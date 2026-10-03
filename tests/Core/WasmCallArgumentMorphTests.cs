// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_WASM
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.gtCallTypes;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class WasmCallArgumentMorphTests
{
    [TestCase(0u, WasmValueType.I32)]
    [TestCase(21u, WasmValueType.I64)]
    [TestCase(100_000u, WasmValueType.V128)]
    public static void StackPointerRegisterIndexUsesTheWasmLocalIndex(uint index, WasmValueType type)
    {
        WithCompiler(compiler => {
            compiler.compFuncInfos = [new FuncInfoDsc {
                funStackPointerReg = regNumberExtensions.MakeWasmReg(index, type),
            }];
            compiler.compFuncInfoCount = 1;
            var codeGen = new CodeGen(compiler);

            Assert.That(codeGen.GetStackPointerRegIndex(), Is.EqualTo(index));
        });
    }

    [Test]
    public static void UnwindableFrameHelperMarksCurrentFunction()
    {
        WithCompiler(compiler => {
            compiler.compFuncInfos = [new FuncInfoDsc { funKind = FuncKind.FUNC_ROOT }];
            compiler.compFuncInfoCount = 1;
            compiler.fgFuncletsCreated = true;
            var codeGen = new CodeGen(compiler);

            codeGen.ensureCurrentFuncIsUnwindable();

            Assert.That(compiler.compFuncInfos[0].needsUnwindableFrame, Is.True);
        });
    }

    [Test]
    public static void UnportedWasmPrologEmitterOperationsFailExplicitly()
    {
        WithCompiler(compiler => {
            var emitter = new CodeGen(compiler).Emitter;

            Assert.Throws<FatalJitException>(() =>
                emitter.emitIns_I_Ty(instruction.INS_local_decl, 1, WasmValueType.I32, 0));
            Assert.Throws<FatalJitException>(() =>
                emitter.emitIns_S(instruction.INS_i32_store, EA_4BYTE, 0, 0));
            Assert.Throws<FatalJitException>(() =>
                emitter.emitFuncletAddressConstant((nint)0));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ShadowStackArgumentPrecedesClassificationOnlyForManagedCalls(bool unmanaged)
    {
        WithCompiler(compiler => {
            compiler.lvaWasmSpArg = 1;
            compiler.lvaTable[0].Type = TYP_INT;
            compiler.lvaTable[1].Type = TYP_I_IMPL;
            var call = compiler.gtNewCallNode(TYP_VOID, CT_USER_FUNC, null);
            if (unmanaged)
            {
                call.Flags |= GTF_CALL_UNMANAGED;
            }

            var userNode = compiler.gtNewLclAddrNode(TYP_BYREF, 0, 0);
            var userArgument = call.Args.PushBack(NewCallArg.CreateForPrimitive(userNode));
            call.Args.AddFinalArgsAndDetermineAbiInfo(compiler, call);

            var shadowStack = call.Args.FindWellKnownArg(WellKnownArg.WasmShadowStackPointer);
            Assert.That(shadowStack is not null, Is.EqualTo(!unmanaged));
            Assert.That(call.Args.IsAbiInformationDetermined, Is.True);
            Assert.That(call.Args.CountArgs(), Is.EqualTo(unmanaged ? 1 : 2));
            Assert.That(call.Args.OutgoingArgsStackSize, Is.Zero);
            Assert.That(userArgument.AbiInfo.HasExactlyOneRegisterSegment, Is.True);
            if (shadowStack is not null)
            {
                Assert.That(call.Args.Head, Is.SameAs(shadowStack));
                Assert.That(shadowStack.Node.Type, Is.EqualTo(TYP_I_IMPL));
                Assert.That(shadowStack.Node.AsLclVar().LclNum, Is.EqualTo(compiler.lvaWasmSpArg));
                Assert.That(shadowStack.Next, Is.SameAs(userArgument));
                Assert.That(shadowStack.AbiInfo.HasExactlyOneRegisterSegment, Is.True);
                Assert.That(regNumberExtensions.WasmRegToIndex(shadowStack.AbiInfo.Segments[0].Register), Is.Zero);
                Assert.That(regNumberExtensions.WasmRegToIndex(userArgument.AbiInfo.Segments[0].Register), Is.EqualTo(1));
            }
            else
            {
                Assert.That(userNode.Type, Is.EqualTo(TYP_I_IMPL));
                Assert.That(regNumberExtensions.WasmRegToIndex(userArgument.AbiInfo.Segments[0].Register), Is.Zero);
            }
        });
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info.compRetBuffArg = BAD_VAR_NUM;
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.lvaTable = new LclVarDsc[3];
        compiler.lvaCount = 3;
        compiler.virtualStubParamInfo = new Compiler.VirtualStubParamInfo();
        JitTls.Compiler = compiler;

        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
#endif
