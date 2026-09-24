// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
    [TestCase(0u, 1, true)]
    [TestCase(4u, 5, true)]
    [TestCase(5u, 5, false)]
    public static void TreeComplexityStopsOnlyAfterExceedingTheLimit(uint limit, int visited, bool exceeds)
    {
        WithCompiler(compiler => {
            var first = compiler.gtNewIconNode(TYP_INT, 1);
            var second = compiler.gtNewIconNode(TYP_INT, 2);
            var third = compiler.gtNewIconNode(TYP_INT, 3);
            var left = compiler.gtNewBinaryNode(GT_ADD, TYP_INT, first, second);
            var root = compiler.gtNewBinaryNode(GT_ADD, TYP_INT, left, third);
            List<GenTree> visits = [];

            Assert.That(compiler.gtComplexityExceeds(root, limit, node => {
                visits.Add(node);
                return 1;
            }), Is.EqualTo(exceeds));
            GenTree[] order = [root, left, first, second, third];
            Assert.That(visits, Is.EqualTo(order[..visited]));
            Assert.That(root.Op1, Is.SameAs(left));
        });
    }

    [TestCase(0, false)]
    [TestCase(3, false)]
    [TestCase(0x3FFFFFFF, false)]
    [TestCase(0, true)]
    public static void TlsExpansionPreservesIdentityAndNativeModuleIndexArithmetic(int module, bool indirect)
    {
        WithCompiler(compiler => {
            TlsQuery query = new() { Module = module, Indirect = indirect };
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.getFieldThreadLocalStoreID = &GetFieldThreadLocalStoreId;
            vtable.Base.Base.isFieldStatic = &IsStaticField;
            ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
            compiler.info.compCompHnd = &jitInfo;
            var field = new GenTreeFieldAddr(TYP_I_IMPL, null, (CORINFO_FIELD_STRUCT_*)&query, -4) {
                Flags = GTF_FLD_TLS | GTF_DONT_CSE | GTF_COLON_COND | GTF_ORDER_SIDEEFF,
                _vnPair = new ValueNumPair(123, 456),
            };
#if DEBUG
            var nextId = compiler.compGenTreeID;
#endif
            var result = compiler.fgMorphExpandTlsFieldAddr(field);
            Assert.That(query.Queries, Is.EqualTo(1));
            Assert.That(result.Oper, Is.EqualTo(GT_ADD));
            Assert.That(result.Type, Is.EqualTo(TYP_I_IMPL));
            Assert.That(result.Flags, Is.EqualTo(field.Flags & GTF_COMMON_MASK));
            Assert.That(result._vnPair, Is.EqualTo(new ValueNumPair()));
            Assert.That(field.Oper, Is.EqualTo(GT_FIELD_ADDR));
            Assert.That(field._vnPair, Is.EqualTo(new ValueNumPair(123, 456)));
            Assert.That(result.Op2.AsIntCon().IconValue, Is.EqualTo((nint)(-4)));
            Assert.That(result.Op2.AsIntCon().FieldSeq,
                Is.SameAs(compiler.FieldSeqStore.Create(field.FldHnd, -4, FieldSeq.FieldKind.SimpleStatic)));
#if DEBUG
            Assert.That(result.TreeId, Is.EqualTo(field.TreeId));
            Assert.That(compiler.compGenTreeID - nextId, Is.EqualTo(indirect ? 9 : module != 0 ? 6 : 4));
#endif
            var tls = result.Op1.AsIndir().Addr;
            if (indirect || (module != 0))
            {
                var dll = tls.AsOp().Op2;
                tls = tls.AsOp().Op1;
                if (indirect)
                {
                    Assert.That(dll.Oper, Is.EqualTo(GT_MUL));
                    Assert.That(dll.AsOp().Op1.AsIndir().Addr.AsIntCon().IconValue, Is.EqualTo((nint)0x123400));
                    Assert.That(dll.AsOp().Op2.AsIntCon().IconValue, Is.EqualTo((nint)4));
                }
                else
                {
                    Assert.That(dll.AsIntCon().IconValue, Is.EqualTo(unchecked((nint)((uint)module * 4))));
                }
            }
            Assert.That(tls.Oper, Is.EqualTo(GT_IND));
            Assert.That(tls.Flags & (GTF_IND_NONFAULTING | GTF_IND_INVARIANT),
                Is.EqualTo(GTF_IND_NONFAULTING | GTF_IND_INVARIANT));
            Assert.That(tls.AsIndir().Addr.AsIntCon().IconHandleFlag, Is.EqualTo(GTF_ICON_TLS_HDL));
            Assert.That(tls.AsIndir().Addr.AsIntCon().IconValue, Is.EqualTo((nint)0x2C));
        });
    }

    [TestCase(2L, 1, 0xD800)]
    [TestCase(0x100000002L, 1, 0)]
    [TestCase(0L, 0, 0)]
    [TestCase(0L, -1, 0)]
    public static void ConstantStringCharacterUsesTheEEQueryAndNativeIndexWidth(long index, int length, int character)
    {
        WithCompiler(compiler => {
            compiler.opts.SetMinOpts(false);
            LiteralQuery query = new() { ResultLength = length, Character = (char)character };
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.Base.getStringLiteral = &GetStringLiteral;
            ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
            compiler.info.compCompHnd = &jitInfo;
            var literal = new GenTreeStrCon(42, (CORINFO_MODULE_STRUCT_*)&query);
            var address = new GenTreeIndexAddr(literal, compiler.gtNewIconNode(TYP_I_IMPL, (nint)index),
                TYP_USHORT, null, 2, 8, 12, boundsCheck: true);
            var indirection = compiler.gtNewIndir(TYP_USHORT, address);

            var result = compiler.gtFoldIndirConst(indirection);
            Assert.That(query.Queries, Is.EqualTo(1));
            Assert.That(query.Token, Is.EqualTo(42));
            Assert.That(query.BufferSize, Is.EqualTo(1));
            Assert.That(query.StartIndex, Is.EqualTo(unchecked((int)index)));
            Assert.That(indirection.Addr, Is.SameAs(address));
            if (length > 0)
            {
                Assert.That(result, Is.TypeOf<GenTreeIntCon>());
                assert(result is not null);
                Assert.That(result.Type, Is.EqualTo(TYP_INT));
                Assert.That(result.AsIntCon().IconValue, Is.EqualTo((nint)character));
                Assert.That(result.Flags & GTF_ALL_EFFECT, Is.EqualTo(GTF_EMPTY));
            }
            else
            {
                Assert.That(result, Is.Null);
            }
        });
    }

    [TestCase(-1, false, TYP_USHORT)]
    [TestCase(0, true, TYP_USHORT)]
    [TestCase(0, false, TYP_INT)]
    public static void IneligibleStringCharacterLoadsDoNotQueryTheEE(int index, bool emptyField, var_types type)
    {
        WithCompiler(compiler => {
            compiler.opts.SetMinOpts(false);
            LiteralQuery query = default;
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.Base.getStringLiteral = &GetStringLiteral;
            ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
            compiler.info.compCompHnd = &jitInfo;
            var literal = emptyField
                ? new GenTreeStrCon(EMPTY_STRING_SCON, null)
                : new GenTreeStrCon(42, (CORINFO_MODULE_STRUCT_*)&query);
            var address = new GenTreeIndexAddr(literal, compiler.gtNewIconNode(TYP_INT, index),
                type, null, type.Size, 8, 12, boundsCheck: true);

            Assert.That(compiler.gtFoldIndirConst(compiler.gtNewIndir(type, address)), Is.Null);
            Assert.That(query.Queries, Is.Zero);
        });
    }

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

    [TestCase((ushort)4, false, true)]
    [TestCase((ushort)6, false, false)]
    [TestCase((ushort)4, true, false)]
    public static void FinalizeIndirectionRespectsExtentAndVolatility(ushort offset, bool isVolatile, bool converted)
    {
        WithCompiler(compiler => {
            compiler.lvaTable = [new LclVarDsc { Type = TYP_LONG }];
            compiler.lvaCount = 1;
            var address = compiler.gtNewLclAddrNode(TYP_BYREF, 0, offset);
            var indirection = compiler.gtNewIndir(TYP_INT, address, isVolatile ? GTF_IND_VOLATILE : GTF_EMPTY);
            indirection._vnPair.SetBoth(123);
            indirection.Flags |= GTF_GLOB_REF;
            var result = compiler.fgMorphFinalizeIndir(indirection);
            if (converted)
            {
                Assert.That(result, Is.SameAs(address));
                Assert.That(address.Oper, Is.EqualTo(GT_LCL_FLD));
                Assert.That(address.Type, Is.EqualTo(TYP_INT));
                Assert.That(address.LclOffs, Is.EqualTo(offset));
                Assert.That(address._vnPair.Liberal, Is.EqualTo(123));
                Assert.That(address.Flags & GTF_GLOB_REF, Is.EqualTo(GTF_GLOB_REF));
            }
            else
            {
                Assert.That(result, Is.Null);
                Assert.That(address.Oper, Is.EqualTo(GT_LCL_ADDR));
                Assert.That(address.Type, Is.EqualTo(TYP_BYREF));
            }
        });
    }

    [TestCase(TYP_INT, true)]
    [TestCase(TYP_LONG, false)]
    public static void FinalizeLocalStorePreservesDataAndPartialDefinition(var_types type, bool partial)
    {
        WithCompiler(compiler => {
            compiler.lvaTable = [new LclVarDsc { Type = TYP_LONG }];
            compiler.lvaCount = 1;
            var address = compiler.gtNewLclAddrNode(TYP_BYREF, 0, 0);
            var value = compiler.gtNewIndir(type, compiler.gtNewIconNode(TYP_I_IMPL, 0x1234));
            var store = compiler.gtNewStoreIndNode(type, address, value);
            store._vnPair.SetBoth(456);
            var result = compiler.fgMorphFinalizeIndir(store);

            Assert.That(result, Is.SameAs(address));
            Assert.That(address.Oper, Is.EqualTo(GT_STORE_LCL_FLD));
            Assert.That(address.Data, Is.SameAs(value));
            Assert.That(address.Flags & (GTF_ASG | GTF_VAR_DEF), Is.EqualTo(GTF_ASG | GTF_VAR_DEF));
            Assert.That((address.Flags & GTF_VAR_USEASG) != 0, Is.EqualTo(partial));
            Assert.That(address.Flags & GTF_EXCEPT, Is.EqualTo(GTF_EXCEPT));
            Assert.That(address._vnPair.Liberal, Is.EqualTo(456));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void StructReturnReplacementUpdatesTheOwningUse(bool promoted)
    {
        WithCompiler(compiler => {
            compiler.lvaTable = [
                new LclVarDsc { Type = TYP_STRUCT, Layout = new ClassLayout(8), lvPromoted = promoted, lvFieldLclStart = 1, lvFieldCnt = 2 },
                new LclVarDsc { Type = TYP_INT, lvIsStructField = true, lvParentLcl = 0, lvFldOffset = 0 },
                new LclVarDsc { Type = TYP_INT, lvIsStructField = true, lvParentLcl = 0, lvFldOffset = 4 },
            ];
            compiler.lvaCount = 3;
            var local = compiler.gtNewLclvNode(TYP_STRUCT, 0);
            var returnNode = compiler.gtNewUnaryNode(GT_RETURN, TYP_STRUCT, local);
            var replaced = compiler.fgTryReplaceStructLocalWithFields(ref returnNode.Op1Ref);
            Assert.That(replaced, Is.EqualTo(promoted));
            Assert.That(returnNode.Op1.Oper, Is.EqualTo(promoted ? GT_FIELD_LIST : GT_LCL_VAR));
            Assert.That(local.Oper, Is.EqualTo(GT_LCL_VAR));
        });
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    public static void BitCastRetypesEqualSizedMemoryLoads(bool field, bool minOpts)
    {
        WithCompiler(compiler => {
            compiler.opts.SetMinOpts(minOpts);
            GenTree source = field
                ? new GenTreeLclFld(GT_LCL_FLD, TYP_INT, 0, 4)
                : compiler.gtNewIndir(TYP_INT, compiler.gtNewIconNode(TYP_I_IMPL, 0x1234));
            var cast = compiler.gtNewUnaryNode(GT_BITCAST, TYP_FLOAT, source);
            cast._vnPair.SetBoth(789);
            var result = compiler.fgOptimizeBitCast(cast);
            if (minOpts)
            {
                Assert.That(result, Is.Null);
                Assert.That(source.Type, Is.EqualTo(TYP_INT));
            }
            else
            {
                Assert.That(result, Is.SameAs(source));
                Assert.That(source.Type, Is.EqualTo(TYP_FLOAT));
                Assert.That(source._vnPair.Liberal, Is.EqualTo(789));
            }
        });
    }

    private struct LiteralQuery
    {
        public int ResultLength;
        public char Character;
        public int Queries;
        public int Token;
        public int BufferSize;
        public int StartIndex;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetStringLiteral(ICorJitInfo* self, CORINFO_MODULE_STRUCT_* module, int token,
        char* buffer, int bufferSize, int startIndex)
    {
        var query = (LiteralQuery*)module;
        query->Queries++;
        query->Token = token;
        query->BufferSize = bufferSize;
        query->StartIndex = startIndex;
        if (query->ResultLength > 0)
        {
            *buffer = query->Character;
        }
        return query->ResultLength;
    }

    private struct TlsQuery
    {
        public int Module;
        public bool Indirect;
        public int Queries;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetFieldThreadLocalStoreId(ICorJitInfo* jitInfo, CORINFO_FIELD_STRUCT_* field, void** indirection)
    {
        var query = (TlsQuery*)field;
        query->Queries++;
        *indirection = query->Indirect ? (void*)0x123400 : null;
        return query->Module;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte IsStaticField(ICorJitInfo* jitInfo, CORINFO_FIELD_STRUCT_* field) => 1;

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
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
