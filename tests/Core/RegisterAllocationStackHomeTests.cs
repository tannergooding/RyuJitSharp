// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class RegisterAllocationStackHomeTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void UsedAndUnusedLocalsRetainNativeStackHomeAndInitializationPolicy(bool framePointer)
    {
        WithCompiler(compiler => {
            compiler.codeGen!.IsFramePointerUsed = framePointer;
            compiler.lvaTable = [
                new() { Type = TYP_REF, lvOnFrame = true, lvMustInit = true },
                new() { Type = TYP_INT, lvOnFrame = true, lvMustInit = true },
                new() { Type = TYP_INT, lvRegister = true, lvLRACandidate = true, RegNum = REG_RAX },
                new() { Type = TYP_INT, lvLRACandidate = true, RegNum = REG_RCX },
            ];
            compiler.lvaCount = compiler.lvaTable.Length;
            for (var index = 1; index < compiler.lvaCount; index++)
            {
                compiler.lvaTable[index].setLvRefCnt(1);
            }

            compiler.raMarkStkVars();

            Assert.That(compiler.lvaTable[0].lvOnFrame, Is.False);
            Assert.That(compiler.lvaTable[0].lvMustInit, Is.False);
            Assert.That(compiler.lvaTable[1].lvOnFrame, Is.True);
            Assert.That(compiler.lvaTable[1].lvMustInit, Is.True);
            Assert.That(compiler.lvaTable[2].lvOnFrame, Is.False);
            Assert.That(compiler.lvaTable[2].lvRegister, Is.True);
            Assert.That(compiler.lvaTable[3].lvOnFrame, Is.False);
            foreach (var local in compiler.lvaTable)
            {
                Assert.That(local.lvFramePointerBased, Is.EqualTo(framePointer));
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void OnlyDependentUnusedFieldsRetainTheirStackHome(bool dependent)
    {
        WithCompiler(compiler => {
            compiler.lvaTable = [
                new() { Type = TYP_STRUCT, Layout = new ClassLayout(8), lvPromoted = true,
                    lvDoNotEnregister = dependent, lvOnFrame = true },
                new() { Type = TYP_INT, lvIsStructField = true, lvParentLcl = 0, lvOnFrame = false,
                    lvMustInit = true },
            ];
            compiler.lvaCount = 2;

            compiler.raMarkStkVars();

            Assert.That(compiler.lvaTable[0].lvOnFrame, Is.False);
            Assert.That(compiler.lvaTable[1].lvOnFrame, Is.EqualTo(dependent));
            Assert.That(compiler.lvaTable[1].lvMustInit, Is.EqualTo(dependent));
        });
    }

    [Test]
    public static void ZeroSizedOutgoingArgumentAreaRemainsAValidStackHome()
    {
        WithCompiler(compiler => {
            compiler.lvaTable = [new() { Type = TYP_STRUCT, Layout = new ClassLayout(0), lvOnFrame = true }];
            compiler.lvaCount = 1;
            compiler.lvaOutgoingArgSpaceVar = 0;
            compiler.lvaTable[0].setLvRefCnt(1);

            compiler.raMarkStkVars();

            Assert.That(compiler.lvaTable[0].lvOnFrame, Is.True);
        });
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.lvaRefCountState = RefCountState.RCS_NORMAL;
        compiler.lvaOutgoingArgSpaceVar = BAD_VAR_NUM;
        JitTls.Compiler = compiler;
        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
        codeGen.IsFramePointerUsed = false;
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
