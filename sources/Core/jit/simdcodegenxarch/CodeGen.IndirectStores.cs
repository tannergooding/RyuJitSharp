// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH && FEATURE_SIMD
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genStoreIndTypeSimd12(GenTreeStoreInd tree)
    {
#if !TARGET_AMD64 || UNIX_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "SIMD12 indirect stores require Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(tree.Oper is GT_STOREIND);
        assert(GCInfo.gcIsWriteBarrierCandidate(tree) == GCInfo.WriteBarrierForm.WBF_NoBarrier);
        var addr = tree.Addr;
        genConsumeAddress(addr);
        var data = tree.Data;
        var dataReg = genConsumeReg(data);

        if (addr.IsContained && (addr.Oper is GT_LCL_ADDR))
        {
            genEmitStoreLclTypeSimd12(tree, addr.AsLclFld().LclNum, addr.AsLclFld().LclOffs);
            genUpdateLife(tree);
            return;
        }

        Emitter.emitInsStoreInd(INS_movsd_simd, EA_8BYTE, tree);
        if (tree.IsIndirAddrMode)
        {
            var addrMode = addr.AsAddrMode();
            addrMode.Offset = unchecked(addrMode.Offset + 8);
        }
        else if (addr.Oper.IsCnsIntOrI && addr.IsContained)
        {
            var icon = addr.AsIntConCommon();
            assert(!icon.ImmedValNeedsReloc(_compiler));
            icon.IconValue = unchecked(icon.IconValue + 8);
        }
        else
        {
            addr = new GenTreeAddrMode(addr.Type, addr, null, 0, 8) { IsContained = true };
        }
        tree.Addr = addr;

        if (data.IsVectorZero)
        {
            Emitter.emitInsStoreInd(INS_movss, EA_4BYTE, tree);
        }
        else
        {
            var store = new GenTreeStoreInd(TYP_SIMD16, addr, data) { RegNum = REG_NA };
            Emitter.emitIns_A_R_I(INS_extractps, EA_16BYTE, store, dataReg, 2);
        }
#endif
    }
}
#endif
