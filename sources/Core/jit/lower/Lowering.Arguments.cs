// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private void InsertBitCastIfNecessary(ref GenTree argNode, in AbiPassingSegment registerSegment)
    {
        if (varTypeUsesIntReg(argNode.Type) == genIsValidIntReg(registerSegment.Register))
        {
            return;
        }

#if DEBUG
        JITDUMP($"Argument node [{argNode.TreeId:D6}] needs to be passed in {registerSegment.Register.Name}; inserting bitcast\n");
#endif
        var cutRegisterSegment = registerSegment;
        var argNodeSize = argNode.Type.ActualType.Size;
        // ABI padding is not part of the value being reinterpreted.
        if (registerSegment.Size > argNodeSize)
        {
            cutRegisterSegment = AbiPassingSegment.InRegister(registerSegment.Register, registerSegment.Offset, argNodeSize);
        }

        var bitCast = CompilerInstance.gtNewBitCastNode(cutRegisterSegment.GetRegisterType(), argNode);
        BlockRange().InsertAfter(argNode, bitCast);
        argNode = bitCast;
        if (!TryRemoveBitCast(bitCast))
        {
            ContainCheckBitCast(bitCast);
        }
    }

    private void InsertPutArgReg(ref GenTree argNode, in AbiPassingSegment registerSegment)
    {
        assert(registerSegment.IsPassedInRegister);
        InsertBitCastIfNecessary(ref argNode, in registerSegment);

#if HAS_FIXED_REGISTER_SET
        var putArg = new GenTreeUnOp(GT_PUTARG_REG, argNode.Type.ActualType, argNode) {
            RegNum = registerSegment.Register,
        };
        BlockRange().InsertAfter(argNode, putArg);
        argNode = putArg;
#endif
    }
}
