// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public TempDsc getSpillTempDsc(GenTree tree)
    {
        assert((tree.Flags & GTF_SPILLED) != 0);
        var spill = _regSet.rsGetSpillInfo(tree, tree.RegNum, out var previous);
        assert(spill is not null);

        return _regSet.rsGetSpillTempWord(tree.RegNum, spill, previous);
    }

    public void spillReg(var_types type, TempDsc temp, regNumber reg)
    {
        if (Configuration.TARGET_WASM)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.RegisterSpillsAreNotSupportedOnWasm);
            return;
        }

        Emitter.emitIns_S_R(ins_Store(type), type.EmitActualSize, reg, temp.tdTempNum, 0);
    }

    public void reloadReg(var_types type, TempDsc temp, regNumber reg)
    {
        if (Configuration.TARGET_WASM)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.RegisterReloadsAreNotSupportedOnWasm);
            return;
        }

        Emitter.emitIns_R_S(ins_Load(type), type.EmitActualSize, reg, temp.tdTempNum, 0);
    }
}
