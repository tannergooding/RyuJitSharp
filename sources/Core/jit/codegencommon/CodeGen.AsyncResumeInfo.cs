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
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Async resume location recording requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        var index = unchecked((nuint)asyncResume.Val1);
        assert(_compiler.compSuspensionPoints is not null);
        assert(index < (nuint)_compiler.compSuspensionPoints.Count);

        _ = genEmitAsyncResumeInfoTable(out var asyncResumeInfo);
        asyncResumeInfo.Locations[unchecked((int)index)] = new emitLocation(Emitter);
#endif
    }

    public unsafe void genAsyncResumeInfo(GenTreeVal treeNode)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
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
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Async resume table registration requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(_compiler.compSuspensionPoints is not null);

        if (genAsyncResumeInfoTable is null)
        {
            Emitter.emitAsyncResumeTable((uint)_compiler.compSuspensionPoints.Count,
                out genAsyncResumeInfoTableOffset, out genAsyncResumeInfoTable);
        }

        dataSection = genAsyncResumeInfoTable;

        return genAsyncResumeInfoTableOffset;
#endif
    }

    public unsafe CORINFO_FIELD_HANDLE genEmitAsyncResumeInfo(uint stateNum)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Async resume data handles require Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(_compiler.compSuspensionPoints is not null);
        assert(stateNum < (uint)_compiler.compSuspensionPoints.Count);

        var baseOffs = genEmitAsyncResumeInfoTable(out _);

        return Compiler.eeFindJitDataOffs(unchecked(baseOffs + stateNum * (uint)sizeof(CORINFO_AsyncResumeInfo)));
#endif
    }
}
