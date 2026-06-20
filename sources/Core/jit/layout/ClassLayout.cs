// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using RyuJitSharp;

namespace RyuJitSharp;

/// <summary>Encapsulates layout information about a class (typically a value class but this can also be be used for reference classes when they are stack allocated).</summary>
/// <remarks>The class handle is optional, allowing the creation of custom layout objects having a specific size where the offsets of GC fields can be specified during creation.</remarks>
public sealed class ClassLayout
{
    // Class handle or NO_CLASS_HANDLE for "block" layouts.
    private readonly unsafe CORINFO_CLASS_HANDLE _classHandle;

    // Size of the layout in bytes (as reported by ICorJitInfo.getClassSize/getHeapClassSize for non "block" layouts).
    // For "block" layouts this may be 0 due to 0 being a valid size for cpblk/initblk.
    private readonly int _size;

    private int _bitfield;

    internal CorInfoGCType[]? _gcPtrs;

    internal InlineArrayTargetPointerSize<CorInfoGCType> _inlineGCPtrs;

    private SegmentList? _nonPadding;

    // The normalized type to use in IR for block nodes with this layout.
    private readonly var_types _type;

#if DEBUG
    // Name of the layout
    private string _name;

    // Short name of the layout
    private string _shortName;
#endif

    public ClassLayout(int size)
    {
        _size = size;
        _type = TYP_STRUCT;

#if DEBUG
        if (size == 0)
        {
            _name = "Empty";
            _shortName = "Empty";
        }
        else
        {
            _name = "Custom";
            _shortName = "Custom";
        }
#endif
    }

    public unsafe ClassLayout(CORINFO_CLASS_HANDLE classHandle, bool isValueClass, int size, var_types type, string className, string shortClassName)
    {
        assert(size != 0);
        _classHandle = classHandle;
        _size = size;
        _bitfield = isValueClass ? 1 : 0;
        _type = type;
#if DEBUG
        _name = className;
        _shortName = shortClassName;
#endif
    }

    public unsafe CORINFO_CLASS_HANDLE ClassHandle => _classHandle;

#if DEBUG
    public string ClassName => _name;

    public string ShortClassName => _shortName;
#endif

    /// <summary>The number of GC pointers in this layout.</summary>
    /// <remarks>Since the maximum size == 2^32-1 the count can fit in at most 30 bits.</remarks>
    public int GCPtrCount
    {
        get
        {
            return (_bitfield >>> 1) & 0x3FFF_FFFF;
        }

        set
        {
            _bitfield = (_bitfield & 1) | ((value & 0x3FFF_FFFF) << 1);
        }
    }

    public bool HasGCPtr => GCPtrCount != 0;

    public bool IsBlockLayout => IsCustomLayout && !HasGCPtr;

    public unsafe bool IsCustomLayout => _classHandle == NO_CLASS_HANDLE;

    public bool IsValueClass => (_bitfield & 1) != 0;

    /// <summary>Determine register type for the layout.</summary>
    public var_types RegisterType
    {
        get
        {
            if (HasGCPtr)
            {
                return (SlotCount == 1) ? GetGCPtrType(0) : TYP_UNDEF;
            }

            return _size switch {
                1 => TYP_UBYTE,
                2 => TYP_USHORT,
                4 => TYP_INT,
#if TARGET_64BIT || TARGET_WASM
                8 => TYP_LONG,
#endif
#if FEATURE_SIMD
                // TODO: check TYP_SIMD12 profitability, it will need additional support in `BuildStoreLoc`.
                16 => TYP_SIMD16,
#endif
                _ => TYP_UNDEF,
            };
        }
    }

    public ushort SlotCount => (ushort)(roundUp(_size, TARGET_POINTER_SIZE) / TARGET_POINTER_SIZE);

    public int Size => _size;

    public var_types Type => _type;

    /// <summary>check if 2 layouts are the same for copying.</summary>
    /// <param name="layout1">the first layout</param>
    /// <param name="layout2">the second layout</param>
    /// <returns>true if compatible, false otherwise.</returns>
    /// <remarks>
    ///   <para>Layouts are called compatible if they are equal or if they have the same size and the same GC slots.</para>
    ///   <para>This is an equivalence relation:</para>
    ///   <list type="bullet">
    ///     <item><c>AreCompatible(a, b) == AreCompatible(b, a)</c></item>
    ///     <item><c>AreCompatible(a, a) == true</c></item>
    ///     <item><c>AreCompatible(a, b) &amp;&amp; AreCompatible(b, c) ==&gt; AreCompatible(a, c)</c></item>
    ///   </list>
    /// </remarks>
    public static unsafe bool AreCompatible(ClassLayout? layout1, ClassLayout? layout2)
    {
        if ((layout1 is null) || (layout2 is null))
        {
            return false;
        }

        var clsHnd1 = layout1.ClassHandle;
        var clsHnd2 = layout2.ClassHandle;

        if ((clsHnd1 != NO_CLASS_HANDLE) == (clsHnd2 != NO_CLASS_HANDLE))
        {
            // Either both are class-based layout or both are custom layouts.
            // Custom layouts only match each other if they are the same pointer.
            if (clsHnd1 == NO_CLASS_HANDLE)
            {
                return layout1 == layout2;
            }

            // For class-based layouts they are definitely compatible for the same handle
            if (clsHnd1 == clsHnd2)
            {
                return true;
            }

            // But they may still be compatible for different handles.
        }

        if (layout1.Size != layout2.Size)
        {
            return false;
        }

        if (layout1.HasGCPtr != layout2.HasGCPtr)
        {
            return false;
        }

        if (layout1.Type != layout2.Type)
        {
            return false;
        }

        if (!layout1.HasGCPtr && !layout2.HasGCPtr)
        {
            return true;
        }

        assert(layout1.HasGCPtr && layout2.HasGCPtr);

        if (layout1.GCPtrCount != layout2.GCPtrCount)
        {
            return false;
        }

        assert(layout1.SlotCount == layout2.SlotCount);
        var slotsCount = layout1.SlotCount;

        for (var i = 0; i < slotsCount; i++)
        {
            if (layout1.GetGCPtrType(i) != layout2.GetGCPtrType(i))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Create a ClassLayout from an EE side class handle.</summary>
    /// <param name="compiler">The Compiler object</param>
    /// <param name="classHandle">The class handle</param>
    /// <returns>New layout representing an EE side class.</returns>
    public static unsafe ClassLayout Create(Compiler compiler, CORINFO_CLASS_HANDLE classHandle)
    {
        var isValueClass = compiler.eeIsValueClass(classHandle);
        var size = isValueClass
                 ? compiler.info.compCompHnd->getClassSize(classHandle)
                 : compiler.info.compCompHnd->getHeapClassSize(classHandle);

        var type = compiler.impNormStructType(classHandle);

#if DEBUG
        var className = compiler.eeGetClassName(classHandle);
        var shortClassName = compiler.eeGetShortClassName(classHandle);
#else
        var className = "";
        var shortClassName = "";
#endif

        var layout = new ClassLayout(classHandle, isValueClass, size, type, className, shortClassName);

        if (layout._size < TARGET_POINTER_SIZE)
        {
            assert(layout.SlotCount == 1);
            assert(layout.GCPtrCount == 0);

            layout._inlineGCPtrs[0] = TYPE_GC_NONE;
        }
        else
        {
            Span<CorInfoGCType> gcPtrs;

            if (layout.SlotCount <= TARGET_POINTER_SIZE)
            {
                gcPtrs = layout._inlineGCPtrs;
            }
            else
            {
                layout._gcPtrs = new CorInfoGCType[layout.SlotCount];
                gcPtrs = layout._gcPtrs;
            }

            int gcPtrCount;

            fixed (CorInfoGCType* pGCPtrs = gcPtrs)
            {
                gcPtrCount = compiler.info.compCompHnd->getClassGClayout(classHandle, pGCPtrs);
            }

            assert((gcPtrCount == 0) || ((compiler.info.compCompHnd->getClassAttribs(classHandle) & (CORINFO_FLG_CONTAINS_GC_PTR | CORINFO_FLG_BYREF_LIKE)) != 0));

            // Since class size is unsigned there's no way we could have more than 2^30 slots
            // so it should be safe to fit this into a 30 bits bit field.
            assert(gcPtrCount < (1 << 30));

            layout.GCPtrCount = gcPtrCount;
        }

        return layout;
    }

    /// <summary>Create a ClassLayout from a ClassLayoutBuilder.</summary>
    /// <param name="compiler">The Compiler object</param>
    /// <param name="builder">Builder representing the layout</param>
    /// <returns>New layout representing a custom (JIT internal) class layout.</returns>
    public static unsafe ClassLayout Create(Compiler compiler, in ClassLayoutBuilder builder)
    {
        var newLayout = new ClassLayout(builder._size) {
            GCPtrCount = builder._gcPtrCount,
            _nonPadding = builder._nonPadding,
#if DEBUG
            _name = builder._name,
            _shortName = builder._shortName,
#endif
        };

        if (builder._gcPtrCount <= 0)
        {
            var slotCount = newLayout.SlotCount;
            newLayout._gcPtrs = (slotCount != 0) ? new CorInfoGCType[newLayout.SlotCount] : [];
        }
        else if (newLayout.SlotCount <= TARGET_POINTER_SIZE)
        {
            builder._gcPtrs.CopyTo(newLayout._inlineGCPtrs);
        }
        else
        {
            newLayout._gcPtrs = builder._gcPtrs;
        }
        return newLayout;
    }

    /// <summary>true if assignment to this layout from the indicated layout is sensible</summary>
    /// <param name="sourceLayout">the source of a possible assigment</param>
    /// <returns>true if assignable, false otherwise.</returns>
    /// <remarks>This may not be an equivalence relation: a->CanAssignFrom(b) and b->CanAssignFrom(a) may differ.</remarks>
    public bool CanAssignFrom(ClassLayout sourceLayout)
    {
        if (this == sourceLayout)
        {
            return true;
        }

        // Do the normal compatibility check first
        if (AreCompatible(this, sourceLayout))
        {
            return true;
        }

        // Must be same size
        if (Size != sourceLayout.Size)
        {
            return false;
        }

        // Must be same IR type
        if (Type != sourceLayout.Type)
        {
            return false;
        }

        // Dest is GC, source is GC. Allow, slotwise:
        //
        //   byref <- ref, byref, nint
        //   ref   <- ref
        //   nint  <- nint

        if (HasGCPtr && sourceLayout.HasGCPtr)
        {
            var slotsCount = SlotCount;
            assert(slotsCount == sourceLayout.SlotCount);

            for (var i = 0; i < slotsCount; i++)
            {
                var slotType = GetGCPtrType(i);
                var layoutSlotType = sourceLayout.GetGCPtrType(i);

                if ((slotType is not TYP_BYREF) && (slotType != layoutSlotType))
                {
                    return false;
                }
            }
            return true;
        }

        // Dest is GC, source is noGC. Allow, slotwise:
        //
        //    byref <- nint
        //    nint  <- nint

        if (HasGCPtr && !sourceLayout.HasGCPtr)
        {
            var slotsCount = SlotCount;

            for (var i = 0; i < slotsCount; i++)
            {
                var slotType = GetGCPtrType(i);

                if (slotType is TYP_REF)
                {
                    return false;
                }
            }
            return true;
        }

        // Dest is noGC, source is GC. Disallow.

        if (!HasGCPtr && sourceLayout.HasGCPtr)
        {
            assert(!HasGCPtr);
            return false;
        }

        // Dest is noGC, source is noGC, and they're not compatible.
        return false;
    }

    public var_types GetGCPtrType(int slot) => GetGCPtr(slot) switch {
        TYPE_GC_NONE => TYP_I_IMPL,
        TYPE_GC_REF => TYP_REF,
        TYPE_GC_BYREF => TYP_BYREF,
        _ => TYP_UNKNOWN,
    };

    /// <summary>Get a SegmentList containing segments for all the non-padding in the layout. This is generally the areas of the layout covered by fields, but in some cases may also include other parts.</summary>
    /// <param name="compiler">Compiler instance</param>
    /// <returns>A segment list.</returns>
    public unsafe SegmentList GetNonPadding(Compiler compiler)
    {
        if (_nonPadding is not null)
        {
            return _nonPadding;
        }

        var nonPadding = new SegmentList();
        _nonPadding = nonPadding;

        if (IsCustomLayout)
        {
            if (_size > 0)
            {
                var segment = new SegmentList.Segment(0, Size);
                nonPadding.Add(segment);
            }

            return nonPadding;
        }

        Unsafe.SkipInit(out InlineArray256<CORINFO_TYPE_LAYOUT_NODE> inlineNodes);

        var numNodes = (nint)(256);
        var result = compiler.info.compCompHnd->getTypeLayout(ClassHandle, &inlineNodes.e0, &numNodes);

        if (result != GetTypeLayoutResult.Success)
        {
            var segment = new SegmentList.Segment(0, Size);
            nonPadding.Add(segment);
        }
        else
        {
            Span<CORINFO_TYPE_LAYOUT_NODE> nodes = inlineNodes;
            nodes = nodes[..(int)(numNodes)];

            for (var i = 0; i < nodes.Length; i++)
            {
                ref var node = ref nodes[i];

                if ((node.type is not CORINFO_TYPE_VALUECLASS) || (node.simdTypeHnd != NO_CLASS_HANDLE) || node.hasSignificantPadding)
                {
                    var segment = new SegmentList.Segment(node.offset, node.offset + node.size);
                    nonPadding.Add(segment);
                }
            }
        }
        return nonPadding;
    }

    /// <summary>Check if this classlayout has a TYP_BYREF GC pointer in it.</summary>
    /// <returns>true if it does</returns>
    public bool HasGCByRef()
    {
        if (!HasGCPtr)
        {
            return false;
        }

        var numSlots = SlotCount;

        for (var i = 0; i < numSlots; i++)
        {
            if (GetGCPtrType(i) is TYP_BYREF)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>check if the specified interval intersects with a GC pointer.</summary>
    /// <param name="offset">The start offset of the interval</param>
    /// <param name="size">The size of the interval</param>
    /// <returns>true if it does</returns>
    public bool IntersectsGCPtr(int offset, int size)
    {
        if (!HasGCPtr)
        {
            return false;
        }

        var startSlot = offset / TARGET_POINTER_SIZE;
        var endSlot = (offset + size - 1) / TARGET_POINTER_SIZE;

        assert((startSlot < SlotCount) && (endSlot < SlotCount));

        for (var i = startSlot; i <= endSlot; i++)
        {
            if (IsGCPtr(i))
            {
                return true;
            }
        }
        return false;
    }

    public bool IsGCByRef(int slot) => GetGCPtr(slot) is TYPE_GC_BYREF;

    public bool IsGCPtr(int slot) => GetGCPtr(slot) is not TYPE_GC_NONE;

    public bool IsGCRef(int slot) => GetGCPtr(slot) is TYPE_GC_REF;

    /// <summary>does the layout represent a block that can never be on the heap?</summary>
    /// <param name="compiler">The Compiler object</param>
    /// <returns>true if the block is stack only</returns>
    public unsafe bool IsStackOnly(Compiler compiler)
    {
        // Byref-like structs are stack only
        return (_classHandle != NO_CLASS_HANDLE)
            && compiler.eeIsByrefLike(_classHandle);
    }

    internal CorInfoGCType GetGCPtr(int slot)
    {
        assert(slot < SlotCount);
        var gcPtrs = _gcPtrs ?? (ReadOnlySpan<CorInfoGCType>)(_inlineGCPtrs);
        return gcPtrs[slot];
    }
}
