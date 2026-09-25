// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCallPlaceRegArgs(GenTreeCall call)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Call register argument placement requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();

        foreach (var arg in call.Args.LateArgs)
        {
            ref var abiInfo = ref arg.AbiInfo;
            var argNode = arg.LateNode;
            assert(argNode is not null);

            if (abiInfo.HasExactlyOneRegisterSegment)
            {
                var argReg = abiInfo.Segments[0].Register;
                _ = genConsumeReg(argNode);
                inst_Mov(argNode.Type.ActualType, argReg, argNode.RegNum, canSkip: true);

                if (call.IsFastTailCall)
                {
                    // The argument remains live through the epilog rather than being consumed here.
                    _gcInfo.gcMarkRegPtrVal(argReg, argNode.Type);
                }
                continue;
            }

            assert(!abiInfo.HasAnyRegisterSegment);
        }

        // Windows AMD64 varargs duplicate floating-point arguments in the corresponding
        // integer registers after all arguments have reached their ABI registers.
        if (call.Args.IsVarArgs)
        {
            foreach (var arg in call.Args.Args)
            {
                foreach (ref readonly var segment in arg.AbiInfo.Segments)
                {
                    if (segment.IsPassedInRegister && genIsValidFloatReg(segment.Register))
                    {
                        var targetReg = _compiler.getCallArgIntRegister(segment.Register);
                        inst_Mov(TYP_LONG, targetReg, segment.Register, canSkip: false,
                            size: TYP_I_IMPL.EmitActualSize);
                    }
                }
            }
        }
#endif
    }
}
