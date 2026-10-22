// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed class GenTreeFptrVal : GenTree
{
    private readonly unsafe CORINFO_METHOD_HANDLE _fptrMethod;

    private bool _fptrDelegateTarget;

    private CORINFO_CONST_LOOKUP _entryPoint;

    public unsafe GenTreeFptrVal(var_types type, CORINFO_METHOD_HANDLE meth)
        : base (GT_FTN_ADDR, type)
    {
        _fptrMethod = meth;
    }

    public unsafe CORINFO_METHOD_HANDLE FptrMethod => _fptrMethod;

    public bool FptrDelegateTarget
    {
        get
        {
            return _fptrDelegateTarget;
        }

        set
        {
            _fptrDelegateTarget = value;
        }
    }

    public CORINFO_CONST_LOOKUP EntryPoint
    {
        get
        {
            if (!Configuration.FEATURE_READYTORUN)
            {
                JitExceptionHelpers.ThrowPlatformNotSupportedException(FatalJitErrorCode.ReadyToRunFunctionPointerEntryPointsAreUnsupported);
            }

            return _entryPoint;
        }

        set
        {
            if (!Configuration.FEATURE_READYTORUN)
            {
                JitExceptionHelpers.ThrowPlatformNotSupportedException(FatalJitErrorCode.ReadyToRunFunctionPointerEntryPointsAreUnsupported);
            }

            _entryPoint = value;
        }
    }
}
