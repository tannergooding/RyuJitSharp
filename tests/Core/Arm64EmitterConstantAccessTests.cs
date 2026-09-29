// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm64EmitterConstantAccessTests
{
    [TestCase(-64L, false)]
    [TestCase(0L, false)]
    [TestCase(63L, false)]
    [TestCase(-65L, true)]
    [TestCase(64L, true)]
    [TestCase(long.MinValue, true)]
    [TestCase(long.MaxValue, true)]
    public static void ConstantAccessPreservesSmallAndLargeValues(long value, bool large)
    {
        var descriptor = AccessEmitter.Constant((nint)value, large);
        Assert.That(ReadConstant(null, descriptor), Is.EqualTo((nint)value));
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public static void RelocationAccessCombinesBothFlags(bool displacement, bool constant)
    {
        var descriptor = AccessEmitter.Constant(0, false);
        descriptor.idSetIsDspReloc(displacement);
        if (constant)
        {
            descriptor.idSetIsCnsReloc();
        }

        Assert.That(IsReloc(descriptor), Is.EqualTo(displacement || constant));
    }

    [Test]
    public static void LocalAndTlsFlagsPreserveConstantAndRegisterState()
    {
        var descriptor = AccessEmitter.Constant(31, false);
        descriptor.idReg1(REG_R19);
        descriptor.idReg2(REG_R28);
        Assert.That(IsLocal(descriptor), Is.False);
        Assert.That(IsTls(descriptor), Is.False);

        SetLocal(descriptor);
        Assert.That(IsLocal(descriptor), Is.True);
        Assert.That(IsTls(descriptor), Is.False);
        SetTls(descriptor);
        Assert.That(IsTls(descriptor), Is.True);
        Assert.That(IsLocal(descriptor), Is.True);
        Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R19));
        Assert.That(descriptor.idReg2(), Is.EqualTo(REG_R28));
        Assert.That(ReadConstant(null, descriptor), Is.EqualTo((nint)31));
    }

#if DEBUG
    [TestCase(IF_BI_1B, 63)]
    [TestCase(IF_LS_2B, 4095)]
    [TestCase(IF_LS_2C, -256)]
    public static void SanityChecksReadCompactAndExtendedImmediates(Emitter.insFormat format, int value)
    {
        var descriptor = AccessEmitter.Constant(value, !Emitter.instrDesc.fitsInSmallCns(value));
        descriptor.idInsFmt(format);
        descriptor.idOpSize(EA_8BYTE);
        descriptor.idReg1(REG_R0);
        descriptor.idReg2(REG_R1);
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var emitter = new Emitter(new CodeGen(compiler));
        emitter.emitBegCG(compiler, default);
        CheckSanity(emitter, descriptor);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitInsSanityCheck")]
    private static extern void CheckSanity(Emitter emitter, Emitter.instrDesc descriptor);
#endif

    private abstract class AccessEmitter(CodeGen codeGen) : Emitter(codeGen)
    {
        internal static instrDesc Constant(nint value, bool large)
        {
            instrDesc descriptor;
            if (large)
            {
                descriptor = new instrDescCns { idcCnsVal = value };
                descriptor.idSetIsLargeCns();
            }
            else
            {
                descriptor = new instrDescBasic();
                descriptor.idSmallCns(value);
            }
            descriptor.idIns(INS_nop);

            return descriptor;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "emitGetInsSC")]
    private static extern nint ReadConstant(Emitter? emitter, Emitter.instrDesc descriptor);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "idIsReloc")]
    private static extern bool IsReloc(Emitter.instrDesc descriptor);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "idIsLclVar")]
    private static extern bool IsLocal(Emitter.instrDesc descriptor);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "idSetIsLclVar")]
    private static extern void SetLocal(Emitter.instrDesc descriptor);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "idIsTlsGD")]
    private static extern bool IsTls(Emitter.instrDesc descriptor);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "idSetTlsGD")]
    private static extern void SetTls(Emitter.instrDesc descriptor);
}
#endif
