// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm64CodeGenBinaryTargetTests
{
    [Test]
    public static void RegisterOperandsUseTheMappedArithmeticInstruction()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var left = Register(compiler, TYP_LONG, REG_R0);
            var right = Register(compiler, TYP_LONG, REG_R1);
            var tree = new GenTreeOp(GT_ADD, TYP_LONG, left, right)
            {
                RegNum = REG_R2,
            };

            codeGen.genConsumeOperands(tree);
            codeGen.genCodeForBinary(tree);

            var descriptor = Descriptors(codeGen.Emitter).Single();
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_add));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R2));
        });
    }

    [TestCase(GT_ADD, INS_madd)]
    [TestCase(GT_SUB, INS_msub)]
    public static void ContainedMultiplyUsesTheFusedArithmeticInstruction(genTreeOps oper, instruction expected)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var addend = Register(compiler, TYP_LONG, REG_R0);
            var left = Register(compiler, TYP_LONG, REG_R1);
            var right = Register(compiler, TYP_LONG, REG_R2);
            var multiply = new GenTreeOp(GT_MUL, TYP_LONG, left, right)
            {
                IsContained = true,
            };
            var tree = new GenTreeOp(oper, TYP_LONG, addend, multiply)
            {
                RegNum = REG_R3,
            };

            codeGen.genConsumeOperands(tree);
            codeGen.genCodeForBinary(tree);

            var descriptor = Descriptors(codeGen.Emitter).Single();
            Assert.That(descriptor.idIns(), Is.EqualTo(expected));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R3));
        });
    }

    [Test]
    public static void ContainedShiftUsesFlagSettingArithmeticAndTheShiftOption()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var left = Register(compiler, TYP_LONG, REG_R0);
            var shiftedValue = Register(compiler, TYP_LONG, REG_R1);
            var amount = compiler.gtNewIconNode(TYP_INT, 7);
            amount.IsContained = true;
            var shift = new GenTreeOp(GT_LSH, TYP_LONG, shiftedValue, amount)
            {
                IsContained = true,
            };
            var tree = new GenTreeOp(GT_ADD, TYP_LONG, left, shift)
            {
                RegNum = REG_R2,
                Flags = GTF_SET_FLAGS,
            };

            codeGen.genConsumeOperands(tree);
            codeGen.genCodeForBinary(tree);

            var descriptor = Descriptors(codeGen.Emitter).Single();
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_adds));
            Assert.That(descriptor.idInsOpt(), Is.EqualTo(INS_OPTS_LSL));
        });
    }

    [Test]
    public static void ContainedRotateUsesTheRotateOperandForm()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var left = Register(compiler, TYP_LONG, REG_R0);
            var rotatedValue = Register(compiler, TYP_LONG, REG_R1);
            var amount = compiler.gtNewIconNode(TYP_INT, 11);
            amount.IsContained = true;
            var rotate = new GenTreeOp(GT_ROR, TYP_LONG, rotatedValue, amount)
            {
                IsContained = true,
            };
            var tree = new GenTreeOp(GT_XOR, TYP_LONG, left, rotate)
            {
                RegNum = REG_R2,
            };

            codeGen.genConsumeOperands(tree);
            codeGen.genCodeForBinary(tree);

            var descriptor = Descriptors(codeGen.Emitter).Single();
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_eor));
            Assert.That(descriptor.idInsOpt(), Is.EqualTo(INS_OPTS_ROR));
        });
    }

    [TestCase(false, INS_OPTS_SXTB)]
    [TestCase(true, INS_OPTS_UXTB)]
    public static void ContainedCastUsesItsSignednessForTheExtendedOperand(bool isUnsigned, insOpts expected)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var left = Register(compiler, TYP_LONG, REG_R0);
            var castOperand = Register(compiler, TYP_INT, REG_R1);
            var cast = compiler.gtNewCastNode(TYP_LONG, castOperand, isUnsigned, TYP_BYTE);
            cast.IsContained = true;
            var tree = new GenTreeOp(GT_ADD, TYP_LONG, left, cast)
            {
                RegNum = REG_R2,
                Flags = GTF_SET_FLAGS,
            };

            codeGen.genConsumeOperands(tree);
            codeGen.genCodeForBinary(tree);

            var descriptor = Descriptors(codeGen.Emitter).Single();
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_adds));
            Assert.That(descriptor.idInsOpt(), Is.EqualTo(expected));
        });
    }

    [Test]
    public static void WindowsNativeAotSectionRelocationRecordsTheTwoTlsAdds()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.opts.compReloc = true;
            var left = Register(compiler, TYP_LONG, REG_R0);
            var offset = compiler.gtNewIconNode(TYP_LONG, 8);
            offset.Flags |= GTF_ICON_SECREL_OFFSET;
            offset.IsContained = true;
            var tree = new GenTreeOp(GT_ADD, TYP_LONG, left, offset)
            {
                RegNum = REG_R1,
            };

            codeGen.genConsumeOperands(tree);
            codeGen.genCodeForBinary(tree);

            var descriptors = Descriptors(codeGen.Emitter);
            Assert.That(descriptors.Select(descriptor => descriptor.idIns()),
                Is.EqualTo(new[] { INS_add, INS_add }));
            Assert.That(descriptors[0].idInsFmt(), Is.EqualTo(Emitter.insFormat.IF_DI_2A));
            Assert.That(descriptors[0].idInsOpt(), Is.EqualTo(INS_OPTS_LSL12));
            Assert.That(descriptors[0].idIsCnsReloc(), Is.True);
            Assert.That(descriptors[1].idIsCnsReloc(), Is.False);
            foreach (var descriptor in descriptors)
            {
                Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R1));
                Assert.That(descriptor.idReg2(), Is.EqualTo(REG_R0));
                Assert.That(descriptor.idOpSize(), Is.EqualTo(EA_8BYTE));
                Assert.That(descriptor.idIsTlsGD(), Is.True);
                Assert.That((nint)descriptor.idAddr().iiaAddr, Is.EqualTo(offset.IconValue));
            }
#if DEBUG
            foreach (var descriptor in descriptors)
            {
                var debugInfo = descriptor.idDebugOnlyInfo()
                    ?? throw new AssertionException("Missing TLS relocation debug information.");
                Assert.That(debugInfo.idMemCookie, Is.EqualTo(offset.IconValue));
                Assert.That(debugInfo.idFlags, Is.EqualTo(GTF_EMPTY));
            }
#endif
        }, CORINFO_RUNTIME_ABI.CORINFO_NATIVEAOT_ABI);
    }

    private static GenTreeIntCon Register(Compiler compiler, var_types type, regNumber reg)
    {
        var node = compiler.gtNewIconNode(type, 7);
        node.RegNum = reg;

        return node;
    }

    private static void WithCodeGen(Action<Compiler, CodeGen> action,
        CORINFO_RUNTIME_ABI targetAbi = CORINFO_RUNTIME_ABI.CORINFO_CORECLR_ABI)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.eeInfoInitialized = true;
        compiler.eeInfo.targetAbi = targetAbi;
        compiler.compCurLife = VarSetOps.MakeEmpty(compiler);
        JitTls.Compiler = compiler;
        try
        {
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            codeGen.RegSet.rsClearRegsModified();
            codeGen.Emitter.emitBegCG(compiler, default);
            codeGen.Emitter.Init();
            codeGen.Emitter.emitBegFN(false
#if DEBUG
                , true
#endif
                );
            LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);

            action(compiler, codeGen);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    private static List<Emitter.instrDesc> Descriptors(Emitter emitter)
    {
        return CurrentDescriptors(emitter) ?? throw new AssertionException("No instructions were recorded.");
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "treeLifeUpdater")]
    private static extern ref TreeLifeUpdater? LifeUpdater(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentDescriptors(Emitter emitter);
}
#endif
