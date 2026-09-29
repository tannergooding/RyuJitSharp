// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
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

    [Test]
    public static void IndexedCallResultTypesAndSpillFlags()
    {
        WithCompiler((compiler, _) =>
        {
            var call = new GenTreeCall(TYP_STRUCT);
            call._returnTypeDesc.InitializeReturnType(compiler, TYP_REF, null, CorInfoCallConvExtension.Managed);
            ReturnTypes(ref call._returnTypeDesc)[1] = TYP_BYREF;
            GenTree result = call;

            Assert.That(result.GetRegTypeByIndex(0), Is.EqualTo(TYP_REF));
            Assert.That(result.GetRegTypeByIndex(1), Is.EqualTo(TYP_BYREF));

            result.SetRegSpillFlagByIdx(GTF_SPILL, 1);
            result.SetRegSpillFlagByIdx(GTF_SPILLED, 0);

            Assert.That(result.GetRegSpillFlagByIdx(0), Is.EqualTo(GTF_SPILLED));
            Assert.That(result.GetRegSpillFlagByIdx(1), Is.EqualTo(GTF_SPILL));
        });
    }

    [TestCase(8, TYP_SIMD8)]
    [TestCase(16, TYP_SIMD16)]
    public static void IndexedHardwareIntrinsicResultTypesAndSpillFlags(int simdSize, var_types expectedType)
    {
        WithCompiler((_, _) =>
        {
            var intrinsic = NewPairIntrinsic(checked((byte)simdSize));
            GenTree result = intrinsic;

            Assert.That(result.GetRegTypeByIndex(1), Is.EqualTo(expectedType));

            result.SetRegSpillFlagByIdx(GTF_SPILL, 1);
            Assert.That(result.GetRegSpillFlagByIdx(1), Is.EqualTo(GTF_SPILL));
        });
    }

    [Test]
    public static void IndexedHardwareIntrinsicSpillFlagRead()
    {
        WithCompiler((_, _) =>
        {
            var intrinsic = NewPairIntrinsic(16);
            GenTree result = intrinsic;
            Assert.That(result.GetRegSpillFlagByIdx(1), Is.EqualTo(GTF_EMPTY));
        });
    }

    [Test]
    public static void IndexedLocalSpillFlagsAndLongResultType()
    {
        WithCompiler((_, _) =>
        {
            var local = new GenTreeLclVar(TYP_STRUCT, 0);
            local.SetMultiReg();
            GenTree result = local;

            result.SetRegSpillFlagByIdx(GTF_SPILL, 1);
            Assert.That(result.GetRegSpillFlagByIdx(1), Is.EqualTo(GTF_SPILL));
            Assert.That(new GenTreeLclVar(TYP_LONG, 0).GetRegTypeByIndex(1), Is.EqualTo(TYP_INT));
        });
    }

    private static GenTreeHWIntrinsic NewPairIntrinsic(byte simdSize)
    {
        var operands = new GenTree[HWIntrinsicInfo.lookupNumArgs(NI_AdvSimd_Arm64_LoadPairVector128)];
        for (var index = 0; index < operands.Length; index++)
        {
            operands[index] = new GenTreeLclVar(TYP_I_IMPL, index);
        }

        return new GenTreeHWIntrinsic(
            TYP_STRUCT, NI_AdvSimd_Arm64_LoadPairVector128, TYP_INT, simdSize, operands);
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

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_regType")]
    private static extern ref InlineArrayMaxRetRegCount<var_types> ReturnTypes(ref ReturnTypeDesc descriptor);
}
#endif
