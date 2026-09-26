// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CorJitResult;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using static RyuJitSharp.CorInfoOptions;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;

namespace RyuJitSharp.UnitTests;

internal static unsafe class CodeGenPrologInitializationTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void ZeroRegisterIsInitializedOnlyOnce(bool initiallyZero)
    {
        WithProlog((_, codeGen) =>
        {
            var zeroed = initiallyZero;
            Assert.That(codeGen.genGetZeroReg(REG_RAX, ref zeroed), Is.EqualTo(REG_RAX));
            Assert.That(codeGen.genGetZeroReg(REG_RAX, ref zeroed), Is.EqualTo(REG_RAX));
            Assert.That(zeroed, Is.True);
            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(initiallyZero ? 0 : 1));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void FloatingInitializationSharesTheFirstZeroAcrossBothWidths(bool firstDouble)
    {
        WithProlog((_, codeGen) =>
        {
            var first = regMaskTP.CreateFromRegNum(REG_XMM0, REG_XMM0.SingleTypeMask);
            var second = regMaskTP.CreateFromRegNum(REG_XMM1, REG_XMM1.SingleTypeMask);
            var third = regMaskTP.CreateFromRegNum(REG_XMM2, REG_XMM2.SingleTypeMask);
            codeGen.genZeroInitFltRegs(firstDouble ? second : first | third,
                firstDouble ? first | third : second, REG_RAX);

            var ids = Descriptors(codeGen);
            Assert.That(ids, Has.Count.EqualTo(3));
            Assert.That(ids[0].idIns(), Is.EqualTo(INS_xorps));
            Assert.That(ids[0].idReg1(), Is.EqualTo(REG_XMM0));
            Assert.That(ids.Skip(1).Select(id => id.idReg2()), Is.EqualTo((regNumber[])[REG_XMM0, REG_XMM0]));
            Assert.That(ids.Skip(1).Select(id => id.idOpSize()), Is.EqualTo(firstDouble
                ? (emitAttr[])[EA_4BYTE, EA_8BYTE] : [EA_8BYTE, EA_4BYTE]));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ScalarInitializationOnlyClearsGcStructSlotsUnlessInitMemIsRequested(bool initMem)
    {
        WithProlog((compiler, codeGen) =>
        {
            var builder = new ClassLayoutBuilder(compiler, 16);
            builder.SetGCPtrType(1, TYP_REF);
            compiler.lvaTable[0] = new LclVarDsc
            {
                Type = TYP_STRUCT, Layout = ClassLayout.Create(compiler, in builder),
                lvOnFrame = true, lvFramePointerBased = true, StackOffset = -32, RegNum = REG_STK,
            };
            compiler.info.compInitMem = initMem;
            PlanInitialization(codeGen);
            var zeroed = false;
            codeGen.genZeroInitFrame(-16, -32, REG_RAX, ref zeroed);

            var stores = Descriptors(codeGen).Where(id => id.idIns() == INS_mov).ToArray();
            Assert.That(stores.Select(id => id.idAddr().iiaLclVar.lvaOffset()),
                Is.EqualTo(initMem ? (uint[])[0, 8] : [8]));
            Assert.That(stores.All(id => id.idOpSize() == EA_8BYTE), Is.True);
            Assert.That(zeroed, Is.True);
        });
    }

    [Test]
    public static void ScalarInitializationClearsGcSpillTempsButNotIntegerTemps()
    {
        WithProlog((compiler, codeGen) =>
        {
            compiler.lvaTable[0].lvOnFrame = false;
            codeGen.RegSet.tmpPreAllocateTemps(TYP_REF, 1);
            codeGen.RegSet.tmpPreAllocateTemps(TYP_INT, 1);
            var temp = codeGen.RegSet.tmpGetTemp(TYP_REF);
            temp.tdTempOffs = -48;
            codeGen.RegSet.tmpRlsTemp(temp);
            PlanInitialization(codeGen);
            var zeroed = false;

            codeGen.genZeroInitFrame(-40, -48, REG_RAX, ref zeroed);

            var ids = Descriptors(codeGen);
            Assert.That(ids, Has.Count.EqualTo(2));
            Assert.That(ids[^1].idAddr().iiaLclVar.lvaVarNum(), Is.EqualTo(temp.tdTempNum));
            Assert.That(ids[^1].idOpSize(), Is.EqualTo(EA_8BYTE));
            Assert.That(zeroed, Is.True);
        });
    }

    [TestCase(16, -128, 4, false)]
    [TestCase(16, -128, 8, false)]
    [TestCase(16, -128, 12, false)]
    [TestCase(16, -128, 16, false)]
    [TestCase(16, -124, 20, false)]
    [TestCase(16, -120, 28, false)]
    [TestCase(16, -116, 36, false)]
    [TestCase(16, -128, 80, false)]
    [TestCase(16, -128, 96, true)]
    [TestCase(16, -128, 112, true)]
    [TestCase(16, -256, 128, true)]
    [TestCase(16, -252, 140, true)]
    [TestCase(32, -256, 176, false)]
    [TestCase(32, -256, 192, true)]
    [TestCase(32, -252, 208, true)]
    [TestCase(64, -256, 112, false)]
    [TestCase(64, -512, 368, false)]
    [TestCase(64, -512, 384, true)]
    [TestCase(64, -508, 412, true)]
    public static void BlockInitializationCoversExactlyTheRangeAcrossWidthsAlignmentAndLoopThresholds(
        int vectorWidth, int low, int size, bool loop)
    {
        WithProlog((compiler, codeGen) =>
        {
            compiler.info.compInitMem = true;
            compiler.lvaTable[0].Type = TYP_STRUCT;
            compiler.lvaTable[0].Layout = new ClassLayout(Math.Max(size, 24));
            PlanInitialization(codeGen);
            ConfigureVectors(compiler, codeGen, vectorWidth);
            var firstGroup = codeGen.Emitter.emitCurIG;
            var zeroed = false;
            codeGen.genZeroInitFrameUsingBlockInit(low + size, low, REG_RAX, ref zeroed);
            var ids = CodeGenLocalHeapTests.AllDescriptors(firstGroup, codeGen);

            Assert.That(ids.Any(id => id.idIns() == INS_jne), Is.EqualTo(loop));
            if (loop)
            {
                Assert.That(ids.Single(id => id.idIns() == INS_jne).idCodeSize(), Is.EqualTo(2u));
                Assert.That(zeroed, Is.True);
            }

            HashSet<int> cleared = [];
            var loopCount = loop
                ? (int)InstructionConstant(codeGen.Emitter, ids.Single(id => id.idIns() == INS_mov
                    && id.idInsFmt() == Emitter.insFormat.IF_RWR_CNS && id.idReg1() == REG_RAX))
                : 0;
            foreach (var id in ids.Where(id => id.idInsFmt() == Emitter.insFormat.IF_AWR_RRD))
            {
                var address = id.idAddr().iiaAddrMode;
                var offset = (int)Displacement(codeGen.Emitter, id);
                var bytes = (int)EA_SIZE_IN_BYTES(id.idOpSize());
                if (address.amIndxReg != REG_NA)
                {
                    Assert.That(new[] { address.amBaseReg, address.amIndxReg },
                        Is.EquivalentTo((regNumber[])[REG_RBP, REG_RAX]));
                    for (var index = loopCount; index < 0; index += 48)
                    {
                        cleared.UnionWith(Enumerable.Range(offset + index, bytes));
                    }
                }
                else
                {
                    Assert.That(address.amBaseReg, Is.EqualTo(REG_RBP));
                    cleared.UnionWith(Enumerable.Range(offset, bytes));
                }
            }

            Assert.That(cleared.Order(), Is.EqualTo(Enumerable.Range(low, size)));
            if ((vectorWidth == 64) && (size == 112))
            {
                var stores = ids.Where(id => id.idOpSize() == EA_64BYTE).ToArray();
                Assert.That(stores.Select(id => Displacement(codeGen.Emitter, id)),
                    Is.EqualTo((nint[])[low, low + 48]));
            }
        });
    }

    [TestCase(1L, false)]
    [TestCase(-1L, false)]
    [TestCase(int.MaxValue, false)]
    [TestCase(int.MinValue, false)]
    [TestCase(2147483648L, true)]
    [TestCase(4294967295L, true)]
    [TestCase(4294967296L, true)]
    public static void CookieInitializationPreservesSignedImmediateFitAndScratchState(long cookie, bool usesScratch)
    {
        WithProlog((compiler, codeGen) =>
        {
            compiler.compNeedsGSSecurityCookie = true;
            compiler.lvaGSSecurityCookie = 0;
            compiler.gsGlobalSecurityCookieVal = (nint)cookie;
            var zeroed = true;
            codeGen.genSetGSSecurityCookie(REG_R10, ref zeroed);

            var ids = Descriptors(codeGen);
            Assert.That(ids, Has.Count.EqualTo(usesScratch ? 2 : 1));
            Assert.That(InstructionConstant(codeGen.Emitter, ids[0]), Is.EqualTo((nint)cookie));
            Assert.That(zeroed, Is.EqualTo(!usesScratch));
        });
    }

    [TestCase(REG_RAX)]
    [TestCase(REG_R10)]
    public static void IndirectCookiesAlwaysLoadThroughRax(regNumber scratch)
    {
        WithProlog((compiler, codeGen) =>
        {
            compiler.compNeedsGSSecurityCookie = true;
            compiler.lvaGSSecurityCookie = 0;
            compiler.gsGlobalSecurityCookieAddr = (nint*)0x12345678;
            compiler.opts.compReloc = true;
            var zeroed = true;
            codeGen.genSetGSSecurityCookie(scratch, ref zeroed);

            var ids = Descriptors(codeGen);
            Assert.That(ids, Has.Count.EqualTo(2));
            Assert.That(ids[0].idReg1(), Is.EqualTo(REG_RAX));
            Assert.That(ids[0].idIsDspReloc(), Is.True);
            Assert.That(Displacement(codeGen.Emitter, ids[0]), Is.EqualTo((nint)0x12345678));
            Assert.That(ids[1].idReg1(), Is.EqualTo(REG_RAX));
            Assert.That(zeroed, Is.EqualTo(scratch != REG_RAX));
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void GenericContextUsesIncomingRegisterOrStackHome(bool fromThis, bool onStack)
    {
        WithProlog((compiler, codeGen) =>
        {
            compiler.info.compIsStatic = !fromThis;
            compiler.info.compThisArg = 0;
            compiler.info.compTypeCtxtArg = 0;
            compiler.info.compArgsCount = 1;
            compiler.info.compMethodInfo->options = (fromThis
                ? CORINFO_GENERICS_CTXT_FROM_THIS : CORINFO_GENERICS_CTXT_FROM_METHODDESC) | CORINFO_GENERICS_CTXT_KEEP_ALIVE;
            compiler.lvaTable[0].Type = fromThis ? TYP_REF : TYP_I_IMPL;
            compiler.lvaTable[0].lvIsParam = true;
            compiler.lvaParameterPassingInfo = [AbiPassingInformation.FromSegment(compiler, false,
                onStack ? AbiPassingSegment.OnStack(0, 0, 8) : AbiPassingSegment.InRegister(REG_RCX, 0, 8))];
            compiler.lvaCachedGenericContextArgOffs = -40;
            var zeroed = true;
            codeGen.genReportGenericContextArg(REG_RAX, ref zeroed);

            var ids = Descriptors(codeGen);
            Assert.That(ids, Has.Count.EqualTo(onStack ? 2 : 1));
            Assert.That(ids[^1].idReg1(), Is.EqualTo(onStack ? REG_RAX : REG_RCX));
            Assert.That(Displacement(codeGen.Emitter, ids[^1]), Is.EqualTo((nint)(-40)));
            Assert.That(zeroed, Is.EqualTo(!onStack));
            if (onStack)
            {
                Assert.That(Displacement(codeGen.Emitter, ids[0]), Is.EqualTo((nint)(-16)));
            }
        });
    }

    [Test]
    public static void OsrReusesTheExistingCookieWithoutTouchingScratch()
    {
        WithProlog((compiler, codeGen) =>
        {
            var storage = stackalloc byte[PatchpointInfo.ComputeSize(1)];
            var patchpoint = (PatchpointInfo*)storage;
            patchpoint->Initialize(1, 64);
            patchpoint->SecurityCookieOffset = -24;
            compiler.info.compPatchpointInfo = patchpoint;
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_OSR);
            compiler.compNeedsGSSecurityCookie = true;
            var zeroed = true;

            codeGen.genSetGSSecurityCookie(REG_RAX, ref zeroed);

            Assert.That(Descriptors(codeGen), Is.Empty);
            Assert.That(zeroed, Is.True);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void OsrReusesTheExistingGenericContextSlot(bool fromThis)
    {
        WithProlog((compiler, codeGen) =>
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
            compiler.lvaTable[0].Type = TYP_REF;
            compiler.info.compMethodInfo->options = (fromThis
                ? CORINFO_GENERICS_CTXT_FROM_THIS : CORINFO_GENERICS_CTXT_FROM_METHODDESC) | CORINFO_GENERICS_CTXT_KEEP_ALIVE;
            var zeroed = true;

            codeGen.genReportGenericContextArg(REG_RAX, ref zeroed);

            Assert.That(Descriptors(codeGen), Is.Empty);
            Assert.That(zeroed, Is.True);
        });
    }

#if DEBUG
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public static void DspCodeRecordsPrologInitializationAndPreservesUnneededContextNoOp(int operation)
    {
        WithProlog((compiler, codeGen) =>
        {
            compiler.opts.dspCode = true;
            if (operation == 3)
            {
                compiler.compNeedsGSSecurityCookie = true;
                compiler.lvaGSSecurityCookie = 0;
                compiler.gsGlobalSecurityCookieVal = 0x1234;
            }
            var zeroed = false;

            void Generate()
            {
                switch (operation)
                {
                    case 0:
                    {
                        _ = codeGen.genGetZeroReg(REG_RAX, ref zeroed);
                        break;
                    }

                    case 1:
                    {
                        codeGen.genZeroInitFltRegs(RBM_XMM0, RBM_NONE, REG_RAX);
                        break;
                    }

                    case 2:
                    {
                        codeGen.genZeroInitFrame(-16, -32, REG_RAX, ref zeroed);
                        break;
                    }

                    case 3:
                    {
                        codeGen.genSetGSSecurityCookie(REG_RAX, ref zeroed);
                        break;
                    }

                    default:
                    {
                        codeGen.genReportGenericContextArg(REG_RAX, ref zeroed);
                        break;
                    }
                }
            }

            var diagnostic = InstructionRecordingTestSupport.Capture(Generate);
            Assert.That(Descriptors(codeGen).Count > 0, Is.EqualTo(operation is not 2 and not 4));
            Assert.That(diagnostic.Length > 0, Is.EqualTo(operation is not 2 and not 4));
        });
    }
#endif

    private static void WithProlog(Action<Compiler, CodeGen> action)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.lvaTable = [new LclVarDsc
            {
                Type = TYP_INT, lvOnFrame = true, lvFramePointerBased = true, StackOffset = -16, RegNum = REG_STK,
            }];
            compiler.lvaRefCountState = RefCountState.RCS_NORMAL;
            compiler.lvaGSSecurityCookie = BAD_VAR_NUM;
            compiler.lvaInlinedPInvokeFrameVar = BAD_VAR_NUM;
            compiler.lvaStubArgumentVar = BAD_VAR_NUM;
            compiler.lvaRetAddrVar = BAD_VAR_NUM;
            codeGen.RegSet.tmpInit();
            var group = codeGen.Emitter.emitCurIG ?? throw new AssertionException("Missing instruction group.");
            group.igFlags |= InsGroupFlags.Prolog;
            action(compiler, codeGen);
        });
    }

    private static void PlanInitialization(CodeGen codeGen)
    {
        var group = codeGen.Emitter.emitCurIG ?? throw new AssertionException("Missing instruction group.");
        group.igFlags &= ~InsGroupFlags.Prolog;
        codeGen.genCheckUseBlockInit();
        group.igFlags |= InsGroupFlags.Prolog;
    }

    private static void ConfigureVectors(Compiler compiler, CodeGen codeGen, int width)
    {
        if (width > 16)
        {
            EnableAvx2(compiler);
        }
        if (width == 64)
        {
            compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_AVX512);
            compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_AVX512);
            compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_AVX512);
        }
        compiler.opts.preferredVectorByteLength = width;
        codeGen.Emitter.UseVexEncodings = width > 16;
        codeGen.Emitter.UseEvexEncodings = width == 64;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitGetInsAmdAny")]
    private static extern nint Displacement(Emitter emitter, Emitter.instrDesc descriptor);
}
