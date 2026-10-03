// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class Arm64CodeGenSimdUpperSaveTests
{
    [Test]
    public static void RegisterSaveExtractsTheUpperDoubleLane()
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            var source = CreateSource(compiler);
            var save = new GenTreeIntrinsic(TYP_DOUBLE, source, NI_SIMD_UpperSave, methodHandle: null)
            {
                RegNum = REG_V1,
            };

            codeGen.genSimdUpperSave(save);

            var descriptors = Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_mov));
            Assert.That(descriptors[0].idInsFmt(), Is.EqualTo(IF_DV_2F));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(EA_8BYTE));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_V1));
            Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_V0));
            Assert.That((nint)descriptors[0].idAddr().iiaAddr, Is.EqualTo((nint)1));
        });
    }

    [Test]
    public static void SpilledRegisterSaveStoresTheUpperDoubleLaneInTheLocalHome()
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            var source = CreateSource(compiler);
            var save = new GenTreeIntrinsic(TYP_DOUBLE, source, NI_SIMD_UpperSave, methodHandle: null)
            {
                RegNum = REG_V1,
                Flags = GTF_SPILL,
            };

            codeGen.genSimdUpperSave(save);

            var descriptors = Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter);
            Assert.That(descriptors, Has.Count.EqualTo(2));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_mov));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_V1));
            Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_V0));
            Assert.That(descriptors[1].idIns(), Is.EqualTo(INS_str));
            Assert.That(descriptors[1].idOpSize(), Is.EqualTo(EA_8BYTE));
            Assert.That(descriptors[1].idReg1(), Is.EqualTo(REG_V1));
            Assert.That(descriptors[1].idIsLclVar(), Is.True);
            Assert.That(descriptors[1].idAddr().iiaLclVar.lvaVarNum(), Is.Zero);
            Assert.That(descriptors[1].idAddr().iiaLclVar.lvaOffset(), Is.EqualTo(8u));
        });
    }

    private static GenTreeLclVar CreateSource(Compiler compiler)
    {
        compiler.lvaTable[0].Type = TYP_SIMD16;
        compiler.lvaTable[0].lvLRACandidate = true;
        compiler.lvaTable[0].RegNum = REG_V0;

        return new GenTreeLclVar(TYP_SIMD16, 0)
        {
            RegNum = REG_V0,
        };
    }
}
#endif
