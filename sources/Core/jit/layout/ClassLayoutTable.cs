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
    // Each layout is assigned a number, starting with TYP_UNKNOWN + 1.
    // This way one could use a single value to represent the notion of type
    // - values below TYP_UNKNOWN are var_types and values above it are struct layouts.
    private const int ZeroSizedBlockLayoutNum = (int)(TYP_UNKNOWN + 1);
    private const int FirstLayoutNum = (int)(TYP_UNKNOWN + 2);

    // Up to 3 layouts can be stored "inline" and finding a layout by handle/size can be done using linear search.
    // Most methods need no more than 2 layouts
    private InlineArray3<ClassLayout> _layoutArray;
    private ClassLayout[] _layoutLargeArray = [];
    private Dictionary<CustomLayoutKey, int> _customLayoutMap = [];
    private Dictionary<nint, int> _objLayoutMap = [];

    // We furthermore fast-path the 0-sized block layout which is used for
    // block locals that may grow (e.g. the outgoing arg area in every non-x86
    // compilation).
    private ClassLayout _zeroSizedBlockLayout = new ClassLayout(0);

    private int _layoutCount;

    private bool HasSmallCapacity => _layoutCount <= 3;

    // Get the layout that corresponds to the specified identifier number.
    public ClassLayout GetLayoutByNum(int num)
    {
        if (num == ZeroSizedBlockLayoutNum)
        {
            return _zeroSizedBlockLayout;
        }
        assert(num >= FirstLayoutNum);
        return GetLayoutByIndex(num - FirstLayoutNum);
    }

    // Get a number that uniquely identifies the specified layout.
    public int GetLayoutNum(ClassLayout layout)
    {
        if (layout == _zeroSizedBlockLayout)
        {
            return ZeroSizedBlockLayoutNum;
        }
        return GetLayoutIndex(layout) + FirstLayoutNum;
    }

    // Get the layout having the specified size but no class handle.
    public ClassLayout GetCustomLayout(Compiler compiler, in ClassLayoutBuilder builder)
    {
        if (builder._size == 0)
        {
            return _zeroSizedBlockLayout;
        }
        return GetLayoutByIndex(GetCustomLayoutIndex(compiler, builder));
    }

    // Get a number that uniquely identifies a layout having the specified size but no class handle.
    public int GetCustomLayoutNum(Compiler compiler, in ClassLayoutBuilder builder)
    {
        if (builder._size == 0)
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
            var layoutCount = _layoutCount++;
            _layoutArray[layoutCount] = layout;
            return layoutCount;
        }

        var index = AddLayoutLarge(compiler, layout);
        _customLayoutMap[new CustomLayoutKey(layout)] = index;
        return index;
    }

    private unsafe int AddLayoutLarge(Compiler compiler, ClassLayout layout)
    {
        if (_layoutCount >= _layoutLargeArray.Length)
        {
            var newCapacity = _layoutCount * 2;
            var newArray = new ClassLayout[newCapacity];

            if (_layoutCount <= 3)
            {
                for (var i = 0; i < _layoutCount; i++)
                {
                    var l = _layoutArray[i];
                    newArray[i] = l;

                    if (l.IsCustomLayout)
                    {
                        _customLayoutMap[new CustomLayoutKey(l)] = i;
                    }
                    else
                    {
                        _objLayoutMap[unchecked((nint)(l.ClassHandle))] = i;
                    }
                }
            }
            else
            {
                _layoutLargeArray.AsSpan().CopyTo(newArray);
            }

            _layoutLargeArray = newArray;
        }

        _layoutLargeArray[_layoutCount] = layout;
        return _layoutCount++;
    }

    public unsafe int AddObjLayout(Compiler compiler, ClassLayout layout)
    {
        if (HasSmallCapacity)
        {
            var layoutCount = _layoutCount++;
            _layoutArray[layoutCount] = layout;
            return layoutCount;
        }

        var index = AddLayoutLarge(compiler, layout);
        _objLayoutMap[unchecked((nint)(layout.ClassHandle))] = index;
        return index;
    }

    private int GetCustomLayoutIndex(Compiler compiler, in ClassLayoutBuilder builder)
    {
        // The 0-sized layout has its own fast path.
        assert(builder._size != 0);

        var key = new CustomLayoutKey(builder);

        if (HasSmallCapacity)
        {
            var layoutArray = (ReadOnlySpan<ClassLayout>)(_layoutArray);
            layoutArray = layoutArray[.._layoutCount];

            for (var i = 0; i < layoutArray.Length; i++)
            {
                var layout = layoutArray[i];

                if (layout.IsCustomLayout && Equals(key, new CustomLayoutKey(layout)))
                {
                    return i;
                }
            }
        }
        else if (_customLayoutMap.TryGetValue(key, out var index))
        {
            return index;
        }
        return AddCustomLayout(compiler, ClassLayout.Create(compiler, builder));
    }

    private ClassLayout GetLayoutByIndex(int index)
    {
        assert(index < _layoutCount);

        if (HasSmallCapacity)
        {
            return _layoutArray[index];
        }
        else
        {
            return _layoutLargeArray[index];
        }
    }

    private unsafe int GetLayoutIndex(ClassLayout layout)
    {
        assert(layout is not null);
        assert(layout != _zeroSizedBlockLayout);

        if (HasSmallCapacity)
        {
            var layoutArray = (ReadOnlySpan<ClassLayout>)(_layoutArray);
            layoutArray = layoutArray[.._layoutCount];

            for (var i = 0; i < layoutArray.Length; i++)
            {
                if (layoutArray[i] == layout)
                {
                    return i;
                }
            }
        }
        else if (layout.IsCustomLayout ? _customLayoutMap.TryGetValue(new CustomLayoutKey(layout), out var index)
                                       : _objLayoutMap.TryGetValue(unchecked((nint)(layout.ClassHandle)), out index))
        {
            return index;
        }

        unreached();
        return -1;
    }

    private unsafe int GetObjLayoutIndex(Compiler compiler, CORINFO_CLASS_HANDLE classHandle)
    {
        assert(classHandle != NO_CLASS_HANDLE);

        if (HasSmallCapacity)
        {
            var layoutArray = (ReadOnlySpan<ClassLayout>)(_layoutArray);
            layoutArray = layoutArray[.._layoutCount];

            for (var i = 0; i < layoutArray.Length; i++)
            {
                if (layoutArray[i].ClassHandle == classHandle)
                {
                    return i;
                }
            }
        }
        else if (_objLayoutMap.TryGetValue(unchecked((nint)(classHandle)), out var index))
        {
            return index;
        }
        return AddObjLayout(compiler, ClassLayout.Create(compiler, classHandle));
    }
}
