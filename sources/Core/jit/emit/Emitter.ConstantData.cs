// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public partial class Emitter
{
    public uint emitDataGenBeg(uint size, uint alignment, var_types dataType)
    {
        assert(emitDataSecCur is null);
        assert((size != 0) && ((size % dataSection.MIN_DATA_ALIGN) == 0));

        var secOffs = roundUp(emitConsDsc.dsdOffs, alignment);
        emitConsDsc.dsdOffs = unchecked(secOffs + size);
        var section = new dataSection
        {
            dsType = dataSection.sectionType.data,
            Data = new byte[size],
            dsSize = size,
            dsAlignment = alignment,
            dsOffset = secOffs,
            dsDataType = dataType,
        };
        emitDataSecCur = section;

        if (emitConsDsc.dsdLast is dataSection last)
        {
            last.dsNext = section;
        }
        else
        {
            emitConsDsc.dsdList = section;
        }
        emitConsDsc.dsdLast = section;

        return secOffs;
    }

    public uint emitBBTableDataGenBeg(uint numEntries, bool relativeAddr)
    {
        assert(emitDataSecCur is null);
        var elemSize = relativeAddr ? 4u : TARGET_POINTER_SIZE;
        var emittedSize = unchecked(numEntries * elemSize);
        var secOffs = roundUp(emitConsDsc.dsdOffs, elemSize);
        emitConsDsc.dsdOffs = unchecked(secOffs + emittedSize);

        var section = new dataSection
        {
            dsType = relativeAddr ? dataSection.sectionType.blockRelative32 : dataSection.sectionType.blockAbsoluteAddr,
            Blocks = new BasicBlock?[numEntries],
            dsSize = emittedSize,
            dsAlignment = elemSize,
            dsOffset = secOffs,
            dsDataType = TYP_UNKNOWN,
        };
        emitDataSecCur = section;

        if (emitConsDsc.dsdLast is dataSection last)
        {
            last.dsNext = section;
        }
        else
        {
            emitConsDsc.dsdList = section;
        }
        emitConsDsc.dsdLast = section;

        return secOffs;
    }

    public void emitDataGenData(uint offset, ReadOnlySpan<byte> data)
    {
        var section = emitDataSecCur;
        assert(section is not null);
        assert(section.dsSize >= unchecked(offset + (uint)data.Length));
        assert(section.dsType == dataSection.sectionType.data);
        data.CopyTo(section.Data.AsSpan(checked((int)offset), data.Length));
    }

    public void emitDataGenData(uint index, BasicBlock label)
    {
        var section = emitDataSecCur;
        assert(section is not null);
        assert(section.dsType is dataSection.sectionType.blockAbsoluteAddr or dataSection.sectionType.blockRelative32);
        var elemSize = section.dsType == dataSection.sectionType.blockAbsoluteAddr ? TARGET_POINTER_SIZE : 4u;
        assert(section.dsSize >= unchecked(elemSize * (index + 1)));
        section.Blocks[index] = label;
    }

    public void emitDataGenEnd()
    {
#if DEBUG
        assert(emitDataSecCur is not null);
        emitDataSecCur = null;
#endif
    }

    public uint emitDataGenFind(ReadOnlySpan<byte> data, uint alignment, var_types dataType)
    {
        var cnum = uint.MaxValue;
        var cmpCount = 0u;
        var section = emitConsDsc.dsdList;

        while (section is not null)
        {
            // A shorter constant can reuse a larger section's prefix, independently of its element type.
            if ((section.dsType == dataSection.sectionType.data) &&
                (section.dsSize >= (uint)data.Length) && (section.dsAlignment >= alignment) &&
                data.SequenceEqual(section.Data.AsSpan(0, data.Length)))
            {
                cnum = section.dsOffset;
                if ((section.dsDataType != dataType) && (section.dsSize == (uint)data.Length) &&
                    varTypeIsFloating(dataType))
                {
                    section.dsDataType = dataType;
                }

                break;
            }

            section = section.dsNext;

            // Preserve the native post-iteration bound: at most 65 sections are examined.
            if (++cmpCount > 64)
            {
                break;
            }
        }

        return cnum;
    }

    public uint emitDataConst(ReadOnlySpan<byte> data, uint alignment, var_types dataType)
    {
        var cnum = emitDataGenFind(data, alignment, dataType);
        if (cnum == uint.MaxValue)
        {
            cnum = emitDataGenBeg((uint)data.Length, alignment, dataType);
            emitDataGenData(0, data);
            emitDataGenEnd();
        }

        return cnum;
    }

    public unsafe CORINFO_FIELD_HANDLE emitBlkConst(ReadOnlySpan<byte> data, uint alignment, var_types elemType)
    {
        var cnum = emitDataGenBeg((uint)data.Length, alignment, elemType);
        emitDataGenData(0, data);
        emitDataGenEnd();

        return Compiler.eeFindJitDataOffs(cnum);
    }

    public unsafe CORINFO_FIELD_HANDLE emitFltOrDblConst(double constValue, emitAttr attr)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Floating constant-data materialization outside AMD64 is not implemented.");
#else
        assert(_compiler is not null);
        assert(attr is EA_4BYTE or EA_8BYTE);
        var cnsSize = attr == EA_4BYTE ? sizeof(float) : sizeof(double);
        Span<byte> data = stackalloc byte[cnsSize];
        var_types dataType;

        if (attr == EA_4BYTE)
        {
            var value = (float)constValue;
            MemoryMarshal.Write(data, in value);
            dataType = TYP_FLOAT;
        }
        else
        {
            MemoryMarshal.Write(data, in constValue);
            dataType = TYP_DOUBLE;
        }

        var cnsAlign = (uint)cnsSize;
        if (_compiler.compCodeOpt == Compiler.SMALL_CODE)
        {
            cnsAlign = dataSection.MIN_DATA_ALIGN;
        }

        var cnum = emitDataConst(data, cnsAlign, dataType);
        return Compiler.eeFindJitDataOffs(cnum);
#endif
    }
}
