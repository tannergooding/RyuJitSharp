// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public struct ClassLayoutBuilder
{
    private Compiler _compiler;
    internal CorInfoGCType[]? _gcPtrs;
    internal int _size;
    internal int _gcPtrCount;
    internal SegmentList? _nonPadding;

#if DEBUG
    internal string _name = "UNNAMED";
    internal string _shortName = "UNNAMED";
#endif

    public ClassLayoutBuilder(Compiler compiler, int size)
    {
        _compiler = compiler;
        _size = size;
    }

    /// <summary>check if an array of the specified length would exceed the specified maximum byte size for its payload.</summary>
    /// <param name="compiler">Compiler instance</param>
    /// <param name="arrayHandle">class handle for array</param>
    /// <param name="length">array length (in elements)</param>
    /// <param name="maxByteSize">maximum allowed byte size for the array payload</param>
    /// <returns>true if the array would be too large</returns>
    public static unsafe bool IsArrayTooLarge(Compiler compiler, CORINFO_CLASS_HANDLE arrayHandle, int length, int maxByteSize)
    {
        var elemClsHnd = NO_CLASS_HANDLE;
        var type = compiler.info.compCompHnd->getChildType(arrayHandle, &elemClsHnd).VarType;

        var elementSize = (type is TYP_STRUCT) ? compiler.typGetObjLayout(elemClsHnd).Size : type.Size;
        var byteSize = unchecked((long)(elementSize) * length);

        return byteSize > maxByteSize;
    }

    /// <summary>Construct a builder for an array layout</summary>
    /// <param name="compiler">Compiler instance</param>
    /// <param name="arrayHandle">class handle for array</param>
    /// <param name="length">array length (in elements)</param>
    /// <returns>For arrays of structs we currently do not copy any struct padding, with the presumption that it is unlikely we will ever promote array elements.</returns>
    public static unsafe ClassLayoutBuilder BuildArray(Compiler compiler, CORINFO_CLASS_HANDLE arrayHandle, int length)
    {
        assert(length <= CORINFO_Array_MaxLength);
        assert(arrayHandle != NO_CLASS_HANDLE);

        var elemClsHnd = NO_CLASS_HANDLE;
        var corType = compiler.info.compCompHnd->getChildType(arrayHandle, &elemClsHnd);
        var type = corType.VarType;

        var elementLayout = null as ClassLayout;
        var elementSize = 0;

        if (type == TYP_STRUCT)
        {
            elementLayout = compiler.typGetObjLayout(elemClsHnd);
            elementSize = elementLayout.Size;
        }
        else
        {
            elementSize = type.Size;
        }

        // should never overflow if caller used IsArrayTooLarge beforehand
        var overflow = !CheckedOps.TryMul(elementSize, length, out var totalSize);
        overflow |= !CheckedOps.TryAlignUp(totalSize, TARGET_POINTER_SIZE, out totalSize);
        overflow |= !CheckedOps.TryAdd(totalSize, OFFSETOF__CORINFO_Array__data, out totalSize);
        assert(!overflow);

        var builder = new ClassLayoutBuilder(compiler, totalSize);

        if (elementLayout is not null)
        {
            if (elementLayout.HasGCPtr)
            {
                var offset = (int)(OFFSETOF__CORINFO_Array__data);

                for (var i = 0; i < length; i++)
                {
                    builder.CopyGCInfoFrom(offset, elementLayout);
                    offset += elementSize;
                }
            }
        }
        else if (varTypeIsGC(type))
        {
            var offset = (int)(OFFSETOF__CORINFO_Array__data);

            for (var i = 0; i < length; i++)
            {
                assert((offset % TARGET_POINTER_SIZE) == 0);
                var slot = offset / TARGET_POINTER_SIZE;

                builder.SetGCPtrType(slot, type);
                offset += elementSize;
            }
        }

#if DEBUG
        var className = compiler.eeGetClassName(arrayHandle);
        var shortClassName = compiler.eeGetShortClassName(arrayHandle);
        builder.SetName(className, shortClassName);
#endif

        return builder;
    }

    /// <summary>Mark that part of the layout has padding.</summary>
    /// <param name="padding">The segment to mark as being padding.</param>
    /// <remarks>The ClassLayoutBuilder starts out with the entire layout being considered to NOT be padding.</remarks>
    public void AddPadding(SegmentList.Segment padding)
    {
        assert((padding.Start <= padding.End) && (padding.End <= _size));
        GetOrCreateNonPadding().Subtract(padding);
    }

    /// <summary>Copy GC pointers from another layout.</summary>
    /// <param name="offset">Offset in this builder to start copy information into.</param>
    /// <param name="layout">Layout to get information from.</param>
    public void CopyGCInfoFrom(int offset, ClassLayout layout)
    {
        assert((offset + layout.Size) <= _size);

        if (layout.GCPtrCount > 0)
        {
            assert(offset % TARGET_POINTER_SIZE == 0);
            var startSlot = offset / TARGET_POINTER_SIZE;

            for (var slot = 0; slot < layout.SlotCount; slot++)
            {
                SetGCPtr(startSlot + slot, layout.GetGCPtr(slot));
            }
        }
    }

#if DEBUG
    /// <summary>Copy layout names, with optional prefix.</summary>
    /// <param name="layout">layout to copy from</param>
    /// <param name="prefix">prefix to add (or empty)</param>
    public void CopyNameFrom(ClassLayout layout, string prefix)
    {
        var layoutName = layout.ClassName;
        var layoutShortName = layout.ShortClassName;

        if (prefix.Length is not 0)
        {
            layoutName = $"{prefix}{layoutName}";
            layoutShortName = $"{prefix}{layoutShortName}";
        }
        SetName(layoutName, layoutShortName);
    }
#endif

    /// <summary>Copy padding from another layout.</summary>
    /// <param name="offset">Offset in this builder to start copy information into.</param>
    /// <param name="layout">Layout to get information from.</param>
    public void CopyPaddingFrom(int offset, ClassLayout layout)
    {
        var addedSegment = new SegmentList.Segment(offset, offset + layout.Size);
        AddPadding(addedSegment);

        foreach (var nonPadding in layout.GetNonPadding(_compiler))
        {
            var removedSegment = new SegmentList.Segment(offset + nonPadding.Start, offset + nonPadding.End);
            RemovePadding(removedSegment);
        }
    }

    /// <summary>Mark that part of the layout does not have padding.</summary>
    /// <param name="nonPadding">The segment to mark as having significant data.</param>
    /// <remarks>The ClassLayoutBuilder starts out with the entire layout being considered to NOT be padding.</remarks>
    public void RemovePadding(SegmentList.Segment nonPadding)
    {
        assert((nonPadding.Start <= nonPadding.End) && (nonPadding.End <= _size));
        GetOrCreateNonPadding().Add(nonPadding);
    }

    /// <summary>Set a slot to have specified type.</summary>
    /// <param name="slot">The GC pointer slot. The slot number corresponds to offset slot * TARGET_POINTER_SIZE.</param>
    /// <param name="type">Type that this slot contains. Must be TYP_REF, TYP_BYREF or TYP_I_IMPL.</param>
    /// <remarks>GC pointer information can only be set in layouts of size divisible by TARGET_POINTER_SIZE.</remarks>
    public void SetGCPtrType(int slot, var_types type)
    {
        switch (type)
        {
            case TYP_REF:
            {
                SetGCPtr(slot, TYPE_GC_REF);
                break;
            }

            case TYP_BYREF:
            {
                SetGCPtr(slot, TYPE_GC_BYREF);
                break;
            }

            case TYP_I_IMPL:
            {
                SetGCPtr(slot, TYPE_GC_NONE);
                break;
            }

            default:
            {
                NO_WAY("Invalid type passed to ClassLayoutBuilder::SetGCPtrType");
                break;
            }
        }
    }

#if DEBUG
    /// <summary>Set the long and short name of the layout.</summary>
    /// <param name="name">The long name</param>
    /// <param name="shortName">The short name</param>
    public void SetName(string name, string shortName)
    {
        _name      = name;
        _shortName = shortName;
    }
#endif

    /// <summary>Get the non padding segment list, or create it if it does not exist.</summary>
    /// <returns>The ClassLayoutBuilder starts out with the entire layout being considered to NOT be padding.</returns>
    private SegmentList GetOrCreateNonPadding()
    {
        var nonPadding = _nonPadding;

        if (nonPadding is null)
        {
            nonPadding = [];
            _nonPadding = nonPadding;

            var segment = new SegmentList.Segment(0, _size);
            nonPadding.Add(segment);
        }
        return nonPadding;
    }

    /// <summary>Get or create the array indicating GC pointer types.</summary>
    /// <returns>The array of CorInfoGCType.</returns>
    private CorInfoGCType[] GetOrCreateGCPtrs()
    {
        assert(_size % TARGET_POINTER_SIZE == 0);
        _gcPtrs ??= new CorInfoGCType[_size / TARGET_POINTER_SIZE];
        return _gcPtrs;
    }

    /// <summary>Set a slot to have specified GC pointer type.</summary>
    /// <param name="slot">The GC pointer slot. The slot number corresponds to offset slot * TARGET_POINTER_SIZE.</param>
    /// <param name="type">Type of GC pointer that this slot contains.</param>
    /// <remarks>GC pointer information can only be set in layouts of size divisible by TARGET_POINTER_SIZE.</remarks>
    private void SetGCPtr(int slot, CorInfoGCType type)
    {
        var ptrs = GetOrCreateGCPtrs();
        assert((slot * TARGET_POINTER_SIZE) < _size);

        if (ptrs[slot] != TYPE_GC_NONE)
        {
            _gcPtrCount--;
        }

        ptrs[slot] = type;

        if (type != TYPE_GC_NONE)
        {
            _gcPtrCount++;
        }
    }
}
