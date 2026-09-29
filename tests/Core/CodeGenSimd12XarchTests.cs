// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Linq;
using NUnit.Framework;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenSimd12XarchTests
{
    [Test]
    public static void IndirectLoadInsertsTheThirdLaneWithoutReadingAFullVector()
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var address = Register(compiler, TYP_BYREF, REG_RAX);
            var load = new GenTreeIndir(GT_IND, TYP_SIMD12, address) { RegNum = REG_XMM1 };

            codeGen.genLoadIndTypeSimd12(load);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(2));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_movsd_simd));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(EA_8BYTE));
            Assert.That(descriptors[1].idIns(), Is.EqualTo(INS_insertps));
            Assert.That(load.Addr.AsAddrMode().Offset, Is.EqualTo(8));
        });
    }

    [Test]
    public static void LocalStoreUsesTwoDistinctWidths(
        [Values(false, true)] bool directHelper, [Values(false, true)] bool zero)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.lvaTable[0].Type = TYP_SIMD12;
            compiler.lvaTable[0].RegNum = REG_STK;
            var data = new GenTreeVecCon(TYP_SIMD12) { RegNum = REG_XMM1 };
            data.SimdVal.u32[0] = zero ? 0u : 1u;
            var store = compiler.gtNewStoreLclVarNode(0, data);
            store.RegNum = REG_NA;

            if (directHelper)
            {
                codeGen.genEmitStoreLclTypeSimd12(store, 0, 0);
            }
            else
            {
                codeGen.genStoreLclTypeSimd12(store);
            }

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(2));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_movsd_simd));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(EA_8BYTE));
            Assert.That(descriptors[1].idIns(), Is.EqualTo(zero ? INS_movss : INS_extractps));
            Assert.That(descriptors[1].idAddr().iiaLclVar.lvaOffset(), Is.EqualTo(8u));
        });
    }

    [Test]
    public static void LocalLoadInsertsAndClearsTheFinalLane([Values(false, true)] bool directHelper)
    {
        CodeGenBinaryTests.WithCodeGen((_, codeGen) =>
        {
            var load = new GenTreeLclFld(GT_LCL_FLD, TYP_SIMD12, 0, 4) { RegNum = REG_XMM1 };

            if (directHelper)
            {
                codeGen.genEmitLoadLclTypeSimd12(REG_XMM1, 0, 4);
            }
            else
            {
                codeGen.genLoadLclTypeSimd12(load);
            }

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(2));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_movsd_simd));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(EA_8BYTE));
            Assert.That(descriptors[1].idIns(), Is.EqualTo(INS_insertps));
            Assert.That(descriptors[1].idAddr().iiaLclVar.lvaOffset(), Is.EqualTo(12u));
        });
    }

    [Test]
    public static void UpperClearTargetsOnlyTheFourthLane()
    {
        CodeGenBinaryTests.WithCodeGen((_, codeGen) =>
        {
            codeGen.genSimd12UpperClear(REG_XMM1);

            var descriptor = Descriptors(codeGen).Single();
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_insertps));
            Assert.That(descriptor.idOpSize(), Is.EqualTo(EA_16BYTE));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_XMM1));
        });
    }
}
