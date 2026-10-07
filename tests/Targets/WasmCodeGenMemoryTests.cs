// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See License.md in the repository root for more information.

#if TARGET_WASM
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.Target.UnitTests;

internal static unsafe class WasmCodeGenMemoryTests
{
    private static int s_wellKnownGlobalsQueries;

    [Test]
    public static void WellKnownGlobalsAreCachedPerCompilerAndReturnedByReference()
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getWasmWellKnownGlobals = &GetWasmWellKnownGlobals;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
        s_wellKnownGlobalsQueries = 0;

        var compiler = NewCompiler(&jitInfo);
        ref var first = ref compiler.eeGetWasmWellKnownGlobals();
        Assert.That(s_wellKnownGlobalsQueries, Is.EqualTo(1));
        ref var second = ref compiler.eeGetWasmWellKnownGlobals();
        Assert.That(Unsafe.AreSame(ref first, ref second), Is.True);
        Assert.That(s_wellKnownGlobalsQueries, Is.EqualTo(1));

        var other = NewCompiler(&jitInfo);
        _ = other.eeGetWasmWellKnownGlobals();
        Assert.That(s_wellKnownGlobalsQueries, Is.EqualTo(2));
    }

    [Test]
    public static void InternalRegistersTrackWasmLocals()
    {
        var node = (GenTree)RuntimeHelpers.GetUninitializedObject(typeof(GenTreeIntCon));
        var first = regNumberExtensions.MakeWasmReg(3, TYP_INT);
        var second = regNumberExtensions.MakeWasmReg(7, TYP_SIMD16);
        var registers = new NodeInternalRegisters();

        registers.Add(node, first);
        registers.Add(node, second);

        ref var all = ref registers.GetAll(node);
        Assert.That(all.Count, Is.EqualTo(2));
        Assert.That(all.GetAt(0), Is.EqualTo(first));
        Assert.That(all.GetAt(1), Is.EqualTo(second));

        all.SetAt(1, first);
        Assert.That(registers.Extract(node), Is.EqualTo(first));
        Assert.That(registers.GetSingle(node), Is.EqualTo(first));
        Assert.That(registers.Extract(node), Is.EqualTo(first));
        Assert.That(registers.GetAll(node).IsEmpty, Is.True);
    }

    [TestCase(8)]
    [TestCase(128)]
    public static void ContainedAddressModeCarriesItsOffsetIntoTheMemarg(int offset)
    {
        Assert.That(GetMemargOffset(offset, isContained: true), Is.EqualTo((nint)offset));
    }

    [Test]
    public static void UnfoldedAddressUsesZeroMemargOffset()
    {
        Assert.That(GetMemargOffset(128, isContained: false), Is.EqualTo((nint)0));
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

    private static nint GetMemargOffset(int offset, bool isContained)
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
            var address = new GenTreeAddrMode(
                TYP_BYREF, new GenTreeIntCon(TYP_I_IMPL, 0), null, 0, offset)
            {
                IsContained = isContained,
            };
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

    private static Compiler NewCompiler(ICorJitInfo* jitInfo)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info = new Compiler.Info { compCompHnd = jitInfo };
        return compiler;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void GetWasmWellKnownGlobals(ICorJitInfo* jitInfo, CORINFO_WASM_WELLKNOWN_GLOBALS* result)
    {
        _ = jitInfo;
        *result = default;
        s_wellKnownGlobalsQueries++;
    }
}
#endif
