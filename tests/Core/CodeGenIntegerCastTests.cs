// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Linq;
using NUnit.Framework;
using static RyuJitSharp.CodeGen.GenIntCastDesc.CheckKind;
using static RyuJitSharp.CodeGen.GenIntCastDesc.ExtendKind;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenIntegerCastTests
{
    [TestCase(TYP_INT, TYP_LONG, INS_movsxd)]
    [TestCase(TYP_INT, TYP_DOUBLE, INS_cvtsi2sd32)]
    [TestCase(TYP_FLOAT, TYP_DOUBLE, INS_cvtss2sd)]
    [TestCase(TYP_DOUBLE, TYP_FLOAT, INS_cvtsd2ss)]
    public static void CastDispatchUsesTheManagedUnaryCastRepresentation(
        var_types sourceType, var_types castType, instruction expected)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var sourceFloating = varTypeIsFloating(sourceType);
            GenTree source = sourceFloating
                ? compiler.gtNewDconNode(sourceType, 1.0)
                : Register(compiler, sourceType, REG_RAX);
            source.RegNum = sourceFloating ? REG_XMM0 : REG_RAX;
            var cast = CreateCast(source, false, castType, false);
            cast.RegNum = varTypeIsFloating(castType) ? REG_XMM1 : REG_RCX;

            codeGen.genCodeForCast(cast);

            Assert.That(Descriptors(codeGen)[^1].idIns(), Is.EqualTo(expected));
        });
    }

    [TestCase(TYP_INT, false, TYP_LONG, CHECK_NONE, SIGN_EXTEND_INT, 4)]
    [TestCase(TYP_INT, true, TYP_LONG, CHECK_NONE, ZERO_EXTEND_INT, 4)]
    [TestCase(TYP_INT, false, TYP_ULONG, CHECK_POSITIVE, ZERO_EXTEND_INT, 4)]
    [TestCase(TYP_INT, true, TYP_ULONG, CHECK_NONE, ZERO_EXTEND_INT, 4)]
    [TestCase(TYP_LONG, false, TYP_INT, CHECK_INT_RANGE, COPY, 4)]
    [TestCase(TYP_LONG, true, TYP_INT, CHECK_POSITIVE_INT_RANGE, COPY, 4)]
    [TestCase(TYP_LONG, false, TYP_UINT, CHECK_UINT_RANGE, COPY, 4)]
    [TestCase(TYP_LONG, true, TYP_UINT, CHECK_UINT_RANGE, COPY, 4)]
    [TestCase(TYP_INT, false, TYP_INT, CHECK_NONE, COPY, 4)]
    [TestCase(TYP_INT, true, TYP_INT, CHECK_POSITIVE, COPY, 4)]
    [TestCase(TYP_INT, false, TYP_UINT, CHECK_POSITIVE, COPY, 4)]
    [TestCase(TYP_INT, true, TYP_UINT, CHECK_NONE, COPY, 4)]
    [TestCase(TYP_LONG, false, TYP_LONG, CHECK_NONE, COPY, 8)]
    [TestCase(TYP_LONG, true, TYP_LONG, CHECK_POSITIVE, COPY, 8)]
    [TestCase(TYP_LONG, false, TYP_ULONG, CHECK_POSITIVE, COPY, 8)]
    [TestCase(TYP_LONG, true, TYP_ULONG, CHECK_NONE, COPY, 8)]
    public static void CheckedDescriptorsPreserveWidthAndSignedness(var_types sourceType, bool unsigned,
        var_types castType, CodeGen.GenIntCastDesc.CheckKind check,
        CodeGen.GenIntCastDesc.ExtendKind extend, int width)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, _) =>
        {
            var cast = CreateCast(Register(compiler, sourceType, REG_RAX), unsigned, castType, true);
            var desc = new CodeGen.GenIntCastDesc(cast);

            Assert.That(desc.Check, Is.EqualTo(check));
            Assert.That(desc.Extend, Is.EqualTo(extend));
            Assert.That(desc.ExtendSrcSize, Is.EqualTo(width));
            if (check != CHECK_NONE)
            {
                Assert.That(desc.CheckSrcSize, Is.EqualTo(sourceType.Size));
            }
        });
    }

    [Test]
    public static void SmallCheckedCastsKeepExactBoundsAndUnsignedBranching(
        [Values(TYP_BYTE, TYP_UBYTE, TYP_SHORT, TYP_USHORT)] var_types castType,
        [Values(TYP_INT, TYP_LONG)] var_types sourceType, [Values(false, true)] bool unsigned)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var (min, max) = castType switch
            {
                TYP_BYTE => (-128, 127),
                TYP_UBYTE => (0, 255),
                TYP_SHORT => (-32768, 32767),
                _ => (0, 65535),
            };
            var target = CodeGenBinaryTests.PrepareThrowTarget(compiler);
            var cast = CreateCast(Register(compiler, sourceType, REG_RAX), unsigned, castType, true);
            var desc = new CodeGen.GenIntCastDesc(cast);
            var expectedMin = unsigned ? 0 : min;
            Assert.That(desc.Check, Is.EqualTo(CHECK_SMALL_INT_RANGE));
            Assert.That(desc.CheckSmallIntMin, Is.EqualTo(expectedMin));
            Assert.That(desc.CheckSmallIntMax, Is.EqualTo(max));
            Assert.That(desc.Extend, Is.EqualTo(COPY));

            codeGen.genIntToIntCast(cast);

            var instructions = Descriptors(codeGen);
            Assert.That(instructions, Has.Count.EqualTo(expectedMin == 0 ? 3 : 5));
            Assert.That(instructions[0].idIns(), Is.EqualTo(INS_cmp));
            Assert.That(instructions[0].idOpSize(), Is.EqualTo((emitAttr)sourceType.Size));
            Assert.That(InstructionConstant(codeGen.Emitter, instructions[0]), Is.EqualTo((nint)max));
            Assert.That(instructions[1].idIns(), Is.EqualTo(expectedMin == 0 ? INS_ja : INS_jg));
            Assert.That(EmitterJumpInstructionTests.JumpView.Target(instructions[1]), Is.SameAs(target));
            if (expectedMin != 0)
            {
                Assert.That(InstructionConstant(codeGen.Emitter, instructions[2]), Is.EqualTo((nint)min));
                Assert.That(instructions[3].idIns(), Is.EqualTo(INS_jl));
                Assert.That(EmitterJumpInstructionTests.JumpView.Target(instructions[3]), Is.SameAs(target));
            }
            Assert.That(instructions[^1].idIns(), Is.EqualTo(INS_mov));
        });
    }

    [Test]
    public static void UncheckedSmallCastsExtendTheTruncatedValue(
        [Values(TYP_BYTE, TYP_UBYTE, TYP_SHORT, TYP_USHORT)] var_types castType,
        [Values(TYP_INT, TYP_LONG)] var_types sourceType, [Values(false, true)] bool alias)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var (ins, size) = castType switch
            {
                TYP_BYTE => (INS_movsx, EA_1BYTE),
                TYP_UBYTE => (INS_movzx, EA_1BYTE),
                TYP_SHORT => (INS_movsx, EA_2BYTE),
                _ => (INS_movzx, EA_2BYTE),
            };
            var cast = CreateCast(Register(compiler, sourceType, REG_RAX), false, castType, false);
            cast.RegNum = alias ? REG_RAX : REG_RCX;

            codeGen.genIntToIntCast(cast);

            var descriptors = Descriptors(codeGen);
            var accumulatorExtension = alias && (castType == TYP_SHORT);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(accumulatorExtension ? INS_cwde : ins));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(accumulatorExtension ? EA_4BYTE : size));
            if (!accumulatorExtension)
            {
                Assert.That(descriptors[0].idReg1(), Is.EqualTo(cast.RegNum));
                Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_RAX));
            }
        });
    }

    [TestCase(TYP_INT, false, TYP_LONG, INS_movsxd, EA_4BYTE, false)]
    [TestCase(TYP_INT, false, TYP_LONG, INS_movsxd, EA_4BYTE, true)]
    [TestCase(TYP_INT, true, TYP_LONG, INS_mov, EA_4BYTE, false)]
    [TestCase(TYP_INT, true, TYP_LONG, INS_mov, EA_4BYTE, true)]
    [TestCase(TYP_LONG, false, TYP_INT, INS_mov, EA_4BYTE, false)]
    [TestCase(TYP_LONG, false, TYP_INT, INS_mov, EA_4BYTE, true)]
    [TestCase(TYP_LONG, false, TYP_LONG, INS_mov, EA_8BYTE, false)]
    [TestCase(TYP_LONG, false, TYP_LONG, INS_mov, EA_8BYTE, true)]
    public static void WideningAndCopiesPreserveMandatoryZeroExtension(var_types sourceType, bool unsigned,
        var_types castType, instruction ins, emitAttr size, bool alias)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var cast = CreateCast(Register(compiler, sourceType, REG_RAX), unsigned, castType, false);
            cast.RegNum = alias ? REG_RAX : REG_RCX;

            codeGen.genIntToIntCast(cast);

            var descriptors = Descriptors(codeGen);
            var elided = alias && (sourceType == TYP_LONG);
            Assert.That(descriptors, Has.Count.EqualTo(elided ? 0 : 1));
            if (!elided)
            {
                var accumulatorExtension = alias && (ins == INS_movsxd);
                Assert.That(descriptors[0].idIns(), Is.EqualTo(accumulatorExtension ? INS_cwde : ins));
                Assert.That(descriptors[0].idOpSize(), Is.EqualTo(accumulatorExtension ? EA_8BYTE : size));
            }
        });
    }

    [TestCase(TYP_BYTE, false, TYP_LONG, LOAD_SIGN_EXTEND_SMALL_INT, INS_movsx, EA_1BYTE)]
    [TestCase(TYP_UBYTE, true, TYP_LONG, LOAD_ZERO_EXTEND_SMALL_INT, INS_movzx, EA_1BYTE)]
    [TestCase(TYP_SHORT, false, TYP_LONG, LOAD_SIGN_EXTEND_SMALL_INT, INS_movsx, EA_2BYTE)]
    [TestCase(TYP_USHORT, true, TYP_LONG, LOAD_ZERO_EXTEND_SMALL_INT, INS_movzx, EA_2BYTE)]
    [TestCase(TYP_INT, false, TYP_LONG, LOAD_SIGN_EXTEND_INT, INS_movsxd, EA_4BYTE)]
    [TestCase(TYP_INT, true, TYP_LONG, LOAD_ZERO_EXTEND_INT, INS_mov, EA_4BYTE)]
    [TestCase(TYP_UBYTE, false, TYP_USHORT, LOAD_ZERO_EXTEND_SMALL_INT, INS_movzx, EA_1BYTE)]
    [TestCase(TYP_BYTE, false, TYP_SHORT, LOAD_SIGN_EXTEND_SMALL_INT, INS_movsx, EA_1BYTE)]
    [TestCase(TYP_INT, false, TYP_BYTE, LOAD_SIGN_EXTEND_SMALL_INT, INS_movsx, EA_1BYTE)]
    [TestCase(TYP_LONG, false, TYP_INT, LOAD_SOURCE, INS_mov, EA_8BYTE)]
    public static void ContainedLoadsUseTheActualStoredWidth(var_types sourceType, bool unsigned, var_types castType,
        CodeGen.GenIntCastDesc.ExtendKind extend, instruction ins, emitAttr size)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var source = compiler.gtNewLclvNode(sourceType, 0);
            source.IsContained = true;
            var cast = CreateCast(source, unsigned, castType, false);
            var desc = new CodeGen.GenIntCastDesc(cast);
            Assert.That(desc.Extend, Is.EqualTo(extend));

            codeGen.genIntToIntCast(cast);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(ins));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(size));
            Assert.That(descriptors[0].idInsFmt(), Is.EqualTo(Emitter.insFormat.IF_RWR_SRD));
        });
    }

    [TestCase(TYP_LONG, false, TYP_UINT, CHECK_UINT_RANGE)]
    [TestCase(TYP_LONG, false, TYP_INT, CHECK_INT_RANGE)]
    [TestCase(TYP_LONG, true, TYP_INT, CHECK_POSITIVE_INT_RANGE)]
    [TestCase(TYP_INT, false, TYP_ULONG, CHECK_POSITIVE)]
    [TestCase(TYP_LONG, true, TYP_LONG, CHECK_POSITIVE)]
    public static void WideOverflowChecksPrecedeConversion(var_types sourceType, bool unsigned, var_types castType,
        CodeGen.GenIntCastDesc.CheckKind check)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var target = CodeGenBinaryTests.PrepareThrowTarget(compiler);
            var cast = CreateCast(Register(compiler, sourceType, REG_RAX), unsigned, castType, true);
            codeGen.InternalRegisters.Add(cast, RBM_R11);

            codeGen.genIntToIntCast(cast);

            var descriptors = Descriptors(codeGen);
            instruction[] expected = check switch
            {
                CHECK_UINT_RANGE => [INS_mov, INS_shr_N, INS_jne, INS_mov],
                CHECK_INT_RANGE => [INS_movsxd, INS_cmp, INS_jne, INS_mov],
                CHECK_POSITIVE_INT_RANGE => [INS_cmp, INS_ja, INS_mov],
                _ => [INS_test, INS_jl, INS_mov],
            };
            Assert.That(descriptors.Select(static descriptor => descriptor.idIns()), Is.EqualTo(expected));
            Assert.That(EmitterJumpInstructionTests.JumpView.Target(descriptors[^2]), Is.SameAs(target));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo((emitAttr)sourceType.Size));
            if (check is CHECK_UINT_RANGE or CHECK_INT_RANGE)
            {
                Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_R11));
                if (check == CHECK_UINT_RANGE)
                {
                    Assert.That(InstructionConstant(codeGen.Emitter, descriptors[1]), Is.EqualTo((nint)32));
                }
                else
                {
                    Assert.That(descriptors[1].idReg1(), Is.EqualTo(REG_RAX));
                    Assert.That(descriptors[1].idReg2(), Is.EqualTo(REG_R11));
                }
            }
            else if (check == CHECK_POSITIVE_INT_RANGE)
            {
                Assert.That(InstructionConstant(codeGen.Emitter, descriptors[0]), Is.EqualTo((nint)int.MaxValue));
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void SpillLoadsUseTheAlreadyExtendedActualType(bool unsigned)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            codeGen.RegSet.tmpInit();
            codeGen.RegSet.tmpPreAllocateTemps(TYP_INT, 1);
            var temp = codeGen.RegSet.tmpGetTemp(TYP_INT);
            temp.tdTempOffs = -32;
            codeGen.RegSet.tmpRlsTemp(temp);
            var source = Register(compiler, unsigned ? TYP_BYTE : TYP_UBYTE, REG_RAX);
            source.Flags |= GTF_SPILL;
            codeGen.RegSet.rsSpillTree(REG_RAX, source);
            source.Flags |= GTF_NOREG_AT_USE;
            source.IsRegOptional = true;
            var cast = CreateCast(source, unsigned, TYP_LONG, false);
            var desc = new CodeGen.GenIntCastDesc(cast);
            Assert.That(desc.Extend, Is.EqualTo(unsigned ? LOAD_ZERO_EXTEND_INT : LOAD_SIGN_EXTEND_INT));
            Assert.That(desc.ExtendSrcSize, Is.EqualTo(4));

            codeGen.genIntToIntCast(cast);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors[^1].idIns(), Is.EqualTo(unsigned ? INS_mov : INS_movsxd));
            Assert.That(descriptors[^1].idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(codeGen.RegSet.rsGetSpillInfo(source, REG_RAX, out _), Is.Null);
            var reused = codeGen.RegSet.tmpGetTemp(TYP_INT);
            Assert.That(reused, Is.SameAs(temp));
            codeGen.RegSet.tmpRlsTemp(reused);
        });
    }

    [Test]
    public static void CheckedCastsResumeAfterTheInlineOverflowThrow()
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            var cast = CreateCast(Register(compiler, TYP_LONG, REG_RAX), false, TYP_UINT, true);
            codeGen.InternalRegisters.Add(cast, RBM_R11);
            compiler.opts.compDbgCode = true;
            var firstGroup = codeGen.Emitter.emitCurIG;

            codeGen.genIntToIntCast(cast);
            _ = CodeGenThrowHelperTests.AssertInlineThrow(firstGroup, INS_je, 4);
            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(1));
            Assert.That(Descriptors(codeGen)[0].idIns(), Is.EqualTo(INS_mov));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void CastsWithoutAnActualOverflowCheckDoNotRequireSharedThrows(bool overflow)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.opts.compDbgCode = true;
            var cast = CreateCast(Register(compiler, TYP_INT, REG_RAX), true, TYP_LONG, overflow);

            codeGen.genIntToIntCast(cast);

            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(1));
            Assert.That(Descriptors(codeGen)[0].idIns(), Is.EqualTo(INS_mov));
        });
    }

#if DEBUG
    [Test]
    public static void DisassemblyRejectsBeforeConsumingTheCastSource()
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var cast = CreateCast(Register(compiler, TYP_INT, REG_RAX), false, TYP_LONG, false);
            compiler.opts.dspCode = true;
            _ = Assert.Throws<FatalJitException>(() => codeGen.genIntToIntCast(cast));
            Assert.That(Descriptors(codeGen), Is.Empty);
            compiler.opts.dspCode = false;

            codeGen.genIntToIntCast(cast);
            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(1));
        });
    }
#endif

    private static GenTreeCast CreateCast(GenTree source, bool unsigned, var_types type, bool overflow)
    {
        var cast = new GenTreeCast(type.ActualType, source, unsigned, type) { RegNum = REG_RCX };
        if (overflow)
        {
            cast.Flags |= GTF_OVERFLOW;
        }

        return cast;
    }
}
