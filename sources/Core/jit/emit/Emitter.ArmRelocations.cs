// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
namespace RyuJitSharp;

public partial class Emitter
{
#if EMITTER_STATS
    private static uint emitTotalIDescRelocCnt;
#endif

    private unsafe instrDescReloc emitNewInstrReloc(emitAttr attr, nuint address)
    {
        assert(EA_IS_RELOC(attr));

        var id = emitAllocAnyInstr<instrDescReloc>(
            unchecked((nuint)(INSTR_DESC_SIZE + System.IntPtr.Size)), attr);
        id.idSetRelocFlags(attr);
        assert(id.idIsReloc());
        id.idrRelocVal = (byte*)address;

#if EMITTER_STATS
        emitTotalIDescRelocCnt = unchecked(emitTotalIDescRelocCnt + 1);
#endif

        return id;
    }

    private unsafe byte* emitGetInsRelocValue(instrDesc id)
    {
        return ((instrDescReloc)id).idrRelocVal;
    }

    // Relative-code-relocation mode lets the EE record the PC-relative Thumb MOV32 relocation.
    // Otherwise the emitter requests the absolute relocation used for compilation-time fixup.
    public unsafe void emitHandlePCRelativeMov32(void* location, void* target)
    {
        assert(_compiler is not null);

        if (_compiler.opts.jitFlags->IsSet(JitFlags.JIT_FLAG_RELATIVE_CODE_RELOCS))
        {
            emitRecordRelocation(location, target, CorInfoReloc.ARM32_THUMB_MOV32_PCREL);
        }
        else
        {
            emitRecordRelocation(location, target, CorInfoReloc.ARM32_THUMB_MOV32);
        }
    }
}
#endif
