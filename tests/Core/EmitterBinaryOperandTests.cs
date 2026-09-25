// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static class EmitterBinaryOperandTests
{
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void RegisterAndImmediateSourcesPreserveNddDestinations(bool immediate, bool ndd)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var dst = Register(compiler, REG_RAX);
            var src = Register(compiler, immediate ? REG_NA : REG_RCX);
            src.IsContained = immediate;
            codeGen.Emitter.UsePromotedEvexEncodings = ndd;

            var result = codeGen.Emitter.emitInsBinary(INS_add, EA_4BYTE, dst, src, ndd ? REG_RDX : REG_NA);

            var descriptor = Last(codeGen);
            Assert.That(result, Is.EqualTo(ndd ? REG_RDX : REG_RAX));
            Assert.That(descriptor.idReg1(), Is.EqualTo(result));
            Assert.That(descriptor.idIsEvexNdContextSet(), Is.EqualTo(ndd));
            if (ndd)
            {
                Assert.That(descriptor.idReg2(), Is.EqualTo(REG_RAX));
            }
            Assert.That(Descriptors(codeGen.Emitter), Has.Count.EqualTo(1));
        });
    }

    [TestCase(0, false)]
    [TestCase(1, false)]
    [TestCase(2, false)]
    [TestCase(3, false)]
    [TestCase(0, true)]
    [TestCase(1, true)]
    [TestCase(2, true)]
    [TestCase(3, true)]
    public static void MemorySourcesCoverLocalsFieldsContainedAddressesAndSibModes(int kind, bool ndd)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var dst = Register(compiler, REG_RAX);
            var src = Memory(compiler, kind);
            codeGen.Emitter.UsePromotedEvexEncodings = ndd;

            var result = codeGen.Emitter.emitInsBinary(INS_add, EA_4BYTE, dst, src, ndd ? REG_RDX : REG_NA);

            Assert.That(result, Is.EqualTo(ndd ? REG_RDX : REG_RAX));
            Assert.That(Last(codeGen).idReg1(), Is.EqualTo(result));
            Assert.That(Last(codeGen).idIsEvexNdContextSet(), Is.EqualTo(ndd));
            Assert.That(Last(codeGen).idInsFmt(), Is.EqualTo(kind == 3
                ? (ndd ? Emitter.insFormat.IF_RWR_RRD_ARD : Emitter.insFormat.IF_RRW_ARD)
                : (ndd ? Emitter.insFormat.IF_RWR_RRD_SRD : Emitter.insFormat.IF_RRW_SRD)));
            if (kind == 3)
            {
                Assert.That(Last(codeGen).idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_RCX));
                Assert.That(Last(codeGen).idAddr().iiaAddrMode.amIndxReg, Is.EqualTo(REG_RDX));
            }
        });
    }

    [TestCase(1, false)]
    [TestCase(1, true)]
    [TestCase(3, false)]
    [TestCase(3, true)]
    public static void MemoryDestinationsPreserveRegisterAndImmediateForms(int kind, bool immediate)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var dst = Memory(compiler, kind);
            var src = Register(compiler, immediate ? REG_NA : REG_RAX);
            src.IsContained = immediate;

            var result = codeGen.Emitter.emitInsBinary(INS_add, EA_4BYTE, dst, src);

            Assert.That(result, Is.EqualTo(REG_NA));
            Assert.That(Last(codeGen).idInsFmt(), Is.EqualTo(kind == 3
                ? (immediate ? Emitter.insFormat.IF_ARW_CNS : Emitter.insFormat.IF_ARW_RRD)
                : (immediate ? Emitter.insFormat.IF_SRW_CNS : Emitter.insFormat.IF_SRW_RRD)));
        });
    }

    [TestCase(-1)]
    [TestCase(0)]
    [TestCase(3)]
    public static void ImplicitRegisterPairsKeepTheSourceLocation(int memoryKind)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var dst = Register(compiler, REG_RAX);
            var src = memoryKind < 0 ? Register(compiler, REG_RCX) : Memory(compiler, memoryKind);
            var result = codeGen.Emitter.emitInsBinary(INS_idiv, EA_4BYTE, dst, src);

            Assert.That(result, Is.EqualTo(REG_RAX));
            Assert.That(Last(codeGen).idInsFmt(), Is.EqualTo(memoryKind switch
            {
                -1 => Emitter.insFormat.IF_RRD,
                0 => Emitter.insFormat.IF_SRD,
                _ => Emitter.insFormat.IF_ARD,
            }));
        });
    }

    [Test]
    public static void FloatingImmediatesBecomeRealConstantDataReferences()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var dst = compiler.gtNewDconNode(TYP_DOUBLE, 0);
            dst.RegNum = REG_XMM0;
            var src = compiler.gtNewDconNode(TYP_DOUBLE, -0.0);
            src.IsContained = true;

            _ = codeGen.Emitter.emitInsBinary(INS_addsd, EA_8BYTE, dst, src);

            var section = codeGen.Emitter.emitConsDsc.dsdList ?? throw new AssertionException("Missing constant data.");
            Assert.That(BitConverter.ToUInt64(section.Data), Is.EqualTo(0x8000000000000000UL));
            Assert.That(Last(codeGen).idIsDspReloc(), Is.True);
            Assert.That(Last(codeGen).idInsFmt(), Is.EqualTo(Emitter.insFormat.IF_RWR_MRD));
        });
    }

    [Test]
    public static void SpillOperandsUnlinkByIdentityAndReturnTheTempToItsOwner()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            PrepareTemps(codeGen, 2);
            var first = Register(compiler, REG_RAX);
            var second = Register(compiler, REG_RAX);
            first.Flags |= GTF_SPILL;
            second.Flags |= GTF_SPILL;
            codeGen.RegSet.rsSpillTree(REG_RAX, first);
            codeGen.RegSet.rsSpillTree(REG_RAX, second);
            first.Flags |= GTF_NOREG_AT_USE;
            second.Flags |= GTF_NOREG_AT_USE;
            first.IsRegOptional = true;
            second.IsRegOptional = true;
            var dst = Register(compiler, REG_RCX);

            _ = codeGen.Emitter.emitInsBinary(INS_add, EA_4BYTE, dst, first);
            var firstNumber = Last(codeGen).idAddr().iiaLclVar.lvaVarNum();
            _ = codeGen.Emitter.emitInsBinary(INS_add, EA_4BYTE, dst, second);
            var secondNumber = Last(codeGen).idAddr().iiaLclVar.lvaVarNum();
            Assert.That(firstNumber, Is.Not.EqualTo(secondNumber));
            Assert.That(Last(codeGen).idInsFmt(), Is.EqualTo(Emitter.insFormat.IF_RRW_SRD));

            var temp1 = codeGen.RegSet.tmpGetTemp(TYP_INT);
            var temp2 = codeGen.RegSet.tmpGetTemp(TYP_INT);
            Assert.That(new[] { temp1.tdTempNum, temp2.tdTempNum }, Is.EquivalentTo(new[] { firstNumber, secondNumber }));
            codeGen.RegSet.tmpRlsTemp(temp1);
            codeGen.RegSet.tmpRlsTemp(temp2);
        });
    }

    [TestCase(GT_BSWAP, TYP_INT, REG_RAX, 1)]
    [TestCase(GT_BSWAP, TYP_LONG, REG_RCX, 2)]
    [TestCase(GT_BSWAP16, TYP_INT, REG_RAX, 2)]
    [TestCase(GT_BSWAP16, TYP_INT, REG_RCX, 3)]
    public static void RegisterByteSwapsRetainCopyRotateAndNormalization(
        genTreeOps oper, var_types type, regNumber destination, int count)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var operand = compiler.gtNewIconNode(type, 1);
            operand.RegNum = REG_RAX;
            var tree = compiler.gtNewUnaryNode(oper, type, operand);
            tree.RegNum = destination;

            codeGen.genCodeForBswap(tree);

            var descriptors = Descriptors(codeGen.Emitter);
            Assert.That(descriptors, Has.Count.EqualTo(count));
            var operationIndex = destination == REG_RAX ? 0 : 1;
            Assert.That(descriptors[operationIndex].idIns(), Is.EqualTo(oper == GT_BSWAP ? INS_bswap : INS_ror_N));
            Assert.That(descriptors[^1].idIns(), Is.EqualTo(oper == GT_BSWAP ? INS_bswap : INS_movzx));
        });
    }

    [TestCase(REG_RAX, REG_RCX, REG_RDX, INS_movbe)]
    [TestCase(REG_R16, REG_RCX, REG_RDX, INS_movbe_apx)]
    [TestCase(REG_RAX, REG_R16, REG_RDX, INS_movbe_apx)]
    [TestCase(REG_RAX, REG_RCX, REG_R17, INS_movbe_apx)]
    public static void ContainedByteSwapsUseMovbeAndHonorAllExtendedRegisters(
        regNumber destination, regNumber baseReg, regNumber indexReg, instruction expected)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            codeGen.Emitter.UseRex2Encodings = true;
            codeGen.Emitter.UsePromotedEvexEncodings = true;
            var operand = Memory(compiler, 3, baseReg, indexReg);
            var tree = compiler.gtNewUnaryNode(GT_BSWAP, TYP_INT, operand);
            tree.RegNum = destination;

            codeGen.genCodeForBswap(tree);

            Assert.That(Descriptors(codeGen.Emitter), Has.Count.EqualTo(1));
            Assert.That(Last(codeGen).idIns(), Is.EqualTo(expected));
            Assert.That(Last(codeGen).idReg1(), Is.EqualTo(destination));
        });
    }

    [TestCase(0)]
    [TestCase(3)]
    public static void ContainedNarrowByteSwapsNormalizeTheLoadedWord(int kind)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var operand = Memory(compiler, kind);
            operand.Type = TYP_USHORT;
            var tree = compiler.gtNewUnaryNode(GT_BSWAP16, TYP_INT, operand);
            tree.RegNum = REG_RAX;

            codeGen.genCodeForBswap(tree);

            var descriptors = Descriptors(codeGen.Emitter);
            Assert.That(descriptors, Has.Count.EqualTo(2));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_movbe));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(EA_2BYTE));
            Assert.That(descriptors[1].idIns(), Is.EqualTo(INS_movzx));
        });
    }

    [Test]
    public static void ByteSwapsReadSpilledOperandsWithoutReloadingThemFirst()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            PrepareTemps(codeGen, 1);
            var operand = Register(compiler, REG_RAX);
            operand.Flags |= GTF_SPILL;
            codeGen.RegSet.rsSpillTree(REG_RAX, operand);
            operand.Flags |= GTF_NOREG_AT_USE;
            operand.IsRegOptional = true;
            var tree = compiler.gtNewUnaryNode(GT_BSWAP, TYP_INT, operand);
            tree.RegNum = REG_RCX;

            codeGen.genCodeForBswap(tree);

            Assert.That(Descriptors(codeGen.Emitter), Has.Count.EqualTo(2));
            Assert.That(Last(codeGen).idIns(), Is.EqualTo(INS_movbe));
            Assert.That(Last(codeGen).idInsFmt(), Is.EqualTo(Emitter.insFormat.IF_RWR_SRD));
            var temp = codeGen.RegSet.tmpGetTemp(TYP_INT);
            codeGen.RegSet.tmpRlsTemp(temp);
        });
    }

    [TestCase(true, TYP_USHORT, false, true, false)]
    [TestCase(false, TYP_USHORT, false, true, true)]
    [TestCase(false, TYP_SHORT, false, true, true)]
    [TestCase(false, TYP_INT, false, true, false)]
    [TestCase(false, TYP_USHORT, true, true, false)]
    [TestCase(false, TYP_USHORT, false, false, false)]
    public static void ByteSwapNormalizationRequiresAnAdjacentUncheckedNarrowingUse(
        bool minopts, var_types castType, bool overflow, bool sameOperand, bool omit)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var operand = Register(compiler, REG_RAX);
            var tree = compiler.gtNewUnaryNode(GT_BSWAP16, TYP_INT, operand);
            tree.RegNum = REG_RAX;
            var cast = compiler.gtNewCastNode(TYP_INT, sameOperand ? tree : operand, false, castType);
            if (overflow)
            {
                cast.Flags |= GTF_OVERFLOW;
            }
            tree.Next = cast;
            cast.Prev = tree;

            Assert.That(codeGen.genCanOmitNormalizationForBswap16(tree), Is.EqualTo(omit));
            codeGen.genCodeForBswap(tree);
            Assert.That(Descriptors(codeGen.Emitter), Has.Count.EqualTo(omit ? 1 : 2));
        }, minopts);
    }

#if DEBUG
    [Test]
    public static void RejectedBinaryRecordingDoesNotConsumeSpillOwnership()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            PrepareTemps(codeGen, 1);
            var src = Register(compiler, REG_RAX);
            src.Flags |= GTF_SPILL;
            codeGen.RegSet.rsSpillTree(REG_RAX, src);
            src.Flags |= GTF_NOREG_AT_USE;
            src.IsRegOptional = true;
            var dst = Register(compiler, REG_RCX);
            compiler.opts.dspCode = true;

            _ = Assert.Throws<FatalJitException>(() => codeGen.Emitter.emitInsBinary(INS_add, EA_4BYTE, dst, src));

            Assert.That(Descriptors(codeGen.Emitter), Has.Count.EqualTo(1));
            compiler.opts.dspCode = false;
            _ = codeGen.Emitter.emitInsBinary(INS_add, EA_4BYTE, dst, src);
            Assert.That(Descriptors(codeGen.Emitter), Has.Count.EqualTo(2));
        });
    }
#endif

    private static GenTreeIntCon Register(Compiler compiler, regNumber reg)
    {
        var node = compiler.gtNewIconNode(TYP_INT, 7);
        node.RegNum = reg;
        return node;
    }

    private static GenTree Memory(Compiler compiler, int kind,
        regNumber baseReg = REG_RCX, regNumber indexReg = REG_RDX)
    {
        GenTree node;
        if (kind == 0)
        {
            node = compiler.gtNewLclvNode(TYP_INT, 0);
        }
        else if (kind == 1)
        {
            node = new GenTreeLclFld(GT_LCL_FLD, TYP_INT, 0, 4);
        }
        else if (kind == 2)
        {
            var address = new GenTreeLclFld(GT_LCL_ADDR, TYP_BYREF, 0, 4) { IsContained = true };
            node = new GenTreeIndir(GT_IND, TYP_INT, address);
        }
        else
        {
            var baseNode = compiler.gtNewIconNode(TYP_I_IMPL, 0);
            baseNode.RegNum = baseReg;
            var indexNode = Register(compiler, indexReg);
            var address = new GenTreeAddrMode(TYP_BYREF, baseNode, indexNode, 4, 24) { IsContained = true };
            node = new GenTreeIndir(GT_IND, TYP_INT, address);
        }
        node.IsContained = true;

        return node;
    }

    private static void PrepareTemps(CodeGen codeGen, int count)
    {
        codeGen.RegSet.tmpInit();
        codeGen.RegSet.tmpPreAllocateTemps(TYP_INT, (uint)count);
        var temps = new TempDsc[count];
        for (var i = 0; i < count; i++)
        {
            temps[i] = codeGen.RegSet.tmpGetTemp(TYP_INT);
            temps[i].tdTempOffs = -32 - (i * 8);
        }

        foreach (var temp in temps)
        {
            codeGen.RegSet.tmpRlsTemp(temp);
        }
    }

    private static void WithCodeGen(Action<Compiler, CodeGen> action, bool minopts = true)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_INT, REG_RAX, (compiler, codeGen, _) =>
        {
            compiler.eeInfoInitialized = true;
            compiler.eeInfo.targetAbi = CORINFO_RUNTIME_ABI.CORINFO_CORECLR_ABI;
            compiler.compCurLife = VarSetOps.MakeEmpty(compiler);
            LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);
            compiler.lvaTable[0].lvLRACandidate = false;
            codeGen.RegSet.ClearMaskVars();
            action(compiler, codeGen);
        }, minopts);
    }

    private static Emitter.instrDesc Last(CodeGen codeGen) => Descriptors(codeGen.Emitter)[^1];

    private static List<Emitter.instrDesc> Descriptors(Emitter emitter)
        => CurrentDescriptors(emitter) ?? throw new AssertionException("Missing descriptor buffer.");

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "treeLifeUpdater")]
    private static extern ref TreeLifeUpdater? LifeUpdater(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentDescriptors(Emitter emitter);
}
