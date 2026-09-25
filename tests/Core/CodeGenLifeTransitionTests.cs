// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
#if DEBUG
using System.IO;
using System.Text;
#endif
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenLifeTransitionTests
{
    [TestCase(TYP_REF, TYP_BYREF)]
    [TestCase(TYP_BYREF, TYP_REF)]
    [TestCase(TYP_REF, TYP_INT)]
    public static void DeathPrecedesBirthWhenLocalsShareARegister(var_types oldType, var_types newType)
    {
        WithCompiler((compiler, codeGen) =>
        {
            compiler.lvaTable[0].Type = oldType;
            compiler.lvaTable[1].Type = newType;
            compiler.lvaTable[0].RegNum = REG_RAX;
            compiler.lvaTable[1].RegNum = REG_RAX;
            codeGen.genUpdateLife(Set(compiler, 1));
            codeGen.Emitter.emitIns_S_R(INS_mov, EA_8BYTE, REG_RAX, 0, 0);

            codeGen.genUpdateLife(Set(compiler, 0));

            var mask = regMaskTP.CreateFromRegNum(REG_RAX, REG_RAX.SingleTypeMask);
            Assert.That(codeGen.RegSet.GetMaskVars(), Is.EqualTo(mask));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(newType == TYP_REF ? mask : default));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(newType == TYP_BYREF ? mask : default));
            Assert.That(VarSetOps.IsMember(compiler, compiler.compCurLife, 0), Is.True);
            Assert.That(VarSetOps.IsMember(compiler, compiler.compCurLife, 1), Is.False);
            var oldRange = codeGen.getVariableLiveKeeper().getLiveRangesForVarForBody(0)[0];
            var newRange = codeGen.getVariableLiveKeeper().getLiveRangesForVarForBody(1)[0];
            Assert.That(oldRange.m_EndEmitLocation.Valid(), Is.True);
            Assert.That(oldRange.m_EndEmitLocation, Is.EqualTo(newRange.m_StartEmitLocation));
            Assert.That(oldRange.m_StartEmitLocation, Is.Not.EqualTo(oldRange.m_EndEmitLocation));
        });
    }

    [TestCase(false, true, false)]
    [TestCase(true, true, true)]
    [TestCase(true, false, false)]
    public static void RegisterBirthRetainsOnlyExistingAlwaysAliveStackRoots(bool alwaysAlive, bool stackLive, bool expected)
    {
        WithCompiler((compiler, codeGen) =>
        {
            compiler.lvaTable[1].RegNum = REG_RAX;
            compiler.lvaTable[1].lvSpillAtSingleDef = alwaysAlive;
            if (stackLive)
            {
                VarSetOps.AddElemD(compiler, codeGen.GCInfo.gcVarPtrSetCur, 0);
            }

            codeGen.genUpdateLife(Set(compiler, 0));

            Assert.That(VarSetOps.IsMember(compiler, codeGen.GCInfo.gcVarPtrSetCur, 0), Is.EqualTo(expected));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void StackRootBirthUsesGcTrackingPolicyAndDeathRemovesTheRoot(bool stackParameter)
    {
        WithCompiler((compiler, codeGen) =>
        {
            compiler.lvaTable[1].lvIsParam = stackParameter;
            compiler.lvaTable[1].lvIsRegArg = false;
            codeGen.genUpdateLife(Set(compiler, 0));
            Assert.That(VarSetOps.IsMember(compiler, codeGen.GCInfo.gcVarPtrSetCur, 0), Is.EqualTo(!stackParameter));

            codeGen.genUpdateLife(VarSetOps.MakeEmpty(compiler));

            Assert.That(VarSetOps.IsEmpty(compiler, codeGen.GCInfo.gcVarPtrSetCur), Is.True);
            Assert.That(codeGen.getVariableLiveKeeper().getLiveRangesForVarForBody(1)[0].m_EndEmitLocation.Valid(), Is.True);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void IncomingSetsAreCopiedRatherThanAdopted(bool codegen)
    {
        WithCompiler((compiler, codeGen) =>
        {
            if (!codegen)
            {
                compiler.codeGen = null;
            }
            var newLife = Set(compiler, 0);
            var original = compiler.compCurLife;

            compiler.compUpdateLife(newLife, forCodeGen: codegen);
            VarSetOps.RemoveElemD(compiler, newLife, 0);

            Assert.That(compiler.compCurLife, Is.SameAs(original));
            Assert.That(VarSetOps.IsMember(compiler, compiler.compCurLife, 0), Is.True);
            Assert.That(VarSetOps.IsMember(compiler, codeGen.GCInfo.gcVarPtrSetCur, 0), Is.EqualTo(codegen));
        });
    }

    [Test]
    public static void UnchangedMembershipDoesNotReopenVariableRanges()
    {
        WithCompiler((compiler, codeGen) =>
        {
            codeGen.genUpdateLife(Set(compiler, 0));
            var before = compiler.compCurLife;
            codeGen.genUpdateLife(Set(compiler, 0));
            codeGen.genUpdateLife(compiler.compCurLife);

            Assert.That(compiler.compCurLife, Is.SameAs(before));
            Assert.That(codeGen.getVariableLiveKeeper().getLiveRangesForVarForBody(1), Has.Count.EqualTo(1));
        });
    }

#if DEBUG
    [Test]
    public static void DiagnosticsRetainNullTreeSpellingAndDeathBeforeBirthOrder()
    {
        WithCompiler((compiler, codeGen) =>
        {
            compiler.opts.compDbgInfo = false;
            compiler.lvaTable[0].RegNum = REG_RAX;
            compiler.lvaTable[1].RegNum = REG_RAX;
            codeGen.genUpdateLife(Set(compiler, 1));
            compiler.verbose = true;
            var output = Capture(() =>
            {
                codeGen.genUpdateLife(Set(compiler, 0));
                codeGen.genUpdateLife(Set(compiler, 0));
            });
            var indent = new string('\t', 7);

            Assert.That(output, Is.EqualTo(
                $"Change life 0000000000000002 {{V00}} -> 0000000000000001 {{V01}}{Environment.NewLine}" +
                $"{indent}V00 in reg rax is becoming dead  [------]{Environment.NewLine}" +
                $"{indent}Live regs: 0000000000000001 {{rax}} - {{rax}} => 0000000000000000 {{}}{Environment.NewLine}" +
                $"{indent}V01 in reg rax is becoming live  [------]{Environment.NewLine}" +
                $"{indent}Live regs: 0000000000000000 {{}} + {{rax}} => 0000000000000001 {{rax}}{Environment.NewLine}" +
                $"Liveness not changing: 0000000000000001 {{V01}}{Environment.NewLine}"));
        });
    }

    private static string Capture(Action action)
    {
        using var stream = new MemoryStream();
        using var writer = new JitTextWriter(stream, leaveOpen: true);
        var previous = s_jitstdout;
        try
        {
            s_jitstdout = writer;
            action();
            writer.Flush();
        }
        finally
        {
            s_jitstdout = previous;
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
#endif

    private static nint[] Set(Compiler compiler, int index)
    {
        var set = VarSetOps.MakeEmpty(compiler);
        VarSetOps.AddElemD(compiler, set, index);

        return set;
    }

    private static void WithCompiler(Action<Compiler, CodeGen> action)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_REF, REG_RAX, (compiler, codeGen, _) =>
        {
            compiler.lvaCount = 2;
            compiler.info.compLocalsCount = 2;
            compiler.lvaTrackedCount = 2;
            compiler.lvaTrackedToVarNum = [1, 0];
            compiler.lvaTable = [
                new()
                {
                    Type = TYP_REF, RegNum = REG_STK, lvTracked = true, lvLRACandidate = true,
                    lvOnFrame = true, lvFramePointerBased = true, StackOffset = -16, _varIndex = 1,
                },
                new()
                {
                    Type = TYP_REF, RegNum = REG_STK, lvTracked = true, lvLRACandidate = true,
                    lvOnFrame = true, lvFramePointerBased = true, StackOffset = -8, _varIndex = 0,
                },
            ];
            compiler.opts.compDbgInfo = true;
            codeGen.RegSet.ClearMaskVars();
            codeGen.genPrepForCompiler();
            codeGen.genInitialize();
            action(compiler, codeGen);
        });
    }
}
