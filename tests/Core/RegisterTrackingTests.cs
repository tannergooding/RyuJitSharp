// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static unsafe class RegisterTrackingTests
{
    [Test]
    public static void ModifiedMaskAccumulatesAndRemovingRegistersPreservesOthers()
    {
        WithCompiler(minOpts: true, (_, codeGen) => {
            ref var registers = ref codeGen.RegSet;
            registers.rsClearRegsModified();

            var modified = new regMaskTP(SRBM_RAX | SRBM_RBX | SRBM_XMM6);
            registers.rsSetRegsModified(modified);
            registers.rsSetRegsModified(new regMaskTP(SRBM_RBX), suppressDump: true);

            Assert.That(registers.rsGetModifiedRegsMask(), Is.EqualTo(modified));
            Assert.That(registers.rsGetModifiedCalleeSavedRegsMask(),
                Is.EqualTo(new regMaskTP(SRBM_RBX | SRBM_XMM6)));
            Assert.That(registers.rsGetModifiedIntCalleeSavedRegsMask(), Is.EqualTo(new regMaskTP(SRBM_RBX)));
            Assert.That(registers.rsGetModifiedOsrIntCalleeSavedRegsMask(), Is.EqualTo(new regMaskTP(SRBM_RBX)));
            Assert.That(registers.rsGetModifiedFltCalleeSavedRegsMask(), Is.EqualTo(new regMaskTP(SRBM_XMM6)));
            Assert.That(registers.rsRegsModified(new regMaskTP(SRBM_RBX)), Is.True);
            Assert.That(registers.rsRegsModified(new regMaskTP(SRBM_RCX)), Is.False);

            registers.rsRemoveRegsModified(new regMaskTP(SRBM_RBX | SRBM_RCX));
            Assert.That(registers.rsGetModifiedRegsMask(), Is.EqualTo(new regMaskTP(SRBM_RAX | SRBM_XMM6)));

            registers.rsClearRegsModified();
            Assert.That(registers.rsGetModifiedRegsMask(), Is.EqualTo(RBM_NONE));
        });
    }

    [Test]
    public static void VerifyRegisterUsedAddsRegisterToModifiedMask()
    {
        WithCompiler(minOpts: true, (_, codeGen) => {
            ref var registers = ref codeGen.RegSet;
            registers.rsClearRegsModified();

            registers.verifyRegUsed(REG_RBX);

            Assert.That(registers.rsGetModifiedRegsMask(), Is.EqualTo(new regMaskTP(SRBM_RBX)));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public static void VerifyRegistersUsedSkipsMinOptsButMarksOptimizedRegisters(bool minOpts)
    {
        WithCompiler(minOpts, (_, codeGen) => {
            ref var registers = ref codeGen.RegSet;
            registers.rsClearRegsModified();

            registers.verifyRegistersUsed(RBM_NONE);
            registers.verifyRegistersUsed(new regMaskTP(SRBM_RBX | SRBM_RAX));

            Assert.That(registers.rsGetModifiedRegsMask(),
                Is.EqualTo(minOpts ? RBM_NONE : new regMaskTP(SRBM_RBX | SRBM_RAX)));
        });
    }

    [Test]
    public static void FinalFrameLayoutAllowsAlreadySavedAndCallerSavedRegisters()
    {
        WithCompiler(minOpts: true, (compiler, codeGen) => {
            ref var registers = ref codeGen.RegSet;
            registers.rsClearRegsModified();
            registers.rsSetRegsModified(new regMaskTP(SRBM_RBX));
            compiler.lvaDoneFrameLayout = Compiler.FINAL_FRAME_LAYOUT;

            registers.rsSetRegsModified(new regMaskTP(SRBM_RBX | SRBM_RAX));
            registers.rsRemoveRegsModified(new regMaskTP(SRBM_RAX));

            Assert.That(registers.rsGetModifiedRegsMask(), Is.EqualTo(new regMaskTP(SRBM_RBX)));
        });
    }

    [TestCase(InsGroupFlags.Prolog)]
    [TestCase(InsGroupFlags.FuncletProlog)]
    [TestCase(InsGroupFlags.Epilog)]
    [TestCase(InsGroupFlags.FuncletEpilog)]
    public static void FinalFrameLayoutAllowsCalleeSavedChangesInPrologAndEpilogGroups(InsGroupFlags phase)
    {
        WithCompiler(minOpts: true, (compiler, codeGen) => {
            ref var registers = ref codeGen.RegSet;
            registers.rsClearRegsModified();
            compiler.lvaDoneFrameLayout = Compiler.FINAL_FRAME_LAYOUT;
            codeGen.Emitter.emitCurIG = new insGroup { igFlags = phase };

            registers.rsSetRegsModified(new regMaskTP(SRBM_RBX));
            Assert.That(registers.rsGetModifiedCalleeSavedRegsMask(), Is.EqualTo(new regMaskTP(SRBM_RBX)));

            registers.rsRemoveRegsModified(new regMaskTP(SRBM_RBX));
            Assert.That(registers.rsGetModifiedRegsMask(), Is.EqualTo(RBM_NONE));
        });
    }

    private static void WithCompiler(bool minOpts, Action<Compiler, CodeGen> action)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(minOpts);
        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
        action(compiler, codeGen);
    }
}
