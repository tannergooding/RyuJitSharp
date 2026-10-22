// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed class GenTreeIntrinsic : GenTreeOp
{
    private readonly NamedIntrinsic _intrinsicName;

    // Method handle of the method which is treated as an intrinsic.
    private readonly unsafe CORINFO_METHOD_HANDLE _methodHandle;

    // Call target lookup info for method call from a Ready To Run module
    private CORINFO_CONST_LOOKUP _entryPoint;

    public unsafe GenTreeIntrinsic(var_types type, GenTree op1, NamedIntrinsic intrinsicName, CORINFO_METHOD_HANDLE methodHandle)
        : base(GT_INTRINSIC, type, op1, op2: null)
    {
        assert(intrinsicName != NI_Illegal);
        _intrinsicName = intrinsicName;
        _methodHandle = methodHandle;
    }

    public unsafe GenTreeIntrinsic(var_types type, GenTree op1, GenTree op2, NamedIntrinsic intrinsicName, CORINFO_METHOD_HANDLE methodHandle)
        : base(GT_INTRINSIC, type, op1, op2)
    {
        assert(intrinsicName != NI_Illegal);
        _intrinsicName = intrinsicName;
        _methodHandle = methodHandle;
    }

    public CORINFO_CONST_LOOKUP EntryPoint
    {
        get
        {
            if (!Configuration.FEATURE_READYTORUN)
            {
                JitExceptionHelpers.ThrowPlatformNotSupportedException(FatalJitErrorCode.ReadyToRunIntrinsicEntryPointsAreUnsupported);
            }

            return _entryPoint;
        }

        set
        {
            if (!Configuration.FEATURE_READYTORUN)
            {
                JitExceptionHelpers.ThrowPlatformNotSupportedException(FatalJitErrorCode.ReadyToRunIntrinsicEntryPointsAreUnsupported);
            }

            _entryPoint = value;
        }
    }

    public NamedIntrinsic IntrinsicName => _intrinsicName;

    public unsafe CORINFO_METHOD_HANDLE MethodHandle => _methodHandle;
}
