// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm64Simd12IndirectLoadTests
{
#if FEATURE_SIMD
    [Test]
    public static void LoadsEightBytesThenInsertsTheUpperFourBytes()
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            var address = compiler.gtNewIconNode(TYP_BYREF, 0);
            address.RegNum = REG_R2;
            var load = new GenTreeIndir(GT_IND, TYP_SIMD12, address)
            {
                RegNum = REG_V0,
            };
            codeGen.InternalRegisters.Add(load, RBM_R10);

            codeGen.genLoadIndTypeSimd12(load);

            var descriptors = Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter);
            Assert.That(descriptors, Has.Count.EqualTo(3));

            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_ldr));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(EA_8BYTE));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_V0));
            Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_R2));

            Assert.That(descriptors[1].idIns(), Is.EqualTo(INS_ldr));
            Assert.That(descriptors[1].idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(descriptors[1].idReg1(), Is.EqualTo(REG_R10));
            Assert.That(descriptors[1].idReg2(), Is.EqualTo(REG_R2));
            // A 4-byte load encodes an 8-byte offset as two words.
            Assert.That(descriptors[1].idSmallCns(), Is.EqualTo(2));

            Assert.That(descriptors[2].idIns(), Is.EqualTo(INS_mov));
            Assert.That(descriptors[2].idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(descriptors[2].idReg1(), Is.EqualTo(REG_V0));
            Assert.That(descriptors[2].idReg2(), Is.EqualTo(REG_R10));
            Assert.That(descriptors[2].idSmallCns(), Is.EqualTo(2));
            Assert.That(load.RegNum, Is.EqualTo(REG_V0));
        });
    }
#endif
}
#endif
