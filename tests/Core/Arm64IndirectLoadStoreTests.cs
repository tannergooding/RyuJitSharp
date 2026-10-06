// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class Arm64IndirectLoadStoreTests
{
    [Test]
    public static void ContainedBaseOffsetLoadUsesImmediateAddressing()
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            var baseAddress = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            baseAddress.RegNum = REG_R1;
            var address = new GenTreeAddrMode(TYP_I_IMPL, baseAddress, null, 0, 8) { IsContained = true };
            var indir = new GenTreeIndir(GT_IND, TYP_INT, address);

            codeGen.Emitter.emitInsLoadStoreOp(INS_ldr, EA_4BYTE, REG_R0, indir);

            var descriptors = Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_ldr));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_R0));
            Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_R1));
            Assert.That(Emitter.emitGetInsSC(descriptors[0]), Is.EqualTo((nint)2));
        });
    }

    [Test]
    public static void ContainedScaledIndexLoadUsesExtendedAddressing()
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            var baseAddress = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            baseAddress.RegNum = REG_R1;
            var index = compiler.gtNewLclvNode(TYP_INT, 0);
            index.RegNum = REG_R2;
            var address = new GenTreeAddrMode(TYP_I_IMPL, baseAddress, index, 4, 0) { IsContained = true };
            var indir = new GenTreeIndir(GT_IND, TYP_INT, address);

            codeGen.Emitter.emitInsLoadStoreOp(INS_ldr, EA_4BYTE, REG_R0, indir);

            var descriptors = Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_ldr));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_R0));
            Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_R1));
            Assert.That(descriptors[0].idReg3(), Is.EqualTo(REG_R2));
        });
    }
}
#endif
