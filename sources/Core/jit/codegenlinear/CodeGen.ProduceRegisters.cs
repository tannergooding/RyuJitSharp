// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genSpillLocal(int varNum, var_types type, GenTreeLclVar lclNode, regNumber regNum)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Local spills outside AMD64 are not implemented.");
#else
        ref var varDsc = ref _compiler.lvaGetDesc(varNum);
        assert(!varDsc.lvNormalizeOnStore || (type == varDsc.GetStackSlotHomeType()));

        // Write-through locals already have a valid stack copy on uses, but every definition must store.
        if (((lclNode.Flags & GTF_VAR_DEF) != 0) || !varDsc.IsAlwaysAliveInMemory)
        {
            Emitter.emitIns_S_R(ins_Store(type, _compiler.isSIMDTypeLocalAligned(varNum)), type.EmitSize,
                regNum, varNum, 0);
        }
#endif
    }

    public void genProduceReg(GenTree tree)
    {
#if !TARGET_AMD64 || UNIX_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Register production outside Windows AMD64 is not implemented.");
#else
#if DEBUG
        assert((tree._debugFlags & GTF_DEBUG_NODE_CG_PRODUCED) == 0);
        tree._debugFlags |= GTF_DEBUG_NODE_CG_PRODUCED;
#endif
        if ((tree.Flags & GTF_SPILL) != 0)
        {
            noway_assert(!tree.Oper.IsCopyOrReload);

            if (genIsRegCandidateLocal(tree))
            {
                var lclNode = tree.AsLclVar();
                ref var varDsc = ref _compiler.lvaGetDesc(lclNode.LclNum);
                var spillType = varDsc.GetRegisterType(lclNode);
                genSpillLocal(lclNode.LclNum, spillType, lclNode, tree.RegNum);
            }
            else if (tree.IsMultiRegLclVar)
            {
                assert(_compiler.lvaEnregMultiRegVars);
                var lclNode = tree.AsLclVar();
                ref var varDsc = ref _compiler.lvaGetDesc(lclNode.LclNum);
                var regCount = lclNode.GetFieldCount(_compiler);

                for (byte i = 0; i < regCount; i++)
                {
                    var flags = lclNode.GetRegSpillFlagByIdx(i);
                    if ((flags & GTF_SPILL) != 0)
                    {
                        var reg = lclNode.GetRegNumByIdx(i);
                        var fieldVarNum = varDsc.lvFieldLclStart + i;
                        var spillType = _compiler.lvaGetDesc(fieldVarNum).GetRegisterType();
                        genSpillLocal(fieldVarNum, spillType, lclNode, reg);
                    }
                }
            }
            else
            {
                if (tree.IsMultiRegNode)
                {
                    var regCount = tree.GetMultiRegCount(_compiler);
                    for (byte i = 0; i < regCount; i++)
                    {
                        var flags = tree.GetRegSpillFlagByIdx(i);
                        if ((flags & GTF_SPILL) != 0)
                        {
                            var reg = tree.GetRegByIndex(i);
                            _regSet.rsSpillTree(reg, tree, i);
                            _gcInfo.gcMarkRegSetNpt(regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask));
                        }
                    }
                }
                else
                {
                    _regSet.rsSpillTree(tree.RegNum, tree);
                    _gcInfo.gcMarkRegSetNpt(tree.RegMask);
                }

                tree.Flags |= GTF_SPILLED;
                tree.Flags &= ~GTF_SPILL;
                return;
            }
        }

        genUpdateLife(tree);

#if EMIT_GENERATE_GCINFO
        if (tree.HasReg(_compiler) && (!genIsRegCandidateLocal(tree) || ((tree.Flags & GTF_VAR_DEATH) == 0)))
        {
            if (tree.IsMultiRegCall)
            {
                var call = tree.AsCall();
                ref readonly var retTypeDesc = ref call.ReturnTypeDesc;
                var regCount = retTypeDesc.ReturnRegCount;
                for (byte i = 0; i < regCount; i++)
                {
                    _gcInfo.gcMarkRegPtrVal(call.GetRegNumByIdx(i), retTypeDesc.GetReturnRegType(i));
                }
            }
            else if (tree.IsCopyOrReloadOfMultiRegCall)
            {
                noway_assert(tree.Oper is GT_COPY);
                var copy = tree.AsCopyOrReload();
                var call = copy.Op1.AsCall();
                ref readonly var retTypeDesc = ref call.ReturnTypeDesc;
                var regCount = retTypeDesc.ReturnRegCount;

                for (byte i = 0; i < regCount; i++)
                {
                    var type = retTypeDesc.GetReturnRegType(i);
                    var toReg = copy.GetRegNumByIdx(i);
                    if (toReg != REG_NA)
                    {
                        _gcInfo.gcMarkRegPtrVal(toReg, type);
                    }
                }
            }
            else if (tree.IsMultiRegLclVar)
            {
                assert(_compiler.lvaEnregMultiRegVars);
                var lclNode = tree.AsLclVar();
                ref var varDsc = ref _compiler.lvaGetDesc(lclNode.LclNum);

                for (byte i = 0; i < varDsc.lvFieldCnt; i++)
                {
                    if (!lclNode.IsLastUse(i))
                    {
                        var reg = lclNode.GetRegNumByIdx(i);
                        if (reg != REG_NA)
                        {
                            _gcInfo.gcMarkRegPtrVal(reg, _compiler.lvaGetDesc(varDsc.lvFieldLclStart + i).Type);
                        }
                    }
                }
            }
            else
            {
                _gcInfo.gcMarkRegPtrVal(tree.RegNum, tree.Type);
            }
        }
#endif
#endif
    }

    public void genTransferRegGCState(regNumber dst, regNumber src)
    {
        var srcMask = regMaskTP.CreateFromRegNum(src, src.SingleTypeMask);
        var dstMask = regMaskTP.CreateFromRegNum(dst, dst.SingleTypeMask);

        if ((_gcInfo.gcRegGCrefSetCur & srcMask).IsNonEmpty)
        {
            _gcInfo.gcMarkRegSetGCref(dstMask);
        }
        else if ((_gcInfo.gcRegByrefSetCur & srcMask).IsNonEmpty)
        {
            _gcInfo.gcMarkRegSetByref(dstMask);
        }
        else
        {
            _gcInfo.gcMarkRegSetNpt(dstMask);
        }
    }
}
