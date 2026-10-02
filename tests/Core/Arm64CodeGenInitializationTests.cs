// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm64CodeGenInitializationTests
{
    [Test]
    public static void InitializationResetsLivenessAndMarksLiveInRegisterParameters()
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.lvaCount = 1;
        compiler.lvaTrackedCount = 1;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        compiler.lvaTrackedToVarNum = [0];
        compiler.lvaTable =
        [
            new LclVarDsc
            {
                Type = TYP_INT,
                RegNum = REG_R0,
                lvIsParam = true,
                lvRegister = true,
                lvTracked = true,
                lvLRACandidate = true,
                _varIndex = 0,
            },
        ];
        compiler.lvaDoneFrameLayout = Compiler.INITIAL_FRAME_LAYOUT;
        compiler.compCurLife = VarSetOps.MakeEmpty(compiler);
        VarSetOps.AddElemD(compiler, compiler.compCurLife, 0);
        compiler.fgFirstBB = new BasicBlock(null, null)
        {
            bbLiveIn = VarSetOps.MakeSingleton(compiler, 0),
        };
        JitTls.Compiler = compiler;

        try
        {
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            codeGen.RegSet.rsClearRegsModified();
            codeGen.GCInfo.gcVarPtrSetCur = VarSetOps.MakeSingleton(compiler, 0);
            codeGen.GCInfo.gcRegGCrefSetCur = regMaskTP.CreateFromRegNum(REG_R0, REG_R0.SingleTypeMask);
            codeGen.GCInfo.gcRegByrefSetCur = regMaskTP.CreateFromRegNum(REG_R1, REG_R1.SingleTypeMask);
            codeGen.genInitialize();

            Assert.That(codeGen.RegSet.rsGetModifiedRegsMask(),
                Is.EqualTo(regMaskTP.CreateFromRegNum(REG_R0, REG_R0.SingleTypeMask)));
            Assert.That(VarSetOps.IsEmpty(compiler, compiler.compCurLife), Is.True);
            Assert.That(VarSetOps.IsEmpty(compiler, codeGen.GCInfo.gcVarPtrSetCur), Is.True);
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur.IsEmpty, Is.True);
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur.IsEmpty, Is.True);
            Assert.That(codeGen.getCurrentStackLevel(), Is.Zero);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
#endif
