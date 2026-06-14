// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if NODEBASH_STATS
using System;

namespace RyuJitSharp;

public partial class GenTree
{
    public struct BashHashDsc
    {
        /// <summary>the hash value (unique for all old->new pairs)</summary>
        public int bhFullHash;

        /// <summary>the same old->new bashings seen so far</summary>
        public int bhCount;

        /// <summary>original gtOper</summary>
        public genTreeOps bhOperOld;

        /// <summary>new gtOper</summary>
        public genTreeOps bhOperNew;
    }
}
#endif
