// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class Arm32PhysicalRegisterCodeGenTests
{
    [TestCase(TYP_REF)]
    [TestCase(TYP_BYREF)]
    public static void PhysicalRegisterReadsMoveAndTransferGcState(var_types type)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((_, codeGen) =>
        {
            var source = REG_R2;
            var target = REG_R3;
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

    private static regMaskTP Mask(regNumber reg)
    {
        return regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentDescriptors(Emitter emitter);
}
#endif
