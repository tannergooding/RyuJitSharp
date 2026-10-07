// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genRangeCheck(GenTree oper)
    {
        noway_assert(oper.Oper is GT_BOUNDS_CHECK);
        var boundsCheck = oper.AsBoundsChk();

        var index = boundsCheck.Index;
        var length = boundsCheck.ArrayLength;
        var indexReg = index.RegNum;
        var lengthReg = length.RegNum;

        genConsumeRegs(index);
        genConsumeRegs(length);

        if (genActualType(length) is TYP_INT)
        {
            var tempReg = InternalRegisters.Extract(oper);
            GetEmitter().emitIns_R_R(INS_sext_w, EA_4BYTE, tempReg, lengthReg);
            lengthReg = tempReg;
        }

        if (genActualType(index) is TYP_INT)
        {
            var tempReg = InternalRegisters.GetSingle(oper);
            GetEmitter().emitIns_R_R(INS_sext_w, EA_4BYTE, tempReg, indexReg);
            indexReg = tempReg;
        }

#if DEBUG
        var lengthType = genActualType(length);
        var indexType = genActualType(index);
        assert(lengthType is TYP_INT or TYP_LONG);
        assert(indexType is TYP_INT or TYP_LONG);
#endif

        genJumpToThrowHlpBlk_la(boundsCheck.ThrowKind, INS_bgeu, indexReg, null, lengthReg);
    }
}
#endif
