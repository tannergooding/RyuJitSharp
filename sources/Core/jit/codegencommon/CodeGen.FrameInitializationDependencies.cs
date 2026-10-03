// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
#if TARGET_ARM
    private regMaskTP genPrespilledUnmappedRegs()
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM prespilled unmapped register selection is not ported.");
    }
#endif

#if TARGET_RISCV64
    private bool genInstrWithConstant(instruction ins, emitAttr attr, regNumber reg1,
        regNumber reg2, nint imm, regNumber tmpReg, bool inUnwindRegion = false)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Target frame instruction with constant is not ported.");
    }
#endif
}
