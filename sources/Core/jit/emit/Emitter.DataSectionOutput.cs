// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
using System;

namespace RyuJitSharp;

public partial class Emitter
{
    public unsafe void emitOutputDataSec(dataSecDsc sec, AllocMemChunk* chunks)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Data-section output requires Windows AMD64.");
#else
        var compiler = _compiler ?? throw new FatalJitException("Data-section output requires an active compiler.");
#if DEBUG
        if (compiler.verbose)
        {
            jitprintf($"\nEmitting data sections: {sec.dsdOffs} total bytes\n");
        }

        var secNum = 0u;
#endif
        if (compiler.opts.disAsm)
        {
            emitDispDataSec(sec, chunks);
        }

        assert(sec.dsdOffs != 0);
        assert(sec.dsdList is not null);
        if (sec.dsdOffs == 0 || sec.dsdList is null || chunks is null)
        {
            throw new FatalJitException("Data-section output requires allocated chunks and nonempty sections.");
        }

        var chunk = chunks;
        for (var dsc = sec.dsdList; dsc is not null; dsc = dsc.dsNext, chunk++)
        {
            var dscSize = dsc.dsSize;
            var dstRW = chunk->blockRW;

            if (dsc.dsType == dataSection.sectionType.blockAbsoluteAddr)
            {
#if DEBUG
                if (compiler.verbose)
                {
                    JITDUMP($"  section {secNum++}, size {dscSize}, block absolute addr\n");
                }
#endif
                assert(dscSize != 0 && dscSize % TARGET_POINTER_SIZE == 0);
                var numElems = dscSize / TARGET_POINTER_SIZE;
                var bDstRW = (nuint*)dstRW;
                for (uint i = 0; i < numElems; i++)
                {
                    var block = dsc.Blocks[i]
                        ?? throw new FatalJitException("An absolute-label table requires a block for every entry.");
                    var lab = emitCodeGetCookie(block)
                        ?? throw new FatalJitException("An absolute-label table requires an emitted block label.");
                    var target = emitOffsetToPtr(lab.igOffs);

                    bDstRW[i] = (nuint)target;
                    if (compiler.opts.compReloc)
                    {
                        emitRecordRelocation(&bDstRW[i], target, CorInfoReloc.DIRECT);
                    }

                    JITDUMP($"  {FMT_BB(block.bbNum)}: 0x{bDstRW[i]:x}\n");
                }
            }
            else if (dsc.dsType == dataSection.sectionType.blockRelative32)
            {
#if DEBUG
                if (compiler.verbose)
                {
                    JITDUMP($"  section {secNum++}, size {dscSize}, block relative addr\n");
                }
#endif
                var numElems = dscSize / sizeof(uint);
                var uDstRW = (uint*)dstRW;
                var firstBlock = compiler.fgFirstBB
                    ?? throw new FatalJitException("A relative-label table requires the first basic block.");
                var labFirst = emitCodeGetCookie(firstBlock)
                    ?? throw new FatalJitException("A relative-label table requires the first emitted label.");

                for (uint i = 0; i < numElems; i++)
                {
                    var block = dsc.Blocks[i]
                        ?? throw new FatalJitException("A relative-label table requires a block for every entry.");
                    var lab = emitCodeGetCookie(block)
                        ?? throw new FatalJitException("A relative-label table requires an emitted block label.");
                    uDstRW[i] = unchecked(lab.igOffs - labFirst.igOffs);

                    JITDUMP($"  {FMT_BB(block.bbNum)}: 0x{uDstRW[i]:x}\n");
                }
            }
            else if (dsc.dsType == dataSection.sectionType.asyncResumeInfo)
            {
#if DEBUG
                if (compiler.verbose)
                {
                    JITDUMP($"  section {secNum++}, size {dscSize}, async resume info\n");
                }
#endif
                var numElems = dscSize / (uint)sizeof(CORINFO_AsyncResumeInfo);
                var aDstRW = (CORINFO_AsyncResumeInfo*)dstRW;
                for (uint i = 0; i < numElems; i++)
                {
                    var location = dsc.Locations[i];
                    var target = location.Valid() ? emitOffsetToPtr(location.CodeOffset(this)) : null;

                    aDstRW[i].Resume = (nint)emitAsyncResumeStubEntryPoint;
                    aDstRW[i].DiagnosticIP = (nint)target;

                    if (compiler.opts.compReloc)
                    {
                        emitRecordRelocation(&aDstRW[i].Resume, emitAsyncResumeStubEntryPoint, CorInfoReloc.DIRECT);
                        if (target is not null)
                        {
                            emitRecordRelocation(&aDstRW[i].DiagnosticIP, target, CorInfoReloc.DIRECT);
                        }
                    }

                    JITDUMP($"  Resume={FMT_PTR(emitAsyncResumeStubEntryPoint)}, FinalResumeIP={FMT_PTR(target)}\n");
                }
            }
            else
            {
                assert(dsc.dsType == dataSection.sectionType.data);
                if (dsc.dsType != dataSection.sectionType.data)
                {
                    throw new FatalJitException($"Unexpected data-section type {dsc.dsType}.");
                }
                var size = checked((int)dscSize);
                dsc.Data.AsSpan(0, size).CopyTo(new Span<byte>(dstRW, size));

#if DEBUG
                if (compiler.verbose)
                {
                    jitprintf($"  section {secNum++,3}, size {dscSize,2}, RWD{dsc.dsOffset,2}:\t");
                    for (var i = 0; i < size; i++)
                    {
                        jitprintf($"{dsc.Data[i]:x2} ");
                        if ((i + 1) % 16 == 0 && i + 1 != size)
                        {
                            jitprintf("\n\t\t\t\t\t");
                        }
                    }
                    switch (dsc.dsDataType)
                    {
                        case TYP_FLOAT:
                        {
                            var value = BitConverter.ToSingle(dsc.Data, 0);
                            jitprintf($" ; float  {FormatDataFloat(value, 6, 9)}");
                            break;
                        }

                        case TYP_DOUBLE:
                        {
                            var value = BitConverter.ToDouble(dsc.Data, 0);
                            jitprintf($" ; double {FormatDataFloat(value, 9, 12)}");
                            break;
                        }
                    }
                    jitprintf("\n");
                }
#endif
            }
        }
#endif
    }
}
#endif
