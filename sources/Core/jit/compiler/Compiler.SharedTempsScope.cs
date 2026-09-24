// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.

using System;
using System.Collections.Generic;

#if TARGET_AMD64
using indexType = ulong;
#else
using indexType = uint;
#endif

namespace RyuJitSharp;

public partial class Compiler
{
    internal readonly struct SharedTempsScope : IDisposable
    {
        private readonly Compiler _compiler;
        private readonly Stack<int> _usedTemps;
        private readonly Stack<int>? _previousUsedTemps;

        public SharedTempsScope(Compiler compiler)
        {
            _compiler = compiler;
            _usedTemps = new Stack<int>();
            _previousUsedTemps = compiler.fgUsedSharedTemps;
            compiler.fgUsedSharedTemps = _usedTemps;
        }

        public void Dispose()
        {
            _compiler.fgUsedSharedTemps = _previousUsedTemps;
            foreach (var temp in _usedTemps)
            {
                assert(_compiler.fgAvailableOutgoingArgTemps is not null);
                _compiler.fgAvailableOutgoingArgTemps.setBit((indexType)temp);
            }
        }
    }
}
