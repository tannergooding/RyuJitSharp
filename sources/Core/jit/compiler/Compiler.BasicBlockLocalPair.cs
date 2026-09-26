// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial class Compiler
{
    public readonly struct BasicBlockLocalPair : IEquatable<BasicBlockLocalPair>
    {
        public BasicBlockLocalPair(BasicBlock block, int lclNum)
        {
            Block = block;
            LclNum = lclNum;
        }

        public BasicBlock Block { get; }

        public int LclNum { get; }

        public bool Equals(BasicBlockLocalPair other)
        {
            return ReferenceEquals(Block, other.Block) && (LclNum == other.LclNum);
        }

        public override bool Equals(object? obj)
        {
            return (obj is BasicBlockLocalPair other) && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(RuntimeHelpers.GetHashCode(Block), LclNum);
        }

        public static bool operator ==(BasicBlockLocalPair left, BasicBlockLocalPair right) => left.Equals(right);

        public static bool operator !=(BasicBlockLocalPair left, BasicBlockLocalPair right) => !left.Equals(right);
    }
}
