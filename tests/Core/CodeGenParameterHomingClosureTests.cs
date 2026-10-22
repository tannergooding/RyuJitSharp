// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.IO;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.CorInfoOptions;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using System.Linq;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CodeGenParameterHomingClosureTests
{
    [SetUp]
    public static void RequireSupportedTarget()
    {
        Assume.That(!Configuration.TARGET_WASM, "This fixture is not supported on WebAssembly.");
    }

    [Test]
    public static void GraphLookupDoesNotCreateNodesAndPreservesRegisterIdentity()
    {
        var graphType = typeof(CodeGen).GetNestedType("RegGraph", BindingFlags.NonPublic)
            ?? throw new AssertionException("Missing parameter homing graph.");
        var graph = Activator.CreateInstance(graphType, nonPublic: true)
            ?? throw new AssertionException("Missing parameter homing graph instance.");
        var get = graphType.GetMethod("Get")
            ?? throw new AssertionException("Missing graph lookup.");
        var getOrAdd = graphType.GetMethod("GetOrAdd")
            ?? throw new AssertionException("Missing graph insertion.");

        Assert.That(get.Invoke(graph, [REG_INTRET]), Is.Null);
        var integerNode = getOrAdd.Invoke(graph, [REG_INTRET]);
        Assert.That(integerNode, Is.Not.Null);
        Assert.That(get.Invoke(graph, [REG_FP_FIRST]), Is.Null);
        var floatingNode = getOrAdd.Invoke(graph, [REG_FP_FIRST]);
        Assert.That(floatingNode, Is.Not.Null.And.Not.SameAs(integerNode));
        Assert.That(get.Invoke(graph, [REG_INTRET]), Is.SameAs(integerNode));
        Assert.That(get.Invoke(graph, [REG_FP_FIRST]), Is.SameAs(floatingNode));
        Assert.That(getOrAdd.Invoke(graph, [REG_INTRET]), Is.SameAs(integerNode));
    }

    [TestCase(0u, "")]
    [TestCase(8u, " (offset: 8)")]
    [TestCase(0x80000000u, " (offset: -2147483648)")]
    [TestCase(uint.MaxValue, " (offset: -1)")]
    public static void GraphDumpRetainsNativeSignedOffsetText(uint offset, string offsetText)
    {
        Assume.That(TestConfiguration.IsDebug, "This test requires Debug diagnostics.");

        var graphType = typeof(CodeGen).GetNestedType("RegGraph", BindingFlags.NonPublic)
            ?? throw new AssertionException("Missing parameter homing graph.");
        var graph = Activator.CreateInstance(graphType, nonPublic: true)
            ?? throw new AssertionException("Missing parameter homing graph instance.");
        var getOrAdd = graphType.GetMethod("GetOrAdd")
            ?? throw new AssertionException("Missing graph insertion.");
        var addEdge = graphType.GetMethod("AddEdge")
            ?? throw new AssertionException("Missing graph edge insertion.");
        var dump = graphType.GetMethod("Dump")
            ?? throw new AssertionException("Missing graph dump.");
        var source = getOrAdd.Invoke(graph, [REG_INTRET])
            ?? throw new AssertionException("Missing source register.");
        var destination = getOrAdd.Invoke(graph, [REG_FP_FIRST])
            ?? throw new AssertionException("Missing destination register.");
        _ = addEdge.Invoke(graph, [source, destination, TYP_INT, offset]);

        using var stream = new MemoryStream();
        using var writer = new JitTextWriter(stream, leaveOpen: true);
        var previous = s_jitstdout;
        try
        {
            s_jitstdout = writer;
            _ = dump.Invoke(graph, null);
            writer.Flush();

            var expected = $"2 registers in register parameter interference graph{Environment.NewLine}" +
                $"  {REG_INTRET.Name}{Environment.NewLine}" +
                $"  {REG_FP_FIRST.Name}{Environment.NewLine}" +
                $"    <- {REG_INTRET.Name} ({TYP_INT.Name}){offsetText}{Environment.NewLine}";
            Assert.That(Encoding.UTF8.GetString(stream.ToArray()), Is.EqualTo(expected));
        }
        finally
        {
            s_jitstdout = previous;
        }
    }

    [TestCase(long.MinValue, false)]
    [TestCase(-2049L, false)]
    [TestCase(-2048L, true)]
    [TestCase(0L, true)]
    [TestCase(2047L, true)]
    [TestCase(2048L, false)]
    [TestCase(long.MaxValue, false)]
    public static void SignedTwelveBitImmediateUsesInclusiveLowerAndExclusiveUpperBound(long value, bool expected)
    {
        Assume.That(Configuration.TARGET_LOONGARCH64, "This test requires LoongArch64.");

        Assert.That(Emitter.isValidSimm12(unchecked((nint)value)), Is.EqualTo(expected));
    }

    [TestCase(1, false)]
    [TestCase(2, false)]
    [TestCase(3, false)]
    [TestCase(1, true)]
    [TestCase(2, true)]
    [TestCase(3, true)]
    public static void PartialStructSegmentsRetainTargetAndSwiftStoreWidths(int size, bool swift)
    {
        WithCompiler((compiler, codeGen) =>
        {
            compiler.info.compCallConv = swift ? CorInfoCallConvExtension.Swift : CorInfoCallConvExtension.Managed;
            var descriptor = new LclVarDsc { Type = TYP_STRUCT, Layout = new ClassLayout(16) };
            var segment = AbiPassingSegment.InRegister(REG_INTRET, 9, size);
            var expected = segment.GetRegisterType();
            if (!swift)
            {
                if (Configuration.TARGET_ARM64)
                {
                    expected = TYP_I_IMPL;
                }
                else if (Configuration.TARGET_XARCH)
                {
                    expected = expected.ActualType;
                }
            }

            Assert.That(codeGen.genParamStackType(in descriptor, in segment), Is.EqualTo(expected));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void HomingCandidatesIncludeModifiedRegistersAndExcludeReservations(bool reserveIncoming)
    {
        WithCompiler((_, codeGen) =>
        {
            var incoming = Mask(REG_INTRET);
            var modified = new regMaskTP(SRBM_INT_CALLEE_SAVED);
            codeGen.CalleeRegArgMaskLiveIn = incoming;
            codeGen.RegSet.rsClearRegsModified();
            codeGen.RegSet.rsSetRegsModified(modified);
            codeGen.RegSet.rsMaskResvd = reserveIncoming ? incoming : RBM_NONE;
            regMaskTP trash;
            if (Configuration.HAS_MORE_THAN_64_REGISTERS)
            {
                trash = new regMaskTP(codeGen.SRBM_INT_CALLEE_TRASH | codeGen.SRBM_FLT_CALLEE_TRASH,
                    codeGen.SRBM_MSK_CALLEE_TRASH);
            }
            else if (Configuration.TARGET_XARCH)
            {
                trash = new regMaskTP(SRBM_INT_CALLEE_TRASH | SRBM_FLT_CALLEE_TRASH |
                    codeGen.SRBM_MSK_CALLEE_TRASH);
            }
            else
            {
                trash = new regMaskTP(SRBM_INT_CALLEE_TRASH | SRBM_FLT_CALLEE_TRASH);
            }
            var expected = (trash | incoming | modified) & ~codeGen.RegSet.rsMaskResvd;

            Assert.That(codeGen.genGetParameterHomingTempRegisterCandidates(), Is.EqualTo(expected));
        });
    }

    [Test]
    public static void UnneededGenericContextPreservesScratchWithoutEmittingInstructions()
    {
        WithCompiler((_, codeGen) =>
        {
            var zeroed = true;

            codeGen.genReportGenericContextArg(REG_INTRET, ref zeroed);

            Assert.That(zeroed, Is.True);
            Assert.That(CurrentInstructionCount(codeGen.Emitter), Is.Zero);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void OsrContextUsesTheTierZeroSlotWithoutHomingArguments(bool fromThis)
    {
        WithCompiler((compiler, codeGen) =>
        {
            var storage = stackalloc byte[PatchpointInfo.ComputeSize(1)];
            var patchpoint = (PatchpointInfo*)storage;
            patchpoint->Initialize(1, 64);
            patchpoint->GenericContextArgOffset = -32;
            patchpoint->KeptAliveThisOffset = -40;
            compiler.info.compPatchpointInfo = patchpoint;
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_OSR);
            compiler.info.compTypeCtxtArg = 0;
            compiler.info.compIsStatic = !fromThis;
            compiler.lvaTable = [new LclVarDsc { Type = TYP_REF }];
            compiler.info.compMethodInfo->options = (fromThis
                ? CORINFO_GENERICS_CTXT_FROM_THIS : CORINFO_GENERICS_CTXT_FROM_METHODDESC) | CORINFO_GENERICS_CTXT_KEEP_ALIVE;
            compiler.lvaGenericsContextInUse = true;
            var zeroed = true;

            codeGen.genReportGenericContextArg(REG_INTRET, ref zeroed);

            Assert.That(zeroed, Is.True);
            Assert.That(CurrentInstructionCount(codeGen.Emitter), Is.Zero);
        });
    }

    [Test]
    public static void Jit32KeptAliveThisDoesNotCreateAGenericContextSlot()
    {
        Assume.That(Configuration.JIT32_GCENCODER);
        WithCompiler((compiler, codeGen) =>
        {
            compiler.info.compIsStatic = false;
            compiler.info.compMethodInfo->options = CORINFO_GENERICS_CTXT_FROM_THIS;
            compiler.lvaGenericsContextInUse = true;
            compiler.lvaTable = [new LclVarDsc { Type = TYP_REF }];
            Assert.That(compiler.lvaKeepAliveAndReportThis(), Is.True);
            var zeroed = true;

            codeGen.genReportGenericContextArg(REG_INTRET, ref zeroed);

            Assert.That(zeroed, Is.True);
            Assert.That(CurrentInstructionCount(codeGen.Emitter), Is.Zero);
        });
    }

    [Test]
    public static void PlatformsWithContiguousArgumentHomesRetainTheEmptySplitHomingBody()
    {
        Assume.That(!Configuration.TARGET_RISCV64 && !Configuration.TARGET_LOONGARCH64,
            "This test covers targets with contiguous argument homes.");

        var codeGen = (CodeGen)RuntimeHelpers.GetUninitializedObject(typeof(CodeGen));
        var zeroed = true;

        codeGen.genHomeStackPartOfSplitParameter(REG_INTRET, ref zeroed);

        Assert.That(zeroed, Is.True);
    }

    [TestCase(true, true, true)]
    [TestCase(false, true, true)]
    [TestCase(true, false, true)]
    [TestCase(true, true, false)]
    public static void SwiftReassemblyRunsOnlyInTheFirstBlockWithMixedSwiftSegments(
        bool firstBlock, bool split, bool swift)
    {
        Assume.That(Configuration.TARGET_AMD64 && Configuration.SWIFT_SUPPORT && !Configuration.UNIX_AMD64_ABI,
            "This test requires Swift support on non-Unix AMD64.");

        CodeGenBlockDriverTests.WithDriver((compiler, codeGen) =>
        {
            var blocks = CodeGenBlockDriverTests.Blocks(compiler,
                BBKinds.BBJ_ALWAYS, BBKinds.BBJ_ALWAYS, BBKinds.BBJ_RETURN);
            blocks[0].TargetEdge = new FlowEdge(blocks[0], blocks[1], null);
            blocks[1].TargetEdge = new FlowEdge(blocks[1], blocks[2], null);
            compiler.info.compCallConv = swift ? CorInfoCallConvExtension.Swift : CorInfoCallConvExtension.Managed;
            compiler.info.compArgsCount = 1;
            compiler.lvaSwiftSelfArg = BAD_VAR_NUM;
            compiler.lvaSwiftIndirectResultArg = BAD_VAR_NUM;
            compiler.compCalleeRegsPushed = 0;
            compiler.compLclFrameSize = 32;
            compiler.lvaTable[0] = new LclVarDsc
            {
                Type = TYP_STRUCT, Layout = new ClassLayout(16), RegNum = REG_STK,
                lvIsParam = true, lvOnFrame = true, lvFramePointerBased = true, StackOffset = -16,
            };
            compiler.lvaParameterPassingInfo = [
                split
                    ? AbiPassingInformation.FromSegments(compiler,
                        AbiPassingSegment.InRegister(REG_RCX, 0, 8), AbiPassingSegment.OnStack(24, 8, 3))
                    : AbiPassingInformation.FromSegment(compiler, false, AbiPassingSegment.OnStack(24, 0, 16)),
            ];
            Assert.That(compiler.lvaHasAnySwiftStackParamToReassemble(), Is.EqualTo(split && swift));

            codeGen.genCodeForBlock(blocks[firstBlock ? 0 : 1]);

            var ids = CodeGenShiftTests.Descriptors(codeGen);
            Assert.That(ids, Has.Count.EqualTo(firstBlock && split && swift ? 2 : 0));
            if (firstBlock && split && swift)
            {
                Assert.That(ids[0].idReg1(), Is.EqualTo(REG_SCRATCH));
                Assert.That(ids[1].idAddr().iiaLclVar.lvaOffset(), Is.EqualTo(8u));
            }
        });
    }

    [TestCase(0, true)]
    [TestCase(1, false)]
    [TestCase(2, false)]
    [TestCase(3, false)]
    [TestCase(4, false)]
    [TestCase(5, false)]
    public static void SwiftHomingReassemblesOnlyEligibleStructStackSegments(int excludedKind, bool expectHome)
    {
        Assume.That(Configuration.TARGET_AMD64 && Configuration.SWIFT_SUPPORT,
            "This test requires Swift calling-convention support on AMD64.");
        if (excludedKind == 5)
        {
            Assume.That(Configuration.FEATURE_IMPLICIT_BYREFS,
                "This case requires implicit by-reference arguments.");
        }

        CodeGenSpillVariableTests.WithCompiler(TYP_LONG, REG_RAX, (compiler, codeGen, _) =>
        {
            compiler.info.compCallConv = CorInfoCallConvExtension.Swift;
            compiler.info.compArgsCount = 1;
            compiler.lvaSwiftSelfArg = excludedKind == 1 ? 0 : BAD_VAR_NUM;
            compiler.lvaSwiftIndirectResultArg = excludedKind == 2 ? 0 : BAD_VAR_NUM;
            compiler.compCalleeRegsPushed = 0;
            compiler.compLclFrameSize = 32;
            compiler.lvaTable[0] = new LclVarDsc
            {
                Type = excludedKind == 4 ? TYP_LONG : TYP_STRUCT,
                RegNum = REG_STK,
                lvIsParam = true, lvOnFrame = excludedKind != 3, lvFramePointerBased = true,
            };
            if (excludedKind != 4)
            {
                compiler.lvaTable[0].Layout = new ClassLayout(16);
            }
            compiler.lvaTable[0].StackOffset = -16;
            if (Configuration.FEATURE_IMPLICIT_BYREFS)
            {
                compiler.lvaTable[0].IsImplicitByRef = excludedKind == 5;
            }
            compiler.lvaParameterPassingInfo = [
                AbiPassingInformation.FromSegments(compiler,
                    AbiPassingSegment.InRegister(REG_RCX, 0, 8), AbiPassingSegment.OnStack(24, 8, 3)),
            ];

            codeGen.genHomeSwiftStructStackParameters();

            var ids = CodeGenShiftTests.Descriptors(codeGen);
            Assert.That(ids, Has.Count.EqualTo(expectHome ? 2 : 0));
            if (expectHome)
            {
                Assert.That(ids.All(id => id.idOpSize() == EA_4BYTE), Is.True);
                Assert.That(ids[0].idReg1(), Is.EqualTo(REG_SCRATCH));
                Assert.That(ids[1].idIns(), Is.EqualTo(INS_mov));
                Assert.That(ids[1].idAddr().iiaLclVar.lvaVarNum(), Is.Zero);
                Assert.That(ids[1].idAddr().iiaLclVar.lvaOffset(), Is.EqualTo(8u));
            }
        });
    }
    private static void WithCompiler(Action<Compiler, CodeGen> action)
    {
        using var tls = TestConfiguration.CreateJitTls();
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        CORINFO_METHOD_INFO methodInfo = default;
        compiler.opts.jitFlags = &flags;
        compiler.SetMinOpts(true);
        compiler.info.compMethodInfo = &methodInfo;
        compiler.info.compIsStatic = true;
        compiler.info.compTypeCtxtArg = BAD_VAR_NUM;
        if (Configuration.SWIFT_SUPPORT)
        {
            compiler.lvaSwiftErrorArg = BAD_VAR_NUM;
        }
        JitTls.Compiler = compiler;
        try
        {
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            if (Configuration.TARGET_AMD64)
            {
                compiler.srbmIntCalleeTrash = SRBM_INT_CALLEE_TRASH_INIT;
                compiler.srbmFltCalleeTrash = SRBM_FLT_CALLEE_TRASH_INIT;
                compiler.srbmMskCalleeTrash = SRBM_MSK_CALLEE_TRASH_INIT;
                codeGen.CopyRegisterInfo();
            }
            CurrentGroup(codeGen.Emitter) = new insGroup { igFlags = InsGroupFlags.Prolog };
            action(compiler, codeGen);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    private static regMaskTP Mask(regNumber reg) => regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIG")]
    private static extern ref insGroup? CurrentGroup(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGinsCnt")]
    private static extern ref int CurrentInstructionCount(Emitter emitter);
}
