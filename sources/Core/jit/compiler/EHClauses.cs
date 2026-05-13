// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.InteropServices;

namespace RyuJitSharp;

public ref partial struct EHClauses
{
    private ref EHblkDsc _first;
    private ushort _count;

    public EHClauses(Compiler compiler)
    {
        _first = ref MemoryMarshal.GetArrayDataReference(compiler.compHndBBtab);
        _count = compiler.compHndBBtabCount;
    }

    public EHClauses(Compiler compiler, ushort start)
    {
        _first = ref compiler.ehGetDsc(start);
        _count = (ushort)(compiler.compHndBBtabCount - start);
    }

    public Enumerator GetEnumerator() => new Enumerator(ref _first, _count);
}
