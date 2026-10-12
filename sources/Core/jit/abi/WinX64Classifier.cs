// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public ref struct WinX64Classifier
{
    private const int WindowsAmd64PointerSize = 8;

    private RegisterQueue _intRegs;
    private RegisterQueue _fltRegs;
    private int _stackArgSize = 32;

    public WinX64Classifier(in ClassifierInfo info)
    {
        if (!Configuration.TARGET_AMD64 || !Configuration.WINDOWS_AMD64_ABI)
        {
            throw new FatalJitException(CORJIT_SKIPPED, "Windows x64 ABI classification requires Windows AMD64.");
        }

        _intRegs = new RegisterQueue(WindowsAmd64IntArgRegs);
        _fltRegs = new RegisterQueue(WindowsAmd64FloatArgRegs);
    }

    public readonly int StackSize => _stackArgSize;

    /// <summary>Classify a parameter for the Windows x64 ABI.</summary>
    /// <param name="comp">Compiler instance</param>
    /// <param name="type">The type of the parameter</param>
    /// <param name="structLayout">The layout of the struct. Expected to be non-null if varTypeIsStruct(type) is true.</param>
    /// <param name="wellKnownParam">Well known type of the parameter (if it may affect its ABI classification)</param>
    /// <returns>Classification information for the parameter.</returns>
    public AbiPassingInformation Classify(Compiler comp, var_types type, ClassLayout? structLayout, WellKnownArg wellKnownParam)
    {
        if (wellKnownParam is WellKnownArg.SecretStubParam)
        {
            return AbiPassingInformation.FromSegment(comp, false,
                AbiPassingSegment.InRegister((regNumber)(uint)WindowsAmd64Register.R10, 0, WindowsAmd64PointerSize));
        }

        // On windows-x64 ABI all parameters take exactly 1 stack slot (structs
        // that do not fit are passed implicitly by reference). Passing a parameter
        // in an int register also consumes the corresponding float register and
        // vice versa.
        assert(_intRegs.Count == _fltRegs.Count);

        var passedByRef = false;
        var typeSize = (uint)type.Size;

        if (type is TYP_STRUCT)
        {
            assert(structLayout is not null);
            typeSize = structLayout.Size;
        }

        if ((typeSize > WindowsAmd64PointerSize) || !uint.IsPow2(typeSize))
        {
            passedByRef = true;
            typeSize = WindowsAmd64PointerSize;
        }

        AbiPassingSegment segment;

        if (_intRegs.Count > 0)
        {
            var reg = varTypeUsesFloatArgReg(type) ? _fltRegs.Peek() : _intRegs.Peek();
            segment = AbiPassingSegment.InRegister(reg, 0, (int)typeSize);

            _ = _intRegs.Dequeue();
            _ = _fltRegs.Dequeue();
        }
        else
        {
            segment = AbiPassingSegment.OnStack(_stackArgSize, 0, (int)typeSize);
            _stackArgSize += WindowsAmd64PointerSize;
        }
        return AbiPassingInformation.FromSegment(comp, passedByRef, segment);
    }

    private static ReadOnlySpan<regNumber> WindowsAmd64IntArgRegs =>
    [
        (regNumber)(uint)WindowsAmd64Register.Rcx,
        (regNumber)(uint)WindowsAmd64Register.Rdx,
        (regNumber)(uint)WindowsAmd64Register.R8,
        (regNumber)(uint)WindowsAmd64Register.R9,
    ];

    private static ReadOnlySpan<regNumber> WindowsAmd64FloatArgRegs =>
    [
        (regNumber)(uint)WindowsAmd64Register.Xmm0,
        (regNumber)(uint)WindowsAmd64Register.Xmm1,
        (regNumber)(uint)WindowsAmd64Register.Xmm2,
        (regNumber)(uint)WindowsAmd64Register.Xmm3,
    ];
}
