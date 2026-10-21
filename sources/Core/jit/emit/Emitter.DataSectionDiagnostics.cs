// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Buffers.Binary;
using System.Globalization;

namespace RyuJitSharp;

public partial class Emitter
{
    public unsafe void emitDispDataSec(dataSecDsc section, AllocMemChunk* dataChunks)
    {
        var compiler = _compiler;
        if (compiler is null)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.DataSectionDiagnosticsMissingCompiler);
            return;
        }
        jitprintf("\n");

        var chunk = dataChunks;
        for (var data = section.dsdList; data is not null; data = data.dsNext, chunk++)
        {
            if (Configuration.DEBUG && compiler.opts.disAddr)
            {
                jitprintf($"; @{FMT_PTR((void*)dspPtr(chunk->block))}\n");
            }
            var label = $"RWD{data.dsOffset:D2}";
            jitprintf($"{label,-7}");

            if (data.dsType is dataSection.sectionType.blockRelative32 or dataSection.sectionType.blockAbsoluteAddr)
            {
                var firstBlock = compiler.fgFirstBB;
                if (firstBlock is null)
                {
                    FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.LabelTableDiagnosticsMissingFirstBasicBlock);
                    return;
                }
                var igFirst = emitCodeGetCookie(firstBlock);
                if (igFirst is null)
                {
                    FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.LabelTableDiagnosticsMissingFirstEmittedLabel);
                    return;
                }
                var isRelative = data.dsType == dataSection.sectionType.blockRelative32;
                var blockCount = data.dsSize / (isRelative ? 4u : Configuration.TARGET_POINTER_SIZE);

                for (uint i = 0; i < blockCount; i++)
                {
                    if (i > 0)
                    {
                        jitprintf("       ");
                    }

                    var block = data.Blocks[i];
                    if (block is null)
                    {
                        FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.LabelTableDiagnosticsMissingBlock);
                        return;
                    }
                    var ig = emitCodeGetCookie(block);
                    if (ig is null)
                    {
                        FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.LabelTableDiagnosticsMissingEmittedBlockLabel);
                        return;
                    }
                    var blockLabel = emitLabelString(ig);
                    var firstLabel = emitLabelString(igFirst);

                    if (isRelative)
                    {
                        if (compiler.opts.disDiffable)
                        {
                            jitprintf($"\tdd\t{blockLabel} - {firstLabel}\n");
                        }
                        else
                        {
                            jitprintf($"\tdd\t{unchecked(ig.igOffs - igFirst.igOffs):X8}h");
                        }
                    }
                    else
                    {
                        if (Configuration.TARGET_64BIT)
                        {
                            if (compiler.opts.disDiffable)
                            {
                                jitprintf($"\tdq\t{blockLabel}\n");
                            }
                            else
                            {
                                var address = emitOffsetToPtr(ig.igOffs);
                                jitprintf($"\tdq\t{(nuint)address:X16}h");
                            }
                        }
                        else
                        {
                            if (compiler.opts.disDiffable)
                            {
                                jitprintf($"\tdd\t{blockLabel}\n");
                            }
                            else
                            {
                                var address = emitOffsetToPtr(ig.igOffs);
                                jitprintf($"\tdd\t{unchecked((uint)(nuint)address):X8}h");
                            }
                        }
                    }

                    if (!compiler.opts.disDiffable)
                    {
                        jitprintf($" ; case {blockLabel}\n");
                    }
                }
            }
            else if (data.dsType == dataSection.sectionType.asyncResumeInfo)
            {
                assert(emitAsyncResumeStub != NO_METHOD_HANDLE);
                assert(emitAsyncResumeStubEntryPoint is not null);

                var resumeStubName = compiler.eeGetMethodFullName(emitAsyncResumeStub, true, true);
                // The oracle divides by host-sized emitLocation, not the target entry size,
                // and prints dq even for 32-bit targets. Preserve both diagnostic quirks.
                var infoCount = data.dsSize / (2u * (uint)sizeof(nint));
                var entrySize = 2u * (uint)Configuration.TARGET_POINTER_SIZE;
                for (uint i = 0; i < infoCount; i++)
                {
                    if (i > 0)
                    {
                        label = $"RWD{unchecked(data.dsOffset + (i * entrySize)):D2}";
                        jitprintf($"{label,-7}");
                    }

                    var location = data.Locations[i];
                    jitprintf($"\tdq\t{resumeStubName}\n");

                    var codeOffset = location.CodeOffset(this);
                    var group = location.GetIG();
                    if (group is null)
                    {
                        FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.AsyncDiagnosticsMissingCodeLocation);
                        return;
                    }
                    if (codeOffset != group.igOffs)
                    {
                        jitprintf($"\tdq\t{emitLabelString(group)} + {codeOffset - group.igOffs}\n");
                    }
                    else
                    {
                        jitprintf($"\tdq\t{emitLabelString(group)}\n");
                    }
                }
            }
            else
            {
                assert(data.dsType == dataSection.sectionType.data);
                if (data.dsType != dataSection.sectionType.data)
                {
                    FatalJitException.ThrowFatalJitException(
                        FatalJitErrorCode.UnexpectedDataSectionType,
                        Configuration.DETAILED_FATAL_ERROR_MESSAGES
                            ? $"Unexpected data-section type {data.dsType}."
                    : null);
                    return;
                }
                var bytes = data.Data;
                uint elemSize = data.dsDataType.Size;
                if (elemSize == 0)
                {
                    if (data.dsSize % 8 == 0)
                    {
                        elemSize = 8;
                    }
                    else if (data.dsSize % 4 == 0)
                    {
                        elemSize = 4;
                    }
                    else if (data.dsSize % 2 == 0)
                    {
                        elemSize = 2;
                    }
                    else
                    {
                        elemSize = 1;
                    }
                }

                var i = 0u;
                while (i < data.dsSize)
                {
                    switch (data.dsDataType)
                    {
                        case TYP_FLOAT:
                        {
                            if (data.dsSize < sizeof(float))
                            {
                                jitprintf($"\t<Unexpected data size {data.dsSize} (expected >= 4)\n");
                            }
                            var bits = ReadDataWord(bytes, i, sizeof(float));
                            var value = BitConverter.Int32BitsToSingle(unchecked((int)bits));
                            jitprintf($"\tdd\t{bits:X8}h\t");
                            jitprintf($"\t; {FormatDataFloat(value, 6, 9)}");
                            i += sizeof(float);
                            break;
                        }

                        case TYP_DOUBLE:
                        {
                            if (data.dsSize < sizeof(double))
                            {
                                jitprintf($"\t<Unexpected data size {data.dsSize} (expected >= 8)\n");
                            }
                            var bits = ReadDataQword(bytes, i);
                            var value = BitConverter.Int64BitsToDouble(unchecked((long)bits));
                            jitprintf($"\tdq\t{bits:X16}h");
                            jitprintf($"\t; {FormatDataFloat(value, 9, 12)}");
                            i += sizeof(double);
                            break;
                        }

                        default:
                        {
                            uint j;
                            switch (elemSize)
                            {
                                case 1:
                                {
                                    jitprintf($"\tdb\t{bytes[checked((int)i)]:X2}h");
                                    for (j = 1; j < 16; j++)
                                    {
                                        if (i + j >= data.dsSize)
                                        {
                                            break;
                                        }
                                        jitprintf($", {bytes[checked((int)(i + j))]:X2}h");
                                    }
                                    i += j;
                                    break;
                                }

                                case 2:
                                {
                                    if (data.dsSize % 2 != 0)
                                    {
                                        jitprintf($"\t<Unexpected data size {data.dsSize} (expected size%2 == 0)\n");
                                    }
                                    jitprintf($"\tdw\t{ReadDataWord(bytes, i, 2):X4}h");
                                    for (j = 2; j < 24; j += 2)
                                    {
                                        if (i + j >= data.dsSize)
                                        {
                                            break;
                                        }
                                        jitprintf($", {ReadDataWord(bytes, i + j, 2):X4}h");
                                    }
                                    i += j;
                                    break;
                                }

                                case 12:
                                case 4:
                                {
                                    if (data.dsSize % 4 != 0)
                                    {
                                        jitprintf($"\t<Unexpected data size {data.dsSize} (expected size%4 == 0)\n");
                                    }
                                    jitprintf($"\tdd\t{ReadDataWord(bytes, i, 4):X8}h");
                                    for (j = 4; j < 24; j += 4)
                                    {
                                        if (i + j >= data.dsSize)
                                        {
                                            break;
                                        }
                                        jitprintf($", {ReadDataWord(bytes, i + j, 4):X8}h");
                                    }
                                    i += j;
                                    break;
                                }

                                case 64:
                                case 32:
                                case 16:
                                case 8:
                                {
                                    if (data.dsSize % 8 != 0)
                                    {
                                        jitprintf($"\t<Unexpected data size {data.dsSize} (expected size%8 == 0)\n");
                                    }
                                    jitprintf($"\tdq\t{ReadDataQword(bytes, i):X16}h");
                                    for (j = 8; j < 64; j += 8)
                                    {
                                        if (i + j >= data.dsSize)
                                        {
                                            break;
                                        }
                                        jitprintf($", {ReadDataQword(bytes, i + j):X16}h");
                                    }
                                    i += j;
                                    break;
                                }

                                default:
                                {
                                    FatalJitException.ThrowFatalJitException(
                                        FatalJitErrorCode.UnexpectedDataSectionElementSize,
                                        Configuration.DETAILED_FATAL_ERROR_MESSAGES
                                            ? $"Unexpected data-section element size {elemSize}."
                                    : null);
                                    return;
                                }
                            }
                            break;
                        }
                    }
                    jitprintf("\n");
                }
            }
        }
    }

    private static uint ReadDataWord(byte[] data, uint offset, int size)
    {
        var start = checked((int)offset);
        var span = data.AsSpan(start, size);
        return size == 2 ? BinaryPrimitives.ReadUInt16LittleEndian(span)
            : BinaryPrimitives.ReadUInt32LittleEndian(span);
    }

    private static ulong ReadDataQword(byte[] data, uint offset)
        => BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(checked((int)offset), sizeof(ulong)));

    private const ulong WindowsNegativeIndefiniteNaNBits = 0xFFF8_0000_0000_0000UL;

    private static string FormatDataFloat(double value, int precision, int width)
    {
        var formatted = double.IsNaN(value) ? FormatDataNaN(value)
            : double.IsPositiveInfinity(value) ? "inf"
            : double.IsNegativeInfinity(value) ? "-inf"
            : value.ToString($"G{precision}", CultureInfo.InvariantCulture).Replace('E', 'e');
        return formatted.PadLeft(width);
    }

    private static string FormatDataNaN(double value)
    {
        if (OperatingSystem.IsWindows() && BitConverter.DoubleToUInt64Bits(value) == WindowsNegativeIndefiniteNaNBits)
        {
            return "-nan(ind)";
        }

        return double.IsNegative(value) ? "-nan" : "nan";
    }
}
