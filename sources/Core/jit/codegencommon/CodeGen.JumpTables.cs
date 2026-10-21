// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public uint genEmitJumpTable(GenTree tree, bool relativeAddr)
    {
        if (Configuration.TARGET_WASM)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Jump table emission is not supported on Wasm.");
            return default!;
        }

        assert(_compiler.compCurBB is not null);
        noway_assert(_compiler.compCurBB.Kind == BBJ_SWITCH);
        assert(tree.Oper == GT_JMPTABLE);
        var targets = _compiler.compCurBB.SwitchTargets.Cases;
        var tableBase = Emitter.emitBBTableDataGenBeg((uint)targets.Length, relativeAddr);
        if (Configuration.DEBUG && _compiler.verbose)
        {
            jitprintf($"\n      J_M{_compiler.compMethodID:D3}_DS{tableBase:D2} LABEL   DWORD\n");
        }

        for (var i = 0; i < targets.Length; i++)
        {
            var target = targets[i].DestinationBlock;
            noway_assert(target.HasFlag(BBF_HAS_LABEL));
            if (Configuration.DEBUG && _compiler.verbose)
            {
                jitprintf($"            DD      L_M{_compiler.compMethodID:D3}_{FMT_BB(target.bbNum)}\n");
            }
            Emitter.emitDataGenData((uint)i, target);
        }
        Emitter.emitDataGenEnd();

        return tableBase;
    }
}
