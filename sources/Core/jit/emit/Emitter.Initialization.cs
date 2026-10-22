// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial class Emitter
{
    private sealed class DebugData
    {
        private static ConditionalWeakTable<Emitter, DebugData>? s_instances;

        internal VARSET_TP debugPrevGCrefVars = [];

        internal VARSET_TP debugThisGCrefVars = [];

        internal GCInfo.regPtrDsc? debugPrevRegPtrDsc;

        internal regMaskTP debugPrevGCrefRegs;

        internal regMaskTP debugPrevByrefRegs;

        internal int VarRefOffs;

        internal bool EmitIssuing;

        internal bool CheckAlignment;

        internal static DebugData Get(Emitter emitter)
        {
            var instances = LazyStatic.GetOrCreate(ref s_instances);
            return instances.GetValue(emitter, static _ => new DebugData());
        }
    }

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

    private DebugData GetDebugData()
    {
        if (!Configuration.DEBUG)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.EmitterGCDiagnosticStateIsOnlyAvailableInDebugBuilds);
        }

        return DebugData.Get(this);
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
