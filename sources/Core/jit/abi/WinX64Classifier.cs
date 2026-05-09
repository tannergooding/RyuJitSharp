// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public ref struct WinX64Classifier
{
    private RegisterQueue m_intRegs;
    private RegisterQueue m_fltRegs;
    private int m_stackArgSize = 32;

    public WinX64Classifier(in ClassifierInfo info)
    {
        m_intRegs = new RegisterQueue(IntArgRegs);
        m_fltRegs = new RegisterQueue(FltArgRegs);
    }

    public readonly int StackSize => m_stackArgSize;

    /// <summary>Classify a parameter for the Windows x64 ABI.</summary>
    /// <param name="comp">Compiler instance</param>
    /// <param name="type">The type of the parameter</param>
    /// <param name="structLayout">The layout of the struct. Expected to be non-null if varTypeIsStruct(type) is true.</param>
    /// <param name="wellKnownParam">Well known type of the parameter (if it may affect its ABI classification)</param>
    /// <returns>Classification information for the parameter.</returns>
    public AbiPassingInformation Classify(Compiler comp, var_types type, ClassLayout? structLayout, WellKnownArg wellKnownParam)
    {
        // On windows-x64 ABI all parameters take exactly 1 stack slot (structs
        // that do not fit are passed implicitly by reference). Passing a parameter
        // in an int register also consumes the corresponding float register and
        // vice versa.
        assert(m_intRegs.Count == m_fltRegs.Count);

        var passedByRef = false;
        int typeSize = type.Size;

        if (type is TYP_STRUCT)
        {
            assert(structLayout is not null);
            typeSize = structLayout.Size;
        }

        if ((typeSize > TARGET_POINTER_SIZE) || !int.IsPow2(typeSize))
        {
            passedByRef = true;
            typeSize = TARGET_POINTER_SIZE;
        }

        AbiPassingSegment segment;

        if (m_intRegs.Count > 0)
        {
            var reg = varTypeUsesFloatArgReg(type) ? m_fltRegs.Peek() : m_intRegs.Peek();
            segment = AbiPassingSegment.InRegister(reg, 0, typeSize);

            _ = m_intRegs.Dequeue();
            _ = m_fltRegs.Dequeue();
        }
        else
        {
            segment = AbiPassingSegment.OnStack(m_stackArgSize, 0, typeSize);
            m_stackArgSize += TARGET_POINTER_SIZE;
        }
        return AbiPassingInformation.FromSegment(comp, passedByRef, segment);
    }
}
