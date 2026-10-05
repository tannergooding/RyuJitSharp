// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class GenTreeSetOperTests
{
    [Test]
    public static void SetOperToIntConstantClearsFieldMetadata()
    {
        SsaLivenessTests.WithCompiler(2, _ => {
            var fieldSeq = (FieldSeq)RuntimeHelpers.GetUninitializedObject(typeof(FieldSeq));
            var node = new GenTreeIntCon(TYP_INT, 1, fieldSeq);
#if DEBUG
            node.TargetHandle = 2;
#endif

            node.SetOper(GT_CNS_INT);

            Assert.That(node.FieldSeq, Is.Null);
#if DEBUG
            Assert.That(node.TargetHandle, Is.EqualTo((nint)0));
#endif
        });
    }

    [TestCase(GT_LCL_FLD)]
    [TestCase(GT_STORE_LCL_FLD)]
    public static void SetOperToLocalFieldClearsOffsetAndLayout(genTreeOps oper)
    {
        SsaLivenessTests.WithCompiler(2, _ => {
            var layout = new ClassLayout(8);
            var node = oper is GT_LCL_FLD
                ? new GenTreeLclFld(GT_LCL_FLD, TYP_INT, 0, 12, layout)
                : new GenTreeLclFld(TYP_INT, 0, 12, new GenTreeIntCon(TYP_INT, 1), layout);

            node.SetOper(oper);

            Assert.That(node.LclOffs, Is.Zero);
            Assert.That(node.Layout, Is.Null);
        });
    }

    [Test]
    public static void SetOperToLocalAddressClearsLayoutWithoutChangingOffset()
    {
        SsaLivenessTests.WithCompiler(2, _ => {
            var node = new GenTreeLclFld(GT_LCL_ADDR, TYP_BYREF, 0, 12, new ClassLayout(8));

            node.SetOper(GT_LCL_ADDR);

            Assert.That(node.LclOffs, Is.EqualTo(12));
            Assert.That(node.Layout, Is.Null);
        });
    }

    [Test]
    public static void SetOperToCallClearsArguments()
    {
        SsaLivenessTests.WithCompiler(2, _ => {
            var node = new GenTreeCall(TYP_VOID);
            node.Args.IsVarArgs = true;

            node.SetOper(GT_CALL);

            Assert.That(node.Args.IsVarArgs, Is.False);
        });
    }

#if DEBUG
    [Test]
    public static void SetOperToLocalVariableResetsIlOffset()
    {
        SsaLivenessTests.WithCompiler(2, _ => {
            var node = new GenTreeLclVar(TYP_INT, 0, 12);

            node.SetOper(GT_LCL_VAR);

            Assert.That(node.LclIlOffs, Is.EqualTo(BAD_IL_OFFSET));
        });
    }
#endif
}
