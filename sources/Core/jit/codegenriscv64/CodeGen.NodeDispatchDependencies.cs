// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genLeaInstruction(GenTreeAddrMode tree)
    {
        genConsumeOperands(tree);

        var emit = GetEmitter();
        var size = tree.Type.EmitSize;
        var offset = tree.Offset;

        assert(tree.HasBaseAddress);
        assert(!tree.HasIndex);
        assert(tree.Scale <= 1);

        var baseAddress = tree.BaseAddress!;
        var memBaseReg = baseAddress.RegNum;
        var targetReg = tree.RegNum;

        if (Emitter.isValidSimm12(offset))
        {
            if ((offset != 0) || (targetReg != memBaseReg))
            {
                emit.emitIns_R_R_I(INS_addi, size, targetReg, memBaseReg, offset);
            }
        }
        else
        {
            var tempReg = InternalRegisters.GetSingle(tree);
            _ = Emitter.emitLoadImmediate(true, EA_PTRSIZE, tempReg, offset);
            emit.emitIns_R_R_R(INS_add, size, targetReg, memBaseReg, tempReg);
        }

        genProduceReg(tree);
    }
}
#endif
