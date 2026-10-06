// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForStoreInd(GenTreeStoreInd tree)
    {
        var data = tree.Data;
        var addr = tree.Addr;
        var type = tree.Type;

        assert(tree.Oper is GT_STOREIND);
        assert(!varTypeIsFloating(type) || (type == data.Type));

        var writeBarrierForm = GCInfo.gcIsWriteBarrierCandidate(tree);
        if (writeBarrierForm is not GCInfo.WriteBarrierForm.WBF_NoBarrier)
        {
            genConsumeOperands(tree);

            noway_assert(data.RegNum != REG_ARG_0);

            inst_Mov(addr.Type, REG_ARG_0, addr.RegNum, canSkip: true);
            inst_Mov(data.Type, REG_ARG_1, data.RegNum, canSkip: true);
            genGCWriteBarrier(writeBarrierForm);
        }
        else
        {
            genConsumeAddress(addr);
            if (!data.IsContained)
            {
                genConsumeRegs(data);
            }

            if (tree.IsVolatile)
            {
                instGen_MemoryBarrier(BARRIER_FULL);
            }

            var dataReg = data.RegNum;
            var ins = ins_StoreFromSrc(dataReg, type);
            var attr = type.EmitActualSize;
            Emitter.emitInsLoadStoreOp(ins, attr, dataReg, tree);

            genUpdateLife(tree);
        }
    }
}
#endif
