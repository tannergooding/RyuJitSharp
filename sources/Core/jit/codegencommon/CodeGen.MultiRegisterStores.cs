// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genMultiRegStoreToLocal(GenTreeLclVar lclNode)
    {
#if TARGET_WASM
        throw new FatalJitException(CORJIT_SKIPPED, "Register-local stores are not supported on Wasm.");
#else
#if TARGET_AMD64
        Emitter.RequireSupportedInstructionRecording();
#endif
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

#if TARGET_RISCV64 || TARGET_LOONGARCH64
        ref readonly var returnDescriptor = ref actualOp1.AsCall().ReturnTypeDesc;
#endif
#if SWIFT_SUPPORT
        ReadOnlySpan<int> offsets = default;
        if ((actualOp1.Oper is GT_CALL) && (actualOp1.AsCall().UnmanagedCallConv is CorInfoCallConvExtension.Swift))
        {
            unsafe
            {
                ref readonly var lowering = ref _compiler.GetSwiftLowering(actualOp1.AsCall().RetClsHnd);
                assert(!lowering.byReference && (regCount == lowering.numLoweredElements));
                offsets = lowering.offsets;
            }
        }
#endif

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
#if TARGET_LOONGARCH64 || TARGET_RISCV64
                offset = unchecked((uint)returnDescriptor.GetReturnFieldOffset(i));
#endif
#if SWIFT_SUPPORT
                if (!offsets.IsEmpty)
                {
                    offset = unchecked((uint)offsets[i]);
                }
#endif
                // Unpromoted stack homes are pointer-size rounded, so register-sized
                // stores may cover padding beyond an individual narrow field.
                Emitter.emitIns_S_R(ins_Store(srcType), srcType.EmitSize, reg, lclNum, unchecked((int)offset));
                offset = unchecked(offset + srcType.Size);
#if DEBUG
                var stackHomeSize = _compiler.lvaLclStackHomeSize(lclNum);
#if TARGET_64BIT
                assert(offset <= stackHomeSize);
#else
                if (varTypeIsStruct(varDsc.Type))
                {
                    assert(offset <= stackHomeSize);
                }
                else
                {
                    assert(varDsc.Type is TYP_LONG);
                    assert(offset <= TYP_LONG.Size);
                }
#endif
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
#if TARGET_ARMARCH && FEATURE_SIMD
        assert(varTypeIsSimd(lclNode.Type));

        var op1 = lclNode.Op1;
        var actualOp1 = op1.SkipCopyOrReload;
        var regCount = actualOp1.GetMultiRegCount(_compiler);
        assert(op1.IsMultiRegNode);
        genConsumeRegs(op1);

        var targetReg = lclNode.RegNum;
        for (var i = regCount - 1; i >= 0; i--)
        {
            var type = actualOp1.GetRegTypeByIndex(i);
            var reg = actualOp1.GetRegByIndex(checked((byte)i));

            if (op1.Oper.IsCopyOrReload)
            {
                var reloadReg = op1.AsCopyOrReload().GetRegNumByIdx(checked((byte)i));
                if (reloadReg != REG_NA)
                {
                    reg = reloadReg;
                }
            }

            assert(reg != REG_NA);
            if (varTypeIsFloating(type))
            {
                Emitter.emitIns_R_R_I_I(INS_mov, emitTypeSize(type), targetReg, reg, i, 0);
            }
            else
            {
                Emitter.emitIns_R_R_I(INS_mov, emitTypeSize(type), targetReg, reg, i);
            }
        }

        genProduceReg(lclNode);
#elif !TARGET_XARCH || !FEATURE_SIMD
        throw new FatalJitException(CORJIT_SKIPPED, "Multi-register SIMD local stores require xarch SIMD support.");
#elif TARGET_AMD64 && !UNIX_AMD64_ABI
        Emitter.RequireSupportedInstructionRecording();
        assert(varTypeIsSimd(lclNode.Type));

        // The native Windows AMD64 branch asserts that this ABI case is unsupported.
        // Reject before its operand-consumption preamble rather than returning success.
        throw new FatalJitException(CORJIT_SKIPPED, "Multi-register returns into SIMD registers are unsupported on Windows AMD64.");
#else
        assert(varTypeIsSimd(lclNode.Type));
        var op1 = lclNode.Op1;
        var actualOp1 = op1.SkipCopyOrReload;
        var regCount = actualOp1.GetMultiRegCount(_compiler);
        assert(op1.IsMultiRegNode);
        genConsumeRegs(op1);

        var call = actualOp1.AsCall();
        ref readonly var returnDescriptor = ref call.ReturnTypeDesc;
        assert(regCount == 2);
        var targetReg = lclNode.RegNum;
        var reg0 = call.GetRegNumByIdx(0);
        var reg1 = call.GetRegNumByIdx(1);

        if (op1.Oper.IsCopyOrReload)
        {
            var reloadReg = op1.AsCopyOrReload().GetRegNumByIdx(0);
            if (reloadReg != REG_NA)
            {
                reg0 = reloadReg;
            }

            reloadReg = op1.AsCopyOrReload().GetRegNumByIdx(1);
            if (reloadReg != REG_NA)
            {
                reg1 = reloadReg;
            }
        }

#if UNIX_AMD64_ABI
        assert(varTypeIsFloating(returnDescriptor.GetReturnRegType(0)));
        assert(varTypeIsFloating(returnDescriptor.GetReturnRegType(1)));

        if (targetReg != reg1)
        {
            Emitter.emitIns_SIMD_R_R_R(INS_movlhps, EA_16BYTE, targetReg, reg0, reg1, INS_OPTS_NONE);
        }
        else
        {
            // Assemble reversed halves first, then swap them without losing the
            // second source when it aliases the destination.
            Emitter.emitIns_SIMD_R_R_R(INS_movlhps, EA_16BYTE, targetReg, reg1, reg0, INS_OPTS_NONE);
            Emitter.emitIns_SIMD_R_R_R_I(INS_shufpd, EA_16BYTE, targetReg, targetReg, reg1, 1, INS_OPTS_NONE);
        }
        genProduceReg(lclNode);
#elif TARGET_X86
        if (TargetOS.IsWindows)
        {
            assert(varTypeIsIntegral(returnDescriptor.GetReturnRegType(0)));
            assert(varTypeIsIntegral(returnDescriptor.GetReturnRegType(1)));
            assert(lclNode.Type is TYP_SIMD8);

            inst_Mov(TYP_FLOAT, targetReg, reg0, canSkip: false);
            Emitter.emitIns_SIMD_R_R_R_I(INS_pinsrd, TYP_SIMD8.EmitSize, targetReg, targetReg, reg1, 1, INS_OPTS_NONE);
            genProduceReg(lclNode);
        }
#endif
#endif
    }
}
