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

internal static unsafe class X86EmitterRegisterImmediateOutputTests
{
    [TestCase(INS_inc, EA_4BYTE, REG_EAX, "40")]
    [TestCase(INS_inc, EA_1BYTE, REG_EAX, "FEC0")]
    [TestCase(INS_push, EA_4BYTE, REG_ECX, "51")]
    public static void RegisterOutputRetainsCompactAndByteForms(
        instruction ins, emitAttr size, regNumber reg, string expected)
    {
        X86EmitterStaticOutputTests.WithEmitter((_, emitter) =>
        {
            var id = NewDescriptor(ins, size, ins == INS_inc ? IF_RRW : IF_RRD, reg);
            var buffer = stackalloc byte[32];
#if DEBUG
            emitter.emitIssuing = true;
#endif
            var end = emitter.emitOutputR(buffer, id);
            Assert.That(new ReadOnlySpan<byte>(buffer, (int)(end - buffer)).ToArray(),
                Is.EqualTo(Convert.FromHexString(expected)));
        });
    }

    [TestCase(INS_push, EA_4BYTE, -1, "6AFF")]
    [TestCase(INS_push, EA_4BYTE, 128, "6880000000")]
    [TestCase(INS_ret, EA_4BYTE, 16, "C21000")]
    public static void ImmediateOutputRetainsSignedByteAndFullWidthForms(
        instruction ins, emitAttr size, int immediate, string expected)
    {
        X86EmitterStaticOutputTests.WithEmitter((_, emitter) =>
        {
            var id = NewDescriptor(ins, size, IF_CNS, constant: immediate);
            var buffer = stackalloc byte[32];
#if DEBUG
            emitter.emitIssuing = true;
#endif
            var end = emitter.emitOutputIV(buffer, id);
            Assert.That(new ReadOnlySpan<byte>(buffer, (int)(end - buffer)).ToArray(),
                Is.EqualTo(Convert.FromHexString(expected)));
        });
    }

    private static Emitter.instrDesc NewDescriptor(instruction ins, emitAttr size, Emitter.insFormat format,
        regNumber reg = REG_NA, nint? constant = null)
    {
        var descriptorType = typeof(Emitter).GetNestedType(
            constant.HasValue ? "instrDescCns" : "instrDescBasic", BindingFlags.NonPublic)
            ?? throw new AssertionException("Missing instruction descriptor type.");
        var id = Activator.CreateInstance(descriptorType, nonPublic: true) as Emitter.instrDesc
            ?? throw new AssertionException("Cannot create instruction descriptor.");
        var sizeEncoder = typeof(Emitter).GetMethod("emitEncodeSize", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new AssertionException("Missing instruction size encoder.");
        var sizeField = typeof(Emitter.instrDesc).GetField("_idOpSize", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new AssertionException("Missing instruction size field.");
        sizeField.SetValue(id, sizeEncoder.Invoke(null, [size]));

        id.idIns(ins);
        id.idInsFmt(format);
        Assert.That(id.idOpSize(), Is.EqualTo(size));

        if (reg != REG_NA)
        {
            var regField = typeof(Emitter.instrDesc).GetField("_idReg1", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new AssertionException("Missing first register field.");
            regField.SetValue(id, reg);
            Assert.That(id.idReg1(), Is.EqualTo(reg));
        }

        if (constant.HasValue)
        {
            id.idSetIsLargeCns();
            var constantField = descriptorType.GetField("idcCnsVal", BindingFlags.Instance | BindingFlags.Public)
                ?? throw new AssertionException("Missing large constant field.");
            constantField.SetValue(id, constant.Value);
            Assert.That(id.idIsLargeCns(), Is.True);
            Assert.That(constantField.GetValue(id), Is.EqualTo(constant.Value));
        }

        return id;
    }
}
#endif
