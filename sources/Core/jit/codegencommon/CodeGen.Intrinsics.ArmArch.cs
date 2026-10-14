// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.emitAttr;
using static RyuJitSharp.Globals;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.instruction;
using static RyuJitSharp.ManagedInstructionSet;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genIntrinsicArmArch(GenTreeIntrinsic tree)
    {
        if (!Configuration.TARGET_ARMARCH)
        {
            throw new FatalJitException(CORJIT_SKIPPED,
                "ARM architecture intrinsic generation is unavailable for this target.");
        }

        var srcNode = tree.Op1;

        if (Configuration.DEBUG && tree.IntrinsicName is (> NI_SYSTEM_MATH_START) and (< NI_SYSTEM_MATH_END))
        {
            assert(varTypeIsFloating(srcNode.Type));
            assert(srcNode.Type == tree.Type);
        }

        switch (tree.IntrinsicName)
        {
            case NI_System_Math_Abs:
            {
                genConsumeOperands(tree.AsOp());
                var instruction = Configuration.TARGET_ARM64 ? INS_fabs : INS_vabs;
                _ = Emitter.emitInsBinary(instruction, tree.Type.EmitActualSize, tree, srcNode);
                break;
            }

            case NI_System_Math_Ceiling:
            {
                if (!Configuration.TARGET_ARM64)
                {
                    throw new FatalJitException(CORJIT_SKIPPED, "Ceiling intrinsic generation requires ARM64.");
                }

                genConsumeOperands(tree.AsOp());
                _ = Emitter.emitInsBinary(INS_frintp, tree.Type.EmitActualSize, tree, srcNode);
                break;
            }

            case NI_System_Math_Floor:
            {
                if (!Configuration.TARGET_ARM64)
                {
                    throw new FatalJitException(CORJIT_SKIPPED, "Floor intrinsic generation requires ARM64.");
                }

                genConsumeOperands(tree.AsOp());
                _ = Emitter.emitInsBinary(INS_frintm, tree.Type.EmitActualSize, tree, srcNode);
                break;
            }

            case NI_System_Math_Truncate:
            {
                if (!Configuration.TARGET_ARM64)
                {
                    throw new FatalJitException(CORJIT_SKIPPED, "Truncate intrinsic generation requires ARM64.");
                }

                genConsumeOperands(tree.AsOp());
                _ = Emitter.emitInsBinary(INS_frintz, tree.Type.EmitActualSize, tree, srcNode);
                break;
            }

            case NI_System_Math_Round:
            {
                if (!Configuration.TARGET_ARM64)
                {
                    throw new FatalJitException(CORJIT_SKIPPED, "Round intrinsic generation requires ARM64.");
                }

                genConsumeOperands(tree.AsOp());
                _ = Emitter.emitInsBinary(INS_frintn, tree.Type.EmitActualSize, tree, srcNode);
                break;
            }

            case NI_PRIMITIVE_PopCount:
            {
                if (!Configuration.TARGET_ARM64)
                {
                    throw new FatalJitException(CORJIT_SKIPPED, "PopCount intrinsic generation requires ARM64.");
                }

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
                if (!Configuration.TARGET_ARM64)
                {
                    throw new FatalJitException(CORJIT_SKIPPED, "TrailingZeroCount intrinsic generation requires ARM64.");
                }

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

            case NI_System_Math_Sqrt:
            {
                genConsumeOperands(tree.AsOp());
                var instruction = Configuration.TARGET_ARM64 ? INS_fsqrt : INS_vsqrt;
                _ = Emitter.emitInsBinary(instruction, tree.Type.EmitActualSize, tree, srcNode);
                break;
            }

            case NI_PRIMITIVE_SaturateToInt8:
            {
                if (!Configuration.TARGET_ARM)
                {
                    throw new FatalJitException(CORJIT_SKIPPED, "SaturateToInt8 intrinsic generation requires ARM.");
                }

                // SSAT Rd, #8, Rn - saturate int32 to signed 8-bit range [-128, 127]
                genConsumeOperands(tree.AsOp());
                Emitter.emitIns_R_R_I_I(INS_ssat, EA_4BYTE, tree.RegNum, srcNode.RegNum, 0, 8);
                break;
            }

            case NI_PRIMITIVE_SaturateToInt16:
            {
                if (!Configuration.TARGET_ARM)
                {
                    throw new FatalJitException(CORJIT_SKIPPED, "SaturateToInt16 intrinsic generation requires ARM.");
                }

                // SSAT Rd, #16, Rn - saturate int32 to signed 16-bit range [-32768, 32767]
                genConsumeOperands(tree.AsOp());
                Emitter.emitIns_R_R_I_I(INS_ssat, EA_4BYTE, tree.RegNum, srcNode.RegNum, 0, 16);
                break;
            }

            case NI_PRIMITIVE_SaturateToUInt8:
            {
                if (!Configuration.TARGET_ARM)
                {
                    throw new FatalJitException(CORJIT_SKIPPED, "SaturateToUInt8 intrinsic generation requires ARM.");
                }

                // USAT Rd, #8, Rn - saturate int32 to unsigned 8-bit range [0, 255]
                genConsumeOperands(tree.AsOp());
                Emitter.emitIns_R_R_I_I(INS_usat, EA_4BYTE, tree.RegNum, srcNode.RegNum, 0, 8);
                break;
            }

            case NI_PRIMITIVE_SaturateToUInt16:
            {
                if (!Configuration.TARGET_ARM)
                {
                    throw new FatalJitException(CORJIT_SKIPPED, "SaturateToUInt16 intrinsic generation requires ARM.");
                }

                // USAT Rd, #16, Rn - saturate int32 to unsigned 16-bit range [0, 65535]
                genConsumeOperands(tree.AsOp());
                Emitter.emitIns_R_R_I_I(INS_usat, EA_4BYTE, tree.RegNum, srcNode.RegNum, 0, 16);
                break;
            }

            // The handling is a bit more complex so genSimdUpperSave/Restore
            // handles genConsumeOperands and genProduceReg.
            case NI_SIMD_UpperRestore:
            {
                if (!Configuration.FEATURE_SIMD)
                {
                    throw new FatalJitException(CORJIT_SKIPPED, "SIMD upper-register restoration is unsupported.");
                }

                if (Configuration.TARGET_ARM64)
                {
                    genSimdUpperRestoreArm64(tree);
                }
                else
                {
                    genSimdUpperRestore(tree);
                }
                return;
            }

            case NI_SIMD_UpperSave:
            {
                if (!Configuration.FEATURE_SIMD)
                {
                    throw new FatalJitException(CORJIT_SKIPPED, "SIMD upper-register saving is unsupported.");
                }

                if (Configuration.TARGET_ARM64)
                {
                    genSimdUpperSaveArm64(tree);
                }
                else
                {
                    genSimdUpperSave(tree);
                }
                return;
            }

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
