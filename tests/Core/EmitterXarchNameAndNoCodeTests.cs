// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class EmitterXarchNameAndNoCodeTests
{
    [TestCase(instruction.INS_mov, emitAttr.EA_4BYTE, "mov")]
    [TestCase(instruction.INS_cdq, emitAttr.EA_2BYTE, "cwd")]
    [TestCase(instruction.INS_cdq, emitAttr.EA_4BYTE, "cdq")]
    [TestCase(instruction.INS_cwde, emitAttr.EA_2BYTE, "cbw")]
    [TestCase(instruction.INS_cwde, emitAttr.EA_4BYTE, "cwde")]
    public static void InstructionDisplayNamesRetainCommonXarchPseudoForms(
        instruction ins, emitAttr size, string expected)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var codeGen = new CodeGen(compiler);
        var id = DescriptorView.Basic(ins, 1);
        id.idOpSize(size);

        Assert.That(codeGen.genInsDisplayName(id), Is.EqualTo(expected));
    }

    [Test]
    public static void NoCodeClassificationDistinguishesRemovedAlignAndJump()
    {
        Assert.Multiple(() =>
        {
            Assert.That(HasNoCode(null, DescriptorView.Basic(instruction.INS_nop, 0)), Is.False);
            Assert.That(HasNoCode(null, DescriptorView.Align(0)), Is.True);
            Assert.That(HasNoCode(null, DescriptorView.Align(1)), Is.False);
            Assert.That(HasNoCode(null, DescriptorView.Jump(0, removable: true)), Is.True);
            Assert.That(HasNoCode(null, DescriptorView.Jump(1, removable: false)), Is.False);
        });
    }

#if TARGET_AMD64
    [Test]
    public static void Amd64RemovableJumpAllowsAfterCallNopReplacement()
    {
        var id = DescriptorView.Jump(1, removable: true);
        DescriptorView.MarkAfterCall(id);

        Assert.That(HasNoCode(null, id), Is.True);
    }
#endif

#if TARGET_X86
    [Test]
    public static void MarkingExistingX86StackDepthUpdatesCurrentGroupAndMaximum()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var emitter = new CodeGen(compiler).Emitter;
        emitter.emitBegCG(compiler, default);

        emitter.emitMarkStackLvl(32);

        Assert.Multiple(() =>
        {
            Assert.That(emitter.emitCurStackLvl, Is.EqualTo(32));
            Assert.That(emitter.emitCurIG?.igStkLvl, Is.EqualTo(32));
            Assert.That(emitter.emitMaxStackDepth, Is.EqualTo(32));
        });
    }
#endif

    private abstract class DescriptorView(CodeGen codeGen) : Emitter(codeGen)
    {
        public static instrDesc Basic(instruction ins, uint size)
        {
            var id = new instrDescBasic();
            id.idIns(ins);
            id.idCodeSize(size);
            return id;
        }

        public static instrDesc Align(uint size)
        {
            var id = new instrDescAlign();
            id.idIns(instruction.INS_align);
            id.idCodeSize(size);
            return id;
        }

        public static instrDescJmp Jump(uint size, bool removable)
        {
            var id = new instrDescJmp { idjIsRemovableJmpCandidate = removable };
            id.idIns(instruction.INS_jmp);
            id.idCodeSize(size);
            return id;
        }

        public static void MarkAfterCall(instrDesc id)
        {
            ((instrDescJmp)id).idjIsAfterCallBeforeEpilog = true;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "emitInstHasNoCode")]
    private static extern bool HasNoCode(Emitter? emitter, Emitter.instrDesc id);
}
#endif
