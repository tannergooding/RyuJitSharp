// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using NUnit.Framework;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class AbiShadowSpaceTests
{
    // Use Windows AMD64 register numbers so this fixture compiles with every target's regNumber enum.
    private const regNumber WindowsAmd64Rcx = (regNumber)1;
    private const regNumber WindowsAmd64Rdx = (regNumber)2;
    private const regNumber WindowsAmd64R8 = (regNumber)8;
    private const regNumber WindowsAmd64R9 = (regNumber)9;
    private const regNumber WindowsAmd64Xmm0 = (regNumber)32;
    private const regNumber WindowsAmd64Xmm1 = (regNumber)33;
    private const regNumber WindowsAmd64Xmm2 = (regNumber)34;
    private const regNumber WindowsAmd64Xmm3 = (regNumber)35;
    private const regNumber WindowsAmd64Rax = (regNumber)0;
    private const regNumber WindowsAmd64R10 = (regNumber)10;
    private const regNumber WindowsAmd64Rsp = (regNumber)4;
    private const regNumber WindowsAmd64Xmm4 = (regNumber)36;

    [TestCase(WindowsAmd64Rcx, 0)]
    [TestCase(WindowsAmd64Xmm0, 0)]
    [TestCase(WindowsAmd64Rdx, 8)]
    [TestCase(WindowsAmd64Xmm1, 8)]
    [TestCase(WindowsAmd64R8, 16)]
    [TestCase(WindowsAmd64Xmm2, 16)]
    [TestCase(WindowsAmd64R9, 24)]
    [TestCase(WindowsAmd64Xmm3, 24)]
    public static void RegisterPairsShareTheirCallerHome(regNumber reg, int expectedOffset)
    {
        AssumeWindowsAmd64Abi();

        Assert.That(AbiPassingInformation.GetShadowSpaceCallerOffsetForReg(reg, out var offset), Is.True);
        Assert.That(offset, Is.EqualTo(expectedOffset));
    }

    [TestCase(WindowsAmd64Rax)]
    [TestCase(WindowsAmd64R10)]
    [TestCase(WindowsAmd64Rsp)]
    [TestCase(WindowsAmd64Xmm4)]
    [TestCase(REG_NA)]
    public static void RegistersWithoutCallerHomesAreRejected(regNumber reg)
    {
        AssumeWindowsAmd64Abi();

        Assert.That(AbiPassingInformation.GetShadowSpaceCallerOffsetForReg(reg, out _), Is.False);
    }

    [Test]
    public static void DisabledWindowsAmd64AbiRejectsCallerShadowSpaceOffsets()
    {
        const string switchName = "RyuJitSharp.WINDOWS_AMD64_ABI";
        var hadOriginalValue = AppContext.TryGetSwitch(switchName, out var originalValue);
        AppContext.SetSwitch(switchName, false);

        try
        {
            var exception = Assert.Throws<FatalJitException>(() =>
                AbiPassingInformation.GetShadowSpaceCallerOffsetForReg(WindowsAmd64Rcx, out _));
            Assert.That(exception?.Message, Is.EqualTo("Caller shadow-space offsets require Windows AMD64."));
            Assert.Throws<FatalJitException>(CreateWinX64Classifier);
        }
        finally
        {
            AppContext.SetSwitch(switchName, hadOriginalValue && originalValue);
        }
    }

    private static void AssumeWindowsAmd64Abi()
    {
        Assume.That(Configuration.TARGET_AMD64 && Configuration.WINDOWS_AMD64_ABI);
    }

    private static void CreateWinX64Classifier()
    {
        var info = new ClassifierInfo();
        _ = new WinX64Classifier(info);
    }
}
