// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class DevirtualizationContextTests
{
    [Test]
    public static void DisabledDevirtualizationPreservesExactContext()
    {
        Assume.That(TestConfiguration.IsDebug, "the test requires Debug JIT TLS state");

        var previousConfig = Globals.JitConfig;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags jitFlags = default;
        compiler.opts.jitFlags = &jitFlags;
        compiler.SetMinOpts(false);
        using var tls = TestConfiguration.CreateJitTlsWithCompiler(compiler);
        Globals.JitConfig = new JitConfigValues();

        try
        {
            var call = new GenTreeCall(TYP_VOID) {
                Flags = GTF_CALL_VIRT_STUB,
            };
            CORINFO_METHOD_STRUCT_* method = null;
            CorInfoFlag methodFlags = default;
            var contextHandle = (CORINFO_CONTEXT_STRUCT_*)0x5678;
            var exactContextHandle = (CORINFO_CONTEXT_STRUCT_*)0x1234;
            var resolvedToken = new CORINFO_RESOLVED_TOKEN();

            compiler.impDevirtualizeCall(
                call,
                resolvedToken,
                ref method,
                ref methodFlags,
                ref contextHandle,
                ref exactContextHandle,
                isLateDevirtualization: false,
                isExplicitTailCall: false);

            Assert.That((nuint)exactContextHandle, Is.EqualTo((nuint)0x1234));
        }
        finally
        {
            Globals.JitConfig = previousConfig;
        }
    }
}
