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

#if REGMASK_BITS_8
global using regMaskSmall = byte;
#elif REGMASK_BITS_16
global using regMaskSmall = ushort;
#elif REGMASK_BITS_32
global using regMaskSmall = uint;
#elif REGMASK_BITS_64
global using regMaskSmall = ulong;
#else
#error Unsupported REGMASK_BITS size
#endif

#if TARGET_64BIT
global using target_size_t = ulong;

global using target_ssize_t = long;
#else
global using target_size_t = uint;

global using target_ssize_t = int;
#endif
