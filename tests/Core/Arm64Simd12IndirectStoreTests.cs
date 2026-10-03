// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using NUnit.Framework;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class Arm64Simd12IndirectStoreTests
{
#if FEATURE_SIMD
    [Test]
    public static void StoresEightBytesThenExtractsAndStoresTheUpperFourBytes()
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            var address = compiler.gtNewIconNode(TYP_BYREF, 0);
            address.RegNum = REG_R2;
            var data = new GenTreeVecCon(TYP_SIMD12)
            {
                RegNum = REG_V1,
            };
            var store = new GenTreeStoreInd(TYP_SIMD12, address, data);
            codeGen.InternalRegisters.Add(store, RBM_R10);

            codeGen.genCodeForStoreInd(store);

            var descriptors = Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter);
            Assert.That(descriptors, Has.Count.EqualTo(3));

            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_str));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(EA_8BYTE));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_V1));
            Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_R2));

            Assert.That(descriptors[1].idIns(), Is.EqualTo(INS_mov));
            Assert.That(descriptors[1].idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(descriptors[1].idReg1(), Is.EqualTo(REG_R10));
            Assert.That(descriptors[1].idReg2(), Is.EqualTo(REG_V1));
            Assert.That((nint)descriptors[1].idAddr().iiaAddr, Is.EqualTo((nint)2));

            Assert.That(descriptors[2].idIns(), Is.EqualTo(INS_str));
            Assert.That(descriptors[2].idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(descriptors[2].idReg1(), Is.EqualTo(REG_R10));
            Assert.That(descriptors[2].idReg2(), Is.EqualTo(REG_R2));
            Assert.That((nint)descriptors[2].idAddr().iiaAddr, Is.EqualTo((nint)8));
            Assert.That(store.Addr, Is.SameAs(address));
        });
    }
#endif

    [Test]
    public static void ScalarStoresStopAtTheUnportedIndirectStoreEmitter()
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            var address = compiler.gtNewIconNode(TYP_BYREF, 0);
            address.RegNum = REG_R2;
            var data = compiler.gtNewIconNode(TYP_INT, 7);
            data.RegNum = REG_R3;
            var store = new GenTreeStoreInd(TYP_INT, address, data);

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForStoreInd(store)) ??
                throw new AssertionException("The unported ARM64 indirect-store emitter did not fail.");

            Assert.That(failure.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure.Message, Does.Contain("ARM64 indirect-store emitter recording is not yet ported."));
        });
    }
}
#endif
