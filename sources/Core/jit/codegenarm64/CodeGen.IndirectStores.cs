// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForStoreIndArm64(GenTreeStoreInd tree)
    {
        assert(tree.Oper is GT_STOREIND);

        if (Configuration.FEATURE_SIMD && tree.Type is TYP_SIMD12)
        {
            genStoreIndTypeSimd12Arm64(tree);
            return;
        }

        var data = tree.Data;
        var addr = tree.Addr;
        var writeBarrierForm = GCInfo.gcIsWriteBarrierCandidate(tree);
        if (writeBarrierForm is not GCInfo.WriteBarrierForm.WBF_NoBarrier)
        {
            genConsumeOperands(tree);

            noway_assert(data.RegNum != REG_WRITE_BARRIER_DST_ARM64);
            genCopyRegIfNeeded(addr, REG_WRITE_BARRIER_DST_ARM64);
            genCopyRegIfNeeded(data, REG_WRITE_BARRIER_SRC_ARM64);
            genGCWriteBarrier(writeBarrierForm);
        }
        else
        {
            genConsumeAddress(addr);
            if (!data.IsContained)
            {
                genConsumeRegs(data);
            }

            regNumber dataReg;
            if (data.IsContainedIntOrIImmed)
            {
                assert(data.IsIntegralConst(0));
                dataReg = REG_ARM64_ZR;
            }
            else
            {
                assert(!data.IsContained);
                dataReg = data.RegNum;
            }

            var type = tree.Type;
            var ins = ins_StoreFromSrc(dataReg, type);
            var attr = emitActualTypeSize(type);

            if (varTypeUsesMaskReg(type))
            {
                attr = EA_SCALABLE_ARM64;
            }

            if (tree.IsVolatile)
            {
                var needsBarrier = true;
                ins = genGetVolatileLdStIns(ins, dataReg, tree, ref needsBarrier);

                if (needsBarrier)
                {
                    instGen_MemoryBarrier(BARRIER_FULL);
                }
            }

            Emitter.emitInsLoadStoreOpArm64(ins, attr, dataReg, tree);
            genUpdateLife(tree);
        }
    }

    public void genStoreIndTypeSimd12Arm64(GenTreeStoreInd treeNode)
    {
        if (!Configuration.TARGET_ARM64 || !Configuration.FEATURE_SIMD)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
                "ARM64 SIMD12 indirect stores are unavailable for this target.");
            return;
        }

        assert(treeNode.Oper is GT_STOREIND);
        assert(GCInfo.gcIsWriteBarrierCandidate(treeNode) == GCInfo.WriteBarrierForm.WBF_NoBarrier);

        var addr = treeNode.Addr;
        assert(!addr.IsContained);

        var data = treeNode.Data;
        assert(!data.IsContained);

        var addrReg = genConsumeReg(addr);
        var dataReg = genConsumeReg(data);
        var tmpReg = InternalRegisters.GetSingle(treeNode);

        Emitter.emitIns_R_R(INS_str, EA_8BYTE, dataReg, addrReg);
        // The remaining upper 32 bits occupy SIMD lane 2 and must be extracted to a GPR.
        Emitter.emitIns_R_R_IArm64(INS_mov, EA_4BYTE, tmpReg, dataReg, 2);
        Emitter.emitIns_R_R_IArm64(INS_str, EA_4BYTE, tmpReg, addrReg, 8);
    }
}
