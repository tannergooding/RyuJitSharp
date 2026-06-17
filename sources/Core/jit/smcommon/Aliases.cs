// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

// TODO-Cleanup: The types defined below are mildly confusing: why are there both?
// regMaskSmall is large enough to represent the entire set of registers.
// If regMaskSmall is smaller than a "natural" integer type, regMaskTP is wider, based
// on a belief by the original authors of the JIT that in some situations it is more
// efficient to have the wider representation.  This belief should be tested, and if it
// is false, then we should coalesce these two types into one (the Small width, probably).
// In any case, we believe that is OK to freely cast between these types; no information will
// be lost.

// To conserve memory, we don't want to have more than 256 states.
global using SM_STATE_ID = byte;
