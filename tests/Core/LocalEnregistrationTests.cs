// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.RefCountState;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LocalEnregistrationTests
{
    [TestCase(false, true, 2, false)]
    [TestCase(true, false, 2, false)]
    [TestCase(true, true, 0, false)]
    [TestCase(true, true, 1, false)]
    [TestCase(true, true, 2, true)]
    public static void EhEligibilityRequiresEnabledSingleDefWithMoreThanOneReference(bool enabled, bool singleDef,
        int references, bool expected)
    {
        WithCompiler(false, compiler => {
            compiler.lvaEnregEHVars = enabled;
            ref var local = ref compiler.lvaTable[0];
            local.lvSingleDefRegCandidate = singleDef;
            local.setLvRefCnt((ushort)references);

            Assert.That(compiler.IsEHVarARegCandidate(in local), Is.EqualTo(expected));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void BulkMarkingPreservesExistingReasonsAndNativeEhBeforeStructOrdering(bool ehEnregistration)
    {
        WithCompiler(false, compiler => {
            compiler.lvaEnregEHVars = ehEnregistration;
            for (var index = 0; index < 3; index++)
            {
                compiler.lvaTable[index].lvTracked = true;
                SetLiveInOutOfHandler(ref compiler.lvaTable[index], true);
                compiler.lvaTable[index].setLvRefCnt(2);
            }
            compiler.lvaTable[0].lvSingleDefRegCandidate = true;

            for (var index = 2; index <= 6; index++)
            {
                compiler.lvaTable[index].Type = TYP_STRUCT;
                var size = index == 5 ? TARGET_POINTER_SIZE : 3 * TARGET_POINTER_SIZE;
                var builder = new ClassLayoutBuilder(compiler, size);
                compiler.lvaTable[index].Layout = ClassLayout.Create(compiler, builder);
            }
            compiler.lvaTable[4].lvPromoted = true;
            compiler.lvaTable[4].lvFieldLclStart = 7;
            compiler.lvaTable[4].lvFieldCnt = 1;
            compiler.lvaTable[7].lvIsStructField = true;
            compiler.lvaTable[7].lvParentLcl = 4;
            compiler.lvaSetVarAddrExposed(6, AddressExposedReason.EXTERNALLY_VISIBLE_IMPLICITLY);

            compiler.lvSetVarsDoNotEnreg();

            for (var index = 0; index < compiler.lvaCount; index++)
            {
                var expected = index is 1 or 2 or 3 or 6 || ((index == 0) && !ehEnregistration);
                Assert.That(compiler.lvaTable[index].lvDoNotEnregister, Is.EqualTo(expected), $"V{index}");
            }
#if DEBUG
            Assert.That(compiler.lvaTable[1].DoNotEnregisterReason, Is.EqualTo(DoNotEnregisterReason.LiveInOutOfHandler));
            Assert.That(compiler.lvaTable[2].DoNotEnregisterReason, Is.EqualTo(DoNotEnregisterReason.LiveInOutOfHandler));
            Assert.That(compiler.lvaTable[3].DoNotEnregisterReason, Is.EqualTo(DoNotEnregisterReason.NotRegSizeStruct));
            Assert.That(compiler.lvaTable[6].DoNotEnregisterReason, Is.EqualTo(DoNotEnregisterReason.AddrExposed));
#endif
        });
    }

    [Test]
    public static void MinoptsDnerInitializationRemainsSeparateAndPreservesExistingReasons()
    {
        WithCompiler(true, compiler => {
            compiler.lvaSetVarAddrExposed(0, AddressExposedReason.EXTERNALLY_VISIBLE_IMPLICITLY);
            compiler.lvSetMinOptsDoNotEnreg();

            compiler.lvSetVarsDoNotEnreg();

            foreach (ref var local in compiler.lvaTable.AsSpan())
            {
                Assert.That(local.lvDoNotEnregister, Is.True);
            }
#if DEBUG
            Assert.That(compiler.lvaTable[0].DoNotEnregisterReason, Is.EqualTo(DoNotEnregisterReason.AddrExposed));
            Assert.That(compiler.lvaTable[1].DoNotEnregisterReason, Is.EqualTo(DoNotEnregisterReason.NoRegVars));
#endif
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set__lvLiveInOutOfHandler")]
    private static extern void SetLiveInOutOfHandler(ref LclVarDsc local, bool value);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitEnregStructLocals")]
    private static extern ref int EnregStructLocals(ref JitConfigValues config);

    private static void WithCompiler(bool minopts, Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var previousConfig = JitConfig;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(minopts);
        compiler.opts.compFlags = minopts ? 0 : CLFLG_REGVAR;
        compiler.lvaRefCountState = RCS_NORMAL;
        compiler.lvaTable = new LclVarDsc[8];
        compiler.lvaCount = compiler.lvaTable.Length;
        foreach (ref var local in compiler.lvaTable.AsSpan())
        {
            local.Type = TYP_INT;
        }
        JitTls.Compiler = compiler;
        EnregStructLocals(ref JitConfig) = 1;
        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
            JitConfig = previousConfig;
        }
    }
}
