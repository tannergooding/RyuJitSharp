// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenParameterHomingTests
{
    [TestCase(TYP_INT, REG_RDX, INS_mov, EA_4BYTE)]
    [TestCase(TYP_LONG, REG_RDX, INS_mov, EA_8BYTE)]
    [TestCase(TYP_REF, REG_RCX, INS_mov, EA_8BYTE)]
    [TestCase(TYP_DOUBLE, REG_XMM0, INS_movsd_simd, EA_8BYTE)]
    public static void MinoptsSpillsOnlyLiveIncomingRegisters(var_types type, regNumber source,
        instruction ins, emitAttr size)
    {
        CodeGenSpillVariableTests.WithCompiler(type, source, (compiler, codeGen, _) =>
        {
            SetParameter(compiler, 0, source, type.Size);
            codeGen.CalleeRegArgMaskLiveIn = Mask(source);
            var stillZeroed = true;

            codeGen.genHomeRegisterParams(REG_R10, ref stillZeroed);

            var id = CodeGenShiftTests.Descriptors(codeGen).Single();
            Assert.That(id.idIns(), Is.EqualTo(ins));
            Assert.That(id.idOpSize(), Is.EqualTo(size));
            Assert.That(id.idReg1(), Is.EqualTo(source));
            Assert.That(id.idAddr().iiaLclVar.lvaVarNum(), Is.Zero);
            Assert.That(stillZeroed, Is.True);
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(source));
        });
    }

    [Test]
    public static void MinoptsSkipsDeadArgumentsAndDoesNotChangeScratchState()
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_LONG, REG_RCX, (compiler, codeGen, _) =>
        {
            SetParameter(compiler, 0, REG_RCX, 8);
            codeGen.CalleeRegArgMaskLiveIn = Mask(REG_RDX);
            var stillZeroed = true;

            codeGen.genHomeRegisterParams(REG_R10, ref stillZeroed);

            Assert.That(CodeGenShiftTests.Descriptors(codeGen), Is.Empty);
            Assert.That(stillZeroed, Is.True);
        });
    }

    [TestCase(TYP_INT, REG_RDX, REG_RAX, INS_mov, EA_4BYTE)]
    [TestCase(TYP_DOUBLE, REG_XMM0, REG_XMM1, INS_movaps, EA_8BYTE)]
    [TestCase(TYP_SIMD16, REG_XMM0, REG_XMM1, INS_movaps, EA_16BYTE)]
    public static void OptimizedHomingCopiesIntegerAndSimdParameters(var_types type, regNumber source,
        regNumber target, instruction ins, emitAttr size)
    {
        CodeGenSpillVariableTests.WithCompiler(type, source, (compiler, codeGen, _) =>
        {
            SetParameter(compiler, 0, source, type.Size);
            compiler.lvaTable[0].RegNum = target;
            compiler.lvaTable[0].lvOnFrame = false;
            codeGen.CalleeRegArgMaskLiveIn = Mask(source);
            var stillZeroed = true;

            codeGen.genHomeRegisterParams(target, ref stillZeroed);

            var id = CodeGenShiftTests.Descriptors(codeGen).Single();
            Assert.That(id.idIns(), Is.EqualTo(ins));
            Assert.That(id.idOpSize(), Is.EqualTo(size));
            Assert.That((id.idReg1(), id.idReg2()), Is.EqualTo((target, source)));
            Assert.That(stillZeroed, Is.False);
        }, minopts: false);
    }

    [Test]
    public static void OptimizedHomingStoresAWriteThroughParameterBeforeMovingIt()
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_REF, REG_RCX, (compiler, codeGen, _) =>
        {
            SetParameter(compiler, 0, REG_RCX, 8);
            compiler.lvaTable[0].RegNum = REG_RDX;
            compiler.lvaTable[0]._lvLiveInOutOfHandler = true;
            codeGen.CalleeRegArgMaskLiveIn = Mask(REG_RCX);
            var stillZeroed = true;

            codeGen.genHomeRegisterParams(REG_R10, ref stillZeroed);

            var ids = CodeGenShiftTests.Descriptors(codeGen);
            Assert.That(ids, Has.Count.EqualTo(2));
            Assert.That(ids[0].idAddr().iiaLclVar.lvaVarNum(), Is.Zero);
            Assert.That(ids[0].idReg1(), Is.EqualTo(REG_RCX));
            Assert.That((ids[1].idReg1(), ids[1].idReg2()), Is.EqualTo((REG_RDX, REG_RCX)));
            Assert.That(stillZeroed, Is.True);
        }, minopts: false);
    }

    [TestCase(false, 2)]
    [TestCase(true, 1)]
    public static void MappedParameterIsStoredInBothHomesUnlessBaseIsPromoted(bool promoted, int expectedStores)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_LONG, REG_RCX, (compiler, codeGen, _) =>
        {
            SetParameter(compiler, 0, REG_RCX, 8);
            compiler.lvaTable[0].RegNum = REG_STK;
            compiler.lvaTable[0].lvPromoted = promoted;
            if (promoted)
            {
                compiler.lvaTable[0].Type = TYP_STRUCT;
                compiler.lvaTable[0].Layout = new ClassLayout(8);
            }
            compiler.lvaTable = [
                compiler.lvaTable[0],
                new LclVarDsc
                {
                    Type = TYP_LONG, RegNum = REG_STK, lvOnFrame = true, lvFramePointerBased = true,
                    StackOffset = -32,
                },
            ];
            compiler.lvaCount = 2;
            compiler._paramRegLocalMappings = [
                new ParameterRegisterLocalMapping(AbiPassingSegment.InRegister(REG_RCX, 0, 8), 1, 0),
            ];
            codeGen.CalleeRegArgMaskLiveIn = Mask(REG_RCX);
            var stillZeroed = true;

            codeGen.genHomeRegisterParams(REG_R10, ref stillZeroed);

            var ids = CodeGenShiftTests.Descriptors(codeGen);
            Assert.That(ids.Select(id => id.idAddr().iiaLclVar.lvaVarNum()),
                Is.EqualTo(expectedStores == 1 ? (uint[])[1] : [1, 0]));
            Assert.That(stillZeroed, Is.True);
        }, minopts: false);
    }

    [TestCase(TYP_LONG, REG_RAX, REG_RCX, REG_RDX, INS_mov, EA_8BYTE)]
    [TestCase(TYP_DOUBLE, REG_XMM0, REG_XMM1, REG_XMM2, INS_movaps, EA_8BYTE)]
    public static void RegisterCycleUsesFreeScratchOfTheCorrectClassAndUpdatesZeroedContract(
        var_types type, regNumber source, regNumber destination, regNumber scratch, instruction ins, emitAttr size)
    {
        CodeGenSpillVariableTests.WithCompiler(type, source, (compiler, codeGen, _) =>
        {
            InitializeModifiedRegisters(compiler, codeGen);
            SetParameter(compiler, 0, source, type.Size);
            compiler.lvaTable[0].RegNum = destination;
            compiler.lvaTable[0].lvOnFrame = false;
            compiler.lvaTable = [
                compiler.lvaTable[0],
                new LclVarDsc
                {
                    Type = type, RegNum = source, lvIsParam = true, lvIsRegArg = true,
                    lvLRACandidate = true, lvOnFrame = false,
                },
            ];
            compiler.lvaCount = 2;
            compiler.info.compArgsCount = 2;
            compiler.lvaParameterPassingInfo = [
                compiler.lvaParameterPassingInfo[0],
                AbiPassingInformation.FromSegment(compiler, false,
                    AbiPassingSegment.InRegister(destination, 0, type.Size)),
            ];
            codeGen.CalleeRegArgMaskLiveIn = Mask(source) | Mask(destination);
            var stillZeroed = true;
            var initReg = type == TYP_DOUBLE ? REG_R10 : scratch;
            Assert.That((codeGen.genGetParameterHomingTempRegisterCandidates() & Mask(scratch)).IsNonEmpty, Is.True);

            codeGen.genHomeRegisterParams(initReg, ref stillZeroed);

            var ids = CodeGenShiftTests.Descriptors(codeGen);
            Assert.That(ids.Select(id => (id.idReg1(), id.idReg2())),
                Is.EqualTo((ValueTuple<regNumber, regNumber>[])[
                    (scratch, destination), (destination, source), (source, scratch),
                ]));
            Assert.That(ids.All(id => id.idIns() == ins && id.idOpSize() == size), Is.True);
            Assert.That(stillZeroed, Is.EqualTo(type == TYP_DOUBLE));
        }, minopts: false);
    }

    [TestCase(true)]
    [TestCase(false)]
    public static void PublishedStubParameterHomesUsingItsNonstandardRegister(bool minopts)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_LONG, REG_RAX, (compiler, codeGen, _) =>
        {
            compiler.info.compArgsCount = 0;
            compiler.info.compPublishStubParam = true;
            compiler.lvaStubArgumentVar = 0;
            compiler.lvaTable[0].RegNum = REG_STK;
            codeGen.CalleeRegArgMaskLiveIn = Mask(REG_SECRET_STUB_PARAM);
            var stillZeroed = true;

            codeGen.genHomeRegisterParams(REG_R10, ref stillZeroed);

            var id = CodeGenShiftTests.Descriptors(codeGen).Single();
            Assert.That(id.idReg1(), Is.EqualTo(REG_SECRET_STUB_PARAM));
            Assert.That(id.idAddr().iiaLclVar.lvaVarNum(), Is.Zero);
            Assert.That(id.idOpSize(), Is.EqualTo(EA_8BYTE));
            Assert.That(stillZeroed, Is.True);
        }, minopts);
    }

    [TestCase(true, false, true)]
    [TestCase(false, false, false)]
    [TestCase(true, true, false)]
    public static void IncomingStackArgumentLoadsOnlyLiveEnregisteredNonRegisterArguments(
        bool liveIn, bool registerArgument, bool expectLoad)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_LONG, REG_R10, (compiler, codeGen, _) =>
        {
            InitializeModifiedRegisters(compiler, codeGen);
            compiler.lvaTable[0].lvIsParam = true;
            compiler.lvaTable[0].lvIsRegArg = registerArgument;
            compiler.lvaTable[0].ArgInitReg = REG_R10;
            compiler.fgFirstBB = new BasicBlock(null, null) { bbLiveIn = VarSetOps.MakeEmpty(compiler) };
            if (liveIn)
            {
                VarSetOps.AddElemD(compiler, compiler.fgFirstBB.bbLiveIn, 0);
            }
            var group = codeGen.Emitter.emitCurIG ?? throw new AssertionException("Missing instruction group.");
            group.igFlags |= InsGroupFlags.Prolog;

            codeGen.genEnregisterIncomingStackArgs();

            var ids = CodeGenShiftTests.Descriptors(codeGen);
            Assert.That(ids, Has.Count.EqualTo(expectLoad ? 1 : 0));
            if (expectLoad)
            {
                Assert.That(ids[0].idReg1(), Is.EqualTo(REG_R10));
                Assert.That(ids[0].idAddr().iiaLclVar.lvaVarNum(), Is.Zero);
                Assert.That((codeGen.RegSet.rsGetModifiedRegsMask() & Mask(REG_R10)).IsNonEmpty, Is.True);
            }
        });
    }

    private static void SetParameter(Compiler compiler, int index, regNumber source, int size)
    {
        compiler.info.compArgsCount = Math.Max(compiler.info.compArgsCount, index + 1);
        compiler.lvaTable[index].lvIsParam = true;
        compiler.lvaTable[index].lvIsRegArg = true;
        compiler.lvaParameterPassingInfo = [
            AbiPassingInformation.FromSegment(compiler, false,
                AbiPassingSegment.InRegister(source, 0, size)),
        ];
    }

    private static regMaskTP Mask(regNumber reg)
    {
        return regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);
    }

    private static void InitializeModifiedRegisters(Compiler compiler, CodeGen codeGen)
    {
        var layout = compiler.lvaDoneFrameLayout;
        compiler.lvaDoneFrameLayout = Compiler.TENTATIVE_FRAME_LAYOUT;
        codeGen.RegSet.rsClearRegsModified();
        compiler.lvaDoneFrameLayout = layout;

        compiler.srbmIntCalleeTrash = SRBM_INT_CALLEE_TRASH_INIT;
        compiler.srbmFltCalleeTrash = SRBM_FLT_CALLEE_TRASH_INIT;
        compiler.srbmMskCalleeTrash = SRBM_MSK_CALLEE_TRASH_INIT;
        AllIntRegisters(compiler) = SRBM_ALLINT_INIT;
        AllFloatRegisters(compiler) = SRBM_ALLFLOAT_INIT;
        codeGen.CopyRegisterInfo();
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllInt")]
    private static extern ref regMask AllIntRegisters(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllFloat")]
    private static extern ref regMask AllFloatRegisters(Compiler compiler);
}
