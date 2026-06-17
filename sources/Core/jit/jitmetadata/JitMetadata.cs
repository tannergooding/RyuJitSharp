// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static partial class JitMetadata
{
    public static unsafe void report(Compiler compiler, string name, double data)
        => report(compiler, name, &data, sizeof(double));

    public static unsafe void report(Compiler compiler, string name, int data)
        => report(compiler, name, &data, sizeof(int));

    public static unsafe void report(Compiler compiler, string name, long data)
        => report(compiler, name, &data, sizeof(long));

    public static unsafe void report(Compiler compiler, string name, string data)
    {
        using var utf8Data = new MarshaledUtf8String(data);

        fixed (byte* pUtf8Data = utf8Data)
        {
            report(compiler, name, pUtf8Data, utf8Data.Length);
        }
    }

    /// <summary>Report metadata back to the EE.</summary>
    /// <param name="compiler">Compiler instance</param>
    /// <param name="name">Key name of metadata</param>
    /// <param name="data">Pointer to the value to report back</param>
    /// <param name="length"></param>
    public static unsafe void report(Compiler compiler, string name, void* data, int length)
    {
        using var utf8Name = new MarshaledUtf8String(name);

        fixed (byte* pUtf8Name = utf8Name)
        {
            compiler.info.compCompHnd->reportMetadata(pUtf8Name, data, length);
        }
    }
}
