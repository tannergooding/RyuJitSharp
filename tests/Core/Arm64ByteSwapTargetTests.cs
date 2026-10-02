// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static unsafe class Arm64ByteSwapTargetTests
{
    [TestCase(TYP_INT, EA_4BYTE)]
    [TestCase(TYP_LONG, EA_8BYTE)]
    public static void FullByteSwapRecordsReverseAtTheValueWidth(var_types type, emitAttr expectedSize)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var operand = compiler.gtNewIconNode(type, 0);
            operand.RegNum = REG_R0;
            var tree = compiler.gtNewUnaryNode(GT_BSWAP, type, operand);
            tree.RegNum = REG_R1;

            codeGen.genCodeForBswap(tree);

            var descriptor = LastInstruction(codeGen.Emitter)
                ?? throw new AssertionException("Missing byte-reverse instruction.");
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_rev));
            Assert.That(descriptor.idOpSize(), Is.EqualTo(expectedSize));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R1));
            Assert.That(descriptor.idReg2(), Is.EqualTo(REG_R0));
            Assert.That(GroupSize(codeGen.Emitter), Is.EqualTo(4));
        });
    }

    [Test]
    public static void SixteenBitByteSwapZeroExtendsWhenTheUseNeedsNormalization()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var operand = compiler.gtNewIconNode(TYP_INT, 0);
            operand.RegNum = REG_R0;
            var tree = compiler.gtNewUnaryNode(GT_BSWAP16, TYP_INT, operand);
            tree.RegNum = REG_R1;

            codeGen.genCodeForBswap(tree);

            var descriptor = LastInstruction(codeGen.Emitter)
                ?? throw new AssertionException("Missing byte-swap normalization.");
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_uxth));
            Assert.That(descriptor.idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R1));
            Assert.That(descriptor.idReg2(), Is.EqualTo(REG_R1));
            Assert.That(GroupSize(codeGen.Emitter), Is.EqualTo(8));
        });
    }

    [Test]
    public static void SixteenBitByteSwapOmitsZeroExtensionForAnAdjacentNarrowCast()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var operand = compiler.gtNewIconNode(TYP_INT, 0);
            operand.RegNum = REG_R0;
            var tree = compiler.gtNewUnaryNode(GT_BSWAP16, TYP_INT, operand);
            tree.RegNum = REG_R1;
            var cast = compiler.gtNewCastNode(TYP_INT, tree, false, TYP_USHORT);
            tree.Next = cast;
            cast.Prev = tree;

            codeGen.genCodeForBswap(tree);

            var descriptor = LastInstruction(codeGen.Emitter)
                ?? throw new AssertionException("Missing byte-reverse instruction.");
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_rev16));
            Assert.That(descriptor.idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R1));
            Assert.That(descriptor.idReg2(), Is.EqualTo(REG_R0));
            Assert.That(GroupSize(codeGen.Emitter), Is.EqualTo(4));
        }, minopts: false);
    }

    private static void WithCodeGen(Action<Compiler, CodeGen> action, bool minopts = true)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(minopts);
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

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? LastInstruction(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGsize")]
    private static extern ref int GroupSize(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "treeLifeUpdater")]
    private static extern ref TreeLifeUpdater? LifeUpdater(CodeGen codeGen);
}
#endif
