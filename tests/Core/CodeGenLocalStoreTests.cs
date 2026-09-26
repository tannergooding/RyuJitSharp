// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenLocalStoreTests
{
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void StackStoresRetainImmediateSelectionOffsetsAndHomes(bool field, bool immediate)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var value = Register(compiler, TYP_INT, immediate ? REG_NA : REG_RCX);
            value.IsContained = immediate;
            GenTreeLclVarCommon tree;
            if (field)
            {
                compiler.lvaTable[0].Type = TYP_LONG;
                tree = compiler.gtNewStoreLclFldNode(TYP_INT, 0, 4, value);
            }
            else
            {
                tree = compiler.gtNewStoreLclVarNode(0, value);
#if DEBUG
                tree.AsLclVar().LclIlOffs = immediate ? 0x24 : unchecked((int)BAD_IL_OFFSET);
#endif
            }
            tree.RegNum = REG_NA;

            if (field)
            {
                codeGen.genCodeForStoreLclFld(tree.AsLclFld());
            }
            else
            {
                codeGen.genCodeForStoreLclVar(tree.AsLclVar());
            }

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_mov));
            Assert.That(descriptors[0].idAddr().iiaLclVar.lvaOffset(), Is.EqualTo(field ? 4u : 0u));
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_STK));
            if (immediate)
            {
                Assert.That(InstructionConstant(codeGen.Emitter, descriptors[0]), Is.EqualTo((nint)7));
            }
#if DEBUG
            if (!field)
            {
                var info = descriptors[0].idDebugOnlyInfo() ??
                    throw new AssertionException("Missing store debug information.");
                Assert.That(info.idVarRefOffs, Is.EqualTo(immediate ? 0x24u : unchecked((uint)BAD_IL_OFFSET)));
            }
#endif
        });
    }

    [TestCase(0, INS_xor)]
    [TestCase(7, INS_mov)]
    public static void RegisterStoresRematerializeOnlyZeroInsteadOfCopying(int value, instruction ins)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.lvaTable[0].lvLRACandidate = true;
            var source = compiler.gtNewIconNode(TYP_INT, value);
            source.RegNum = REG_RCX;
            source.IsReuseRegVal = true;
            var tree = compiler.gtNewStoreLclVarNode(0, source);
            tree.RegNum = REG_RAX;

            codeGen.genCodeForStoreLclVar(tree);

            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(1));
            Assert.That(Descriptors(codeGen)[0].idIns(), Is.EqualTo(ins));
            Assert.That(source.IsContained, Is.EqualTo(value == 0));
            Assert.That(source.IsReuseRegVal, Is.EqualTo(value != 0));
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_RAX));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ReusedFloatingZeroPreservesItsSign(bool negative)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            EnableAvx2(compiler);
            compiler.lvaTable[0].Type = TYP_DOUBLE;
            compiler.lvaTable[0].lvLRACandidate = true;
            compiler.lvaTable[0].RegNum = REG_XMM0;
            var source = compiler.gtNewDconNode(TYP_DOUBLE, negative ? -0.0 : 0.0);
            source.RegNum = REG_XMM1;
            source.IsReuseRegVal = true;
            var tree = compiler.gtNewStoreLclVarNode(0, source);
            tree.RegNum = REG_XMM0;

            codeGen.genCodeForStoreLclVar(tree);

            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(1));
            Assert.That(Descriptors(codeGen)[0].idIns(), Is.EqualTo(negative ? INS_movaps : INS_xorps));
            Assert.That(source.IsContained, Is.EqualTo(!negative));
            Assert.That(source.IsReuseRegVal, Is.EqualTo(negative));
        });
    }

    [TestCase(false, false, false)]
    [TestCase(false, true, false)]
    [TestCase(true, false, false)]
    [TestCase(false, true, true)]
    public static void ContainedBitcastStoresUseTheSourceRegisterClass(bool field, bool register, bool wide)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var type = wide ? TYP_DOUBLE : TYP_FLOAT;
            compiler.lvaTable[0].Type = type;
            compiler.lvaTable[0].lvLRACandidate = register;
            compiler.lvaTable[0].RegNum = register ? REG_XMM0 : REG_STK;
            var value = Register(compiler, wide ? TYP_LONG : TYP_INT, REG_RCX);
            var bitcast = new GenTreeUnOp(GT_BITCAST, type, value) { IsContained = true };
            GenTreeLclVarCommon tree = field
                ? compiler.gtNewStoreLclFldNode(type, 0, 0, bitcast)
                : compiler.gtNewStoreLclVarNode(0, bitcast);
            tree.RegNum = register ? REG_XMM0 : REG_NA;

            if (field)
            {
                codeGen.genCodeForStoreLclFld(tree.AsLclFld());
            }
            else
            {
                codeGen.genCodeForStoreLclVar(tree.AsLclVar());
            }

            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(1));
            var ins = register ? wide ? INS_movd64 : INS_movd32 : INS_mov;
            Assert.That(Descriptors(codeGen)[0].idIns(), Is.EqualTo(ins));
            Assert.That(Descriptors(codeGen)[0].idOpSize(), Is.EqualTo(wide ? EA_8BYTE : EA_4BYTE));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void BitcastNodesLoadContainedLocalsOrCopyAcrossRegisterClasses(bool memory)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            GenTree source = memory ? compiler.gtNewLclvNode(TYP_INT, 0)
                : Register(compiler, TYP_INT, REG_RCX);
            source.IsContained = memory;
            var tree = new GenTreeUnOp(GT_BITCAST, TYP_FLOAT, source) { RegNum = REG_XMM0 };

            codeGen.genCodeForBitCast(tree);

            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(1));
            Assert.That(Descriptors(codeGen)[0].idIns(), Is.EqualTo(memory ? INS_movss : INS_movd32));
        });
    }

    [TestCase(false, false, false)]
    [TestCase(false, true, false)]
    [TestCase(true, false, false)]
    [TestCase(true, true, false)]
    [TestCase(false, false, true)]
    [TestCase(true, false, true)]
    public static void Simd12StoresPreserveTheZeroFastPathAndRegisterCopies(bool zero, bool register, bool field)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.lvaTable[0].Type = field ? TYP_SIMD16 : TYP_SIMD12;
            compiler.lvaTable[0].lvLRACandidate = register;
            compiler.lvaTable[0].RegNum = register ? REG_XMM0 : REG_STK;
            var source = new GenTreeVecCon(TYP_SIMD12) { RegNum = REG_XMM1 };
            if (!zero)
            {
                source.SimdVal.u32[0] = 1;
            }
            GenTreeLclVarCommon tree = field
                ? compiler.gtNewStoreLclFldNode(TYP_SIMD12, 0, 4, source)
                : compiler.gtNewStoreLclVarNode(0, source);
            tree.RegNum = register ? REG_XMM0 : REG_NA;

            if (field)
            {
                codeGen.genCodeForStoreLclFld(tree.AsLclFld());
            }
            else
            {
                codeGen.genCodeForStoreLclVar(tree.AsLclVar());
            }

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(register ? 1 : 2));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(register ? INS_movaps : INS_movsd_simd));
            if (!register)
            {
                Assert.That(descriptors[1].idIns(), Is.EqualTo(zero ? INS_movss : INS_extractps));
                Assert.That(descriptors[1].idAddr().iiaLclVar.lvaOffset(), Is.EqualTo(field ? 12u : 8u));
                Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_STK));
            }
        });
    }

    [TestCase(TYP_REF)]
    [TestCase(TYP_BYREF)]
    public static void StackStoresTransferGcLivenessFromTheOperandToTheHome(var_types type)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.lvaTable[0].Type = type;
            compiler.lvaTable[0].RegNum = REG_STK;
            VarSetOps.AddElemD(compiler, codeGen.GCInfo.gcTrkStkPtrLcls, 0);
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RCX, type);
            var tree = compiler.gtNewStoreLclVarNode(0, Register(compiler, type, REG_RCX));
            tree.RegNum = REG_NA;

            codeGen.genCodeForStoreLclVar(tree);

            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(RBM_NONE));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(RBM_NONE));
            Assert.That(VarSetOps.IsMember(compiler, codeGen.GCInfo.gcVarPtrSetCur, 0), Is.True);
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_STK));
            Assert.That(Descriptors(codeGen)[0].idGCref(),
                Is.EqualTo(type == TYP_REF ? GCInfo.GCtype.GCT_GCREF : GCInfo.GCtype.GCT_BYREF));
        });
    }

    [TestCase(TYP_BYTE, true, INS_movsx)]
    [TestCase(TYP_UBYTE, false, INS_movzx)]
    [TestCase(TYP_LONG, true, INS_mov)]
    [TestCase(TYP_FLOAT, false, INS_movss)]
    [TestCase(TYP_DOUBLE, false, INS_movsd_simd)]
    [TestCase(TYP_SIMD16, false, INS_movups)]
    [TestCase(TYP_DOUBLE, true, INS_movaps)]
    [TestCase(TYP_MASK, true, INS_kmovq_msk)]
    public static void ExtendedMoveSelectionRetainsNativeTypeAndSourceRules(
        var_types type, bool sourceInRegister, instruction ins)
    {
        CodeGenBinaryTests.WithCodeGen((_, codeGen) =>
            Assert.That(codeGen.ins_Move_Extend(type, sourceInRegister), Is.EqualTo(ins)));
    }

#if DEBUG
    [Test]
    public static void DisassemblyRecordsLocalStoresAndZeroRewriting()
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.lvaTable[0].lvLRACandidate = true;
            var source = compiler.gtNewIconNode(TYP_INT, 0);
            source.RegNum = REG_RCX;
            source.IsReuseRegVal = true;
            var tree = compiler.gtNewStoreLclVarNode(0, source);
            tree.RegNum = REG_RAX;
            compiler.opts.dspCode = true;
            var diagnostic = InstructionRecordingTestSupport.Capture(() => codeGen.genCodeForStoreLclVar(tree));
            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(1));
            Assert.That(diagnostic, Does.Contain("xor"));
        });
    }
#endif
}
