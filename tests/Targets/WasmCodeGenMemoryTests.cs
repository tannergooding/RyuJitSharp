// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See License.md in the repository root for more information.

#if TARGET_WASM
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.Target.UnitTests;

internal static unsafe class WasmCodeGenMemoryTests
{
    [TestCase(8)]
    [TestCase(128)]
    public static void ContainedAddressModeCarriesItsOffsetIntoTheMemarg(int offset)
    {
        var address = new GenTreeAddrMode(
            TYP_BYREF, new GenTreeIntCon(TYP_I_IMPL, 0), null, 0, offset)
        {
            IsContained = true,
        };

        Assert.That(GetMemargOffset(address), Is.EqualTo((nint)offset));
    }

    [Test]
    public static void UnfoldedAddressUsesZeroMemargOffset()
    {
        var address = new GenTreeAddrMode(
            TYP_BYREF, new GenTreeIntCon(TYP_I_IMPL, 0), null, 0, 128);

        Assert.That(GetMemargOffset(address), Is.Zero);
    }

    [Test]
    public static void CallerStackPointerToInitialStackPointerDeltaIsUnreached()
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previousCompiler = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitTls.Compiler = compiler;

        try
        {
            var codeGen = new CodeGen(compiler);
            _ = Assert.Throws<FatalJitException>(() => _ = codeGen.genCallerSPtoInitialSPdelta);
        }
        finally
        {
            JitTls.Compiler = previousCompiler;
        }
    }

    private static nint GetMemargOffset(GenTree address)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previousCompiler = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitTls.Compiler = compiler;

        try
        {
            var codeGen = new CodeGen(compiler);
            var method = typeof(CodeGen).GetMethod(
                "genWasmMemargOffset", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new AssertionException("Missing Wasm memarg offset implementation.");

            return (nint)(method.Invoke(codeGen, [address])
                ?? throw new AssertionException("Wasm memarg offset returned no value."));
        }
        finally
        {
            JitTls.Compiler = previousCompiler;
        }
    }
}
#endif
