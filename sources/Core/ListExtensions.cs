// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace RyuJitSharp;

public static class ListExtensions
{
    extension<T>(List<T> list)
    {
        public T ExpandAndGet(int index)
        {
            if (list.Count <= index)
            {
                var newCount = index + 1;
                _ = list.EnsureCapacity(newCount);
                CollectionsMarshal.SetCount(list, newCount);
            }
            return list[index];
        }

        public void ExpandAndSet(int index, T value)
        {
            if (list.Count <= index)
            {
                var newCount = index + 1;
                _ = list.EnsureCapacity(newCount);
                CollectionsMarshal.SetCount(list, newCount);
            }
            list[index] = value;
        }
    }
}
