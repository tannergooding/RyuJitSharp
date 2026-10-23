using System;
using System.Reflection;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class SMOpcodeDebugDataTests
{
    [Test]
    public static void OpcodeNamesAreStoredInCompilerDebugData()
    {
        Assert.That(SM_OPCODE.SM_LDARG_0.Name, Is.EqualTo("ldarg.0"));
        Assert.That(
            typeof(SM_OPCODEExtensions).GetField(
                "s_names",
                BindingFlags.Static | BindingFlags.NonPublic),
            Is.Null);

        var namesData = typeof(Compiler.DebugData).GetNestedType(
            "SMOpcodeNamesData",
            BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing SM opcode name sidecar.");
        Assert.That(
            namesData.GetField("Names", BindingFlags.Static | BindingFlags.NonPublic),
            Is.Not.Null);
    }
}
