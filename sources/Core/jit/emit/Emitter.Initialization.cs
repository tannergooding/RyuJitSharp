// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Emitter
{
    private ref VARSET_TP debugPrevGCrefVars
    {
        get
        {
            var debugData = GetDebugData();
            return ref debugData.debugPrevGCrefVars;
        }
    }

    private ref VARSET_TP debugThisGCrefVars
    {
        get
        {
            var debugData = GetDebugData();
            return ref debugData.debugThisGCrefVars;
        }
    }

    private ref GCInfo.regPtrDsc? debugPrevRegPtrDsc
    {
        get
        {
            var debugData = GetDebugData();
            return ref debugData.debugPrevRegPtrDsc;
        }
    }

    private ref regMaskTP debugPrevGCrefRegs
    {
        get
        {
            var debugData = GetDebugData();
            return ref debugData.debugPrevGCrefRegs;
        }
    }

    private ref regMaskTP debugPrevByrefRegs
    {
        get
        {
            var debugData = GetDebugData();
            return ref debugData.debugPrevByrefRegs;
        }
    }

    private Compiler.DebugData.EmitterDebugData GetDebugData()
    {
        if (!Configuration.DEBUG)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.EmitterGCDiagnosticStateIsOnlyAvailableInDebugBuilds);
            return null!;
        }

        return Compiler.DebugData.Get(this);
    }

    public void Init()
    {
        var compiler = _compiler;
        if (compiler is null)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.EmitterInitializationBeforeCodegenStart);
            return;
        }

        VarSetOps.AssignNoCopy(compiler, ref emitPrevGCrefVars, VarSetOps.MakeEmpty(compiler));
        VarSetOps.AssignNoCopy(compiler, ref emitInitGCrefVars, VarSetOps.MakeEmpty(compiler));
        VarSetOps.AssignNoCopy(compiler, ref emitThisGCrefVars, VarSetOps.MakeEmpty(compiler));
        if (Configuration.DEBUG)
        {
            var debugData = GetDebugData();
            VarSetOps.AssignNoCopy(compiler, ref debugData.debugPrevGCrefVars, VarSetOps.MakeEmpty(compiler));
            VarSetOps.AssignNoCopy(compiler, ref debugData.debugThisGCrefVars, VarSetOps.MakeEmpty(compiler));
            debugData.debugPrevRegPtrDsc = null;
            debugData.debugPrevGCrefRegs = RBM_NONE;
            debugData.debugPrevByrefRegs = RBM_NONE;
        }
        emitCurIG = null;
    }
}
