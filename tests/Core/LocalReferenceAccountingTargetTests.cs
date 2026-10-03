// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.RefCountState;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LocalReferenceAccountingTargetTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void ReferenceCountPathsUseSharedNativeAlgorithmOnTarget(bool minopts)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(minopts);
        compiler.info.compInitMem = true;
        compiler.lvaRefCountState = RCS_NORMAL;
        compiler.lvaCount = 1;
        compiler.lvaCurEpoch = 10;
        compiler.lvaTrackedCount = 3;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        compiler.lvaGenericsContextInUse = true;
        compiler.lvaTable =
        [
            new LclVarDsc
            {
                Type = TYP_INT,
                lvTracked = true,
                lvSlotNum = 99,
                lvSingleDef = true,
                lvSingleDefRegCandidate = true,
            },
        ];
        JitTls.Compiler = compiler;

        try
        {
            compiler.lvaComputeRefCounts(isRecompute: false, setSlotNumbers: true);

            ref var local = ref compiler.lvaTable[0];
            Assert.That(local.lvTracked, Is.EqualTo(!minopts));
            Assert.That(local.lvSlotNum, Is.Zero);

            if (minopts)
            {
                Assert.That(local.lvImplicitlyReferenced, Is.True);
                Assert.That(local.lvRefCnt(), Is.EqualTo(1));
                Assert.That(compiler.lvaCurEpoch, Is.EqualTo(11));
                Assert.That(compiler.lvaTrackedCount, Is.Zero);
                Assert.That(compiler.lvaTrackedCountInSizeTUnits, Is.Zero);
                Assert.That(compiler.lvaGenericsContextInUse, Is.True);
            }
            else
            {
                Assert.That(local.lvRefCnt(), Is.Zero);
                Assert.That(local.lvSingleDef, Is.False);
                Assert.That(local.lvSingleDefRegCandidate, Is.False);
                Assert.That(compiler.lvaCurEpoch, Is.EqualTo(10));
                Assert.That(compiler.lvaTrackedCount, Is.EqualTo(3));
                Assert.That(compiler.lvaTrackedCountInSizeTUnits, Is.EqualTo(1));
                Assert.That(compiler.lvaGenericsContextInUse, Is.False);
            }
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
