// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class Arm64PhysicalRegisterCodeGenTests
{
    [TestCase(TYP_REF)]
    [TestCase(TYP_BYREF)]
    public static void PhysicalRegisterReadsMoveAndTransferGcState(var_types type)
    {
        Arm64CalleeSavedRegisterTests.WithCodeGen((_, codeGen) =>
        {
            var source = REG_R1;
            var target = REG_R2;
            var tree = new GenTreePhysReg(source, type) { RegNum = target };
            codeGen.GCInfo.gcMarkRegPtrVal(source, type);

            codeGen.genCodeForPhysReg(tree);

            var descriptors = CurrentDescriptors(codeGen.Emitter)
                ?? throw new AssertionException("Missing descriptor buffer.");
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_mov));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(target));
            Assert.That(descriptors[0].idReg2(), Is.EqualTo(source));

            var expected = Mask(source) | Mask(target);
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur,
                Is.EqualTo(type == TYP_REF ? expected : RBM_NONE));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur,
                Is.EqualTo(type == TYP_BYREF ? expected : RBM_NONE));
        });
    }

    [Test]
    public static void FfrPhysicalRegisterReadsUseScalableSVEInstruction()
    {
        Arm64CalleeSavedRegisterTests.WithCodeGen((_, codeGen) =>
        {
            var tree = new GenTreePhysReg(REG_FFR, TYP_MASK) { RegNum = REG_P0 };
            codeGen.genCodeForPhysReg(tree);

            var descriptors = CurrentDescriptors(codeGen.Emitter)
                ?? throw new AssertionException("Missing descriptor buffer.");
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_sve_rdffr));
            Assert.That(descriptors[0].idInsFmt(), Is.EqualTo(IF_SVE_DH_1A));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(EA_SCALABLE));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_P0));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(RBM_NONE));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(RBM_NONE));
        });
    }

    private static regMaskTP Mask(regNumber reg)
    {
        return regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentDescriptors(Emitter emitter);
}
#endif
