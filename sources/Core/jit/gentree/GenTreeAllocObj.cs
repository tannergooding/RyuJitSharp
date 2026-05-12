// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed class GenTreeAllocObj : GenTreeUnOp
{
    /// <summary>Value returned by ICorJitInfo::getNewHelper</summary>
    private CorInfoHelpFunc _newHelper;
    private bool _newHelperHasSideEffects;
    private unsafe CORINFO_CLASS_HANDLE _clsHnd;

#if FEATURE_READYTORUN
    private CORINFO_CONST_LOOKUP _entryPoint;
#endif

    public unsafe GenTreeAllocObj(var_types type, GenTree op1, CorInfoHelpFunc newHelper, bool newHelperHasSideEffects, CORINFO_CLASS_HANDLE clsHnd)
        : base(GT_ALLOCOBJ, type, op1)
    {
        _newHelper = newHelper;
        _newHelperHasSideEffects = newHelperHasSideEffects;
        _clsHnd = clsHnd;
    }

    public unsafe CORINFO_CLASS_HANDLE ClsHnd => _clsHnd;

#if FEATURE_READYTORUN
    public CORINFO_CONST_LOOKUP EntryPoint
    {
        get
        {
            return _entryPoint;
        }

        set
        {
            _entryPoint = value;
        }
    }
#endif

    public CorInfoHelpFunc NewHelper => _newHelper;

    public bool NewHelperHasSideEffects => _newHelperHasSideEffects;
}
