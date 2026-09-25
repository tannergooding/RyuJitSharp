// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static unsafe class CodeGenOSRParameterHomingTests
{
    private delegate void OSRLocalAction(Compiler compiler, CodeGen codeGen, PatchpointInfo* patchpoint);

    [TestCase(TYP_INT, REG_RCX, INS_mov)]
    [TestCase(TYP_REF, REG_RDX, INS_mov)]
    [TestCase(TYP_DOUBLE, REG_XMM0, INS_movsd_simd)]
    public static void LiveOSRLocalIsLoadedFromTheOriginalFrame(var_types type, regNumber reg, instruction ins)
    {
        WithOSRLocal(type, reg, (compiler, codeGen, patchpoint) =>
        {
            MarkLiveIn(compiler, 0);
            var stillZeroed = true;

            codeGen.genEnregisterOSRArgsAndLocals(REG_R10, ref stillZeroed);

            var id = CodeGenShiftTests.Descriptors(codeGen).Single();
            Assert.That(id.idIns(), Is.EqualTo(ins));
            Assert.That(id.idReg1(), Is.EqualTo(reg));
            Assert.That(id.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_RBP));
            Assert.That(Displacement(codeGen.Emitter, id), Is.EqualTo((nint)64));
            Assert.That(stillZeroed, Is.True);
            Assert.That(patchpoint->Offset(0), Is.EqualTo(-40));
        });
    }

    [Test]
    public static void StackPointerFrameIncludesTheOSRFrameDelta()
    {
        WithOSRLocal(TYP_LONG, REG_RCX, (compiler, codeGen, _) =>
        {
            codeGen.resetFramePointerUsedWritePhase();
            codeGen.IsFramePointerUsed = false;
            compiler.compCalleeRegsPushed = 0;
            compiler.compLclFrameSize = 32;
            MarkLiveIn(compiler, 0);
            var stillZeroed = true;

            codeGen.genEnregisterOSRArgsAndLocals(REG_R10, ref stillZeroed);

            var id = CodeGenShiftTests.Descriptors(codeGen).Single();
            Assert.That(id.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_RSP));
            Assert.That(Displacement(codeGen.Emitter, id), Is.EqualTo((nint)88));
            Assert.That(stillZeroed, Is.True);
        });
    }

    [TestCase(false, true, true)]
    [TestCase(true, false, true)]
    [TestCase(true, true, false)]
    public static void UnavailableDeadAndMemoryResidentLocalsAreNotLoaded(
        bool isOSRLocal, bool liveIn, bool enregistered)
    {
        WithOSRLocal(TYP_LONG, REG_RDX, (compiler, codeGen, _) =>
        {
            compiler.lvaTable[0].lvIsOSRLocal = isOSRLocal;
            if (!enregistered)
            {
                compiler.lvaTable[0].RegNum = REG_STK;
            }
            if (liveIn)
            {
                MarkLiveIn(compiler, 0);
            }
            var stillZeroed = false;

            codeGen.genEnregisterOSRArgsAndLocals(REG_R10, ref stillZeroed);

            Assert.That(CodeGenShiftTests.Descriptors(codeGen), Is.Empty);
            Assert.That(stillZeroed, Is.False);
        });
    }

    [Test]
    public static void PromotedFieldReadsTheParentTier0SlotAtItsFieldOffset()
    {
        WithOSRLocal(TYP_STRUCT, REG_STK, (compiler, codeGen, _) =>
        {
            compiler.lvaTable[1] = new LclVarDsc
            {
                Type = TYP_REF,
                RegNum = REG_RDX,
                lvIsOSRLocal = true,
                lvIsStructField = true,
                lvParentLcl = 0,
                lvFldOffset = 8,
                lvLRACandidate = true,
                lvTracked = true,
                _varIndex = 1,
            };
            compiler.lvaCount = 2;
            compiler.lvaTrackedCount = 2;
            compiler.lvaTrackedToVarNum = [0, 1];
            MarkLiveIn(compiler, 1);
            var stillZeroed = true;

            codeGen.genEnregisterOSRArgsAndLocals(REG_R10, ref stillZeroed);

            var id = CodeGenShiftTests.Descriptors(codeGen).Single();
            Assert.That(id.idReg1(), Is.EqualTo(REG_RDX));
            Assert.That(id.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_RBP));
            Assert.That(Displacement(codeGen.Emitter, id), Is.EqualTo((nint)72));
            Assert.That(id.idGCref(), Is.EqualTo(GCInfo.GCtype.GCT_GCREF));
            Assert.That(stillZeroed, Is.True);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void WindowsAMD64SplitParameterHomingDoesNotAlterScratchState(bool initiallyZero)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_LONG, REG_RAX, (_, codeGen, _) =>
        {
            var stillZeroed = initiallyZero;

            codeGen.genHomeStackPartOfSplitParameter(REG_R10, ref stillZeroed);

            Assert.That(stillZeroed, Is.EqualTo(initiallyZero));
            Assert.That(CodeGenShiftTests.Descriptors(codeGen), Is.Empty);
        });
    }

    private static void WithOSRLocal(var_types type, regNumber reg, OSRLocalAction action)
    {
        CodeGenSpillVariableTests.WithCompiler(type == TYP_STRUCT ? TYP_LONG : type, REG_RAX,
            (compiler, codeGen, _) =>
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
                var local = new LclVarDsc
                {
                    Type = type, RegNum = reg, lvOnFrame = true, lvIsOSRLocal = true,
                    lvLRACandidate = reg != REG_STK, lvTracked = true, lvFramePointerBased = true,
                };
                if (type == TYP_STRUCT)
                {
                    var builder = new ClassLayoutBuilder(compiler, 16);
                    builder.SetGCPtrType(1, TYP_REF);
                    local.Layout = ClassLayout.Create(compiler, in builder);
                }
                local.StackOffset = -16;
                compiler.lvaTable = [local, default];
                compiler.fgFirstBB = new BasicBlock(null, null) { bbLiveIn = VarSetOps.MakeEmpty(compiler) };
                var group = codeGen.Emitter.emitCurIG ?? throw new AssertionException("Missing prolog group.");
                group.igFlags |= InsGroupFlags.Prolog;

                action(compiler, codeGen, patchpoint);
            });
    }

    private static void MarkLiveIn(Compiler compiler, int index)
    {
        var entry = compiler.fgFirstBB ?? throw new AssertionException("Missing OSR entry block.");
        VarSetOps.AddElemD(compiler, entry.bbLiveIn, index);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitGetInsAmdAny")]
    private static extern nint Displacement(Emitter emitter, Emitter.instrDesc descriptor);
}
