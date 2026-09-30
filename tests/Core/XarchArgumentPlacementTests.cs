// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_X86 || UNIX_AMD64_ABI
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;

namespace RyuJitSharp.UnitTests;

internal static class XarchArgumentPlacementTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void EmptyLateArgumentsDoNotRequireInstructionRecording(bool varargs)
    {
        void Check(CodeGen codeGen)
        {
            var call = new GenTreeCall(var_types.TYP_VOID);
            call.Args.IsVarArgs = varargs;
            codeGen.genCallPlaceRegArgs(call);
#if TARGET_X86
            Assert.That(DescriptorBuffer(codeGen.Emitter)?.Count ?? 0, Is.Zero);
#else
            Assert.That(Descriptors(codeGen), Is.Empty);
#endif
        }

#if TARGET_X86
        X86EmitterStaticOutputTests.WithEmitter((compiler, emitter) =>
        {
            var codeGen = compiler.codeGen as CodeGen ??
                throw new AssertionException("Expected an x86 code generator.");
            Check(codeGen);
        });
#else
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) => Check(codeGen));
#endif
    }

#if TARGET_X86
    [Test]
    public static void ArgumentMasksExcludeFrameAndStackRegisters()
    {
        X86EmitterStaticOutputTests.WithEmitter((compiler, emitter) =>
        {
            var codeGen = compiler.codeGen as CodeGen ??
                throw new AssertionException("Expected an x86 code generator.");
            Assert.That(codeGen.SRBM_ALLINT,
                Is.EqualTo(regMask.SRBM_EBX | regMask.SRBM_ESI | regMask.SRBM_EDI |
                    regMask.SRBM_EAX | regMask.SRBM_ECX | regMask.SRBM_EDX));
            Assert.That(codeGen.SRBM_ALLFLOAT,
                Is.EqualTo(regMask.SRBM_XMM0 | regMask.SRBM_XMM1 | regMask.SRBM_XMM2 |
                    regMask.SRBM_XMM3 | regMask.SRBM_XMM4 | regMask.SRBM_XMM5 |
                    regMask.SRBM_XMM6 | regMask.SRBM_XMM7));
        });
    }

    [Test]
    public static void FieldListPushesLeaveStackAdjustmentToTheIndividualPushes()
    {
        X86EmitterStaticOutputTests.WithEmitter((compiler, emitter) =>
        {
            var codeGen = compiler.codeGen as CodeGen ??
                throw new AssertionException("Expected an x86 code generator.");
            var fields = new GenTreeFieldList { IsContained = true };
            var argument = new GenTreePutArgStk(var_types.TYP_VOID, fields, null, 0, 8, false)
            {
                _kind = GenTreePutArgStk.Kind.Push,
            };
            var before = StackLevel(codeGen);

            Assert.That(AdjustStack(codeGen, argument), Is.False);
            Assert.That(PushStackArgument(codeGen), Is.True);
            Assert.That(StackLevel(codeGen), Is.EqualTo(before));
            Assert.That(DescriptorBuffer(emitter)?.Count ?? 0, Is.Zero);
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "genAdjustStackForPutArgStk")]
    private static extern bool AdjustStack(CodeGen codeGen, GenTreePutArgStk argument);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_pushStkArg")]
    private static extern ref bool PushStackArgument(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "genStackLevel")]
    private static extern ref uint StackLevel(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? DescriptorBuffer(Emitter emitter);
#endif

#if UNIX_AMD64_ABI
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public static void FirstIncomingStackArgumentUsesParameterAbiSegments(int firstStackArgument)
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
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
                var segment = index < firstStackArgument
                    ? AbiPassingSegment.InRegister(regNumber.REG_RDI, 0, 8)
                    : AbiPassingSegment.OnStack((index - firstStackArgument) * 8, 0, 8);
                compiler.lvaParameterPassingInfo[index] =
                    AbiPassingInformation.FromSegment(compiler, false, segment);
            }

            Assert.That(codeGen.getFirstArgWithStackSlot(), Is.EqualTo(firstStackArgument));
            Assert.That(Descriptors(codeGen), Is.Empty);
        });
    }
#endif
}
#endif
