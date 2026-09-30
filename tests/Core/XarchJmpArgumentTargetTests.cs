// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_XARCH
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static class XarchJmpArgumentTargetTests
{
#if TARGET_X86
    [Test]
    public static void X86VarargsRequireNoRegisterPlacement()
    {
        X86EmitterStaticOutputTests.WithEmitter((compiler, emitter) =>
        {
            var codeGen = compiler.codeGen as CodeGen ??
                throw new AssertionException("Expected an x86 code generator.");
            compiler.info.compIsVarArgs = true;

            codeGen.genJmpPlaceVarArgs();

            Assert.That(DescriptorBuffer(emitter)?.Count ?? 0, Is.Zero);
        });
    }

    [TestCase(TYP_REF, 4, TYP_REF)]
    [TestCase(TYP_BYREF, 4, TYP_BYREF)]
    [TestCase(TYP_BYTE, 1, TYP_INT)]
    public static void X86RegisterParametersUseTheirNativeStackTypes(
        var_types parameterType, int segmentSize, var_types expected)
    {
        X86EmitterStaticOutputTests.WithEmitter((compiler, emitter) =>
        {
            var codeGen = compiler.codeGen as CodeGen ??
                throw new AssertionException("Expected an x86 code generator.");
            var descriptor = new LclVarDsc { Type = parameterType };
            var segment = AbiPassingSegment.InRegister(regNumber.REG_ECX, 0, segmentSize);

            Assert.That(codeGen.genParamStackType(in descriptor, in segment), Is.EqualTo(expected));
            Assert.That(DescriptorBuffer(emitter)?.Count ?? 0, Is.Zero);
        });
    }

    [Test]
    public static void X86EmptyJumpStillInvokesTheNativePlacementOrder()
    {
        X86EmitterStaticOutputTests.WithEmitter((compiler, emitter) =>
        {
            var codeGen = compiler.codeGen as CodeGen ??
                throw new AssertionException("Expected an x86 code generator.");
            compiler.compJmpOpUsed = true;
            compiler.info.compArgsCount = 0;

            codeGen.genJmpPlaceArgs(new GenTreeVal(GT_JMP, TYP_VOID, 0));

            Assert.That(DescriptorBuffer(emitter)?.Count ?? 0, Is.Zero);
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? DescriptorBuffer(Emitter emitter);
#endif

#if TARGET_AMD64
    [Test]
    public static void FloatingHomeSpillAndReloadPreserveTheIntegerRegisterBank()
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            compiler.compJmpOpUsed = true;
            compiler.compCalleeRegsPushed = 2;
            compiler.compLclFrameSize = 64;
            compiler.compHndBBtab = [];
            _ = compiler.fgCreateFunclets();
            compiler.info.compArgsCount = 1;
            compiler.lvaCount = 2;
            compiler.lvaTrackedCount = 1;
            compiler.lvaTrackedCountInSizeTUnits = 1;
            compiler.lvaTrackedToVarNum = [0];
            compiler.lvaTable =
            [
                new LclVarDsc
                {
                    Type = TYP_FLOAT,
                    RegNum = REG_XMM2,
                    lvIsParam = true,
                    lvIsRegArg = true,
                    lvTracked = true,
                    lvOnFrame = true,
                    lvFramePointerBased = true,
                    StackOffset = 16,
                    _varIndex = 0,
                },
                new LclVarDsc
                {
                    Type = TYP_STRUCT,
                    Layout = new ClassLayout(32),
                    lvOnFrame = true,
                    StackOffset = 0,
                    RegNum = REG_STK,
                },
            ];
            compiler.lvaParameterPassingInfo =
            [
                AbiPassingInformation.FromSegment(compiler, false,
                    AbiPassingSegment.InRegister(REG_XMM0, 0, 4)),
            ];
            compiler.compCurLife = VarSetOps.MakeEmpty(compiler);
            VarSetOps.AddElemD(compiler, compiler.compCurLife, 0);
            codeGen.GCInfo.gcVarPtrSetCur = VarSetOps.MakeEmpty(compiler);
            codeGen.GCInfo.gcTrkStkPtrLcls = VarSetOps.MakeEmpty(compiler);

            var integerLive = Mask(REG_RDX);
            var floatingHome = Mask(REG_XMM2);
            var floatingArgument = Mask(REG_XMM0);
            codeGen.RegSet.ClearMaskVars();
            codeGen.RegSet.AddMaskVars(integerLive | floatingHome);

            codeGen.genJmpPlaceArgs(new GenTreeVal(GT_JMP, TYP_VOID, 0));

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(2));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_movss));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_XMM2));
            Assert.That(descriptors[1].idIns(), Is.EqualTo(INS_movss));
            Assert.That(descriptors[1].idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(descriptors[1].idReg1(), Is.EqualTo(REG_XMM0));
            Assert.That(codeGen.RegSet.GetMaskVars(), Is.EqualTo(integerLive | floatingArgument));
            Assert.That((codeGen.RegSet.GetMaskVars() & floatingHome).IsEmpty, Is.True);
            Assert.That(codeGen.RegSet.GetMaskVars() & integerLive, Is.EqualTo(integerLive));
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_XMM2));
        });
    }

    private static regMaskTP Mask(regNumber reg) => regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);
#endif

#if UNIX_AMD64_ABI
    [Test]
    public static void UnixEmptyJumpDoesNotInventRegisterOrStackArguments()
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            compiler.compJmpOpUsed = true;
            compiler.info.compArgsCount = 0;

            codeGen.genJmpPlaceArgs(new GenTreeVal(GT_JMP, TYP_VOID, 0));

            Assert.That(Descriptors(codeGen), Is.Empty);
        });
    }
#endif
}
#endif
