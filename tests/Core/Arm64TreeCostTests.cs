// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm64TreeCostTests
{
    [TestCase(TYP_INT, 0x00000fffL, 1, 2)]
    [TestCase(TYP_INT, 0x00001234L, 1, 4)]
    [TestCase(TYP_INT, 0x12345678L, 4, 16)]
    [TestCase(TYP_LONG, 0x123456789abcdef0L, 4, 16)]
    [TestCase(TYP_LONG, 0x1234567800000000L, 2, 8)]
    [TestCase(TYP_LONG, unchecked((long)0xffffffff12345678UL), 2, 8)]
    public static void IntegerConstantCostsUseNativeArm64InstructionEstimate(
        var_types type, long value, int executionCost, int sizeCost)
    {
        WithCompiler(compiler => {
            var constant = new GenTreeIntCon(type, (nint)value);

            Assert.That(compiler.gtSetEvalOrder(constant), Is.Zero);
            Assert.That(((int)constant.CostEx, (int)constant.CostSz), Is.EqualTo((executionCost, sizeCost)));
        });
    }

    [Test]
    public static void RelocatableHandleUsesTwoInstructions()
    {
        WithCompiler(compiler => {
            compiler.opts.compReloc = true;
            var constant = new GenTreeIntCon(TYP_I_IMPL, 0x1234);
            constant.Flags |= GTF_ICON_CLASS_HDL;

            Assert.That(compiler.gtSetEvalOrder(constant), Is.Zero);
            Assert.That((constant.CostEx, constant.CostSz), Is.EqualTo((2, 8)));
        });
    }

    private static void WithCompiler(System.Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
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
#endif
