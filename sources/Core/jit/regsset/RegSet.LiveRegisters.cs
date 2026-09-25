// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct RegSet
{
#if HAS_FIXED_REGISTER_SET
    private regMaskTP _rsMaskVars;

    public readonly regMaskTP GetMaskVars() => _rsMaskVars;

    public void SetMaskVars(regMaskTP newMaskVars)
    {
#if DEBUG
        if (Compiler.verbose)
        {
            jitprintf("\t\t\t\t\t\t\tLive regs: ");
            if (_rsMaskVars == newMaskVars)
            {
                jitprintf("(unchanged) ");
            }
            else
            {
                printRegMask(_rsMaskVars);
                _codeGen.Emitter.emitDispRegSet(_rsMaskVars);
                var deadSet = _rsMaskVars & ~newMaskVars;
                var bornSet = newMaskVars & ~_rsMaskVars;
                if (deadSet.IsNonEmpty)
                {
                    jitprintf(" -");
                    _codeGen.Emitter.emitDispRegSet(deadSet);
                }
                if (bornSet.IsNonEmpty)
                {
                    jitprintf(" +");
                    _codeGen.Emitter.emitDispRegSet(bornSet);
                }
                jitprintf(" => ");
            }
            printRegMask(newMaskVars);
            _codeGen.Emitter.emitDispRegSet(newMaskVars);
            jitprintf("\n");
        }
#endif
        _rsMaskVars = newMaskVars;
    }

    public void AddMaskVars(regMaskTP mask) => SetMaskVars(_rsMaskVars | mask);

    public void RemoveMaskVars(regMaskTP mask) => SetMaskVars(_rsMaskVars & ~mask);

    public void ClearMaskVars() => _rsMaskVars = RBM_NONE;
#endif
}
