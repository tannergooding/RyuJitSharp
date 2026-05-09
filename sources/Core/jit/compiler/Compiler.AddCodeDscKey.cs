// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics.CodeAnalysis;

namespace RyuJitSharp;

public partial class Compiler
{
    public struct AddCodeDscKey : IEquatable<AddCodeDscKey>
    {
        private SpecialCodeKind acdKind;
        private int acdData;

        /// <summary>construct from kind and block</summary>
        /// <param name="kind">exception kind</param>
        /// <param name="block">block throwing (or potentially throwing) an exception</param>
        /// <param name="compiler"></param>
        public AddCodeDscKey(SpecialCodeKind kind, BasicBlock block, Compiler compiler)
        {
            acdKind = kind;

            if (acdKind is not SCK_FAIL_FAST)
            {
                acdData = compiler.bbThrowIndex(block, out _);
            }
        }

        public AddCodeDscKey(AddCodeDsc add)
        {
            acdKind = add.acdKind;

            if (acdKind is not SCK_FAIL_FAST)
            {
                acdData = add.acdKeyDsg switch {
                    AcdKeyDesignator.KD_NONE => 0,
                    AcdKeyDesignator.KD_TRY => add.acdTryIndex,
                    AcdKeyDesignator.KD_HND => add.acdHndIndex | 0x40000000,
                    AcdKeyDesignator.KD_FLT => add.acdHndIndex | int.MinValue,
                    _ => -1,
                };
            }
        }

        public readonly int Data => acdData;

        public static bool operator ==(AddCodeDscKey left, AddCodeDscKey right) => left.Equals(right);

        public static bool operator !=(AddCodeDscKey left, AddCodeDscKey right) => !left.Equals(right);

        public override readonly int GetHashCode() => (acdData << 3) | (int)(acdKind);

        public override readonly bool Equals([NotNullWhen(true)] object? obj) => (obj is AddCodeDscKey other)
                                                                              && Equals(other);

        public readonly bool Equals(AddCodeDscKey other) => (acdData == other.acdData)
                                                && (acdKind == other.acdKind);
    }
}
