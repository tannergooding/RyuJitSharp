// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.RefCountState;
using static RyuJitSharp.var_types;
using BitOps = RyuJitSharp.BitSetOps<RyuJitSharp.BitVecTraits, RyuJitSharp.BitVecTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class BlockMorphTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void InitValueClassificationUsesTheUnaryOperand(bool constant)
    {
        WithCompiler(compiler => {
            GenTree value = constant
                ? compiler.gtNewIconNode(TYP_INT, 17)
                : compiler.gtNewLclvNode(TYP_INT, 0);
            var init = new GenTreeUnOp(GT_INIT_VAL, TYP_INT, value);

            Assert.That(value.IsCnsInitVal, Is.EqualTo(constant));
            Assert.That(init.IsCnsInitVal, Is.EqualTo(constant));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ValueRetypingCanPreserveSimpleAndCompositeSsaIdentity(bool composite)
    {
        WithCompiler(compiler => {
            SetPromotedDestination(compiler);
            var original = compiler.gtNewLclvNode(TYP_STRUCT, 0);
            if (composite)
            {
                original.SetSsaNum(compiler, 0, 7);
                original.SetSsaNum(compiler, 1, 9);
            }
            else
            {
                original.SsaNum = 7;
            }

            var replacement = new GenTreeLclFld(GT_LCL_FLD, TYP_LONG, 0, 0, null, null, original, NodeThreading.None);
            Assert.That(replacement.HasSsaIdentity, Is.False);
            replacement.CopySsaIdentityFrom(original);
            Assert.That(replacement.HasSsaIdentity, Is.True);
            Assert.That(replacement.HasCompositeSsaName, Is.EqualTo(composite));
            if (composite)
            {
                Assert.That(replacement.GetSsaNum(compiler, 0), Is.EqualTo(7));
                Assert.That(replacement.GetSsaNum(compiler, 1), Is.EqualTo(9));
            }
            else
            {
                Assert.That(replacement.SsaNum, Is.EqualTo(7));
            }
        });
    }

    [Test]
    public static void PrimitiveStoreReplacementRetainsItsManagedKindAndLogicalIdentity()
    {
        WithCompiler(compiler => {
            var address = compiler.gtNewLclvNode(TYP_BYREF, 1);
            var value = compiler.gtNewLclvNode(TYP_LONG, 2);
            compiler.lvaTable[2].Type = TYP_LONG;
            var layout = new ClassLayout(8);
            var blockValue = compiler.gtNewLclFldNode(TYP_STRUCT, 2, 0, layout);
            var original = compiler.gtNewStoreBlkNode(address, blockValue, layout);
            original.Flags |= GTF_IND_VOLATILE | GTF_IND_UNALIGNED | GTF_IND_TGT_NOT_HEAP;
            original._vnPair.SetBoth(42);
#if DEBUG
            var nextId = compiler.compGenTreeID;
#endif
            var replacement = new GenTreeStoreInd(TYP_LONG, address, value, original, NodeThreading.None) {
                Flags = original.Flags,
            };
            Assert.That(replacement.AsStoreInd(), Is.SameAs(replacement));
            Assert.That(replacement.Addr, Is.SameAs(address));
            Assert.That(replacement.Data, Is.SameAs(value));
            Assert.That(replacement.Flags, Is.EqualTo(original.Flags));
            Assert.That(replacement._vnPair.Liberal, Is.EqualTo(ValueNumStore.NoVN));
            Assert.That(original.Oper, Is.EqualTo(GT_STORE_BLK));
            Assert.That(original.Data, Is.SameAs(blockValue));
            Assert.That(original._vnPair.Liberal, Is.EqualTo(42));
#if DEBUG
            Assert.That(replacement.Oper.StructType, Is.EqualTo(replacement.GetType()));
            Assert.That(replacement.TreeId, Is.EqualTo(original.TreeId));
            Assert.That(compiler.compGenTreeID, Is.EqualTo(nextId));
#endif
        });
    }

    [TestCase(TYP_BYTE, 0x80, -128L)]
    [TestCase(TYP_UBYTE, 0x80, 128L)]
    [TestCase(TYP_SHORT, 0x80, -32640L)]
    [TestCase(TYP_USHORT, 0x80, 32896L)]
    [TestCase(TYP_INT, 0xFF, -1L)]
    [TestCase(TYP_LONG, 0x80, unchecked((long)0x8080808080808080))]
    [TestCase(TYP_REF, 0, 0L)]
    [TestCase(TYP_BYREF, 0, 0L)]
    public static void InitPatternsPreserveWidthAndSignedness(var_types type, byte pattern, long expected)
    {
        WithCompiler(compiler => {
            var node = compiler.gtNewConWithPattern(type, pattern);
            Assert.That(node.Type, Is.EqualTo(type.ActualType));
            Assert.That(node.AsIntConCommon().IntegralValue, Is.EqualTo(expected));
        });
    }

    [TestCase(TYP_FLOAT, 0)]
    [TestCase(TYP_FLOAT, 0x2A)]
    [TestCase(TYP_FLOAT, 0x80)]
    [TestCase(TYP_FLOAT, 0xFF)]
    [TestCase(TYP_DOUBLE, 0)]
    [TestCase(TYP_DOUBLE, 0x2A)]
    [TestCase(TYP_DOUBLE, 0x80)]
    [TestCase(TYP_DOUBLE, 0xFF)]
    public static void FloatingInitPatternsReinterpretRatherThanConvert(var_types type, byte pattern)
    {
        WithCompiler(compiler => {
            var node = compiler.gtNewConWithPattern(type, pattern).AsDblCon();
            if (type is TYP_FLOAT)
            {
                Assert.That(BitConverter.SingleToInt32Bits((float)node.DconVal),
                    Is.EqualTo(unchecked(pattern * 0x01010101)));
            }
            else
            {
                Assert.That(BitConverter.DoubleToInt64Bits(node.DconVal),
                    Is.EqualTo(unchecked(pattern * 0x0101010101010101L)));
            }
        });
    }

    [TestCase(TYP_SIMD8)]
    [TestCase(TYP_SIMD12)]
    [TestCase(TYP_SIMD16)]
    [TestCase(TYP_SIMD32)]
    [TestCase(TYP_SIMD64)]
    public static void VectorInitPatternsFillAllConstantStorage(var_types type)
    {
        WithCompiler(compiler => {
            var node = compiler.gtNewConWithPattern(type, 0xA5).AsVecCon();
            Assert.That(node.SimdVal.AsSpan<byte>().ToArray(), Is.All.EqualTo((byte)0xA5));
        });
    }

    [TestCase(TYP_BYTE, false)]
    [TestCase(TYP_BYTE, true)]
    [TestCase(TYP_INT, false)]
    [TestCase(TYP_LONG, false)]
    [TestCase(TYP_REF, false)]
    [TestCase(TYP_BYREF, false)]
    [TestCase(TYP_FLOAT, false)]
    [TestCase(TYP_DOUBLE, false)]
    [TestCase(TYP_SIMD16, false)]
    public static void PrimitiveZeroInitReplacesOwnersAndPreservesLogicalIdentity(var_types type, bool parameter)
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = type;
            compiler.lvaTable[0].lvIsParam = parameter;
            var zero = compiler.gtNewIconNode(TYP_INT, 0);
            zero._vnPair.SetBoth(42);
            var original = new GenTreeLclFld(TYP_STRUCT, 0, 0, zero, new ClassLayout(type.Size)) {
                Flags = GTF_ASG | GTF_VAR_DEF,
            };
            original._vnPair.SetBoth(43);
            var statement = compiler.gtNewStmt(original);
#if DEBUG
            var nextId = compiler.compGenTreeID;
#endif
            statement.RootNode = compiler.fgMorphInitBlock(original);
            var store = statement.RootNode.AsLclVar();
            Assert.That(store.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
            Assert.That(store.Type, Is.EqualTo(parameter ? type : type.ActualType));
            Assert.That(store.LclNum, Is.Zero);
            Assert.That(store._vnPair.Liberal, Is.EqualTo(ValueNumStore.NoVN));
            Assert.That(store.Flags & (GTF_ASG | GTF_VAR_DEF), Is.EqualTo(GTF_ASG | GTF_VAR_DEF));
            Assert.That(store.Data, Is.Not.SameAs(zero));

            if (type is TYP_SIMD16)
            {
                Assert.That(store.Data.AsVecCon().IsZero, Is.True);
            }
            else
            {
                Assert.That(store.Data.Type, Is.EqualTo(type.ActualType));
                Assert.That(store.Data._vnPair.Liberal, Is.EqualTo(ValueNumStore.NoVN));
                if (type is TYP_FLOAT or TYP_DOUBLE)
                {
                    Assert.That(store.Data.AsDblCon().IsPositiveZero, Is.True);
                }
                else
                {
                    Assert.That(store.Data.AsIntConCommon().IntegralValue, Is.Zero);
                }
#if DEBUG
                Assert.That(store.Data.TreeId, Is.EqualTo(zero.TreeId));
#endif
            }
#if DEBUG
            Assert.That(store.TreeId, Is.EqualTo(original.TreeId));
            Assert.That(compiler.compGenTreeID, Is.EqualTo(nextId + (type is TYP_SIMD16 ? 1 : 0)));
#endif
        });
    }

    [TestCase(0, 0)]
    [TestCase(0x2A, 0)]
    [TestCase(0x80, 0)]
    [TestCase(0xFF, 0)]
    [TestCase(0, 1)]
    [TestCase(0, 3)]
    public static void PromotedInitExpandsLiveFieldsInOrder(byte pattern, int deadFields)
    {
        WithCompiler(compiler => {
            SetPromotedDestination(compiler);
            var value = compiler.gtNewIconNode(TYP_INT, pattern);
            var store = compiler.gtNewStoreLclVarNode(0,
                pattern == 0 ? value : compiler.gtNewUnaryNode(GT_INIT_VAL, TYP_INT, value));
            store.SetLastUse(0, (deadFields & 1) != 0);
            store.SetLastUse(1, (deadFields & 2) != 0);
            var result = compiler.fgMorphInitBlock(store);

            if (deadFields == 3)
            {
                Assert.That(result.Oper, Is.EqualTo(GT_NOP));
            }
            else
            {
                GenTree[] stores = deadFields == 0 ? [result.AsOp().Op1, result.AsOp().Op2] : [result];
                for (var i = 0; i < stores.Length; i++)
                {
                    Assert.That(stores[i].Oper, Is.EqualTo(GT_STORE_LCL_VAR));
                    Assert.That(stores[i].AsLclVar().LclNum, Is.EqualTo(i + (deadFields == 0 ? 1 : 2)));
                    Assert.That(stores[i].Data.AsIntConCommon().IntegralValue,
                        Is.EqualTo(unchecked(pattern * 0x0101010101010101L)));
                }
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void NonZeroGcInitAndExposedHolesRemainBlockStores(bool exposedHoles)
    {
        WithCompiler(compiler => {
            SetPromotedDestination(compiler);
            compiler.lvaTable[0].lvContainsHoles = exposedHoles;
            compiler.lvaTable[0].SetAddressExposed(exposedHoles, AddressExposedReason.ESCAPE_ADDRESS);
            compiler.lvaTable[1].Type = exposedHoles ? TYP_LONG : TYP_REF;
            var store = compiler.gtNewStoreLclVarNode(0,
                compiler.gtNewUnaryNode(GT_INIT_VAL, TYP_INT, compiler.gtNewIconNode(TYP_INT, 1)));

            Assert.That(compiler.fgMorphInitBlock(store), Is.SameAs(store));
            Assert.That(compiler.lvaTable[0].lvDoNotEnregister, Is.True);
        });
    }

    [TestCase((ushort)0, 8)]
    [TestCase((ushort)8, 8)]
    public static void PartialPromotedInitRemainsAFieldStore(ushort offset, int size)
    {
        WithCompiler(compiler => {
            SetPromotedDestination(compiler);
            var store = new GenTreeLclFld(TYP_STRUCT, 0, offset,
                compiler.gtNewIconNode(TYP_INT, 0), new ClassLayout(size)) {
                Flags = GTF_ASG | GTF_VAR_DEF | GTF_VAR_USEASG,
            };

            Assert.That(compiler.fgMorphInitBlock(store), Is.SameAs(store));
            Assert.That(compiler.lvaTable[0].lvDoNotEnregister, Is.True);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ComputedInitValuePreservesAddressAndValueOrder(bool reverse)
    {
        WithCompiler(compiler => {
            var address = compiler.gtNewCallNode(TYP_BYREF, gtCallTypes.CT_USER_FUNC, null);
            var effect = compiler.gtNewCallNode(TYP_VOID, gtCallTypes.CT_USER_FUNC, null);
            var zero = compiler.gtNewIconNode(TYP_INT, 0);
            var comma = compiler.gtNewCommaNode(TYP_INT, effect, zero);
            var value = compiler.gtNewUnaryNode(GT_INIT_VAL, TYP_INT, comma);
            var store = compiler.gtNewStoreBlkNode(address, value, new ClassLayout(16));
            store.IsReverseOp = reverse;

            var result = compiler.fgMorphInitBlock(store);

            Assert.That(result, Is.SameAs(store));
            Assert.That(store.AsIndir().Addr, Is.SameAs(address));
            Assert.That(store.Data, Is.SameAs(value));
            Assert.That(value.AsUnOp().Op1, Is.SameAs(comma));
            Assert.That(comma.AsOp().Op1, Is.SameAs(effect));
            Assert.That(comma.AsOp().Op2, Is.SameAs(zero));
            Assert.That(store.IsReverseOp, Is.EqualTo(reverse));
            Assert.That(store.Flags & GTF_CALL, Is.Not.Zero);
            Assert.That(compiler.lvaCount, Is.EqualTo(3));
        });
    }

    [TestCase(false, 0x8000_0000u, false)]
    [TestCase(false, 0xffff_fff0u, true)]
    [TestCase(true, 0x8000_0000u, false)]
    [TestCase(true, 0xffff_fff0u, true)]
    public static void LargeUnsignedBlockMorphKeepsFullLayoutAndBlockStore(bool copy, uint size, bool minOpts)
    {
        WithCompiler(compiler => {
            var layout = new ClassLayout(size);
            var destination = compiler.gtNewLclvNode(TYP_BYREF, 1);
            GenTree source;
            if (copy)
            {
                var sourceAddress = compiler.gtNewLclvNode(TYP_BYREF, 2);
                source = new GenTreeBlk(TYP_STRUCT, sourceAddress, layout);
            }
            else
            {
                source = compiler.gtNewIconNode(TYP_INT, 0);
            }
            var store = compiler.gtNewStoreBlkNode(destination, source, layout);
            var result = copy ? compiler.fgMorphCopyBlock(store) : compiler.fgMorphInitBlock(store);

            Assert.That(result, Is.SameAs(store));
            Assert.That(store.Layout.Size, Is.EqualTo(size));
            Assert.That(store.Data, Is.SameAs(source));
            Assert.That(store.Size, Is.EqualTo(size));
        }, minOpts);
    }

    private static void SetPromotedDestination(Compiler compiler)
    {
        ref var destination = ref compiler.lvaTable[0];
        destination.Type = TYP_STRUCT;
        destination.Layout = new ClassLayout(16);
        destination.lvPromoted = true;
        destination.lvFieldCnt = 2;
        destination.lvFieldLclStart = 1;

        for (var i = 1; i < 3; i++)
        {
            ref var field = ref compiler.lvaTable[i];
            field.Type = TYP_LONG;
            field.lvIsStructField = true;
            field.lvParentLcl = 0;
            field.lvFldOffset = (byte)((i - 1) * 8);
        }
    }

    private static void WithCompiler(Action<Compiler> action, bool minOpts = false)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(minOpts);
        compiler.lvaTable = new LclVarDsc[3];
        compiler.lvaCount = 3;
        compiler.lvaRefCountState = RCS_EARLY;
        compiler.fgGlobalMorph = true;
#if DEBUG
        compiler.info.compFullName = nameof(BlockMorphTests);
#endif
        JitTls.Compiler = compiler;

        try
        {
            compiler.optAssertionInit(isLocalProp: true);
            var traits = compiler.apTraits ?? throw new InvalidOperationException("Missing assertion traits.");
            compiler.apLocal = BitOps.MakeEmpty(traits);
            compiler.apLocalPostorder = BitOps.MakeEmpty(traits);
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
