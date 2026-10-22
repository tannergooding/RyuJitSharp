// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.gtCallTypes;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class WasmCallArgumentMorphTests
{
    [SetUp]
    public static void RequireWasmTarget()
    {
        Assume.That(Configuration.TARGET_WASM, "This fixture requires the Wasm target.");
    }

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
    public static void WasmLocalDeclarationsAndStoresRecordDescriptors()
    {
        WithCompiler(compiler => {
            compiler.lvaTrackedCount = 1;
            compiler.lvaTrackedCountInSizeTUnits = 1;
            compiler.lvaDoneFrameLayout = Compiler.REGALLOC_FRAME_LAYOUT;
            compiler.lvaTable[0].Type = TYP_INT;
            compiler.lvaTable[0].lvOnFrame = true;
            compiler.lvaTable[0].lvFramePointerBased = true;
            compiler.lvaTable[0].StackOffset = 16;
            compiler.compFuncInfos = [new FuncInfoDsc { funKind = FuncKind.FUNC_ROOT }];
            compiler.compFuncInfoCount = 1;
            compiler.fgFuncletsCreated = true;

            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            var emitter = codeGen.Emitter;
            emitter.emitBegCG(compiler, default);
            emitter.Init();
            emitter.emitBegFN(false, TestConfiguration.IsDebug);

            emitter.emitIns_I_Ty(instruction.INS_local_decl, 1, WasmValueType.I32, 0);
            emitter.emitIns_S(instruction.INS_i32_store, RyuJitSharp.emitAttr.EA_4BYTE, 0, 0);

            var descriptors = CurrentDescriptors(emitter);
            Assert.That(descriptors, Has.Count.EqualTo(2));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(instruction.INS_local_decl));
            Assert.That(Emitter.emitGetLclVarDeclCount(descriptors[0]), Is.EqualTo(1u));
            Assert.That(Emitter.emitGetLclVarDeclType(descriptors[0]), Is.EqualTo(WasmValueType.I32));
            Assert.That(descriptors[1].idIns(), Is.EqualTo(instruction.INS_i32_store));
        });
    }

    [Test]
    public static void NativeAotFuncletAddressUsesAnAbsoluteTableIndex()
    {
        WithCompiler(compiler => {
            compiler.eeInfoInitialized = true;
            compiler.eeInfo.targetAbi = CORINFO_RUNTIME_ABI.CORINFO_NATIVEAOT_ABI;
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_AOT);
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            var emitter = codeGen.Emitter;
            emitter.emitBegCG(compiler, default);
            emitter.Init();
            emitter.emitBegFN(false, TestConfiguration.IsDebug);

            emitter.emitFuncletAddressConstant(17);

            var descriptors = CurrentDescriptors(emitter);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(instruction.INS_i32_const_funcletptr));
        });
    }

    [TestCase(true, GTF_ICON_FTN_ADDR, false)]
    [TestCase(false, GTF_ICON_FTN_ADDR, true)]
    [TestCase(true, GTF_ICON_CLASS_HDL, true)]
    public static void NativeAotKeepsFunctionAddressIndirectionsOutOfMemargs(
        bool nativeAot, GenTreeFlags handleKind, bool expectedContained)
    {
        WithCompiler(compiler => {
            compiler.eeInfoInitialized = true;
            compiler.eeInfo.targetAbi = nativeAot
                ? CORINFO_RUNTIME_ABI.CORINFO_NATIVEAOT_ABI
                : CORINFO_RUNTIME_ABI.CORINFO_CORECLR_ABI;
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_AOT);
            compiler.opts.compReloc = true;
            compiler.SetMinOpts(true);

            var address = compiler.gtNewIconHandleNode(0x1234, handleKind);
            var indir = new GenTreeIndir(GT_IND, TYP_INT, address);
            indir.Flags |= GTF_IND_NONFAULTING;
            compiler.codeGen = new CodeGen(compiler);
            var lowering = new Lowering(compiler, new LinearScan(compiler));

            ContainCheckIndir(lowering, indir);

            Assert.That(address.IsContained, Is.EqualTo(expectedContained));
        });
    }

    [Test]
    public static void GcArrayAddressKeepsTheElementOffsetOutermost()
    {
        WithCompiler(compiler => {
            compiler.SetMinOpts(false);
            compiler.lvaTable[0].Type = TYP_REF;
            compiler.lvaTable[1].Type = TYP_INT;
            var array = compiler.gtNewLclvNode(TYP_REF, 0);
            var index = compiler.gtNewLclvNode(TYP_INT, 1);
            var address = MorphIndexAddress(compiler,
                new GenTreeIndexAddr(array, index, TYP_INT, null, 4, 8, 12, boundsCheck: false));
            var arrayAddress = address as GenTreeArrAddr
                ?? throw new AssertionException("Missing array-address wrapper.");
            var offset = arrayAddress.Addr.AsOp();

            Assert.Multiple(() => {
                Assert.That(offset.Oper, Is.EqualTo(GT_ADD));
                Assert.That(offset.Op2.IsIntegralConst(12), Is.True);
                Assert.That(offset.Op1.Oper, Is.EqualTo(GT_ADD));
                Assert.That(offset.Op1.AsOp().Op1, Is.SameAs(array));
            });
        });
    }

    [Test]
    public static void FatPointerTransformUsesWasmHighBit()
    {
        WithCompiler(compiler => {
            TestConfiguration.EnableSafeBasicBlockCreation(compiler);
            TestConfiguration.EnableSafeFlowEdgeCreation(compiler);
            compiler.compHndBBtab = [];
            compiler.fgPredsComputed = true;
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            block.bbRefs = 0;
            compiler.fgFirstBB = block;
            compiler.fgLastBB = block;
            compiler.lvaTable[0].Type = TYP_I_IMPL;

            var call = compiler.gtNewCallNode(TYP_VOID, CT_INDIRECT, null);
            call.ControlExpr = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            call.IsFatPointerCandidate = true;
            var statement = compiler.gtNewStmt(call);
            compiler.fgInsertStmtAtEnd(block, statement);

            new IndirectCallTransformer.FatPointerCallTransformer(compiler, block, statement).Run();

            var check = block.Target;
            var checkStatement = check.FirstStmt ?? throw new InvalidOperationException();
            var condition = checkStatement.RootNode.AsUnOp().Op1.AsOp();
            var mask = condition.Op1.AsOp().Op2.AsIntCon().IconValue;
            Assert.That(mask, Is.EqualTo((nint)int.MinValue));
        });
    }

    [TestCase(TYP_INT, TYP_FLOAT, RyuJitSharp.instruction.INS_i32_reinterpret_f32)]
    [TestCase(TYP_FLOAT, TYP_INT, RyuJitSharp.instruction.INS_f32_reinterpret_i32)]
    [TestCase(TYP_LONG, TYP_DOUBLE, RyuJitSharp.instruction.INS_i64_reinterpret_f64)]
    [TestCase(TYP_DOUBLE, TYP_LONG, RyuJitSharp.instruction.INS_f64_reinterpret_i64)]
    [TestCase(TYP_BYREF, TYP_REF, RyuJitSharp.instruction.INS_none)]
    [TestCase(TYP_INT, TYP_INT, RyuJitSharp.instruction.INS_none)]
    public static void WasmBitCastInstructionMatchesValueTypes(var_types targetType, var_types sourceType,
        RyuJitSharp.instruction expected)
    {
        Assert.That(CodeGen.WasmBitCastInstruction(targetType, sourceType), Is.EqualTo(expected));
    }

    [TestCase(TYP_BYTE, WasmValueType.I32, TYP_INT)]
    [TestCase(TYP_FLOAT, WasmValueType.F32, TYP_FLOAT)]
    [TestCase(TYP_LONG, WasmValueType.I64, TYP_LONG)]
    public static void WasmParameterStackTypeUsesTheIncomingRegisterType(
        var_types parameterType, WasmValueType registerType, var_types expectedType)
    {
        WithCompiler(compiler => {
            var descriptor = new LclVarDsc { Type = parameterType };
            var segment = AbiPassingSegment.InRegister(
                regNumberExtensions.MakeWasmReg(0, registerType), 0, parameterType.Size);
            var codeGen = new CodeGen(compiler);

            Assert.That(codeGen.genParamStackTypeWasm(in descriptor, in segment), Is.EqualTo(expectedType));
        });
    }

    [Test]
    public static void RegisterParameterHomingLeavesMatchingIncomingLocalInPlace()
    {
        WithCompiler(compiler => {
            compiler.info.compArgsCount = 1;
            var register = regNumberExtensions.MakeWasmReg(0, WasmValueType.F32);
            var segment = AbiPassingSegment.InRegister(register, 0, TYP_FLOAT.Size);
            compiler.lvaParameterPassingInfo =
                [AbiPassingInformation.FromSegmentByValue(compiler, in segment)];
            compiler.lvaTable[0].Type = TYP_FLOAT;
            compiler.lvaTable[0].lvLRACandidate = true;
            compiler.lvaTable[0].RegNum = register;

            var initRegStillZeroed = true;
            new CodeGen(compiler).genHomeRegisterParams(Globals.REG_NA, ref initRegStillZeroed);

            Assert.That(initRegStillZeroed, Is.True);
        });
    }

    [Test]
    public static void WasmFuncletFrameCaptureMatchesTheNativeNoOp()
    {
        WithCompiler(compiler => new CodeGen(compiler).genCaptureFuncletPrologEpilogInfo());
    }

    [TestCase(CORINFO_HELP_PROF_FCN_LEAVE)]
    [TestCase(CORINFO_HELP_PROF_FCN_TAILCALL)]
    public static void WasmProfilerLeaveCallbacksRemainEmpty(CorInfoHelpFunc helper)
    {
        Assume.That(Configuration.PROFILING_SUPPORTED, "Wasm profiling support is not enabled.");

        WithCompiler(compiler => {
            ProfilerHookNeeded(compiler) = true;
            var codeGen = new CodeGen(compiler);

            codeGen.genProfilingLeaveCallback(helper);

            Assert.That(ProfilerHookNeeded(compiler), Is.True);
            Assert.That(compiler.info.compProfilerCallback, Is.False);
        });
    }

    [Test]
    public static void WasmGSCookieChecksUseTheNYIBoundary()
    {
        WithCompiler(compiler => {
            var codeGen = new CodeGen(compiler);
            var failure = Assert.Throws<FatalJitException>(() => codeGen.genEmitGSCookieCheck(false));

            Assert.That(failure?.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
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
        using var tls = TestConfiguration.CreateJitTls();
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

    private static List<Emitter.instrDesc> CurrentDescriptors(Emitter emitter)
    {
        return CurrentDescriptorsField(emitter)
            ?? throw new AssertionException("Missing Wasm emitter instruction buffer.");
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentDescriptorsField(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ContainCheckIndir")]
    private static extern void ContainCheckIndir(Lowering lowering, GenTreeIndir node);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "fgMorphIndexAddr")]
    private static extern GenTree MorphIndexAddress(Compiler compiler, GenTreeIndexAddr address);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "compProfilerHookNeeded")]
    private static extern ref bool ProfilerHookNeeded(Compiler compiler);
}
