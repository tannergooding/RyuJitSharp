// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genMultiRegStoreToLocal(GenTreeLclVar lclNode)
    {
#if !TARGET_AMD64 || UNIX_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Multi-register local stores outside Windows AMD64 are not implemented.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(lclNode.Oper is GT_STORE_LCL_VAR);
        assert(varTypeIsStruct(lclNode.Type) || varTypeIsMultiReg(lclNode.Type));

        var op1 = lclNode.Op1;
        assert(op1.IsMultiRegNode);
        var actualOp1 = op1.SkipCopyOrReload;
        var regCount = actualOp1.GetMultiRegCount(_compiler);
        assert(regCount > 1);

        var lclNum = lclNode.LclNum;
        ref var varDsc = ref _compiler.lvaGetDesc(lclNum);
        if (actualOp1.Oper is GT_CALL)
        {
            assert(regCount <= MAX_RET_REG_COUNT);
            noway_assert(varDsc.lvIsMultiRegDest);
        }

#if FEATURE_SIMD
        if (varDsc.lvIsRegCandidate && (lclNode.RegNum != REG_NA))
        {
            assert(varTypeIsSimd(lclNode.Type));
            genMultiRegStoreToSIMDLocal(lclNode);
            return;
        }
#endif

        // Consume and define each field before consuming the next one. LSRA resolves
        // conflicts with copies or spills in this order; consuming all sources first
        // could reload a later field over an earlier field's still-needed value.
        uint offset = 0;
        var isMultiRegVar = lclNode.IsMultiRegLclVar;
        var hasRegs = false;
        if (isMultiRegVar)
        {
            assert(_compiler.lvaEnregMultiRegVars);
            assert(regCount == varDsc.lvFieldCnt);
        }

        for (byte i = 0; i < regCount; i++)
        {
            var reg = genConsumeReg(op1, i);
            var srcType = actualOp1.GetRegTypeByIndex(i);
            assert(reg != REG_NA);

            if (isMultiRegVar)
            {
                var varReg = lclNode.GetRegByIndex(i);
                var fieldLclNum = varDsc.lvFieldLclStart + i;
                ref var fieldVarDsc = ref _compiler.lvaGetDesc(fieldLclNum);
                var destType = fieldVarDsc.Type;

                if (varReg != REG_NA)
                {
                    hasRegs = true;
                    inst_Mov(destType, varReg, reg, canSkip: true);
                }
                else
                {
                    varReg = REG_STK;
                }

                if ((varReg == REG_STK) || fieldVarDsc.IsAlwaysAliveInMemory)
                {
                    if (!lclNode.IsLastUse(i))
                    {
                        // A narrow field can arrive in a full-width register.
                        var storeIns = ins_StoreFromSrc(reg, destType);
                        Emitter.emitIns_S_R(storeIns, destType.EmitSize, reg, fieldLclNum, 0);
                    }
                }
                fieldVarDsc.RegNum = varReg;
            }
            else
            {
                // Unpromoted stack homes are pointer-size rounded, so register-sized
                // stores may cover padding beyond an individual narrow field.
                Emitter.emitIns_S_R(ins_Store(srcType), srcType.EmitSize, reg, lclNum, unchecked((int)offset));
                offset = unchecked(offset + srcType.Size);
#if DEBUG
                var stackHomeSize = _compiler.lvaLclStackHomeSize(lclNum);
                assert(offset <= stackHomeSize);
#endif
            }
        }

        if (isMultiRegVar)
        {
            if (hasRegs)
            {
                genProduceReg(lclNode);
            }
            else
            {
                genUpdateLife(lclNode);
            }
        }
        else
        {
            genUpdateLife(lclNode);
            varDsc.RegNum = REG_STK;
        }
#endif
    }

    public void genMultiRegStoreToSIMDLocal(GenTreeLclVar lclNode)
    {
#if !TARGET_AMD64 || UNIX_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Multi-register SIMD local stores outside Windows AMD64 are not implemented.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(varTypeIsSimd(lclNode.Type));

        // The native Windows AMD64 branch asserts that this ABI case is unsupported.
        // Reject before its operand-consumption preamble rather than returning success.
        throw new FatalJitException(CORJIT_SKIPPED, "Multi-register returns into SIMD registers are unsupported on Windows AMD64.");
#endif
    }
}
