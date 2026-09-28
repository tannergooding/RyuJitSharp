// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCallPlaceRegArgs(GenTreeCall call)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Call register argument placement requires AMD64.");
#else
#if UNIX_AMD64_ABI
        if (call.Args.IsVarArgs)
        {
            throw new FatalJitException(CORJIT_SKIPPED, "SysV AMD64 varargs are not supported.");
        }
#endif
        Emitter.RequireSupportedInstructionRecording();

        foreach (var arg in call.Args.LateArgs)
        {
            ref var abiInfo = ref arg.AbiInfo;
            var argNode = arg.LateNode;
            assert(argNode is not null);

#if FEATURE_MULTIREG_ARGS
            if (argNode.Oper == GT_FIELD_LIST)
            {
                var use = argNode.AsFieldList().Uses.Head;
                foreach (ref readonly var segment in abiInfo.Segments)
                {
                    if (!segment.IsPassedInRegister)
                    {
                        continue;
                    }

                    assert(use is not null);
                    var putArgReg = use.Node;
                    assert(putArgReg.Oper == GT_PUTARG_REG);
                    _ = genConsumeReg(putArgReg);
                    inst_Mov(putArgReg.Type.ActualType, segment.Register, putArgReg.RegNum, canSkip: true);
                    use = use.Next;

                    if (call.IsFastTailCall)
                    {
                        _gcInfo.gcMarkRegPtrVal(segment.Register, putArgReg.Type);
                    }
                }

                assert(use is null);
                continue;
            }
#endif
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

#if WINDOWS_AMD64_ABI
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
#endif
    }
}
