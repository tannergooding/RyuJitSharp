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
        internal VARSET_TP debugPrevGCrefVars = [];

        internal VARSET_TP debugThisGCrefVars = [];

        internal GCInfo.regPtrDsc? debugPrevRegPtrDsc;

        internal regMaskTP debugPrevGCrefRegs;

        internal regMaskTP debugPrevByrefRegs;

        internal int VarRefOffs;

        internal bool EmitIssuing;

        internal bool CheckAlignment;
    }

    private static class DebugDataStorage
    {
        internal static readonly ConditionalWeakTable<Emitter, DebugData> Table = [];
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
            throw new InvalidOperationException("Emitter GC diagnostic state is only available in Debug builds.");
        }

        return DebugDataStorage.Table.GetValue(this, static _ => new DebugData());
    }

    private DebugData? GetExistingDebugData()
    {
        return DebugDataStorage.Table.TryGetValue(this, out var debugData) ? debugData : null;
    }

    public void Init()
    {
        var compiler = _compiler
            ?? throw new FatalJitException("emitBegCG must precede emitter initialization.");

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
