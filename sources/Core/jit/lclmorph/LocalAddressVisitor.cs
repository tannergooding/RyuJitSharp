// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.

namespace RyuJitSharp;

internal partial struct LocalAddressVisitor
{
    private readonly Compiler _compiler;
    private readonly bool _sequenceLocals;
    private readonly LocalEqualsLocalAddrAssertions? _lclAddrAssertions;
    private LocalSequencer _sequencer;
    private bool _stmtModified;
    private bool _stmtSideEffectsModified;

    private enum IndirTransform
    {
        Nop,
        BitCast,
        NarrowCast,
#if FEATURE_HW_INTRINSICS
        GetElement,
        WithElement,
#endif
        LclVar,
        LclFld,
    }

    internal LocalAddressVisitor(Compiler compiler, LocalSequencer? sequencer = null,
        LocalEqualsLocalAddrAssertions? assertions = null)
    {
        _compiler = compiler;
        _sequenceLocals = sequencer.HasValue;
        _sequencer = sequencer.GetValueOrDefault();
        _lclAddrAssertions = assertions;
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

    internal void EscapeValue(ref Value value, GenTree? user)
    {
        if (value.IsAddress)
        {
            EscapeAddress(ref value, user);
        }
        else
        {
            value.Consume();
        }
    }

    internal unsafe void EscapeAddress(ref Value value, GenTree? user)
    {
        assert(value.IsAddress);
        var lclNum = value.LclNum;
        ref var varDsc = ref _compiler.lvaGetDesc(lclNum);
        var defFlags = GTF_EMPTY;
        var call = (user is not null) && user.Oper.IsCall ? user.AsCall() : null;
        var escapeAddress = true;

        if ((call is not null) && _compiler.IsValidLclAddr(lclNum, unchecked((int)value.Offset)))
        {
            var defSize = uint.MaxValue;
            var retBuffer = call.Args.RetBufferArg;
            assert(!call.Args.HasRetBuffer || (retBuffer is not null));

            if ((retBuffer is not null) && (value.Node == retBuffer.Node))
            {
                // The local must not turn into an indirection during later morph.
                var suitable = _compiler.opts.compJitOptimizeStructHiddenBuffer && varTypeIsStruct(varDsc.Type) &&
                    !_compiler.lvaIsUnknownSizeLocal(lclNum) && !_compiler.lvaIsImplicitByRefLocal(lclNum) &&
                    (!varDsc.lvIsStructField || !_compiler.lvaIsImplicitByRefLocal(varDsc.lvParentLcl));
#if TARGET_X86
                if (_compiler.lvaIsArgAccessedViaVarArgsCookie(lclNum))
                {
                    suitable = false;
                }
#endif
                if (suitable)
                {
                    _compiler.lvaSetHiddenBufferStructArg(lclNum);
                    call._callMoreFlags |= GTF_CALL_M_RETBUFFARG_LCLOPT;
                    defSize = (uint)_compiler.typGetObjLayout(call.RetClsHnd).Size;
                }
            }
            else if (call.IsAsync)
            {
                var resumedDef = call.Args.FindWellKnownArg(WellKnownArg.AsyncResumedDef);

                if ((resumedDef is not null) && (value.Node == resumedDef.Node))
                {
                    defSize = TARGET_POINTER_SIZE;
                }
            }

            if (defSize != uint.MaxValue)
            {
#if DEBUG
                varDsc.IsDefinedViaAddress = true;
#endif
                escapeAddress = false;
                defFlags = GTF_VAR_DEF;
                _stmtSideEffectsModified |= (call.Flags & GTF_ASG) == 0;
                call.Flags |= GTF_ASG;

                if (!_compiler.IsEntireAccess(lclNum, value.Offset, new ValueSize(unchecked((int)defSize))))
                {
                    defFlags |= GTF_VAR_USEASG;
                }
            }
        }

        if (escapeAddress)
        {
            var exposedLclNum = varDsc.lvIsStructField ? varDsc.lvParentLcl : lclNum;

            if (_lclAddrAssertions is not null)
            {
                _lclAddrAssertions.OnExposed(exposedLclNum);
            }
            else
            {
                _compiler.lvaSetVarAddrExposed(exposedLclNum, AddressExposedReason.ESCAPE_ADDRESS);
            }
        }

#if TARGET_64BIT
        // Match JIT64's extra storage for byref int32 arguments: some P/Invoke
        // signatures incorrectly write a native-sized value through these addresses.
        if ((call is not null) && !varDsc.lvIsParam && !varDsc.lvIsStructField &&
            (varDsc.Type.ActualType is TYP_INT) && escapeAddress)
        {
            varDsc.lvQuirkToLong = true;
            JITDUMP($"Adding a quirk for the storage size of V{value.LclNum:D2} of type {varDsc.Type.Name}\n");
        }
#endif
        MorphLocalAddress(ref value.Use, lclNum, value.Offset);
        value.Node.Flags |= defFlags;
        value.Consume();
    }

    internal void ProcessIndirection(ref GenTree use, ref Value value, GenTree? user)
    {
        assert(value.IsAddress);
        var node = use.AsIndir();
        var lclNum = value.LclNum;
        var offset = value.Offset;
        ref var varDsc = ref _compiler.lvaGetDesc(lclNum);
        var indirSize = node.ValueSize;

        // A wide access can span promoted fields. Expose their parent as well,
        // so dependent promotion retains the original contiguous storage.
        if (indirSize.IsNull || _compiler.IsWideAccess(lclNum, offset, indirSize))
        {
            var exposedLclNum = varDsc.lvIsStructField ? varDsc.lvParentLcl : lclNum;

            if (_lclAddrAssertions is not null)
            {
                _lclAddrAssertions.OnExposed(exposedLclNum);
            }
            else
            {
                _compiler.lvaSetVarAddrExposed(exposedLclNum, AddressExposedReason.WIDE_INDIR);
            }

            MorphLocalAddress(ref node.AddrRef, lclNum, offset);
            node.Flags |= GTF_GLOB_REF;
            _stmtSideEffectsModified = true;
        }
        else
        {
            MorphLocalIndir(ref use, lclNum, offset, user);
        }

        value.Consume();
    }

    internal void MorphLocalAddress(ref GenTree use, int lclNum, uint offset)
    {
        assert(use.Type is TYP_BYREF or TYP_I_IMPL);
#if DEBUG
        assert(_compiler.lvaGetDesc(lclNum).IsAddressExposed ||
            ((_lclAddrAssertions is not null) && _lclAddrAssertions.IsMarkedForExposure(lclNum)) ||
            _compiler.lvaGetDesc(lclNum).IsDefinedViaAddress);
#endif
        GenTree replacement;

        if (_compiler.IsValidLclAddr(lclNum, unchecked((int)offset)))
        {
            replacement = new GenTreeLclFld(GT_LCL_ADDR, use.Type, lclNum, (ushort)offset,
                data: null, layout: null, use, ReplacementThreading);
        }
        else
        {
            // An unrepresentable local offset remains explicit pointer arithmetic.
            var address = _compiler.gtNewLclVarAddrNode(TYP_BYREF, lclNum);
            var constant = _compiler.gtNewIconNode(TYP_I_IMPL, unchecked((nint)(nuint)offset));
            replacement = new GenTreeOp(GT_ADD, use.Type, address, constant, use, ReplacementThreading);
        }

        replacement.Flags = GTF_EMPTY;
        ReplaceNode(ref use, replacement);
        _stmtModified = true;
    }

    private GenTreeLclVar MorphAddressToLocal(ref GenTree use, int lclNum)
    {
        ref var varDsc = ref _compiler.lvaGetDesc(lclNum);
        var type = varDsc.lvNormalizeOnLoad ? varDsc.Type : varDsc.Type.ActualType;
        var local = new GenTreeLclVar(GT_LCL_VAR, type, lclNum, data: null, use, ReplacementThreading);
        local.Flags &= GTF_COMMON_MASK;
        ReplaceNode(ref use, local);
        return local;
    }

    internal void MorphLocalIndir(ref GenTree use, int lclNum, uint offset, GenTree? user)
    {
        var indir = use.AsIndir();
        var layout = indir.Oper.IsBlk ? indir.AsBlk().Layout : null;
        var transform = SelectLocalIndirTransform(indir, lclNum, offset, user);
        ref var varDsc = ref _compiler.lvaGetDesc(lclNum);
        GenTreeLclVarCommon? lclNode = null;
        var isDef = indir.Oper is GT_STOREIND or GT_STORE_BLK;

        switch (transform)
        {
            case IndirTransform.Nop:
            {
                indir.BashToNOP();
                _stmtModified = true;
                return;
            }

            case IndirTransform.BitCast:
            {
                lclNode = MorphAddressToLocal(ref indir.AddrRef, lclNum);
                ReplaceNode(ref use, new GenTreeUnOp(GT_BITCAST, indir.Type, lclNode, indir, ReplacementThreading));
                break;
            }

            case IndirTransform.NarrowCast:
            {
                assert(varTypeIsIntegral(indir.Type));
                assert(varTypeIsIntegral(varDsc.Type));
                assert(varDsc.Type.Size >= indir.Type.Size);
                assert(!isDef);

                lclNode = MorphAddressToLocal(ref indir.AddrRef, lclNum);
                ReplaceNode(ref use, _compiler.gtNewCastNode(indir.Type.ActualType, lclNode, false, indir.Type));
                break;
            }

#if FEATURE_HW_INTRINSICS
            // Vector fields include scalar floats, a Plane's Vector3 and halves
            // of larger vectors. Preserve the native construction and ID order.
            case IndirTransform.GetElement:
            {
                GenTree? hwiNode = null;
                var elementType = indir.Type;
                lclNode = MorphAddressToLocal(ref indir.AddrRef, lclNum);

                switch (elementType)
                {
                    case TYP_FLOAT:
                    {
                        var index = _compiler.gtNewIconNode(TYP_INT, (nint)(offset / elementType.Size));
                        hwiNode = _compiler.gtNewSimdGetElementNode(elementType, lclNode, index, TYP_FLOAT, varDsc.Type.Size);
                        break;
                    }

                    case TYP_SIMD12:
                    {
                        assert(varDsc.Type.Size is 16);
                        hwiNode = _compiler.gtNewSimdHWIntrinsicNode(elementType, NI_Vector_AsVector3, TYP_FLOAT, 16, lclNode);
                        break;
                    }

#if TARGET_XARCH
                    case TYP_SIMD16:
                    case TYP_SIMD32:
#elif TARGET_ARM64
                    case TYP_SIMD8:
#endif
#if TARGET_XARCH || TARGET_ARM64
                    {
                        assert((elementType.Size * 2) == varDsc.Type.Size);

                        if (offset is 0)
                        {
                            hwiNode = _compiler.gtNewSimdGetLowerNode(elementType, lclNode, TYP_FLOAT, varDsc.Type.Size);
                        }
                        else
                        {
                            assert(offset == elementType.Size);
                            hwiNode = _compiler.gtNewSimdGetUpperNode(elementType, lclNode, TYP_FLOAT, varDsc.Type.Size);
                        }
                        break;
                    }
#endif

                    default:
                    {
                        unreached();
                        break;
                    }
                }

                assert(hwiNode is not null);
                ReplaceNode(ref use, hwiNode);
                break;
            }

            case IndirTransform.WithElement:
            {
                GenTree? hwiNode = null;
                var elementType = indir.Type;
                GenTree simdLclNode = _compiler.gtNewLclVarNode(TYP_UNDEF, lclNum);
                var elementNode = indir.Data;

                switch (elementType)
                {
                    case TYP_FLOAT:
                    {
                        var index = _compiler.gtNewIconNode(TYP_INT, (nint)(offset / elementType.Size));
                        hwiNode = _compiler.gtNewSimdWithElementNode(varDsc.Type, simdLclNode, index,
                            elementNode, TYP_FLOAT, varDsc.Type.Size);
                        break;
                    }

                    case TYP_SIMD12:
                    {
                        assert(varDsc.Type is TYP_SIMD16);

                        // Use the stored Vector3 as the main value and retain the
                        // original local's fourth element in the resulting Vector4.
                        elementNode = _compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_Vector_AsVector128Unsafe,
                            TYP_FLOAT, 12, elementNode);
                        var index1 = _compiler.gtNewIconNode(TYP_INT, 3);
                        simdLclNode = _compiler.gtNewSimdGetElementNode(TYP_FLOAT, simdLclNode, index1, TYP_FLOAT, 16);
                        var index2 = _compiler.gtNewIconNode(TYP_INT, 3);
                        hwiNode = _compiler.gtNewSimdWithElementNode(TYP_SIMD16, elementNode, index2, simdLclNode, TYP_FLOAT, 16);
                        break;
                    }

#if TARGET_XARCH
                    case TYP_SIMD16:
                    case TYP_SIMD32:
#elif TARGET_ARM64
                    case TYP_SIMD8:
#endif
#if TARGET_XARCH || TARGET_ARM64
                    {
                        assert((elementType.Size * 2) == varDsc.Type.Size);

                        if (offset is 0)
                        {
                            hwiNode = _compiler.gtNewSimdWithLowerNode(varDsc.Type, simdLclNode, elementNode,
                                TYP_FLOAT, varDsc.Type.Size);
                        }
                        else
                        {
                            assert(offset == elementType.Size);
                            hwiNode = _compiler.gtNewSimdWithUpperNode(varDsc.Type, simdLclNode, elementNode,
                                TYP_FLOAT, varDsc.Type.Size);
                        }
                        break;
                    }
#endif

                    default:
                    {
                        unreached();
                        break;
                    }
                }

                assert(hwiNode is not null);
                lclNode = new GenTreeLclVar(GT_STORE_LCL_VAR, varDsc.Type, lclNum, hwiNode, indir, ReplacementThreading);
                ReplaceNode(ref use, lclNode);
                break;
            }
#endif

            case IndirTransform.LclVar:
            {
                var type = indir.Type;

                if (type != varDsc.Type)
                {
                    assert(type.Size == varDsc.Type.Size);
                    type = varDsc.lvNormalizeOnLoad ? varDsc.Type : varDsc.Type.ActualType;
                }

                lclNode = new GenTreeLclVar(isDef ? GT_STORE_LCL_VAR : GT_LCL_VAR, type, lclNum,
                    isDef ? indir.Data : null, indir, ReplacementThreading);
                ReplaceNode(ref use, lclNode);
                break;
            }

            case IndirTransform.LclFld:
            {
                var local = new GenTreeLclFld(isDef ? GT_STORE_LCL_FLD : GT_LCL_FLD, indir.Type, lclNum,
                    (ushort)offset, isDef ? indir.Data : null, layout, indir, ReplacementThreading);
                ReplaceNode(ref use, local);

                // STRUCT fields can still become enregisterable locals during
                // global morph. Other field accesses must already establish DNER.
                if (local.Type is not TYP_STRUCT)
                {
                    MorphLocalField(ref use, user);
                }

                lclNode = use.AsLclVarCommon();
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }

        var lclNodeFlags = GTF_EMPTY;

        if (isDef)
        {
            lclNodeFlags |= use.AsLclVarCommon().Data.Flags & GTF_ALL_EFFECT;
            lclNodeFlags |= GTF_ASG | GTF_VAR_DEF;

            if ((use.Oper is GT_LCL_FLD or GT_STORE_LCL_FLD) && use.AsLclFld().IsPartial(_compiler))
            {
                lclNodeFlags |= GTF_VAR_USEASG;

                // Partial stores can leave a small local's upper bits incorrect.
                // Exposing it ensures every subsequent load normalizes those bits.
                if (varTypeIsSmall(varDsc.Type) && !varDsc.lvIsStructField)
                {
                    _compiler.lvaSetVarAddrExposed(lclNum, AddressExposedReason.SMALL_TYPE_PARTIAL_DEF);
                }
            }
        }

        assert(lclNode is not null);
        lclNode.Flags = lclNodeFlags;
        _stmtModified = true;
        _stmtSideEffectsModified = true;
    }

    private readonly IndirTransform SelectLocalIndirTransform(GenTreeIndir indir, int lclNum, uint offset, GenTree? user)
    {
        assert((offset <= ushort.MaxValue) && !indir.IsVolatile);
        var isDef = indir.Oper is GT_STOREIND or GT_STORE_BLK;

        if (!isDef && IsUnused(indir, user))
        {
            return IndirTransform.Nop;
        }

        ref var varDsc = ref _compiler.lvaGetDesc(lclNum);

        if (indir.Type is not TYP_STRUCT)
        {
            if (indir.Type == varDsc.Type)
            {
                return IndirTransform.LclVar;
            }

            if (isDef && (varTypeToSigned(indir.Type) == varTypeToSigned(varDsc.Type)))
            {
                assert(varTypeIsSmall(indir.Type));
                return IndirTransform.LclVar;
            }

            if (_compiler.opts.OptimizationDisabled)
            {
                return IndirTransform.LclFld;
            }

#if FEATURE_HW_INTRINSICS
            if (varTypeIsSimd(varDsc.Type))
            {
                if (indir.Type is TYP_FLOAT)
                {
                    if ((offset % TYP_FLOAT.Size) is 0)
                    {
                        return isDef ? IndirTransform.WithElement : IndirTransform.GetElement;
                    }
                }
                else if (indir.Type is TYP_SIMD12)
                {
                    if ((offset is 0) && (varDsc.Type is TYP_SIMD16))
                    {
                        return isDef ? IndirTransform.WithElement : IndirTransform.GetElement;
                    }
                }
#if TARGET_ARM64
                else if (indir.Type is TYP_SIMD8)
                {
                    if ((varDsc.Type is TYP_SIMD16) && ((offset % 8) is 0))
                    {
                        return isDef ? IndirTransform.WithElement : IndirTransform.GetElement;
                    }
                }
#endif
#if FEATURE_SIMD && TARGET_XARCH
                else if ((((indir.Type is TYP_SIMD16) && _compiler.compOpportunisticallyDependsOn(InstructionSet_AVX)) ||
                          ((indir.Type is TYP_SIMD32) && _compiler.compOpportunisticallyDependsOn(InstructionSet_AVX512))) &&
                         ((indir.Type.Size * 2) == varDsc.Type.Size) && ((offset % indir.Type.Size) is 0))
                {
                    return isDef ? IndirTransform.WithElement : IndirTransform.GetElement;
                }
#endif
            }
#endif

            if (!isDef && (offset is 0))
            {
                if (varTypeIsIntegral(indir.Type) && varTypeIsIntegral(varDsc.Type))
                {
                    return IndirTransform.NarrowCast;
                }

                if ((indir.Type.Size == varDsc.Type.Size) && (indir.Type.Size <= TARGET_POINTER_SIZE) &&
                    (varTypeIsFloating(indir.Type) || varTypeIsFloating(varDsc.Type)) && !varDsc.lvPromoted)
                {
                    return IndirTransform.BitCast;
                }
            }

            return IndirTransform.LclFld;
        }

        if (varDsc.Type is not TYP_STRUCT)
        {
            return IndirTransform.LclFld;
        }

        if (offset is 0)
        {
            var localLayout = varDsc.Layout;
            assert(localLayout is not null);

            if (indir.AsBlk().Layout.CanAssignFrom(localLayout))
            {
                return IndirTransform.LclVar;
            }
        }

        return IndirTransform.LclFld;
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
