// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static unsafe class WasmRegAllocTests
{
    [SetUp]
    public static void RequireWasmTarget()
    {
        Assume.That(Configuration.TARGET_WASM, "This fixture requires the WebAssembly target.");
    }

    [Test]
    public static void AbiRegisterReuseRequiresAValidVirtualRegisterOfMatchingType()
    {
        var abiRegister = regNumberExtensions.MakeWasmReg(4, WasmValueType.I32);

        Assert.That(WasmRegAlloc.canReuseAbiReg(regNumberExtensions.MakeWasmReg(5, WasmValueType.I32), abiRegister),
            Is.True);
        Assert.That(WasmRegAlloc.canReuseAbiReg(REG_NA, abiRegister), Is.False);
        Assert.That(WasmRegAlloc.canReuseAbiReg(regNumberExtensions.MakeWasmReg(5, WasmValueType.F32), abiRegister),
            Is.False);
    }

    [Test]
    public static void NonFixedRegisterStatePreservesFuncletAndNodeAssociations()
    {
        WithCompiler(compiler =>
        {
            var codeGen = compiler.codeGen
                ?? throw new AssertionException("The compiler code generator was not initialized.");
            var stackPointer = regNumberExtensions.MakeWasmReg(0, WasmValueType.I32);
            var framePointer = regNumberExtensions.MakeWasmReg(1, WasmValueType.I32);
            var secondRegister = regNumberExtensions.MakeWasmReg(2, WasmValueType.I32);

            codeGen.SetStackPointerReg(0, stackPointer);
            codeGen.SetFramePointerReg(0, framePointer);
            Assert.That(compiler.compFuncInfos[0].funStackPointerReg, Is.EqualTo(stackPointer));
            Assert.That(compiler.compFuncInfos[0].funFramePointerReg, Is.EqualTo(framePointer));

            var firstTree = new GenTreeIntCon(TYP_INT, 1);
            var secondTree = new GenTreeIntCon(TYP_INT, 2);
            codeGen.InternalRegisters.Add(firstTree, stackPointer);
            codeGen.InternalRegisters.Add(firstTree, framePointer);
            codeGen.InternalRegisters.Add(secondTree, secondRegister);

            ref var firstRegisters = ref codeGen.InternalRegisters.GetAllWasm(firstTree);
            Assert.That(firstRegisters.Count, Is.EqualTo(2));
            Assert.That(firstRegisters.GetAt(0), Is.EqualTo(stackPointer));
            Assert.That(firstRegisters.GetAt(1), Is.EqualTo(framePointer));

            ref var secondRegisters = ref codeGen.InternalRegisters.GetAllWasm(secondTree);
            Assert.That(secondRegisters.Count, Is.EqualTo(1));
            Assert.That(secondRegisters.GetAt(0), Is.EqualTo(secondRegister));
        });
    }

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

    [Test]
    public static void GCWriteThroughLocalKeepsItsRegisterDefinitionAndPinnedStackHome()
    {
        WithCompiler(compiler =>
        {
            compiler.opts.compFlags |= CLFLG_REGVAR;
            compiler.lvaTable = [new LclVarDsc { Type = TYP_I_IMPL }, new LclVarDsc { Type = TYP_REF }];
            compiler.lvaCount = 2;
            compiler.lvaTrackedCount = 1;
            compiler.lvaTrackedCountInSizeTUnits = 1;
            compiler.lvaTrackedToVarNum = [1];
            compiler.compCurLife = VarSetOps.MakeEmpty(compiler);
            compiler.m_wasmGCRefCandidates = VarSetOps.MakeEmpty(compiler);
            compiler.lvaGetDesc(0).setLvRefCnt(1);

            ref var local = ref compiler.lvaGetDesc(1);
            local.lvTracked = true;
            local._varIndex = 0;
            local.setLvRefCnt(1);

            var allocator = new WasmRegAlloc(compiler);
            Assert.That(allocator.DoRegisterAllocation(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(local.lvWasmGCWriteThru, Is.True);
            Assert.That(local.IsAlwaysAliveInMemory, Is.True);

            var block = new BasicBlock(null, null);
            block.MakeLir(null, null);
            var value = compiler.gtNewIconNode(TYP_REF, 42);
            var store = compiler.gtNewStoreLclVarNode(1, value);
            block.InsertAtEnd(value);
            block.InsertAtEnd(store);

            CurrentBlock(allocator) = block;
            CurrentLowering(compiler) = new Lowering(compiler, allocator);
            RewriteLocalStackStore(allocator, store);

            var address = block.FirstNode ?? throw new AssertionException("Missing pinned-home address.");
            var rhs = address.Next ?? throw new AssertionException("Missing local store value.");
            var localStore = rhs.Next ?? throw new AssertionException("Missing retained local definition.");
            var localRead = localStore.Next ?? throw new AssertionException("Missing write-through local read.");
            var stackStore = localRead.Next ?? throw new AssertionException("Missing pinned-home store.");

            Assert.That(address.Oper, Is.EqualTo(GT_LCL_ADDR));
            Assert.That(address.AsLclVarCommon().LclNum, Is.EqualTo(1));
            Assert.That(rhs, Is.SameAs(value));
            Assert.That(localStore, Is.SameAs(store));
            Assert.That(localRead.Oper, Is.EqualTo(GT_LCL_VAR));
            Assert.That(localRead.AsLclVarCommon().LclNum, Is.EqualTo(1));
            Assert.That(stackStore.Oper, Is.EqualTo(GT_STOREIND));
            Assert.That(stackStore.Next, Is.Null);
        });
    }

    [Test]
    public static void RepeatedFuncletEntryVerifiesTrackedRegisterAssignments()
    {
        WithCompiler(compiler => {
            compiler.lvaTrackedCount = 1;
            compiler.lvaTrackedToVarNum = [0];
            compiler.lvaTable[0].lvTracked = true;
            compiler.lvaTable[0]._varIndex = 0;

            var allocator = new WasmRegAlloc(compiler);

            Assert.That(allocator.DoRegisterAllocation(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));

            var block = new BasicBlock(null, null);
            compiler.fgFirstBB = block;
            allocator.recordVarLocationsAtStartOfBB(block);

            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_STK));
        });
    }

    [Test]
    public static void PhysicalRegisterSourceCanBeRetargetedAfterLirReplacement()
    {
        WithCompiler(compiler => {
            var block = new BasicBlock(null, null);
            block.MakeLir(null, null);
            var source = new GenTreeIntCon(TYP_I_IMPL, 0);
            var following = new GenTreeIntCon(TYP_I_IMPL, 1);
            block.InsertAtEnd(source);
            block.InsertAtEnd(following);

            var virtualStackPointer = regNumberExtensions.MakeWasmReg(0, WasmValueType.I32);
            var resolvedStackPointer = regNumberExtensions.MakeWasmReg(2, WasmValueType.I32);
            var physicalRegister = new GenTreePhysReg(
                virtualStackPointer, TYP_I_IMPL, source, NodeThreading.LIR);
            block.ReplaceNode(source, physicalRegister);
            physicalRegister.SetSrcReg(resolvedStackPointer);

            Assert.That(physicalRegister.SrcReg, Is.EqualTo(resolvedStackPointer));
            Assert.That(block.FirstNode, Is.SameAs(physicalRegister));
            Assert.That(block.LastNode, Is.SameAs(following));
            Assert.That(physicalRegister.Prev, Is.Null);
            Assert.That(physicalRegister.Next, Is.SameAs(following));
            Assert.That(following.Prev, Is.SameAs(physicalRegister));
            Assert.That(source.Prev, Is.Null);
            Assert.That(source.Next, Is.Null);
        });
    }

    [Test]
    public static void SingleUseLocalCopyIsForwardedAndLastUseMovesWithIt()
    {
        WithCompiler(compiler =>
        {
            compiler.lvaTable =
            [
                new LclVarDsc { Type = TYP_INT },
                new LclVarDsc { Type = TYP_INT },
            ];
            compiler.lvaCount = 2;
            compiler.lvaGetDesc(0).setLvRefCnt(1);
            compiler.lvaGetDesc(1).setLvRefCnt(2);
            compiler.fgNodeThreading = NodeThreading.LIR;

            var block = new BasicBlock(null, null);
            block.MakeLir(null, null);

            var source = compiler.gtNewLclvNode(TYP_INT, 0);
            source.Flags |= GenTreeFlags.GTF_VAR_DEATH;
            var copy = compiler.gtNewStoreLclVarNode(1, source);
            var use = compiler.gtNewLclvNode(TYP_INT, 1);
            use.Flags |= GenTreeFlags.GTF_VAR_DEATH;
            block.InsertAtEnd(source);
            block.InsertAtEnd(copy);
            block.InsertAtEnd(use);
            compiler.RecordWasmLocalCopyBlock(block);

            Assert.That(compiler.fgWasmForwardLocalCopies(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(block.FirstNode, Is.SameAs(use));
            Assert.That(block.LastNode, Is.SameAs(use));
            Assert.That(use.AsLclVarCommon().LclNum, Is.Zero);
            Assert.That(use.HasLastUse, Is.True);
            Assert.That(source.HasLastUse, Is.False);
            Assert.That(compiler.lvaGetDesc(1).lvRefCnt(), Is.Zero);
        }, minOpts: false);
    }

    private static void WithCompiler(Action<Compiler> action, bool minOpts = true)
    {
        using var tls = TestConfiguration.CreateJitTls();
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitTls.Compiler = compiler;
        try
        {
            JitFlags flags = default;
            compiler.opts.jitFlags = &flags;
            compiler.lvaRefCountState = RefCountState.RCS_NORMAL;
            compiler.SetMinOpts(minOpts);
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

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_currentBlock")]
    private static extern ref BasicBlock? CurrentBlock(WasmRegAlloc allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "RewriteLocalStackStore")]
    private static extern void RewriteLocalStackStore(WasmRegAlloc allocator, GenTreeLclVarCommon localNode);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_pLowering")]
    private static extern ref Lowering? CurrentLowering(Compiler compiler);
}
