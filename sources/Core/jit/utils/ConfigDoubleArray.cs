// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public unsafe partial struct ConfigDoubleArray
{
    private double[]? _values;

    public void EnsureInit(byte* str)
    {
        if (_values is null)
        {
            Init(str);
        }
    }

    public readonly ReadOnlySpan<double> GetData() => _values;

    public readonly uint GetLength() => (uint)(_values?.Length ?? 0);

    public readonly void Dump()
    {
        if (_values is null)
        {
            jitprintf("<uninitialized config double array>\n");
            return;
        }

        if (_values.Length == 0)
        {
            jitprintf("<empty config double array>\n");
            return;
        }

        for (var i = 0; i < _values.Length; i++)
        {
            jitprintf($"{(i == 0 ? "" : ",")}{formatFloat(_values[i], "F6")} ");
        }
    }

    private void Init(byte* str)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("ConfigDoubleArray requires a port of the host CRT's strtod.");
        }

        // Use the host CRT to retain strtod's syntax, rounding and NaN encodings.
        // B075: reset errno and require progress instead of hanging or accepting
        // partially initialized data. Do not leak errno changes to the native host.
        var error = GetErrno();
        var previousError = *error;
        try
        {
            double[] values = [];
            for (var pass = 0; pass < 2; pass++)
            {
                var p = str;
                var length = 0;
                while ((p is not null) && (*p != 0))
                {
                    if ((*p == ',') || (*p == ' ') || ((*p >= '\t') && (*p <= '\r')))
                    {
                        p++;
                        continue;
                    }

                    byte* next;
                    *error = 0;
                    var value = Strtod(p, &next);
                    if ((next == p) || (*error != 0))
                    {
                        throw new FormatException("Invalid or out-of-range floating-point JIT configuration value.");
                    }

                    if (pass != 0)
                    {
                        values[length] = value;
                    }
                    length++;
                    p = next;
                }

                if (pass == 0)
                {
                    values = new double[length];
                }
            }
            _values = values;
        }
        finally
        {
            *error = previousError;
        }
    }

    [LibraryImport("ucrtbase.dll", EntryPoint = "strtod")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial double Strtod(byte* str, byte** end);

    [LibraryImport("ucrtbase.dll", EntryPoint = "_errno")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int* GetErrno();
}
#endif
