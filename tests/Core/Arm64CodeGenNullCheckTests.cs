// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using NUnit.Framework;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm64CodeGenNullCheckTests
{
    [Test]
    public static void NullChecksLoadIntoTheZeroRegisterToPreserveFaulting()
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            var address = compiler.gtNewIconNode(TYP_REF, 7);
            address.RegNum = REG_R3;
            codeGen.GCInfo.gcMarkRegPtrVal(REG_R3, TYP_REF);

            var tree = new GenTreeIndir(GT_NULLCHECK, TYP_LONG, address);
            codeGen.genCodeForNullCheck(tree);

            var descriptors = Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_ldr));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(EA_8BYTE));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_ZR));
            Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_R3));
        });
    }
}
#endif
