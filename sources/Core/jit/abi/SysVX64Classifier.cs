// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if UNIX_AMD64_ABI
namespace RyuJitSharp;

public ref struct SysVX64Classifier
{
    private RegisterQueue _intRegs;
    private RegisterQueue _fltRegs;
    private int _stackArgSize;

    public SysVX64Classifier(in ClassifierInfo info)
    {
        _intRegs = new RegisterQueue(IntArgRegs);
        _fltRegs = new RegisterQueue(FltArgRegs);
    }

    public readonly int StackSize => _stackArgSize;

    /// <summary>Classify a parameter for the SysV x64 ABI.</summary>
    public unsafe AbiPassingInformation Classify(Compiler comp, var_types type, ClassLayout? structLayout, WellKnownArg wellKnownParam)
    {
        if (wellKnownParam is WellKnownArg.SecretStubParam)
        {
            return AbiPassingInformation.FromSegment(comp, false,
                AbiPassingSegment.InRegister(REG_SECRET_STUB_PARAM, 0, TARGET_POINTER_SIZE));
        }

        var canEnreg = false;
        SYSTEMV_AMD64_CORINFO_STRUCT_REG_PASSING_DESCRIPTOR structDesc = default;

        if (varTypeIsStruct(type))
        {
            assert(structLayout is not null);
            comp.eeGetSystemVAmd64PassStructInRegisterDescriptor(structLayout.ClassHandle, out structDesc);

            if (structDesc.passedInRegisters)
            {
                var intRegCount = 0;
                var floatRegCount = 0;

                for (var i = 0; i < structDesc.eightByteCount; i++)
                {
                    if (structDesc.IsIntegralSlot(i))
                    {
                        intRegCount++;
                    }
                    else if (structDesc.IsSseSlot(i))
                    {
                        floatRegCount++;
                    }
                    else
                    {
                        assert(false);
                        break;
                    }
                }

                canEnreg = (intRegCount <= _intRegs.Count) && (floatRegCount <= _fltRegs.Count);
            }
        }
        else
        {
            var availableRegs = varTypeUsesFloatArgReg(type) ? _fltRegs.Count : _intRegs.Count;
            canEnreg = availableRegs > 0;
        }

        AbiPassingInformation passingInfo;
        if (canEnreg)
        {
            if (varTypeIsStruct(type))
            {
                passingInfo = new AbiPassingInformation(structDesc.eightByteCount);

                for (var i = 0; i < structDesc.eightByteCount; i++)
                {
                    var reg = structDesc.IsIntegralSlot(i) ? _intRegs.Dequeue() : _fltRegs.Dequeue();
                    passingInfo.Segments[i] = AbiPassingSegment.InRegister(reg,
                        structDesc.eightByteOffsets[i], structDesc.eightByteSizes[i]);
                }
            }
            else
            {
                var reg = varTypeUsesFloatArgReg(type) ? _fltRegs.Dequeue() : _intRegs.Dequeue();
                passingInfo = AbiPassingInformation.FromSegment(comp, false,
                    AbiPassingSegment.InRegister(reg, 0, type.Size));
            }
        }
        else
        {
            assert((_stackArgSize % TARGET_POINTER_SIZE) == 0);
            uint size;
            if (type is TYP_STRUCT)
            {
                assert(structLayout is not null);
                size = structLayout.Size;
            }
            else
            {
                size = (uint)type.Size;
            }

            passingInfo = AbiPassingInformation.FromSegment(comp, false,
                AbiPassingSegment.OnStack(_stackArgSize, 0, unchecked((int)size)));
            _stackArgSize = unchecked(_stackArgSize + (int)roundUp(size, (uint)TARGET_POINTER_SIZE));
        }

        return passingInfo;
    }
}
#endif
