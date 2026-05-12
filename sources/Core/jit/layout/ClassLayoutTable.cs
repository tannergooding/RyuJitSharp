// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public sealed class ClassLayoutTable
{
    // Up to 3 layouts can be stored "inline" and finding a layout by handle/size can be done using linear search.
    // Most methods need no more than 2 layouts
    private const int LayoutInlineArrayLength = 3;

    // Each layout is assigned a number, starting with TYP_UNKNOWN + 1.
    // This way one could use a single value to represent the notion of type
    // - values below TYP_UNKNOWN are var_types and values above it are struct layouts.
    private const int ZeroSizedBlockLayoutNum = (int)(TYP_UNKNOWN + 1);
    private const int FirstLayoutNum = (int)(TYP_UNKNOWN + 2);

    private LayoutInlineArray m_layoutArray;
    private ClassLayout[] m_layoutLargeArray = [];
    private Dictionary<CustomLayoutKey, int> m_customLayoutMap = [];
    private Dictionary<nint, int> m_objLayoutMap = [];

    // We furthermore fast-path the 0-sized block layout which is used for
    // block locals that may grow (e.g. the outgoing arg area in every non-x86
    // compilation).
    private ClassLayout m_zeroSizedBlockLayout = new ClassLayout(0);

    private int m_layoutCount;

    private bool HasSmallCapacity => m_layoutCount <= LayoutInlineArrayLength;

    // Get the layout that corresponds to the specified identifier number.
    public ClassLayout GetLayoutByNum(int num)
    {
        if (num == ZeroSizedBlockLayoutNum)
        {
            return m_zeroSizedBlockLayout;
        }
        assert(num >= FirstLayoutNum);
        return GetLayoutByIndex(num - FirstLayoutNum);
    }

    // Get a number that uniquely identifies the specified layout.
    public int GetLayoutNum(ClassLayout layout)
    {
        if (layout == m_zeroSizedBlockLayout)
        {
            return ZeroSizedBlockLayoutNum;
        }
        return GetLayoutIndex(layout) + FirstLayoutNum;
    }

    // Get the layout having the specified size but no class handle.
    public ClassLayout GetCustomLayout(Compiler compiler, in ClassLayoutBuilder builder)
    {
        if (builder.m_size == 0)
        {
            return m_zeroSizedBlockLayout;
        }
        return GetLayoutByIndex(GetCustomLayoutIndex(compiler, builder));
    }

    // Get a number that uniquely identifies a layout having the specified size but no class handle.
    public int GetCustomLayoutNum(Compiler compiler, in ClassLayoutBuilder builder)
    {
        if (builder.m_size == 0)
        {
            return ZeroSizedBlockLayoutNum;
        }
        return GetCustomLayoutIndex(compiler, builder) + FirstLayoutNum;
    }

    /// <summary>Get the layout for the specified class handle.</summary>
    /// <param name="compiler"></param>
    /// <param name="classHandle"></param>
    /// <returns></returns>
    public unsafe ClassLayout GetObjLayout(Compiler compiler, CORINFO_CLASS_HANDLE classHandle)
        => GetLayoutByIndex(GetObjLayoutIndex(compiler, classHandle));

    // Get a number that uniquely identifies a layout for the specified class handle.
    public unsafe int GetObjLayoutNum(Compiler compiler, CORINFO_CLASS_HANDLE classHandle)
        => GetObjLayoutIndex(compiler, classHandle) + FirstLayoutNum;

    public int AddCustomLayout(Compiler compiler, ClassLayout layout)
    {
        if (HasSmallCapacity)
        {
            var layoutCount = m_layoutCount++;
            m_layoutArray[layoutCount] = layout;
            return layoutCount;
        }

        var index = AddLayoutLarge(compiler, layout);
        m_customLayoutMap[new CustomLayoutKey(layout)] = index;
        return index;
    }

    private unsafe int AddLayoutLarge(Compiler compiler, ClassLayout layout)
    {
        if (m_layoutCount >= m_layoutLargeArray.Length)
        {
            var newCapacity = m_layoutCount * 2;
            var newArray = new ClassLayout[newCapacity];

            if (m_layoutCount <= LayoutInlineArrayLength)
            {
                for (var i = 0; i < m_layoutCount; i++)
                {
                    var l = m_layoutArray[i];
                    newArray[i] = l;

                    if (l.IsCustomLayout)
                    {
                        m_customLayoutMap[new CustomLayoutKey(l)] = i;
                    }
                    else
                    {
                        m_objLayoutMap[unchecked((nint)(l.ClassHandle))] = i;
                    }
                }
            }
            else
            {
                m_layoutLargeArray.AsSpan().CopyTo(newArray);
            }

            m_layoutLargeArray = newArray;
        }

        m_layoutLargeArray[m_layoutCount] = layout;
        return m_layoutCount++;
    }

    public unsafe int AddObjLayout(Compiler compiler, ClassLayout layout)
    {
        if (HasSmallCapacity)
        {
            var layoutCount = m_layoutCount++;
            m_layoutArray[layoutCount] = layout;
            return layoutCount;
        }

        var index = AddLayoutLarge(compiler, layout);
        m_objLayoutMap[unchecked((nint)(layout.ClassHandle))] = index;
        return index;
    }

    private int GetCustomLayoutIndex(Compiler compiler, in ClassLayoutBuilder builder)
    {
        // The 0-sized layout has its own fast path.
        assert(builder.m_size != 0);

        var key = new CustomLayoutKey(builder);

        if (HasSmallCapacity)
        {
            var layoutArray = (ReadOnlySpan<ClassLayout>)(m_layoutArray);
            layoutArray = layoutArray[..m_layoutCount];

            for (var i = 0; i < layoutArray.Length; i++)
            {
                var layout = layoutArray[i];

                if (layout.IsCustomLayout && Equals(key, new CustomLayoutKey(layout)))
                {
                    return i;
                }
            }
        }
        else if (m_customLayoutMap.TryGetValue(key, out var index))
        {
            return index;
        }
        return AddCustomLayout(compiler, ClassLayout.Create(compiler, builder));
    }

    private ClassLayout GetLayoutByIndex(int index)
    {
        assert(index < m_layoutCount);

        if (HasSmallCapacity)
        {
            return m_layoutArray[index];
        }
        else
        {
            return m_layoutLargeArray[index];
        }
    }

    private unsafe int GetLayoutIndex(ClassLayout layout)
    {
        assert(layout is not null);
        assert(layout != m_zeroSizedBlockLayout);

        var index = -1;

        if (HasSmallCapacity)
        {
            var layoutArray = (ReadOnlySpan<ClassLayout>)(m_layoutArray);
            layoutArray = layoutArray[..m_layoutCount];

            for (var i = 0; i < layoutArray.Length; i++)
            {
                if (layoutArray[i] == layout)
                {
                    return i;
                }
            }
        }
        else if (layout.IsCustomLayout ? !m_customLayoutMap.TryGetValue(new CustomLayoutKey(layout), out index)
                                       : !m_objLayoutMap.TryGetValue(unchecked((nint)(layout.ClassHandle)), out index))
        {
            unreached();
        }
        return index;
    }

    private unsafe int GetObjLayoutIndex(Compiler compiler, CORINFO_CLASS_HANDLE classHandle)
    {
        assert(classHandle != NO_CLASS_HANDLE);

        if (HasSmallCapacity)
        {
            var layoutArray = (ReadOnlySpan<ClassLayout>)(m_layoutArray);
            layoutArray = layoutArray[..m_layoutCount];

            for (var i = 0; i < layoutArray.Length; i++)
            {
                if (layoutArray[i].ClassHandle == classHandle)
                {
                    return i;
                }
            }
        }
        else if (m_objLayoutMap.TryGetValue(unchecked((nint)(classHandle)), out var index))
        {
            return index;
        }
        return AddObjLayout(compiler, ClassLayout.Create(compiler, classHandle));
    }

    [InlineArray(LayoutInlineArrayLength)]
    private struct LayoutInlineArray
    {
        public ClassLayout e0;
    }
}
