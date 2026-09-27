// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, promotiondecomposition.cpp.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.

using System;
using System.Collections.Generic;

namespace RyuJitSharp;

public partial class Compiler
{
    internal sealed class PhysicalPromotionDecompositionStatementList
    {
        private readonly List<GenTree> _statements = [];

        public int Count => _statements.Count;

        public void AddStatement(GenTree statement) => _statements.Add(statement);

        public GenTree PrefixTo(GenTree value, Compiler compiler, var_types? commaType = null)
        {
            var type = commaType ?? value.Type;
            for (var index = _statements.Count - 1; index >= 0; index--)
            {
                value = compiler.gtNewCommaNode(type, _statements[index], value);
            }

            return value;
        }

        public GenTree ToCommaTree(Compiler compiler)
        {
            if (_statements.Count == 0)
            {
                return compiler.gtNewNothingNode();
            }

            var tree = _statements[^1];
            for (var index = _statements.Count - 2; index >= 0; index--)
            {
                tree = compiler.gtNewCommaNode(TYP_VOID, _statements[index], tree);
            }

            return tree;
        }
    }

    internal sealed class PhysicalPromotionLocationAccess
    {
        private GenTreeLclVarCommon? _local;
        private GenTree? _address;
        private target_ssize_t _addressBaseOffset;
        private FieldSeq? _addressBaseFieldSeq;
        private GenTreeFlags _indirFlags;
        private int _usesLeft = -1;

        public void InitializeIndir(GenTree address, target_ssize_t baseOffset, FieldSeq? fieldSeq,
                                    GenTreeFlags flags, int expectedUses)
        {
            _address = address;
            _addressBaseOffset = baseOffset;
            _addressBaseFieldSeq = fieldSeq;
            _indirFlags = flags;
            _usesLeft = expectedUses;
        }

        public void InitializeLocal(GenTreeLclVarCommon local)
        {
            _local = local;
        }

        public GenTree CreateRead(int offset, var_types type, Compiler compiler)
        {
            if (_address is not null)
            {
                return compiler.gtNewIndir(type, GrabAddress(offset, compiler), GetIndirFlags(type));
            }

            var local = _local ?? throw new InvalidOperationException("Location was not initialized.");
            var fieldLocal = FindRegularlyPromotedField(offset, compiler);
            if ((fieldLocal != BAD_VAR_NUM) && (compiler.lvaGetDesc(fieldLocal).Type == type))
            {
                return compiler.gtNewLclvNode(type, fieldLocal);
            }

            var field = compiler.gtNewLclFldNode(type, local.LclNum,
                checked((ushort)(local.LclOffs + offset)));
            compiler.lvaSetVarDoNotEnregister(local.LclNum, DoNotEnregisterReason.LocalField);
            return field;
        }

        public GenTree CreateStore(int offset, var_types type, GenTree source, Compiler compiler)
        {
            if (_address is not null)
            {
                return compiler.gtNewStoreIndNode(type, GrabAddress(offset, compiler),
                    source, GetIndirFlags(type));
            }

            var local = _local ?? throw new InvalidOperationException("Location was not initialized.");
            var fieldLocal = FindRegularlyPromotedField(offset, compiler);
            if ((fieldLocal != BAD_VAR_NUM) && (compiler.lvaGetDesc(fieldLocal).Type == type))
            {
                return compiler.gtNewStoreLclVarNode(fieldLocal, source);
            }

            var field = compiler.gtNewStoreLclFldNode(type, local.LclNum,
                checked((ushort)(local.LclOffs + offset)), source);
            compiler.lvaSetVarDoNotEnregister(local.LclNum, DoNotEnregisterReason.LocalField);
            return field;
        }

        public int FindRegularlyPromotedField(int offset, Compiler compiler)
        {
            if (_local is null)
            {
                return BAD_VAR_NUM;
            }

            ref var descriptor = ref compiler.lvaGetDesc(_local.LclNum);
            if (!descriptor.lvPromoted)
            {
                return BAD_VAR_NUM;
            }

            return compiler.lvaGetFieldLocal(descriptor, checked((uint)(_local.LclOffs + offset)));
        }

        public GenTree GrabAddress(int offset, Compiler compiler)
        {
            if ((_address is null) || (_usesLeft <= 0))
            {
                throw new InvalidOperationException("Decomposed address was used more times than planned.");
            }

            _usesLeft--;
            var address = _usesLeft == 0 ? _address : compiler.gtCloneExpr(_address);
            var fullOffset = unchecked(_addressBaseOffset + offset);
            if ((fullOffset != 0) || (_addressBaseFieldSeq is not null))
            {
                var offsetNode = compiler.gtNewIconNode(TYP_I_IMPL, unchecked((nint)fullOffset));
                offsetNode.FieldSeq = _addressBaseFieldSeq;
                address = compiler.gtNewBinaryNode(GT_ADD, varTypeIsGC(address.Type) ? TYP_BYREF : TYP_I_IMPL,
                    address, offsetNode);
            }

            return address;
        }

        public void CheckFullyUsed()
        {
            assert((_address is null) || (_usesLeft == 0));
        }

        private GenTreeFlags GetIndirFlags(var_types type)
        {
            var flags = _indirFlags;
            if (!varTypeIsGC(type))
            {
                flags |= GTF_IND_ALLOW_NON_ATOMIC;
            }

            if (type.Size == 1)
            {
                flags &= ~GTF_IND_UNALIGNED;
            }

            return flags;
        }
    }
}
