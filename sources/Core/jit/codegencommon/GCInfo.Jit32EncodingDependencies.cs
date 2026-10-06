// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_X86 && JIT32_GCENCODER
using System;

namespace RyuJitSharp;

public partial struct GCInfo
{
    // The x86 header's native layout belongs to the unported JIT32 encoder.
    internal struct InfoHdr
    {
    }

    internal readonly bool gcIsUntrackedLocalOrNonEnregisteredArg(int varNum)
    {
        ref var variable = ref Compiler.lvaGetDesc(varNum);

        assert(!Compiler.lvaIsFieldOfDependentlyPromotedStruct(in variable));
        assert(varTypeIsGC(variable.Type));

        if (!variable.lvIsParam)
        {
            assert(!variable.lvPinned || !variable.lvTracked);
            if (variable.lvTracked || !variable.lvOnFrame)
            {
                return false;
            }
        }
        else if (!variable.lvOnFrame)
        {
            if (!Compiler.compJmpOpUsed)
            {
                return false;
            }
        }
        else if (variable.lvIsRegArg && variable.lvTracked)
        {
            return false;
        }

        return true;
    }

    internal readonly void gcCountForHeader(out uint untrackedCount, out uint varPtrTableSize,
        out uint noGCRegionCount)
    {
        var compiler = Compiler;
        untrackedCount = 0;

        for (var varNum = 0; varNum < compiler.lvaCount; varNum++)
        {
            ref var variable = ref compiler.lvaTable[varNum];
            if (compiler.lvaIsFieldOfDependentlyPromotedStruct(in variable))
            {
                continue;
            }

            if (varTypeIsGC(variable.Type))
            {
                if (!gcIsUntrackedLocalOrNonEnregisteredArg(varNum))
                {
                    continue;
                }

#if DEBUG
                if (compiler.verbose)
                {
                    var offset = variable.StackOffset;
                    jitprintf($"GCINFO: untrckd {Globals.varTypeGCstring(variable.Type)} lcl at " +
                        $"[{(_codeGen.IsFramePointerUsed ? STR_FPBASE : STR_SPBASE)}");
                    if (offset < 0)
                    {
                        jitprintf($"-0x{unchecked(-offset):X2}");
                    }
                    else if (offset > 0)
                    {
                        jitprintf($"+0x{offset:X2}");
                    }
                    jitprintf("]\n");
                }
#endif

                untrackedCount = unchecked(untrackedCount + 1);
            }
            else if (variable.Type is TYP_STRUCT && variable.lvOnFrame)
            {
                var layout = variable.Layout
                    ?? throw new InvalidOperationException("A struct local on the frame must have a layout.");
                untrackedCount = unchecked(untrackedCount + (uint)layout.GCPtrCount);
            }
        }

#if DEBUG
        assert(RegSet.tmpGetAllFree());
#endif
        for (var temp = RegSet.tmpListBeg(); temp is not null; temp = RegSet.tmpListNxt(temp))
        {
            if (!varTypeIsGC(temp.tdTempType))
            {
                continue;
            }

#if DEBUG
            if (compiler.verbose)
            {
                var offset = temp.tdTempOffs;
                jitprintf($"GCINFO: untrck {Globals.varTypeGCstring(temp.tdTempType)} Temp at " +
                    $"[{(_codeGen.IsFramePointerUsed ? STR_FPBASE : STR_SPBASE)}");
                if (offset < 0)
                {
                    jitprintf($"-0x{unchecked(-offset):X2}");
                }
                else if (offset > 0)
                {
                    jitprintf($"+0x{offset:X2}");
                }
                jitprintf("]\n");
            }
#endif

            untrackedCount = unchecked(untrackedCount + 1);
        }

#if DEBUG
        if (compiler.verbose)
        {
            jitprintf($"GCINFO: untrckVars = {untrackedCount}\n");
        }
#endif

        varPtrTableSize = 0;
        for (var variable = gcVarPtrList; variable is not null; variable = variable.vpdNext)
        {
            if (variable.vpdBegOfs == variable.vpdEndOfs)
            {
                continue;
            }

            varPtrTableSize = unchecked(varPtrTableSize + 1);
        }

#if DEBUG
        if (compiler.verbose)
        {
            jitprintf($"GCINFO: trackdLcls = {varPtrTableSize}\n");
        }
#endif

        var noGCRegions = 0u;
        if (_codeGen.Interruptible)
        {
            var lastEndOffset = uint.MaxValue;
            _ = _codeGen.Emitter.emitGenNoGCLst((_, offset, size, _, _) =>
            {
                if (lastEndOffset != offset)
                {
                    noGCRegions = unchecked(noGCRegions + 1);
                }

                lastEndOffset = unchecked(offset + size);
                return true;
            }, skipMainPrologsAndEpilogs: true);
        }

        noGCRegionCount = noGCRegions;
    }

    internal static void gcInitEncoderLookupTable()
    {
        throw new FatalJitException(CORJIT_SKIPPED, "JIT32 GC encoder lookup initialization is not ported.");
    }

    internal readonly unsafe nuint gcInfoBlockHdrSave(byte* destination, int write,
        uint codeSize, uint prologSize, uint epilogSize, ref InfoHdr header, ref int cached)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "JIT32 GC header encoding is not ported.");
    }

    internal readonly nuint gcPtrTableSize(InfoHdr header, uint codeSize, ref nuint argTabOffset)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "JIT32 GC pointer-table sizing is not ported.");
    }

    internal readonly unsafe byte* gcPtrTableSave(byte* destination, InfoHdr header,
        uint codeSize, ref nuint argTabOffset)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "JIT32 GC pointer-table encoding is not ported.");
    }

#if DUMP_GC_TABLES
    internal readonly unsafe nuint gcInfoBlockHdrDump(byte* source, ref InfoHdr header, out uint codeSize)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "JIT32 GC header decoding is not ported.");
    }

    internal readonly unsafe nuint gcDumpPtrTable(byte* source, InfoHdr header, uint codeSize)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "JIT32 GC pointer-table decoding is not ported.");
    }
#endif
}
#endif
