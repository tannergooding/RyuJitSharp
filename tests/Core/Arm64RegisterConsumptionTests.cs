// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class Arm64RegisterConsumptionTests
{
    [TestCase(TYP_REF)]
    [TestCase(TYP_BYREF)]
    [TestCase(TYP_INT)]
    public static void ScalarConsumptionClearsTheConsumedGcRegister(var_types type)
    {
        WithCompiler((compiler, codeGen) =>
        {
            var value = compiler.gtNewIconNode(type, 0);
            value.RegNum = REG_R0;
            codeGen.GCInfo.gcMarkRegPtrVal(REG_R0, type);

            Assert.That(codeGen.genConsumeReg(value), Is.EqualTo(REG_R0));

            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur.IsEmpty, Is.True);
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur.IsEmpty, Is.True);
        });
    }

    [TestCase(GT_EQ)]
    [TestCase(GT_AND)]
    public static void ContainedPairsConsumeBothOperandsInOrder(genTreeOps oper)
    {
        WithCompiler((compiler, codeGen) =>
        {
            var left = compiler.gtNewIconNode(TYP_BYREF, 0);
            var right = compiler.gtNewIconNode(TYP_BYREF, 0);
            left.RegNum = REG_R0;
            right.RegNum = REG_R1;
            var pair = new GenTreeOp(oper, TYP_I_IMPL, left, right) { IsContained = true };
            codeGen.GCInfo.gcMarkRegPtrVal(REG_R0, TYP_BYREF);
            codeGen.GCInfo.gcMarkRegPtrVal(REG_R1, TYP_BYREF);
#if DEBUG
            var useNumber = 0;
            codeGen.genNumberOperandUse(pair, ref useNumber);
            Assert.That(useNumber, Is.EqualTo(2));
#endif

            codeGen.genConsumeRegs(pair);

            Assert.That(codeGen.GCInfo.gcRegByrefSetCur.IsEmpty, Is.True);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ContainedAddressCastsConsumeTheUnderlyingRegister(bool bitfield)
    {
        WithCompiler((compiler, codeGen) =>
        {
            var address = compiler.gtNewIconNode(TYP_BYREF, 0);
            address.RegNum = REG_R0;
            var cast = new GenTreeCast(TYP_I_IMPL, address, false, TYP_I_IMPL) { IsContained = true };
            GenTree operand = cast;
            if (bitfield)
            {
                var shift = compiler.gtNewIconNode(TYP_INT, 1);
                shift.IsContained = true;
                operand = new GenTreeOp(GT_BFIZ, TYP_I_IMPL, cast, shift) { IsContained = true };
            }
            codeGen.GCInfo.gcMarkRegPtrVal(REG_R0, TYP_BYREF);

            codeGen.genConsumeRegs(operand);

            Assert.That(codeGen.GCInfo.gcRegByrefSetCur.IsEmpty, Is.True);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void UnspilledValuesDoNotReloadOrChangeGcState(bool indexed)
    {
        WithCompiler((compiler, codeGen) =>
        {
            codeGen.GCInfo.gcMarkRegPtrVal(REG_R0, TYP_BYREF);
            if (indexed)
            {
                compiler.lvaTable = [new LclVarDsc { Type = TYP_STRUCT }];
                compiler.lvaCount = 1;
                var local = compiler.gtNewLclvNode(TYP_STRUCT, 0).AsLclVar();
                local.SetMultiReg();
                codeGen.genUnspillRegIfNeeded(local, 1);
            }
            else
            {
                var value = compiler.gtNewIconNode(TYP_BYREF, 0);
                value.RegNum = REG_R0;
                codeGen.genUnspillRegIfNeeded(value);
            }

            Assert.That(codeGen.GCInfo.gcRegByrefSetCur,
                Is.EqualTo(regMaskTP.CreateFromRegNum(REG_R0, REG_R0.SingleTypeMask)));
        });
    }

    private static void WithCompiler(Action<Compiler, CodeGen> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.compCurLife = [];
        JitTls.Compiler = compiler;
        try
        {
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);
            action(compiler, codeGen);
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
