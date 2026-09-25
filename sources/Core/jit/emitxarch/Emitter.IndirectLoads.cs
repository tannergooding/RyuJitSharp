// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitInsLoadInd(instruction ins, emitAttr attr, regNumber dstReg, GenTreeIndir mem)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Indirect-load recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        assert(mem.Oper is GT_IND or GT_NULLCHECK);
        var addr = mem.Addr;

        if (addr.IsContained && (addr.Oper is GT_LCL_ADDR))
        {
            var varNode = addr.AsLclVarCommon();
            emitIns_R_S(ins, attr, dstReg, varNode.LclNum, varNode.LclOffs);

            // Preserve the native local-address lifetime update; its TODO questions
            // whether the indirection should be updated instead (B217).
            codeGen.genUpdateLife(varNode);
            return;
        }

        assert((addr.Oper is GT_LEA) || (addr.Oper.IsCnsIntOrI && addr.IsContained) || !addr.IsContained);
        var id = emitNewInstrAmd(attr, mem.Offset);
        id.idIns(ins);
        id.idReg1(dstReg);
        emitHandleMemOp(mem, id, emitInsModeFormat(ins, IF_RRD_ARD), ins);
        var size = emitInsSizeAM(id, insCodeRM(ins));
        id.idCodeSize(size);

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)size);
#endif
    }
}
