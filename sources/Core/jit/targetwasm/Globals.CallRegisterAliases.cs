// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_WASM
namespace RyuJitSharp;

public partial class Globals
{
    public const regNumber REG_R2R_INDIRECT_PARAM = REG_NA;
    public const regNumber REG_PINVOKE_FRAME = REG_NA;
    public const regNumber REG_LNGARG_LO = REG_NA;
    public const regNumber REG_LNGARG_HI = REG_NA;
    public const regNumber REG_VALIDATE_INDIRECT_CALL_ADDR = REG_NA;
    public const regNumber REG_DISPATCH_INDIRECT_CALL_ADDR = REG_NA;
    public const regNumber REG_SWIFT_ERROR = REG_NA;
    public const regNumber REG_SWIFT_SELF = REG_NA;
}
#endif
