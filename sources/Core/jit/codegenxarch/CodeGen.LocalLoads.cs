// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForLclFldXarch(GenTreeLclFld tree)
    {
        Emitter.RequireSupportedInstructionRecording();
        assert(tree.Oper is GT_LCL_FLD);
        var targetType = tree.Type;

        if (Configuration.FEATURE_SIMD && (targetType is TYP_SIMD12))
        {
            genLoadLclTypeSimd12Xarch(tree);
            return;
        }
        var targetReg = tree.RegNum;
        noway_assert(targetReg != regNumberExtensions.NotAvailable);
        noway_assert(targetType is not TYP_STRUCT);
        var size = targetType.EmitSize;
        var offset = tree.LclOffs;
        var varNum = tree.LclNum;
        assert((uint)varNum < _compiler.lvaCount);

        var loadIns = tree.DontExtend ? INS_mov : ins_Load(targetType);
        Emitter.emitIns_R_S(loadIns, size, targetReg, varNum, offset);
        genProduceReg(tree);
    }

    public void genCodeForLclVarXarch(GenTreeLclVar tree)
    {
        Emitter.RequireSupportedInstructionRecording();
        assert(tree.Oper is GT_LCL_VAR);
        assert((tree.Flags & GTF_VAR_DEF) == 0);
        ref var varDsc = ref _compiler.lvaGetDesc(tree.LclNum);

        // Register candidates spilled by allocation are reloaded at their use.
        // Non-candidates are loaded here unless consumption owns the reload.
        if (!varDsc.lvIsRegCandidate && !tree.IsMultiReg && ((tree.Flags & GTF_SPILLED) == 0))
        {
            if (Configuration.FEATURE_SIMD && Configuration.TARGET_X86 && (tree.Type is TYP_SIMD12))
            {
                genLoadLclTypeSimd12Xarch(tree);
                return;
            }
            var type = varDsc.GetRegisterType(tree);
            Emitter.emitIns_R_S(ins_Load(type, _compiler.isSIMDTypeLocalAligned(tree.LclNum)),
                type.EmitSize, tree.RegNum, tree.LclNum, 0);
            genProduceReg(tree);
        }
    }

    public void genLoadLclTypeSimd12Xarch(GenTreeLclVarCommon tree)
    {
        if (!Configuration.FEATURE_SIMD)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "SIMD12 local loads require SIMD support.");
            return;
        }

        Emitter.RequireSupportedInstructionRecording();
        assert(tree.Oper is GT_LCL_FLD or GT_LCL_VAR);
        genEmitLoadLclTypeSimd12(tree.RegNum, tree.LclNum, tree.LclOffs);
        genProduceReg(tree);
    }

    public void genEmitLoadLclTypeSimd12(regNumber targetReg, int lclNum, uint offset)
    {
        if (!Configuration.FEATURE_SIMD)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "SIMD12 local loads require SIMD support.");
            return;
        }

        Emitter.RequireSupportedInstructionRecording();
        Emitter.emitIns_R_S(INS_movsd_simd, EA_8BYTE, targetReg, lclNum, unchecked((int)offset));

        // Insert the upper float in lane 2 (0x20) and zero lane 3 (0x08).
        Emitter.emitIns_SIMD_R_R_S_I(INS_insertps, EA_16BYTE, targetReg, targetReg, lclNum,
            unchecked((int)(offset + 8)), 0x28, INS_OPTS_NONE);
    }
}
