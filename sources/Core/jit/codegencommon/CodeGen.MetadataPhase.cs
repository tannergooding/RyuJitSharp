// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
using System.Runtime.InteropServices;
#endif

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    internal unsafe void genEmitUnwindDebugGCandEH()
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Final runtime metadata publication requires Windows AMD64.");
#else
#if LATE_DISASM
        RequireSupportedLateDisassembly();
#endif
        assert(_codePtr is not null);
        _compiler.unwindEmit(*_codePtr, _coldCodePtr);
        genIPmappingGen();
        genReportRichDebugInfo();
        genReportAsyncDebugInfo();
        genSetScopeInfo();

#if LATE_DISASM || DEBUG
        uint finalHotCodeSize;
        uint finalColdCodeSize;
        var allocatedHotCodeSize = unchecked((uint)_compiler.info.compTotalHotCodeSize);
        var allocatedColdCodeSize = unchecked((uint)_compiler.info.compTotalColdCodeSize);
        if (_compiler.fgFirstColdBlock is not null)
        {
            // Hot code is padded to its allocation; the final cold section is not.
            assert(_codeSize <= unchecked(allocatedHotCodeSize + allocatedColdCodeSize));
            assert(allocatedHotCodeSize > 0);
            assert(allocatedColdCodeSize > 0);
            finalHotCodeSize = allocatedHotCodeSize;
            finalColdCodeSize = unchecked(_codeSize - finalHotCodeSize);
        }
        else
        {
            assert(_codeSize <= allocatedHotCodeSize);
            assert(allocatedHotCodeSize > 0);
            assert(allocatedColdCodeSize == 0);
            finalHotCodeSize = _codeSize;
            finalColdCodeSize = 0;
        }
#endif
#if LATE_DISASM
        Disassembler.disAsmCode((byte*)*_codePtr, (byte*)_codePtrRW, finalHotCodeSize,
            (byte*)_coldCodePtr, (byte*)_coldCodePtrRW, finalColdCodeSize);
#endif
#if DEBUG
        if (JitConfig.JitRawHexCode.contains(_compiler.info.compMethodHnd, _compiler.info.compClassHnd,
            &_compiler.info.compMethodInfo->args))
        {
            // Native raw-hex output includes only the hot region.
            var dumpAddress = (byte*)_codePtrRW;
            var pathPointer = JitConfig.JitRawHexCodeFile;
            if (pathPointer is not null)
            {
                var path = Marshal.PtrToStringUTF8((nint)pathPointer)
                    ?? throw new FatalJitException(CORJIT_SKIPPED, "Raw-hex output file path is invalid.");
                using var file = OpenRichDebugInfoFile(path);
                if (file is not null)
                {
                    using var writer = new JitTextWriter(file, leaveOpen: true);
                    hexDump(writer, dumpAddress, finalHotCodeSize);
                }
            }
            else
            {
                var writer = jitstdout();
                writer.Write($"Generated native code for {_compiler.info.compFullName}:\n");
                hexDump(writer, dumpAddress, finalHotCodeSize);
                writer.Write("\n\n");
            }
        }
#endif
        genReportEH();
        genCreateAndStoreGCInfo(_codeSize, _prologSize, _epilogSize
#if DEBUG
            , _codePtr
#endif
            );
        _compiler.Metrics.GCInfoBytes = unchecked((int)_compiler.compInfoBlkSize);
        Emitter.emitEndFN();
        RegSet.rsSpillDone();
        RegSet.tmpDone();

#if DISPLAY_SIZES
        var dataSize = unchecked((nuint)(uint)Emitter.emitDataSize());
        grossVMsize = unchecked(grossVMsize + (uint)_compiler.info.compILCodeSize);
        totalNCsize = unchecked(totalNCsize + _codeSize + dataSize + (nuint)_compiler.compInfoBlkSize);
        grossNCsize = unchecked(grossNCsize + _codeSize + dataSize);
#endif
#endif
    }
}
