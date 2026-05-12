// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public ref struct RecursiveGuard : IDisposable
{
    private ref bool _address;

    public RecursiveGuard(ref bool address, bool initialize)
    {
        assert(!address, "Recursive guard violation");
        address = initialize;
        _address = ref address;
    }

    public void Dispose()
    {
        if (!Unsafe.IsNullRef(in _address))
        {
            _address = false;
        }
    }
}
