// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenBlockScopeTests
{
    [TestCase(false, 0, 0)]
    [TestCase(false, 1, 1)]
    [TestCase(true, 0, 1)]
    public static void UntrackedScopesRespectDebugCodeAndReferenceCounts(bool debugCode, int references, int count)
    {
        WithScopes((compiler, codeGen) =>
        {
            compiler.opts.compDbgCode = debugCode;
            compiler.lvaTable[0].setLvRefCnt((ushort)references);
            codeGen.siBeginBlock(new BasicBlock(null, null) { bbCodeOffs = 0, bbCodeOffsEnd = 2 });

            Assert.That(codeGen.getVariableLiveKeeper().getLiveRangesForVarForBody(0), Has.Count.EqualTo(count));
            Assert.That(compiler.compNextEnterScopeIndex, Is.EqualTo(1));
        });
    }

    [Test]
    public static void IlGapsConsumeBothSortedCursorsWithoutOpeningSkippedScopes()
    {
        WithScopes((compiler, codeGen) =>
        {
            compiler.opts.compDbgCode = true;
            var first = new BasicBlock(null, null) { bbCodeOffs = 0, bbCodeOffsEnd = 2 };
            codeGen.siBeginBlock(first);
            codeGen.instGen(INS_nop);
            codeGen.siEndBlock(first);

            codeGen.siBeginBlock(new BasicBlock(null, null) { bbCodeOffs = 10, bbCodeOffsEnd = 15 });

            var keeper = codeGen.getVariableLiveKeeper();
            Assert.That(keeper.getLiveRangesForVarForBody(0), Has.Count.EqualTo(1));
            Assert.That(keeper.getLiveRangesForVarForBody(0)[0].m_EndEmitLocation.Valid(), Is.False);
            Assert.That(keeper.getLiveRangesForVarForBody(1), Is.Empty);
            Assert.That(keeper.getLiveRangesForVarForBody(2), Has.Count.EqualTo(1));
            Assert.That(keeper.getLiveRangesForVarForBody(2)[0].m_StartEmitLocation.GetInsNum(), Is.EqualTo(1));
            Assert.That(compiler.compNextEnterScopeIndex, Is.EqualTo(3));
            Assert.That(compiler.compNextExitScopeIndex, Is.EqualTo(2));
            Assert.That(LastEndOffset(codeGen), Is.EqualTo(2));
        });
    }

    [Test]
    public static void FuncletRegionSuppressesFurtherScopeAndEndOffsetUpdates()
    {
        WithScopes((compiler, codeGen) =>
        {
            compiler.opts.compDbgCode = true;
            var funclet = new BasicBlock(null, null) { bbCodeOffs = 0, bbCodeOffsEnd = 2 };
            compiler.fgFirstFuncletBB = funclet;
            codeGen.siBeginBlock(funclet);
            codeGen.siEndBlock(funclet);
            codeGen.siBeginBlock(new BasicBlock(null, null) { bbCodeOffs = 10, bbCodeOffsEnd = 15 });

            Assert.That(compiler.compNextEnterScopeIndex, Is.Zero);
            Assert.That(LastEndOffset(codeGen), Is.Zero);
            Assert.That(codeGen.getVariableLiveKeeper().getLiveRangesForVarForBody(0), Is.Empty);
            Assert.That(codeGen.getVariableLiveKeeper().getLiveRangesForVarForBody(2), Is.Empty);
        });
    }

    [Test]
    public static void InternalOffsetsAndTrackedLocalsDoNotConsumeScopeCursors()
    {
        WithScopes((compiler, codeGen) =>
        {
            var block = new BasicBlock(null, null) { bbCodeOffs = BAD_IL_OFFSET, bbCodeOffsEnd = BAD_IL_OFFSET };
            codeGen.siBeginBlock(block);
            codeGen.siEndBlock(block);
            Assert.That(LastEndOffset(codeGen), Is.Zero);
            compiler.lvaTrackedCount = 1;
            block.bbCodeOffs = 0;
            codeGen.siBeginBlock(block);

            Assert.That(compiler.compNextEnterScopeIndex, Is.Zero);
            Assert.That(codeGen.getVariableLiveKeeper().getLiveRangesForVarForBody(0), Is.Empty);
        });
    }

    private static void WithScopes(Action<Compiler, CodeGen> action)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_INT, REG_RAX, (compiler, codeGen, _) =>
        {
            var local = compiler.lvaTable[0];
            local.lvTracked = false;
            local.lvLRACandidate = false;
            local.RegNum = REG_STK;
            compiler.lvaTable = [local, local, local];
            compiler.lvaCount = 3;
            compiler.info.compLocalsCount = 3;
            compiler.lvaTrackedCount = 0;
            compiler.lvaRefCountState = RefCountState.RCS_NORMAL;
            compiler.opts.compScopeInfo = true;
            compiler.opts.compDbgInfo = true;
            compiler.info.compVarScopes = [
                new() { vsdVarNum = 2, vsdLVnum = 2, vsdLifeBeg = 10, vsdLifeEnd = 15 },
                new() { vsdVarNum = 0, vsdLVnum = 0, vsdLifeBeg = 0, vsdLifeEnd = 2 },
                new() { vsdVarNum = 1, vsdLVnum = 1, vsdLifeBeg = 4, vsdLifeEnd = 6 },
            ];
            compiler.info.compVarScopesCount = 3;
            compiler.compInitScopeLists();
            compiler.compResetScopeLists();
            codeGen.initializeVariableLiveKeeper();
            action(compiler, codeGen);
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "siLastEndOffs")]
    private static extern ref int LastEndOffset(CodeGen codeGen);
}
