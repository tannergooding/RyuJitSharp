// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.EHHandlerType;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static unsafe class WasmEmitterOracleRefreshTests
{
    [SetUp]
    public static void RequireWasmTarget()
    {
        Assume.That(Configuration.TARGET_WASM, "This fixture requires the Wasm target.");
    }

    [Test]
    public static void LastUseLocalGetRewritesTheSetToTeeAndPreservesTheStackValue()
    {
        WithEmitter((compiler, emitter) =>
        {
            emitter.emitIns_I(INS_local_set, EA_PTRSIZE, 4);
            var sizeAfterSet = CurrentGroupSize(emitter);

            emitter.emitIns_I(INS_local_get, EA_PTRSIZE, 4, isLastUse: true);

            var descriptors = CurrentDescriptors(emitter);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_local_tee));
            Assert.That(descriptors[0].idInsFmt(), Is.EqualTo(Emitter.insFormat.IF_WASM_ULEB128));
            Assert.That(CurrentGroupSize(emitter), Is.EqualTo(sizeAfterSet));
        });
    }

    [Test]
    public static void LocalGetWithoutLastUseStillRewritesTheSetToTee()
    {
        WithEmitter((_, emitter) =>
        {
            emitter.emitIns_I(INS_local_set, EA_PTRSIZE, 4);
            var sizeAfterSet = CurrentGroupSize(emitter);

            emitter.emitIns_I(INS_local_get, EA_PTRSIZE, 4);

            var descriptors = CurrentDescriptors(emitter);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_local_tee));
            Assert.That(descriptors[0].idInsFmt(), Is.EqualTo(Emitter.insFormat.IF_WASM_ULEB128));
            Assert.That(CurrentGroupSize(emitter), Is.EqualTo(sizeAfterSet));
        });
    }

    [Test]
    public static void WasmSmallConstantsUseTheExpandedDescriptorField()
    {
        var signBit = Configuration.HOST_64BIT ? 25 : 26;
        var minimum = -(1 << signBit);
        var maximum = (1 << signBit) - 1;

        Assert.That(Emitter.instrDesc.fitsInSmallCns((nint)minimum), Is.True);
        Assert.That(Emitter.instrDesc.fitsInSmallCns((nint)maximum), Is.True);
        Assert.That(Emitter.instrDesc.fitsInSmallCns((nint)(minimum - 1)), Is.False);
        Assert.That(Emitter.instrDesc.fitsInSmallCns((nint)(maximum + 1)), Is.False);
    }

    [Test]
    public static void LastUseLocalGetAcrossAnInstructionGroupStillUsesTee()
    {
        WithEmitter((_, emitter) =>
        {
            emitter.emitIns_I(INS_local_set, EA_PTRSIZE, 4);
            var setDescriptor = CurrentDescriptors(emitter)[0];
            NextInstructionGroup(emitter, extend: true);

            emitter.emitIns_I(INS_local_get, EA_PTRSIZE, 4, isLastUse: true);

            Assert.That(setDescriptor.idIns(), Is.EqualTo(INS_local_tee));
            Assert.That(setDescriptor.idInsFmt(), Is.EqualTo(Emitter.insFormat.IF_WASM_ULEB128));
            Assert.That(CurrentDescriptors(emitter), Is.Empty);
        });
    }

    [Test]
    public static void BranchIfElidesTwoEqzInstructionsAndUpdatesGroupSize()
    {
        WithEmitter((_, emitter) =>
        {
            emitter.emitInsWasm(INS_i32_eqz);
            emitter.emitInsWasm(INS_i32_eqz);
            emitter.emitIns_J(INS_br_if, emitAttr.EA_4BYTE, 0, null);

            var descriptors = CurrentDescriptors(emitter);
            Assert.That(descriptors, Has.Count.EqualTo(3));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_nop));
            Assert.That(descriptors[0].idInsFmt(), Is.EqualTo(Emitter.insFormat.IF_NONE));
            Assert.That(descriptors[1].idIns(), Is.EqualTo(INS_nop));
            Assert.That(descriptors[1].idInsFmt(), Is.EqualTo(Emitter.insFormat.IF_NONE));
            Assert.That(descriptors[2].idIns(), Is.EqualTo(INS_br_if));
            Assert.That(CurrentGroupSize(emitter), Is.EqualTo((int)descriptors[2].idCodeSize()));

            var group = CurrentGroup(emitter)
                ?? throw new AssertionException("The emitter has no current instruction group.");
            Assert.That((group.igFlags & InsGroupFlags.UpdatedInstructionSize) != 0, Is.True);
        });
    }

    [Test]
    public static void BranchIfTraversesAnExtendedGroupWhenElidingDoubleEqz()
    {
        WithEmitter((_, emitter) =>
        {
            emitter.emitInsWasm(INS_i32_eqz);
            var firstDescriptor = CurrentDescriptors(emitter)[0];
            var firstGroup = CurrentGroup(emitter)
                ?? throw new AssertionException("The emitter has no current instruction group.");
            NextInstructionGroup(emitter, extend: true);

            emitter.emitInsWasm(INS_i32_eqz);
            emitter.emitIns_J(INS_br_if, emitAttr.EA_4BYTE, 0, null);

            var descriptors = CurrentDescriptors(emitter);
            Assert.That(firstDescriptor.idIns(), Is.EqualTo(INS_nop));
            Assert.That(firstDescriptor.idInsFmt(), Is.EqualTo(Emitter.insFormat.IF_NONE));
            Assert.That(firstGroup.igSize, Is.Zero);
            Assert.That((firstGroup.igFlags & InsGroupFlags.UpdatedInstructionSize) != 0, Is.True);
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_nop));
            Assert.That(descriptors[0].idInsFmt(), Is.EqualTo(Emitter.insFormat.IF_NONE));
            Assert.That(descriptors[1].idIns(), Is.EqualTo(INS_br_if));
            Assert.That(CurrentGroupSize(emitter), Is.EqualTo((int)descriptors[1].idCodeSize()));
        });
    }

    [Test]
    public static void MultiplyUsedRegisterLocalKeepsMetadataWithoutAllocatingATemporary()
    {
        WithCompiler(compiler =>
        {
            compiler.lvaTable[0].Type = TYP_INT;
            compiler.lvaTable[0].lvLRACandidate = true;
            compiler.fgFuncletsCreated = true;
            var local = compiler.gtNewLclvNode(TYP_INT, 0);
            local._lirFlags |= LIR.Flags.MultiplyUsed;

            var allocator = new WasmRegAlloc(compiler);
            RequestTemporaryRegisterForMultiplyUsedNode(allocator, local);
            ConsumeTemporaryRegForOperand(allocator, local, "oracle refresh test");

            Assert.That(local._lirFlags & LIR.Flags.MultiplyUsed, Is.EqualTo(LIR.Flags.MultiplyUsed));
            Assert.That(local.RegNum, Is.EqualTo(REG_NA));
        });
    }

    [Test]
    public static void ThrowHelperHandlerInheritsItsVirtualIPWithoutAnEntryStore()
    {
        WithCompiler(compiler =>
        {
            compiler.fgNodeThreading = NodeThreading.LIR;
            compiler.fgPredsComputed = true;
            compiler.fgFuncletsCreated = true;
            compiler.compHndBBtab = [];
            TestConfiguration.EnableSafeBasicBlockCreation(compiler);
            TestConfiguration.EnableSafeFlowEdgeCreation(compiler);

            var root = BasicBlock.New(compiler, BBJ_RETURN);
            root.MakeLir(null, null);
            var handler = BasicBlock.New(compiler, BBJ_RETURN);
            handler.MakeLir(null, null);
            root.Next = handler;
            handler.Prev = root;
            compiler.fgFirstBB = root;
            compiler.fgLastBB = handler;
            compiler.fgFirstFuncletBB = handler;
            root.TryIndex = 0;
            handler.HndIndex = 0;
            handler.SetFlags(BBF_THROW_HELPER);
            var handlerReturn = compiler.gtNewUnaryNode(GT_RETURN, TYP_VOID, null);
            handler.InsertAtEnd(handlerReturn);

            compiler.compHndBBtab =
            [
                new EHblkDsc
                {
                    ebdTryBeg = root,
                    ebdTryLast = root,
                    ebdHndBeg = handler,
                    ebdHndLast = handler,
                    ebdHandlerType = EH_HANDLER_CATCH,
                    ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                    ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                },
            ];
            compiler.compHndBBtabCount = 1;
            compiler.compEHTabOrderToVMClauseOrder = [0];
            compiler.compFuncInfos =
            [
                new FuncInfoDsc { funKind = FuncKind.FUNC_ROOT },
                new FuncInfoDsc { funKind = FuncKind.FUNC_HANDLER, funEHIndex = 0 },
            ];
            compiler.compFuncInfoCount = 2;
            compiler.lvaWasmSpArg = 0;
            compiler.lvaWasmVirtualIP = BAD_VAR_NUM;
            compiler.lvaWasmFunctionIndex = BAD_VAR_NUM;
            compiler.lvaTable[0].Type = TYP_I_IMPL;
            compiler.lvaTable[0].lvIsParam = true;
            compiler.codeGen = new CodeGen(compiler);
            CurrentLowering(compiler) = new Lowering(compiler, new LinearScan(compiler));

            Assert.That(compiler.fgWasmVirtualIP(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(handler.FirstNode, Is.SameAs(handlerReturn));
        });
    }

    private static void WithEmitter(Action<Compiler, Emitter> action)
    {
        WithCompiler(compiler =>
        {
            compiler.compCurBB = new BasicBlock(null, null);
            var emitter = compiler.codeGen!.Emitter;
            emitter.emitBegCG(compiler, default);
            emitter.Init();
            emitter.emitBegFN(false, TestConfiguration.IsDebug);
            action(compiler, emitter);
        });
    }

    private static void WithCompiler(Action<Compiler> action)
    {
        using var tls = TestConfiguration.CreateJitTls();
        var previousCompiler = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitTls.Compiler = compiler;

        try
        {
            JitFlags flags = default;
            compiler.opts.jitFlags = &flags;
            compiler.SetMinOpts(false);
            compiler.info.compRetBuffArg = BAD_VAR_NUM;
            compiler.virtualStubParamInfo = new Compiler.VirtualStubParamInfo();
            compiler.lvaTable = [new LclVarDsc { Type = TYP_I_IMPL }];
            compiler.lvaCount = 1;
            compiler.lvaWasmSpArg = 0;
            compiler.lvaWasmResumeIP = BAD_VAR_NUM;
            compiler.compCurLife = VarSetOps.MakeEmpty(compiler);
            compiler.compFuncInfos = [new FuncInfoDsc { funKind = FuncKind.FUNC_ROOT }];
            compiler.compFuncInfoCount = 1;
            compiler.codeGen = new CodeGen(compiler);
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previousCompiler;
        }
    }

    private static List<Emitter.instrDesc> CurrentDescriptors(Emitter emitter)
    {
        return CurrentDescriptorsField(emitter)
            ?? throw new AssertionException("Missing Wasm instruction descriptors.");
    }

    private static int CurrentGroupSize(Emitter emitter)
    {
        return CurrentGroupSizeField(emitter);
    }

    private static insGroup? CurrentGroup(Emitter emitter)
    {
        return CurrentGroupField(emitter);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentDescriptorsField(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGsize")]
    private static extern ref int CurrentGroupSizeField(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIG")]
    private static extern ref insGroup? CurrentGroupField(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitNxtIG")]
    private static extern void NextInstructionGroup(Emitter emitter, bool extend);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "RequestTemporaryRegisterForMultiplyUsedNode")]
    private static extern void RequestTemporaryRegisterForMultiplyUsedNode(WasmRegAlloc allocator, GenTree node);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ConsumeTemporaryRegForOperand")]
    private static extern void ConsumeTemporaryRegForOperand(WasmRegAlloc allocator, GenTree operand, string reason);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_pLowering")]
    private static extern ref Lowering? CurrentLowering(Compiler compiler);
}
