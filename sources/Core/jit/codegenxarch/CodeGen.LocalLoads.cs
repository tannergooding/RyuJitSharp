// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForLclAddr(GenTreeLclFld tree)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Local address generation requires AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(tree.Oper is GT_LCL_ADDR);
        var targetType = tree.Type;
        var size = targetType.EmitSize;
        var targetReg = tree.RegNum;
        noway_assert(targetType is TYP_BYREF or TYP_I_IMPL);

        Emitter.emitIns_R_S(INS_lea, size, targetReg, tree.LclNum, tree.LclOffs);
        genProduceReg(tree);
#endif
    }

    public void genCodeForLclFld(GenTreeLclFld tree)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Local field generation requires AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(tree.Oper is GT_LCL_FLD);
        var targetType = tree.Type;

#if FEATURE_SIMD
        if (targetType is TYP_SIMD12)
        {
            genLoadLclTypeSimd12(tree);
            return;
        }
#endif

        var targetReg = tree.RegNum;
        noway_assert(targetReg != REG_NA);
        noway_assert(targetType is not TYP_STRUCT);
        var size = targetType.EmitSize;
        var offset = tree.LclOffs;
        var varNum = tree.LclNum;
        assert((uint)varNum < _compiler.lvaCount);

        var loadIns = tree.DontExtend ? INS_mov : ins_Load(targetType);
        Emitter.emitIns_R_S(loadIns, size, targetReg, varNum, offset);
        genProduceReg(tree);
#endif
    }

    public void genCodeForLclVar(GenTreeLclVar tree)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Local variable generation requires AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(tree.Oper is GT_LCL_VAR);
        assert((tree.Flags & GTF_VAR_DEF) == 0);
        ref var varDsc = ref _compiler.lvaGetDesc(tree.LclNum);

        // Register candidates spilled by allocation are reloaded at their use.
        // Non-candidates are loaded here unless consumption owns the reload.
        if (!varDsc.lvIsRegCandidate && !tree.IsMultiReg && ((tree.Flags & GTF_SPILLED) == 0))
        {
            var type = varDsc.GetRegisterType(tree);
            Emitter.emitIns_R_S(ins_Load(type, _compiler.isSIMDTypeLocalAligned(tree.LclNum)),
                type.EmitSize, tree.RegNum, tree.LclNum, 0);
            genProduceReg(tree);
        }
#endif
    }

#if FEATURE_SIMD
    public void genLoadLclTypeSimd12(GenTreeLclVarCommon tree)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "SIMD12 local generation requires AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(tree.Oper is GT_LCL_FLD or GT_LCL_VAR);
        genEmitLoadLclTypeSimd12(tree.RegNum, tree.LclNum, tree.LclOffs);
        genProduceReg(tree);
#endif
    }

    public void genEmitLoadLclTypeSimd12(regNumber targetReg, int lclNum, uint offset)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "SIMD12 stack loads require AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        Emitter.emitIns_R_S(INS_movsd_simd, EA_8BYTE, targetReg, lclNum, unchecked((int)offset));

        // Insert the upper float in lane 2 (0x20) and zero lane 3 (0x08).
        Emitter.emitIns_SIMD_R_R_S_I(INS_insertps, EA_16BYTE, targetReg, targetReg, lclNum,
            unchecked((int)(offset + 8)), 0x28, INS_OPTS_NONE);
#endif
    }
#endif
}
#endif
