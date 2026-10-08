// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private Emitter.dataSection? genAsyncResumeInfoTable;
    private uint genAsyncResumeInfoTableOffset = uint.MaxValue;

    public void genRecordAsyncResume(GenTreeVal asyncResume)
    {
        var index = unchecked((nuint)asyncResume.Val1);
        assert(_compiler.compSuspensionPoints is not null);
        assert(index < (nuint)_compiler.compSuspensionPoints.Count);

        _ = genEmitAsyncResumeInfoTable(out var asyncResumeInfo);
        asyncResumeInfo.Locations[unchecked((int)index)] = new emitLocation(Emitter);
    }

    public unsafe void genAsyncResumeInfo(GenTreeVal treeNode)
    {
#if TARGET_WASM
        assert(treeNode.Oper is GT_ASYNC_RESUME_INFO);
        assert(treeNode.Type is TYP_I_IMPL);

        var field = genEmitAsyncResumeInfo(unchecked((uint)treeNode.Val1));
        assert(Compiler.eeIsJitDataOffs(field));
        var dataOffset = Compiler.eeGetJitDataOffs(field);
        assert(dataOffset >= 0);

        GetEmitter().emitDataOffsetConstant(unchecked((nuint)dataOffset));
        WasmProduceReg(treeNode);
#elif TARGET_LOONGARCH64
        // INS_bl/b are placeholders that is not the final instruction.
        var ins = INS_bl;
        var attr = EA_PTRSIZE;
        if (_compiler.eeDataWithCodePointersNeedsRelocs())
        {
            ins = INS_b;
            attr = EA_SET_FLG(EA_PTRSIZE, EA_CNS_RELOC_FLG);
        }

        GetEmitter().emitIns_R_C(ins, attr, treeNode.RegNum, REG_NA,
            genEmitAsyncResumeInfo(unchecked((uint)treeNode.Val1)), 0);
        genProduceReg(treeNode);
#elif TARGET_RISCV64
        var attr = EA_PTRSIZE;
        if (_compiler.eeDataWithCodePointersNeedsRelocs())
        {
            attr = EA_SET_FLG(EA_PTRSIZE, EA_CNS_RELOC_FLG);
        }

        GetEmitter().emitIns_R_C(INS_addi, attr, treeNode.RegNum, REG_NA,
            genEmitAsyncResumeInfo(unchecked((uint)treeNode.Val1)));
        genProduceReg(treeNode);
#elif TARGET_ARM64
        var attr = EA_PTRSIZE;
        if (_compiler.eeDataWithCodePointersNeedsRelocs())
        {
            attr = EA_SET_FLG(EA_PTRSIZE, EA_CNS_RELOC_FLG);
        }

        Emitter.emitIns_R_C(INS_adr, attr, treeNode.RegNum, REG_NA,
            genEmitAsyncResumeInfo(unchecked((uint)treeNode.Val1)), 0);
        genProduceReg(treeNode);
#elif TARGET_ARM
        var field = genEmitAsyncResumeInfo(unchecked((uint)treeNode.Val1));
        assert(Compiler.eeIsJitDataOffs(field));
        genMov32RelocatableDataLabel(unchecked((uint)Compiler.eeGetJitDataOffs(field)), treeNode.RegNum);
        genProduceReg(treeNode);
#elif !TARGET_XARCH
        throw new FatalJitException(CORJIT_SKIPPED, "Async resume address generation requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        Emitter.emitIns_R_C(INS_lea, TYP_I_IMPL.EmitSize, treeNode.RegNum,
            genEmitAsyncResumeInfo(unchecked((uint)treeNode.Val1)), 0);
        genProduceReg(treeNode);
#endif
    }

    public uint genEmitAsyncResumeInfoTable(out Emitter.dataSection dataSection)
    {
        assert(_compiler.compSuspensionPoints is not null);

        if (genAsyncResumeInfoTable is null)
        {
            Emitter.emitAsyncResumeTable((uint)_compiler.compSuspensionPoints.Count,
                out genAsyncResumeInfoTableOffset, out genAsyncResumeInfoTable);
        }

        dataSection = genAsyncResumeInfoTable;

        return genAsyncResumeInfoTableOffset;
    }

    public unsafe CORINFO_FIELD_HANDLE genEmitAsyncResumeInfo(uint stateNum)
    {
        assert(_compiler.compSuspensionPoints is not null);
        assert(stateNum < (uint)_compiler.compSuspensionPoints.Count);

        var baseOffs = genEmitAsyncResumeInfoTable(out _);

        return Compiler.eeFindJitDataOffs(unchecked(baseOffs + stateNum * (uint)sizeof(CORINFO_AsyncResumeInfo)));
    }
}
