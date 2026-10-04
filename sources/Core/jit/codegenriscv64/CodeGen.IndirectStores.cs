// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForStoreInd(GenTreeStoreInd tree)
    {
        assert(tree.Oper is GT_STOREIND);

#if FEATURE_SIMD
        if (tree.Type is TYP_SIMD12)
        {
            genStoreIndTypeSimd12(tree);
            return;
        }
#endif

        var data = tree.Data;
        var addr = tree.Addr;
        var writeBarrierForm = GCInfo.gcIsWriteBarrierCandidate(tree);
        if (writeBarrierForm is not GCInfo.WriteBarrierForm.WBF_NoBarrier)
        {
            genConsumeOperands(tree);

            noway_assert(data.RegNum != REG_WRITE_BARRIER_DST);
            genCopyRegIfNeeded(addr, REG_WRITE_BARRIER_DST);
            genCopyRegIfNeeded(data, REG_WRITE_BARRIER_SRC);
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
                dataReg = REG_R0;
            }
            else
            {
                assert(!data.IsContained);
                dataReg = data.RegNum;
            }

            var type = tree.Type;
            var ins = ins_Store(type);
            if (tree.IsVolatile)
            {
                instGen_MemoryBarrier();
            }

            Emitter.emitInsLoadStoreOp(ins, type.EmitActualSize, dataReg, tree);
        }
    }

#if FEATURE_SIMD
    private void genStoreIndTypeSimd12(GenTreeStoreInd tree)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "RISC-V64 SIMD12 indirect stores are not ported.");
    }
#endif
}
#endif
