// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if !TARGET_X86 && !TARGET_WASM
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
#if DEBUG
        using var tls = new JitTls(null);
        JitTls.Compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
#endif
        var codeGen = (CodeGen)RuntimeHelpers.GetUninitializedObject(typeof(CodeGen));
        var call = hasCall ? new GenTreeCall(TYP_VOID) : null;
#if TARGET_AMD64
        var expected = tailCall ? RBM_R10 : RBM_R9;
#elif TARGET_ARM
        var expected = new regMaskTP(SRBM_R12 | SRBM_LR);
#elif TARGET_ARM64
        var expected = new regMaskTP(SRBM_IP0 | SRBM_IP1);
#else
        var expected = new regMaskTP(SRBM_T0 | SRBM_T1);
#endif
        Assert.That(codeGen.genGetGSCookieTempRegs(tailCall, call), Is.EqualTo(expected));
    }
}
#endif
