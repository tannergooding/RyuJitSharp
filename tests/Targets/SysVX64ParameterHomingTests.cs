// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if UNIX_AMD64_ABI
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using static RyuJitSharp.CorInfoOptions;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class SysVX64ParameterHomingTests
{
    [Test]
    public static void MinoptsHomesBothIntegerAndFloatStructSegments()
    {
        SysVX64FrameCodeGenTests.WithProlog((compiler, codeGen) =>
        {
            var first = AbiPassingSegment.InRegister(REG_RDI, 0, 8);
            var second = AbiPassingSegment.InRegister(REG_XMM0, 8, 8);
            compiler.lvaTable[0] = new LclVarDsc
            {
                Type = TYP_STRUCT, Layout = new ClassLayout(16), lvIsParam = true,
                lvIsRegArg = true, lvOnFrame = true, lvFramePointerBased = true,
                RegNum = REG_STK, StackOffset = -32,
            };
            compiler.info.compArgsCount = 1;
            compiler.lvaParameterPassingInfo = [AbiPassingInformation.FromSegments(compiler, in first, in second)];
            codeGen.CalleeRegArgMaskLiveIn = Mask(REG_RDI) | Mask(REG_XMM0);

            var zeroed = true;
            codeGen.genHomeRegisterParams(REG_R10, ref zeroed);

            var ids = Descriptors(codeGen);
            Assert.That(ids.Select(id => id.idReg1()), Is.EqualTo((regNumber[])[REG_RDI, REG_XMM0]));
            Assert.That(ids.Select(id => id.idAddr().iiaLclVar.lvaOffset()), Is.EqualTo((uint[])[0, 8]));
            Assert.That(zeroed, Is.True);
        });
    }

    [Test]
    public static void OptimizedVectorSegmentsInsertUpperEightBytesWithShuffle()
    {
        SysVX64FrameCodeGenTests.WithProlog((compiler, codeGen) =>
        {
            var first = AbiPassingSegment.InRegister(REG_XMM0, 0, 8);
            var second = AbiPassingSegment.InRegister(REG_XMM1, 8, 4);
            compiler.lvaTable[0] = new LclVarDsc
            {
                Type = TYP_SIMD16, lvIsParam = true, lvIsRegArg = true,
                lvLRACandidate = true, RegNum = REG_XMM2,
            };
            compiler.info.compArgsCount = 1;
            compiler.lvaParameterPassingInfo = [AbiPassingInformation.FromSegments(compiler, in first, in second)];
            codeGen.CalleeRegArgMaskLiveIn = Mask(REG_XMM0) | Mask(REG_XMM1);

            var zeroed = true;
            codeGen.genHomeRegisterParams(REG_R10, ref zeroed);

            var ids = Descriptors(codeGen);
            Assert.That(ids.Select(id => id.idIns()), Is.EqualTo((instruction[])[INS_movaps, INS_shufpd]));
            Assert.That(ids.Select(id => (id.idReg1(), id.idReg2())),
                Is.EqualTo((ValueTuple<regNumber, regNumber>[])[(REG_XMM2, REG_XMM0), (REG_XMM2, REG_XMM1)]));
            Assert.That(zeroed, Is.True);
        }, minopts: false);
    }

    [Test]
    public static void OptimizedIntegerCycleUsesScratchWithoutLosingIncomingValues()
    {
        SysVX64FrameCodeGenTests.WithProlog((compiler, codeGen) =>
        {
            compiler.lvaTable =
            [
                new LclVarDsc { Type = TYP_LONG, lvIsParam = true, lvIsRegArg = true,
                    lvLRACandidate = true, RegNum = REG_RSI },
                new LclVarDsc { Type = TYP_LONG, lvIsParam = true, lvIsRegArg = true,
                    lvLRACandidate = true, RegNum = REG_RDI },
            ];
            compiler.lvaCount = 2;
            compiler.info.compArgsCount = 2;
            compiler.lvaParameterPassingInfo =
            [
                AbiPassingInformation.FromSegment(compiler, false, AbiPassingSegment.InRegister(REG_RDI, 0, 8)),
                AbiPassingInformation.FromSegment(compiler, false, AbiPassingSegment.InRegister(REG_RSI, 0, 8)),
            ];
            codeGen.CalleeRegArgMaskLiveIn = Mask(REG_RDI) | Mask(REG_RSI);
            var zeroed = true;

            codeGen.genHomeRegisterParams(REG_RAX, ref zeroed);

            var ids = Descriptors(codeGen);
            Assert.That(ids.Select(id => (id.idReg1(), id.idReg2())),
                Is.EqualTo((ValueTuple<regNumber, regNumber>[])[
                    (REG_RAX, REG_RSI), (REG_RSI, REG_RDI), (REG_RDI, REG_RAX),
                ]));
            Assert.That(zeroed, Is.False);
        }, minopts: false);
    }

    [Test]
    public static void IncomingStackArgumentLoadsOnlyWhenLiveIn()
    {
        SysVX64FrameCodeGenTests.WithProlog((compiler, codeGen) =>
        {
            compiler.lvaTable[0] = new LclVarDsc
            {
                Type = TYP_LONG, lvIsParam = true, lvLRACandidate = true,
                lvOnFrame = true, lvFramePointerBased = true,
                RegNum = REG_R10, ArgInitReg = REG_R10, StackOffset = 16,
            };
            compiler.fgFirstBB = new BasicBlock(null, null) { bbLiveIn = VarSetOps.MakeEmpty(compiler) };
            VarSetOps.AddElemD(compiler, compiler.fgFirstBB.bbLiveIn, 0);

            codeGen.genEnregisterIncomingStackArgs();

            var id = Descriptors(codeGen).Single();
            Assert.That(id.idReg1(), Is.EqualTo(REG_R10));
            Assert.That(id.idAddr().iiaLclVar.lvaVarNum(), Is.Zero);
        });
    }

    [Test]
    public static void Amd64SplitStackHomingDoesNotTouchScratch()
    {
        SysVX64FrameCodeGenTests.WithProlog((_, codeGen) =>
        {
            var zeroed = true;
            codeGen.genHomeStackPartOfSplitParameter(REG_R10, ref zeroed);

            Assert.That(zeroed, Is.True);
            Assert.That(Descriptors(codeGen), Is.Empty);
        });
    }

    [Test]
    public static void LiveOSRLocalLoadsFromTierZeroFrame()
    {
        SysVX64FrameCodeGenTests.WithProlog((compiler, codeGen) =>
        {
            var storage = stackalloc byte[PatchpointInfo.ComputeSize(1)];
            var patchpoint = (PatchpointInfo*)storage;
            patchpoint->Initialize(1, 96);
            patchpoint->SetOffsetAndExposure(0, -40, true);
            compiler.info.compPatchpointInfo = patchpoint;
            compiler.info.compLocalsCount = 1;
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_OSR);
            compiler.lvaMonAcquired = BAD_VAR_NUM;
            compiler.lvaResumedIndicator = BAD_VAR_NUM;
            compiler.lvaAsyncThreadObjectVar = BAD_VAR_NUM;
            compiler.lvaAsyncExecutionContextVar = BAD_VAR_NUM;
            compiler.lvaAsyncSynchronizationContextVar = BAD_VAR_NUM;
            compiler.lvaTable[0] = new LclVarDsc
            {
                Type = TYP_LONG, lvIsOSRLocal = true, lvLRACandidate = true,
                lvTracked = true, lvOnFrame = true, RegNum = REG_R10,
            };
            compiler.fgFirstBB = new BasicBlock(null, null) { bbLiveIn = VarSetOps.MakeEmpty(compiler) };
            VarSetOps.AddElemD(compiler, compiler.fgFirstBB.bbLiveIn, 0);
            codeGen.IsFramePointerUsed = true;
            var zeroed = true;

            codeGen.genEnregisterOSRArgsAndLocals(REG_RAX, ref zeroed);

            var id = Descriptors(codeGen).Single();
            Assert.That(id.idIns(), Is.EqualTo(INS_mov));
            Assert.That(id.idReg1(), Is.EqualTo(REG_R10));
            Assert.That(id.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_RBP));
            Assert.That(Displacement(codeGen.Emitter, id), Is.EqualTo((nint)64));
            Assert.That(zeroed, Is.True);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void GenericContextUsesIncomingRegisterOrStackSlot(bool onStack)
    {
        SysVX64FrameCodeGenTests.WithProlog((compiler, codeGen) =>
        {
            CORINFO_METHOD_INFO methodInfo = default;
            methodInfo.options = CORINFO_GENERICS_CTXT_FROM_METHODDESC | CORINFO_GENERICS_CTXT_KEEP_ALIVE;
            compiler.info.compMethodInfo = &methodInfo;
            compiler.info.compTypeCtxtArg = 0;
            compiler.info.compArgsCount = 1;
            compiler.lvaTable[0] = new LclVarDsc
            {
                Type = TYP_I_IMPL, lvIsParam = true, lvOnFrame = true,
                lvFramePointerBased = true, RegNum = REG_STK, StackOffset = -16,
            };
            compiler.lvaParameterPassingInfo =
            [
                AbiPassingInformation.FromSegment(compiler, false,
                    onStack ? AbiPassingSegment.OnStack(0, 0, 8)
                            : AbiPassingSegment.InRegister(REG_RDI, 0, 8)),
            ];
            compiler.lvaCachedGenericContextArgOffs = -40;
            codeGen.IsFramePointerUsed = true;
            var zeroed = true;

            codeGen.genReportGenericContextArg(REG_RAX, ref zeroed);

            var ids = Descriptors(codeGen);
            Assert.That(ids, Has.Count.EqualTo(onStack ? 2 : 1));
            Assert.That(ids[^1].idReg1(), Is.EqualTo(onStack ? REG_RAX : REG_RDI));
            Assert.That(Displacement(codeGen.Emitter, ids[^1]), Is.EqualTo((nint)(-40)));
            Assert.That(zeroed, Is.EqualTo(!onStack));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void RootPrologMaterializationRemainsGated(bool entirePhase)
    {
        SysVX64FrameCodeGenTests.WithProlog((compiler, codeGen) =>
        {
            var exception = Assert.Throws<FatalJitException>(() =>
            {
                if (entirePhase)
                {
                    codeGen.genGeneratePrologsAndEpilogs();
                }
                else
                {
                    codeGen.genFnProlog();
                }
            });

            Assert.That(exception?.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
            Assert.That(compiler.compGeneratingUnwindProlog, Is.False);
            Assert.That(compiler.funCurrentFunc().unwindCodes, Is.Null);
            Assert.That(Descriptors(codeGen), Is.Empty);
        });
    }

    private static regMaskTP Mask(regNumber reg) => regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitGetInsAmdAny")]
    private static extern nint Displacement(Emitter emitter, Emitter.instrDesc descriptor);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentDescriptors(Emitter emitter);

    private static List<Emitter.instrDesc> Descriptors(CodeGen codeGen)
    {
        return CurrentDescriptors(codeGen.Emitter) ?? throw new AssertionException("Missing instruction descriptors.");
    }
}
#endif
