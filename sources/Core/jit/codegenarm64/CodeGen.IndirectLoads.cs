// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
#if FEATURE_SIMD
    public void genLoadIndTypeSimd12(GenTreeIndir treeNode)
    {
        assert(treeNode.Oper is GT_IND);

        var addr = treeNode.Addr;
        assert(!addr.IsContained);

        var targetReg = treeNode.RegNum;
        var addrReg = genConsumeReg(addr);
        var tmpReg = InternalRegisters.GetSingle(treeNode);

        Emitter.emitIns_R_R(INS_ldr, EA_8BYTE, targetReg, addrReg);
        Emitter.emitIns_R_R_I(INS_ldr, EA_4BYTE, tmpReg, addrReg, 8);

        // Keep the load within 12 bytes; the final word is inserted into SIMD lane 2.
        Emitter.emitIns_R_R_I(INS_mov, EA_4BYTE, targetReg, tmpReg, 2);

        genProduceReg(treeNode);
    }
#endif
}
#endif
