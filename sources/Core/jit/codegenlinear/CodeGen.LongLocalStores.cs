// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if !TARGET_64BIT && !TARGET_WASM
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private void genStoreLongLclVar(GenTreeLclVar tree)
    {
        var lclNum = tree.LclNum;
        ref var varDsc = ref _compiler.lvaGetDesc(lclNum);
        assert(varDsc.Type == TYP_LONG);
        assert(!varDsc.lvPromoted);

        var value = tree.Op1;
        // A contained GT_LONG cannot have COPY or RELOAD between it and its consumer.
        noway_assert(value.Oper is GT_LONG);
        genConsumeRegs(value);

        var loVal = value.AsOp().Op1;
        var hiVal = value.AsOp().Op2;
        noway_assert((loVal.RegNum != REG_NA) && (hiVal.RegNum != REG_NA));

        Emitter.emitIns_S_R(ins_Store(TYP_INT), EA_4BYTE, loVal.RegNum, lclNum, 0);
        Emitter.emitIns_S_R(ins_Store(TYP_INT), EA_4BYTE, hiVal.RegNum, lclNum, (int)TYP_INT.Size);
    }
}
#endif
