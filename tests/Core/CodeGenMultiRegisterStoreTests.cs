// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenMultiRegisterStoreTests
{
    [TestCase(TYP_INT, 4u, EA_4BYTE)]
    [TestCase(TYP_LONG, 8u, EA_8BYTE)]
    public static void StackHomesUseTheSourceRegisterWidthAndOrderedOffsets(
        var_types type, uint secondOffset, emitAttr attr)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            ConfigureStackHome(compiler);
            var source = Source(compiler, type);
            var store = new GenTreeLclVar(TYP_STRUCT, 0, source) { Flags = GTF_VAR_DEF };

            codeGen.genMultiRegStoreToLocal(store);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(2));
            for (var i = 0; i < 2; i++)
            {
                Assert.That(descriptors[i].idIns(), Is.EqualTo(INS_mov));
                Assert.That(descriptors[i].idInsFmt(), Is.EqualTo(IF_SWR_RRD));
                Assert.That(descriptors[i].idOpSize(), Is.EqualTo(attr));
                Assert.That(descriptors[i].idReg1(), Is.EqualTo(i == 0 ? REG_RAX : REG_RDX));
                Assert.That(descriptors[i].idAddr().iiaLclVar.lvaVarNum(), Is.Zero);
                Assert.That(descriptors[i].idAddr().iiaLclVar.lvaOffset(), Is.EqualTo(i == 0 ? 0u : secondOffset));
            }
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_STK));
            Assert.That(compiler.compCurLifeTree, Is.SameAs(store));
        });
    }

    [TestCase(REG_RAX, REG_RDX, 0)]
    [TestCase(REG_R8, REG_R9, 2)]
    [TestCase(REG_R8, REG_NA, 2)]
    [TestCase(REG_NA, REG_NA, 2)]
    public static void PromotedFieldsRetainRegisterAndStackHomesAndGcLiveness(
        regNumber first, regNumber second, int instructions)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            ConfigureFields(compiler, codeGen, TYP_LONG, TYP_REF);
            var source = Source(compiler, TYP_LONG);
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RDX, TYP_REF);
            var store = Store(source, first, second);

            codeGen.genMultiRegStoreToLocal(store);

            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(instructions));
            Assert.That(compiler.lvaTable[1].RegNum, Is.EqualTo(first == REG_NA ? REG_STK : first));
            Assert.That(compiler.lvaTable[2].RegNum, Is.EqualTo(second == REG_NA ? REG_STK : second));
            Assert.That(VarSetOps.IsMember(compiler, compiler.compCurLife, 0), Is.True);
            Assert.That(VarSetOps.IsMember(compiler, compiler.compCurLife, 1), Is.True);
            Assert.That(VarSetOps.IsMember(compiler, codeGen.GCInfo.gcVarPtrSetCur, 1), Is.EqualTo(second == REG_NA));
            var expectedGc = second == REG_NA ? default : Mask(second);
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(expectedGc));
            var expectedVars = (first == REG_NA ? default : Mask(first)) | expectedGc;
            Assert.That(codeGen.RegSet.GetMaskVars(), Is.EqualTo(expectedVars));
            Assert.That(compiler.compCurLifeTree, Is.SameAs(store));
        });
    }

    [Test]
    public static void WriteThroughFieldsStoreTheConsumedRegisterAndDeadFieldsSkipTheirHome(
        [Values(false, true)] bool dead, [Values(false, true)] bool enregistered)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            ConfigureFields(compiler, codeGen, TYP_BYTE, TYP_LONG);
            compiler.lvaTable[1].lvSpillAtSingleDef = true;
            var source = Source(compiler, TYP_LONG);
            var store = Store(source, enregistered ? REG_R8 : REG_NA, REG_RDX);
            store.SetLastUse(0, dead);

            codeGen.genMultiRegStoreToLocal(store);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo((enregistered ? 1 : 0) + (dead ? 0 : 1)));
            if (!dead)
            {
                var home = descriptors[^1];
                Assert.That(home.idInsFmt(), Is.EqualTo(IF_SWR_RRD));
                Assert.That(home.idOpSize(), Is.EqualTo(EA_1BYTE));
                Assert.That(home.idReg1(), Is.EqualTo(REG_RAX));
                Assert.That(home.idAddr().iiaLclVar.lvaVarNum(), Is.EqualTo(1));
            }
            Assert.That(VarSetOps.IsMember(compiler, compiler.compCurLife, 0), Is.EqualTo(!dead));
            Assert.That(VarSetOps.IsMember(compiler, compiler.compCurLife, 1), Is.True);
            Assert.That(compiler.lvaTable[1].RegNum, Is.EqualTo(enregistered ? REG_R8 : REG_STK));
        });
    }

    [TestCase(REG_XMM0, REG_XMM1, TYP_DOUBLE, INS_movd64)]
    [TestCase(REG_XMM0, REG_XMM1, TYP_FLOAT, INS_movd32)]
    public static void PromotedFloatingFieldsUseBitwiseCrossRegisterFileCopies(
        regNumber first, regNumber second, var_types type, instruction expected)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            ConfigureFields(compiler, codeGen, type, type);
            var source = Source(compiler, type == TYP_FLOAT ? TYP_INT : TYP_LONG);
            var store = Store(source, first, second);

            codeGen.genMultiRegStoreToLocal(store);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(2));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(expected));
            Assert.That(descriptors[1].idIns(), Is.EqualTo(expected));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(first));
            Assert.That(descriptors[1].idReg1(), Is.EqualTo(second));
            Assert.That(codeGen.RegSet.GetMaskVars(), Is.EqualTo(Mask(first) | Mask(second)));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void EachFieldIsDefinedBeforeTheNextSourceIsReloaded(bool copyFirst)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            ConfigureFields(compiler, codeGen, TYP_LONG, TYP_LONG);
            var source = Source(compiler, TYP_LONG);
            PrepareSpill(codeGen, source, 1);
            GenTree operand;
            if (copyFirst)
            {
                var copy = new GenTreeCopyOrReload(GT_COPY, TYP_STRUCT, source);
                copy.SetRegNumByIdx(REG_RCX, 0);
                operand = copy;
            }
            else
            {
                var reload = new GenTreeCopyOrReload(GT_RELOAD, TYP_STRUCT, source);
                reload.SetRegNumByIdx(REG_RAX, 1);
                operand = reload;
            }
            var store = Store(operand, REG_R8, REG_R9);
            var before = Descriptors(codeGen).Count;

            codeGen.genMultiRegStoreToLocal(store);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(before + (copyFirst ? 4 : 3)));
            var firstDef = descriptors[before + (copyFirst ? 1 : 0)];
            var reloadIns = descriptors[before + (copyFirst ? 2 : 1)];
            Assert.That(firstDef.idInsFmt(), Is.EqualTo(IF_RWR_RRD));
            Assert.That(firstDef.idReg1(), Is.EqualTo(REG_R8));
            Assert.That(firstDef.idReg2(), Is.EqualTo(copyFirst ? REG_RCX : REG_RAX));
            Assert.That(reloadIns.idInsFmt(), Is.EqualTo(IF_RWR_SRD));
            Assert.That(reloadIns.idReg1(), Is.EqualTo(copyFirst ? REG_RDX : REG_RAX));
            Assert.That(descriptors[^1].idReg1(), Is.EqualTo(REG_R9));
            Assert.That(descriptors[^1].idReg2(), Is.EqualTo(copyFirst ? REG_RDX : REG_RAX));
            var returned = codeGen.RegSet.tmpGetTemp(TYP_LONG);
            Assert.That(returned.tdTempNum, Is.EqualTo(reloadIns.idAddr().iiaLclVar.lvaVarNum()));
            codeGen.RegSet.tmpRlsTemp(returned);
        });
    }

    [TestCase(REG_RAX, TYP_BYTE, false, INS_mov)]
    [TestCase(REG_XMM0, TYP_INT, false, INS_movss)]
    [TestCase(REG_XMM0, TYP_LONG, false, INS_movsd_simd)]
    [TestCase(REG_K1, TYP_LONG, false, INS_kmovq_msk)]
    [TestCase(REG_K1, TYP_MASK, false, INS_kmovq_msk)]
    [TestCase(REG_RAX, TYP_MASK, false, INS_kmovq_msk)]
    [TestCase(REG_RAX, TYP_FLOAT, false, INS_mov)]
    [TestCase(REG_RAX, TYP_DOUBLE, false, INS_mov)]
    [TestCase(REG_XMM0, TYP_SIMD16, false, INS_movups)]
    [TestCase(REG_XMM0, TYP_SIMD16, true, INS_movaps)]
    public static void StoreSelectionPreservesSourceRegisterClassAndDestinationWidth(
        regNumber source, var_types destination, bool aligned, instruction expected)
    {
        CodeGenBinaryTests.WithCodeGen((_, codeGen) =>
            Assert.That(codeGen.ins_StoreFromSrc(source, destination, aligned), Is.EqualTo(expected)));
    }

    [Test]
    public static void NativeUnsupportedWindowsSimdAssemblyRejectsBeforeConsumption(
        [Values(false, true)] bool direct)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.lvaTable[0].Type = TYP_SIMD16;
            compiler.lvaTable[0].lvLRACandidate = true;
            var source = Source(compiler, TYP_LONG);
            var store = new GenTreeLclVar(TYP_SIMD16, 0, source) { RegNum = REG_XMM0 };
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RAX, TYP_BYREF);
            var before = codeGen.GCInfo.gcRegByrefSetCur;

            var exception = Assert.Throws<FatalJitException>(() =>
            {
                if (direct)
                {
                    codeGen.genMultiRegStoreToSIMDLocal(store);
                }
                else
                {
                    codeGen.genMultiRegStoreToLocal(store);
                }
            });

            Assert.That(exception, Has.Property(nameof(FatalJitException.Result)).EqualTo(CorJitResult.CORJIT_SKIPPED));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(before));
            Assert.That(Descriptors(codeGen), Is.Empty);
            Assert.That(compiler.compCurLifeTree, Is.Null);
        });
    }

#if DEBUG
    [Test]
    public static void DisassemblyRejectionRetainsSpillOwnershipAndAllowsRetry()
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            ConfigureStackHome(compiler);
            var source = Source(compiler, TYP_LONG);
            PrepareSpill(codeGen, source, 1);
            var store = new GenTreeLclVar(TYP_STRUCT, 0, source);
            var before = Descriptors(codeGen).Count;
            compiler.opts.dspCode = true;

            _ = Assert.Throws<FatalJitException>(() => codeGen.genMultiRegStoreToLocal(store));
            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(before));
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_RAX));
            Assert.That(compiler.compCurLifeTree, Is.Null);
            compiler.opts.dspCode = false;

            codeGen.genMultiRegStoreToLocal(store);

            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(before + 3));
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_STK));
        });
    }
#endif

    private static GenTreeHWIntrinsic Source(Compiler compiler, var_types type)
    {
        var low = compiler.gtNewIconNode(type, 1);
        var high = compiler.gtNewIconNode(type, 0);
        var divisor = compiler.gtNewIconNode(type, 3);
        var source = new GenTreeHWIntrinsic(TYP_STRUCT,
            type == TYP_INT ? NI_X86Base_DivRem : NI_X86Base_X64_DivRem, type, 0, low, high, divisor)
        {
            RegNum = REG_RAX,
        };
        source.SetRegNumByIdx(REG_RDX, 1);

        return source;
    }

    private static GenTreeLclVar Store(GenTree source, regNumber first, regNumber second)
    {
        var store = new GenTreeLclVar(TYP_STRUCT, 0, source);
        store.Flags |= GTF_VAR_DEF;
        store.SetMultiReg();
        store.SetRegNumByIdx(first, 0);
        store.SetRegNumByIdx(second, 1);

        return store;
    }

    private static void ConfigureStackHome(Compiler compiler)
    {
        compiler.lvaTable[0].Type = TYP_STRUCT;
        compiler.lvaTable[0].Layout = new ClassLayout(16);
        compiler.lvaTable[0].lvTracked = false;
        compiler.lvaTable[0].RegNum = REG_RAX;
    }

    private static void ConfigureFields(Compiler compiler, CodeGen codeGen, var_types first, var_types second)
    {
        compiler.lvaCount = 3;
        compiler.info.compLocalsCount = 3;
        compiler.lvaTrackedCount = 2;
        compiler.lvaTrackedToVarNum = [1, 2];
        compiler.lvaEnregMultiRegVars = true;
        compiler.lvaTable = [
            new() { Type = TYP_STRUCT, Layout = new ClassLayout(16), lvPromoted = true,
                lvFieldLclStart = 1, lvFieldCnt = 2 },
            new() { Type = first, lvIsStructField = true, lvTracked = true, lvLRACandidate = true,
                RegNum = REG_STK, lvOnFrame = true, lvFramePointerBased = true, StackOffset = -16, _varIndex = 0 },
            new() { Type = second, lvIsStructField = true, lvTracked = true, lvLRACandidate = true,
                RegNum = REG_STK, lvOnFrame = true, lvFramePointerBased = true, StackOffset = -8, _varIndex = 1 },
        ];
        compiler.compCurLife = VarSetOps.MakeEmpty(compiler);
        codeGen.GCInfo.gcTrkStkPtrLcls = VarSetOps.MakeEmpty(compiler);
        codeGen.GCInfo.gcVarPtrSetCur = VarSetOps.MakeEmpty(compiler);
        for (byte i = 0; i < 2; i++)
        {
            if (varTypeIsGC(compiler.lvaTable[1 + i].Type))
            {
                VarSetOps.AddElemD(compiler, codeGen.GCInfo.gcTrkStkPtrLcls, i);
            }
        }
        LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);
        codeGen.initializeVariableLiveKeeper();
    }

    private static void PrepareSpill(CodeGen codeGen, GenTreeHWIntrinsic source, byte index)
    {
        codeGen.RegSet.tmpInit();
        codeGen.RegSet.tmpPreAllocateTemps(TYP_LONG, 1);
        var temp = codeGen.RegSet.tmpGetTemp(TYP_LONG);
        temp.tdTempOffs = -32;
        codeGen.RegSet.tmpRlsTemp(temp);
        source.Flags |= GTF_SPILL;
        source.SetRegSpillFlagByIdx(GTF_SPILL, index);
        codeGen.RegSet.rsSpillTree(source.GetRegByIndex(index), source, index);
        source.Flags |= GTF_NOREG_AT_USE;
    }

    private static regMaskTP Mask(regNumber reg) => regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "treeLifeUpdater")]
    private static extern ref TreeLifeUpdater? LifeUpdater(CodeGen codeGen);
}
