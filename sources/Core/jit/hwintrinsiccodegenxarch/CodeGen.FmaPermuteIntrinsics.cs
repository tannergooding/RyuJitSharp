// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
#if FEATURE_HW_INTRINSICS
    public void genFmaIntrinsic(GenTreeHWIntrinsic node, insOpts instOptions)
    {
#if !TARGET_AMD64 || UNIX_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "FMA generation requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        var intrinsicId = node.HWIntrinsicId;
        assert(HWIntrinsicInfo.IsFmaIntrinsic(intrinsicId));
        var baseType = node.SimdBaseType;
        var attr = Compiler.GetSimdTypeForSize(node.SimdSize).EmitActualSize;
        var form213 = HWIntrinsicInfo.lookupIns(intrinsicId, baseType, _compiler);
        var form132 = (instruction)((int)form213 - 1);
        var form231 = (instruction)((int)form213 + 1);
        var op1 = node.GetOp(1);
        var op2 = node.GetOp(2);
        var op3 = node.GetOp(3);
        var targetReg = node.RegNum;
        var op2NodeReg = op2.RegNum;
        var op3NodeReg = op3.RegNum;
        var emitOp1 = op1;
        var emitOp2 = op2;
        var emitOp3 = op3;
        var copiesUpperBits = HWIntrinsicInfo.CopiesUpperBits(intrinsicId);
        assert(!copiesUpperBits || !op1.IsContained);
        instruction ins;

        // The caller consumes/produces the node. This routine can be reused by every
        // arm of the dynamic embedded-rounding dispatch without mutating its operands.
        if (op1.IsContained || op1.IsUsedFromSpillTemp)
        {
            // 231: accumulator = (register * memory) + accumulator.
            ins = form231;
            (emitOp1, emitOp3) = (emitOp3, emitOp1);
            if (targetReg == op2NodeReg)
            {
                ins = form132;
                (emitOp1, emitOp2) = (emitOp2, emitOp1);
            }
        }
        else if (op3.IsContained || op3.IsUsedFromSpillTemp)
        {
            // 213: destination = (register * destination) + memory.
            // Scalar forms must retain op1 as the source of unmodified upper lanes.
            ins = form213;
            if (!copiesUpperBits && (targetReg == op2NodeReg))
            {
                (emitOp1, emitOp2) = (emitOp2, emitOp1);
            }
        }
        else if (op2.IsContained || op2.IsUsedFromSpillTemp)
        {
            // 132: destination = (destination * memory) + register.
            ins = form132;
            (emitOp2, emitOp3) = (emitOp3, emitOp2);
            if (!copiesUpperBits && (targetReg == op3NodeReg))
            {
                ins = form231;
                (emitOp1, emitOp2) = (emitOp2, emitOp1);
            }
        }
        else
        {
            // With no memory operand, prefer the assigned destination to avoid a copy.
            if (targetReg == op2NodeReg)
            {
                ins = form213;
                (emitOp1, emitOp2) = (emitOp2, emitOp1);
            }
            else if (targetReg == op3NodeReg)
            {
                ins = form231;
                (emitOp1, emitOp3) = (emitOp3, emitOp1);
            }
            else
            {
                ins = form213;
            }
        }

        assert(ins != INS_invalid);
        genHWIntrinsic_R_R_R_RM(ins, attr, targetReg, emitOp1.RegNum, emitOp2.RegNum, emitOp3, instOptions);
#endif
    }

    public void genPermuteVar2x(GenTreeHWIntrinsic node, insOpts instOptions)
    {
#if !TARGET_AMD64 || UNIX_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "PermuteVar2x generation requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        var intrinsicId = node.HWIntrinsicId;
        assert(HWIntrinsicInfo.IsPermuteVar2x(intrinsicId));
        var baseType = node.SimdBaseType;
        var attr = Compiler.GetSimdTypeForSize(node.SimdSize).EmitActualSize;
        var op1 = node.GetOp(1);
        var op2 = node.GetOp(2);
        var op3 = node.GetOp(3);
        var targetReg = node.RegNum;
        genConsumeMultiOpOperands(node);
        var emitOp1 = op1;
        var emitOp2 = op2;
        assert(!op1.IsContained);
        assert(!op2.IsContained);
        var ins = HWIntrinsicInfo.lookupIns(intrinsicId, baseType, _compiler);

        // Keep the index-as-destination choice synchronized with LSRA operand constraints.
        if (targetReg == op2.RegNum)
        {
            (emitOp1, emitOp2) = (emitOp2, emitOp1);
            switch (ins)
            {
                case INS_vpermt2b:
                {
                    ins = INS_vpermi2b;
                    break;
                }
                case INS_vpermt2d:
                {
                    ins = INS_vpermi2d;
                    break;
                }
                case INS_vpermt2pd:
                {
                    ins = INS_vpermi2pd;
                    break;
                }
                case INS_vpermt2ps:
                {
                    ins = INS_vpermi2ps;
                    break;
                }
                case INS_vpermt2q:
                {
                    ins = INS_vpermi2q;
                    break;
                }
                case INS_vpermt2w:
                {
                    ins = INS_vpermi2w;
                    break;
                }
                default:
                {
                    unreached();
                    break;
                }
            }
        }

        assert(ins != INS_invalid);
        genHWIntrinsic_R_R_R_RM(ins, attr, targetReg, emitOp1.RegNum, emitOp2.RegNum, op3, instOptions);
        genProduceReg(node);
#endif
    }

#endif
}
