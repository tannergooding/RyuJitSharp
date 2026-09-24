// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.InfoAccessType;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class MorphTreeSupportTests
{
    [TestCase(IAT_VALUE, 0, GTF_ICON_OBJ_HDL)]
    [TestCase(IAT_PVALUE, 1, GTF_ICON_STR_HDL)]
    [TestCase(IAT_PPVALUE, 2, GTF_ICON_CONST_PTR)]
    public static void StringLiteralAccessPreservesHandleAndIndirections(
        InfoAccessType accessType, int indirections, GenTreeFlags handleKind)
    {
        WithCompiler(compiler => {
#if DEBUG
            var nextId = compiler.compGenTreeID;
#endif
            var node = compiler.gtNewStringLiteralNode(accessType, (void*)0x123400);
            Assert.That(node.Type, Is.EqualTo(TYP_REF));
            for (var i = 0; i < indirections; i++)
            {
                Assert.That(node.Oper, Is.EqualTo(GT_IND));
                Assert.That(node.Flags & (GTF_IND_NONFAULTING | GTF_IND_INVARIANT),
                    Is.EqualTo(GTF_IND_NONFAULTING | GTF_IND_INVARIANT));
                Assert.That(node.Flags & GTF_EXCEPT, Is.EqualTo(GTF_EMPTY));
                if (i == 0)
                {
                    Assert.That(node.Flags & GTF_IND_NONNULL, Is.EqualTo(GTF_IND_NONNULL));
                }

                node = node.AsIndir().Addr;
                Assert.That(node.Type, Is.EqualTo(TYP_I_IMPL));
            }

            Assert.That(node.Oper, Is.EqualTo(GT_CNS_INT));
            Assert.That(node.AsIntCon().IconValue, Is.EqualTo((nint)0x123400));
            Assert.That(node.Flags & GTF_ICON_HDL_MASK, Is.EqualTo(handleKind));
#if DEBUG
            Assert.That(compiler.compGenTreeID, Is.EqualTo(nextId + indirections + 1));
            if (indirections != 0)
            {
                Assert.That(node.AsIntCon().TargetHandle, Is.EqualTo((nint)0x123400));
            }
#endif
        });
    }

#if DEBUG
    [Test]
    public static void MorphStressCopyPreservesIdentityAndShallowChildren()
    {
        WithCompiler(compiler => {
            var left = compiler.gtNewIconNode(TYP_INT, 42);
            var right = compiler.gtNewIconNode(TYP_INT, 7);
            var original = compiler.gtNewBinaryNode(GT_ADD, TYP_INT, left, right);
            original.Flags |= GTF_REVERSE_OPS | GTF_DONT_CSE;
            original._vnPair.SetBoth(123);
            original._seqNum = 19;
            var nextId = compiler.compGenTreeID;

            var clone = original.CloneForMorphStress(compiler);
            Assert.That(clone, Is.Not.SameAs(original));
            Assert.That(clone.GetType(), Is.EqualTo(original.GetType()));
            Assert.That(clone.TreeId, Is.EqualTo(original.TreeId));
            Assert.That(clone.Type, Is.EqualTo(original.Type));
            Assert.That(clone.Oper, Is.EqualTo(original.Oper));
            Assert.That(clone.Flags, Is.EqualTo(original.Flags));
            Assert.That(clone._debugFlags, Is.EqualTo(original._debugFlags));
            Assert.That(clone._vnPair.Liberal, Is.EqualTo(123));
            Assert.That(clone._vnPair.Conservative, Is.EqualTo(123));
            Assert.That(clone._seqNum, Is.Zero);
            Assert.That(original._seqNum, Is.EqualTo(19));
            Assert.That(clone.Prev, Is.Null);
            Assert.That(clone.Next, Is.Null);
            Assert.That(clone.AsOp().Op1, Is.SameAs(left));
            Assert.That(clone.AsOp().Op2, Is.SameAs(right));
            Assert.That(compiler.compGenTreeID, Is.EqualTo(nextId + 1));

            clone.AsOp().Op1 = right;
            Assert.That(original.Op1, Is.SameAs(left));
        });
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public static void MorphStressCopiesInlineOperandSlotsButSharesExternalSlots(int count)
    {
        WithCompiler(compiler => {
            var operands = new GenTree[count];
            for (var i = 0; i < count; i++)
            {
                operands[i] = compiler.gtNewIconNode(TYP_INT, i);
            }

            var original = new GenTreeHWIntrinsic(TYP_SIMD16, NamedIntrinsic.NI_Vector_Create, TYP_INT, 16, operands);
            var clone = original.CloneForMorphStress(compiler).AsHWIntrinsic();
            Assert.That(clone.HWIntrinsicId, Is.EqualTo(original.HWIntrinsicId));
            Assert.That(clone.SimdBaseType, Is.EqualTo(original.SimdBaseType));
            Assert.That(clone.SimdSize, Is.EqualTo(original.SimdSize));
            Assert.That(clone.Operands.ToArray(), Is.EqualTo(operands));

            var first = original.GetOp(1);
            var replacement = compiler.gtNewIconNode(TYP_INT, 100);
            clone.SetOp(1, replacement);
            Assert.That(original.GetOp(1), Is.SameAs(count <= 2 ? first : replacement));
        });
    }

    [Test]
    public static void MorphStressCopiesArrayIndexSlots()
    {
        WithCompiler(compiler => {
            var array = compiler.gtNewLclvNode(TYP_REF, 0);
            var index = compiler.gtNewIconNode(TYP_INT, 3);
            var original = new GenTreeArrElem(TYP_BYREF, array, 4, [index, index]);
            var clone = original.CloneForMorphStress(compiler).AsArrElem();
            Assert.That(clone.ArrObj, Is.SameAs(array));
            Assert.That(clone.ArrElemSize, Is.EqualTo(4));
            Assert.That(clone.ArrRank, Is.EqualTo(2));
            Assert.That(clone.ArrInds.ToArray(), Is.EqualTo(original.ArrInds.ToArray()));

            clone.ArrInds[0] = compiler.gtNewIconNode(TYP_INT, 5);
            Assert.That(original.ArrInds[0], Is.SameAs(index));
        });
    }
#endif

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitTls.Compiler = compiler;

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
