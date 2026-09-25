// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial class Emitter
{
    public unsafe byte emitOutputByte(byte* dst, long val)
    {
        *(dst + writeableOffset) = unchecked((byte)val);
#if DEBUG && TARGET_AMD64
        assert(((val & 0xFF00000000L) == 0) || ((val & -0x100000000L) == -0x100000000L));
#endif
        return sizeof(byte);
    }

    public unsafe byte emitOutputWord(byte* dst, long val)
    {
        Unsafe.WriteUnaligned(dst + writeableOffset, unchecked((short)val));
#if DEBUG && TARGET_AMD64
        assert(((val & 0xFF00000000L) == 0) || ((val & -0x100000000L) == -0x100000000L));
#endif
        return sizeof(short);
    }

    public unsafe byte emitOutputLong(byte* dst, long val)
    {
        Unsafe.WriteUnaligned(dst + writeableOffset, unchecked((int)val));
#if DEBUG && TARGET_AMD64
        assert(((val & 0xFF00000000L) == 0) || ((val & -0x100000000L) == -0x100000000L));
#endif
        return sizeof(int);
    }

    public unsafe byte emitOutputSizeT(byte* dst, long val)
    {
#if TARGET_64BIT
        Unsafe.WriteUnaligned(dst + writeableOffset, val);
#else
        Unsafe.WriteUnaligned(dst + writeableOffset, unchecked((int)val));
#endif
        return TARGET_POINTER_SIZE;
    }

    public unsafe void emitRecordRelocation(void* location, void* target, CorInfoReloc relocType, int addlDelta = 0,
        [CallerArgumentExpression(nameof(relocType))] string relocTypeName = "")
    {
        assert(_compiler is not null);
        var locationRW = (byte*)location + writeableOffset;
        JITDUMP($"recordRelocation: {FMT_PTR((void*)dspPtr(location))} (rw: {FMT_PTR((void*)dspPtr(locationRW))}) => {FMT_PTR((void*)dspPtr(target))}, type {(uint)relocType} ({relocTypeName.Replace("CorInfoReloc.", "CorInfoReloc::", StringComparison.Ordinal)}), delta {addlDelta}\n");

        // Unmatched AltJITs retain late-disassembly records without notifying the VM.
        if (_compiler.info.compMatchedVM)
        {
            emitCmpHandle->recordRelocation(location, locationRW, target, relocType, addlDelta);
        }
#if LATE_DISASM
        codeGen.Disassembler.disRecordRelocation((nuint)location, (nuint)target);
#endif
    }
}
