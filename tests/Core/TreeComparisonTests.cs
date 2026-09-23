// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class TreeComparisonTests
{
    [Test]
    public static void ComparesNullIdentityAndLocalNumbers()
    {
        WithCompiler(compiler => {
            var local = compiler.gtNewLclvNode(TYP_INT, 1);
            Assert.That(GenTree.Compare(null, null), Is.True);
            Assert.That(GenTree.Compare(local, null), Is.False);
            Assert.That(GenTree.Compare(null, local), Is.False);
            Assert.That(GenTree.Compare(local, local), Is.True);
            Assert.That(GenTree.Compare(local, compiler.gtNewLclvNode(TYP_INT, 1)), Is.True);
            Assert.That(GenTree.Compare(local, compiler.gtNewLclvNode(TYP_INT, 2)), Is.False);
            Assert.That(GenTree.Compare(local, compiler.gtNewLclvNode(TYP_LONG, 1)), Is.False);
        });
    }

    [TestCase(0L, 0L, true)]
    [TestCase(0L, long.MinValue, false)]
    [TestCase(0x7FF8000000000001L, 0x7FF8000000000001L, true)]
    [TestCase(0x7FF8000000000001L, 0x7FF8000000000002L, false)]
    public static void FloatingConstantsUseBitwiseEquality(long first, long second, bool expected)
    {
        WithCompiler(compiler => {
            var left = compiler.gtNewDconNode(TYP_DOUBLE, BitConverter.Int64BitsToDouble(first));
            var right = compiler.gtNewDconNode(TYP_DOUBLE, BitConverter.Int64BitsToDouble(second));
            Assert.That(GenTree.Compare(left, right), Is.EqualTo(expected));
        });
    }

    [TestCase(GT_ADD, true)]
    [TestCase(GT_MUL, true)]
    [TestCase(GT_AND, true)]
    [TestCase(GT_SUB, false)]
    public static void SwappingRequiresPermissionCommutativityAndNoEffects(genTreeOps oper, bool commutative)
    {
        WithCompiler(compiler => {
            var a = compiler.gtNewLclvNode(TYP_INT, 0);
            var b = compiler.gtNewLclvNode(TYP_INT, 1);
            var left = new GenTreeOp(oper, TYP_INT, a, b);
            var right = new GenTreeOp(oper, TYP_INT, b, a);
            Assert.That(GenTree.Compare(left, right), Is.False);
            Assert.That(GenTree.Compare(left, right, swapOk: true), Is.EqualTo(commutative));
            b.Flags |= GTF_ORDER_SIDEEFF;
            Assert.That(GenTree.Compare(left, right, swapOk: true), Is.False);
        });
    }

    [Test]
    public static void UnaryComparisonDoesNotPropagateSwapPermission()
    {
        WithCompiler(compiler => {
            var a = compiler.gtNewLclvNode(TYP_INT, 0);
            var b = compiler.gtNewLclvNode(TYP_INT, 1);
            var left = new GenTreeOp(GT_ADD, TYP_INT, a, b);
            var right = new GenTreeOp(GT_ADD, TYP_INT, b, a);
            Assert.That(GenTree.Compare(left, right, swapOk: true), Is.True);
            Assert.That(GenTree.Compare(new GenTreeUnOp(GT_NEG, TYP_INT, left),
                new GenTreeUnOp(GT_NEG, TYP_INT, right), swapOk: true), Is.False);
        });
    }

    [TestCase(GT_ADD, GTF_OVERFLOW)]
    [TestCase(GT_ADD, GTF_UNSIGNED)]
    [TestCase(GT_DIV, GTF_DIV_MOD_NO_BY_ZERO)]
    [TestCase(GT_DIV, GTF_DIV_MOD_NO_OVERFLOW)]
    public static void ArithmeticFlagsParticipateInEquality(genTreeOps oper, GenTreeFlags flag)
    {
        WithCompiler(compiler => {
            var left = new GenTreeOp(oper, TYP_INT, compiler.gtNewIconNode(TYP_INT, 4), compiler.gtNewIconNode(TYP_INT, 2));
            var right = new GenTreeOp(oper, TYP_INT, compiler.gtNewIconNode(TYP_INT, 4), compiler.gtNewIconNode(TYP_INT, 2));
            Assert.That(GenTree.Compare(left, right), Is.True);
            right.Flags |= flag;
            Assert.That(GenTree.Compare(left, right), Is.False);
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    public static void ExtendedNodeMetadataParticipatesInEquality(int kind)
    {
        WithCompiler(compiler => {
            GenTree Make(int variant)
            {
                var local = compiler.gtNewLclvNode(TYP_REF, 0);

                return kind switch {
                    0 => new GenTreeLclFld(GT_LCL_FLD, TYP_INT, 0, (ushort)(variant * 4)),
                    1 => new GenTreeArrLen(TYP_INT, local, 8 + variant),
                    2 => new GenTreeMDArr(GT_MDARR_LENGTH, local, variant, 2),
                    3 => new GenTreeCast(TYP_INT, compiler.gtNewLclvNode(TYP_INT, 1), false, variant == 0 ? TYP_BYTE : TYP_SHORT),
                    4 => new GenTreeAddrMode(Globals.TYP_I_IMPL, local, null, 1, variant),
                    _ => new GenTreeVal(GT_CONTINUATION_MEMBER_OFFSET, Globals.TYP_I_IMPL, variant),
                };
            }

            Assert.That(GenTree.Compare(Make(0), Make(0)), Is.True);
            Assert.That(GenTree.Compare(Make(0), Make(1)), Is.False);
        });
    }

    [TestCase(TYP_SIMD8)]
    [TestCase(TYP_SIMD12)]
    [TestCase(TYP_SIMD16)]
    [TestCase(TYP_SIMD32)]
    [TestCase(TYP_SIMD64)]
    public static void VectorEqualityUsesOnlyActiveBytes(var_types type)
    {
        WithCompiler(_ => {
            var left = new GenTreeVecCon(type);
            var right = new GenTreeVecCon(type);
            left.SimdVal.u64[0] = right.SimdVal.u64[0] = 0x7FF8000000000001;
            Assert.That(GenTree.Compare(left, right), Is.True);

            if (type.Size < 64)
            {
                right.SimdVal.AsSpan<byte>()[type.Size] = 0x80;
                Assert.That(GenTree.Compare(left, right), Is.True);
            }

            right.SimdVal.AsSpan<byte>()[type.Size - 1] ^= 0x80;
            Assert.That(GenTree.Compare(left, right), Is.False);
        });
    }

    [Test]
    public static void MaskEqualityIncludesHighBits()
    {
        WithCompiler(_ => {
            var left = new GenTreeMskCon(default);
            var right = new GenTreeMskCon(default);
            Assert.That(GenTree.Compare(left, right), Is.True);
            right.SimdMaskVal.u64[0] = 1UL << 63;
            Assert.That(GenTree.Compare(left, right), Is.False);
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    [TestCase(6)]
    [TestCase(7)]
    [TestCase(8)]
    [TestCase(9)]
    public static void CallEqualityPreservesMetadataAndArgumentOrder(int difference)
    {
        WithCompiler(compiler => {
            GenTreeCall MakeCall(out CallArg argument)
            {
                var call = compiler.gtNewCallNode(TYP_INT, gtCallTypes.CT_USER_FUNC, null);
                argument = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewIconNode(TYP_INT, 1)));

                return call;
            }

            var left = MakeCall(out _);
            var right = MakeCall(out var rightArgument);
            Assert.That(GenTree.Compare(left, right), Is.True);

            switch (difference)
            {
                case 0:
                {
                    right._callMethHnd = (CORINFO_METHOD_STRUCT_*)1;
                    break;
                }

                case 1:
                {
                    right.Flags |= GTF_CALL_NULLCHECK;
                    break;
                }

                case 2:
                {
                    right._returnType = TYP_UINT;
                    break;
                }

                case 3:
                {
                    right.Args.IsVarArgs = true;
                    break;
                }

                case 4:
                {
                    right.ControlExpr = compiler.gtNewIconNode(TYP_INT, 1);
                    break;
                }

                case 5:
                {
                    rightArgument.EarlyNode = compiler.gtNewIconNode(TYP_INT, 2);
                    break;
                }

                case 6:
                {
                    rightArgument.LateNode = compiler.gtNewIconNode(TYP_INT, 1);
                    break;
                }

                case 7:
                {
                    _ = right.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewIconNode(TYP_INT, 2)));
                    break;
                }

                case 8:
                {
                    right.Args = default;
                    _ = right.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewIconNode(TYP_INT, 1), TYP_BYTE));
                    break;
                }

                case 9:
                {
                    left._callMoreFlags |= GenTreeCallFlags.GTF_CALL_M_ASYNC;
                    right._callMoreFlags |= GenTreeCallFlags.GTF_CALL_M_ASYNC;
                    right.GetAsyncInfo().IsTailAwait = true;
                    break;
                }
            }

            Assert.That(GenTree.Compare(left, right), Is.False);
            Assert.That(GenTree.Compare(right, left), Is.False);
        });
    }

    [Test]
    public static void ConditionalAndArrayOperandsAreComparedInOrder()
    {
        WithCompiler(compiler => {
            var condition = compiler.gtNewIconNode(TYP_INT, 1);
            var one = compiler.gtNewIconNode(TYP_INT, 1);
            var two = compiler.gtNewIconNode(TYP_INT, 2);
            var left = new GenTreeConditional(GT_SELECT, TYP_INT, condition, one, two);
            var right = new GenTreeConditional(GT_SELECT, TYP_INT, condition, one, two);
            Assert.That(GenTree.Compare(left, right), Is.True);
            Assert.That(GenTree.Compare(left, new GenTreeConditional(GT_SELECT, TYP_INT, condition, two, one), true), Is.False);
            var array = compiler.gtNewLclvNode(TYP_REF, 0);
            var arr1 = new GenTreeArrElem(TYP_BYREF, array, 4, [one, two]);
            var arr2 = new GenTreeArrElem(TYP_BYREF, array, 4, [one, two]);
            Assert.That(GenTree.Compare(arr1, arr2), Is.True);
            Assert.That(GenTree.Compare(arr1, new GenTreeArrElem(TYP_BYREF, array, 4, [two, one]), true), Is.False);
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public static void IntrinsicEqualityPreservesMetadataAndOperands(int difference)
    {
        WithCompiler(compiler => {
            static GenTreeHWIntrinsic MakeIntrinsic()
            {
                var intrinsic = new GenTreeHWIntrinsic(TYP_SIMD16, NamedIntrinsic.NI_X86Base_Add, TYP_INT, 16,
                    new GenTreeLclVar(TYP_SIMD16, 0), new GenTreeLclVar(TYP_SIMD16, 1));

                return intrinsic;
            }

            var left = MakeIntrinsic();
            var right = MakeIntrinsic();
            Assert.That(GenTree.Compare(left, right), Is.True);

            switch (difference)
            {
                case 0:
                {
                    right.SetHWIntrinsicId(NamedIntrinsic.NI_X86Base_Subtract);
                    break;
                }

                case 1:
                {
                    right.SimdBaseType = TYP_FLOAT;
                    break;
                }

                case 2:
                {
                    right.SimdSize = 12;
                    break;
                }

                case 3:
                {
                    right.AuxiliaryType = TYP_FLOAT;
                    break;
                }

                case 4:
                {
                    right.SetOp(2, new GenTreeLclVar(TYP_SIMD16, 2));
                    break;
                }
            }

            Assert.That(GenTree.Compare(left, right, true), Is.False);
        });
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var jitTls = new JitTls(null);
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
