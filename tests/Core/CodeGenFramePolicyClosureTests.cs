// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if !TARGET_WASM
using NUnit.Framework;
#if !TARGET_AMD64
using System.Runtime.CompilerServices;
#endif
using static RyuJitSharp.Globals;
#if TARGET_AMD64 || TARGET_ARMARCH || TARGET_LOONGARCH64 || TARGET_RISCV64
using static RyuJitSharp.regNumber;
#endif
#if TARGET_AMD64
using System.Linq;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;
using static RyuJitSharp.var_types;
#endif

namespace RyuJitSharp.UnitTests;

internal static unsafe class CodeGenFramePolicyClosureTests
{
#if TARGET_ARMARCH || TARGET_LOONGARCH64 || TARGET_RISCV64
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public static void FirstStackArgumentUsesTheNativeParameterAbiScan(int firstStack)
    {
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
#endif

#if !TARGET_AMD64
    [TestCase(true, false, true)]
    [TestCase(false, true, true)]
    [TestCase(false, false, false)]
    public static void SkippedPoisonLocalsDoNotInvokeUnsupportedRecording(
        bool parameter, bool initialized, bool exposed)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.compDbgCode = true;
        compiler.info.compLocalsCount = 1;
        compiler.lvaCount = 1;
        compiler.lvaTable = [new() { lvIsParam = parameter, lvMustInit = initialized }];
        compiler.lvaTable[0].SetAddressExposed(exposed, AddressExposedReason.NONE);
        var codeGen = new CodeGen(compiler);

        Assert.DoesNotThrow(() => codeGen.genPoisonFrame(RBM_NONE));
        Assert.That(codeGen.Emitter.emitCurIG, Is.Null);
    }
#endif

#if TARGET_AMD64
    [Test]
    public static void PoisonImmediateSurvivesSmallRepStosdAndSmallLocals()
    {
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
                local.SetAddressExposed(true, AddressExposedReason.NONE);
            }
            var firstGroup = codeGen.Emitter.emitCurIG;

            codeGen.genPoisonFrame(RBM_NONE);

            var descriptors = CodeGenLocalHeapTests.AllDescriptors(firstGroup, codeGen);
            Assert.That(descriptors.Count(descriptor => descriptor.idIns() == INS_r_stosd), Is.EqualTo(1));
            Assert.That(descriptors.Count(descriptor => descriptor.idIns() == INS_mov
                && descriptor.idInsFmt() == Emitter.insFormat.IF_RWR_CNS
                && descriptor.idReg1() == REG_EAX && descriptor.idOpSize() == EA_PTRSIZE), Is.EqualTo(1));
            Assert.That(InstructionConstant(codeGen.Emitter, descriptors[0]),
                Is.EqualTo(unchecked((nint)0xCDCDCDCDCDCDCDCDUL)));
            var stores = descriptors.Where(descriptor => descriptor.idInsFmt() == Emitter.insFormat.IF_SWR_RRD).ToArray();
            Assert.That(stores, Has.Length.EqualTo(3));
            Assert.That(stores.All(descriptor => descriptor.idReg1() == REG_SCRATCH), Is.True);
            Assert.That(descriptors.Count, Is.EqualTo(7));
        });
    }
#endif
}
#endif
