// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class LoongArch64CodeGenRegisterTransferTests
{
    [TestCase(REG_T0, REG_T1, TYP_LONG)]
    [TestCase(REG_F0, REG_F1, TYP_FLOAT)]
    [TestCase(REG_T0, REG_F0, TYP_FLOAT)]
    [TestCase(REG_F0, REG_T0, TYP_FLOAT)]
    public static void RegisterArgumentMoveReachesLoongArchInstructionRecording(
        regNumber sourceReg, regNumber targetReg, var_types type)
    {
        WithCodeGen(codeGen =>
        {
            var operand = new GenTreePhysReg(sourceReg, type) { RegNum = sourceReg };
            var tree = new GenTreeUnOp(GT_PUTARG_REG, type, operand) { RegNum = targetReg };

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genPutArgReg(tree));

            Assert.That(failure?.Message, Is.EqualTo("Target two-register instruction recording is not implemented."));
        });
    }

    [Test]
    public static void PhysicalRegisterMoveReachesLoongArchInstructionRecording()
    {
        WithCodeGen(codeGen =>
        {
            var tree = new GenTreePhysReg(REG_T0, TYP_BYREF) { RegNum = REG_T1 };

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForPhysReg(tree));

            Assert.That(failure?.Message, Is.EqualTo("Target two-register instruction recording is not implemented."));
        });
    }

    private static void WithCodeGen(Action<CodeGen> action)
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
        compiler.lvaTable = [new LclVarDsc()];
        compiler.lvaTrackedCount = 1;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        compiler.eeInfoInitialized = true;
        compiler.eeInfo.osPageSize = 4096;
        compiler.lvaOutgoingArgSpaceSize.Value = 0;
        compiler.compCurLife = VarSetOps.MakeEmpty(compiler);
        compiler.compCurBB = new BasicBlock(null, null);
        JitTls.Compiler = compiler;
        try
        {
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);
            codeGen.RegSet.rsClearRegsModified();
            codeGen.Emitter.emitBegCG(compiler, default);
            codeGen.Emitter.Init();
            codeGen.Emitter.emitBegFN(false
#if DEBUG
                , true
#endif
                );
            action(codeGen);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "treeLifeUpdater")]
    private static extern ref TreeLifeUpdater? LifeUpdater(CodeGen codeGen);
}
#endif
