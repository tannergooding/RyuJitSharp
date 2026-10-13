// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using static RyuJitSharp.emitAttr;
using static RyuJitSharp.Globals;
using static RyuJitSharp.insFlags;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForLclFldArmArch(GenTreeLclFld tree)
    {
        assert(tree.Oper is GT_LCL_FLD);

        var targetType = tree.Type;
        var targetReg = tree.RegNum;
        NYI_IF(targetType is TYP_STRUCT, "GT_LCL_FLD: struct load local field not supported");
        assert(targetReg != regNumberExtensions.NotAvailable);

        var offset = tree.LclOffs;
        var localNumber = tree.LclNum;
        assert((uint)localNumber < _compiler.lvaCount);

        if (Configuration.TARGET_ARM && tree.IsOffsetMisaligned)
        {
            // ARM32 only supports unaligned integer accesses, so floating-point values are
            // loaded through integer registers.
            var addressReg = _internalRegisters.Extract(tree);
            Emitter.emitIns_R_S(INS_lea, EA_PTRSIZE, addressReg, localNumber, offset);

            if (targetType is TYP_FLOAT)
            {
                var floatAsIntReg = _internalRegisters.GetSingle(tree);
                Emitter.emitIns_R_R(INS_ldr, EA_4BYTE, floatAsIntReg, addressReg);
                _ = Emitter.emitIns_Mov(INS_vmov_i2f, EA_4BYTE, targetReg, floatAsIntReg, false);
            }
            else
            {
                var firstHalfReg = _internalRegisters.Extract(tree);
                var secondHalfReg = _internalRegisters.GetSingle(tree);
                Emitter.emitIns_R_R_I(
                    INS_ldr, EA_4BYTE, firstHalfReg, addressReg, 0, INS_FLAGS_DONT_CARE, INS_OPTS_NONE);
                Emitter.emitIns_R_R_I(
                    INS_ldr, EA_4BYTE, secondHalfReg, addressReg, 4, INS_FLAGS_DONT_CARE, INS_OPTS_NONE);
                Emitter.emitIns_R_R_R(INS_vmov_i2d, EA_8BYTE, targetReg, firstHalfReg, secondHalfReg);
            }
        }
        else
        {
            var size = targetType.EmitActualSize;
            var loadInstruction = ins_Load(targetType);
            Emitter.emitIns_R_S(loadInstruction, size, targetReg, localNumber, offset);
        }

        genProduceReg(tree);
    }
}
