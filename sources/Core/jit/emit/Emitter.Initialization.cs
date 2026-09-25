// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
#if DEBUG
    private VARSET_TP debugPrevGCrefVars = [];

    private VARSET_TP debugThisGCrefVars = [];

    private GCInfo.regPtrDsc? debugPrevRegPtrDsc;

    private regMaskTP debugPrevGCrefRegs;

    private regMaskTP debugPrevByrefRegs;
#endif

    public void Init()
    {
        var compiler = _compiler
            ?? throw new FatalJitException("emitBegCG must precede emitter initialization.");

        VarSetOps.AssignNoCopy(compiler, ref emitPrevGCrefVars, VarSetOps.MakeEmpty(compiler));
        VarSetOps.AssignNoCopy(compiler, ref emitInitGCrefVars, VarSetOps.MakeEmpty(compiler));
        VarSetOps.AssignNoCopy(compiler, ref emitThisGCrefVars, VarSetOps.MakeEmpty(compiler));
#if DEBUG
        VarSetOps.AssignNoCopy(compiler, ref debugPrevGCrefVars, VarSetOps.MakeEmpty(compiler));
        VarSetOps.AssignNoCopy(compiler, ref debugThisGCrefVars, VarSetOps.MakeEmpty(compiler));
        debugPrevRegPtrDsc = null;
        debugPrevGCrefRegs = RBM_NONE;
        debugPrevByrefRegs = RBM_NONE;
#endif
        emitCurIG = null;
    }
}
