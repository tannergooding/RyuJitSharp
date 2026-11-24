// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

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
        Assume.That(Configuration.TARGET_ARM64);
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            var baseAddress = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            baseAddress.RegNum = REG_ARM64_R1;
            var address = new GenTreeAddrMode(TYP_I_IMPL, baseAddress, null, 0, 8) { IsContained = true };
            var indir = new GenTreeIndir(GT_IND, TYP_INT, address);

            codeGen.Emitter.emitInsLoadStoreOpArm64(INS_ldr, EA_4BYTE, REG_ARM64_R0, indir);

            var descriptors = Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_ldr));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_ARM64_R0));
            Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_ARM64_R1));
            Assert.That(Emitter.emitGetInsSC(descriptors[0]), Is.EqualTo((nint)2));
        });
    }

    [Test]
    public static void ContainedScaledIndexLoadUsesExtendedAddressing()
    {
        Assume.That(Configuration.TARGET_ARM64);
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            var baseAddress = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            baseAddress.RegNum = REG_ARM64_R1;
            var index = compiler.gtNewLclvNode(TYP_INT, 0);
            index.RegNum = REG_ARM64_R2;
            var address = new GenTreeAddrMode(TYP_I_IMPL, baseAddress, index, 4, 0) { IsContained = true };
            var indir = new GenTreeIndir(GT_IND, TYP_INT, address);

            codeGen.Emitter.emitInsLoadStoreOpArm64(INS_ldr, EA_4BYTE, REG_ARM64_R0, indir);

            var descriptors = Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_ldr));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_ARM64_R0));
            Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_ARM64_R1));
            Assert.That(descriptors[0].idReg3(), Is.EqualTo(REG_ARM64_R2));
        });
    }

    [TestCase(false, 1)]
    [TestCase(true, 2)]
    public static void VolatileLoadsDoNotUseTheRedundantLoadPeephole(bool isVolatile, int expectedCount)
    {
        Assume.That(Configuration.TARGET_ARM64);
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            var baseAddress = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            baseAddress.RegNum = REG_ARM64_R1;
            var firstAddress = new GenTreeAddrMode(TYP_I_IMPL, baseAddress, null, 0, 8) { IsContained = true };
            var first = new GenTreeStoreInd(TYP_LONG, firstAddress, compiler.gtNewIconNode(TYP_LONG, 1));
            codeGen.Emitter.emitInsLoadStoreOpArm64(INS_str, EA_8BYTE, REG_ARM64_R0, first);

            var secondAddress = new GenTreeAddrMode(TYP_I_IMPL, baseAddress, null, 0, 8) { IsContained = true };
            var second = new GenTreeIndir(GT_IND, TYP_LONG, secondAddress);
            if (isVolatile)
            {
                second.Flags |= GenTreeFlags.GTF_IND_VOLATILE;
            }

            codeGen.Emitter.emitInsLoadStoreOpArm64(INS_ldr, EA_8BYTE, REG_ARM64_R0, second);

            Assert.That(Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter), Has.Count.EqualTo(expectedCount));
        }, minOpts: false);
    }

    [TestCase(false, 1)]
    [TestCase(true, 2)]
    public static void VolatileRegisterAddressLoadsDoNotUseThePeephole(bool isVolatile, int expectedCount)
    {
        Assume.That(Configuration.TARGET_ARM64);
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            var address = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            address.RegNum = REG_ARM64_R1;
            var store = new GenTreeStoreInd(TYP_LONG, address, compiler.gtNewIconNode(TYP_LONG, 1));
            codeGen.Emitter.emitInsLoadStoreOpArm64(INS_str, EA_8BYTE, REG_ARM64_R0, store);

            var load = new GenTreeIndir(GT_IND, TYP_LONG, address);
            if (isVolatile)
            {
                load.Flags |= GenTreeFlags.GTF_IND_VOLATILE;
            }

            codeGen.Emitter.emitInsLoadStoreOpArm64(INS_ldr, EA_8BYTE, REG_ARM64_R0, load);

            Assert.That(Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter), Has.Count.EqualTo(expectedCount));
        }, minOpts: false);
    }

    [Test]
    public static void StackBaseRegisterIsNormalizedForRedundantLoadChecks()
    {
        Assume.That(Configuration.TARGET_ARM64);
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            var baseAddress = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            baseAddress.RegNum = REG_ARM64_SP;
            var address = new GenTreeAddrMode(TYP_I_IMPL, baseAddress, null, 0, 8) { IsContained = true };
            var store = new GenTreeStoreInd(TYP_LONG, address, compiler.gtNewIconNode(TYP_LONG, 1));
            codeGen.Emitter.emitInsLoadStoreOpArm64(INS_str, EA_8BYTE, REG_ARM64_R0, store);
            var load = new GenTreeIndir(GT_IND, TYP_LONG, address);
            codeGen.Emitter.emitInsLoadStoreOpArm64(INS_ldr, EA_8BYTE, REG_ARM64_R0, load);

            Assert.That(Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter), Has.Count.EqualTo(1));
        }, minOpts: false);
    }

    [Test]
    public static void RedundantLoadPeepholePreservesDifferentGcMetadata()
    {
        Assume.That(Configuration.TARGET_ARM64);
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            var baseAddress = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            baseAddress.RegNum = REG_ARM64_R1;
            var integerAddress = new GenTreeAddrMode(TYP_I_IMPL, baseAddress, null, 0, 8) { IsContained = true };
            var integerStore = new GenTreeStoreInd(TYP_LONG, integerAddress, compiler.gtNewIconNode(TYP_LONG, 1));
            codeGen.Emitter.emitInsLoadStoreOpArm64(INS_str, EA_8BYTE, REG_ARM64_R0, integerStore);

            var referenceAddress = new GenTreeAddrMode(TYP_I_IMPL, baseAddress, null, 0, 8) { IsContained = true };
            var referenceLoad = new GenTreeIndir(GT_IND, TYP_REF, referenceAddress);
            codeGen.Emitter.emitInsLoadStoreOpArm64(INS_ldr, EA_64BIT_GCREF, REG_ARM64_R0, referenceLoad);

            var descriptors = Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter);
            Assert.That(descriptors, Has.Count.EqualTo(2));
            Assert.That(descriptors[0].idGCref(), Is.EqualTo(GCInfo.GCtype.GCT_NONE));
            Assert.That(descriptors[1].idGCref(), Is.EqualTo(GCInfo.GCtype.GCT_GCREF));
        }, minOpts: false);
    }

    [Test]
    public static void RedundantVectorLoadDoesNotRequireEightByteGprSize()
    {
        Assume.That(Configuration.TARGET_ARM64 && Configuration.FEATURE_SIMD);
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            var baseAddress = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            baseAddress.RegNum = REG_ARM64_R1;
            var address = new GenTreeAddrMode(TYP_I_IMPL, baseAddress, null, 0, 16) { IsContained = true };
            var store = new GenTreeStoreInd(TYP_SIMD16, address, new GenTreeVecCon(TYP_SIMD16));
            codeGen.Emitter.emitInsLoadStoreOpArm64(INS_str, EA_16BYTE, REG_ARM64_V0, store);
            var load = new GenTreeIndir(GT_IND, TYP_SIMD16, address);
            codeGen.Emitter.emitInsLoadStoreOpArm64(INS_ldr, EA_16BYTE, REG_ARM64_V0, load);

            Assert.That(Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter), Has.Count.EqualTo(1));
        }, minOpts: false);
    }
}
