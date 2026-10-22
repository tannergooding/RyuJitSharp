// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Emitter
{
    public unsafe void emitOutputDataSec(dataSecDsc sec, AllocMemChunk* chunks)
    {
        var compiler = _compiler;
        if (compiler is null)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.DataSectionOutputMissingCompiler);
            return;
        }
        if (Configuration.DEBUG && compiler.verbose)
        {
            jitprintf($"\nEmitting data sections: {sec.dsdOffs} total bytes\n");
        }

        var secNum = 0u;
        if (compiler.opts.disAsm)
        {
            emitDispDataSec(sec, chunks);
        }

        assert(sec.dsdOffs != 0);
        assert(sec.dsdList is not null);
        if (sec.dsdOffs == 0 || sec.dsdList is null || chunks is null)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.DataSectionOutputMissingChunksOrSections);
            return;
        }

        var chunk = chunks;
        for (var dsc = sec.dsdList; dsc is not null; dsc = dsc.dsNext, chunk++)
        {
            var dscSize = dsc.dsSize;
            var dstRW = chunk->blockRW;

            if (dsc.dsType == dataSection.sectionType.blockAbsoluteAddr)
            {
                if (Configuration.DEBUG && compiler.verbose)
                {
                    JITDUMP($"  section {secNum++}, size {dscSize}, block absolute addr\n");
                }
                assert(dscSize != 0 && dscSize % Configuration.TARGET_POINTER_SIZE == 0);
                var numElems = dscSize / Configuration.TARGET_POINTER_SIZE;
                for (uint i = 0; i < numElems; i++)
                {
                    var block = dsc.Blocks[i];
                    if (block is null)
                    {
                        FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.AbsoluteLabelTableMissingBlock);
                        return;
                    }
                    var lab = emitCodeGetCookie(block);
                    if (lab is null)
                    {
                        FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.AbsoluteLabelTableMissingEmittedBlockLabel);
                        return;
                    }
                    var target = emitOffsetToPtr(lab.igOffs);
                    var targetSlot = (byte*)dstRW + unchecked((nint)(i * (uint)Configuration.TARGET_POINTER_SIZE));

                    if (Configuration.TARGET_ARM && !compiler.opts.compReloc)
                    {
                        target = (byte*)((nuint)target | 1u);
                    }

                    var targetAddress = unchecked((ulong)(nuint)target);
                    if (!Configuration.TARGET_64BIT)
                    {
                        targetAddress = unchecked((uint)targetAddress);
                    }
                    WriteTargetSize(targetSlot, targetAddress);

                    if (compiler.opts.compReloc)
                    {
                        emitRecordRelocation(targetSlot, target, CorInfoReloc.DIRECT);
                    }

                    JITDUMP($"  {FMT_BB(block.bbNum)}: 0x{targetAddress:x}\n");
                }
            }
            else if (dsc.dsType == dataSection.sectionType.blockRelative32)
            {
                if (Configuration.DEBUG && compiler.verbose)
                {
                    JITDUMP($"  section {secNum++}, size {dscSize}, block relative addr\n");
                }
                var numElems = dscSize / sizeof(uint);
                var uDstRW = (uint*)dstRW;
                var firstBlock = compiler.fgFirstBB;
                if (firstBlock is null)
                {
                    FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.RelativeLabelTableMissingFirstBasicBlock);
                    return;
                }
                var labFirst = emitCodeGetCookie(firstBlock);
                if (labFirst is null)
                {
                    FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.RelativeLabelTableMissingFirstEmittedLabel);
                    return;
                }

                for (uint i = 0; i < numElems; i++)
                {
                    var block = dsc.Blocks[i];
                    if (block is null)
                    {
                        FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.RelativeLabelTableMissingBlock);
                        return;
                    }
                    var lab = emitCodeGetCookie(block);
                    if (lab is null)
                    {
                        FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.RelativeLabelTableMissingEmittedBlockLabel);
                        return;
                    }
                    uDstRW[i] = unchecked(lab.igOffs - labFirst.igOffs);

                    JITDUMP($"  {FMT_BB(block.bbNum)}: 0x{uDstRW[i]:x}\n");
                }
            }
            else if (dsc.dsType == dataSection.sectionType.asyncResumeInfo)
            {
                if (Configuration.DEBUG && compiler.verbose)
                {
                    JITDUMP($"  section {secNum++}, size {dscSize}, async resume info\n");
                }
                var targetPointerSize = (uint)Configuration.TARGET_POINTER_SIZE;
                var entrySize = targetPointerSize * 2u;
                var numElems = dscSize / entrySize;
                for (uint i = 0; i < numElems; i++)
                {
                    var location = dsc.Locations[i];
                    var entry = (byte*)dstRW + unchecked((nint)(i * entrySize));
                    var resumeSlot = entry;
                    var diagnosticSlot = entry + targetPointerSize;
                    byte* target;
                    if (Configuration.TARGET_WASM)
                    {
                        target = location.Valid() && compiler.IsReadyToRun ? emitCodeBlock : null;
                    }
                    else
                    {
                        target = location.Valid() ? emitOffsetToPtr(location.CodeOffset(this)) : null;
                    }

                    WriteTargetSize(resumeSlot, unchecked((ulong)(nuint)emitAsyncResumeStubEntryPoint));
                    WriteTargetSize(diagnosticSlot, unchecked((ulong)(nuint)target));

                    if (compiler.opts.compReloc)
                    {
                        emitRecordRelocation(resumeSlot, emitAsyncResumeStubEntryPoint, CorInfoReloc.DIRECT);
                        if (target is not null)
                        {
                            var relocationType = Configuration.TARGET_WASM
                                ? CorInfoReloc.WASM_METHOD_RELATIVE_VIRTUAL_IP_I32
                                : CorInfoReloc.DIRECT;
                            emitRecordRelocation(diagnosticSlot, target, relocationType);
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
                    FatalJitException.ThrowFatalJitException(
        FatalJitErrorCode.UnexpectedDataSectionType,
        (Configuration.DETAILED_FATAL_ERROR_MESSAGES ? dsc.dsType : default)
    );
                    return;
                }
                var size = checked((int)dscSize);
                dsc.Data.AsSpan(0, size).CopyTo(new Span<byte>(dstRW, size));

                if (Configuration.DEBUG && compiler.verbose)
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
            }
        }
    }

    private static unsafe void WriteTargetSize(void* destination, ulong value)
    {
        if (Configuration.TARGET_64BIT)
        {
            *(ulong*)destination = value;
        }
        else
        {
            *(uint*)destination = unchecked((uint)value);
        }
    }
}
