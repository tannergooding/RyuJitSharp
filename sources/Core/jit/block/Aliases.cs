// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using unsafe EXPSET_TP = nuint*;

global using unsafe EXPSET_VALARG_TP = nuint*;

global using unsafe EXPSET_VALRET_TP = nuint*;

global using unsafe ASSERT_TP = nuint*;

global using unsafe ASSERT_VALARG_TP = nuint*;

global using unsafe ASSERT_VALRET_TP = nuint*;

// Bitmask describing a set of memory kinds (usable in bitfields)
global using MemoryKindSet = uint;

// A set of blocks.
global using BlkSet = System.Collections.Generic.Dictionary<RyuJitSharp.BasicBlock, bool>;

// A vector of blocks.
global using BlkVector = System.Collections.Generic.List<RyuJitSharp.BasicBlock>;

// A map of block -> set of blocks, can be used as sparse block trees.
global using BlkToBlkSetMap = System.Collections.Generic.Dictionary<RyuJitSharp.BasicBlock, System.Collections.Generic.Dictionary<RyuJitSharp.BasicBlock, bool>>;

// A map of block -> vector of blocks, can be used as sparse block trees.
global using BlkToBlkVectorMap = System.Collections.Generic.Dictionary<RyuJitSharp.BasicBlock, System.Collections.Generic.List<RyuJitSharp.BasicBlock>>;

// Map from Block to Block.  Used for a variety of purposes.
global using BlockToBlockMap = System.Collections.Generic.Dictionary<RyuJitSharp.BasicBlock, RyuJitSharp.BasicBlock>;
