// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_X86
using System;
using System.Reflection;
using NUnit.Framework;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static unsafe class X86EmitterInstructionDispatchTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void ThreeRegisterOutputWritesBytesBeforeTheRetainedGcDependency(bool dispatch)
    {
        X86EmitterStaticOutputTests.WithEmitter((_, emitter) =>
        {
            emitter.UseVexEncodings = true;
            var id = NewDescriptor(INS_addps, IF_RWR_RRD_RRD);
            SetRegister(id, "_idReg1", REG_XMM1);
            SetRegister(id, "_idReg2", REG_XMM2);
            id.idReg3(REG_XMM3);
            Assert.That((id.idReg1(), id.idReg2(), id.idReg3()),
                Is.EqualTo((REG_XMM1, REG_XMM2, REG_XMM3)));

            var buffer = stackalloc byte[32];
            var end = buffer;
            var endPtr = &end;
#if DEBUG
            emitter.emitIssuing = true;
#endif
            var exception = Assert.Throws<FatalJitException>(() =>
            {
                if (dispatch)
                {
                    var group = emitter.emitCurIG ?? throw new AssertionException("Missing instruction group.");
                    emitter.emitOutputInstr(group, id, endPtr);
                }
                else
                {
                    emitter.emitOutputRRR(buffer, id);
                }
            });

            Assert.That(exception!.Message, Does.Contain("x86 SIMD register-write classification"));
            Assert.That(new ReadOnlySpan<byte>(buffer, 4).ToArray(), Is.EqualTo(Convert.FromHexString("C5E858CB")));
            Assert.That((nint)end, Is.EqualTo((nint)buffer));
        });
    }

    private static Emitter.instrDesc NewDescriptor(instruction ins, Emitter.insFormat format)
    {
        var type = typeof(Emitter).GetNestedType("instrDescBasic", BindingFlags.NonPublic)
            ?? throw new AssertionException("Missing instruction descriptor type.");
        var id = Activator.CreateInstance(type, nonPublic: true) as Emitter.instrDesc
            ?? throw new AssertionException("Cannot create instruction descriptor.");
        var sizeEncoder = typeof(Emitter).GetMethod("emitEncodeSize", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new AssertionException("Missing instruction size encoder.");
        var sizeField = typeof(Emitter.instrDesc).GetField("_idOpSize", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new AssertionException("Missing instruction size field.");
        sizeField.SetValue(id, sizeEncoder.Invoke(null, [EA_16BYTE]));
        id.idIns(ins);
        id.idInsFmt(format);

        return id;
    }

    private static void SetRegister(Emitter.instrDesc id, string name, regNumber reg)
    {
        var field = typeof(Emitter.instrDesc).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new AssertionException($"Missing instruction {name} field.");
        field.SetValue(id, reg);
    }
}
#endif
