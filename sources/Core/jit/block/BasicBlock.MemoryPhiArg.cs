// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class BasicBlock
{
    // We want to make phi functions for the special implicit var memory.
    // But since this is not a real lclVar, and thus has no local #, we can't use a GenTreePhiArg.
    // Instead, we use this struct.
    public sealed class MemoryPhiArg
    {
        public int _ssaNum;

        /// <summary>Next arg in the list, else null.</summary>
        public MemoryPhiArg? _nextArg;

        public MemoryPhiArg(int ssaNum, MemoryPhiArg? nextArg = null)
        {
            _ssaNum = ssaNum;
            _nextArg = nextArg;
        }

        /// <summary>SSA# for incoming value.</summary>
        public int SsaNum => _ssaNum;
    }
}
