// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;
using static RyuJitSharp.VNFunc;
using BitOps = RyuJitSharp.BitSetOps<RyuJitSharp.BitVecTraits, RyuJitSharp.BitVecTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class TargetSignedSizeTests
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "compMaxUncheckedOffsetForNullObject")]
    private static extern ref int MaxUncheckedOffset(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_fieldHandleAndKind")]
    private static extern ref nint FieldHandleAndKind(FieldSeq fieldSeq);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_offset")]
    private static extern ref nint FieldOffset(FieldSeq fieldSeq);

    [TestCase(-1L)]
    [TestCase(int.MinValue)]
    [TestCase(int.MaxValue)]
    [TestCase(0x8000_0000L)]
    [TestCase(0x1_0000_0000L)]
    [TestCase(0x1_0000_0001L)]
    [TestCase(long.MinValue)]
    [TestCase(long.MaxValue)]
    public static void SignedSizeStoragePreservesTargetCastsAndConstantIdentity(long value)
    {
        var expected = Configuration.TARGET_64BIT ? value : unchecked((int)value);

        WithCompiler(compiler =>
        {
            var store = compiler.vnStore ?? throw new InvalidOperationException();
            var constant = store.VNForPtrSizeIntCon(value);
            var expectedVN = Configuration.TARGET_64BIT
                ? store.VNForLongCon(value) : store.VNForIntCon(unchecked((int)value));
            Assert.That(constant, Is.EqualTo(expectedVN));
            Assert.That(store.TypeOfVN(constant), Is.EqualTo(Configuration.TARGET_64BIT ? TYP_LONG : TYP_INT));
            Assert.That(store.GetConstantInt64(constant), Is.EqualTo(expected));
        });
    }

    [TestCase(int.MaxValue, 1L)]
    [TestCase(int.MinValue, -1L)]
    [TestCase(long.MaxValue, 1L)]
    [TestCase(long.MinValue, -1L)]
    [TestCase(0x1_0000_0000L, 16L)]
    public static void ValueNumberOffsetsWrapAtTargetWidth(long first, long second)
    {
        WithCompiler(compiler =>
        {
            var store = compiler.vnStore ?? throw new InvalidOperationException();
            var baseVN = store.VNForExpr(null, TYP_BYREF);
            var inner = store.VNForFuncNoFolding(TYP_BYREF, VNF_ADD, store.VNForLongCon(first), baseVN);
            var address = store.VNForFuncNoFolding(TYP_BYREF, VNF_ADD, inner, store.VNForLongCon(second));
            var cast = store.VNForFuncNoFolding(TYP_I_IMPL, VNF_Cast, address,
                store.VNForCastOper(TYP_I_IMPL, false));

            store.PeelOffsets(ref cast, out var offset);

            var expected = Configuration.TARGET_64BIT
                ? unchecked(first + second) : unchecked((int)first + (int)second);
            Assert.That(cast, Is.EqualTo(baseVN));
            Assert.That(offset, Is.EqualTo(expected));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ValueNumberPeelingRetainsHandlesAndNullBases(bool handle)
    {
        WithCompiler(compiler =>
        {
            var store = compiler.vnStore ?? throw new InvalidOperationException();
            var baseVN = handle ? store.VNForHandle(123, GTF_ICON_OBJ_HDL) : ValueNumStore.VNForNull();
            var address = store.VNForFuncNoFolding(TYP_BYREF, VNF_ADD,
                store.VNForPtrSizeIntCon(8), baseVN);

            store.PeelOffsets(ref address, out var offset);

            Assert.That(address, Is.EqualTo(baseVN));
            Assert.That(offset, Is.EqualTo(8));
        });
    }

    [TestCase(0, int.MaxValue, 1)]
    [TestCase(1, int.MaxValue, 1)]
    [TestCase(2, int.MaxValue, 1)]
    [TestCase(0, int.MinValue, -1)]
    [TestCase(1, int.MinValue, -1)]
    [TestCase(2, int.MinValue, -1)]
    [TestCase(0, long.MaxValue, 1)]
    [TestCase(1, long.MaxValue, 1)]
    [TestCase(2, long.MaxValue, 1)]
    [TestCase(0, 0x1_0000_0000L, 16)]
    public static void TreeOffsetsPreserveTargetWidthAndBuildOverflowPolicy(int shape, long first, int second)
    {
        WithCompiler(compiler =>
        {
            var baseAddress = compiler.gtNewLclvNode(TYP_BYREF, 0);
            var inner = compiler.gtNewBinaryNode(GT_ADD, TYP_BYREF, baseAddress,
                compiler.gtNewIconNode(TYP_I_IMPL, unchecked((nint)first)));
            GenTree address = shape switch {
                0 => compiler.gtNewBinaryNode(GT_ADD, TYP_BYREF, inner, compiler.gtNewIconNode(TYP_I_IMPL, second)),
                1 => compiler.gtNewBinaryNode(GT_ADD, TYP_BYREF, compiler.gtNewIconNode(TYP_I_IMPL, second), inner),
                _ => new GenTreeAddrMode(TYP_BYREF, inner, null, 0, second),
            };

#if DEBUG
            var overflow = Configuration.TARGET_64BIT
                ? !CheckedOps.TryAdd(first, (long)second, out _)
                : !CheckedOps.TryAdd(unchecked((int)first), second, out _);
            if (overflow)
            {
                Assert.Throws<OverflowException>(() => compiler.gtPeelOffsets(ref address, out _));
                return;
            }
#endif
            compiler.gtPeelOffsets(ref address, out var offset, out var fields);

            var expected = Configuration.TARGET_64BIT
                ? unchecked(first + second) : unchecked((int)first + second);
            Assert.That(address, Is.SameAs(baseAddress));
            Assert.That(offset, Is.EqualTo(expected));
            Assert.That(fields, Is.Null);
        });
    }

    [TestCase(int.MaxValue, 1L, false, false, false, true)]
    [TestCase(int.MaxValue, 0L, false, false, false, true)]
    [TestCase(int.MinValue, -1L, false, false, false, true)]
    [TestCase(0x1_0000_0000L, 0L, true, false, false, true)]
    [TestCase(long.MaxValue, 1L, true, false, false, false)]
    [TestCase(-1L, 1L, true, false, true, false)]
    public static void FieldMarkingRejectsTargetSignedOverflowWithoutReordering(
        long first, long second, bool elided32, bool ordered32, bool elided64, bool ordered64)
    {
        WithCompiler(compiler =>
        {
            var field = new GenTreeFieldAddr(TYP_BYREF, compiler.gtNewLclvNode(TYP_REF, 0),
                (CORINFO_FIELD_STRUCT_*)4, 8);
            var inner = compiler.gtNewBinaryNode(GT_ADD, TYP_BYREF, field,
                compiler.gtNewIconNode(TYP_I_IMPL, unchecked((nint)first)));
            var address = compiler.gtNewBinaryNode(GT_ADD, TYP_BYREF, inner,
                compiler.gtNewIconNode(TYP_I_IMPL, unchecked((nint)second)));
            var indirection = compiler.gtNewIndir(TYP_INT, address, GTF_IND_NONFAULTING);

            compiler.fgMarkAddrModeForFieldAddr(indirection);

            var elided = Configuration.TARGET_64BIT ? elided64 : elided32;
            Assert.That((field.Flags & GTF_FLD_TGT_NONFAULTING) != 0, Is.EqualTo(elided));
            Assert.That((indirection.Flags & GTF_IND_NONFAULTING) == 0, Is.EqualTo(elided));
            Assert.That(indirection.HasOrderingSideEffect,
                Is.EqualTo(Configuration.TARGET_64BIT ? ordered64 : ordered32));
            Assert.That(indirection.Addr, Is.SameAs(address));
        });
    }

    [TestCase(int.MinValue, -2147483640L)]
    [TestCase(-1, -4294967287L)]
    public static void FieldOffsetsAreUnsignedOnlyOn64BitTargets(int fieldOffset, long displacement)
    {
        WithCompiler(compiler =>
        {
            var field = new GenTreeFieldAddr(TYP_BYREF, compiler.gtNewLclvNode(TYP_REF, 0),
                (CORINFO_FIELD_STRUCT_*)4, fieldOffset);
            var address = compiler.gtNewBinaryNode(GT_ADD, TYP_BYREF, field,
                compiler.gtNewIconNode(TYP_I_IMPL, unchecked((nint)displacement)));
            var indirection = compiler.gtNewIndir(TYP_INT, address, GTF_IND_NONFAULTING);

            compiler.fgMarkAddrModeForFieldAddr(indirection);

            Assert.That((field.Flags & GTF_FLD_TGT_NONFAULTING) != 0, Is.EqualTo(Configuration.TARGET_64BIT));
            Assert.That(indirection.HasOrderingSideEffect, Is.False);
        });
    }

    [TestCase(0, int.MaxValue, 1L)]
    [TestCase(0, int.MinValue, -2147483632L)]
    [TestCase(0, 0x1_0000_0004L, 0L)]
    [TestCase(0, long.MaxValue, 1L)]
    [TestCase(1, 65536L, 65536L)]
    [TestCase(1, int.MinValue, -1L)]
    [TestCase(1, long.MinValue, -1L)]
    [TestCase(2, 1L, 31L)]
    [TestCase(2, 1L, 32L)]
    [TestCase(2, 1L, 63L)]
    [TestCase(2, 1L, 64L)]
    [TestCase(3, 0L, int.MinValue)]
    [TestCase(4, 1L, int.MinValue)]
    public static void ArrayConstantTermsWrapBeforeRangeChecks(int shape, long first, long second)
    {
        WithCompiler(compiler =>
        {
            var store = compiler.vnStore ?? throw new InvalidOperationException();
            var array = compiler.gtNewLclvNode(TYP_REF, 0);
            var left = compiler.gtNewIconNode(TYP_I_IMPL, unchecked((nint)first));
            var right = compiler.gtNewIconNode(TYP_I_IMPL, unchecked((nint)second));
            var term = shape switch {
                0 => compiler.gtNewBinaryNode(GT_ADD, TYP_I_IMPL, left, right),
                1 => compiler.gtNewBinaryNode(GT_MUL, TYP_I_IMPL, left, right),
                2 => compiler.gtNewBinaryNode(GT_LSH, TYP_I_IMPL, left, right),
                3 => compiler.gtNewBinaryNode(GT_SUB, TYP_I_IMPL, left, right),
                _ => compiler.gtNewBinaryNode(GT_MUL, TYP_I_IMPL,
                    compiler.gtNewBinaryNode(GT_SUB, TYP_I_IMPL, compiler.gtNewIconNode(TYP_I_IMPL, 0), left), right),
            };
            var address = compiler.gtNewBinaryNode(GT_ADD, TYP_BYREF, array,
                compiler.gtNewBinaryNode(GT_ADD, TYP_I_IMPL, compiler.gtNewIconNode(TYP_I_IMPL, 16), term));
            var wrapper = new GenTreeArrAddr(address, TYP_UBYTE, null, 16);
            var sentinel = store.VNForIntCon(1234);
            var result = sentinel;

            wrapper.ParseArrayAddress(compiler, out var parsedArray, ref result);

            var contribution = Configuration.TARGET_64BIT ? shape switch {
                0 => unchecked(first + second),
                1 => unchecked(first * second),
                2 => first << unchecked((int)second),
                3 => unchecked(first - second),
                _ => unchecked(-first * second),
            } : shape switch {
                0 => unchecked((int)first + (int)second),
                1 => unchecked((int)first * (int)second),
                2 => unchecked((int)first) << unchecked((int)second),
                3 => unchecked((int)first - (int)second),
                _ => unchecked(-(int)first * (int)second),
            };
            var offset = Configuration.TARGET_64BIT
                ? unchecked(16 + contribution) : unchecked(16 + (int)contribution);
            if (offset < 16)
            {
                Assert.That(parsedArray, Is.Null);
                Assert.That(result, Is.EqualTo(sentinel));
            }
            else
            {
                Assert.That(parsedArray, Is.SameAs(array));
                Assert.That(result, Is.EqualTo(store.VNForPtrSizeIntCon(offset - 16)));
            }
        });
    }

    [TestCase(0x1_0000_0004L, TYP_INT)]
    [TestCase(-2147483648L, TYP_INT)]
    [TestCase(long.MaxValue - 3, TYP_INT)]
    [TestCase(int.MaxValue, TYP_UBYTE)]
    [TestCase(long.MaxValue, TYP_UBYTE)]
    public static void ArrayConstantVNsCoerceBeforeDivisionAndIndexAddition(long value, var_types elementType)
    {
        WithCompiler(compiler =>
        {
            var store = compiler.vnStore ?? throw new InvalidOperationException();
            var array = compiler.gtNewLclvNode(TYP_REF, 0);
            var term = compiler.gtNewUnaryNode(GT_NEG, TYP_I_IMPL, compiler.gtNewLclvNode(TYP_I_IMPL, 1));
            term._vnPair.SetBoth(store.VNForLongCon(value));
            var elementSize = (int)elementType.Size;
            var address = compiler.gtNewBinaryNode(GT_ADD, TYP_BYREF, array,
                compiler.gtNewBinaryNode(GT_ADD, TYP_I_IMPL, term,
                    compiler.gtNewIconNode(TYP_I_IMPL, 16 + elementSize)));
            var wrapper = new GenTreeArrAddr(address, elementType, null, 16);
            var result = ValueNumStore.NoVN;

            wrapper.ParseArrayAddress(compiler, out var parsedArray, ref result);

            var expected = Configuration.TARGET_64BIT
                ? unchecked((value / elementSize) + 1) : unchecked(((int)value / elementSize) + 1);
            Assert.That(parsedArray, Is.SameAs(array));
            Assert.That(result, Is.EqualTo(store.VNForPtrSizeIntCon(expected)));
        });
    }

    [TestCase(0x1_0000_0001L, -1L, false)]
    [TestCase(65536L, 65537L, false)]
    [TestCase(0x8000_0000L, 1L, true)]
    public static void ArrayVariableMultipliersRetainLongValueNumberIdentity(long inner, long outer, bool negate)
    {
        WithCompiler(compiler =>
        {
            var store = compiler.vnStore ?? throw new InvalidOperationException();
            var array = compiler.gtNewLclvNode(TYP_REF, 0);
            var index = compiler.gtNewLclvNode(TYP_I_IMPL, 1);
            var indexVN = store.VNForExpr(null, TYP_I_IMPL);
            index._vnPair.SetBoth(indexVN);
            GenTree term = compiler.gtNewBinaryNode(GT_MUL, TYP_I_IMPL, index,
                compiler.gtNewIconNode(TYP_I_IMPL, unchecked((nint)inner)));
            if (negate)
            {
                term = compiler.gtNewBinaryNode(GT_SUB, TYP_I_IMPL, compiler.gtNewIconNode(TYP_I_IMPL, 0), term);
            }
            term = compiler.gtNewBinaryNode(GT_MUL, TYP_I_IMPL, term,
                compiler.gtNewIconNode(TYP_I_IMPL, unchecked((nint)outer)));
            var address = compiler.gtNewBinaryNode(GT_ADD, TYP_BYREF, array,
                compiler.gtNewBinaryNode(GT_ADD, TYP_I_IMPL, term, compiler.gtNewIconNode(TYP_I_IMPL, 16)));
            var wrapper = new GenTreeArrAddr(address, TYP_UBYTE, null, 16);
            var result = ValueNumStore.NoVN;

            wrapper.ParseArrayAddress(compiler, out var parsedArray, ref result);

            var multiplier = Configuration.TARGET_64BIT
                ? unchecked(inner * outer) : unchecked((int)inner * (int)outer);
            if (negate)
            {
                multiplier = Configuration.TARGET_64BIT ? unchecked(-multiplier) : unchecked(-(int)multiplier);
            }
            var scaled = store.VNForFunc(TYP_I_IMPL, VNF_MUL, store.VNForLongCon(multiplier), indexVN);
            var expected = store.VNForFunc(TYP_I_IMPL, VNF_DIV, scaled, store.VNForPtrSizeIntCon(1));
            Assert.That(parsedArray, Is.SameAs(array));
            Assert.That(result, Is.EqualTo(expected));
        });
    }

    [TestCase(int.MaxValue, 8, false)]
    [TestCase(long.MaxValue, 8, false)]
    [TestCase(0xffff_fff8L, 8, false)]
    [TestCase(0xffff_fff8L, 8, true)]
    [TestCase(0x1_0000_0004L, 0, false)]
    public static void PhysicalPromotionOffsetsWrapBeforeZeroTestsAndRetainFields(long baseOffset, int offset, bool hasFields)
    {
        WithCompiler(compiler =>
        {
            var baseAddress = compiler.gtNewLclvNode(TYP_BYREF, 0);
            var fields = hasFields ? CreateFieldSeq() : null;
            var access = new Compiler.PhysicalPromotionLocationAccess();
            access.InitializeIndir(baseAddress, baseOffset, fields, GTF_EMPTY, 1);

            var address = access.GrabAddress(offset, compiler);

            var expected = Configuration.TARGET_64BIT
                ? unchecked(baseOffset + offset) : unchecked((int)baseOffset + offset);
            if ((expected == 0) && !hasFields)
            {
                Assert.That(address, Is.SameAs(baseAddress));
            }
            else
            {
                Assert.That(address.Oper, Is.EqualTo(GT_ADD));
                Assert.That(address.AsOp().Op1, Is.SameAs(baseAddress));
                Assert.That(address.AsOp().Op2.AsIntCon().IconValue, Is.EqualTo(unchecked((nint)expected)));
                Assert.That(address.AsOp().Op2.AsIntCon().FieldSeq, Is.SameAs(fields));
            }
            access.CheckFullyUsed();
        });
    }

    [TestCase(int.MaxValue, false)]
    [TestCase(int.MaxValue, true)]
    [TestCase(long.MaxValue, false)]
    [TestCase(long.MaxValue, true)]
    public static void PromotedCopiesWrapFieldAddressesAtTargetWidth(long baseOffset, bool promotedDestination)
    {
        WithCompiler(compiler =>
        {
            var layout = new ClassLayout(8);
            ref var descriptor = ref compiler.lvaTable[0];
            descriptor.Type = TYP_STRUCT;
            descriptor.Layout = layout;
            descriptor.lvPromoted = true;
            descriptor.lvFieldCnt = 2;
            descriptor.lvFieldLclStart = 1;
            for (var index = 1; index <= 2; index++)
            {
                ref var field = ref compiler.lvaTable[index];
                field.Type = TYP_INT;
                field.lvIsStructField = true;
                field.lvParentLcl = 0;
                field.lvFldOffset = (byte)((index - 1) * 4);
            }
            compiler.lvaTable[3].Type = TYP_BYREF;
            compiler.fgGlobalMorph = true;
            var baseAddress = compiler.gtNewLclvNode(TYP_BYREF, 3);
            var address = compiler.gtNewBinaryNode(GT_ADD, TYP_BYREF, baseAddress,
                compiler.gtNewIconNode(TYP_I_IMPL, unchecked((nint)baseOffset)));
            GenTree store = promotedDestination
                ? compiler.gtNewStoreLclVarNode(0, compiler.gtNewBlkIndir(address, layout))
                : compiler.gtNewStoreBlkNode(address, compiler.gtNewLclvNode(TYP_STRUCT, 0), layout);

            var result = compiler.fgMorphCopyBlock(store);

            Assert.That(result.Oper, Is.EqualTo(GT_COMMA));
            GenTree[] stores = [result.AsOp().Op1, result.AsOp().Op2];
            for (var index = 0; index < stores.Length; index++)
            {
                var indirection = promotedDestination ? stores[index].Data.AsIndir() : stores[index].AsIndir();
                var expected = Configuration.TARGET_64BIT
                    ? unchecked(baseOffset + (index * 4)) : unchecked((int)baseOffset + (index * 4));
                Assert.That(indirection.Addr.Oper, Is.EqualTo(GT_ADD));
                Assert.That(indirection.Addr.AsOp().Op2.AsIntCon().IconValue, Is.EqualTo(unchecked((nint)expected)));
            }
        });
    }

    private static void WithCompiler(Action<Compiler> action)
    {
        using var tls = TestConfiguration.CreateJitTls();
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.lvaCount = 4;
        compiler.lvaTable = new LclVarDsc[compiler.lvaCount];
        compiler.lvaRefCountState = RefCountState.RCS_EARLY;
        compiler.info.compRetBuffArg = BAD_VAR_NUM;
        TestConfiguration.SetCompilerFullName(compiler, nameof(TargetSignedSizeTests));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        JitTls.Compiler = compiler;
        try
        {
            MaxUncheckedOffset(compiler) = 4095;
            compiler.optAssertionInit(isLocalProp: true);
            var traits = compiler.apTraits ?? throw new InvalidOperationException("Missing assertion traits.");
            compiler.apLocal = BitOps.MakeEmpty(traits);
            compiler.apLocalPostorder = BitOps.MakeEmpty(traits);
            compiler.vnStore = new ValueNumStore(compiler);
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    private static FieldSeq CreateFieldSeq()
    {
        var fields = (FieldSeq)RuntimeHelpers.GetUninitializedObject(typeof(FieldSeq));
        FieldHandleAndKind(fields) = 4;
        FieldOffset(fields) = 0;
        return fields;
    }
}
