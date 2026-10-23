// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
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
    public static void StatementLastILOffsetUsesGenTreeDebugData()
    {
        SsaLivenessTests.WithCompiler(0, compiler =>
        {
            var node = new GenTreeILOffset(default, 42);

            if (TestConfiguration.IsDebug)
            {
                Assert.That(node.StmtLastILOffset, Is.EqualTo(42));
                Assert.That(Compiler.DebugData.Get(node).StmtLastILOffset, Is.EqualTo(42));
            }
            else
            {
                _ = Assert.Throws<InvalidOperationException>(() => _ = node.StmtLastILOffset);
            }
        });
    }

    [Test]
    public static void StatementDebugStateUsesCompilerDebugData()
    {
        SsaLivenessTests.WithCompiler(0, compiler =>
        {
            var statement = new Statement(compiler.gtNewIconNode(TYP_INT, 1), 17);

            if (TestConfiguration.IsDebug)
            {
                Assert.That(statement.Id, Is.EqualTo(17));
                Assert.That(statement.LastILOffset, Is.EqualTo(BAD_IL_OFFSET));
                statement.LastILOffset = 23;

                var debugData = Compiler.DebugData.Get(statement);
                Assert.That(debugData.Id, Is.EqualTo(17));
                Assert.That(debugData.LastILOffset, Is.EqualTo(23));
            }
            else
            {
                _ = Assert.Throws<InvalidOperationException>(() => _ = statement.Id);
                _ = Assert.Throws<InvalidOperationException>(() => _ = statement.LastILOffset);
                _ = Assert.Throws<InvalidOperationException>(() => statement.LastILOffset = 23);
            }
        });
    }

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
            if (TestConfiguration.IsDebug)
            {
                Assert.That(constant._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED,
                    Is.EqualTo(GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED));
                Assert.That(negation._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED,
                    Is.EqualTo(GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED));
                Assert.That(codeGen.IsGcTypeFixed, Is.True);
                Assert.That(compiler.fgSafeBasicBlockCreation, Is.False);
            }
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

    [Test]
    public static void BlockExitLiveRegisterMismatchIsCheckedOnlyInDebug()
    {
        WithDriver((compiler, codeGen) =>
        {
            var block = Blocks(compiler, BBJ_RETURN)[0];
            block.bbLiveIn = VarSetOps.MakeSingleton(compiler, 0);
            block.bbLiveOut = VarSetOps.MakeEmpty(compiler);
            compiler.compCurLife = VarSetOps.MakeEmpty(compiler);
            compiler.lvaTable[0].lvLRACandidate = true;

            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.doAssert = &RecordAssertion;
            ICorJitInfo ee = new() { lpVtbl = &vtable };
            using var assertTls = TestConfiguration.CreateJitTls(&ee);
            JitTls.Compiler = compiler;
            s_assertions.Clear();
            void Generate()
            {
                codeGen.genCodeForBlock(block);
            }

            Generate();
            if (TestConfiguration.IsDebug)
            {
                Assert.That(s_assertions, Is.Not.Empty);
            }
            else
            {
                Assert.That(s_assertions, Is.Empty);
            }
            s_assertions.Clear();
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

    [Test]
    public static void CatchRetFallthroughTargetWithoutCodeEmitsNop()
    {
        WithDriver((compiler, codeGen) =>
        {
            var blocks = Blocks(compiler, BBJ_EHCATCHRET, BBJ_ALWAYS, BBJ_RETURN);
            compiler.fgPredsComputed = true;
            blocks[0].TargetEdge = compiler.fgAddRefPred(blocks[1], blocks[0]);
            blocks[1].TargetEdge = compiler.fgAddRefPred(blocks[2], blocks[1]);
            blocks[1].SetFlags(BBF_HAS_LABEL);
            compiler.compCurBB = blocks[1];
            blocks[1].bbEmitCookie = codeGen.Emitter.emitCurIG;

            codeGen.genEmitEndBlock(blocks[1]);

            Assert.That(Descriptors(codeGen).Select(id => id.idIns()), Is.EqualTo((instruction[])[INS_nop]));
        });
    }

    [Test]
    public static void CatchRetTargetWithoutCodeDisablesRemovableJumpCandidate()
    {
        WithDriver((compiler, codeGen) =>
        {
            var blocks = Blocks(compiler, BBJ_EHCATCHRET, BBJ_ALWAYS, BBJ_RETURN, BBJ_RETURN);
            compiler.fgPredsComputed = true;
            blocks[0].TargetEdge = compiler.fgAddRefPred(blocks[1], blocks[0]);
            blocks[1].TargetEdge = compiler.fgAddRefPred(blocks[3], blocks[1]);
            blocks[1].SetFlags(BBF_HAS_LABEL);
            blocks[3].SetFlags(BBF_HAS_LABEL);
            compiler.compCurBB = blocks[1];
            blocks[1].bbEmitCookie = codeGen.Emitter.emitCurIG;

            codeGen.genEmitEndBlock(blocks[1]);

            Assert.That(Descriptors(codeGen).Select(id => id.idIns()), Is.EqualTo((instruction[])[INS_jmp]));
            Assert.That(ContainsRemovableJumpCandidates(codeGen.Emitter), Is.False);
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

    [TestCase(true, "sse2", true, false)]
    [TestCase(true, "sse2", true, true)]
    [TestCase(true, "prefixsse2suffix", true, false)]
    [TestCase(true, "all", true, false)]
    [TestCase(true, "SSE2", true, false)]
    [TestCase(true, "unknown", true, false)]
    [TestCase(true, "", true, false)]
    [TestCase(true, null, true, false)]
    [TestCase(false, "sse2", true, false)]
    [TestCase(true, "sse2", false, false)]
    public static void EmitterPayloadUsesNativeSectionSelectionAtTheLastBlock(
        bool matches, string? section, bool lastBlock, bool wholeList)
    {
        Assume.That(TestConfiguration.IsDebug);
        WithDriver((compiler, codeGen) =>
        {
            var blocks = Blocks(compiler, BBJ_ALWAYS, BBJ_RETURN);
            blocks[0].TargetEdge = new FlowEdge(blocks[0], blocks[1], null);
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.Base.runWithSPMIErrorTrap = &InvokeCallback;
            vtable.Base.Base.printMethodName = &PrintMethodName;
            vtable.Base.notifyInstructionSetUsage =
                (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_InstructionSet, bool, bool, byte>)
                (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_InstructionSet, byte, byte, byte>)&NotifyInstructionSetUsage;
            ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
            compiler.info.compCompHnd = &jitInfo;
            var previousSections = EmitterSections(ref JitConfig);
            var previousMethods = EmitterTests(ref JitConfig);
            if (section == "all")
            {
                foreach (var isa in new[] { ManagedInstructionSet.InstructionSet_APX, ManagedInstructionSet.InstructionSet_AVX10v2 })
                {
                    compiler.opts.compSupportsISA.AddInstructionSet(isa);
                    compiler.opts.compSupportsISAExactly.AddInstructionSet(isa);
                    compiler.opts.compSupportsISAReported.AddInstructionSet(isa);
                }
                codeGen.Emitter.UseEvexEncodings = true;
                codeGen.Emitter.UseRex2Encodings = true;
                codeGen.Emitter.UsePromotedEvexEncodings = true;
            }
            var sectionBytes = section is null ? [] : Encoding.UTF8.GetBytes(section + '\0');
            fixed (byte* pattern = matches ? "Test\0"u8 : "Unmatched\0"u8)
            fixed (byte* payload = sectionBytes)
            {
                EmitterTests(ref JitConfig) = new JitConfigValues.MethodSet(pattern, null);
                EmitterSections(ref JitConfig) = section is null ? null : payload;
                try
                {
                    var firstGroup = codeGen.Emitter.emitCurIG;
                    if (wholeList)
                    {
                        codeGen.genCodeForBBlist();
                    }
                    else
                    {
                        codeGen.genCodeForBlock(blocks[lastBlock ? 1 : 0]);
                    }

                    var instructions = CodeGenLocalHeapTests.AllDescriptors(firstGroup, codeGen)
                        .Select(id => id.idIns()).ToArray();
                    if (!matches || (section is null) || !lastBlock)
                    {
                        Assert.That(instructions, Is.Empty);
                    }
                    else if ((section == "all") || section.Contains("sse2", StringComparison.Ordinal))
                    {
                        Assert.That(instructions[0], Is.EqualTo(INS_jmp));
                        Assert.That(instructions[1], Is.EqualTo(INS_nop));
                        Assert.That(instructions, Does.Contain(INS_addpd));
                        Assert.That(instructions.TakeLast(4), Is.EqualTo((instruction[])[INS_nop, INS_nop, INS_nop, INS_nop]));
                        if (section == "all")
                        {
                            Assert.That(instructions, Does.Contain(INS_crc32_apx));
                            Assert.That(instructions, Does.Contain(INS_vcvttps2dqs));
                            Assert.That(instructions, Does.Contain(INS_ccmpe));
                            Assert.That(instructions, Does.Contain(INS_cfcmovo));
                            Assert.That(instructions, Does.Contain(INS_cteste));
                        }
                    }
                    else
                    {
                        Assert.That(instructions, Is.EqualTo((instruction[])[INS_jmp, INS_nop, INS_nop, INS_nop, INS_nop, INS_nop]));
                    }
                    Assert.That(compiler.compCurBB, Is.Null);
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
    private static byte NotifyInstructionSetUsage(ICorJitInfo* self, CORINFO_InstructionSet isa,
        byte supported, byte preserveNegativeDependency) => supported;

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
    public static void DisassemblyRecordsBlockListInstructions()
    {
        Assume.That(TestConfiguration.IsDebug);
        WithDriver((compiler, codeGen) =>
        {
            var blocks = Blocks(compiler, BBJ_RETURN);
            var constant = compiler.gtNewIconNode(TYP_INT, 17);
            constant.RegNum = REG_RAX;
            var negation = new GenTreeUnOp(GT_NEG, TYP_INT, constant) { RegNum = REG_RAX, IsUnusedValue = true };
            blocks[0].InsertAtEnd(constant);
            blocks[0].InsertAtEnd(negation);
            var first = codeGen.Emitter.emitCurIG;
            TestConfiguration.SetDspCode(compiler, true);
            var diagnostic = InstructionRecordingTestSupport.Capture(codeGen.genCodeForBBlist);
            Assert.That(blocks[0].HasFlag(BBF_HAS_LABEL), Is.True);
            Assert.That(CodeGenLocalHeapTests.AllDescriptors(first, codeGen).Select(id => id.idIns()),
                Is.EqualTo((instruction[])[INS_mov, INS_neg]));
            Assert.That(diagnostic, Does.Contain("neg"));
        });
    }

    internal static BasicBlock[] Blocks(Compiler compiler, params BBKinds[] kinds)
    {
        TestConfiguration.EnableSafeBasicBlockCreation(compiler);
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
        Action? restoreDebugConfig = null;
        if (TestConfiguration.IsDebug)
        {
            var saved = EmitterTests(ref JitConfig);
            EmitterTests(ref JitConfig) = new JitConfigValues.MethodSet(null, null);
            restoreDebugConfig = () => EmitterTests(ref JitConfig) = saved;
        }
        try
        {
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
        }
        finally
        {
            restoreDebugConfig?.Invoke();
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_regAlloc")]
    private static extern ref IRegAlloc? Allocator(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitPlaceholderLast")]
    internal static extern ref insGroup? LastPlaceholder(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitContainsRemovableJmpCandidates")]
    private static extern ref bool ContainsRemovableJumpCandidates(Emitter emitter);

    private static readonly List<string?> s_assertions = [];

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        s_assertions.Add(Marshal.PtrToStringUTF8((nint)expression));
        return 0;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_richDebugInfo")]
    private static extern ref int RichMappings(ref JitConfigValues config);

    private static ref JitConfigValues.MethodSet EmitterTests(ref JitConfigValues config)
        => ref TestConfiguration.GetJitConfigDebugDataForWrite(ref config)._jitEmitUnitTests;

    private static ref byte* EmitterSections(ref JitConfigValues config)
        => ref TestConfiguration.GetJitConfigDebugDataForWrite(ref config)._jitEmitUnitTestsSections;

}
