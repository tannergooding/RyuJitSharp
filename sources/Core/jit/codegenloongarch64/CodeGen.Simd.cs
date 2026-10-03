// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64 && FEATURE_SIMD
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public insOpts genGetSimdInsOpt(emitAttr size, var_types elementType)
    {
        NYI("unimplemented on LOONGARCH64 yet");
        return INS_OPTS_NONE;
    }
}
#endif
