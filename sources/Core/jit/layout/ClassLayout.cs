// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

/// <summary>Encapsulates layout information about a class (typically a value class but this can also be be used for reference classes when they are stack allocated).</summary>
/// <remarks>The class handle is optional, allowing the creation of custom layout objects having a specific size where the offsets of GC fields can be specified during creation.</remarks>
public sealed class ClassLayout
{
    // Class handle or NO_CLASS_HANDLE for "block" layouts.
    private readonly unsafe CORINFO_CLASS_HANDLE _classHandle;

    // Size of the layout in bytes (as reported by ICorJitInfo::getClassSize/getHeapClassSize for non "block" layouts).
    // For "block" layouts this may be 0 due to 0 being a valid size for cpblk/initblk.
    private readonly int _size;

    private uint _bitfield;

    // Array of CorInfoGCType (as BYTE) that describes the GC layout of the class.
    // For small classes the array is stored inline, avoiding an extra allocation and the pointer size overhead.
    private _Anonymous_e__Union _anonymous;

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

    public unsafe CORINFO_CLASS_HANDLE ClassHandle => _classHandle;

    /// <summary>The number of GC pointers in this layout.</summary>
    /// <remarks>Since the maximum size is 2^32-1 the count can fit in at most 30 bits.</remarks>
    public uint GcPtrCount => (_bitfield >> 1) & 0x3FFF_FFFF;

    public bool HasGcPtr => GcPtrCount != 0;

    public bool IsValueClass => (_bitfield & 1) != 0;

    public int SlotCount => roundUp(_size, TARGET_POINTER_SIZE) / TARGET_POINTER_SIZE;

    public int Size => _size;

    public var_types Type => _type;

    private unsafe Span<byte> GcPtrs
    {
        get
        {
            var slotCount = SlotCount;
            Span<byte> result = _anonymous.GCPtrsArray;

            if (slotCount > result.Length)
            {
                result = new Span<byte>(_anonymous.GCPtrs, slotCount);
            }
            return result;
        }
    }

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

        if (layout1.HasGcPtr != layout2.HasGcPtr)
        {
            return false;
        }

        if (layout1.Type != layout2.Type)
        {
            return false;
        }

        if (!layout1.HasGcPtr && !layout2.HasGcPtr)
        {
            return true;
        }

        assert(layout1.HasGcPtr && layout2.HasGcPtr);

        if (layout1.GcPtrCount != layout2.GcPtrCount)
        {
            return false;
        }

        assert(layout1.SlotCount == layout2.SlotCount);
        var slotsCount = layout1.SlotCount;

        for (var i = 0; i < slotsCount; i++)
        {
            if (layout1.GetGcPtrType(i) != layout2.GetGcPtrType(i))
            {
                return false;
            }
        }
        return true;
    }

    public var_types GetGcPtrType(int slot) => GetGcPtr(slot) switch {
        TYPE_GC_NONE => TYP_I_IMPL,
        TYPE_GC_REF => TYP_REF,
        TYPE_GC_BYREF => TYP_BYREF,
        _ => TYP_UNKNOWN,
    };

    /// <summary>does the layout represent a block that can never be on the heap?</summary>
    /// <param name="compiler">The Compiler object</param>
    /// <returns>true if the block is stack only</returns>
    public unsafe bool IsStackOnly(Compiler compiler)
    {
        // Byref-like structs are stack only
        return (_classHandle != NO_CLASS_HANDLE)
            && compiler.eeIsByrefLike(_classHandle);
    }

    private CorInfoGCType GetGcPtr(int slot)
    {
        assert(slot < SlotCount);
        return (GcPtrCount != 0) ? (CorInfoGCType)(GcPtrs[slot]) : TYPE_GC_NONE;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct _Anonymous_e__Union
    {
        [FieldOffset(0)]
        public unsafe byte* GCPtrs;

        [FieldOffset(0)]
        public GCPtrsArrayInlineArray GCPtrsArray;
    }

    [InlineArray(TARGET_POINTER_SIZE)]
    private struct GCPtrsArrayInlineArray
    {
        public byte e0;
    }
}
