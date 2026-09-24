// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

internal static unsafe class RegisterEligibilityTests
{
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void StructEligibilityRespectsConfigurationAndGcLayout(bool enabled, bool containsGc)
    {
        var previousConfig = JitConfig;
        try
        {
            EnregStructLocals(ref JitConfig) = enabled ? 1 : 0;
            WithCompiler(compiler => {
                var builder = new ClassLayoutBuilder(compiler, TARGET_POINTER_SIZE);
                if (containsGc)
                {
                    builder.SetGCPtrType(0, var_types.TYP_REF);
                }
                ref var local = ref compiler.lvaTable[0];
                local.Type = var_types.TYP_STRUCT;
                local.Layout = ClassLayout.Create(compiler, builder);
                local.setLvRefCnt(1);

                var allocator = new LinearScan(compiler);
                Assert.That(allocator.IsRegCandidate(in local), Is.EqualTo(enabled && !containsGc));
            });
        }
        finally
        {
            JitConfig = previousConfig;
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ZeroReferenceEligibilityUpdatesTheOwningDescriptor(bool implicitlyReferenced)
    {
        WithCompiler(compiler => {
            ref var local = ref compiler.lvaTable[0];
            local.Type = var_types.TYP_INT;
            local.lvImplicitlyReferenced = implicitlyReferenced;
            local.setLvRefCnt(0);
            local.setLvRefCntWtd(11);

            var allocator = new LinearScan(compiler);
            Assert.That(allocator.IsRegCandidate(in local), Is.EqualTo(implicitlyReferenced));
            Assert.That(local.lvRefCntWtd(), Is.EqualTo(implicitlyReferenced ? 11.0 : 0.0));
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitEnregStructLocals")]
    private static extern ref int EnregStructLocals(ref JitConfigValues config);

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.opts.compFlags = CLFLG_REGVAR;
        compiler.lvaTable = new LclVarDsc[1];
        compiler.lvaCount = 1;
        compiler.lvaTable[0].lvTracked = true;
        compiler.lvaRefCountState = RefCountState.RCS_NORMAL;
        JitTls.Compiler = compiler;
        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
