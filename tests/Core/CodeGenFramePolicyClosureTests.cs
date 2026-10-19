// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Linq;
using NUnit.Framework;
using System.Runtime.CompilerServices;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class CodeGenFramePolicyClosureTests
{
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public static void FirstStackArgumentUsesTheNativeParameterAbiScan(int firstStack)
    {
        Assume.That(!Configuration.TARGET_WASM &&
            (Configuration.TARGET_ARMARCH || Configuration.TARGET_LOONGARCH64 || Configuration.TARGET_RISCV64));

        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info.compArgsCount = 3;
        compiler.lvaCount = 3;
        compiler.lvaTable =
        [
            new() { lvIsParam = true },
            new() { lvIsParam = true },
            new() { lvIsParam = true },
        ];
        compiler.lvaParameterPassingInfo = new AbiPassingInformation[3];
        for (var index = 0; index < compiler.info.compArgsCount; index++)
        {
            var segment = index < firstStack
                ? AbiPassingSegment.InRegister(REG_ARG_0, 0, TARGET_POINTER_SIZE)
                : AbiPassingSegment.OnStack((index - firstStack) * TARGET_POINTER_SIZE, 0, TARGET_POINTER_SIZE);
            compiler.lvaParameterPassingInfo[index] =
                AbiPassingInformation.FromSegment(compiler, false, segment);
        }

        var codeGen = new CodeGen(compiler);
        Assert.That(codeGen.getFirstArgWithStackSlot(), Is.EqualTo(firstStack));
    }

    [TestCase(true, false, true)]
    [TestCase(false, true, true)]
    [TestCase(false, false, false)]
    public static void SkippedPoisonLocalsDoNotInvokeUnsupportedRecording(
        bool parameter, bool initialized, bool exposed)
    {
        Assume.That(!Configuration.TARGET_WASM && !Configuration.TARGET_AMD64);

        using var tls = TestConfiguration.CreateJitTls();
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.compDbgCode = true;
        compiler.info.compLocalsCount = 1;
        compiler.lvaCount = 1;
        compiler.lvaTable = [new() { lvIsParam = parameter, lvMustInit = initialized }];
        compiler.SetLocalAddressExposed(0, exposed, AddressExposedReason.NONE);
        var codeGen = new CodeGen(compiler);

        Assert.DoesNotThrow(() => codeGen.genPoisonFrame(RBM_NONE));
        Assert.That(codeGen.Emitter.emitCurIG, Is.Null);
    }

    [Test]
    public static void PoisonImmediateSurvivesSmallRepStosdAndSmallLocals()
    {
        Assume.That(Configuration.TARGET_AMD64);

        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.opts.compDbgCode = true;
            compiler.info.compInitMem = false;
            compiler.info.compLocalsCount = 3;
            compiler.lvaCount = 3;
            var template = compiler.lvaTable[0];
            compiler.lvaTable = [template, template, template];
            int[] sizes = [8, 136, 12];
            int[] offsets = [-16, -160, -32];
            for (var index = 0; index < sizes.Length; index++)
            {
                ref var local = ref compiler.lvaTable[index];
                local.Type = TYP_STRUCT;
                local.Layout = new ClassLayout((uint)sizes[index]);
                local.StackOffset = offsets[index];
                compiler.SetLocalAddressExposed(in local, true, AddressExposedReason.NONE);
            }
            var firstGroup = codeGen.Emitter.emitCurIG;

            codeGen.genPoisonFrame(RBM_NONE);

            var descriptors = CodeGenLocalHeapTests.AllDescriptors(firstGroup, codeGen);
            Assert.That(descriptors.Count(descriptor => descriptor.idIns() == INS_r_stosd), Is.EqualTo(1));
            Assert.That(descriptors.Count(descriptor => descriptor.idIns() == INS_mov
                && descriptor.idInsFmt() == Emitter.insFormat.IF_XARCH_RWR_CNS
                && descriptor.idReg1() == REG_EAX && descriptor.idOpSize() == EA_PTRSIZE), Is.EqualTo(1));
            Assert.That(InstructionConstant(codeGen.Emitter, descriptors[0]),
                Is.EqualTo(unchecked((nint)0xCDCDCDCDCDCDCDCDUL)));
            var stores = descriptors.Where(descriptor => descriptor.idInsFmt() == Emitter.insFormat.IF_XARCH_SWR_RRD).ToArray();
            Assert.That(stores, Has.Length.EqualTo(3));
            Assert.That(stores.All(descriptor => descriptor.idReg1() == REG_SCRATCH), Is.True);
            Assert.That(descriptors.Count, Is.EqualTo(7));
        });
    }
}
