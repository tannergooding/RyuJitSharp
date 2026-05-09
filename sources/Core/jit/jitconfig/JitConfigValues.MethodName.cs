// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial struct JitConfigValues
{
    private sealed partial class MethodName
    {
        private MethodName? _next;
        private readonly unsafe byte* _patternStart;
        private readonly unsafe byte* _patternEnd;
        private readonly MethodNameFlags _flags;

        public unsafe MethodName(MethodName? next, byte* patternStart, byte* patternEnd, MethodNameFlags flags)
        {
            _next = next;
            _patternStart = patternStart;
            _patternEnd = patternEnd;
            _flags = flags;
        }

        public MethodName? Next
        {
            get
            {
                return _next;
            }

            set
            {
                _next = value;
            }
        }

        public unsafe ReadOnlySpan<byte> Pattern => new ReadOnlySpan<byte>(_patternStart, (int)(_patternEnd - _patternStart));

        public bool ContainsAssemblyName => (_flags & MethodNameFlags.ContainsAssemblyName) != 0;

        public bool ContainsClassName => (_flags & MethodNameFlags.ContainsClassName) != 0;

        public bool ClassNameContainsInstantiation => (_flags & MethodNameFlags.ClassNameContainsInstantiation) != 0;

        public bool MethodNameContainsInstantiation => (_flags & MethodNameFlags.MethodNameContainsInstantiation) != 0;

        public bool ContainsSignature => (_flags & MethodNameFlags.ContainsSignature) != 0;
    }
}
