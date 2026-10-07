// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_WASM
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.EHHandlerType;
using static RyuJitSharp.FuncKind;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.Target.UnitTests;

internal static unsafe class WasmCompilerSpillRefsTests
{
    [TestCase(TYP_REF, BBJ_THROW)]
    [TestCase(TYP_BYREF, BBJ_THROW)]
    [TestCase(TYP_REF, BBJ_RETURN)]
    [TestCase(TYP_BYREF, BBJ_RETURN)]
    public static void LiveReferencesUseTypedPinnedSlotsAndReuseThemAcrossBlocks(var_types type, BBKinds kind)
    {
        WithCompiler(type, compiler =>
        {
            var blocks = new BasicBlock[2];
            var definitions = new GenTreeCall[2];
            var uses = new GenTreeLclVar[2];

            for (var i = 0; i < blocks.Length; i++)
            {
                var block = new BasicBlock(null, null) { Kind = kind };
                block.MakeLir(null, null);
                var definition = new GenTreeCall(type);
                definition._lirFlags |= LIR.Flags.MultiplyUsed;
                var call = new GenTreeCall(TYP_VOID);
                var use = compiler.gtNewStoreLclVarNode(0, definition);
                block.InsertAtEnd(definition);
                block.InsertAtEnd(call);
                block.InsertAtEnd(use);

                if (kind is BBJ_RETURN)
                {
                    block.InsertAtEnd(compiler.gtNewUnaryNode(GT_RETURN, TYP_VOID, null));
                }

                blocks[i] = block;
                definitions[i] = definition;
                uses[i] = use;
            }

            blocks[0].Next = blocks[1];
            blocks[1].Prev = blocks[0];
            compiler.fgFirstBB = blocks[0];
            compiler.fgLastBB = blocks[1];

            Assert.That(compiler.fgWasmSpillRefs(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.lvaCount, Is.EqualTo(2));
            ref var slot = ref compiler.lvaGetDesc(1);
            Assert.That(slot.Type, Is.EqualTo(type));
            Assert.That(slot.lvPinned, Is.True);
            Assert.That(slot.lvMustInit, Is.True);
            Assert.That(slot.lvDoNotEnregister, Is.True);

            for (var i = 0; i < blocks.Length; i++)
            {
                var definition = definitions[i];
                var spill = definition.Next as GenTreeLclVar
                    ?? throw new AssertionException("The live reference was not followed by a spill.");
                var reload = spill.Next as GenTreeLclVar
                    ?? throw new AssertionException("The spill was not followed by a reload.");

                Assert.That(spill.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
                Assert.That(spill.LclNum, Is.EqualTo(1));
                Assert.That(spill.Data, Is.SameAs(definition));
                Assert.That(reload.Oper, Is.EqualTo(GT_LCL_VAR));
                Assert.That(reload.LclNum, Is.EqualTo(1));
                Assert.That(reload.Type, Is.EqualTo(type));
                Assert.That(uses[i].Data, Is.SameAs(reload));
                Assert.That(definition._lirFlags & LIR.Flags.MultiplyUsed, Is.EqualTo(LIR.Flags.None));
                Assert.That(reload._lirFlags & LIR.Flags.MultiplyUsed, Is.EqualTo(LIR.Flags.MultiplyUsed));

                if (kind is BBJ_RETURN)
                {
                    Assert.That(uses[i].Next, Is.SameAs(blocks[i].LastNode));
                    Assert.That(blocks[i].LastNode?.Oper, Is.EqualTo(GT_RETURN));
                }
                else
                {
                    var clear = blocks[i].LastNode as GenTreeLclVar
                        ?? throw new AssertionException("The non-returning block did not clear its spill slot.");
                    Assert.That(clear.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
                    Assert.That(clear.LclNum, Is.EqualTo(1));
                    Assert.That(clear.Data.Type, Is.EqualTo(TYP_I_IMPL));
                    Assert.That(clear.Data.IsIntegralConst(0), Is.True);
                    Assert.That(uses[i].Next, Is.SameAs(clear.Data));
                    Assert.That(clear.Data.Next, Is.SameAs(clear));
                }
            }
        });
    }

    [TestCase(TYP_REF)]
    [TestCase(TYP_BYREF)]
    public static void NonAddressExposedLocalDoesNotRequireASpill(var_types type)
    {
        WithCompiler(type, compiler =>
        {
            var block = new BasicBlock(null, null) { Kind = BBJ_THROW };
            block.MakeLir(null, null);
            var definition = compiler.gtNewLclVarNode(type, 0);
            var call = new GenTreeCall(TYP_VOID);
            var use = compiler.gtNewStoreLclVarNode(0, definition);
            block.InsertAtEnd(definition);
            block.InsertAtEnd(call);
            block.InsertAtEnd(use);
            compiler.fgFirstBB = block;
            compiler.fgLastBB = block;

            Assert.That(compiler.fgWasmSpillRefs(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(compiler.lvaCount, Is.EqualTo(1));
            Assert.That(definition.Next, Is.SameAs(call));
            Assert.That(use.Data, Is.SameAs(definition));
            Assert.That(block.LastNode, Is.SameAs(use));
        });
    }

    [Test]
    public static void SimultaneouslyLiveReferencesUseDistinctTypeCompatibleSpillSlots()
    {
        WithCompiler([TYP_REF, TYP_REF, TYP_BYREF], compiler =>
        {
            compiler.lvaSetVarAddrExposed(0, AddressExposedReason.ESCAPE_ADDRESS);
            compiler.lvaSetVarAddrExposed(1, AddressExposedReason.ESCAPE_ADDRESS);
            compiler.lvaSetVarAddrExposed(2, AddressExposedReason.ESCAPE_ADDRESS);

            var block = new BasicBlock(null, null) { Kind = BBJ_RETURN };
            block.MakeLir(null, null);
            var referenceDefinition = compiler.gtNewLclVarNode(TYP_REF, 0);
            var secondReferenceDefinition = compiler.gtNewLclVarNode(TYP_REF, 1);
            var byRefDefinition = compiler.gtNewLclVarNode(TYP_BYREF, 2);
            var call = new GenTreeCall(TYP_VOID);
            var referenceUse = compiler.gtNewStoreLclVarNode(0, referenceDefinition);
            var secondReferenceUse = compiler.gtNewStoreLclVarNode(1, secondReferenceDefinition);
            var byRefUse = compiler.gtNewStoreLclVarNode(2, byRefDefinition);
            block.InsertAtEnd(referenceDefinition);
            block.InsertAtEnd(secondReferenceDefinition);
            block.InsertAtEnd(byRefDefinition);
            block.InsertAtEnd(call);
            block.InsertAtEnd(referenceUse);
            block.InsertAtEnd(secondReferenceUse);
            block.InsertAtEnd(byRefUse);
            block.InsertAtEnd(compiler.gtNewUnaryNode(GT_RETURN, TYP_VOID, null));
            compiler.fgFirstBB = block;
            compiler.fgLastBB = block;

            Assert.That(compiler.fgWasmSpillRefs(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.lvaCount, Is.EqualTo(6));

            var referenceSpill = referenceDefinition.Next as GenTreeLclVar
                ?? throw new AssertionException("The live reference was not followed by a spill.");
            var referenceReload = referenceSpill.Next as GenTreeLclVar
                ?? throw new AssertionException("The reference spill was not followed by a reload.");
            var secondReferenceSpill = secondReferenceDefinition.Next as GenTreeLclVar
                ?? throw new AssertionException("The second live reference was not followed by a spill.");
            var secondReferenceReload = secondReferenceSpill.Next as GenTreeLclVar
                ?? throw new AssertionException("The second reference spill was not followed by a reload.");
            var byRefSpill = byRefDefinition.Next as GenTreeLclVar
                ?? throw new AssertionException("The live byref was not followed by a spill.");
            var byRefReload = byRefSpill.Next as GenTreeLclVar
                ?? throw new AssertionException("The byref spill was not followed by a reload.");

            Assert.That(referenceSpill.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
            Assert.That(referenceSpill.Data, Is.SameAs(referenceDefinition));
            Assert.That(referenceSpill.LclNum, Is.Not.EqualTo(secondReferenceSpill.LclNum));
            Assert.That(referenceSpill.LclNum, Is.Not.EqualTo(byRefSpill.LclNum));
            Assert.That(secondReferenceSpill.LclNum, Is.Not.EqualTo(byRefSpill.LclNum));
            Assert.That(compiler.lvaGetDesc(referenceSpill.LclNum).Type, Is.EqualTo(TYP_REF));
            Assert.That(compiler.lvaGetDesc(secondReferenceSpill.LclNum).Type, Is.EqualTo(TYP_REF));
            Assert.That(compiler.lvaGetDesc(byRefSpill.LclNum).Type, Is.EqualTo(TYP_BYREF));
            Assert.That(compiler.lvaGetDesc(referenceSpill.LclNum).lvPinned, Is.True);
            Assert.That(compiler.lvaGetDesc(secondReferenceSpill.LclNum).lvPinned, Is.True);
            Assert.That(compiler.lvaGetDesc(byRefSpill.LclNum).lvPinned, Is.True);
            Assert.That(referenceReload.LclNum, Is.EqualTo(referenceSpill.LclNum));
            Assert.That(referenceReload.Type, Is.EqualTo(TYP_REF));
            Assert.That(secondReferenceSpill.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
            Assert.That(secondReferenceSpill.Data, Is.SameAs(secondReferenceDefinition));
            Assert.That(secondReferenceReload.LclNum, Is.EqualTo(secondReferenceSpill.LclNum));
            Assert.That(secondReferenceReload.Type, Is.EqualTo(TYP_REF));
            Assert.That(byRefSpill.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
            Assert.That(byRefSpill.Data, Is.SameAs(byRefDefinition));
            Assert.That(byRefReload.LclNum, Is.EqualTo(byRefSpill.LclNum));
            Assert.That(byRefReload.Type, Is.EqualTo(TYP_BYREF));
            Assert.That(referenceUse.Data, Is.SameAs(referenceReload));
            Assert.That(secondReferenceUse.Data, Is.SameAs(secondReferenceReload));
            Assert.That(byRefUse.Data, Is.SameAs(byRefReload));
        });
    }

    [Test]
    public static void FuncletVirtualIPUsesAnIntStackOffset()
    {
        WithCompiler([TYP_I_IMPL], compiler =>
        {
            compiler.fgNodeThreading = NodeThreading.LIR;
#if DEBUG
            compiler.fgSafeBasicBlockCreation = true;
            compiler.fgSafeFlowEdgeCreation = true;
#endif

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
            handler.InsertAtEnd(compiler.gtNewUnaryNode(GT_RETURN, TYP_VOID, null));

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
                new FuncInfoDsc { funKind = FUNC_ROOT },
                new FuncInfoDsc { funKind = FUNC_HANDLER, funEHIndex = 0 },
            ];
            compiler.compFuncInfoCount = 2;
            compiler.fgFuncletsCreated = true;
            compiler.lvaWasmSpArg = 0;
            compiler.lvaWasmVirtualIP = BAD_VAR_NUM;
            compiler.lvaWasmFunctionIndex = BAD_VAR_NUM;
            compiler.lvaTable[0].lvIsParam = true;

            compiler.codeGen = new CodeGen(compiler);
            CurrentLowering(compiler) = new Lowering(compiler, new LinearScan(compiler));

            Assert.That(compiler.fgWasmVirtualIP(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));

            var node = handler.FirstNode;
            while (node is not null && node.Oper is not GT_STOREIND)
            {
                node = node.Next;
            }

            var address = node?.AsStoreInd().Addr
                ?? throw new AssertionException("The funclet did not store its virtual IP through the stack pointer.");
            Assert.That(address.Oper, Is.EqualTo(GT_ADD));
            Assert.That(address.AsOp().Op2.Type, Is.EqualTo(TYP_INT));
        });
    }

    private static void WithCompiler(var_types type, Action<Compiler> action)
    {
        WithCompiler([type], action);
    }

    private static void WithCompiler(var_types[] types, Action<Compiler> action)
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
            compiler.opts.SetMinOpts(true);
            compiler.fgNodeThreading = NodeThreading.LIR;
            compiler.lvaTable = new LclVarDsc[types.Length];
            for (var i = 0; i < types.Length; i++)
            {
                compiler.lvaTable[i].Type = types[i];
            }

            compiler.lvaCount = types.Length;
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_pLowering")]
    private static extern ref Lowering? CurrentLowering(Compiler compiler);
}
#endif
