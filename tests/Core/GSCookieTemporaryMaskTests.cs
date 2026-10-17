// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regMask;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class GSCookieTemporaryMaskTests
{
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static unsafe void CookieMaskRetainsNativeRegistersWithoutCompilerState(bool tailCall, bool hasCall)
    {
        Assume.That(!Configuration.TARGET_X86 && !Configuration.TARGET_WASM,
            "requires a target with supported stack-cookie registers");

        using var tls = TestConfiguration.CreateJitTlsWithCompiler((Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler)));
        var codeGen = (CodeGen)RuntimeHelpers.GetUninitializedObject(typeof(CodeGen));
        var call = hasCall ? new GenTreeCall(TYP_VOID) : null;
        var expected = Configuration.TARGET_AMD64
            ? tailCall ? RBM_R10 : RBM_R9
            : Configuration.TARGET_ARM
                ? new regMaskTP(SRBM_R12 | SRBM_LR)
                : Configuration.TARGET_ARM64
                    ? new regMaskTP(SRBM_ARM64_IP0 | SRBM_ARM64_IP1)
                    : new regMaskTP(SRBM_T0 | SRBM_T1);

        Assert.That(codeGen.genGetGSCookieTempRegs(tailCall, call), Is.EqualTo(expected));
    }
}
