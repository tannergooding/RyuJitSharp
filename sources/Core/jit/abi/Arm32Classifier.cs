// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
namespace RyuJitSharp;

public ref struct Arm32Classifier
{
    private static FatalJitException NotImplemented()
    {
        NYI("ARM32 ABI argument classification");
        return new FatalJitException(CORJIT_IMPLLIMITATION, "ARM32 ABI argument classification is not ported.");
    }

    private static int GetUnimplementedStackSize()
    {
        throw NotImplemented();
    }

    public Arm32Classifier(in ClassifierInfo info)
    {
        throw NotImplemented();
    }

    public readonly int StackSize => GetUnimplementedStackSize();

    public readonly unsafe AbiPassingInformation Classify(
        Compiler comp, var_types type, ClassLayout? structLayout, WellKnownArg wellKnownParam)
    {
        throw NotImplemented();
    }
}
#endif
