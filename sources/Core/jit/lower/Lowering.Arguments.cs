// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private unsafe void LowerPutArgStk(GenTreePutArgStk putArgStk)
    {
        if (Configuration.TARGET_XARCH)
        {
            var src = putArgStk.Data;
            var srcIsLocal = src.Oper.IsLocalRead;
            if (src.Oper is GT_FIELD_LIST)
            {
                if (Configuration.TARGET_X86)
                {
                    var fieldList = src.AsFieldList();
                    // Expose the reverse push order to LSRA. Integral fields must be contained or
                    // reg-optional because the call may consume more fields than available registers.
                    assert(fieldList.Uses.IsSorted);
                    fieldList.Uses.Reverse();
                    foreach (var use in fieldList.Uses)
                    {
                        var field = use.Node;
                        assert(field.Type is not TYP_LONG);
                        if (varTypeIsIntegralOrI(field.Type))
                        {
                            if (IsContainableImmed(putArgStk, field) ||
                                (IsContainableMemoryOp(field) && IsSafeToContainMem(putArgStk, field)))
                            {
                                MakeSrcContained(putArgStk, field);
                            }
                            else
                            {
                                field.IsRegOptional = true;
                            }
                        }
                    }
                    putArgStk._kind = GenTreePutArgStk.Kind.Push;
                }

                return;
            }

            if (src.Type is TYP_STRUCT)
            {
                assert((src.Oper is GT_BLK) || srcIsLocal);
                var layout = src.GetLayout(CompilerInstance);
                var regType = layout.RegisterType;
                if (regType is TYP_UNDEF)
                {
                    // A copy helper could kill outgoing arguments that have already been set up.
                    // A local's stack slot can be read through its padding; an arbitrary address cannot.
                    var loadSize = srcIsLocal
                        ? unchecked((uint)(((ulong)layout.Size + (ulong)(Configuration.TARGET_POINTER_SIZE - 1)) &
                            ~(ulong)(Configuration.TARGET_POINTER_SIZE - 1)))
                        : layout.Size;
                    putArgStk.ArgLoadSize = checked((int)loadSize);
                    if (!layout.HasGCPtr)
                    {
                        if (Configuration.TARGET_X86 && (loadSize < Configuration.XMM_REGSIZE_BYTES) &&
                            ((loadSize % Configuration.TARGET_POINTER_SIZE) == 0))
                        {
                            putArgStk._kind = GenTreePutArgStk.Kind.Push;
                        }
                        else if (loadSize <= CompilerInstance.GetUnrollThreshold(Compiler.UnrollKind.Memcpy))
                        {
                            putArgStk._kind = GenTreePutArgStk.Kind.Unroll;
                        }
                        else
                        {
                            putArgStk._kind = GenTreePutArgStk.Kind.RepInstr;
                        }
                    }
                    else
                    {
                        // GC references must be pushed so the emitter updates their stack liveness.
                        putArgStk._kind = Configuration.TARGET_X86
                            ? GenTreePutArgStk.Kind.Push
                            : GenTreePutArgStk.Kind.PartialRepInstr;
                    }

                    if (src.Oper is GT_LCL_VAR)
                    {
                        CompilerInstance.lvaSetVarDoNotEnregister(src.AsLclVar().LclNum, DoNotEnregisterReason.IsStructArg);
                    }
                    MakeSrcContained(putArgStk, src);
                }
                else
                {
                    if (varTypeIsSmall(regType) && srcIsLocal)
                    {
                        assert(TYP_INT.Size <= putArgStk.StackByteSize);
                        regType = TYP_INT;
                    }
                    src.Type = regType;
                    if (src.Oper is GT_BLK)
                    {
                        var indir = new GenTreeIndir(GT_IND, regType, src.AsBlk().Addr, null, src, NodeThreading.LIR) {
                            Flags = src.Flags,
                        };
                        BlockRange().ReplaceNode(src, indir);
                        src = indir;
                        _ = LowerIndir(indir);
                    }
                }
            }

            if (src.Type is TYP_STRUCT)
            {
                return;
            }

            // AMD64 can store zero more cheaply after XOR-zeroing a register; x86 can push zero directly.
            if (IsContainableImmed(putArgStk, src) &&
                (!Configuration.TARGET_AMD64 || !src.IsIntegralConst(0)))
            {
                MakeSrcContained(putArgStk, src);
            }
            else if (Configuration.TARGET_X86 && (src.Type.Size == Configuration.TARGET_POINTER_SIZE))
            {
                TryMakeSrcContainedOrRegOptional(putArgStk, src);
            }
            return;
        }

        if (Configuration.TARGET_ARMARCH)
        {
            var source = putArgStk.Data;
            if (source.Type is TYP_STRUCT)
            {
                MakeSrcContained(putArgStk, source);
                if (source.Oper is GT_LCL_VAR)
                {
                    CompilerInstance.lvaSetVarDoNotEnregister(source.AsLclVar().LclNum, DoNotEnregisterReason.IsStructArg);
                }
            }
            return;
        }

        if (Configuration.TARGET_RISCV64)
        {
            var source = putArgStk.Data;
            if (source.Type is TYP_STRUCT)
            {
                MakeSrcContained(putArgStk, source);
                if (source.Oper is GT_LCL_VAR)
                {
                    CompilerInstance.lvaSetVarDoNotEnregister(source.AsLclVar().LclNum,
                        DoNotEnregisterReason.IsStructArg);
                }
            }
            return;
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
            var source = putArgStk.Data;
            if (source.Type is TYP_STRUCT)
            {
                MakeSrcContained(putArgStk, source);

                if (source.Oper.IsLocalRead)
                {
                    var localNumber = source.AsLclVarCommon().LclNum;
                    var layout = source.GetLayout(CompilerInstance);
                    GenTree localAddress;
                    if (source.Oper is GT_LCL_VAR)
                    {
                        localAddress = CompilerInstance.gtNewLclVarAddrNode(TYP_BYREF, localNumber);
                        CompilerInstance.lvaSetVarDoNotEnregister(localNumber, DoNotEnregisterReason.IsStructArg);
                    }
                    else
                    {
                        localAddress = CompilerInstance.gtNewLclAddrNode(TYP_BYREF, localNumber,
                            source.AsLclFld().LclOffs, layout);
                    }

                    var block = new GenTreeBlk(TYP_STRUCT, localAddress, layout, source, NodeThreading.LIR);
                    BlockRange().ReplaceNode(source, block);
                    BlockRange().InsertBefore(block, localAddress);
                    source = block;
                }

                if ((source.Oper is GT_BLK) && source.AsBlk().Addr.IsLclVarAddr &&
                    IsContainableLclAddr(source.AsBlk().Addr.AsLclFld(), source.AsBlk().Size))
                {
                    MakeSrcContained(source, source.AsBlk().Addr);
                }
            }
            return;
        }

        if (Configuration.TARGET_WASM)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Stack argument lowering is not supported for Wasm.");
            return;
        }

        FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Stack argument lowering is not ported for this target.");
        return;
    }

    private void InsertBitCastIfNecessary(ref GenTree argNode, in AbiPassingSegment registerSegment)
    {
        if (varTypeUsesIntReg(argNode.Type) == genIsValidIntReg(registerSegment.Register))
        {
            return;
        }

        JITDUMP($"Argument node [{argNode.TreeId:D6}] needs to be passed in {registerSegment.Register.Name}; inserting bitcast\n");
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

        if (Configuration.HAS_FIXED_REGISTER_SET)
        {
            var putArg = CompilerInstance.gtNewPutArgReg(argNode.Type.ActualType, argNode, registerSegment.Register);
            BlockRange().InsertAfter(argNode, putArg);
            argNode = putArg;
        }
    }
}
