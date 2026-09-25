// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CodeGenBlockDriverTests
{
    [Test]
    public static void BlockListGeneratesLinearOperandsAndReservesTheExit()
    {
        WithDriver((compiler, codeGen) =>
        {
            var blocks = Blocks(compiler, BBJ_ALWAYS, BBJ_RETURN);
            blocks[0].TargetEdge = new FlowEdge(blocks[0], blocks[1], null);
            var constant = compiler.gtNewIconNode(TYP_INT, 17);
            constant.RegNum = REG_RAX;
            var negation = new GenTreeUnOp(GT_NEG, TYP_INT, constant) { RegNum = REG_RAX, IsUnusedValue = true };
            blocks[0].InsertAtEnd(constant);
            blocks[0].InsertAtEnd(negation);
            var firstGroup = codeGen.Emitter.emitCurIG;

            codeGen.genCodeForBBlist();

            var instructions = CodeGenLocalHeapTests.AllDescriptors(firstGroup, codeGen);
            Assert.That(instructions.Select(id => id.idIns()), Is.EqualTo((instruction[])[INS_mov, INS_neg]));
            Assert.That(blocks[0].bbEmitCookie, Is.Not.Null);
            Assert.That(compiler.compCurBB, Is.Null);
            Assert.That(codeGen.getCurrentStackLevel(), Is.Zero);
            Assert.That(VarSetOps.IsEmpty(compiler, compiler.compCurLife), Is.True);
            Assert.That(codeGen.Emitter.emitCurIG, Is.Null);
            Assert.That(LastPlaceholder(codeGen.Emitter)?.igFlags & InsGroupFlags.Epilog, Is.EqualTo(InsGroupFlags.Epilog));
#if DEBUG
            Assert.That(constant._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED,
                Is.EqualTo(GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED));
            Assert.That(negation._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED,
                Is.EqualTo(GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED));
            Assert.That(codeGen.IsGcTypeFixed, Is.True);
            Assert.That(compiler.fgSafeBasicBlockCreation, Is.False);
#endif
        });
    }

    [TestCase(TYP_REF, false)]
    [TestCase(TYP_BYREF, false)]
    [TestCase(TYP_INT, false)]
    [TestCase(TYP_REF, true)]
    public static void BlockEntryRestoresUnchangedRegisterRootsAndStackHomes(var_types type, bool alwaysAlive)
    {
        WithDriver((compiler, codeGen) =>
        {
            var blocks = Blocks(compiler, BBJ_ALWAYS, BBJ_RETURN);
            blocks[0].TargetEdge = new FlowEdge(blocks[0], blocks[1], null);
            blocks[0].bbLiveIn = VarSetOps.MakeSingleton(compiler, 0);
            blocks[0].bbLiveOut = VarSetOps.MakeSingleton(compiler, 0);
            compiler.compCurLife = VarSetOps.MakeSingleton(compiler, 0);
            ref var local = ref compiler.lvaTable[0];
            local.Type = type;
            local.RegNum = REG_RBX;
            local.lvRegister = true;
            local.lvLRACandidate = true;
            local.lvSpillAtSingleDef = alwaysAlive;
            VarSetOps.AddElemD(compiler, codeGen.GCInfo.gcVarPtrSetCur, 0);

            codeGen.genCodeForBlock(blocks[0]);

            Assert.That(codeGen.RegSet.GetMaskVars(), Is.EqualTo(RBM_RBX));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(type == TYP_REF ? RBM_RBX : RBM_NONE));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(type == TYP_BYREF ? RBM_RBX : RBM_NONE));
            Assert.That(VarSetOps.IsMember(compiler, codeGen.GCInfo.gcVarPtrSetCur, 0), Is.EqualTo(alwaysAlive));
            Assert.That(compiler.compCurBB, Is.Null);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void DebugMappingsKeepTheFirstNonAsyncLabelAndPadEmptyOffsets(bool rich)
    {
        WithDriver((compiler, codeGen) =>
        {
            compiler.opts.compDbgInfo = true;
            compiler.opts.compDbgCode = true;
            compiler.info.compInitMem = true;
            compiler.info.compILCodeSize = 32;
            var blocks = Blocks(compiler, BBJ_ALWAYS, BBJ_RETURN);
            blocks[0].TargetEdge = new FlowEdge(blocks[0], blocks[1], null);
            var context = (InlineContext)RuntimeHelpers.GetUninitializedObject(typeof(InlineContext));
            blocks[0].InsertAtEnd(new GenTreeILOffset(new DebugInfo(context, new ILLocation(1, ICorDebugInfo.ASYNC))));
            blocks[0].InsertAtEnd(new GenTreeILOffset(new DebugInfo(context, new ILLocation(2, ICorDebugInfo.STACK_EMPTY))));
            blocks[0].InsertAtEnd(new GenTreeILOffset(new DebugInfo(context, new ILLocation(3, ICorDebugInfo.STACK_EMPTY))));
            var saved = RichMappings(ref JitConfig);
            RichMappings(ref JitConfig) = rich ? 1 : 0;
            try
            {
                codeGen.genCodeForBlock(blocks[0]);
            }
            finally
            {
                RichMappings(ref JitConfig) = saved;
            }

            Assert.That(compiler.genIPmappings.Select(mapping => mapping.ipmdIsLabel),
                Is.EqualTo((bool[])[false, true, false]));
            Assert.That(Descriptors(codeGen).Select(id => id.idIns()), Is.EqualTo((instruction[])[INS_nop, INS_nop, INS_nop]));
            Assert.That(compiler.genRichIPmappings, Has.Count.EqualTo(rich ? 3 : 0));
            if (rich)
            {
                Assert.That(compiler.genRichIPmappings.Select(mapping => mapping.debugInfo.Location.Offset),
                    Is.EqualTo((int[])[1, 2, 3]));
            }
        });
    }

    [Test]
    public static void CallFinallyContinuationHasNoGenerationSideEffects()
    {
        WithDriver((compiler, codeGen) =>
        {
            var blocks = Blocks(compiler, BBJ_CALLFINALLYRET, BBJ_RETURN);
            var current = compiler.compCurBB;
            codeGen.GCInfo.gcMarkRegSetGCref(RBM_RBX);
            codeGen.genCodeForBlock(blocks[0]);

            Assert.That(compiler.compCurBB, Is.SameAs(current));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(RBM_RBX));
            Assert.That(Descriptors(codeGen), Is.Empty);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void BranchElisionRespectsHotColdBoundaries(bool cold)
    {
        WithDriver((compiler, codeGen) =>
        {
            var blocks = Blocks(compiler, BBJ_ALWAYS, BBJ_RETURN);
            blocks[0].TargetEdge = new FlowEdge(blocks[0], blocks[1], null);
            blocks[1].SetFlags(BBF_HAS_LABEL);
            compiler.fgFirstColdBlock = cold ? blocks[1] : null;
            compiler.compCurBB = blocks[0];
            codeGen.genEmitEndBlock(blocks[0]);

            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(cold ? 1 : 0));
            if (cold)
            {
                Assert.That(Descriptors(codeGen).Single().idIns(), Is.EqualTo(INS_jmp));
            }
        });
    }

    [TestCase(false, false, false)]
    [TestCase(true, false, true)]
    [TestCase(false, true, true)]
    public static void ThrowBlocksSeparateUnwindRegionsAndNoReturnCalls(bool ehBoundary, bool noReturn, bool breakpoint)
    {
        WithDriver((compiler, codeGen) =>
        {
            var blocks = Blocks(compiler, BBJ_THROW, BBJ_RETURN);
            if (ehBoundary)
            {
                blocks[0].TryIndex = 0;
            }
            if (noReturn)
            {
                var call = new GenTreeCall(TYP_VOID);
                call._callMoreFlags |= GenTreeCallFlags.GTF_CALL_M_DOES_NOT_RETURN;
                blocks[0].InsertAtEnd(call);
            }
            compiler.compCurBB = blocks[0];
            codeGen.genEmitEndBlock(blocks[0]);

            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(breakpoint ? 1 : 0));
            if (breakpoint)
            {
                Assert.That(Descriptors(codeGen).Single().idIns(), Is.EqualTo(INS_int3));
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ElidedBranchesKeepCallReturnAddressesInsideTheirEHRegion(bool ehBoundary)
    {
        WithDriver((compiler, codeGen) =>
        {
            var blocks = Blocks(compiler, BBJ_ALWAYS, BBJ_RETURN);
            blocks[0].TargetEdge = new FlowEdge(blocks[0], blocks[1], null);
            if (ehBoundary)
            {
                blocks[0].TryIndex = 0;
            }
            compiler.compCurBB = blocks[0];
            _ = EmitterLabelTests.RecordCallDescriptor(codeGen.Emitter, noGc: false);

            codeGen.genEmitEndBlock(blocks[0]);

            Assert.That(Descriptors(codeGen).Select(id => id.idIns()),
                Is.EqualTo(ehBoundary ? (instruction[])[INS_call, INS_nop] : [INS_call]));
        });
    }

#if DEBUG
    [TestCase(true, true, false)]
    [TestCase(true, true, true)]
    [TestCase(false, true, false)]
    [TestCase(true, false, false)]
    public static void EmitterPayloadRejectsOnlyTheRequestedNativeModeBeforeMutation(
        bool matches, bool sections, bool wholeList)
    {
        WithDriver((compiler, codeGen) =>
        {
            var blocks = Blocks(compiler, BBJ_CALLFINALLYRET, BBJ_RETURN);
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.Base.runWithSPMIErrorTrap = &InvokeCallback;
            vtable.Base.Base.printMethodName = &PrintMethodName;
            ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
            compiler.info.compCompHnd = &jitInfo;
            var previousSections = EmitterSections(ref JitConfig);
            var previousMethods = EmitterTests(ref JitConfig);
            fixed (byte* pattern = matches ? "Test\0"u8 : "Unmatched\0"u8)
            fixed (byte* payload = "all\0"u8)
            {
                EmitterTests(ref JitConfig) = new JitConfigValues.MethodSet(pattern, null);
                EmitterSections(ref JitConfig) = sections ? payload : null;
                try
                {
                    void Generate()
                    {
                        if (wholeList)
                        {
                            codeGen.genCodeForBBlist();
                        }
                        else
                        {
                            codeGen.genCodeForBlock(blocks[0]);
                        }
                    }

                    if (matches && sections)
                    {
                        var failure = Assert.Throws<FatalJitException>(Generate);
                        Assert.That(failure?.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
                    }
                    else
                    {
                        Generate();
                    }
                    Assert.That(blocks[0].HasFlag(BBF_HAS_LABEL), Is.False);
                    Assert.That(compiler.fgSafeBasicBlockCreation, Is.True);
                    Assert.That(Descriptors(codeGen), Is.Empty);
                }
                finally
                {
                    EmitterTests(ref JitConfig).destroy(null);
                    EmitterTests(ref JitConfig) = previousMethods;
                    EmitterSections(ref JitConfig) = previousSections;
                }
            }
        });
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte InvokeCallback(ICorJitInfo* self, delegate* unmanaged[Cdecl]<void*, void> callback, void* state)
    {
        callback(state);
        return 1;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static nint PrintMethodName(ICorJitInfo* self, CORINFO_METHOD_STRUCT_* method, byte* buffer,
        nint size, nint* required)
    {
        *required = 5;
        "Test\0"u8.CopyTo(new Span<byte>(buffer, (int)size));
        return 4;
    }

    [Test]
    public static void DisassemblyRejectsBeforeBlockListMutation()
    {
        WithDriver((compiler, codeGen) =>
        {
            var blocks = Blocks(compiler, BBJ_RETURN);
            compiler.opts.dspCode = true;
            _ = Assert.Throws<FatalJitException>(() => codeGen.genCodeForBBlist());
            Assert.That(blocks[0].HasFlag(BBF_HAS_LABEL), Is.False);
            Assert.That(Descriptors(codeGen), Is.Empty);
        });
    }
#endif

    internal static BasicBlock[] Blocks(Compiler compiler, params BBKinds[] kinds)
    {
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
#endif
        var blocks = kinds.Select(kind => BasicBlock.New(compiler, kind)).ToArray();
        foreach (var block in blocks)
        {
            block.bbLiveIn = VarSetOps.MakeEmpty(compiler);
            block.bbLiveOut = VarSetOps.MakeEmpty(compiler);
        }
        for (var i = 1; i < blocks.Length; i++)
        {
            blocks[i - 1].Next = blocks[i];
        }
        compiler.fgFirstBB = blocks[0];
        compiler.fgLastBB = blocks[^1];
        compiler.fgBBcount = blocks.Length;
        compiler.fgBBNumMax = blocks.Length;
        return blocks;
    }

    internal static void WithDriver(Action<Compiler, CodeGen> action)
    {
#if DEBUG
        var saved = EmitterTests(ref JitConfig);
        EmitterTests(ref JitConfig) = new JitConfigValues.MethodSet(null, null);
        try
        {
#endif
            CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
            {
                compiler.lvaTable[0].RegNum = REG_STK;
                compiler.genIPmappings = [];
                compiler.genRichIPmappings = [];
                compiler.compHndBBtab = [];
                compiler.compFuncInfos = [new FuncInfoDsc { funKind = FuncKind.FUNC_ROOT }];
                compiler.compFuncInfoCount = 1;
                compiler.fgFuncletsCreated = true;
                compiler.compRetTypeDesc.InitializeReturnType(compiler, TYP_VOID, null, compiler.info.compCallConv);
                Allocator(compiler) = new LinearScan(compiler);
                action(compiler, codeGen);
            });
#if DEBUG
        }
        finally
        {
            EmitterTests(ref JitConfig) = saved;
        }
#endif
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_regAlloc")]
    private static extern ref IRegAlloc? Allocator(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitPlaceholderLast")]
    internal static extern ref insGroup? LastPlaceholder(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_richDebugInfo")]
    private static extern ref int RichMappings(ref JitConfigValues config);

#if DEBUG
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitEmitUnitTests")]
    private static extern ref JitConfigValues.MethodSet EmitterTests(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitEmitUnitTestsSections")]
    private static extern ref byte* EmitterSections(ref JitConfigValues config);

#endif
}
