// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForBswap(GenTree tree)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Byte-swap node generation requires AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(tree.Oper is GT_BSWAP or GT_BSWAP16);
        var targetReg = tree.RegNum;
        var targetType = tree.Type;
        var operand = tree.AsUnOp().Op1;
        genConsumeRegs(operand);

        if (operand.IsUsedFromReg)
        {
            inst_Mov(targetType, targetReg, operand.RegNum, canSkip: true);
            if (tree.Oper is GT_BSWAP)
            {
                inst_RV(INS_bswap, targetReg, targetType);
            }
            else
            {
                inst_RV_IV(INS_ror_N, targetReg, 8, EA_2BYTE);
            }
        }
        else
        {
            var needsEvex = false;
            if (Emitter.IsExtendedGPReg(targetReg))
            {
                needsEvex = true;
            }
            else if (operand.Oper.IsIndir)
            {
                var indir = operand.AsIndir();
                if (indir.HasBase && Emitter.IsExtendedGPReg(indir.Base.RegNum))
                {
                    needsEvex = true;
                }
                else if (indir.HasIndex && Emitter.IsExtendedGPReg(indir.Index.RegNum))
                {
                    needsEvex = true;
                }
            }

            var ins = needsEvex ? INS_movbe_apx : INS_movbe;
            _ = Emitter.emitInsBinary(ins, operand.Type.EmitSize, tree, operand);
        }

        if ((tree.Oper is GT_BSWAP16) && !genCanOmitNormalizationForBswap16(tree))
        {
            _ = Emitter.emitIns_Mov(INS_movzx, EA_2BYTE, targetReg, targetReg, canSkip: false);
        }

        genProduceReg(tree);
#endif
    }
}
#endif
