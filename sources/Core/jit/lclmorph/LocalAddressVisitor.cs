// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.

namespace RyuJitSharp;

internal partial struct LocalAddressVisitor
{
    private readonly Compiler _compiler;
    private readonly bool _sequenceLocals;
    private LocalSequencer _sequencer;
    private bool _stmtModified;

    internal LocalAddressVisitor(Compiler compiler, LocalSequencer? sequencer = null)
    {
        _compiler = compiler;
        _sequenceLocals = sequencer.HasValue;
        _sequencer = sequencer.GetValueOrDefault();
    }

    private readonly NodeThreading ReplacementThreading => _sequenceLocals ? NodeThreading.AllLocals : NodeThreading.None;

    private void ReplaceNode(ref GenTree use, GenTree replacement)
    {
        if (_sequenceLocals)
        {
            _sequencer.ReplaceNode(use, replacement);
        }

        use = replacement;
    }

    internal bool MorphStructField(ref GenTree use, GenTree? user)
    {
        var node = use.AsIndir();
        var addr = node.Addr;

        if (node.IsVolatile && ((addr.Oper is not GT_FIELD_ADDR) || ((addr.Flags & GTF_FLD_DEREFERENCED) == 0)))
        {
            // TODO-Bug: transforming volatile indirections like this is not legal.
            // The native FIELD_ADDR exception above is a compatibility quirk.
            return false;
        }

        var fieldLclNum = MorphStructFieldAddress(ref node.AddrRef, node.ValueSize);

        if (fieldLclNum == BAD_VAR_NUM)
        {
            return false;
        }

        ref var fieldVarDsc = ref _compiler.lvaGetDesc(fieldLclNum);
        var fieldType = fieldVarDsc.Type;
        assert(fieldType is not TYP_STRUCT);

        if (node.Type != fieldType)
        {
            return false;
        }

        var isDef = node.Oper is GT_STOREIND or GT_STORE_BLK;
        var replacement = new GenTreeLclVar(isDef ? GT_STORE_LCL_VAR : GT_LCL_VAR, fieldType, fieldLclNum,
            isDef ? node.Data : null, node, ReplacementThreading);
        replacement.Flags &= GTF_COMMON_MASK;

        if (isDef)
        {
            replacement.Flags |= GTF_VAR_DEF;
        }
        else
        {
            // TODO-ASG-Cleanup: preserve the native load flag-clearing quirk.
            replacement.Flags &= GTF_NODE_MASK | GTF_DONT_CSE;
        }

        ReplaceNode(ref use, replacement);
        return true;
    }

    internal int MorphStructFieldAddress(ref GenTree use, ValueSize accessSize)
    {
        uint offset = 0;
        var addr = use;

        if ((addr.Oper is GT_FIELD_ADDR) && addr.AsFieldAddr().IsInstance)
        {
            offset = unchecked((uint)addr.AsFieldAddr().FldOffset);
            addr = addr.AsFieldAddr().FldObj;
        }

        if (addr.Oper is GT_LCL_ADDR)
        {
            offset = unchecked(offset + addr.AsLclFld().LclOffs);
            ref var varDsc = ref _compiler.lvaGetDesc(addr.AsLclVarCommon().LclNum);

            if (varDsc.lvPromoted)
            {
                var fieldLclNum = _compiler.lvaGetFieldLocal(varDsc, offset);

                if (fieldLclNum == BAD_VAR_NUM)
                {
                    // Reinterpreting a struct can introduce offsets that do not
                    // correspond to any of its promoted fields.
                    return BAD_VAR_NUM;
                }

                if (!accessSize.IsNull && _compiler.IsWideAccess(fieldLclNum, 0, accessSize))
                {
                    return BAD_VAR_NUM;
                }

                JITDUMP($"Replacing the field in promoted struct with local var V{fieldLclNum:D2}\n");
                _stmtModified = true;

                var replacement = new GenTreeLclFld(GT_LCL_ADDR, use.Type, fieldLclNum, 0,
                    data: null, layout: null, use, ReplacementThreading);
                ReplaceNode(ref use, replacement);
                return fieldLclNum;
            }
        }

        return BAD_VAR_NUM;
    }

    internal void MorphLocalField(ref GenTree use, GenTree? user)
    {
        assert(use.Oper is GT_LCL_FLD or GT_STORE_LCL_FLD);
        var node = use.AsLclFld();
        var lclNum = node.LclNum;
        ref var varDsc = ref _compiler.lvaGetDesc(lclNum);

        if (varDsc.lvPromoted)
        {
            var fieldLclNum = _compiler.lvaGetFieldLocal(varDsc, node.LclOffs);

            if (fieldLclNum != BAD_VAR_NUM)
            {
                var fieldType = _compiler.lvaGetDesc(fieldLclNum).Type;

                if (node.Type == fieldType)
                {
                    var isDef = node.Oper is GT_STORE_LCL_FLD;
                    var replacement = new GenTreeLclVar(isDef ? GT_STORE_LCL_VAR : GT_LCL_VAR, fieldType, fieldLclNum,
                        isDef ? node.Data : null, node, ReplacementThreading);

                    if (isDef)
                    {
                        replacement.Flags &= ~GTF_VAR_USEASG;
                    }

                    ReplaceNode(ref use, replacement);
                    JITDUMP($"Replacing the GT_LCL_FLD in promoted struct with local var V{fieldLclNum:D2}\n");
                }
            }
        }

        if (!use.Oper.IsScalarLocal)
        {
            _compiler.lvaSetVarDoNotEnregister(lclNum, DoNotEnregisterReason.LocalField);
        }
        else
        {
            _stmtModified = true;
        }
    }

    internal readonly void UpdateEarlyRefCount(int lclNum, GenTree? node, GenTree? user)
    {
        ref var varDsc = ref _compiler.lvaGetDesc(lclNum);
        varDsc.incLvRefCntSaturating(1, RCS_EARLY);

        if (!_compiler.lvaIsImplicitByRefLocal(lclNum))
        {
            return;
        }

        // The weighted early count approximates uses as call arguments for
        // fgRetypeImplicitByRefArgs' decision to undo struct promotion.
        if ((node is not null) && (node.Oper is GT_LCL_VAR) && (user is not null) && (user.Oper is GT_CALL))
        {
            JITDUMP($"LocalAddressVisitor incrementing weighted ref count from {FMT_WT(varDsc.lvRefCntWtd(RCS_EARLY))} to {FMT_WT(varDsc.lvRefCntWtd(RCS_EARLY) + 1)} for implicit by-ref V{lclNum:D2} arg passed to call\n");
            varDsc.incLvRefCntWtd(1, RCS_EARLY);
        }
    }

    private static bool IsUnused(GenTree node, GenTree? user)
        => (user is null) || ((user.Oper is GT_COMMA) && (user.AsOp().Op1 == node));
}
