// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    // SIMD upper halves are volatile across calls, so this intrinsic preserves the live upper lane.
    public void genSimdUpperSaveArm64(GenTreeIntrinsic node)
    {
        if (!Configuration.TARGET_ARM64 || !Configuration.FEATURE_SIMD)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
                "ARM64 SIMD upper-lane saves are unavailable for this target.");
            return;
        }

        assert(node.IntrinsicName == NI_SIMD_UpperSave);

        var op1 = node.Op1;
        assert(op1.Oper.IsLocal);

        var lclNode = op1.AsLclVar();
        ref var varDsc = ref _compiler.lvaGetDesc(lclNode.LclNum);
        assert(emitTypeSize(varDsc.GetRegisterType(lclNode)) == EA_16BYTE);

        var targetReg = node.RegNum;
        assert(targetReg != REG_ARM64_NA);

        var op1Reg = genConsumeReg(op1);
        assert(op1Reg != REG_ARM64_NA);

        Emitter.emitIns_R_R_I_I(INS_mov, EA_8BYTE, targetReg, op1Reg, 0, 1);

        if ((node.Flags & GTF_SPILL) != 0)
        {
            // This is an upper-lane spill into the local's home, not a full-register spill.
            var varNum = lclNode.LclNum;
            assert(varDsc.lvOnFrame);

            const int offset = 8;
            var attr = emitTypeSize(TYP_SIMD8);
            Emitter.emitIns_S_R(INS_str, attr, targetReg, varNum, offset);
        }
        else
        {
            genProduceReg(node);
        }
    }

    // The local child owns the destination; the intrinsic register contains the saved upper lane.
    public void genSimdUpperRestoreArm64(GenTreeIntrinsic node)
    {
        if (!Configuration.TARGET_ARM64 || !Configuration.FEATURE_SIMD)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
                "ARM64 SIMD upper-lane restores are unavailable for this target.");
            return;
        }

        assert(node.IntrinsicName == NI_SIMD_UpperRestore);

        var op1 = node.Op1;
        assert(op1.Oper.IsLocal);

        var lclNode = op1.AsLclVar();
        ref var varDsc = ref _compiler.lvaGetDesc(lclNode.LclNum);
        assert(emitTypeSize(varDsc.GetRegisterType(lclNode)) == EA_16BYTE);

        var srcReg = node.RegNum;
        assert(srcReg != REG_ARM64_NA);

        var lclVarReg = genConsumeReg(lclNode);
        assert(lclVarReg != REG_ARM64_NA);

        var varNum = lclNode.LclNum;

        if ((node.Flags & GTF_SPILLED) != 0)
        {
            // Reload the saved lane from the upper half of the local's stack home.
            assert(varDsc.lvOnFrame);

            const int offset = 8;
            var attr = emitTypeSize(TYP_SIMD8);
            Emitter.emitIns_R_S(INS_ldr, attr, srcReg, varNum, offset);
        }

        Emitter.emitIns_R_R_I_I(INS_mov, EA_8BYTE, lclVarReg, srcReg, 1, 0);
    }
}
