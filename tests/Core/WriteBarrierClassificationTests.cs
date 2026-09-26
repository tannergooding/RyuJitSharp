// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class WriteBarrierClassificationTests
{
    [TestCase(var_types.TYP_BYREF, GenTreeFlags.GTF_EMPTY, false, GCInfo.WriteBarrierForm.WBF_BarrierChecked)]
    [TestCase(var_types.TYP_LONG, GenTreeFlags.GTF_EMPTY, false, GCInfo.WriteBarrierForm.WBF_BarrierChecked)]
    [TestCase(var_types.TYP_REF, GenTreeFlags.GTF_EMPTY, false, GCInfo.WriteBarrierForm.WBF_BarrierUnchecked)]
    [TestCase(var_types.TYP_BYREF, GenTreeFlags.GTF_IND_TGT_HEAP, false, GCInfo.WriteBarrierForm.WBF_BarrierUnchecked)]
    [TestCase(var_types.TYP_BYREF, GenTreeFlags.GTF_IND_TGT_NOT_HEAP, false, GCInfo.WriteBarrierForm.WBF_NoBarrier)]
    [TestCase(var_types.TYP_BYREF, GenTreeFlags.GTF_IND_TGT_HEAP, true, GCInfo.WriteBarrierForm.WBF_NoBarrier)]
    public static void WriteBarrierClassificationPreservesAddressAndNullRules(
        var_types addressType, GenTreeFlags flags, bool nullValue, GCInfo.WriteBarrierForm expected)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info.compRetBuffArg = BAD_VAR_NUM;
        compiler.lvaTable = new LclVarDsc[2];
        compiler.lvaCount = 2;
        compiler.lvaTable[0].Type = addressType;
        compiler.lvaTable[1].Type = var_types.TYP_REF;
        JitTls.Compiler = compiler;
        try
        {
            GenTree address = compiler.gtNewLclvNode(addressType, 0);
            if (addressType is var_types.TYP_REF)
            {
                address = new GenTreeOp(genTreeOps.GT_ADD, var_types.TYP_BYREF, address,
                    compiler.gtNewIconNode(var_types.TYP_LONG, 8));
            }
            GenTree value = nullValue
                ? compiler.gtNewIconNode(var_types.TYP_REF, 0)
                : compiler.gtNewLclvNode(var_types.TYP_REF, 1);
            var store = new GenTreeStoreInd(var_types.TYP_REF, address, value) { Flags = flags };
            var gcInfo = new GCInfo(new CodeGen(compiler));

            Assert.That(gcInfo.gcIsWriteBarrierCandidate(store), Is.EqualTo(expected));
            Assert.That(gcInfo.gcIsWriteBarrierStoreIndNode(store),
                Is.EqualTo(expected is not GCInfo.WriteBarrierForm.WBF_NoBarrier));
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ImplicitByrefStorageNeedsNoWriteBarrierEvenWithOffset(bool offset)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info.compRetBuffArg = BAD_VAR_NUM;
        compiler.lvaTable = new LclVarDsc[2];
        compiler.lvaCount = 2;
        compiler.lvaTable[0].Type = TYP_I_IMPL;
        compiler.lvaTable[0].lvIsParam = true;
        compiler.lvaTable[0].IsImplicitByRef = true;
        compiler.lvaTable[1].Type = var_types.TYP_REF;
        JitTls.Compiler = compiler;
        try
        {
            GenTree address = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            if (offset)
            {
                address = new GenTreeOp(genTreeOps.GT_ADD, TYP_I_IMPL, address,
                    compiler.gtNewIconNode(TYP_I_IMPL, 8));
            }
            var store = new GenTreeStoreInd(var_types.TYP_REF, address,
                compiler.gtNewLclvNode(var_types.TYP_REF, 1));

            var gcInfo = new GCInfo(new CodeGen(compiler));
            Assert.That(gcInfo.gcIsWriteBarrierCandidate(store),
                Is.EqualTo(GCInfo.WriteBarrierForm.WBF_NoBarrier));
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
