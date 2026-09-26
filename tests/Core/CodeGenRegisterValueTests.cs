// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenRegisterValueTests
{
    [TestCase(TYP_BYTE, INS_movsx, EA_1BYTE)]
    [TestCase(TYP_UBYTE, INS_movzx, EA_1BYTE)]
    [TestCase(TYP_SHORT, INS_movsx, EA_2BYTE)]
    [TestCase(TYP_USHORT, INS_movzx, EA_2BYTE)]
    [TestCase(TYP_INT, INS_mov, EA_4BYTE)]
    [TestCase(TYP_LONG, INS_mov, EA_8BYTE)]
    public static void ReloadNormalizesFromTheLocalTypeEvenWhenTheNodeIsWider(
        var_types type, instruction ins, emitAttr size)
    {
        CodeGenSpillVariableTests.WithCompiler(type, REG_RAX, (compiler, codeGen, tree) =>
        {
            compiler.lvaTable[0].lvIsParam = true;
            compiler.lvaTable[0].RegNum = REG_STK;
            codeGen.RegSet.ClearMaskVars();
            tree.Type = TYP_LONG;
            tree.Flags = GTF_SPILLED;

            codeGen.genUnspillRegIfNeeded(tree);

            var descriptor = LastInstruction(codeGen.Emitter) ?? throw new AssertionException("Missing reload.");
            Assert.That(descriptor.idIns(), Is.EqualTo(ins));
            Assert.That(descriptor.idOpSize(), Is.EqualTo(size));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_RAX));
            Assert.That(descriptor.idInsFmt(), Is.EqualTo(Emitter.insFormat.IF_RWR_SRD));
            Assert.That(descriptor.idCodeSize(), Is.GreaterThan(0));
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_RAX));
            Assert.That(codeGen.RegSet.GetMaskVars(), Is.EqualTo(Mask(REG_RAX)));
            Assert.That(tree.Flags & GTF_SPILLED, Is.EqualTo(GTF_EMPTY));
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void LocalReloadPreservesWriteThroughRootsAndDoesNotPublishTemporaryHomes(
        bool reSpill, bool alwaysInMemory)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_REF, REG_RAX, (compiler, codeGen, tree) =>
        {
            compiler.lvaTable[0].RegNum = REG_STK;
            compiler.lvaTable[0].lvSpillAtSingleDef = alwaysInMemory;
            codeGen.RegSet.ClearMaskVars();
            VarSetOps.AddElemD(compiler, codeGen.GCInfo.gcVarPtrSetCur, 0);
            tree.Flags = GTF_SPILLED | (reSpill ? GTF_SPILL : GTF_EMPTY);

            codeGen.genUnspillRegIfNeeded(tree);

            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(reSpill ? REG_STK : REG_RAX));
            Assert.That(codeGen.RegSet.GetMaskVars(), Is.EqualTo(reSpill ? default : Mask(REG_RAX)));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(Mask(REG_RAX)));
            Assert.That(VarSetOps.IsMember(compiler, codeGen.GCInfo.gcVarPtrSetCur, 0),
                Is.EqualTo(reSpill || alwaysInMemory));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void LastUseReloadDoesNotOpenANewDebugLiveRange(bool lastUse)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_INT, REG_RAX, (compiler, codeGen, tree) =>
        {
            compiler.opts.compDbgInfo = true;
            compiler.lvaTable[0].RegNum = REG_STK;
            codeGen.RegSet.ClearMaskVars();
            codeGen.initializeVariableLiveKeeper();
            codeGen.getVariableLiveKeeper().siStartVariableLiveRange(in compiler.lvaTable[0], 0);
            tree.Flags = GTF_SPILLED | (lastUse ? GTF_VAR_DEATH : GTF_EMPTY);

            codeGen.genUnspillRegIfNeeded(tree);

            Assert.That(codeGen.getVariableLiveKeeper().getLiveRangesForVarForBody(0),
                Has.Count.EqualTo(lastUse ? 1 : 2));
        });
    }

    [TestCase(TYP_INT, EA_4BYTE)]
    [TestCase(TYP_REF, EA_8BYTE)]
    [TestCase(TYP_BYREF, EA_8BYTE)]
    public static void SpilledTemporaryReloadsIntoTheWrapperRegisterAndReleasesItsTemp(var_types type, emitAttr size)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_INT, REG_RAX, (compiler, codeGen, _) =>
        {
            PrepareLife(compiler, codeGen);
            codeGen.RegSet.ClearMaskVars();
            var temp = PrepareTemp(codeGen, type);
            var value = compiler.gtNewIconNode(type, 0);
            value.RegNum = REG_RAX;
            value.Flags |= GTF_SPILL;
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RAX, type);

            codeGen.genProduceReg(value);
            Assert.That(value.Flags & (GTF_SPILL | GTF_SPILLED), Is.EqualTo(GTF_SPILLED));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur.IsEmpty, Is.True);
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur.IsEmpty, Is.True);

            var reload = new GenTreeCopyOrReload(GT_RELOAD, type, value) { RegNum = REG_RDX };
            codeGen.genUnspillRegIfNeeded(reload);

            var descriptor = LastInstruction(codeGen.Emitter) ?? throw new AssertionException("Missing temp reload.");
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_RDX));
            Assert.That(descriptor.idOpSize(), Is.EqualTo(size));
            Assert.That(descriptor.idAddr().iiaLclVar.lvaVarNum(), Is.EqualTo(temp.tdTempNum));
            Assert.That(value.Flags & GTF_SPILLED, Is.EqualTo(GTF_EMPTY));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(type == TYP_REF ? Mask(REG_RDX) : default));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(type == TYP_BYREF ? Mask(REG_RDX) : default));

            var recycled = codeGen.RegSet.tmpGetTemp(type);
            Assert.That(recycled, Is.SameAs(temp));
            codeGen.RegSet.tmpRlsTemp(recycled);
            codeGen.RegSet.rsSpillBeg();
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void CopyMovesOnlyPermanentLocalHomes(bool temporary)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_REF, REG_RAX, (compiler, codeGen, tree) =>
        {
            PrepareLife(compiler, codeGen);
            VarSetOps.AddElemD(compiler, compiler.compCurLife, 0);
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RAX, TYP_REF);
            tree.Flags = GTF_EMPTY;
            var copy = new GenTreeCopyOrReload(GT_COPY, TYP_REF, tree)
            {
                RegNum = REG_RDX,
                Flags = temporary ? GTF_VAR_DEATH : GTF_EMPTY,
            };

            Assert.That(codeGen.genConsumeReg(copy), Is.EqualTo(REG_RDX));

            var home = temporary ? REG_RAX : REG_RDX;
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(home));
            Assert.That(codeGen.RegSet.GetMaskVars(), Is.EqualTo(Mask(home)));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(Mask(home)));
            Assert.That(LastInstruction(codeGen.Emitter)?.idIns(), Is.EqualTo(INS_mov));
        });
    }

    [TestCase(TYP_REF)]
    [TestCase(TYP_BYREF)]
    [TestCase(TYP_INT)]
    public static void ConsumingANonlocalClearsItsProducedGcRegister(var_types type)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_INT, REG_RAX, (compiler, codeGen, _) =>
        {
            PrepareLife(compiler, codeGen);
            codeGen.RegSet.ClearMaskVars();
            var tree = compiler.gtNewIconNode(type, 0);
            tree.RegNum = REG_RDX;
            codeGen.genProduceReg(tree);
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(type == TYP_REF ? Mask(REG_RDX) : default));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(type == TYP_BYREF ? Mask(REG_RDX) : default));

            Assert.That(codeGen.genConsumeReg(tree), Is.EqualTo(REG_RDX));

            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur.IsEmpty, Is.True);
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur.IsEmpty, Is.True);
        });
    }

    [Test]
    public static void SpillDescriptorsSupportOutOfOrderUnspillingAndReuse()
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_INT, REG_RAX, (compiler, codeGen, _) =>
        {
            codeGen.RegSet.tmpInit();
            codeGen.RegSet.tmpPreAllocateTemps(TYP_INT, 2);
            var temp1 = codeGen.RegSet.tmpGetTemp(TYP_INT);
            var temp2 = codeGen.RegSet.tmpGetTemp(TYP_INT);
            temp1.tdTempOffs = -32;
            temp2.tdTempOffs = -48;
            codeGen.RegSet.tmpRlsTemp(temp1);
            codeGen.RegSet.tmpRlsTemp(temp2);
            var first = compiler.gtNewIconNode(TYP_INT, 1);
            var second = compiler.gtNewIconNode(TYP_INT, 2);
            first.RegNum = REG_RAX;
            second.RegNum = REG_RAX;
            first.Flags |= GTF_SPILL;
            second.Flags |= GTF_SPILL;

            codeGen.RegSet.rsSpillTree(REG_RAX, first);
            codeGen.RegSet.rsSpillTree(REG_RAX, second);
            var firstTemp = codeGen.RegSet.rsUnspillInPlace(first, REG_RAX);
            Assert.That(firstTemp, Is.SameAs(temp2));
            codeGen.RegSet.tmpRlsTemp(firstTemp);
            first.Flags |= GTF_SPILL;
            codeGen.RegSet.rsSpillTree(REG_RAX, first);
            codeGen.RegSet.tmpRlsTemp(codeGen.RegSet.rsUnspillInPlace(second, REG_RAX));
            codeGen.RegSet.tmpRlsTemp(codeGen.RegSet.rsUnspillInPlace(first, REG_RAX));

            codeGen.RegSet.rsSpillBeg();
            Assert.That(first.Flags & (GTF_SPILL | GTF_SPILLED), Is.EqualTo(GTF_EMPTY));
            Assert.That(second.Flags & (GTF_SPILL | GTF_SPILLED), Is.EqualTo(GTF_EMPTY));
        });
    }

    [Test]
    public static void MultiRegisterCopyPrecedesAReloadIntoTheOldSourceRegister()
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_INT, REG_RAX, (compiler, codeGen, _) =>
        {
            compiler.lvaCount = 3;
            compiler.info.compLocalsCount = 3;
            compiler.lvaTrackedCount = 2;
            compiler.lvaTrackedToVarNum = [1, 2];
            compiler.lvaEnregMultiRegVars = true;
            compiler.lvaTable = [
                new() { Type = TYP_STRUCT, lvPromoted = true, lvFieldLclStart = 1, lvFieldCnt = 2 },
                new()
                {
                    Type = TYP_LONG, lvIsStructField = true, lvTracked = true, lvLRACandidate = true,
                    RegNum = REG_RAX, lvOnFrame = true, lvFramePointerBased = true, StackOffset = -16, _varIndex = 0,
                },
                new()
                {
                    Type = TYP_REF, lvIsStructField = true, lvTracked = true, lvLRACandidate = true,
                    RegNum = REG_STK, lvOnFrame = true, lvFramePointerBased = true, StackOffset = -8, _varIndex = 1,
                },
            ];
            PrepareLife(compiler, codeGen);
            VarSetOps.AddElemD(compiler, compiler.compCurLife, 0);
            VarSetOps.AddElemD(compiler, compiler.compCurLife, 1);
            VarSetOps.AddElemD(compiler, codeGen.GCInfo.gcTrkStkPtrLcls, 1);
            VarSetOps.AddElemD(compiler, codeGen.GCInfo.gcVarPtrSetCur, 1);
            codeGen.initializeVariableLiveKeeper();
            var local = compiler.gtNewLclvNode(TYP_STRUCT, 0).AsLclVar();
            local.SetMultiReg();
            local.SetRegNumByIdx(REG_RAX, 0);
            local.SetRegNumByIdx(REG_RAX, 1);
            local.Flags |= GTF_SPILLED;
            local.SetRegSpillFlagByIdx(GTF_SPILLED, 1);
            local.SetLastUse(1, true);
            var copy = new GenTreeCopyOrReload(GT_COPY, TYP_STRUCT, local) { RegNum = REG_RDX };

            codeGen.genRegCopy(copy);

            var descriptors = CurrentDescriptors(codeGen.Emitter) ?? throw new AssertionException("Missing descriptors.");
            Assert.That(descriptors, Has.Count.EqualTo(2));
            Assert.That(descriptors[0].idInsFmt(), Is.EqualTo(Emitter.insFormat.IF_RWR_RRD));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_RDX));
            Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_RAX));
            Assert.That(descriptors[1].idInsFmt(), Is.EqualTo(Emitter.insFormat.IF_RWR_SRD));
            Assert.That(descriptors[1].idReg1(), Is.EqualTo(REG_RAX));
            Assert.That(descriptors[1].idAddr().iiaLclVar.lvaVarNum(), Is.EqualTo(2));
            Assert.That(compiler.lvaTable[1].RegNum, Is.EqualTo(REG_RDX));
            Assert.That(codeGen.RegSet.GetMaskVars(), Is.EqualTo(Mask(REG_RDX)));
            Assert.That(VarSetOps.IsMember(compiler, compiler.compCurLife, 1), Is.False);
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur.IsEmpty, Is.True);
        });
    }

    [TestCase(REG_RAX, TYP_LONG, INS_mov)]
    [TestCase(REG_XMM0, TYP_LONG, INS_movd64)]
    [TestCase(REG_XMM0, TYP_INT, INS_movd32)]
    [TestCase(REG_RAX, TYP_DOUBLE, INS_movd64)]
    [TestCase(REG_RAX, TYP_FLOAT, INS_movd32)]
    [TestCase(REG_XMM0, TYP_DOUBLE, INS_movaps)]
    [TestCase(REG_K1, TYP_LONG, INS_kmovq_gpr)]
    [TestCase(REG_RAX, TYP_MASK, INS_kmovq_gpr)]
    [TestCase(REG_K1, TYP_MASK, INS_kmovq_msk)]
    public static void CopySelectionPreservesRegisterClassAndWidth(
        regNumber source, var_types destinationType, instruction expected)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_INT, REG_RAX,
            (_, codeGen, _) => Assert.That(codeGen.ins_Copy(source, destinationType), Is.EqualTo(expected)));
    }

    [Test]
    public static void ContainedComparisonConsumesItsOperandsInNativeOrder()
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_INT, REG_RAX, (compiler, codeGen, _) =>
        {
            PrepareLife(compiler, codeGen);
            codeGen.RegSet.ClearMaskVars();
            var left = compiler.gtNewIconNode(TYP_REF, 0);
            var right = compiler.gtNewIconNode(TYP_REF, 0);
            left.RegNum = REG_RAX;
            right.RegNum = REG_RDX;
            var comparison = new GenTreeOp(GT_EQ, TYP_INT, left, right) { IsContained = true };
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RAX, TYP_REF);
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RDX, TYP_REF);
#if DEBUG
            var useNum = 0;
            codeGen.genNumberOperandUse(comparison, ref useNum);
            Assert.That(useNum, Is.EqualTo(2));
            Assert.That(left.UseNum, Is.Zero);
            Assert.That(right.UseNum, Is.EqualTo(1));
#endif

            codeGen.genConsumeRegs(comparison);

            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur.IsEmpty, Is.True);
            Assert.That(LastInstruction(codeGen.Emitter), Is.Null);
        });
    }

#if DEBUG
    [Test]
    public static void DspCodeRecordsStackLoads()
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_INT, REG_RAX, (compiler, codeGen, tree) =>
        {
            compiler.opts.dspCode = true;

            var diagnostic = InstructionRecordingTestSupport.Capture(
                () => codeGen.Emitter.emitIns_R_S(INS_mov, EA_4BYTE, REG_RAX, 0, 0));

            Assert.That(LastInstruction(codeGen.Emitter)?.idIns(), Is.EqualTo(INS_mov));
            Assert.That(diagnostic, Does.Contain("mov"));
        });
    }
#endif

    private static void PrepareLife(Compiler compiler, CodeGen codeGen)
    {
        compiler.compCurLife = VarSetOps.MakeEmpty(compiler);
        LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);
    }

    private static TempDsc PrepareTemp(CodeGen codeGen, var_types type)
    {
        codeGen.RegSet.tmpInit();
        codeGen.RegSet.tmpPreAllocateTemps(type, 1);
        var temp = codeGen.RegSet.tmpGetTemp(type);
        temp.tdTempOffs = -32;
        codeGen.RegSet.tmpRlsTemp(temp);

        return temp;
    }

    private static regMaskTP Mask(regNumber reg) => regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "treeLifeUpdater")]
    private static extern ref TreeLifeUpdater? LifeUpdater(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? LastInstruction(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentDescriptors(Emitter emitter);
}
