// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections;
using System.Collections.Generic;

namespace RyuJitSharp;

public readonly partial struct CallEarlyArgList : IEnumerable<CallArg>
{
    private readonly CallArg _head;

    public CallEarlyArgList(CallArg head)
    {
        _head = head;
    }

    public Enumerator GetEnumerator() => new Enumerator(_head);

    IEnumerator<CallArg> IEnumerable<CallArg>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
