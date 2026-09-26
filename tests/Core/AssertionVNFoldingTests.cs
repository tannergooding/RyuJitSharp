// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeCallFlags;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;
using static RyuJitSharp.VNFunc;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class AssertionVNFoldingTests
{
    [TestCase(false, 1, true)]
    [TestCase(false, 0, false)]
    [TestCase(true, 0, true)]
    public static void SequenceEqualFoldsEqualPointersOrZeroLength(bool zeroLength, int samePointer, bool folds)
    {
        WithCompiler("SequenceEqual", (compiler, store) =>
        {
            var first = compiler.gtNewLclvNode(TYP_BYREF, 0);
            var second = compiler.gtNewLclvNode(TYP_BYREF, 1);
            var length = compiler.gtNewLclvNode(TYP_I_IMPL, 2);
            var pointerVN = store.VNForExpr(null, TYP_BYREF);
            first._vnPair.SetBoth(pointerVN);
            second._vnPair.SetBoth(samePointer != 0 ? pointerVN : store.VNForExpr(null, TYP_BYREF));
            length._vnPair.SetBoth(zeroLength ? store.VNForIntCon(0) : store.VNForExpr(null, TYP_I_IMPL));
            var call = NewCall(compiler, TYP_INT, first, second, length);

            var result = compiler.optVNBasedFoldExpr_Call_Memcmp(call);
            Assert.That(result is not null, Is.EqualTo(folds));
            if (folds)
            {
                Assert.That(result?.Oper, Is.EqualTo(GT_CNS_INT));
                Assert.That(result?.AsIntCon().IconValue, Is.EqualTo((nint)1));
            }
        });
    }

    [TestCase(TYP_UBYTE, 2, 0)]
    [TestCase(TYP_INT, 0, 0)]
    [TestCase(TYP_INT, 2, 2)]
    [TestCase(TYP_INT, 100000, 0)]
    public static void FillUnrollsSupportedWidthsAndThresholds(var_types type, int count, int expectedStores)
    {
        WithCompiler("Fill", (compiler, store) =>
        {
            var destination = compiler.gtNewLclvNode(TYP_BYREF, 0);
            var length = compiler.gtNewIconNode(TYP_I_IMPL, count);
            length._vnPair.SetBoth(store.VNForIntCon(count));
            var value = compiler.gtNewIconNode(type, 0x01020304);
            var call = NewCall(compiler, TYP_VOID, destination, length, value);

            var result = compiler.optVNBasedFoldExpr_Call_Memset(call);
            Assert.That(CountStores(result), Is.EqualTo(expectedStores));
            if (expectedStores != 0)
            {
                Assert.That(result, Is.Not.Null);
                var lastStore = result?.Oper is GT_COMMA ? result.AsOp().Op2.AsStoreInd() : result?.AsStoreInd();
                Assert.That(lastStore?.Addr.AsOp().Op2.AsIntCon().IconValue,
                    Is.EqualTo((nint)((count - 1) * type.Size)));
                Assert.That(lastStore?.Flags & GTF_IND_UNALIGNED, Is.EqualTo(GTF_IND_UNALIGNED));
                Assert.That(lastStore?.Flags & GTF_IND_ALLOW_NON_ATOMIC, Is.EqualTo(GTF_IND_ALLOW_NON_ATOMIC));
            }
        });
    }

    [TestCase(0)]
    [TestCase(4)]
    public static void MemmoveZeroLengthPreservesArgumentEffectsWithoutDereferencing(int length)
    {
        WithCompiler("Memmove", (compiler, store) =>
        {
            var destination = compiler.gtNewLclvNode(TYP_BYREF, 0);
            var source = compiler.gtNewLclvNode(TYP_BYREF, 1);
            var lengthNode = compiler.gtNewIconNode(TYP_I_IMPL, length);
            lengthNode._vnPair.SetBoth(store.VNForIntCon(length));
            var call = NewCall(compiler, TYP_VOID, destination, source, lengthNode);
            var result = compiler.optVNBasedFoldExpr_Call_Memmove(call);

            if (length == 0)
            {
                Assert.That(result?.IsNothingNode, Is.True);
            }
            else
            {
                Assert.That(result, Is.Null);
                Assert.That(call.Oper, Is.EqualTo(GT_CALL));
            }
        });
    }

    [Test]
    public static void ZeroLengthMemmoveExtractsDestinationEffectsBeforeReturningNothing()
    {
        WithCompiler("Memmove", (compiler, store) =>
        {
            var effect = compiler.gtNewStoreLclVarNode(2, compiler.gtNewIconNode(TYP_I_IMPL, 7));
            var destination = compiler.gtNewCommaNode(TYP_BYREF, effect, compiler.gtNewLclvNode(TYP_BYREF, 0));
            var source = compiler.gtNewLclvNode(TYP_BYREF, 1);
            var length = compiler.gtNewIconNode(TYP_I_IMPL, 0);
            length._vnPair.SetBoth(store.VNForIntCon(0));
            var call = NewCall(compiler, TYP_VOID, destination, source, length);

            var result = compiler.optVNBasedFoldExpr_Call_Memmove(call);
            Assert.That(result?.Oper, Is.EqualTo(GT_COMMA));
            Assert.That(result?.AsOp().Op1, Is.SameAs(effect));
            Assert.That(result?.AsOp().Op2.IsNothingNode, Is.True);
        });
    }

    [Test]
    public static void DispatchesMemoryIntrinsicsWithoutAttemptingUnrelatedCalls()
    {
        WithCompiler("SequenceEqual", (compiler, store) =>
        {
            var block = new BasicBlock(null, null);
            var first = compiler.gtNewLclvNode(TYP_BYREF, 0);
            var second = compiler.gtNewLclvNode(TYP_BYREF, 1);
            var length = compiler.gtNewIconNode(TYP_I_IMPL, 0);
            length._vnPair.SetBoth(store.VNForIntCon(0));
            var call = NewCall(compiler, TYP_INT, first, second, length);
            Assert.That(compiler.optVNBasedFoldExpr_Call(block, null, call)?.AsIntCon().IconValue, Is.EqualTo((nint)1));

            call._callMoreFlags &= ~GTF_CALL_M_SPECIAL_INTRINSIC;
            Assert.That(compiler.optVNBasedFoldExpr_Call(block, null, call), Is.Null);
        });
    }

    [Test]
    public static void CastHelperWithMatchingObjectVNReturnsOriginalArgument()
    {
        WithCompiler("SequenceEqual", (compiler, store) =>
        {
            var castClass = compiler.gtNewIconNode(TYP_I_IMPL, 0);
            var value = compiler.gtNewLclvNode(TYP_REF, 0);
            value._vnPair.SetBoth(store.VNForExpr(null, TYP_REF));
            var call = compiler.gtNewCallNode(TYP_REF, gtCallTypes.CT_HELPER,
                Compiler.eeFindHelper(CorInfoHelpFunc.CORINFO_HELP_ISINSTANCEOFANY));
            call._vnPair = value._vnPair;
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(castClass));
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(value));

            var result = compiler.optVNBasedFoldExpr_Call(new BasicBlock(null, null), null, call);
            Assert.That(result?.Oper, Is.EqualTo(GT_LCL_VAR));
            Assert.That(result?.AsLclVar().LclNum, Is.EqualTo(value.LclNum));
            Assert.That(result?._vnPair, Is.EqualTo(value._vnPair));
            Assert.That(call.Oper, Is.EqualTo(GT_CALL));
        });
    }

    [Test]
    public static void ProfitabilityRejectsStaticAndClassHandlesButAcceptsOrdinaryIntegers()
    {
        WithCompiler("SequenceEqual", (compiler, _) =>
        {
            var block = new BasicBlock(null, null);
            var destination = compiler.gtNewLclvNode(TYP_INT, 0);
            var constant = compiler.gtNewIconNode(TYP_INT, 1);
            Assert.That(compiler.optIsProfitableToSubstitute(destination, block, null, constant), Is.True);

            constant.Flags |= GTF_ICON_CLASS_HDL;
            Assert.That(compiler.optIsProfitableToSubstitute(destination, block, null, constant), Is.False);
            constant.Flags &= ~GTF_ICON_CLASS_HDL;
            constant.Flags |= GTF_ICON_STATIC_HDL;
            Assert.That(compiler.optIsProfitableToSubstitute(destination, block, null, constant), Is.False);
        });
    }

    [TestCase(0x80000000u)]
    [TestCase(0x7FC01234u)]
    public static void FloatBitCastUsesRawBitsAndRetainsValueNumbers(uint bits)
    {
        WithCompiler("SequenceEqual", (compiler, store) =>
        {
            var constantVN = store.VNForFloatCon(BitConverter.Int32BitsToSingle(unchecked((int)bits)));
            var tree = compiler.gtNewLclvNode(TYP_INT, 0);
            tree._vnPair.SetBoth(constantVN);

            var result = compiler.optVNBasedFoldConstExpr(new BasicBlock(null, null), null, tree);
            Assert.That(result?.AsIntCon().IconValue, Is.EqualTo(unchecked((nint)(int)bits)));
            Assert.That(result?._vnPair, Is.EqualTo(tree._vnPair));
        });
    }

    [TestCase(0x8000000000000000ul)]
    [TestCase(0x7FF8000000001234ul)]
    public static void DoubleBitCastUsesRawBits(ulong bits)
    {
        WithCompiler("SequenceEqual", (compiler, store) =>
        {
            var constantVN = store.VNForDoubleCon(BitConverter.Int64BitsToDouble(unchecked((long)bits)));
            var tree = compiler.gtNewLclvNode(TYP_LONG, 0);
            tree._vnPair.SetBoth(constantVN);

            var result = compiler.optVNBasedFoldConstExpr(new BasicBlock(null, null), null, tree);
            Assert.That(result?.AsIntConCommon().IconValue, Is.EqualTo(unchecked((nint)(long)bits)));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void FloatToDoublePreservesNegativeZeroAndNaNPayload(bool nan)
    {
        WithCompiler("SequenceEqual", (compiler, store) =>
        {
            var bits = nan ? unchecked((int)0xFFC01234) : int.MinValue;
            var value = BitConverter.Int32BitsToSingle(bits);
            var tree = compiler.gtNewLclvNode(TYP_DOUBLE, 0);
            tree._vnPair.SetBoth(store.VNForFloatCon(value));

            var result = compiler.optVNBasedFoldConstExpr(new BasicBlock(null, null), null, tree);
            Assert.That(result?.Oper, Is.EqualTo(GT_CNS_DBL));
            Assert.That(BitConverter.DoubleToInt64Bits(result?.AsDblCon().DconVal ?? 0),
                Is.EqualTo(BitConverter.DoubleToInt64Bits(value)));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ObjectVNOnlyMaterializesNonNullObjectHandlesAsHandles(bool nonNull)
    {
        WithCompiler("SequenceEqual", (compiler, store) =>
        {
            var tree = compiler.gtNewLclvNode(TYP_REF, 0);
            tree._vnPair.SetBoth(nonNull
                ? store.VNForHandle(0x1110, GTF_ICON_OBJ_HDL)
                : ValueNumStore.VNForNull());

            var result = compiler.optVNBasedFoldConstExpr(new BasicBlock(null, null), null, tree);
            Assert.That(result?.Oper.IsCnsIntOrI, Is.True);
            Assert.That(result?.AsIntCon().IconValue, Is.EqualTo(nonNull ? (nint)0x1110 : 0));
            Assert.That(result?.AsIntCon().IconHandleFlag, Is.EqualTo(nonNull ? GTF_ICON_OBJ_HDL : GTF_EMPTY));
        });
    }

    [TestCase(false, true)]
    [TestCase(true, false)]
    public static void LongHandlesRespectRelocations(bool relocations, bool folded)
    {
        WithCompiler("SequenceEqual", (compiler, store) =>
        {
            compiler.opts.compReloc = relocations;
            var tree = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            tree._vnPair.SetBoth(store.VNForHandle(0x12345678, GTF_ICON_FTN_ADDR));

            var result = compiler.optVNBasedFoldConstExpr(new BasicBlock(null, null), null, tree);
            Assert.That(result is not null, Is.EqualTo(folded));
            if (folded)
            {
                Assert.That(result?.AsIntCon().IconHandleFlag, Is.EqualTo(GTF_ICON_FTN_ADDR));
            }
        });
    }

    [TestCase(0)]
    [TestCase(24)]
    [TestCase(ushort.MaxValue)]
    public static void PointerToLocalBecomesAddressOnlyWithoutSideEffects(int offset)
    {
        WithCompiler("SequenceEqual", (compiler, store) =>
        {
            var pointer = store.VNForFuncNoFolding(TYP_BYREF, VNF_PtrToLoc,
                store.VNForIntCon(0), store.VNForIntCon(offset));
            var tree = compiler.gtNewLclvNode(TYP_BYREF, 0);
            tree._vnPair.SetBoth(pointer);
            var result = compiler.optVNBasedFoldConstExpr(new BasicBlock(null, null), null, tree);

            Assert.That(result?.Oper, Is.EqualTo(GT_LCL_ADDR));
            Assert.That(result?.AsLclFld().LclNum, Is.Zero);
            Assert.That(result?.AsLclFld().LclOffs, Is.EqualTo((ushort)offset));

            tree.Flags |= GTF_ASG;
            Assert.That(compiler.optVNBasedFoldConstExpr(new BasicBlock(null, null), null, tree), Is.Null);
        });
    }

#if FEATURE_SIMD
    [Test]
    public static void VectorConstantPreservesAllLaneBytes()
    {
        WithCompiler("SequenceEqual", (compiler, store) =>
        {
            simd16_t value = default;
            value.u64[0] = 0x1122334455667788UL;
            value.u64[1] = 0x99AABBCCDDEEFF00UL;
            compiler.lvaTable[0].Type = TYP_SIMD16;
            var tree = compiler.gtNewLclvNode(TYP_SIMD16, 0);
            tree._vnPair.SetBoth(store.VNForSimd16Con(value));
            var result = compiler.optVNBasedFoldConstExpr(new BasicBlock(null, null), null, tree);

            Assert.That(result?.AsVecCon().SimdVal.u64[0], Is.EqualTo(value.u64[0]));
            Assert.That(result?.AsVecCon().SimdVal.u64[1], Is.EqualTo(value.u64[1]));
        });
    }
#endif

#if FEATURE_MASKED_HW_INTRINSICS
    [Test]
    public static void MaskConstantPreservesRawBits()
    {
        WithCompiler("SequenceEqual", (compiler, store) =>
        {
            simdmask_t mask = default;
            mask.u64[0] = 0x8100000000000001UL;
            compiler.lvaTable[0].Type = TYP_MASK;
            var tree = compiler.gtNewLclvNode(TYP_MASK, 0);
            tree._vnPair.SetBoth(store.VNForSimdMaskCon(mask));
            var result = compiler.optVNBasedFoldConstExpr(new BasicBlock(null, null), null, tree);

            Assert.That(result?.AsMskCon().SimdMaskVal.RawBits, Is.EqualTo(mask.RawBits));
        });
    }
#endif

    [TestCase(false, GT_NE)]
    [TestCase(true, GT_EQ)]
    public static void JTrueConstantUsesRelopWithTwoFalseOperands(bool branchTaken, genTreeOps comparison)
    {
        WithCompiler("SequenceEqual", (compiler, store) =>
        {
            var left = compiler.gtNewLclvNode(TYP_INT, 0);
            var right = compiler.gtNewIconNode(TYP_INT, 0);
            var relop = compiler.gtNewBinaryNode(GT_EQ, TYP_INT, left, right);
            relop.Flags |= GTF_RELOP_JMP_USED;
            relop._vnPair.SetBoth(store.VNForIntCon(branchTaken ? 1 : 0));
            var test = new GenTreeUnOp(GT_JTRUE, TYP_VOID, relop);
            var block = new BasicBlock(null, null);

            Assert.That(compiler.optVNBasedFoldExpr(block, null, test), Is.SameAs(test));
            Assert.That(test.Op1.Oper, Is.EqualTo(comparison));
            Assert.That(test.Op1.AsOp().Op1.IsIntegralConst(0), Is.True);
            Assert.That(test.Op1.AsOp().Op2.IsIntegralConst(0), Is.True);
            Assert.That(block.FirstStmt, Is.Null);
        });
    }

    [Test]
    public static void JTrueMorphsExtractedEffectsBeforeBranchWithoutChangingTerminator()
    {
        WithCompiler("SequenceEqual", (compiler, store) =>
        {
            compiler.lvaTable[0].Type = TYP_INT;
            var effect = compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 7));
            var left = compiler.gtNewCommaNode(TYP_INT, effect, compiler.gtNewIconNode(TYP_INT, 7));
            var relop = compiler.gtNewBinaryNode(GT_EQ, TYP_INT, left, compiler.gtNewIconNode(TYP_INT, 7));
            relop.Flags |= GTF_RELOP_JMP_USED;
            relop._vnPair.SetBoth(store.VNForIntCon(1));
            var test = new GenTreeUnOp(GT_JTRUE, TYP_VOID, relop);
            var block = new BasicBlock(null, null);
            var trueBlock = new BasicBlock(null, null);
            var falseBlock = new BasicBlock(null, null);
            block.SetCond(new FlowEdge(block, trueBlock, null), new FlowEdge(block, falseBlock, null));
            var branch = compiler.gtNewStmt(test);
            compiler.fgInsertStmtAtEnd(block, branch);

            Assert.That(compiler.optVNConstantPropOnJTrue(block, test), Is.SameAs(test));
            Assert.That(block.FirstStmt, Is.Not.SameAs(branch));
            Assert.That(block.FirstStmt?.RootNode.Oper, Is.EqualTo(GT_COMMA));
            Assert.That(block.FirstStmt?.RootNode.AsOp().Op1.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
            Assert.That(block.LastStmt, Is.SameAs(branch));
            Assert.That(test.Op1.Oper, Is.EqualTo(GT_EQ));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public static void ConstantFoldPreservesRootWhenOperandExceptionsAreNotCovered(bool covered)
    {
        WithCompiler("SequenceEqual", (compiler, store) =>
        {
            var operandException = store.VNExcSetSingleton(
                store.VNForFunc(TYP_REF, VNF_OverflowExc, store.VNForIntCon(1)));
            var rootException = covered ? operandException : store.VNExcSetSingleton(
                store.VNForFunc(TYP_REF, VNF_DivideByZeroExc, store.VNForIntCon(0)));
            var dividend = compiler.gtNewIconNode(TYP_INT, 42);
            dividend._vnPair.SetBoth(store.VNWithExc(store.VNForIntCon(42), operandException));
            var divisor = compiler.gtNewIconNode(TYP_INT, 0);
            divisor._vnPair.SetBoth(store.VNForIntCon(0));
            var tree = compiler.gtNewBinaryNode(GT_DIV, TYP_INT, dividend, divisor);
            tree.Flags |= GTF_EXCEPT;
            tree._vnPair.SetBoth(store.VNWithExc(store.VNForIntCon(7), rootException));

            var result = compiler.optVNBasedFoldConstExpr(new BasicBlock(null, null), null, tree);
            if (covered)
            {
                Assert.That(result?.Oper, Is.EqualTo(GT_CNS_INT));
                Assert.That(result?.AsIntCon().IconValue, Is.EqualTo((nint)7));
            }
            else
            {
                Assert.That(result?.Oper, Is.EqualTo(GT_COMMA));
                Assert.That(result?.AsOp().Op1, Is.SameAs(tree));
                Assert.That(result?.AsOp().Op2.AsIntCon().IconValue, Is.EqualTo((nint)7));
            }
        });
    }

    private static int CountStores(GenTree? tree)
    {
        if (tree is null)
        {
            return 0;
        }
        if (tree.Oper is GT_COMMA)
        {
            return CountStores(tree.AsOp().Op1) + CountStores(tree.AsOp().Op2);
        }
        return tree.Oper is GT_STOREIND ? 1 : 0;
    }

    private static GenTreeCall NewCall(Compiler compiler, var_types type, GenTree first, GenTree second, GenTree third)
    {
        var call = new GenTreeCall(type)
        {
            _callType = gtCallTypes.CT_USER_FUNC,
            _callMethHnd = compiler.info.compMethodHnd,
            _callMoreFlags = GTF_CALL_M_SPECIAL_INTRINSIC,
        };
        _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(first));
        _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(second));
        _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(third));
        return call;
    }

    private static void WithCompiler(string method, Action<Compiler, ValueNumStore> action)
    {
        fixed (byte* namespaceName = "System\0"u8)
        fixed (byte* className = "SpanHelpers\0"u8)
        {
            var methodName = System.Text.Encoding.UTF8.GetBytes(method + '\0');
            fixed (byte* methodPointer = methodName)
            {
                var metadata = new MethodMetadata(namespaceName, className, methodPointer);
                ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
                vtable.Base.Base.getMethodNameFromMetadata = &GetMethodName;
                var ee = new ICorJitInfo { lpVtbl = &vtable };
#if DEBUG
                using var tls = new JitTls(&ee);
#endif
                var previous = JitTls.Compiler;
                var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
                JitFlags flags = default;
                compiler.opts.jitFlags = &flags;
                compiler.opts.SetMinOpts(false);
                compiler.lvaTable = [new LclVarDsc { Type = TYP_BYREF }, new LclVarDsc { Type = TYP_BYREF },
                    new LclVarDsc { Type = TYP_I_IMPL }];
                compiler.lvaCount = 3;
                compiler.info.compCompHnd = &ee;
                compiler.info.compMethodHnd = (CORINFO_METHOD_STRUCT_*)&metadata;
                compiler.vnStore = new ValueNumStore(compiler);
#if DEBUG
                compiler.info.compFullName = nameof(AssertionVNFoldingTests);
#endif
                JitTls.Compiler = compiler;
                try
                {
                    action(compiler, compiler.vnStore);
                }
                finally
                {
                    JitTls.Compiler = previous;
                }
            }
        }
    }

    private readonly struct MethodMetadata(byte* namespaceName, byte* className, byte* methodName)
    {
        public readonly byte* Namespace = namespaceName;
        public readonly byte* Class = className;
        public readonly byte* Method = methodName;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvMemberFunction)])]
    private static byte* GetMethodName(ICorJitInfo* self, CORINFO_METHOD_STRUCT_* method, byte** className,
        byte** namespaceName, byte** enclosingClasses, nint maxEnclosingClasses)
    {
        var metadata = (MethodMetadata*)method;
        *className = metadata->Class;
        *namespaceName = metadata->Namespace;
        return metadata->Method;
    }
}
