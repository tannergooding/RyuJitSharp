// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARMARCH
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genIntrinsic(GenTreeIntrinsic tree)
    {
        var srcNode = tree.Op1;

#if DEBUG
        if (tree.IntrinsicName is (> NI_SYSTEM_MATH_START) and (< NI_SYSTEM_MATH_END))
        {
            assert(varTypeIsFloating(srcNode.Type));
            assert(srcNode.Type == tree.Type);
        }
#endif

        switch (tree.IntrinsicName)
        {
            case NI_System_Math_Abs:
            {
                genConsumeOperands(tree.AsOp());
#if TARGET_ARM64
                Emitter.emitInsBinary(INS_fabs, tree.Type.EmitActualSize, tree, srcNode);
#else
                Emitter.emitInsBinary(INS_vabs, tree.Type.EmitActualSize, tree, srcNode);
#endif
                break;
            }

#if TARGET_ARM64
            case NI_System_Math_Ceiling:
            {
                genConsumeOperands(tree.AsOp());
                Emitter.emitInsBinary(INS_frintp, tree.Type.EmitActualSize, tree, srcNode);
                break;
            }

            case NI_System_Math_Floor:
            {
                genConsumeOperands(tree.AsOp());
                Emitter.emitInsBinary(INS_frintm, tree.Type.EmitActualSize, tree, srcNode);
                break;
            }

            case NI_System_Math_Truncate:
            {
                genConsumeOperands(tree.AsOp());
                Emitter.emitInsBinary(INS_frintz, tree.Type.EmitActualSize, tree, srcNode);
                break;
            }

            case NI_System_Math_Round:
            {
                genConsumeOperands(tree.AsOp());
                Emitter.emitInsBinary(INS_frintn, tree.Type.EmitActualSize, tree, srcNode);
                break;
            }

            case NI_PRIMITIVE_PopCount:
            {
                genConsumeOperands(tree.AsOp());

                var srcReg = srcNode.RegNum;
                var dstReg = tree.RegNum;

                assert(genIsValidIntReg(srcReg));
                assert(genIsValidIntReg(dstReg));

                if (_compiler.compOpportunisticallyDependsOn(InstructionSet_Cssc))
                {
                    // FEAT_CSSC provides a scalar cnt operating directly on general registers.
                    Emitter.emitIns_R_R(INS_cnt, srcNode.Type.EmitActualSize, dstReg, srcReg, INS_OPTS_NONE);
                    break;
                }

                var tmpReg = _internalRegisters.GetSingle(tree);

                assert(genIsValidFloatReg(tmpReg));

                var attr = srcNode.Type.EmitSize;

                if (attr != EA_8BYTE)
                {
                    Emitter.emitIns_R_I(INS_movi, EA_8BYTE, tmpReg, 0, INS_OPTS_8B);
                }
                Emitter.emitIns_R_R_I(INS_ins, attr, tmpReg, srcReg, 0, INS_OPTS_NONE);
                Emitter.emitIns_R_R(INS_cnt, EA_8BYTE, tmpReg, tmpReg, INS_OPTS_8B);
                Emitter.emitIns_R_R(INS_addv, EA_8BYTE, tmpReg, tmpReg, INS_OPTS_8B);
                Emitter.emitIns_R_R_I(INS_umov, attr, dstReg, tmpReg, 0, INS_OPTS_NONE);
                break;
            }

            case NI_PRIMITIVE_TrailingZeroCount:
            {
                genConsumeOperands(tree.AsOp());

                var srcReg = srcNode.RegNum;
                var dstReg = tree.RegNum;

                assert(genIsValidIntReg(srcReg));
                assert(genIsValidIntReg(dstReg));

                if (_compiler.compOpportunisticallyDependsOn(InstructionSet_Cssc))
                {
                    // FEAT_CSSC provides a scalar ctz operating directly on general registers.
                    Emitter.emitIns_R_R(INS_ctz, srcNode.Type.EmitActualSize, dstReg, srcReg, INS_OPTS_NONE);
                    break;
                }

                Emitter.emitIns_R_R(INS_rbit, srcNode.Type.EmitActualSize, dstReg, srcReg, INS_OPTS_NONE);
                Emitter.emitIns_R_R(INS_clz, srcNode.Type.EmitActualSize, dstReg, dstReg, INS_OPTS_NONE);
                break;
            }
#endif // TARGET_ARM64

            case NI_System_Math_Sqrt:
            {
                genConsumeOperands(tree.AsOp());
#if TARGET_ARM64
                Emitter.emitInsBinary(INS_fsqrt, tree.Type.EmitActualSize, tree, srcNode);
#else
                Emitter.emitInsBinary(INS_vsqrt, tree.Type.EmitActualSize, tree, srcNode);
#endif
                break;
            }

#if TARGET_ARM
            case NI_PRIMITIVE_SaturateToInt8:
            {
                // SSAT Rd, #8, Rn - saturate int32 to signed 8-bit range [-128, 127]
                genConsumeOperands(tree.AsOp());
                Emitter.emitIns_R_R_I_I(INS_ssat, EA_4BYTE, tree.RegNum, srcNode.RegNum, 0, 8);
                break;
            }

            case NI_PRIMITIVE_SaturateToInt16:
            {
                // SSAT Rd, #16, Rn - saturate int32 to signed 16-bit range [-32768, 32767]
                genConsumeOperands(tree.AsOp());
                Emitter.emitIns_R_R_I_I(INS_ssat, EA_4BYTE, tree.RegNum, srcNode.RegNum, 0, 16);
                break;
            }

            case NI_PRIMITIVE_SaturateToUInt8:
            {
                // USAT Rd, #8, Rn - saturate int32 to unsigned 8-bit range [0, 255]
                genConsumeOperands(tree.AsOp());
                Emitter.emitIns_R_R_I_I(INS_usat, EA_4BYTE, tree.RegNum, srcNode.RegNum, 0, 8);
                break;
            }

            case NI_PRIMITIVE_SaturateToUInt16:
            {
                // USAT Rd, #16, Rn - saturate int32 to unsigned 16-bit range [0, 65535]
                genConsumeOperands(tree.AsOp());
                Emitter.emitIns_R_R_I_I(INS_usat, EA_4BYTE, tree.RegNum, srcNode.RegNum, 0, 16);
                break;
            }
#endif // TARGET_ARM

#if FEATURE_SIMD
            // The handling is a bit more complex so genSimdUpperSave/Restore
            // handles genConsumeOperands and genProduceReg.
            case NI_SIMD_UpperRestore:
            {
                genSimdUpperRestore(tree);
                return;
            }

            case NI_SIMD_UpperSave:
            {
                genSimdUpperSave(tree);
                return;
            }
#endif // FEATURE_SIMD

            default:
            {
                assert(false, "genIntrinsic: Unsupported intrinsic");
                unreached();
                return;
            }
        }

        genProduceReg(tree);
    }
}
#endif
