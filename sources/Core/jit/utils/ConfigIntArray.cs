// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
using System;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public struct ConfigIntArray
{
    private int[]? _values;

    public unsafe void EnsureInit(byte* str)
    {
        if (_values is null)
        {
            if (str is null)
            {
                throw new FatalJitException("ConfigIntArray requires a configuration string.");
            }

            Init(MemoryMarshal.CreateReadOnlySpanFromNullTerminated(str));
        }
    }

    public readonly ReadOnlySpan<int> GetData() => _values;

    public readonly uint GetLength() => (uint)(_values?.Length ?? 0);

    public readonly void Dump()
    {
        if (_values is null)
        {
            jitprintf("<uninitialized config int array>\n");
            return;
        }

        if (_values.Length == 0)
        {
            jitprintf("<empty config int array>\n");
            return;
        }

        for (var i = 0; i < _values.Length; i++)
        {
            jitprintf($"{(i == 0 ? "" : ", ")}{_values[i]}");
        }
    }

    private void Init(ReadOnlySpan<byte> str)
    {
        var count = 0;
        var i = 0;
        while (i < str.Length)
        {
            if (str[i] == '-' || (str[i] >= '0' && str[i] <= '9'))
            {
                if (str[i] == '-')
                {
                    i++;
                }

                while (i < str.Length && str[i] >= '0' && str[i] <= '9')
                {
                    i++;
                }

                count++;
            }
            else
            {
                i++;
            }
        }

        var values = new int[count];
        var next = 0;
        var negative = false;
        i = 0;
        while (i < str.Length)
        {
            if (str[i] == '-' || (str[i] >= '0' && str[i] <= '9'))
            {
                if (str[i] == '-')
                {
                    negative = true;
                    i++;
                }

                var value = 0;
                while (i < str.Length && str[i] >= '0' && str[i] <= '9')
                {
                    value = unchecked(value * 10 + str[i] - '0');
                    i++;
                }

                // Native ConfigIntArray retains the negative flag across values.
                values[next++] = negative ? unchecked(-value) : value;
            }
            else
            {
                i++;
            }
        }

        _values = values;
    }
}
#endif
