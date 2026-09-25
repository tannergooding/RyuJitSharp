// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genSimdUpperSave(GenTreeIntrinsic node)
    {
#if !TARGET_AMD64 || UNIX_AMD64_ABI || !FEATURE_SIMD
        throw new FatalJitException(CORJIT_SKIPPED, "SIMD upper-lane saves require Windows AMD64 SIMD support.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(node.IntrinsicName == NI_SIMD_UpperSave);
        var op1 = node.Op1;
        assert(op1.Oper.IsLocal && (op1.Type is TYP_SIMD32 or TYP_SIMD64));
        var targetReg = node.RegNum;
        var op1Reg = genConsumeReg(op1);
        assert(op1Reg != REG_NA);

        if (targetReg != REG_NA)
        {
            // Only YMM upper halves can be preserved in another callee-save XMM register.
            assert(op1.Type == TYP_SIMD32);
            Emitter.emitIns_R_R_I(INS_vextractf32x4, EA_32BYTE, targetReg, op1Reg, 1);
            genProduceReg(node);
        }
        else
        {
            var varNum = op1.AsLclVarCommon().LclNum;
            ref var varDsc = ref _compiler.lvaGetDesc(varNum);
            assert(varDsc.lvOnFrame);
            if (op1.Type == TYP_SIMD32)
            {
                Emitter.emitIns_S_R_I(INS_vextractf32x4, EA_32BYTE, varNum, 16, op1Reg, 1);
            }
            else
            {
                assert(op1.Type == TYP_SIMD64);
                // Native preserves the entire ZMM value rather than extracting volatile lanes.
                Emitter.emitIns_S_R(INS_movups, EA_64BYTE, op1Reg, varNum, 0);
            }
        }
#endif
    }

    public void genSimdUpperRestore(GenTreeIntrinsic node)
    {
#if !TARGET_AMD64 || UNIX_AMD64_ABI || !FEATURE_SIMD
        throw new FatalJitException(CORJIT_SKIPPED, "SIMD upper-lane restores require Windows AMD64 SIMD support.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(node.IntrinsicName == NI_SIMD_UpperRestore);
        var op1 = node.Op1;
        assert(op1.Oper.IsLocal && (op1.Type is TYP_SIMD32 or TYP_SIMD64));
        var srcReg = node.RegNum;
        var lclVarReg = genConsumeReg(op1);
        assert(lclVarReg != REG_NA);

        // The local child carries the destination; the intrinsic carries the saved upper half.
        if (srcReg != REG_NA)
        {
            assert(op1.Type == TYP_SIMD32);
            Emitter.emitIns_R_R_R_I(INS_vinsertf32x4, EA_32BYTE, lclVarReg, lclVarReg, srcReg, 1);
        }
        else
        {
            var varNum = op1.AsLclVarCommon().LclNum;
            ref var varDsc = ref _compiler.lvaGetDesc(varNum);
            assert(varDsc.lvOnFrame);
            if (op1.Type == TYP_SIMD32)
            {
                Emitter.emitIns_R_R_S_I(INS_vinsertf32x4, EA_32BYTE, lclVarReg, lclVarReg, varNum, 16, 1);
            }
            else
            {
                assert(op1.Type == TYP_SIMD64);
                Emitter.emitIns_R_S(INS_movups, EA_64BYTE, lclVarReg, varNum, 0);
            }
        }
#endif
    }
}
